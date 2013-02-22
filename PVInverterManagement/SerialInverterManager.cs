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
using GenericConnector;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;
using DeviceStream;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public abstract class SerialInverterManager :InverterManager
    {
        protected String SiteId;

        protected bool InverterInitialised = false;

        protected Converse Converse = null;

        private string ConversationConfigFile;
        private SerialStream Stream;
        private String PortName;
        private System.IO.Ports.Parity Parity;
        private int BaudRate;
        private int DataBits;
        private System.IO.Ports.StopBits StopBits;
        private System.IO.Ports.Handshake Handshake;

        protected int CumulativeRes = 0;

        private bool ReaderStarted;

        public abstract String ConversationFileName { get; }

        public SerialInverterManager(GenThreadManagement.GenThreadManager genThreadManager, int imid,
            InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager)
        {
            // System.Threading.Thread.Sleep(20000);

            BaudRate = imSettings.BaudRate == "" ? 9600 : Convert.ToInt32(imSettings.BaudRate);
            PortName = imSettings.PortName;
            Parity = SerialPortSettings.ToParity(imSettings.Parity) == null ? System.IO.Ports.Parity.None :
                SerialPortSettings.ToParity(imSettings.Parity).Value;
            DataBits = imSettings.DataBits == "" ? 8 : Convert.ToInt32(imSettings.DataBits);
            StopBits = SerialPortSettings.ToStopBits(imSettings.StopBits) == null ? System.IO.Ports.StopBits.One :
                SerialPortSettings.ToStopBits(imSettings.StopBits).Value;
            Handshake = SerialPortSettings.ToHandshake(imSettings.Handshake) == null ? System.IO.Ports.Handshake.None :
                SerialPortSettings.ToHandshake(imSettings.Handshake).Value;
            ReaderStarted = false;

            SiteId = null;
        }

        private void LoadConverse()
        {
            ConversationConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConversationFileName);
            ConversationLoader conversations = new ConversationLoader(ConversationConfigFile, GlobalSettings.SystemServices);
            LoadLocalConverse(); // create alternate Converse here if required             
            conversations.LoadConversations(Converse);            
        }

        protected virtual void LoadLocalConverse()
        {
            Converse = new Converse(GlobalSettings.SystemServices, null);
        }

        public int EffectiveDatabasePeriod
        {
            get
            {
                if (InverterManagerSettings.DatabasePeriod.HasValue)
                    return InverterManagerSettings.DatabasePeriod.Value;
                else
                    return 60;
            }
        }

        public int EffectiveQueryPeriod
        {
            get
            {
                if (InverterManagerSettings.QueryPeriod.HasValue)
                    return InverterManagerSettings.QueryPeriod.Value;
                else
                    return 6;
            }
        }

        public bool StartPortReader()
        {
            if (!ReaderStarted)
                try
                {
                    Stream = new SerialStream(GenThreadManager, GlobalSettings.SystemServices, PortName, BaudRate, Parity, DataBits, StopBits, Handshake, 6000);
                    Converse.SetDeviceStream(Stream);
                    Stream.Open();
                    Stream.StartBuffer();
                    ReaderStarted = true;
                }
                catch (Exception e)
                {
                    LogMessage("StartPortReader", "Port: " + PortName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    Stream = null;
                    Converse.SetDeviceStream(null);
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
                    Converse.SetDeviceStream(null);
                    Stream = null;
                }
                catch (Exception e)
                {
                    LogMessage("StopPortReader", "Port: " + PortName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    
                    Stream.ResetToClosed();
                    
                    Stream = null;  
                }
        }

        protected abstract bool InitialiseInverter();

        protected abstract int ExtractInverterYield(IDevice iInfo);

        protected override int ExtractYield()
        {
            bool result;
            int res = 0;

            String stage = "InitialiseInverter";

            try
            {
                if (!InverterInitialised)
                {
                    result = InitialiseInverter();
                    if (!result)
                    {
                        // initialisation failed - end thread - should auto restart after 30 seconds
                        IsRunning = false;
                        InverterInitialised = false;
                        return res;
                    }
                }
            }
            catch (Exception e)
            {
                LogMessage("ExtractYield", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return res;
            }

            stage = "ExtractYield";
            try
            {
                foreach (IDevice device in DeviceList)
                    if (device.Found)
                        res += ExtractInverterYield(device);
            }
            catch (Exception e)
            {
                LogMessage("ExtractYield", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return res;
            }

            if (res == 0)
            {
                // yield extract failed - end thread - should auto restart within 30 seconds
                IsRunning = false;
                InverterInitialised = false;
            }

            return res;
        }

        public override TimeSpan Interval 
        { 
            get 
            {
                return TimeSpan.FromSeconds(EffectiveQueryPeriod);
            } 
        }

        public override void Initialise()
        {
            base.Initialise();
            LoadConverse();

            InverterDataRecorder.UpdatePastDates();
            CumulativeRes = 0;
        }

        /*
        private void ResetInverterDay(InverterDataRecorder.InverterInfo iInfo)
        {
            iInfo.StartEnergyResolved = false;
            iInfo.EnergyDropFound = false;
            iInfo.UseEnergyTotal = true;
        }
        */

        public override void DoInverterWork()
        {
            String state = "start";

            InverterDataRecorder.CheckStartOfDay();

            try
            {
                LogMessage("DoInverterWork", "Calling ExtractYield", LogEntryType.Trace);
                CumulativeRes += ExtractYield();

                state = "before UpdateOutputHistoryInterval";
                
                if (CumulativeRes > 0)                    
                {
                    InverterDataRecorder.UpdateOutputHistoryInterval();
                    CumulativeRes = 0;
                }
            }
            catch (Exception e)
            {
                LogMessage("DoInverterWork", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }

        public override void Finalise()
        {
            StopPortReader();
            base.Finalise();
        }
    }
}
