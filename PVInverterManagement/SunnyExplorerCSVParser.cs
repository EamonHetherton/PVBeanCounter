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
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeviceDataRecorders;
using GenericConnector;
using PVBCInterfaces;
using MackayFisher.Utilities;

namespace PVInverterManagement
{
    class SunnyExplorerCSVParser : ExtractYieldRecords
    {
        public SunnyExplorerCSVParser() 
        {
        }

        public override Int32 NormalDaySize
        {
            get
            {
                return 288;
            }
        }

        private StreamReader StreamReader;
        private String CurrentLine;

        public class FileParams
        {
            public char Separator { get; set; }
            public char DecimalPoint { get; set; }
            public List<DeviceIdentity> Inverters { get; set; }
            public String DateFormat { get; set; }
        }

        public override Boolean ExtractRecords(GenConnection connection, IDeviceManager deviceManager, string fileName, out List<EnergyReadingSet> readings)
        {
            LogMessage("SunnyExplorerCSVParser.ExtractRecords - Starting File: " + fileName, LogEntryType.Trace);
            bool isOpen = false;
            readings = null;
            try
            {
                StreamReader = File.OpenText(fileName);
                isOpen = true;

                FileParams fileParams = ExtractFileParams();

                ExtractDataLines(deviceManager, connection, fileParams, out readings);
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                LogMessage("SunnyExplorerCSVParser.ExtractRecords - File: " + fileName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);                
                return false;
            }
            finally
            {
                if (isOpen)
                    StreamReader.Close();
            }

            LogMessage("SunnyExplorerCSVParser.ExtractRecords - Completed File: " + fileName, LogEntryType.Trace);
            return true;
            
        }

        private FileParams ExtractFileParams()
        {
            FileParams fileParams = new FileParams();

            ExtractSeparator(fileParams);
            ExtractSerialNumbers(fileParams);
            ExtractModel(fileParams);
            ExtractDateFormat(fileParams);

            return fileParams;
        }

        private void ExtractSeparator(FileParams fileParams)
        {
            const String separatorPattern = "sep=";
            const String decimalPattern = "Decimalpoint ";

            if (ReadNextLine())
            {
                int pos = CurrentLine.IndexOf(separatorPattern);
                if (pos >= 0)
                {
                    fileParams.Separator = CurrentLine[pos + separatorPattern.Length];
                }
                else
                {
                    LogMessage("SunnyExplorerCSVParser.ExtractSeparator: separator expected on first line of PV readings file", LogEntryType.ErrorMessage);
                    throw new Exception("SunnyExplorerCSVParser.ExtractSeparator: separator expected on first line of PV readings file");
                }
            }
            else
            {
                LogMessage("SunnyExplorerCSVParser.ExtractSeparator: unexpected end of file locating separator", LogEntryType.ErrorMessage);
                throw new Exception("SunnyExplorerCSVParser.ExtractSeparator: unexpected end of file locating separator");
            }

            if (ReadNextLine())
            {
                int pos = CurrentLine.IndexOf(decimalPattern);
                if (pos >= 0)
                {
                    int start = pos + decimalPattern.Length;
                    int pos2 = CurrentLine.IndexOf('|', start);
                    String val = "";
                    if (pos2 > start)
                        val = CurrentLine.Substring(start, pos2-start);

                    if (val == "comma")
                        fileParams.DecimalPoint = ',';
                    else if (val == "dot")
                        fileParams.DecimalPoint = '.';
                    else
                    {
                        LogMessage("SunnyExplorerCSVParser.ExtractSeparator: unknown decimal pattern - '" + val + "'", LogEntryType.ErrorMessage);
                        throw new Exception("SunnyExplorerCSVParser.ExtractSeparator: unknown decimal pattern");
                    }
                }
                else
                {
                    LogMessage("SunnyExplorerCSVParser.ExtractSeparator: decimal pattern expected on second line of PV readings file", LogEntryType.ErrorMessage);
                    throw new Exception("SunnyExplorerCSVParser.ExtractSeparator: decimal pattern expected on second line of PV readings file");
                }
            }
            else
            {
                LogMessage("SunnyExplorerCSVParser.ExtractSeparator: unexpected end of file locating separator", LogEntryType.ErrorMessage);
                throw new Exception("SunnyExplorerCSVParser.ExtractSeparator: unexpected end of file locating separator");
            }

        }

