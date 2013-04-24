/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;
using Device;

namespace DeviceControl
{
    public class DeviceManager_CC128_Reader : DeviceManager_Listener_Reader<CC128ManagerParams>
    {
        private DeviceManager_CC128.DeviceReadingInfo ReadingInfo;

        public override String ThreadName { get { return "CC128_Reader"; } }

        private CompositeAlgorithm_xml DeviceAlgorithm;
        
        private int DatabaseInterval;

        public DeviceManager_CC128_Reader(DeviceManager_CC128 deviceManager, GenThreadManager genThreadManager, 
            DeviceManagerSettings settings, DeviceManager_CC128.DeviceReadingInfo readingInfo, CompositeAlgorithm_xml deviceAlgorithm, 
            CC128ManagerParams managerParams)
            : base(deviceManager, genThreadManager, settings, managerParams)
        {
            DeviceAlgorithm = deviceAlgorithm;
            DatabaseInterval = settings.DBIntervalInt;

            ReadingInfo = readingInfo;
        }

        private int? GetParamIndex(uint sensor)
        {
            int i = 0;
            foreach (UInt64 address in ReadingInfo.Addresses)
            {
                if (address == sensor)
                    return i;
                i++;
            }
            return null;
        }

        public String GetMessage()
        {
            bool alarmFound = false;
            bool errorFound = false;
           
            if (DeviceAlgorithm.ExtractReading(true, ref alarmFound, ref errorFound))
                return "<msg>" + DeviceAlgorithm.Message + "</msg>";
            else
                return "";
        }

