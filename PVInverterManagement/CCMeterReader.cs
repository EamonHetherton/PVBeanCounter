/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DeviceStream;
using Conversations;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public struct CCLiveRecord
    {
        public DateTime? SelTime;
        public DateTime MeterTime;
        public DateTime TimeStampe;
        public int Watts;
        public double Temperature;
    }

    class CCMeterReader :GenThread
    {
        private CC_EnviR_Converse Converse;
        private SerialStream Stream;
        private bool ReaderStarted;
        private String PortName;
        private int? BaudRate;
        private System.IO.Ports.Parity? Parity;
        private System.IO.Ports.Handshake? Handshake;


        private int ParseFailedCount = 0;
        private DateTime? LastParseFailed = null;

        private CCManager.CCSyncInfo SyncInfo;

        public override String ThreadName { get { return "CCMeterReader"; } }

        private int ConsumptionHistHours;
        private CCMeterManagerSettings Settings;
        private System.Globalization.NumberFormatInfo NumberFormatInfo;
        private System.Globalization.DateTimeFormatInfo TimeFormatInfo;

        public CCMeterReader(GenThreadManager genThreadManager, CCMeterManagerSettings settings, CCManager.CCSyncInfo syncInfo)
            : base(genThreadManager, GlobalSettings.SystemServices)
        {
            NumberFormatInfo = new System.Globalization.NumberFormatInfo();
            NumberFormatInfo.NumberDecimalSeparator = ".";
            NumberFormatInfo.CurrencyDecimalSeparator = ".";

            TimeFormatInfo = new System.Globalization.DateTimeFormatInfo();
            TimeFormatInfo.TimeSeparator = ":";

            SyncInfo = syncInfo;

            Converse = new CC_EnviR_Converse(GlobalSettings.SystemServices);
            ReaderStarted = false;
            Stream = null;

            PortName = settings.SerialPort.PortName;
            BaudRate = settings.SerialPort.BaudRate;
            if (BaudRate == null)
                BaudRate = 57600;
            Parity = settings.SerialPort.Parity;
            if (Parity == null)
                Parity = System.IO.Ports.Parity.None;
            Handshake = settings.SerialPort.Handshake;
            if (Handshake == null)
                Handshake = System.IO.Ports.Handshake.None;

            ConsumptionHistHours = settings.ConsumptionMeterHistHours == null ? 24 : settings.ConsumptionMeterHistHours.Value;
            Settings = settings;
        }

        private bool ParseFailedContinue()
        {
            if (LastParseFailed == null || (LastParseFailed < DateTime.Now.AddMinutes(-60)))
            {
                ParseFailedCount = 1;
                LastParseFailed = DateTime.Now;
                return true;
            }

            if (++ParseFailedCount >= 10)
            {
                LogMessage("ParseFailedContinue", ParseFailedCount + " message parse failures in last hour - Stopping meter reader", LogEntryType.ErrorMessage);
                return false;
            }
            else
                return true;
        }

        public static DateTime TimeToDateTime(TimeSpan time)
        {
            DateTime now = DateTime.Now;

            DateTime time1 = now.Date + time - TimeSpan.FromDays(1);
            DateTime time2 = now.Date + time;
            DateTime time3 = now.Date + time + TimeSpan.FromDays(1);

            int diff1 = Math.Abs((int)(now - time1).TotalMinutes);
            int diff2 = Math.Abs((int)(now - time2).TotalMinutes);
            int diff3 = Math.Abs((int)(time3 - now).TotalMinutes);

            if (diff2 <= diff1)
                if (diff2 <= diff3)
                    return time2;
            if (diff3 <= diff1)
                return time3;
            else
                return time1;
        }

        protected void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.MeterTrace)
        {
            GlobalSettings.LogMessage("CCMeterReader: " + routine, message, logEntryType);
        }

        private void ExtractRealTime(XElement msg, DateTime time, bool isNew)
        {
            int watts = 0;
            double temp = 0;
            bool isCelcius = true;
            int sensor = 0;
            CCLiveRecord curRec;

            try
            {
                if (isNew)
                {
                    sensor = Convert.ToInt32(msg.Element("sensor").Value);
                }

                if (!Settings.ApplianceList[sensor].StoreReading)
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
                curRec.SelTime = null;
                curRec.MeterTime = time;
                curRec.TimeStampe = DateTime.Now;
                curRec.Watts = watts;
                curRec.Temperature = temp;

                state = "Calling WaitOne";
                SyncInfo.MeterRecordsMutex.WaitOne();
                state = "Adding curRec to LiveRecords";
                SyncInfo.LiveRecords[sensor].Add(curRec);
                state = "Setting RecordsAvailEvent";
                SyncInfo.RecordsAvailEvent.Set();
                state = "Releasing Mutex";
                SyncInfo.MeterRecordsMutex.ReleaseMutex();
            }
            catch (Exception e)
            {
                LogMessage("ExtractNewRealTime", state + " - Sensor: " + sensor + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }

        public bool StartPortReader()
        {
            try
            {
                Stream = new SerialStream(GenThreadManager, GlobalSettings.SystemServices, PortName, BaudRate.Value, Parity.Value, 8, System.IO.Ports.StopBits.One, Handshake.Value, 12000);
                Converse.SetDeviceStream(Stream);
                Stream.Open();
                // added flush as I suspect I am receiving data that arrived at the port prior to the Open above!!!
                Stream.GetStream().Flush();
                Stream.StartBuffer();
                return true;
            }
            catch (Exception e)
            {
                LogMessage("StartPortReader", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
        }

        public void StopPortReader()
        {
            Stream.StopBuffer();
            ReaderStarted = false;
            Stream.Close();
            Converse.SetDeviceStream(null);
            Stream = null;
        }

        public String GetMessage()
        {
            if (!ReaderStarted)
                StartPortReader();

            if (Converse.DoConversation("CC_EnviR_Receive"))
                return "<msg>" + Converse.GetSessionVariable("DATA").ToString() + "</msg>";
            else
                return "";
        }

        private void ExtractNewHistory(XElement msg, DateTime time)
        {
            int dayNo = 0;
            int sensor = 0;
            Double energy = 0.0;

            MeterManager.MeterRecord curRec;

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
                                sensor = Convert.ToInt32(elem.Value);
                                sensorFound = true;
                            }
                            else if (sensorFound)
                            {
                                energy = Convert.ToDouble(elem.Value, NumberFormatInfo);
                                String name = elem.Name.ToString();

                                if (name.Length == 4 && name[0] == 'h')
                                {
                                    int intervalNo = Convert.ToInt32(name.Substring(1));

                                    if ((Settings.ApplianceList[sensor].StoreHistory || Settings.ApplianceList[sensor].AdjustHistory)
                                        && intervalNo <= ConsumptionHistHours)
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

                                        SyncInfo.MeterRecordsMutex.WaitOne();
                                        SyncInfo.HistoryRecords.Add(curRec);
                                        SyncInfo.RecordsAvailEvent.Set();
                                        SyncInfo.MeterRecordsMutex.ReleaseMutex();

                                        if (GlobalSettings.SystemServices.LogMeterTrace)
                                            LogMessage("InsertHistoryMeterReading", "Adding new hour history to List: time: " + curRec.Time
                                                + " : Sensor: " + curRec.Sensor + " : Energy: " + curRec.Energy
                                                + " : ReadingNo: " + intervalNo);
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
            int sensor = 0;
            Double energy = 0.0;

            MeterManager.MeterRecord curRec;

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

                                if ((Settings.ApplianceList[sensor].StoreHistory || Settings.ApplianceList[sensor].AdjustHistory)
                                    && intervalNo <= ConsumptionHistHours && intervalNo > 2)
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

                                    SyncInfo.MeterRecordsMutex.WaitOne();
                                    SyncInfo.HistoryRecords.Add(curRec);
                                    SyncInfo.RecordsAvailEvent.Set();
                                    SyncInfo.MeterRecordsMutex.ReleaseMutex();

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
            ReaderStarted = StartPortReader();
            LogMessage("Initialise", "CCMeterReader is starting", LogEntryType.StatusChange);
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
            StopPortReader();
            base.Finalise();
            LogMessage("Finalise", "CCMeterReader has stopped", LogEntryType.StatusChange);
        }
    }
}