        private void ExtractSerialNumbers(FileParams fileParams)
        {
            const String serialNoPattern = "SN: ";
            fileParams.Inverters = new List<DeviceIdentity>(1);

            Boolean serialNumberFound = false;
            do
            {
                if (ReadNextLine())
                {
                    int startPos = 0;
                    int posSN;
                    while ((posSN = CurrentLine.IndexOf(fileParams.Separator, startPos)) >= 0)
                    {
                        DeviceIdentity inverter = new DeviceIdentity();

                        int endSN = CurrentLine.IndexOf(fileParams.Separator, posSN + 1);
                        if (endSN < 0)
                        {
                            LogMessage("SunnyExplorerCSVParser.ExtractSerialNumbers: cannot locate Serial Number", LogEntryType.ErrorMessage);
                            throw new Exception("SunnyExplorerCSVParser.ExtractSerialNumbers: cannot locate Serial Number");
                        }
                        inverter.Make = "SMA"; // sunny explorer only works with SMA inverters
                        inverter.SerialNo = (CurrentLine.Substring(posSN + 1, endSN - (posSN + 1))).Trim();
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
                    LogMessage("SunnyExplorerCSVParser.ExtractSerialNumbers: unexpected end of file looking for Serial Number", LogEntryType.ErrorMessage);
                    throw new Exception("SunnyExplorerCSVParser.ExtractSerialNumbers: unexpected end of file looking for Serial Number");
                }
            }
            while (!serialNumberFound);
        }

        private void ExtractModel(FileParams fileParams)
        {
            //positioned on SN line - next line contains model number
            if (ReadNextLine())
            {
                int startPos = 0;
                int modelCount = 0;
                int posModel;

                while ((posModel = CurrentLine.IndexOf(fileParams.Separator, startPos)) >= 0 && modelCount < fileParams.Inverters.Count)
                {
                    int endModel = CurrentLine.IndexOf(fileParams.Separator, posModel + 1);
                    if (endModel < 0)
                    {
                        LogMessage("SunnyExplorerCSVParser.ExtractModel: separator expected after Model", LogEntryType.ErrorMessage);
                        throw new Exception("SunnyExplorerCSVParser.ExtractModel: separator expected after Model");
                    }
                    DeviceIdentity identity = fileParams.Inverters[modelCount];
                    identity.Model = (CurrentLine.Substring(posModel + 1, endModel - (posModel + 1))).Replace(" ", "");
                    fileParams.Inverters[modelCount] = identity;

                    modelCount++;
                    startPos = endModel + 1;
                }
                if (modelCount != fileParams.Inverters.Count)
                {
                    LogMessage("SunnyExplorerCSVParser.ExtractModel: model missing or model line missing", LogEntryType.ErrorMessage);
                    throw new Exception("SunnyExplorerCSVParser.ExtractModel: model missing or model line missing");
                }
            }
            else
            {
                LogMessage("SunnyExplorerCSVParser.ExtractModel: unexpected end of file looking for Model Number", LogEntryType.ErrorMessage);
                throw new Exception("SunnyExplorerCSVParserEextractModel: unexpected end of file looking for Model Number");
            }
        }

        private void ExtractDateFormat(FileParams fileParams)
        {
            Boolean lineFound = false;

            do
            {
                if (ReadNextLine())
                {
                    if (CurrentLine.IndexOf(fileParams.Separator + "Counter" + fileParams.Separator + "Analog") >= 0)
                    {
                        lineFound = true;
                        if (!ReadNextLine())
                        {
                            LogMessage("SunnyExplorerCSVParser.ExtractDateFormat: unexpected end of file looking for Date Format 2", LogEntryType.ErrorMessage);
                            throw new Exception("SunnyExplorerCSVParser.ExtractDateFormat: unexpected end of file looking for Date Format 2");
                        }
                    }
                }
                else
                {
                    LogMessage("SunnyExplorerCSVParser.ExtractDateFormat: unexpected end of file looking for Date Format 1", LogEntryType.ErrorMessage);
                    throw new Exception("SunnyExplorerCSVParser.ExtractDateFormat: unexpected end of file looking for Date Format 1");
                }
            }
            while (!lineFound);

            int sepPos = CurrentLine.IndexOf(fileParams.Separator);
            if (sepPos < 0)
            {
                LogMessage("SunnyExplorerCSVParser.ExtractDateFormat: separator not found on Date Format line", LogEntryType.ErrorMessage);
                throw new Exception("SunnyExplorerCSVParser.extractDateFormat: separator not found on Date Format line");
            }
            fileParams.DateFormat = CurrentLine.Substring(0, sepPos);
        }

        private void ExtractDataLines(IDeviceManager deviceManager, GenConnection con, FileParams fileParams, out List<EnergyReadingSet> readingSets)
        {
            char[] separator = new char[1];
            int dataLineColumns = fileParams.Inverters.Count * 2 + 1; // datetime + 2 data columns per inverter
            String[] fields = new String[dataLineColumns];
            int period = 0;
            Int32 duration = 300;  // default to 5 minutes
            DateTime? prevTime = null;
            int inverterIndex = 0;

            separator[0] = fileParams.Separator;

            //PVInverter inverter = new PVInverter();

            // Sunny Explorer is not culture sensitive - it always uses dot as the decimal point
            System.Globalization.NumberFormatInfo nfi = new System.Globalization.NumberFormatInfo();
            nfi.CurrencyDecimalSeparator = fileParams.DecimalPoint.ToString();
            nfi.CurrencyGroupSeparator = "";
            nfi.NumberDecimalSeparator = nfi.CurrencyDecimalSeparator;
            nfi.NumberGroupSeparator = nfi.CurrencyGroupSeparator;

            List<EnergyReadingSet> inverterSets = new List<EnergyReadingSet>(fileParams.Inverters.Count);
            try
            {
                for (inverterIndex = 0; inverterIndex < fileParams.Inverters.Count; inverterIndex++)
                {
                    DeviceIdentity identity = fileParams.Inverters[inverterIndex];
                    IDevice dInfo = ((InverterManager)deviceManager).GetKnownDevice(con, 0, identity.Make, identity.Model, identity.SerialNo);
                    inverterSets.Add( new EnergyReadingSet(dInfo, NormalDaySize));
                }
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                LogMessage("SunnyExplorerCSVParser.ExtractDataLines: Error building reading sets: " + e.Message, LogEntryType.ErrorMessage);
                throw new Exception("SunnyExplorerCSVParser.ExtractDataLines: Error building reading sets - " + e.Message, e);
            }

            DateTime? readingTime = null;
            
            while (ReadNextLine())
            {
                if (CurrentLine.Length < 1)
                    break;

                fields = CurrentLine.Split(separator, dataLineColumns);

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

                        EnergyReading reading = new EnergyReading(0, readingTime.Value, 0, duration, false);

                        
                        if ((inverterIndex * 2 + 3) > fields.Length)
                            reading.Power = 0;
                        else if (fields[inverterIndex * 2 + 2] == "")
                            reading.Power = 0;
                        else
                            reading.Power = (int)(Convert.ToDouble(fields[inverterIndex * 2 + 2], nfi) * 1000.0);                       
                        
                        if (period == 1 && inverterIndex == 0)
                        {
                            for (int i = 0; i < fileParams.Inverters.Count; i++)
                                // duration can only be calculated by comparing datetime on two consecutive rows
                                // after second row is examined, update duration in first row
                                (inverterSets[i].Readings[0]).Seconds = duration;                               
                        }                            
                        
                        prevTime = readingTime;

                        inverterSets[inverterIndex].Readings.Add(reading);
                    }
                    period++;
                }
                catch (System.Threading.ThreadInterruptedException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    LogMessage("SunnyExplorerCSVParser.ExtractDataLines: Error reading csv data - Line: " + 
                        CurrentLine + " - Error: " + e.Message, LogEntryType.ErrorMessage);
                    throw new Exception("SunnyExplorerCSVParser.ExtractDataLines: format error in data fields - " + e.Message,e);
                }
            }
            readingSets = inverterSets;
        }

        private Boolean ReadNextLine()
        {
            CurrentLine = StreamReader.ReadLine();
            return CurrentLine != null;
        }
    }
}