        private void ExtractRealTime(XElement msg, DateTime time, bool isNew)
        {
            int watts = 0;
            double temp = 0;
            bool isCelcius = true;
            uint sensor = 0;
            CC128_LiveRecord curRec;
            int? index;

            try
            {
                if (isNew)
                {
                    sensor = Convert.ToUInt32(msg.Element("sensor").Value);
                }

                index = GetParamIndex(sensor);
                if (!index.HasValue) // this sensor is not configured for recording
                    return;
         
                XElement xmlTemp = msg.Element("tmpr");
                if (xmlTemp == null)
                {
                    xmlTemp = msg.Element("tmprF"); // this one was not documented but was found in Issue 184
                    isCelcius = false;
                }

                if (xmlTemp != null)
                {
                    temp = Convert.ToDouble(xmlTemp.Value, NumberFormatInfo);
                    if (!isCelcius)
                        temp = (temp - 32.0) * 5.0 / 9.0; // convert to celcius
                }

                XElement ch1 = msg.Element("ch1");
                if (ch1 != null)
                    watts = Convert.ToInt32(ch1.Element("watts").Value);

                XElement ch2 = msg.Element("ch2");
                if (ch2 != null)
                    watts += Convert.ToInt32(ch2.Element("watts").Value);

                XElement ch3 = msg.Element("ch3");
                if (ch3 != null)
                    watts += Convert.ToInt32(ch3.Element("watts").Value);
            }
            catch (Exception e)
            {
                LogMessage("ExtractRealTime", "Exception extracting data: " + e.Message, LogEntryType.ErrorMessage);
                return;
            }

            String state = "starting";

            try
            {
                if (ReadingInfo.Enabled[index.Value])
                {
                    curRec.MeterTime = time;
                    curRec.TimeStampe = DateTime.Now;
                    curRec.Watts = watts;
                    curRec.Temperature = temp;

                    state = "Calling WaitOne";
                    ReadingInfo.RecordsMutex.WaitOne();
                    state = "Adding curRec to LiveRecords";
                    ReadingInfo.LiveRecords[index.Value].Add(curRec);
                    state = "Setting RecordsAvailEvent";
                    ReadingInfo.RecordsAvailEvent.Set();
                    state = "Releasing Mutex";
                    ReadingInfo.RecordsMutex.ReleaseMutex();
                }
            }
            catch (Exception e)
            {
                LogMessage("ExtractNewRealTime", state + " - Sensor: " + sensor + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }

        private void ExtractNewHistory(XElement msg, DateTime time)
        {
            int dayNo = 0;
            uint sensor = 0;
            Double energy = 0.0;
            int index = 0;

            CC128_HistoryRecord curRec;

            try
            {
                foreach (XElement elem2 in msg.Elements())
                {
                    if (elem2.Name == "dsw")
                        dayNo = Convert.ToInt32(elem2.Value);
                    else if (elem2.Name == "data")
                    {
                        bool sensorFound = false;

                        foreach (XElement elem in elem2.Elements())
                        {
                            if (elem.Name == "sensor")
                            {
                                sensor = Convert.ToUInt32(elem.Value);
                                int? indexEval = GetParamIndex(sensor);
                                if (!indexEval.HasValue) // this sensor is not configured for recording
                                    continue;
                                index = indexEval.Value;
                                sensorFound = true;
                            }
                            else if (sensorFound)
                            {
                                energy = Convert.ToDouble(elem.Value, NumberFormatInfo);
                                String name = elem.Name.ToString();

                                if (name.Length == 4 && name[0] == 'h')
                                {
                                    int intervalNo = Convert.ToInt32(name.Substring(1));
                                    // interval represents up to intervalNo hours ago
                                    // in 2 hour steps - h004 is the lowest - it contains from 4 hours ago to 2 hours ago
                                    // h002 is not transmitted as 2 hours ago to now is not history it is the current 2 hour interval - constantly changing
                                    DeviceManagerDeviceSettings dev = Settings.GetDevice(sensor);

                                    if ((dev.Enabled && dev.UpdateHistory)
                                        && intervalNo <= ManagerParams.HistoryHours)
                                    {
                                        TimeSpan tod = TimeSpan.FromHours(time.Hour - (intervalNo - 4));
                                        curRec.Sensor = sensor;
                                        curRec.Time = (time.Date + tod);
                                        curRec.Energy = energy;
                                        curRec.Calculated = null;
                                        curRec.Duration = 7200;
                                        curRec.Count = 1;
                                        curRec.Temperature = null;
                                        curRec.MinPower = null;
                                        curRec.MaxPower = null;
                                        curRec.InRange = null;

                                        ReadingInfo.RecordsMutex.WaitOne();
                                        
                                        /*
                                        // check for history across midnight - must split into two 1 hour entries
                                        // each history entry only affects one day
                                        if (tod == TimeSpan.FromHours(1.0))
                                        {
                                            DateTime orig = curRec.Time;
                                            curRec.Time = time.Date; // midnight - end of prev day
                                            curRec.Duration = 3600;
                                            curRec.Energy = curRec.Energy / 2.0;
                                            ReadingInfo.HistoryRecords[index].Add(curRec);
                                            if (GlobalSettings.SystemServices.LogTrace)
                                                LogMessage("ExtractNewHistory", "Adding new hour history to List (12:00AM): time: " + curRec.Time
                                                    + " : Sensor: " + curRec.Sensor + " : Energy: " + curRec.Energy
                                                    + " : ReadingNo: " + intervalNo, LogEntryType.Trace);
                                            curRec.Time = orig;
                                            ReadingInfo.HistoryRecords[index].Add(curRec);
                                        }
                                        else 
                                        */
                                        ReadingInfo.HistoryRecords[index].Add(curRec);                                       

                                        if (GlobalSettings.SystemServices.LogTrace)
                                            LogMessage("ExtractNewHistory", "Adding new 2 hour history to List: time: " + curRec.Time
                                                + " : Sensor: " + curRec.Sensor + " : Energy: " + curRec.Energy
                                                + " : ReadingNo: " + intervalNo, LogEntryType.Trace);

                                        ReadingInfo.RecordsMutex.ReleaseMutex();
                                        ReadingInfo.RecordsAvailEvent.Set();
                                    }
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                LogMessage("ExtractNewHistory", "Exception extracting data: " + msg.Value + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return;
            }
        }

        private void ExtractOldHistory(XElement msg, DateTime time)
        {
            uint sensor = 0;
            Double energy = 0.0;
            
            int? index = GetParamIndex(sensor);
            if (!index.HasValue) // this sensor is not configured for recording
                return;

            CC128_HistoryRecord curRec;

            try
            {
                foreach (XElement elem2 in msg.Elements())
                {
                    if (elem2.Name == "hrs")
                    {
                        foreach (XElement elem in elem2.Elements())
                        {

                            energy = Convert.ToDouble(elem.Value, NumberFormatInfo);
                            String name = elem.Name.ToString();

                            if (name.Length == 4 && name[0] == 'h')
                            {
                                int intervalNo = Convert.ToInt32(name.Substring(1));
                                DeviceManagerDeviceSettings dev = Settings.GetDevice(sensor);

                                if ((dev.Enabled && dev.UpdateHistory)
                                        && intervalNo <= ManagerParams.HistoryHours && intervalNo > 2)
                                {
                                    curRec.Sensor = sensor;
                                    curRec.Time = (time.Date + TimeSpan.FromHours(time.Hour - (intervalNo - 4)));
                                    curRec.Energy = energy;
                                    curRec.Calculated = null;
                                    curRec.Duration = 7200;
                                    curRec.Count = 1;
                                    curRec.Temperature = null;
                                    curRec.MinPower = null;
                                    curRec.MaxPower = null;
                                    curRec.InRange = null;

                                    ReadingInfo.RecordsMutex.WaitOne();
                                    ReadingInfo.HistoryRecords[index.Value].Add(curRec);
                                    ReadingInfo.RecordsAvailEvent.Set();
                                    ReadingInfo.RecordsMutex.ReleaseMutex();

                                    if (GlobalSettings.SystemServices.LogMeterTrace)
                                        LogMessage("InsertHistoryMeterReading", "Adding old hour history to List: time: " + curRec.Time
                                            + " : Sensor: " + curRec.Sensor + " : Energy: " + curRec.Energy
                                            + " : ReadingNo: " + intervalNo);
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                LogMessage("ExtractOldHistory", "Exception extracting data: " + msg.Value + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return;
            }
        }

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(0.0); } }

        public override void Initialise()
        {
            base.Initialise();
            LogMessage("Initialise", "CC128_Reader is starting", LogEntryType.StatusChange);
        }

        public override bool DoWork()
        {
            String msgStr;
            XElement msg;
            DateTime time;
            bool newFormat;
            int prevHour = -1;
            bool updateHist = false;

            try
            {
                msgStr = GetMessage();
            }
            catch (Exception e)
            {
                LogMessage("DoWork", "GetMessage - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            if (msgStr == "")
                return true;

            try
            {
                msg = XElement.Parse(msgStr);

                XElement date = msg.Element("date");

                newFormat = (date == null); // new format messages do not have a date element

                if (newFormat)
                    time = TimeToDateTime(TimeSpan.Parse(msg.Element("time").Value, TimeFormatInfo));
                else
                {
                    int hour = Convert.ToInt32(date.Element("hr").Value);
                    int minute = Convert.ToInt32(date.Element("min").Value);
                    int second = Convert.ToInt32(date.Element("sec").Value);
                    time = TimeToDateTime(TimeSpan.FromHours(hour) + TimeSpan.FromMinutes(minute) + TimeSpan.FromSeconds(second));

                    // old format contains history in every record
                    // only update every odd hour
                    bool newHour = (hour != prevHour);
                    prevHour = hour;
                    updateHist = newHour && ((hour % 2) == 1);
                }
            }
            catch (Exception e)
            {
                LogMessage("DoWork", "XML Parse failed - Message: " + msgStr + " :exception: " + e.Message, LogEntryType.ErrorMessage);
                return ParseFailedContinue();
            }

            String state = "get content";
            try
            {
                if (newFormat)
                {
                    XElement hist = msg.Element("hist");

                    if (hist == null)
                    {
                        state = "calling ExtractRealTime";
                        ExtractRealTime(msg, time, newFormat);
                    }
                    else
                    {
                        state = "calling ExtractHistory";
                        ExtractNewHistory(hist, time);
                    }
                }
                else
                {
                    ExtractRealTime(msg, time, newFormat);

                    XElement hist = msg.Element("hist");

                    if (hist != null)
                        ExtractOldHistory(hist, time);
                }
            }
            catch (Exception e)
            {
                LogMessage("DoWork", state + " - Failed - Message: " + msgStr + " :exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            return true;
        }

        public override void Finalise()
        {
            base.Finalise();
            LogMessage("Finalise", "CC128_Reader has stopped", LogEntryType.StatusChange);
        }
    }
}

