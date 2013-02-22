/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
using System.Threading;
using System.IO;
using GenericConnector;
using PVSettings;
using DeviceStream;
using Conversations;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public struct EW4009LiveRecord
    {
        public DateTime TimeStampe;
        public int Watts;
    }

    public class EW4009MeterManager : MeterManager
    {
        const int numChannels = 16;
        
        private string ConversationConfigFile;
        private Converse Converse;
        private SerialStream Stream;
        
        private String PortName;
        private int? BaudRate;
        private System.IO.Ports.Parity? Parity;
        private System.IO.Ports.Handshake? Handshake;

        // private bool IsStarting;
        private int settingsInterval;

        private CCMeterManagerSettings Settings { get { return (CCMeterManagerSettings) MeterManagerSettings; } }

        public EW4009MeterManager(GenThreadManager genThreadManager, IManagerManager managerManager, int meterManagerId, EW4009MeterManagerSettings settings) 
            : base(genThreadManager, settings, managerManager, meterManagerId)
        {
            ConversationConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EW4009_Conversations.txt");

            Converse = new EW4009_Converse(GlobalSettings.SystemServices);
            {
                ConversationLoader conversations = new ConversationLoader(ConversationConfigFile, GlobalSettings.SystemServices);
                conversations.LoadConversations(Converse);
            }

            Stream = null;
            PortName = settings.SerialPort.PortName;
            BaudRate = settings.SerialPort.BaudRate;
            if (BaudRate == null)
                BaudRate = 9600;
            Parity = settings.SerialPort.Parity;
            if (Parity == null)
                Parity = System.IO.Ports.Parity.None;
            Handshake = settings.SerialPort.Handshake;
            if (Handshake == null)
                Handshake = System.IO.Ports.Handshake.None;

            //IsStarting = true;
            settingsInterval = settings.SampleFrequency;
        }

        protected override String MeterManagerType
        {
            get
            {
                return "EW4009";
            }
        }

        private void ProcessOneRecord(int sensor, DateTime time, int watts)
        {
            if (SensorStatusList[sensor].initialise)
            {
                SensorStatusList[sensor].CurrentMinute = GetMinute(time);
                SensorStatusList[sensor].PreviousTime = time;
                SensorStatusList[sensor].initialise = false;
            }

            DateTime thisMinute = GetMinute(time);

            int duration;
            DateTime currentMinute = SensorStatusList[sensor].CurrentMinute;

            if(thisMinute > currentMinute)
            {
                // calc time to end of current minute
                duration = (int)(currentMinute - SensorStatusList[sensor].PreviousTime).TotalSeconds;

                LogMessage("ProcessOneRecord", "End Minute - Sensor: " + sensor + " - Watts: " + watts +
                " - curMin: " + currentMinute + " - prevTime: " + SensorStatusList[sensor].PreviousTime +
                " - Dur: " + duration);

                UpdateSensorList(currentMinute, currentMinute, sensor.ToString(), duration, watts);

                GlobalSettings.SystemServices.GetDatabaseMutex();
                try
                {
                    InsertMeterReading(SensorStatusList[sensor].Record);
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                }

                ResetSensor(SensorStatusList[sensor], thisMinute);
            }
            else if (time < SensorStatusList[sensor].PreviousTime)
            {
                LogMessage("ProcessOneRecord", "Time Warp Error: moved back in time - new time: " +
                    time + " - prev time: " + SensorStatusList[sensor].PreviousTime, LogEntryType.ErrorMessage);
                
                // discard timewarp records
                return;
            }

            duration = (int)(time - SensorStatusList[sensor].PreviousTime).TotalSeconds;

            if (duration > 0)
            {
                LogMessage("ProcessOneRecord", "Sensor: " + sensor + " - Time: " + time + " - Watts: " + watts +
                    " - curMin: " + SensorStatusList[sensor].CurrentMinute + " - prevTime: " + SensorStatusList[sensor].PreviousTime +
                    " - Dur: " + duration);

                UpdateSensorList(thisMinute, time, sensor.ToString(), duration, watts);
            }
        }

        private bool ReadLiveStatus()
        {
            DateTime time = DateTime.Now;
            bool result = Converse.DoConversation("current_output");
            if (!result)
            {
                LogMessage("ReadLiveStatus", "Error in 'current_output' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            for (int i = 1; i <= numChannels; i++)
            {
                String iStr = i.ToString("00");
                ByteVar state = (ByteVar)Converse.GetSessionVariable("State" + iStr);
                if (state.GetByte() == 0x31)
                {
                    ByteVar id = (ByteVar)Converse.GetSessionVariable("ID" + iStr);
                    ByteVar power = (ByteVar)Converse.GetSessionVariable("Power" + iStr);

                    // ProcessOneRecord(i - 1, time, power.GetUInt16LowByteFirst());
                    ProcessOneRecord(i - 1, time, power.GetUInt16());
                }                
            }

            return true;
        }

        public bool StartPortReader()
        {
            try
            {
                Stream = new SerialStream(GenThreadManager, GlobalSettings.SystemServices, PortName, BaudRate.Value, Parity.Value, 8, System.IO.Ports.StopBits.One, Handshake.Value, 12000);
                Converse.SetDeviceStream(Stream);
                Stream.Open();
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
            Stream.Close();
            Converse.SetDeviceStream(null);
            Stream = null;
        }

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(settingsInterval); } }

        public override void Initialise()
        {
            base.Initialise();

            // IsStarting = true;

            LogMessage("Initialise", "EW4009MeterManager is starting", LogEntryType.StatusChange);
            StartPortReader();
        }

        public override bool DoWork()
        {
            DateTime lastZero = DateTime.MinValue;
                
            try
            {
                ReadLiveStatus();                        
            }
            catch (Exception e)
            {
                LogMessage("DoWork", "ReadLiveStatus - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }          

            // IsStarting = false;
            return true;
        }

        public override void Finalise()
        {
            LogMessage("Finalise", "EW4009MeterManager is stopping", LogEntryType.StatusChange);
            StopPortReader();
            base.Finalise();
        }
    }
}
