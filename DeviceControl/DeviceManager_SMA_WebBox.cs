/*
* Copyright (c) 2010 Dennis Mackay-Fisher
*
* This file is part of PV Bean Counter
* 
* PV Bean Counter is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PV Bean Counter is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Threading;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using DeviceStream;
using GenThreadManagement;
using GenericConnector;
using PVBCInterfaces;
using Device;
using System.Diagnostics;
using Ionic.Zip;
using System.Xml;
using System.Net;


namespace DeviceControl
{
    public class DeviceManager_SMA_WebBox : DeviceManagerTyped<SMA_WebBox_Device>
    {
        public struct DeviceReadingInfo
        {
            public int? index;
            public String SerialNumber;
            public String Model;
            public String Manufacturer;
            public List<SMA_WebBox_Record> LiveRecords;
        }

        private bool ExtractHasRun = false;

        protected List<DeviceReadingInfo> ReadingInfo;

        const String NewZipFilePattern = "????-??-??_??????.zip";
        const String NewXmlFilePattern = "????-??-??_??????.xml";
        const String OldZipFilePattern = "Mean.????????_??????.xml.zip";
        const String OldXmlFilePattern = "Mean.????????_??????.xml";

        private String WebBoxFtpUrl;
        private String WebBoxUserName;
        private String WebBoxPassword;
        private Int32 WebBoxFtpLimit;
        private String WebBoxFtpBasePath;
        private String WebBoxDir;
        private String WebBoxPushDir;
        private bool UseFtpPush;
        private bool UseNewFormat;

        private DateTime LastTimeToday = DateTime.Today;
        private DateTime TodayFileListDate;
        private List<String> TodayFileList;

        private DateTime LastDownloadTime;

        private List<DateTime> UpdateDates;     // date list of dates retrieved from webbox
        private List<DateTime> CandidateDates;  // dates needed to complete PVBC database

        //public override TimeSpan? StartHourOffset { get { return TimeSpan.FromMinutes(2.0); } }

        public override String ThreadName { get { return "DeviceMgr_WebBox"; } }

        // WebBox provides 5 minute resolution data every 15 minutes
        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(900); } }

        // give webbox time to assemble data and provide some time tolerance
        public override TimeSpan? StartHourOffset { get { return TimeSpan.FromMinutes(DeviceManagerSettings.IntervalOffset); } }

        private bool DevicesEnabled = false;

        public DeviceManager_SMA_WebBox(GenThreadManager genThreadManager, DeviceManagerSettings imSettings, IDeviceManagerManager ManagerManager)
            : base(genThreadManager, imSettings, ManagerManager)
        {
            ReadingInfo = new List<DeviceReadingInfo>();
            UseFtpPush = imSettings.WebBoxUsePush;
            WebBoxPushDir = imSettings.WebBoxPushDirectory;
            WebBoxFtpUrl = imSettings.WebBoxFtpUrl;
            WebBoxUserName = imSettings.WebBoxUserName;
            WebBoxPassword = imSettings.WebBoxPassword;
            WebBoxFtpBasePath = imSettings.WebBoxFtpBasePath;
            WebBoxDir = Path.Combine(GlobalSettings.ApplicationSettings.DefaultDirectory, "WebBox_" + imSettings.InstanceNo.ToString());
            UseNewFormat = (imSettings.WebBoxVersion > 1);
            if (imSettings.WebBoxFtpLimit == null)
                WebBoxFtpLimit = 800;
            else
                WebBoxFtpLimit = imSettings.WebBoxFtpLimit.Value;
            LastDownloadTime = DateTime.Now - TimeSpan.FromMilliseconds(WebBoxFtpLimit);

            UpdateDates = new List<DateTime>();
            CandidateDates = new List<DateTime>();

            TodayFileListDate = DateTime.Today;
            TodayFileList = new List<String>();

            foreach (DeviceBase dev in DeviceList)
                DevicesEnabled |= dev.Enabled;
        }

        private String ZipFilePattern { get { return UseNewFormat ? NewZipFilePattern : OldZipFilePattern; } }
        private String XmlFilePattern { get { return UseNewFormat ? NewXmlFilePattern : OldXmlFilePattern; } }

        private void AddDateToList(DateTime updateDate)
        {
            DateTime date = updateDate.Date;
            foreach (DateTime dt in UpdateDates)
                if (dt == date)
                    return;
            UpdateDates.Add(date);
        }

        private bool DateIsCandidate(DateTime? date)
        {
            if (!date.HasValue)
                return false;
            DateTime d = date.Value.Date;
            return CandidateDates.Contains(d);
        }

        private bool MonthIsCandidate(DateTime? date)
        {
            if (!date.HasValue)
                return false;
            DateTime d = date.Value.Date;
            foreach (DateTime cd in CandidateDates)
                if (cd.Year == d.Year && cd.Month == d.Month)
                    return true;
            return false;
        }

        private int MinCandidateYear
        {
            get
            {
                int year = DateTime.Now.Year;
                foreach (DateTime date in CandidateDates)
                    if (date.Year < year)
                        year = date.Year;
                return year;
            }
        }

        private bool ListFilesOnServer(Uri serverUri, out List<String> directoryList)
        {
            // The serverUri should start with the ftp:// scheme.
            if (serverUri.Scheme != Uri.UriSchemeFtp)
            {
                directoryList = null;
                return false;
            }

            List<String> list = new List<String>();
            bool res = true;

            // Get the object used to communicate with the server.
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(serverUri);
            request.Credentials = new NetworkCredential(WebBoxUserName, WebBoxPassword);
            request.Method = WebRequestMethods.Ftp.ListDirectory;

            // Get the ServicePoint object used for this request, and limit it to one connection.

            ServicePoint sp = request.ServicePoint;
            // 1 is not the default - not sure if this is appropriate - DMF
            sp.ConnectionLimit = 1;

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

            // The following streams are used to read the data returned from the server.
            Stream responseStream = null;
            StreamReader readStream = null;
            try
            {
                responseStream = response.GetResponseStream();
                readStream = new StreamReader(responseStream, System.Text.Encoding.UTF8);

                if (readStream != null)
                {
                    while (!readStream.EndOfStream)
                        list.Add(readStream.ReadLine());
                }
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("ListFilesOnServer", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                res = false;
            }
            finally
            {
                if (readStream != null)
                {
                    readStream.Close();
                }
                if (response != null)
                {
                    response.Close();
                }
                directoryList = null;
            }

            directoryList = list;

            return res;
        }

        
        private DeviceReadingInfo LocateOrCreateReadingSet(String model, String serialNo, int? index = null)
        {
            foreach(DeviceReadingInfo info in ReadingInfo)
            
                if (info.Model == model && info.SerialNumber == serialNo)
                    return info;

            DeviceReadingInfo newInfo;
            
            newInfo.Model = model;
            newInfo.SerialNumber = serialNo;
            newInfo.Manufacturer = "SMA";
            newInfo.index = index;
            newInfo.LiveRecords = new List<SMA_WebBox_Record>();
            ReadingInfo.Add(newInfo);
            
            return newInfo;
        }

        private SMA_WebBox_Record LocateOrCreateReading(DeviceReadingInfo info, DateTime readingTime, int duration)
        {
            foreach (SMA_WebBox_Record reading in info.LiveRecords)
                if (reading.TimeStampe == readingTime)
                    return reading;

            SMA_WebBox_Record newReading = new SMA_WebBox_Record(readingTime, duration);
            info.LiveRecords.Add(newReading);
            return newReading;
        }       

        private bool ExtractModelSerialKeyType(string txt, ref string model, ref string serialNo, ref string keyType)
        {
            try
            {
                // extract model and serial number
                int colonPos = txt.LastIndexOf(":");
                if (colonPos == -1)
                    return false;
                keyType = txt.Substring(colonPos + 1);

                String temp = txt.Substring(0, colonPos);
                colonPos = temp.LastIndexOf(":");
                if (colonPos == -1)
                    return false;
                serialNo = temp.Substring(colonPos + 1);

                model = txt.Substring(0, colonPos);

                // Discard all WebBox summary entries - 
                // these are identified by webbox somewhere in the Model value
                int pos = model.IndexOf("webbox", StringComparison.InvariantCultureIgnoreCase);
                if (pos >= 0)
                    return false;

                return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("ExtractModelSerialKeyType", "Bad keyType format: " + txt + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
        }

        private void CreateOrUpdateReading(String model, String serialNo,
            DateTime timeStamp, int duration, Int32? minPower, Int32? maxPower, Double? energy)
        {
            DeviceReadingInfo readingSet = LocateOrCreateReadingSet(model, serialNo);
            SMA_WebBox_Record reading = LocateOrCreateReading(readingSet, timeStamp, duration);

            if (energy.HasValue)
            {
                if (reading.EnergyKwh.HasValue)
                    reading.EnergyKwh += energy.Value;
                else
                    reading.EnergyKwh += energy.Value;

                reading.Power = (int)((energy.Value * 3600000.0) / duration);

                if (reading.MaxPower.HasValue)
                {
                    if (reading.MaxPower.Value < reading.Power)
                        reading.MaxPower = reading.Power;
                    if (maxPower.HasValue && reading.MaxPower.Value < maxPower.Value)
                        reading.MaxPower = maxPower.Value;
                }
                else
                {
                    reading.MaxPower = reading.Power;
                    if (maxPower.HasValue && reading.MaxPower.Value < maxPower.Value)
                        reading.MaxPower = maxPower.Value;
                }

                if (reading.MinPower.HasValue)
                {
                    if (reading.MinPower.Value > reading.Power)
                        reading.MinPower = reading.Power;
                    if (minPower.HasValue && reading.MinPower.Value > minPower.Value)
                        reading.MinPower = minPower.Value;
                }
                else
                {
                    reading.MinPower = reading.Power;
                    if (minPower.HasValue && reading.MinPower.Value > minPower.Value)
                        reading.MinPower = minPower.Value;
                }
            }

            if (GlobalSettings.SystemServices.LogDetailTrace)
                GlobalSettings.LogMessage("CreateOrUpdateReading", "Model: " + readingSet.Model + " - Serial: " + readingSet.SerialNumber +
                    " - timeStamp: " + reading.TimeStampe + " - Duration: " + reading.Seconds +
                    " - Energy: " + reading.EnergyKwh + " - Power: " + reading.Power +
                    " - maxPower: " + reading.MaxPower + " - minPower: " + reading.MinPower, LogEntryType.DetailTrace);
        }

        private bool GetReadingNewFormat(XmlDocument document, DateTime timeStamp)
        {
            String serialNo = "";
            String model = "";
            String keyType = "";
            Int32 minPower = 0;
            Int32 maxPower = 0;
            Double energy = 0.0;
            bool dataFound = false;
            int duration = 300;

            foreach (XmlNode pn in document.ChildNodes)
            {
                if (pn.Name == "WebBox")
                {
                    foreach (XmlNode wbn in pn.ChildNodes)
                    {
                        if (wbn.NodeType == XmlNodeType.Element && wbn.Name == "CurrentPublic")
                        {
                            dataFound = false;
                            foreach (XmlNode n in wbn.ChildNodes)
                            {
                                if (n.Name == "Key")
                                {
                                    if (!ExtractModelSerialKeyType(n.InnerText, ref model, ref serialNo, ref keyType))
                                    {
                                        dataFound = false;
                                        break;
                                    }
                                }
                                else if (keyType == "Metering.TotWhOut")
                                {
                                    if (n.Name == "Mean")
                                    {
                                        dataFound = true;
                                        energy = Double.Parse(n.InnerText);
                                    }
                                    else if (n.Name == "Period")
                                        duration = Int32.Parse(n.InnerText);
                                }
                            }
                            if (dataFound)
                                CreateOrUpdateReading(model, serialNo, timeStamp, duration, null, null, energy);
                        }
                        else if (wbn.NodeType == XmlNodeType.Element && wbn.Name == "MeanPublic")
                        {
                            dataFound = false;
                            foreach (XmlNode n in wbn.ChildNodes)
                            {
                                if (n.Name == "Key")
                                {
                                    if (!ExtractModelSerialKeyType(n.InnerText, ref model, ref serialNo, ref keyType))
                                    {
                                        dataFound = false;
                                        break;
                                    }
                                }
                                else if (keyType == "GridMs.TotW")
                                {
                                    if (n.Name == "Min")
                                    {
                                        dataFound = true;
                                        minPower = (Int32)(Double.Parse(n.InnerText) * 1000.0);
                                    }
                                    else if (n.Name == "Max")
                                    {
                                        dataFound = true;
                                        maxPower = (Int32)(Double.Parse(n.InnerText) * 1000.0);
                                    }
                                    else if (n.Name == "Period")
                                        duration = Int32.Parse(n.InnerText);
                                }
                            }
                            if (dataFound)
                                CreateOrUpdateReading(model, serialNo, timeStamp, duration, minPower, maxPower, null);
                        }
                    }
                }
            }

            return true;
        }

        private bool GetReadingOldFormat(XmlDocument document)
        {
            String serialNo = "";
            String model = "";
            String keyType = "";
            Int32 minPower = 0;
            Int32 maxPower = 0;
            Double minEnergy = 0.0;
            Double maxEnergy = 0.0;
            bool dataFound = false;
            int duration = 300;
            DateTime? timeStamp = null;

            foreach (XmlNode pn in document.ChildNodes)
            {
                if (pn.Name == "WebBox")
                {
                    foreach (XmlNode wbn in pn.ChildNodes)
                        if (wbn.NodeType == XmlNodeType.Element && wbn.Name == "MeanPublic")
                        {
                            dataFound = false;
                            foreach (XmlNode n in wbn.ChildNodes)
                            {
                                if (n.Name == "Key")
                                {
                                    // extract model and serial number
                                    int colonPos = n.InnerText.IndexOf(":");
                                    if (colonPos == -1)
                                    {
                                        GlobalSettings.LogMessage("GetReadingOldFormat", "Bad key format: " + document.InnerXml, LogEntryType.ErrorMessage);
                                        return false;
                                    }
                                    model = n.InnerText.Substring(0, colonPos++);

                                    int serialStart = colonPos;
                                    colonPos = n.InnerText.IndexOf(":", serialStart);

                                    serialNo = n.InnerText.Substring(serialStart, colonPos - serialStart);

                                    keyType = n.InnerText.Substring(colonPos + 1);
                                }
                                else if (keyType == "E-Total")
                                {
                                    if (n.Name == "Min")
                                    {
                                        dataFound = true;
                                        minEnergy = Double.Parse(n.InnerText);
                                    }
                                    else if (n.Name == "Max")
                                    {
                                        dataFound = true;
                                        maxEnergy = Double.Parse(n.InnerText);
                                    }
                                    else if (n.Name == "Period")
                                        duration = Int32.Parse(n.InnerText);
                                    else if (n.Name == "TimeStamp")
                                    {
                                        try
                                        {
                                            timeStamp = DateTime.Parse(n.InnerText);
                                            timeStamp = timeStamp.Value.Date + TimeSpan.FromMinutes((int)timeStamp.Value.TimeOfDay.TotalMinutes);
                                        }
                                        catch (FormatException)
                                        {
                                            GlobalSettings.LogMessage("GetReadingOldFormat", "Bad TimeStamp format: " + n.InnerXml, LogEntryType.ErrorMessage);
                                            return false;
                                        }
                                    }
                                }
                                else if (keyType.Length > 12 && keyType.Substring(0, 12) == "GridMs.W.phs") // Identify the 3 phase elements, A B C
                                {
                                    // add the three phases together
                                    if (n.Name == "Min")
                                    {
                                        dataFound = true;
                                        minPower += (Int32)(Double.Parse(n.InnerText) * 1000.0);
                                    }
                                    else if (n.Name == "Max")
                                    {
                                        dataFound = true;
                                        maxPower += (Int32)(Double.Parse(n.InnerText) * 1000.0);
                                    }
                                }
                            }
                            if (dataFound)
                                if (timeStamp.HasValue)
                                    if (keyType == "E-Total")
                                        CreateOrUpdateReading(model, serialNo, timeStamp.Value, duration, null, null, maxEnergy);
                                    else
                                        CreateOrUpdateReading(model, serialNo, timeStamp.Value, duration, minPower, maxPower, null);
                                else
                                {
                                    if (GlobalSettings.SystemServices.LogTrace)
                                        GlobalSettings.LogMessage("GetReadingOldFormat", "No timeStamp: " + document.InnerXml, LogEntryType.Trace);
                                    return false;
                                }
                        }
                }
            }

            return true;
        }

        private DateTime? DateTimeFromNewFileName(String newFileName)
        {
            String timeStr;
            DateTime time;
            try
            {
                if (UseNewFormat)
                {
                    timeStr = Path.GetFileName(newFileName).Substring(0, 17);
                    time = DateTime.ParseExact(timeStr, "yyyy-MM-dd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    timeStr = Path.GetFileName(newFileName).Substring(5, 15);
                    time = DateTime.ParseExact(timeStr, "yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DateTimeFromNewFileName", "Cannot extract date from filename: '" + newFileName + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return null;
            }
            return time;
        }

        private bool LoadOneXML(String fullFileName)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.LogMessage("LoadOneXML", "Parsing file: " + fullFileName, LogEntryType.Trace);

            XmlReader reader;

            // Create the validating reader and specify DTD validation.
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.DtdProcessing = DtdProcessing.Parse;
            readerSettings.ValidationType = ValidationType.None;

            reader = XmlReader.Create(fullFileName, readerSettings);

            // Pass the validating reader to the XML document.
            // Validation fails due to an undefined attribute, but the 
            // data is still loaded into the document.

            XmlDocument document = new XmlDocument();

            document.Load(reader);
            reader.Close();

            if (UseNewFormat)
            {
                // new format Timestamp element is defective - 12 hour time without AM/PM indication
                // workaround is to take date and time from the xml file name

                DateTime? time = DateTimeFromNewFileName(fullFileName);
                if (time == null)
                    return false;
                return GetReadingNewFormat(document, time.Value);
            }
            else
                return GetReadingOldFormat(document);
        }

        private bool ExtractZipFile(String zipFileName)
        {
            bool res = true;
            try
            {
                using (ZipFile zip = ZipFile.Read(zipFileName))
                {
                    foreach (ZipEntry e in zip)
                    {
                        e.Extract(WebBoxDir, ExtractExistingFileAction.OverwriteSilently);
                    }
                }
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("ExtractZipFile", "Extracting: '" + zipFileName + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                res = false;
            }
            return res;
        }

        private bool Download(Uri uri, String filePath, String fileName)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.LogMessage("Download", "Download file: " + fileName, LogEntryType.Trace);

            Uri fileUri = null;
            bool res = false;

            try
            {
                fileUri = new Uri(uri, filePath);
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("Download", "Creating file Uri: '" + filePath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (fileUri == null)
                return res;

            FtpWebResponse response = null;
            Stream responseStream = null;
            FileStream writeStream = null;

            try
            {
                TimeSpan gap = (DateTime.Now - LastDownloadTime);
                //LogMessage("Download: Gap: " + gap.TotalMilliseconds + " - Min: " + MinDownloadTime.TotalMilliseconds, LogEntryType.Information);
                if (gap.TotalMilliseconds < WebBoxFtpLimit)
                {
                    //LogMessage("Download: Gap 2: " + (MinDownloadTime.TotalMilliseconds - gap.TotalMilliseconds), LogEntryType.Information);
                    Thread.Sleep((int)(WebBoxFtpLimit - gap.TotalMilliseconds));
                }

                LastDownloadTime = DateTime.Now;

                FtpWebRequest reqFTP;
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(fileUri);
                reqFTP.Credentials = new NetworkCredential(WebBoxUserName, WebBoxPassword);
                reqFTP.KeepAlive = true;
                reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;
                reqFTP.UseBinary = true;
                reqFTP.Proxy = null;
                reqFTP.UsePassive = false;
                response = (FtpWebResponse)reqFTP.GetResponse();
                responseStream = response.GetResponseStream();

                writeStream = new FileStream(fileName, FileMode.Create);

                int Length = 2048;
                Byte[] buffer = new Byte[Length];
                int bytesRead = responseStream.Read(buffer, 0, Length);
                while (bytesRead > 0)
                {
                    writeStream.Write(buffer, 0, bytesRead);
                    bytesRead = responseStream.Read(buffer, 0, Length);
                }
                res = true;

            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("Download", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (responseStream != null)
                {
                    responseStream.Close();
                    responseStream.Dispose();
                }
                if (response != null)
                {
                    response.Close();
                }
                if (writeStream != null)
                {
                    writeStream.Close();
                    writeStream.Dispose();
                }
            }
            return res;
        }

        private bool BuildReadingSets_OneZip(String directory)
        {
            String pattern = XmlFilePattern;

            foreach (String name in Directory.EnumerateFiles(directory, pattern))
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("BuildReadingSets_OneZip", "File: " + name, LogEntryType.Trace);

                // String unZipFileName = Path.Combine(directory, name);

                bool extracted = false;
                try
                {
                    LoadOneXML(name);
                    File.Delete(name);
                    extracted = true;
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("BuildReadingSets_OneZip", "Parsing XML: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }
                finally
                {
                    try
                    {
                        File.Delete(name);
                    }
                    catch { }
                }
                if (!extracted)
                    return false;
            }
            return true;
        }

        private bool DownLoadThisFile(String fileName, DateTime time)
        {
            DateTime today = DateTime.Today;
            // only reject files for today that have already been loaded
            if (time.Date != today)
                return true;

            if (today != TodayFileListDate)
            {
                // start of new day, no files have been downloaded today yet
                TodayFileListDate = today;
                TodayFileList = new List<String>();
                return true;
            }

            bool found = false;
            foreach (String name in TodayFileList)
                if (name == fileName)
                {
                    found = true;
                    break;
                }

            if (!found) // not already downloaded
                return true;

            // reduce reprocessing on current day            
            if (LastTimeToday.AddHours(-1.5) > time)
            {
                // do not reprocess today older than 1.5 hours ago
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("DownloadThisFile", "File: " + fileName + " - already loaded today", LogEntryType.Trace);
                return false;
            }

            return true;
        }

        private void RecordFileDownloaded(String fileName, DateTime time)
        {
            AddDateToList(time.Date);
            if (time.Date != TodayFileListDate)
                return;

            TodayFileList.Add(fileName);
            if (time > LastTimeToday)
                LastTimeToday = time;
        }

        private bool DownloadPullFiles_Day(Uri uri, String dayUriPath, String dayFilePath)
        {
            List<String> fileList;
            Uri dayUri = null;

            bool directoryCreated = false;

            try
            {
                System.IO.Directory.CreateDirectory(dayFilePath);
                directoryCreated = true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DownloadPullFiles_Day", "Creating day directory: '" + dayFilePath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (!directoryCreated)
                return false;

            try
            {
                dayUri = new Uri(uri, dayUriPath);
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DownloadPullFiles_Day", "Creating day Uri: '" + dayUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (dayUri == null)
                return false;

            if (!ListFilesOnServer(dayUri, out fileList))
                return false;

            foreach (String name in fileList)
            {
                DateTime? time = DateTimeFromNewFileName(name);
                if (time == null)
                {
                    GlobalSettings.LogMessage("DownloadPullFiles_Day", "File: " + name + " - Cannot extract time", LogEntryType.ErrorMessage);
                    return false;
                }

                // avoid downloading files for today at every download cycle
                if (!DownLoadThisFile(name, time.Value))
                    continue;

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("DownloadPullFiles_Day", "File: " + name, LogEntryType.Trace);

                String zipFileName = dayUriPath + "/" + name;
                String zipDestFileName = Path.Combine(dayFilePath, name);

                if (!Download(uri, zipFileName, zipDestFileName))
                {
                    return false;
                }

                // Ensure file date is on PVBC update list and record files for today already downloaded
                RecordFileDownloaded(name, time.Value);
            }
            return true;
        }

        private bool DownloadPullFiles_YearOldFormat(Uri uri, String yearUriPath, String yearFilePath)
        {
            List<String> directoryList;
            Uri yearUri = null;

            try
            {
                yearUri = new Uri(uri, yearUriPath);
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DownloadPullFiles_YearOldFormat", "Creating year Uri: '" + yearUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (yearUri == null)
                return false;

            if (!ListFilesOnServer(yearUri, out directoryList))
                return false;

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("DownloadPullFiles_YearOldFormat", "Date: " + name, LogEntryType.Trace);
                DateTime? date = null;
                try
                {
                    date = DateTime.ParseExact(name, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("DownloadPullFiles_YearOldFormat", "Reading date: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (!DateIsCandidate(date))
                    continue;

                if (date.Value > DateTime.Today)
                {
                    GlobalSettings.LogMessage("DownloadPullFiles_YearOldFormat", "Future date found and ignored: '" + date.Value + "'", LogEntryType.ErrorMessage);
                    continue;
                }

                if (!DownloadPullFiles_Day(uri, BuildUriPath(yearUriPath, name), Path.Combine(yearFilePath, name)))
                    return false;

                AddDateToList(date.Value);
            }
            return true;
        }

        private bool DownloadPullFiles_MonthNewFormat(Uri uri, String monthUriPath, String monthFilePath)
        {
            List<String> directoryList;
            Uri monthUri = null;

            try
            {
                monthUri = new Uri(uri, monthUriPath);
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DownloadPullFiles_MonthNewFormat", "Creating month Uri: '" + monthUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (monthUri == null)
                return false;

            if (!ListFilesOnServer(monthUri, out directoryList))
                return false;

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("DownloadPullFiles_MonthNewFormat", "Date: " + name, LogEntryType.Trace);
                DateTime? date = null;

                try
                {
                    date = DateTime.ParseExact(name, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("DownloadPullFiles_MonthNewFormat", "Reading date: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (!DateIsCandidate(date))
                    continue;

                if (date.Value > DateTime.Today)
                {
                    GlobalSettings.LogMessage("DownloadPullFiles_MonthNewFormat", "Future date found and ignored: '" + date.Value + "'", LogEntryType.ErrorMessage);
                    continue;
                }

                if (!DownloadPullFiles_Day(uri, BuildUriPath(monthUriPath, name), Path.Combine(monthFilePath, name)))
                    return false;
            }
            return true;
        }

        private bool DownloadPullFiles_YearNewFormat(Uri uri, int year, String yearUriPath, String yearFilePath)
        {
            List<String> directoryList;
            Uri yearUri = null;

            try
            {
                yearUri = new Uri(uri, yearUriPath);
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DownloadPullFiles_YearNewFormat", "Creating year Uri: '" + yearUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (yearUri == null)
                return false;

            if (!ListFilesOnServer(yearUri, out directoryList))
                return false;

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("DownloadPullFiles_YearNewFormat", "Month: " + name, LogEntryType.Trace);

                int? month = null;
                try
                {
                    month = int.Parse(name);
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("DownloadPullFiles_YearNewFormat", "Reading month: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (month == null)
                    continue;

                DateTime date = new DateTime(year, month.Value, 1);

                if (!MonthIsCandidate(date))
                    continue;

                if (!DownloadPullFiles_MonthNewFormat(uri, BuildUriPath(yearUriPath, name), Path.Combine(yearFilePath, name)))
                    return false;
            }
            return true;
        }

        private String BuildUriPath(string lhs, string rhs)
        {
            if (lhs == "")
                return rhs;
            else if (rhs == "")
                return lhs;
            else
                return lhs + "/" + rhs;
        }

        private bool DownloadPullFiles()
        {
            List<String> directoryList;
            Uri uri = null;
            Uri dataUri = null;

            try
            {
                uri = new Uri(WebBoxFtpUrl);
                dataUri = new Uri(uri, WebBoxFtpBasePath);
            }
            catch (Exception e)
            {
                if (uri == null)
                    GlobalSettings.LogMessage("DownloadPullFiles", "Exception creating base Uri: " + e.Message, LogEntryType.ErrorMessage);
                else
                    GlobalSettings.LogMessage("DownloadPullFiles", "Exception creating Uri data path: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (dataUri == null)
                return false;

            ListFilesOnServer(dataUri, out directoryList);

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("DownloadPullFiles", "Year: " + name, LogEntryType.Trace);
                int? year = null;
                try
                {
                    year = int.Parse(name);
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("DownloadPullFiles", "Reading year: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (year == null)
                    continue;

                if (MinCandidateYear > year.Value)
                    continue;
                if (UseNewFormat)
                {
                    if (!DownloadPullFiles_YearNewFormat(uri, year.Value, BuildUriPath(WebBoxFtpBasePath, year.ToString()), Path.Combine(WebBoxDir, name)))
                        return false;
                }
                else
                {
                    if (!DownloadPullFiles_YearOldFormat(uri, BuildUriPath(WebBoxFtpBasePath, year.ToString()), Path.Combine(WebBoxDir, name)))
                        return false;
                }
            }

            return true;
        }

        private bool TransferPushFiles()
        {
            String pattern = ZipFilePattern;

            foreach (String name in Directory.EnumerateFiles(WebBoxPushDir, pattern))
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("TransferPushFiles", "File: " + name, LogEntryType.Trace);
                DateTime? time = null;
                try
                {
                    time = DateTimeFromNewFileName(name);
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("TransferPushFiles", "Reading file name time: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (!DateIsCandidate(time))
                    continue;

                try
                {
                    String zipDestFileName;

                    if (UseNewFormat)
                        zipDestFileName = Path.Combine(WebBoxDir, time.Value.Year.ToString(), time.Value.ToString("MM"), time.Value.ToString("yyyy-MM-dd"));
                    else
                        zipDestFileName = Path.Combine(WebBoxDir, time.Value.Year.ToString(), time.Value.ToString("yyyy-MM-dd"));

                    Directory.CreateDirectory(zipDestFileName);

                    zipDestFileName = Path.Combine(zipDestFileName, time.Value.ToString("yyyy-MM-dd_HHmmss") + ".zip");

                    String zipSourceFileName = Path.Combine(WebBoxPushDir, name);
                    File.Copy(zipSourceFileName, zipDestFileName, true);

                    AddDateToList(time.Value);

                    File.Delete(zipSourceFileName);
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("TransferPushFiles", "Moving file: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }
            }

            return true;
        }

        private void AdjustReadingSets()
        {
            foreach (DeviceReadingInfo rs in ReadingInfo)
            {
                rs.LiveRecords.Sort(SMA_WebBox_Record.Compare);
                /*
                Double prevKWHTotal = 0.0;
                bool first = true;
                foreach (SMA_WebBox_Record r in rs.LiveRecords)
                {
                    if (first)
                    {
                        prevKWHTotal = r.EnergyKwh;
                        first = false;
                    }
                    r.EnergyDelta = Math.Round(r.KWHTotal - prevKWHTotal, 3);
                    prevKWHTotal = r.KWHTotal;
                }
                */
            }
        }

        private bool ParseOneZipFile(String fileName)
        {
            if (!ExtractZipFile(fileName))
                return false;

            if (!BuildReadingSets_OneZip(WebBoxDir))
                return false;

            return true;
        }

        private int BuildAndUpdateReadingSets()
        {
            int count = 0;
            foreach (DateTime dt in UpdateDates)
            {
                String dayDirectory;

                if (UseNewFormat)
                    dayDirectory = Path.Combine(WebBoxDir, dt.Year.ToString(), dt.ToString("MM"), dt.ToString("yyyy-MM-dd"));
                else
                    dayDirectory = Path.Combine(WebBoxDir, dt.Year.ToString(), dt.ToString("yyyy-MM-dd"));

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("BuildAndUpdateReadingSets", "Directory: " + dayDirectory, LogEntryType.Trace);

                if (Directory.Exists(dayDirectory))
                {
                    //DeviceDataRecorders.DeviceDetailPeriod_EnergyMeter mainPeriod = (DeviceDataRecorders.DeviceDetailPeriod_EnergyMeter)device.FindOrCreateFeaturePeriod(FeatureType.YieldAC, 0, dt);
                    if (GlobalSettings.SystemServices.LogTrace)
                        GlobalSettings.LogMessage("BuildAndUpdateReadingSets", "Update day: " + dt.ToString("yyyy-MM-dd"), LogEntryType.Trace);
                    

                    foreach (String name in Directory.EnumerateFiles(dayDirectory, ZipFilePattern))
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            GlobalSettings.LogMessage("BuildAndUpdateReadingSets", "Update from file: " + name, LogEntryType.Trace);
                        ParseOneZipFile(name);
                    }

                    if (GlobalSettings.SystemServices.LogTrace)
                        GlobalSettings.LogMessage("BuildAndUpdateReadingSets", "AdjustReadingSets", LogEntryType.Trace);
                    AdjustReadingSets();

                    if (GlobalSettings.SystemServices.LogTrace)
                        GlobalSettings.LogMessage("BuildAndUpdateReadingSets", "UpdateReadingSets", LogEntryType.Trace);
                    foreach (DeviceReadingInfo info in ReadingInfo)
                    {
                        SMA_WebBox_Device device = FindDevice(info.Model, info.SerialNumber);
                        int i = 0;
                        foreach (SMA_WebBox_Record rec in info.LiveRecords)
                        {
                            i++;
                            if (i == info.LiveRecords.Count && rec.TimeStampe >= DateTime.Now.Add(TimeSpan.FromMinutes(-10.0)))
                                device.ProcessOneLiveReading(rec);
                            else
                                device.ProcessOneHistoryReading(rec);
                        }
                    }
                }
            }
            return count;
        }

        protected void RunExtracts()
        {
            String state = "Initial";
            int res = 0;
           
            try
            {
                CandidateDates = FindEmptyDays(false, ExtractHasRun);
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtracts: " + e.Message, e);
            }

            try
            {
                state = "Acquire new data files";
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("ExtractYield", state, LogEntryType.Trace);

                if (UseFtpPush)
                    TransferPushFiles();
                else
                    DownloadPullFiles();

                state = "Build and update reading sets";
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("ExtractYield", state, LogEntryType.Trace);

                res = BuildAndUpdateReadingSets();

                state = "before FindNewStartDate";
                DateTime? newNextFileDate = FindNewStartDate();
                if ((NextFileDate != newNextFileDate) && (newNextFileDate != null))
                    NextFileDate = newNextFileDate;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("ExtractYield", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            ExtractHasRun = true;
            UpdateDates.Clear();
        }


        public override DateTime NextRunTime(DateTime? currentTime = null)
        {
            return base.NextRunTime_Original(currentTime);
        }

        protected override SMA_WebBox_Device NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            return new SMA_WebBox_Device(this, dmDevice, "", "");
        }

        private SMA_WebBox_Device NewDevice(DeviceManagerDeviceSettings dmDevice, string model, string serialNo)
        {
            return new SMA_WebBox_Device(this, dmDevice, model, serialNo);
        }

        public override bool DoWork()
        {
            String state = "start";
            int res = 0;

            try
            {
                if (!DevicesEnabled || !InvertersRunning)
                    return true;  // if all devices disabled or inverters not running always succeed

                state = "before RunExtracts";
                RunExtracts();

                //state = "before FindNewStartDate";
                //DateTime? newNextFileDate = FindNewStartDate();
                //if ((NextFileDate != newNextFileDate) && (newNextFileDate != null))
                //    NextFileDate = newNextFileDate;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DoWork", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            return res > 0;
        }

        private SMA_WebBox_Device FindDevice(String model, String serialNo)
        {
            foreach (SMA_WebBox_Device device in DeviceList)
            {
                if (device.SerialNo == serialNo && device.Model == model)
                    return device;
            }

            SMA_WebBox_Device item = null;

            foreach (SMA_WebBox_Device device in DeviceList)
            {
                if (device.SerialNo == "")
                {
                    item = device;
                    break;
                }
            }

            if (item == null)
            {
                DeviceManagerDeviceSettings settings = DeviceManagerSettings.AddDevice();
                settings.Manufacturer = "SMA";
                settings.Model = model;
                settings.SerialNo = serialNo;
                settings.Enabled = true;

                item = NewDevice(settings, model, serialNo);

                DeviceList.Add(item);
            }
            else
            {
                item.DeviceManagerDeviceSettings.Manufacturer = "SMA";
                item.DeviceManagerDeviceSettings.Model = model;
                item.DeviceManagerDeviceSettings.SerialNo = serialNo;
                item.Model = model;
                item.SerialNo = serialNo;
                item.Manufacturer = "SMA";
            }
            // must save settings to make new SMA device visible in configuration
            GlobalSettings.ApplicationSettings.SaveSettings();

            return item;
        }


    }
}
