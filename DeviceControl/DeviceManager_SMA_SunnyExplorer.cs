/*
* Copyright (c) 2013 Dennis Mackay-Fisher
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


namespace DeviceControl
{
    public class SMA_SE_ManagerParams : DeviceDataRecorders.DeviceParamsBase
    {
        public SMA_SE_ManagerParams()
        {
            DeviceType = PVSettings.DeviceType.Inverter;
            QueryInterval = 300;
            RecordingInterval = 300;
            EnforceRecordingInterval = true;
        }
    }

    public class DeviceManager_SMA_SunnyExplorer : DeviceManager_PassiveController<SMA_SE_Device, SMA_SE_Record, SMA_SE_Record, SMA_SE_ManagerParams>
    {
        private const int MaxSunnyExplorerRunTime = 3; // minutes
        private SMA_SE_ManagerParams ManagerParams;
        private bool ExtractHasRun = false;

        private static StringBuilder Output = null;
        private static StringBuilder ErrorOutput = null;

        string Password;
        string FileNamePattern;

        private bool DevicesEnabled = false;

        public DeviceManager_SMA_SunnyExplorer(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
            Password = DeviceManagerSettings.SunnyExplorerPassword;
            FileNamePattern = DeviceManagerSettings.SunnyExplorerPlantName + "-????????.csv";

            foreach (DeviceBase dev in DeviceList)
                DevicesEnabled |= dev.Enabled;
        }

        public override DateTime NextRunTime(DateTime? currentTime = null)
        {
            return base.NextRunTime_Original(currentTime);
        }

        public override TimeSpan? StartHourOffset { get { return TimeSpan.FromMinutes(2.0); } }

        public override TimeSpan Interval { get { return TimeSpan.FromMinutes(5.0); } }

        public override String ThreadName { get { return "DeviceMgr_SE"; } }

        protected override SMA_SE_Device NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            return new SMA_SE_Device(this, dmDevice, "", "");
        }

        private SMA_SE_Device NewDevice(DeviceManagerDeviceSettings dmDevice, string model, string serialNo)
        {
            return new SMA_SE_Device(this, dmDevice, model, serialNo);
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
                RunExtracts(ConfigFileName, Password, OutputDirectory);

                state = "before UpdateFromDirectory";
                res = UpdateFromDirectory(OutputDirectory, FileNamePattern, GlobalSettings.ApplicationSettings.BuildFileName(ArchiveDirectory));

                state = "before FindNewStartDate";
                DateTime? newNextFileDate = FindNewStartDate();
                if ((NextFileDate != newNextFileDate) && (newNextFileDate != null))                
                    NextFileDate = newNextFileDate;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DoWork", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            
            return res > 0;
        }

        public bool RunExtracts(String configFile, String password, String exportDirectory)
        {
            List<DateTime> dateList;
            bool extractHasRun = false;

            try
            {
                bool resetFirstFullDay = DeviceManagerSettings.ResetFirstFullDay && !ExtractHasRun;
                dateList = FindEmptyDays(resetFirstFullDay);
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtracts: " + e.Message, e);
            }

            int dateCount = dateList.Count;

            // Add today's date to the end of the list if it is not there already
            if (dateCount > 0)
            {
                // Always reload yesterday at startup - incase yesterday was not fully captured - early shutdown
                // older incomplete days require manual intervention
                if (!ExtractHasRun)
                    if (!dateList.Contains(DateTime.Today.AddDays(-1)))
                        dateList.Add(DateTime.Today.AddDays(-1));

                if (!dateList.Contains(DateTime.Today))
                    dateList.Add(DateTime.Today);
            }
            else
                dateList.Add(DateTime.Today);

            bool res = false;
            DateTime? fromDate = null;
            DateTime toDate = DateTime.Today;

            dateList.Sort();

            try
            {
                // call RunExtract for groups of consecutive days - reduce execution time
                foreach (DateTime dt in dateList)
                {
                    if (fromDate == null)
                        fromDate = dt;
                    else if (dt > (toDate.AddDays(1)))
                    {
                        res = ExtractCSV(configFile, password, exportDirectory, (DateTime)fromDate, toDate);
                        fromDate = dt;
                        extractHasRun |= res;
                    }

                    toDate = dt.Date;
                }

                // one call outstanding at end of loop
                if (fromDate != null)
                {
                    res = ExtractCSV(configFile, password, exportDirectory, (DateTime)fromDate, toDate);
                    extractHasRun |= res;
                }
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("RunExtracts", "exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtracts: error calling ExtractCSV: " + e.Message, e);
            }
            ExtractHasRun |= extractHasRun;
            return extractHasRun;
        }

        protected override void LoadParams()
        {
            ManagerParams = new SMA_SE_ManagerParams();
            ManagerParams.RecordingInterval = DeviceManagerSettings.DBIntervalInt;
        }

        private bool ExtractCSV(String configFile, String password, String exportDirectory, DateTime fromDate, DateTime toDate)
        {
            String executablePath = "";
            String args = "";
            Process exeProcess = null;
            String configFileLocal;
            String passwordLocal;

            if (configFile == null || configFile == "")
                configFileLocal = ApplicationSettings.BuildFileName("SunnyExplorer.sx2", exportDirectory);
            else
                configFileLocal = ApplicationSettings.BuildFileName(configFile, exportDirectory);

            if (password == null || password == "")
            {
                passwordLocal = DeviceManagerSettings.SunnyExplorerPassword; 
                if (passwordLocal == null || passwordLocal == "")
                    passwordLocal = "0000"; // default password for some SMA inverters
            }
            else
                passwordLocal = password;

            try
            {
                executablePath = DeviceManagerSettings.ExecutablePath;
                args = "\"" + configFileLocal + "\" -userlevel user -password \"" + passwordLocal + "\" -exportdir \""
                    + exportDirectory + "\" -exportrange \"" + fromDate.Date.ToString("yyyyMMdd") + "-" + toDate.Date.ToString("yyyyMMdd") + "\" "
                    + "-export energy5min";

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = executablePath;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.Arguments = args;

                Output = new StringBuilder("");
                ErrorOutput = new StringBuilder("");

                exeProcess = new Process();
                exeProcess.OutputDataReceived += OutputHandler;
                exeProcess.ErrorDataReceived += ErrorOutputHandler;

                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                exeProcess.StartInfo = startInfo;

                LogMessage("Starting SunnyExplorer: " + args, LogEntryType.Trace);
                //exeProcess = Process.Start(startInfo);
                exeProcess.Start();
                exeProcess.BeginOutputReadLine();
                exeProcess.BeginErrorReadLine();
                LogMessage("SunnyExplorer Started - waiting", LogEntryType.Trace);
                exeProcess.WaitForExit(MaxSunnyExplorerRunTime * 60000);
                if (!exeProcess.HasExited)
                {
                    try
                    {
                        LogMessage("SunnyExplorer taking too long - over " + MaxSunnyExplorerRunTime + " minutes", LogEntryType.Information);
                        exeProcess.Kill();
                        LogMessage("SunnyExplorer killed", LogEntryType.Trace);
                    }
                    catch (Exception e)
                    {
                        LogMessage("Exception killing SunnyExplorer: " + e.Message, LogEntryType.ErrorMessage);
                    }
                }
                else
                {
                    LogMessage("SunnyExplorer Completed: exit code = " + exeProcess.ExitCode, LogEntryType.Trace);
                }
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                LogMessage("ThreadInterruptedException", LogEntryType.Information);
                try
                {
                    if (!exeProcess.HasExited)
                        exeProcess.Kill();
                }
                catch (Exception)
                {
                }
                throw e;
            }
            catch (Exception e)
            {
                LogMessage("Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.ProcessFailed, "RunExtract: failure executing: " + executablePath + " " + args + " :" + e.Message, e);
            }

            try
            {
                String output = Output.ToString();
                if (output.Contains("CSV-Export: completed"))
                {
                    if (GlobalSettings.SystemServices.LogTrace)
                    {
                        LogMessage("Sunny Explorer - Standard Output: " + output, LogEntryType.Trace);
                        LogMessage("Sunny Explorer - Error Output: " + ErrorOutput.ToString(), LogEntryType.Trace);
                    }
                }
                else
                {
                    LogMessage("Sunny Explorer - CSV Export Failed" + output, LogEntryType.Information);
                    LogMessage("Sunny Explorer - Standard Output: " + output, LogEntryType.Trace);
                    LogMessage("Sunny Explorer - Error Output: " + ErrorOutput.ToString(), LogEntryType.Trace);
                }
            }
            catch (Exception e)
            {
                LogMessage("RunExtract - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtract: failure examining SE output: " + e.Message, e);
            }

            return true;
        }

        private static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            try
            {
                if (!String.IsNullOrEmpty(outLine.Data))
                {
                    // Add the text to the collected output.
                    Output.Append(Environment.NewLine + outLine.Data);
                }
            }
            catch (Exception e)
            {
                try
                {
                    Output.Append(Environment.NewLine + "OutputHandler - Exception: " + e.Message);
                }
                finally
                {
                }
            }
        }

        private static void ErrorOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            try
            {
                if (!String.IsNullOrEmpty(outLine.Data))
                {
                    // Add the text to the collected output.
                    ErrorOutput.Append(Environment.NewLine + outLine.Data);
                }
            }
            catch (Exception e)
            {
                try
                {
                    Output.Append(Environment.NewLine + "ErrorOutputHandler - Exception: " + e.Message);
                }
                finally
                {
                }
            }
        }

        class FileParams
        {
            public char Separator { get; set; }
            public char DecimalPoint { get; set; }
            public List<DeviceIdentity> Inverters { get; set; }
            public String DateFormat { get; set; }
        }

        private Boolean ExtractRecords(string fileName)
        {
            LogMessage("ExtractRecords - Starting File: " + fileName, LogEntryType.Trace);
            bool isOpen = false;
            
            StreamReader streamReader = null;
            try
            {
                streamReader = File.OpenText(fileName);
                isOpen = true;

                FileParams fileParams = new FileParams();

                ExtractSeparator(fileParams, streamReader);
                ExtractSerialNumbers(fileParams, streamReader);
                ExtractModel(fileParams, streamReader);
                ExtractDateFormat(fileParams, streamReader);

                ExtractDataLines(fileParams, streamReader);
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                LogMessage("ExtractRecords - File: " + fileName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
            finally
            {
                if (isOpen)
                    streamReader.Close();
            }

            LogMessage("ExtractRecords - Completed File: " + fileName, LogEntryType.Trace);
            return true;

        }

        private void ExtractSeparator(FileParams fileParams, StreamReader streamReader)
        {
            const String separatorPattern = "sep=";
            const String decimalPattern = "Decimalpoint ";
            String currentLine;

            if ((currentLine = streamReader.ReadLine()) != null)
            {
                int pos = currentLine.IndexOf(separatorPattern);
                if (pos >= 0)
                {
                    fileParams.Separator = currentLine[pos + separatorPattern.Length];
                }
                else
                {
                    LogMessage("ExtractSeparator: separator expected on first line of PV readings file", LogEntryType.ErrorMessage);
                    throw new Exception("ExtractSeparator: separator expected on first line of PV readings file");
                }
            }
            else
            {
                LogMessage("ExtractSeparator: unexpected end of file locating separator", LogEntryType.ErrorMessage);
                throw new Exception("ExtractSeparator: unexpected end of file locating separator");
            }

            if ((currentLine = streamReader.ReadLine()) != null)
            {
                int pos = currentLine.IndexOf(decimalPattern);
                if (pos >= 0)
                {
                    int start = pos + decimalPattern.Length;
                    int pos2 = currentLine.IndexOf('|', start);
                    String val = "";
                    if (pos2 > start)
                        val = currentLine.Substring(start, pos2 - start);

                    if (val == "comma")
                        fileParams.DecimalPoint = ',';
                    else if (val == "dot")
                        fileParams.DecimalPoint = '.';
                    else
                    {
                        LogMessage("ExtractSeparator: unknown decimal pattern - '" + val + "'", LogEntryType.ErrorMessage);
                        throw new Exception("ExtractSeparator: unknown decimal pattern");
                    }
                }
                else
                {
                    LogMessage("ExtractSeparator: decimal pattern expected on second line of PV readings file", LogEntryType.ErrorMessage);
                    throw new Exception("ExtractSeparator: decimal pattern expected on second line of PV readings file");
                }
            }
            else
            {
                LogMessage("ExtractSeparator: unexpected end of file locating separator", LogEntryType.ErrorMessage);
                throw new Exception("ExtractSeparator: unexpected end of file locating separator");
            }

        }

        private void ExtractSerialNumbers(FileParams fileParams, StreamReader streamReader)
        {
            const String serialNoPattern = "SN: ";
            fileParams.Inverters = new List<DeviceIdentity>(1);

            Boolean serialNumberFound = false;
            String currentLine;
            do
            {
                if ((currentLine = streamReader.ReadLine()) != null)
                {
                    int startPos = 0;
                    int posSN;
                    while ((posSN = currentLine.IndexOf(fileParams.Separator, startPos)) >= 0)
                    {
                        DeviceIdentity inverter = new DeviceIdentity();

                        int endSN = currentLine.IndexOf(fileParams.Separator, posSN + 1);
                        if (endSN < 0)
                        {
                            LogMessage("ExtractSerialNumbers: cannot locate Serial Number", LogEntryType.ErrorMessage);
                            throw new Exception("ExtractSerialNumbers: cannot locate Serial Number");
                        }
                        inverter.Make = "SMA"; // sunny explorer only works with SMA inverters
                        inverter.SerialNo = (currentLine.Substring(posSN + 1, endSN - (posSN + 1))).Trim();
                        // if id starts with "SN: " remove the "SN: " prefix
                        // SN: is not a fixed value as initially believed - it is part of the default inverter name and can be removed 
                        // by the inverter owner through configuration
                        if (inverter.SerialNo.StartsWith(serialNoPattern) && (inverter.SerialNo.Length > serialNoPattern.Length))
                            inverter.SerialNo = (inverter.SerialNo.Substring(serialNoPattern.Length)).Trim();

                        fileParams.Inverters.Add(inverter);

                        serialNumberFound = true;
                        startPos = endSN + 1;
                    }
                }
                else
                {
                    LogMessage("ExtractSerialNumbers: unexpected end of file looking for Serial Number", LogEntryType.ErrorMessage);
                    throw new Exception("ExtractSerialNumbers: unexpected end of file looking for Serial Number");
                }
            }
            while (!serialNumberFound);
        }

        private void ExtractModel(FileParams fileParams, StreamReader streamReader)
        {
            String currentLine;
            //positioned on SN line - next line contains model number
            if ((currentLine = streamReader.ReadLine()) != null)
            {
                int startPos = 0;
                int modelCount = 0;
                int posModel;

                while ((posModel = currentLine.IndexOf(fileParams.Separator, startPos)) >= 0 && modelCount < fileParams.Inverters.Count)
                {
                    int endModel = currentLine.IndexOf(fileParams.Separator, posModel + 1);
                    if (endModel < 0)
                    {
                        LogMessage("ExtractModel: separator expected after Model", LogEntryType.ErrorMessage);
                        throw new Exception("ExtractModel: separator expected after Model");
                    }
                    DeviceIdentity identity = fileParams.Inverters[modelCount];
                    identity.Model = (currentLine.Substring(posModel + 1, endModel - (posModel + 1))).Replace(" ", "");
                    fileParams.Inverters[modelCount] = identity;

                    modelCount++;
                    startPos = endModel + 1;
                }
                if (modelCount != fileParams.Inverters.Count)
                {
                    LogMessage("ExtractModel: model missing or model line missing", LogEntryType.ErrorMessage);
                    throw new Exception("ExtractModel: model missing or model line missing");
                }
            }
            else
            {
                LogMessage("ExtractModel: unexpected end of file looking for Model Number", LogEntryType.ErrorMessage);
                throw new Exception("EextractModel: unexpected end of file looking for Model Number");
            }
        }

        private void ExtractDateFormat(FileParams fileParams, StreamReader streamReader)
        {
            Boolean lineFound = false;
            String currentLine;

            do
            {
                if ((currentLine = streamReader.ReadLine()) != null)
                {
                    if (currentLine.IndexOf(fileParams.Separator + "Counter" + fileParams.Separator + "Analog") >= 0)
                    {
                        lineFound = true;
                        if ((currentLine = streamReader.ReadLine()) == null)
                        {
                            LogMessage("ExtractDateFormat: unexpected end of file looking for Date Format 2", LogEntryType.ErrorMessage);
                            throw new Exception("ExtractDateFormat: unexpected end of file looking for Date Format 2");
                        }
                    }
                }
                else
                {
                    LogMessage("ExtractDateFormat: unexpected end of file looking for Date Format 1", LogEntryType.ErrorMessage);
                    throw new Exception("ExtractDateFormat: unexpected end of file looking for Date Format 1");
                }
            }
            while (!lineFound);

            int sepPos = currentLine.IndexOf(fileParams.Separator);
            if (sepPos < 0)
            {
                LogMessage("ExtractDateFormat: separator not found on Date Format line", LogEntryType.ErrorMessage);
                throw new Exception("extractDateFormat: separator not found on Date Format line");
            }
            fileParams.DateFormat = currentLine.Substring(0, sepPos);
        }

        private void ExtractDataLines(FileParams fileParams, StreamReader streamReader)
        {
            char[] separator = new char[1];
            int dataLineColumns = fileParams.Inverters.Count * 2 + 1; // datetime + 2 data columns per inverter
            String[] fields = new String[dataLineColumns];
            int period = 0;
            Int32 duration = 300;  // default to 5 minutes
            DateTime? prevTime = null;
            int inverterIndex = 0;

            separator[0] = fileParams.Separator;

            // Sunny Explorer is not culture sensitive - it always uses dot as the decimal point
            System.Globalization.NumberFormatInfo nfi = new System.Globalization.NumberFormatInfo();
            nfi.CurrencyDecimalSeparator = fileParams.DecimalPoint.ToString();
            nfi.CurrencyGroupSeparator = "";
            nfi.NumberDecimalSeparator = nfi.CurrencyDecimalSeparator;
            nfi.NumberGroupSeparator = nfi.CurrencyGroupSeparator;

            InitialiseDeviceInfo(fileParams.Inverters.Count, true);

            try
            {
                for (inverterIndex = 0; inverterIndex < fileParams.Inverters.Count; inverterIndex++)
                {
                    DeviceIdentity identity = fileParams.Inverters[inverterIndex];
                    ReadingInfo.Manufacturers[inverterIndex] = identity.Make;
                    ReadingInfo.Models[inverterIndex] = identity.Model;
                    ReadingInfo.SerialNumbers[inverterIndex] = identity.SerialNo;
                }
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                LogMessage("ExtractDataLines: Error building reading sets: " + e.Message, LogEntryType.ErrorMessage);
                throw new Exception("ExtractDataLines: Error building reading sets - " + e.Message, e);
            }

            DateTime? readingTime = null;
            String currentLine;

            while ((currentLine = streamReader.ReadLine()) != null)
            {
                if (currentLine.Length < 1)
                    break;

                fields = currentLine.Split(separator, dataLineColumns);

                try
                {
                    readingTime = null;

                    for (inverterIndex = 0; inverterIndex < fileParams.Inverters.Count; inverterIndex++)
                    {
                        if (inverterIndex == 0)
                        {
                            readingTime = DateTime.ParseExact(fields[0], fileParams.DateFormat, CultureInfo.InvariantCulture);
                            if (period > 0) // have both times
                                duration = (Int32)((TimeSpan)(readingTime - prevTime)).TotalSeconds;
                        }

                        SMA_SE_Record reading;

                        if ((inverterIndex * 2 + 3) > fields.Length)
                            reading.Watts = 0;
                        else if (fields[inverterIndex * 2 + 2] == "")
                            reading.Watts = 0;
                        else
                            reading.Watts = (int)(Convert.ToDouble(fields[inverterIndex * 2 + 2], nfi) * 1000.0);

                        reading.TimeStampe = readingTime.Value;
                        prevTime = readingTime;
                        reading.Seconds = duration;

                        if (period == 1 && inverterIndex == 0)
                        {
                            for (int i = 0; i < fileParams.Inverters.Count; i++)
                            {
                                // duration can only be calculated by comparing datetime on two consecutive rows
                                // after second row is examined, update duration in first row
                                SMA_SE_Record first = ReadingInfo.LiveRecords[i][0];
                                first.Seconds = duration;
                                ReadingInfo.LiveRecords[i][0] = first;
                            }
                        }

                        ReadingInfo.LiveRecords[inverterIndex].Add(reading);
                    }
                    period++;
                }
                catch (System.Threading.ThreadInterruptedException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    LogMessage("ExtractDataLines: Error reading csv data - Line: " +
                        currentLine + " - Error: " + e.Message, LogEntryType.ErrorMessage);
                    throw new Exception("ExtractDataLines: format error in data fields - " + e.Message, e);
                }
            }
        }

        private int UpdateFromDirectory(String inDirectory, String filePattern, String moveToDirectory)
        {
            int res = 0;
            DirectoryInfo directoryInInfo = new DirectoryInfo(inDirectory);

            if (!directoryInInfo.Exists)
                throw new PVException(PVExceptionType.DirectoryMissing, "UpdateFromDirectory - Directory: " + inDirectory + " :does not exist");

            DirectoryInfo directoryMoveToInfo = new DirectoryInfo(moveToDirectory);
            if (!directoryMoveToInfo.Exists)
                try
                {
                    directoryMoveToInfo.Create();
                }
                catch (Exception e)
                {
                    throw new PVException(PVExceptionType.CannotCreateDirectory,
                        "UpdateFromDirectory - Error creating directory: " + moveToDirectory + " - Exception: " + e.Message, e);
                }

            foreach (FileInfo fileInfo in directoryInInfo.EnumerateFiles(filePattern))
            {
                bool updated = false;

                try
                {
                    updated = UpdateHistory(fileInfo.FullName);
                }
                catch (Exception e)
                {
                    LogMessage("UpdateFromDirectory - Exception: " + e.Message, LogEntryType.ErrorMessage);
                }

                if (updated)
                {
                    res++;
                    try
                    {
                        String newName = System.IO.Path.Combine(directoryMoveToInfo.FullName, fileInfo.Name);
                        FileInfo moveToFileInfo = new FileInfo(newName);
                        if (moveToFileInfo.Exists)
                            moveToFileInfo.Delete();
                        moveToFileInfo = null;

                        fileInfo.MoveTo(newName);
                    }
                    catch (Exception e)
                    {
                        throw new PVException(PVExceptionType.CannotMoveFile,
                            "UpdateFromDirectory - Error moving file: " + fileInfo.FullName + " :to: " + moveToDirectory + " - Exception: " + e.Message, e);
                    }
                }
            }
            return res;
        }

        private void CheckReadingSet(List<SMA_SE_Record> readingSet)
        {
            /*
            * This detects a specific dataset problem found in some SMA sourced datasets where there is a single missing reading
            * followed by a catchup reading with a high power level. pvoutput can reject this catchup as having too high a power level
            * This code will detect the scenario and replace the single missing reading
            */

            int prevDuration = 0;

            for (int i = 0; i < readingSet.Count; i++)
            {
                SMA_SE_Record reading = readingSet[i];

                if (i > 0)
                {
                    int duration = reading.Seconds;

                    // only act on jumps from 0 to > 1.5KW 
                    // this should prevent removal of zero values caused by dark cloud - a natural jump from 0 to 1.5KW would be very unusual
                    // only act on single missing values where duration is double expected
                    if (i > 1 && duration == (prevDuration * 2) && reading.Watts > 1500)
                    {                        
                        reading.Watts = reading.Watts / 2;
                        reading.Seconds = prevDuration;

                        SMA_SE_Record extraReading = reading;
                        extraReading.TimeStampe = extraReading.TimeStampe.AddSeconds(-prevDuration);

                        readingSet.Insert(i, extraReading);
                        i++;
                    }
                }
                prevDuration = reading.Seconds;
            }
        }

        private Boolean UpdateHistory(String fileName)
        {
            Boolean res;
            String stage = "Starting";
            try
            {
                res = ExtractRecords(fileName);
            }
            catch (Exception e)
            {
                LogMessage("UpdateHistory - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }

            stage = "Loop";

            try
            {
                if (res)
                {
                    for (int i = 0; i < ReadingInfo.LiveRecords.Length; i++)
                        if (ReadingInfo.Enabled[i])
                        {
                            stage = "CheckReadingSet";
                            CheckReadingSet(ReadingInfo.LiveRecords[i]);
                            stage = "FindDevice";
                            SMA_SE_Device device = FindDevice(ReadingInfo.Models[i], ReadingInfo.SerialNumbers[i]);

                            DeviceDataRecorders.DeviceDetailPeriod_EnergyMeter mainPeriod = null;
                            DeviceDataRecorders.DeviceDetailPeriod_EnergyMeter prevPeriod = null;
                            DateTime date = DateTime.MinValue;

                            int end = ReadingInfo.LiveRecords[i].Count - 1;
                            if (end >= 0)
                                try
                                {
                                    stage = "Get Periods";
                                    
                                    // The SE csv file contains a reading at midnight that is stored as the last reading of the previous day
                                    // Mark all possible readings already known - we want to detect readings that are no longer relevant so they can
                                    // be deleted in UpdateDatabase
                                    date = ReadingInfo.LiveRecords[i][0].TimeStampe.Date;
                                    mainPeriod = (DeviceDataRecorders.DeviceDetailPeriod_EnergyMeter)device.FindOrCreateFeaturePeriod(FeatureType.YieldAC, 0, date);
                                    // mark all of target day except the last 5 min - this is supplied in file for following day
                                    mainPeriod.SetAddReadingMatch(false, date.AddMinutes(5.0), date.AddMinutes(60.0 * 24.0 - 5.0));
                                    prevPeriod = (DeviceDataRecorders.DeviceDetailPeriod_EnergyMeter)device.FindOrCreateFeaturePeriod(FeatureType.YieldAC, 0, ReadingInfo.LiveRecords[i][0].TimeStampe.Date.AddDays(-1.0));
                                    // mark last 5 min of previous day
                                    prevPeriod.SetAddReadingMatch(false, date, date);
                                    for (int j = 0; j <= end; j++)
                                    {
                                        stage = "Reading Start";
                                        SMA_SE_Record reading = ReadingInfo.LiveRecords[i][j];
                                        bool isLive;
#if (DEBUG)
                                        isLive = j == end;
#else
                                isLive = j == end && reading.TimeStampe.Date == DateTime.Today;    
#endif
                                        stage = "ProcessOneLiveReading";
                                        if (isLive)
                                            device.ProcessOneLiveReading(reading); // handle latest differently - can emit an event
                                        else
                                            device.ProcessOneHistoryReading(reading);
                                    }
                                    stage = "UpdateDatabase";
                                    device.Days.UpdateDatabase(null, null, true, null);
                                }
                                catch (Exception e)
                                {
                                    LogMessage("UpdateHistory - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                                    return false;
                                }
                                finally
                                {
                                    stage = "Finally";
                                    // ensure marks are reset on exception as lingering values can cause deletions
                                    if (mainPeriod != null)
                                        mainPeriod.SetAddReadingMatch(null, date.AddMinutes(5.0), date.AddMinutes(60.0 * 24.0 - 5.0));
                                    if (prevPeriod != null)
                                        prevPeriod.SetAddReadingMatch(null, date, date);
                                }
                        }
                }

                return true;
            }
            catch (Exception e)
            {
                LogMessage("UpdateHistory - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            return false;
        }

        private SMA_SE_Device FindDevice(String model, String serialNo)
        {
            foreach(SMA_SE_Device device in DeviceList)
            {
                if (device.SerialNo == serialNo && device.Model == model)
                    return device;
            }

            SMA_SE_Device item = null;

            foreach (SMA_SE_Device device in DeviceList)
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

        protected List<DateTime> FindEmptyDays(bool resetFirstFullDay)
        {
            DateTime? startDate = NextFileDate;
            List<DateTime> completeDays;

            if (!resetFirstFullDay)
                completeDays = FindDaysWithValues(startDate);
            else
                completeDays = new List<DateTime>();

            try
            {
                // ensure we have a usable startDate                 
                if (startDate == null)
                    if (completeDays.Count > 0)
                    {
                        // limit history retrieval to configured device history limit
                        startDate = completeDays[0];
                        if (startDate == DateTime.Today.AddDays(1 - DeviceManagerSettings.MaxSMAHistoryDays))
                            startDate = DateTime.Today.AddDays(1 - DeviceManagerSettings.MaxSMAHistoryDays);
                    }
                    else
                        startDate = DateTime.Today;

                int numDays = (1 + (DateTime.Today - startDate.Value).Days);
                List<DateTime> incompleteDays = new List<DateTime>(numDays);

                for (int i = 0; i < numDays; i++)
                {
                    DateTime day = startDate.Value.AddDays(i);
                    if (!completeDays.Contains(day))
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            GlobalSettings.SystemServices.LogMessage("FindEmptyDays", "day: " + day, LogEntryType.Trace);
                        incompleteDays.Add(day);
                    }
                }
                return incompleteDays;
            }
            catch (Exception e)
            {
                throw new Exception("FindEmptyDays: error : " + e.Message, e);
            }
        }

        public List<DateTime> FindDaysWithValues(DateTime? startDate)
        {
            // cannot detect complete days until device list is populated
            if (DeviceList.Count == 0)
                return new List<DateTime>();

            GenConnection connection = null;
            String cmdStr;
            GenCommand cmd;
            try
            {
                connection = GlobalSettings.TheDB.NewConnection();
                if (GlobalSettings.SystemServices.LogTrace && startDate != null)
                    GlobalSettings.SystemServices.LogMessage("FindDaysWithValues", "limit day: " + startDate, LogEntryType.Trace);

                // hack for SQLite - I suspect it does a string compare that results in startDate being excluded from the list                 
                // drop back 1 day for SQLite - the possibility of an extra day in this list does not damage the final result                 
                // (in incomplete days that is)                 
                if (connection.DBType == GenDBType.SQLite && startDate != null)
                {
                    startDate -= TimeSpan.FromDays(1);
                    if (GlobalSettings.SystemServices.LogTrace && startDate != null)
                        GlobalSettings.SystemServices.LogMessage("FindDaysWithValues", "SQLite adjusted limit day: " + startDate, LogEntryType.Trace);
                }

                string serials = "";

                foreach (SMA_SE_Device device in DeviceList)
                {
                    if (serials == "")
                        serials += device.SerialNo;
                    else
                        serials += ", " + device.SerialNo;
                }

                // This implementation treats a day as complete if any inverter under the inverter manager reports a full day                  
                if (startDate == null)
                    cmdStr =
                        "select distinct oh.OutputDay " +
                        "from devicedayoutput_v oh, device d " +
                        "where oh.Device_Id = d.Id " +
                        "and d.SerialNumber in ( @SerialNumbers ) " +
                        "order by oh.OutputDay;";
                else
                    cmdStr =
                        "select distinct oh.OutputDay " +
                        "from devicedayoutput_v oh, device d " +
                        "where oh.OutputDay >= @StartDate " +
                        "and oh.Device_Id = d.Id " +
                        "and d.SerialNumber in ( @SerialNumbers ) " +
                        "order by oh.OutputDay;";

                cmd = new GenCommand(cmdStr, connection);
                if (startDate != null)
                    cmd.AddParameterWithValue("@StartDate", startDate);
                cmd.AddParameterWithValue("@SerialNumbers", serials);
                GenDataReader dataReader = (GenDataReader)cmd.ExecuteReader();
                List<DateTime> dateList = new List<DateTime>(7);
                int cnt = 0;
                bool yesterdayFound = false;
                bool todayFound = false;
                DateTime today = DateTime.Today;
                DateTime yesterday = today.AddDays(-1);
                while (dataReader.Read())
                {
                    DateTime day = dataReader.GetDateTime(0);
                    yesterdayFound |= (day == yesterday);
                    todayFound |= (day == today);
                    if (day < yesterday)
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            GlobalSettings.SystemServices.LogMessage("FindDaysWithValues", "day: " + day, LogEntryType.Trace);
                        dateList.Add(dataReader.GetDateTime(0));
                        cnt++;
                    }
                }
                if (todayFound && yesterdayFound)
                    dateList.Add(yesterday);
                dataReader.Close();
                return dateList;
            }
            catch (Exception e)
            {
                throw new Exception("FindDaysWithValues: error executing query: " + e.Message, e);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        protected DateTime FindNewStartDate()
        {
            List<DateTime> dateList;

            try
            {
                dateList = FindEmptyDays(false);
            }
            catch (Exception e)
            {
                throw new Exception("FindNewStartDate: " + e.Message, e);
            }

            DateTime newStartDate;

            if (dateList.Count > 0)
                newStartDate = dateList[0];
            else
                newStartDate = DateTime.Today.Date;

            return newStartDate;
        }
    }
}
