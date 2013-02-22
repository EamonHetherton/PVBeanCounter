/*
* Copyright (c) 2010 Dennis Mackay-Fisher
*
* This file is part of PV Scheduler
* 
* PV Scheduler is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PV Scheduler is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using GenericConnector;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading;
using System.Net;
using System.IO;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using Ionic.Zip;
using DeviceDataRecorders;
using PVBCInterfaces;
using Device;

namespace PVInverterManagement
{
    public class SMA_WebBoxInverterManager : InverterManager
    {
        const String NewZipFilePattern = "????-??-??_??????.zip";
        const String NewXmlFilePattern = "????-??-??_??????.xml";
        const String OldZipFilePattern = "Mean.????????_??????.xml.zip";
        const String OldXmlFilePattern = "Mean.????????_??????.xml";

        private String WebBoxFtpUrl;
        private String WebBoxUserName;
        private String WebBoxPassword;
        private Double WebBoxFtpLimit;
        private String WebBoxFtpBasePath;
        private String WebBoxDir;
        private String WebBoxPushDir;
        private bool UseFtpPush;
        private bool UseNewFormat;
        
        private DateTime LastTimeToday = DateTime.Today;
        private DateTime TodayFileListDate;
        private List<String> TodayFileList;

        private DateTime LastDownloadTime;

        List<DateTime> UpdateDates;

        public override String InverterManagerType
        {
            get
            {
                return "SMA WebBox";
            }
        }

        public override String ThreadName { get { return "SMA_WebBoxInverterManager"; } }

        public SMA_WebBoxInverterManager(GenThreadManager genThreadManager, int imid, InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager)
        {
            UseFtpPush = imSettings.WebBoxUsePush;
            WebBoxPushDir = imSettings.WebBoxPushDirectory;
            WebBoxFtpUrl = imSettings.WebBoxFtpUrl;
            WebBoxUserName = imSettings.WebBoxUserName;
            WebBoxPassword = imSettings.WebBoxPassword;
            WebBoxFtpBasePath = imSettings.WebBoxFtpBasePath;
            WebBoxDir = Path.Combine(GlobalSettings.ApplicationSettings.DefaultDirectory, "WebBox_" + imSettings.InstanceNo.ToString());
            UseNewFormat = (imSettings.WebBoxVersion > 1);
            if (imSettings.WebBoxFtpLimit == null)
                WebBoxFtpLimit = 600.0;
            else
                WebBoxFtpLimit = imSettings.WebBoxFtpLimit.Value;
            LastDownloadTime = DateTime.Now - TimeSpan.FromMilliseconds(WebBoxFtpLimit);

            UpdateDates = new List<DateTime>();

            TodayFileListDate = DateTime.Today;
            TodayFileList = new List<String>();
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
                LogMessage("ListFilesOnServer", "Exception: " + e.Message, LogEntryType.ErrorMessage);
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

        private EnergyReadingSet LocateOrCreateReadingSet(String model, String serialNo, List<EnergyReadingSet> readingSets)
        {
            foreach (EnergyReadingSet rs in readingSets)
                if (rs.Device.Model == model && rs.Device.SerialNo == serialNo)
                    return rs;

            PseudoDevice inverter = new PseudoDevice(this);
            inverter.Make = "SMA";
            inverter.Model = model;
            inverter.SerialNo = serialNo;
            EnergyReadingSet newReadingSet = new EnergyReadingSet(inverter, 300);
            readingSets.Add(newReadingSet);
            return newReadingSet;
        }

        private EnergyReading LocateOrCreateReading(String model, String serialNo, DateTime readingTime, int duration, EnergyReadingSet readingSet)
        {
            foreach (EnergyReading reading in readingSet.Readings)
                if (reading.OutputTime == readingTime)
                    return reading;

            EnergyReading newReading = new EnergyReading(0, readingTime, 0, duration, false);
            readingSet.Readings.Add(newReading);
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
                if (pos >=0)
                    return false;

                return true;
            }
            catch (Exception e)
            {
                LogMessage("ExtractModelSerialKeyType", "Bad keyType format: " + txt + " - Exception: " + e.Message, LogEntryType.ErrorMessage); 
                return false;
            }
        }

        private void CreateOrUpdateReading(List<EnergyReadingSet> readingSets, String model, String serialNo, 
            DateTime timeStamp, int duration, Int32? minPower, Int32? maxPower, Double? energy)
        {
            EnergyReadingSet readingSet = LocateOrCreateReadingSet(model, serialNo, readingSets);
            EnergyReading reading = LocateOrCreateReading(model, serialNo, timeStamp, duration, readingSet);

            if (energy.HasValue)
            {
                reading.KWHTotal = energy.Value;
            }

            if (maxPower.HasValue)
            {
                if (reading.MaxPower.HasValue)
                    reading.MaxPower += maxPower.Value;
                else
                    reading.MaxPower = maxPower.Value;
            }

            if (minPower.HasValue)
            {
                if (reading.MinPower.HasValue)
                    reading.MinPower += minPower.Value;
                else
                    reading.MinPower = minPower.Value;
            }

            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("CreateOrUpdateReading", "Model: " + readingSet.Device.Model + " - Serial: " + readingSet.Device.SerialNo +
                    " - timeStamp: " + reading.OutputTime + " - Duration: " + reading.Seconds +
                    " - Energy: " + reading.KWHTotal +
                    " - maxPower: " + reading.MaxPower + " - minPower: " + reading.MinPower, LogEntryType.Trace);
        }

        private bool GetReadingNewFormat(List<EnergyReadingSet> readingSets, XmlDocument document, DateTime timeStamp )
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
                                CreateOrUpdateReading(readingSets, model, serialNo, timeStamp, duration, null, null, energy);
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
                                CreateOrUpdateReading(readingSets, model, serialNo, timeStamp, duration, minPower, maxPower, null);
                        }
                    }
                }
            }

            return true;
        }

        private bool GetReadingOldFormat(List<EnergyReadingSet> readingSets, XmlDocument document)
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
                                        LogMessage("GetReadingOldFormat", "Bad key format: " + document.InnerXml, LogEntryType.ErrorMessage);
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
                                            LogMessage("GetReadingOldFormat", "Bad TimeStamp format: " + n.InnerXml, LogEntryType.ErrorMessage);                                        
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
                                        CreateOrUpdateReading(readingSets, model, serialNo, timeStamp.Value, duration, null, null, maxEnergy);
                                    else
                                        CreateOrUpdateReading(readingSets, model, serialNo, timeStamp.Value, duration, minPower, maxPower, null);
                                else
                                {
                                    if (GlobalSettings.SystemServices.LogTrace)
                                        LogMessage("GetReadingOldFormat", "No timeStamp: " + document.InnerXml, LogEntryType.Trace);
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
                LogMessage("DateTimeFromNewFileName", "Cannot extract date from filename: '" + newFileName + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return null;
            }
            return time;
        }

        private bool LoadOneXML( String fullFileName, List<EnergyReadingSet> readingSets)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("LoadOneXML", "Parsing file: " + fullFileName, LogEntryType.Trace);

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
                return GetReadingNewFormat(readingSets, document, time.Value);
            }
            else
                return GetReadingOldFormat(readingSets, document);
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
                LogMessage("ExtractZipFile", "Extracting: '" + zipFileName + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                res = false;
            }
            return res;
        }

        private bool Download(Uri uri, String filePath, String fileName)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("Download", "Download file: " + fileName, LogEntryType.Trace);  
 
            Uri fileUri = null;
            bool res = false;

            try
            {
                fileUri = new Uri(uri, filePath);
            }
            catch (Exception e)
            {
                LogMessage("Download", "Creating file Uri: '" + filePath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
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
                LogMessage("Download", "Exception: " + e.Message, LogEntryType.ErrorMessage);
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

        private bool BuildReadingSets_OneZip(String directory, List<EnergyReadingSet> readingSets)
        {
            String pattern = XmlFilePattern;

            foreach (String name in Directory.EnumerateFiles(directory, pattern))
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("BuildReadingSets_OneZip", "File: " + name, LogEntryType.Trace);

                // String unZipFileName = Path.Combine(directory, name);

                bool extracted = false;
                try
                {
                    LoadOneXML(name, readingSets);
                    File.Delete(name);
                    extracted = true;
                }
                catch (Exception e)
                {
                    LogMessage("BuildReadingSets_OneZip", "Parsing XML: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }
                finally
                {
                    try
                    {
                        File.Delete(name);
                    }
                    catch{}
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
            foreach(String name in TodayFileList)
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
                    LogMessage("DownloadThisFile", "File: " + fileName + " - already loaded today", LogEntryType.Trace);
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
                LogMessage("DownloadPullFiles_Day", "Creating day directory: '" + dayFilePath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (!directoryCreated)
                return false;

            try
            {
                dayUri = new Uri(uri, dayUriPath);
            }
            catch (Exception e)
            {
                LogMessage("DownloadPullFiles_Day", "Creating day Uri: '" + dayUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
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
                    LogMessage("DownloadPullFiles_Day", "File: " + name + " - Cannot extract time", LogEntryType.ErrorMessage);
                    return false;
                }

                // avoid downloading files for today at every download cycle
                if (!DownLoadThisFile(name, time.Value))
                    continue;

                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("DownloadPullFiles_Day", "File: " + name, LogEntryType.Trace);

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
                LogMessage("DownloadPullFiles_YearOldFormat", "Creating year Uri: '" + yearUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (yearUri == null)
                return false;

            if (!ListFilesOnServer(yearUri, out directoryList))
                return false;

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("DownloadPullFiles_YearOldFormat", "Date: " + name, LogEntryType.Trace);
                DateTime? date = null;
                try
                {
                    date = DateTime.ParseExact(name, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    LogMessage("DownloadPullFiles_YearOldFormat", "Reading date: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (date == null)
                    continue;

                if (NextFileDate != null && NextFileDate.Value.Date > date.Value)
                    continue;

                if (date.Value > DateTime.Today)
                {
                    LogMessage("DownloadPullFiles_YearOldFormat", "Future date found and ignored: '" + date.Value + "'", LogEntryType.ErrorMessage);
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
                LogMessage("DownloadPullFiles_MonthNewFormat", "Creating month Uri: '" + monthUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (monthUri == null)
                return false;

            if (!ListFilesOnServer(monthUri, out directoryList))
                return false;

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("DownloadPullFiles_MonthNewFormat", "Date: " + name, LogEntryType.Trace);
                DateTime? date = null;

                try
                {
                    date = DateTime.ParseExact(name, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e)
                {
                    LogMessage("DownloadPullFiles_MonthNewFormat", "Reading date: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (date == null)
                    continue;

                if (NextFileDate != null && NextFileDate.Value.Date > date.Value)
                    continue;

                if (date.Value > DateTime.Today)
                {
                    LogMessage("DownloadPullFiles_MonthNewFormat", "Future date found and ignored: '" + date.Value + "'", LogEntryType.ErrorMessage);
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
                LogMessage("DownloadPullFiles_YearNewFormat", "Creating year Uri: '" + yearUriPath + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (yearUri == null)
                return false;

            if (!ListFilesOnServer(yearUri, out directoryList))
                return false;

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("DownloadPullFiles_YearNewFormat", "Month: " + name, LogEntryType.Trace);

                int? month = null;
                try
                {
                    month = int.Parse(name);
                }
                catch (Exception e)
                {
                    LogMessage("DownloadPullFiles_YearNewFormat", "Reading month: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (month == null)
                    continue;

                if (NextFileDate != null && (NextFileDate.Value.Year > year
                    || NextFileDate.Value.Year == year && NextFileDate.Value.Month > month.Value))
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
                    LogMessage("DownloadPullFiles", "Exception creating base Uri: " + e.Message, LogEntryType.ErrorMessage);
                else
                    LogMessage("DownloadPullFiles", "Exception creating Uri data path: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (dataUri == null)
                return false;

            ListFilesOnServer(dataUri, out directoryList);

            foreach (String name in directoryList)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("DownloadPullFiles", "Year: " + name, LogEntryType.Trace);
                int? year = null;
                try
                {
                    year = int.Parse(name);
                }
                catch (Exception e)
                {
                    LogMessage("DownloadPullFiles", "Reading year: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (year == null)
                    continue;

                if (NextFileDate != null && NextFileDate.Value.Year > year.Value)
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
                    LogMessage("TransferPushFiles", "File: " + name, LogEntryType.Trace);
                DateTime? time = null;
                try
                {
                    time = DateTimeFromNewFileName(name);
                }
                catch (Exception e)
                {
                    LogMessage("TransferPushFiles", "Reading file name time: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (time == null)
                    continue;

                if (NextFileDate != null)
                    if (NextFileDate.Value> time.Value)
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
                    LogMessage("TransferPushFiles", "Moving file: '" + name + "' - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }
            }

            return true;
        }

        private void AdjustReadingSets(List<EnergyReadingSet> readingSets)
        {
            foreach (EnergyReadingSet rs in readingSets)
            {
                rs.Readings.Sort(EnergyReading.Compare);
                Double prevKWHTotal = 0.0;
                bool first = true;
                foreach (EnergyReading r in rs.Readings)
                {
                    if (first)
                    {
                        prevKWHTotal = r.KWHTotal;
                        first = false;
                    }
                    r.EnergyDelta = Math.Round(r.KWHTotal - prevKWHTotal, 3);
                    prevKWHTotal = r.KWHTotal;
                }
            }
        }

        private bool ParseOneZipFile(String fileName, List<EnergyReadingSet> readingSets)
        {
            if (!ExtractZipFile(fileName))
                return false;

            if (!BuildReadingSets_OneZip(WebBoxDir, readingSets))
                return false;

            return true;
        }

        private int BuildAndUpdateReadingSets(GenConnection connection)
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
                    LogMessage("BuildAndUpdateReadingSets", "Directory: " + dayDirectory, LogEntryType.Trace);

                if (Directory.Exists(dayDirectory))
                {
                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("BuildAndUpdateReadingSets", "Update day: " + dt.ToString("yyyy-MM-dd"), LogEntryType.Trace);
                    List<EnergyReadingSet> readingSets = new List<EnergyReadingSet>();

                    foreach (String name in Directory.EnumerateFiles(dayDirectory, ZipFilePattern))
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            LogMessage("BuildAndUpdateReadingSets", "Update from file: " + name, LogEntryType.Trace);
                        ParseOneZipFile(name, readingSets);
                    }

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("BuildAndUpdateReadingSets", "AdjustReadingSets", LogEntryType.Trace);
                    AdjustReadingSets(readingSets);

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("BuildAndUpdateReadingSets", "UpdateReadingSets", LogEntryType.Trace);
                    foreach (EnergyReadingSet readingSet in readingSets)
                    {
                        count += InverterDataRecorder.HistoryUpdater.UpdateReadingSet(readingSet, connection);
                    }
                }
            }
            return count;
        }

        protected override int ExtractYield()
        {
            GenConnection connection = null;
            String state = "Initial";
            int res = 0;
            
            try
            {
                state = "Acquire new data files";
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("ExtractYield", state, LogEntryType.Trace);
                
                if (UseFtpPush)
                    TransferPushFiles();
                else
                    DownloadPullFiles();

                state = "Build and update reading sets";
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("ExtractYield", state, LogEntryType.Trace);

                connection = GlobalSettings.TheDB.NewConnection();
                res = BuildAndUpdateReadingSets(connection);                

                state = "Find next file date";
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("ExtractYield", state, LogEntryType.Trace);
                DateTime? newNextFileDate = InverterDataRecorder.FindNewStartDate(InverterManagerID);

                if ((NextFileDate != newNextFileDate) && (newNextFileDate != null))
                {
                    state = "Updating next file date";
                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("ExtractYield", state, LogEntryType.Trace);
                    state = "before UpdateNextFileDate";
                    InverterDataRecorder.UpdateNextFileDate(InverterManagerID, newNextFileDate.Value);
                    NextFileDate = newNextFileDate;
                }
            }
            catch (Exception e)
            {
                LogMessage("ExtractYield", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                    connection = null;
                }
            }

            UpdateDates.Clear();

            return res;
        }

        // WebBox provides 5 minute resolution data every 15 minutes
        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(900); } }

        // give webbox time to assemble data and provide some time tolerance
        public override TimeSpan? StartHourOffset { get { return TimeSpan.FromMinutes(InverterManagerSettings.IntervalOffset); } }

        public override void Initialise()
        {
            base.Initialise();

            foreach (DateTime dt in InverterDataRecorder.FindIncompleteDays(InverterManagerID, InverterManagerSettings.ResetFirstFullDay))
                AddDateToList(dt);
        }

    }
}
