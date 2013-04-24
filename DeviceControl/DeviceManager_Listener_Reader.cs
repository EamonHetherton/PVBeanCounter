/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
using DeviceStream;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;

namespace DeviceControl
{
    public abstract class DeviceManager_Listener_Reader<TDeviceParams> : GenThread
    {
        protected int ParseFailedCount = 0;
        protected DateTime? LastParseFailed = null;

        //protected DeviceManagerSettings Settings;
        protected System.Globalization.NumberFormatInfo NumberFormatInfo;
        protected System.Globalization.DateTimeFormatInfo TimeFormatInfo;

        protected DeviceManagerSettings Settings;

        protected SerialStream Stream { get; private set; }
        private bool ReaderStarted = false;

        protected DeviceManagerBase DeviceManager;

        protected TDeviceParams ManagerParams;

        public DeviceManager_Listener_Reader(DeviceManagerBase deviceManager, GenThreadManager genThreadManager, DeviceManagerSettings settings, TDeviceParams deviceParams)
            : base(genThreadManager, GlobalSettings.SystemServices)
        {
            ManagerParams = deviceParams;
            DeviceManager = deviceManager;
            NumberFormatInfo = new System.Globalization.NumberFormatInfo();
            NumberFormatInfo.NumberDecimalSeparator = ".";
            NumberFormatInfo.CurrencyDecimalSeparator = ".";

            TimeFormatInfo = new System.Globalization.DateTimeFormatInfo();
            TimeFormatInfo.TimeSeparator = ":";

            Settings = settings;
        }

        protected void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.Trace)
        {
            GlobalSettings.LogMessage("DeviceManager_Listener_Reader: " + routine, message, logEntryType);
        }

        protected bool ParseFailedContinue()
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

            int diff1 = Math.Abs((int)(now - time1).TotalMinutes); // expect 24hours plus a bit
            int diff2 = Math.Abs((int)(now - time2).TotalMinutes); // expect just a bit
            int diff3 = Math.Abs((int)(time3 - now).TotalMinutes); // expect just a bit less than 24 hours

            if (diff2 <= diff1)         // expect true
                if (diff2 <= diff3)     // expect true
                    return time2;       // expect this
            if (diff3 <= diff1)
                return time3;           // try to return right day when way off sync - perhaps better to turn off history!!!
            else
                return time1;
        }

        public bool StartPortReader()
        {
            if (!ReaderStarted)
                try
                {
                    // Read timeout is device message interval + 5 seconds
                    Stream = new SerialStream(GenThreadManager, GlobalSettings.SystemServices,
                        DeviceManager.PortName, DeviceManager.BaudRate, DeviceManager.Parity, 
                        DeviceManager.DataBits, DeviceManager.StopBits, DeviceManager.Handshake, (Settings.MessageIntervalInt + 5) * 1000);
                    DeviceManager.Protocol.SetDeviceStream(Stream);
                    Stream.Open();
                    Stream.StartBuffer();
                    ReaderStarted = true;
                }
                catch (Exception e)
                {
                    LogMessage("StartPortReader", "Port: " + DeviceManager.PortName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    Stream = null;
                    DeviceManager.Protocol.SetDeviceStream(null);
                    return false;
                }

            return true;
        }

        public void StopPortReader()
        {
            if (ReaderStarted)
                try
                {
                    ReaderStarted = false;
                    Stream.Close();
                    DeviceManager.Protocol.SetDeviceStream(null);
                    Stream = null;
                }
                catch (Exception e)
                {
                    LogMessage("StopPortReader", "Port: " + DeviceManager.PortName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);

                    Stream.ResetToClosed();

                    Stream = null;
                }
        }

        public override void Initialise()
        {
            base.Initialise();

            StopPortReader();
            StartPortReader();
        }

        public override void Finalise()
        {
            StopPortReader();
            base.Finalise();
            LogMessage("Finalise", "Port Reader Name = " + Settings.Name + " - manager stopping", LogEntryType.StatusChange);
        }

    }
}
