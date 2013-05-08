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
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;
using DeviceStream;
using DeviceDataRecorders;
using GenThreadManagement;
using PVBCInterfaces;
using Algorithms;
using Device;
using GenericConnector;

namespace DeviceControl
{
    public interface IDeviceManagerManager
    {
        bool LiveLoadForced { get; }
        void ReleaseErrorLoggers();
        bool RunMonitors { get; }
        void SetOutputReady(string systemId);
        void StartService(bool fullStartup);
        void StopService();
        IEvents EnergyEvents { get; }
        Device.DeviceBase FindDeviceFromSettings(DeviceManagerDeviceSettings deviceSettings);

        List<DeviceControl.DeviceManagerBase> RunningDeviceManagers { get; }
        List<DeviceControl.DeviceManagerBase> ConsolidationDeviceManagers { get; }
    }

    public abstract class DeviceManagerBase : GenThread, IDeviceManager
    {
        protected DeviceManagerSettings DeviceManagerSettings;
        public IDeviceManagerManager ManagerManager { get; private set; }

        public ErrorLogger ErrorLogger { get; private set; }

        public Protocol Protocol { get; protected set; }

        internal String PortName;
        internal System.IO.Ports.Parity Parity;
        internal int BaudRate;
        internal int DataBits;
        internal System.IO.Ports.StopBits StopBits;
        internal System.IO.Ports.Handshake Handshake;

        protected string ConfigFileName = null;
        protected DateTime? NextFileDate = null;      // specifies the next DateTime to be used for extract
        protected string OutputDirectory;               // directory where extract files will be written
        protected string ArchiveDirectory;              // directory where extract files will be moved when processed

        public DeviceManagerBase(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, GlobalSettings.SystemServices)
        {
            OutputDirectory = GlobalSettings.ApplicationSettings.DefaultDirectory;
            ArchiveDirectory = "Archive";
            DeviceManagerSettings = mmSettings;
            ManagerManager = imm;
            Protocol = null;

            BaudRate = mmSettings.BaudRate == "" ? 9600 : Convert.ToInt32(mmSettings.BaudRate);
            PortName = mmSettings.PortName;
            Parity = SerialPortSettings.ToParity(mmSettings.Parity) == null ? System.IO.Ports.Parity.None :
                SerialPortSettings.ToParity(mmSettings.Parity).Value;
            DataBits = mmSettings.DataBits == "" ? 8 : Convert.ToInt32(mmSettings.DataBits);
            StopBits = SerialPortSettings.ToStopBits(mmSettings.StopBits) == null ? System.IO.Ports.StopBits.One :
                SerialPortSettings.ToStopBits(mmSettings.StopBits).Value;
            Handshake = SerialPortSettings.ToHandshake(mmSettings.Handshake) == null ? System.IO.Ports.Handshake.None :
                SerialPortSettings.ToHandshake(mmSettings.Handshake).Value;

            String directory = GlobalSettings.ApplicationSettings.InverterLogs;
            if (directory == "" || directory == null)
                directory = System.IO.Path.Combine(GlobalSettings.ApplicationSettings.DefaultDirectory, "ErrorLogs");
            else if (System.IO.Path.IsPathRooted(directory))
                directory = System.IO.Path.Combine(directory);
            else
                directory = System.IO.Path.Combine(GlobalSettings.ApplicationSettings.DefaultDirectory, directory);

            ErrorLogger = new ErrorLogger(GlobalSettings.SystemServices, ThreadName + "_Err", "", directory);

            LogMessage("Loading", LogEntryType.Trace);
        }

        public override String ThreadName { get { return "DeviceMgr"; } }

        public virtual void ResetStartOfDay()
        {
        }

        public virtual String ManagerTypeName { get { return DeviceManagerSettings.ManagerTypeName; } }

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage(ThreadName, message, logEntryType);
        }

        public void CloseErrorLogger()
        {
            ErrorLogger.Close();
        }

        public override void Finalise()
        {
            base.Finalise();
            CloseErrorLogger();

            LogMessage("Finalise - Name = " + DeviceManagerSettings.Name + " - manager stopping", LogEntryType.StatusChange);
        }

        public bool InvertersRunning
        {
            get
            {
                TimeSpan curTime = DateTime.Now.TimeOfDay;
                if (GlobalSettings.ApplicationSettings.InverterStartTime.HasValue && curTime < GlobalSettings.ApplicationSettings.InverterStartTime.Value)
                    return false;
                if (GlobalSettings.ApplicationSettings.InverterStopTime.HasValue && curTime >= GlobalSettings.ApplicationSettings.InverterStopTime.Value)
                    return false;
                return true;
            }
        }

        public abstract List<DeviceBase> GenericDeviceList { get; }
    }

    public abstract class DeviceManagerTyped<TDevice> : DeviceManagerBase where TDevice : DeviceBase
    {
        public List<TDevice> DeviceList { get; protected set; }

        public override List<DeviceBase> GenericDeviceList 
        { 
            get 
            {
                List<DeviceBase> list = new List<DeviceBase>();
                foreach (TDevice d in DeviceList)
                    list.Add((DeviceBase)d);
                return list; 
            } 
        }

        public DeviceManagerTyped(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
            SetUpProtocol();
            LoadDevices();
        }

        protected abstract TDevice NewDevice(DeviceManagerDeviceSettings dmDevice);

        private void SetUpProtocol()
        {
            String protocolName = DeviceManagerSettings.Protocol;
            if (protocolName.Trim() == "")
            {
                Protocol = null;
                return;
            }

            ProtocolSettings protocolSettings = GlobalSettings.ApplicationSettings.DeviceManagementSettings.GetProtocol(protocolName);

            foreach (DeviceManagerDeviceSettings dmDeviceSettings in DeviceManagerSettings.DeviceList)
            {
                if (!dmDeviceSettings.Enabled)
                    continue;

                if (protocolName != dmDeviceSettings.DeviceSettings.Protocol)
                    LogMessage("SetUpProtocol - Incompatible device protocol - Expected: " + protocolName + " - Found: " + dmDeviceSettings.DeviceSettings.Protocol, LogEntryType.ErrorMessage);
            }

            Protocol = new Protocol(protocolSettings);
        }

        private void LoadDevices()
        {
            DeviceList = new List<TDevice>();
                       
            foreach (DeviceManagerDeviceSettings dmDeviceSettings in DeviceManagerSettings.DeviceList)
            {
                //if (DeviceManagerSettings.ManagerType != DeviceManagerType.Consolidation && !dmDeviceSettings.Enabled)
                //    continue;

                TDevice device = NewDevice( dmDeviceSettings);

                DeviceList.Add(device);
            }
        }
    }

    public abstract class CommunicationDeviceManager<TDevice> : DeviceManagerTyped<TDevice> where TDevice : DeviceBase
    {
        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(6); } }

        public DateTime NextRunTime_Original(DateTime? currentTime = null)
        {
            return base.NextRunTime(currentTime);
        }

        public override DateTime NextRunTime(DateTime? currentTime = null)
        {
            DateTime nextTime = DateTime.MaxValue;

            foreach (TDevice dev in DeviceList)
            {
                DateTime time = dev.NextRunTime;
                if (time < nextTime)
                    nextTime = time;
            }

            return nextTime > DateTime.Now ? nextTime : DateTime.Now;
        }

        public override String ThreadName { get { return "DeviceMgr_" + PortName; } }

        public CommunicationDeviceManager(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {            
        }

        public override void ResetStartOfDay()
        {
            foreach (TDevice dev in DeviceList)
                dev.ResetStartOfDay();
        }

        public override void Initialise()
        {
            base.Initialise();            
        }

        public override void Finalise()
        {            
            base.Finalise();
            LogMessage("Finalise - Name = " + DeviceManagerSettings.Name + " - manager stopping", LogEntryType.StatusChange);
        }
    }

    public abstract class DeviceManager_ActiveController<TDevice> : CommunicationDeviceManager<TDevice> where TDevice : ActiveDevice
    {
        protected SerialStream Stream { get; private set; }
        private bool ReaderStarted = false;

        public DeviceManager_ActiveController(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm) : base(genThreadManager, mmSettings, imm)
        {            
        }

        public override bool DoWork()
        {
            try
            {
                foreach (TDevice device in DeviceList)
                {
                    if (device.Enabled && device.NextRunTime <= NextRunTimeStamp)
                    {
                        if (device.DeviceSettings.DeviceType == DeviceType.Inverter && !InvertersRunning)
                            continue;

                        bool res = device.DoExtractReadings();
                        if (res)
                        {
                            device.LastRunTime = NextRunTimeStamp;
                        }
                        else
                            IsRunning = false;
                    }
                }
            }
            catch (System.Threading.ThreadInterruptedException)
            {

            }
            return true;
        }

        public override void Initialise()
        {
            base.Initialise();

            StopPortReader();
            StartPortReader();
            
            //SetupInverterDataRecorder();
        }

        public override void Finalise()
        {
            StopPortReader();
            base.Finalise();
            LogMessage("Finalise - Name = " + DeviceManagerSettings.Name + " - manager stopping", LogEntryType.StatusChange);
        }

        public bool StartPortReader()
        {
            if (!ReaderStarted)
                try
                {
                    Stream = new SerialStream(GenThreadManager, GlobalSettings.SystemServices,
                        PortName, BaudRate, Parity, DataBits, StopBits, Handshake, DeviceManagerSettings.MessageIntervalInt * 1000);
                    Protocol.SetDeviceStream(Stream);
                    Stream.Open();
                    Stream.StartBuffer();
                    ReaderStarted = true;
                }
                catch (Exception e)
                {
                    LogMessage("StartPortReader - Port: " + PortName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    Stream = null;
                    Protocol.SetDeviceStream(null);
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
                    Protocol.SetDeviceStream(null);
                    Stream = null;
                }
                catch (Exception e)
                {
                    LogMessage("StopPortReader - Port: " + PortName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);

                    Stream.ResetToClosed();

                    Stream = null;
                }
        }
    }

    public abstract class DeviceManager_PassiveController<TDevice, TLiveRec, THistRec, TManagerParams> : CommunicationDeviceManager<TDevice> where TDevice : PassiveDevice
    {
        public struct DeviceReadingInfo
        {
            public Mutex RecordsMutex;
            public ManualResetEvent RecordsAvailEvent;
            public String[] SerialNumbers;
            public String[] Models;
            public String[] Manufacturers;
            public UInt64[] Addresses;
            public bool[] Enabled;
            public List<TLiveRec>[] LiveRecords;
            public List<THistRec>[] HistoryRecords;
        }

        protected DeviceReadingInfo ReadingInfo;

        public DeviceManager_PassiveController(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
            InitialiseDeviceInfo(DeviceList.Count);
            LoadParams();
        }

        protected virtual void InitialiseDeviceInfo(int numDevices, bool ignoreConfiguredDevices = false)
        {
            ReadingInfo.LiveRecords = new List<TLiveRec>[numDevices];
            ReadingInfo.HistoryRecords = new List<THistRec>[numDevices];
            ReadingInfo.Addresses = new UInt64[numDevices];
            ReadingInfo.SerialNumbers = new String[numDevices];
            ReadingInfo.Models = new String[numDevices];
            ReadingInfo.Manufacturers = new String[numDevices];
            ReadingInfo.Enabled = new bool[numDevices];

            for (int i = 0; i < numDevices; i++)
            {
                ReadingInfo.LiveRecords[i] = new List<TLiveRec>();
                ReadingInfo.HistoryRecords[i] = new List<THistRec>();

                // Some devive managers create their own list of devices without the need for explicit config (eg SMA Sunny Explorer)
                if (!ignoreConfiguredDevices)
                {
                    ReadingInfo.Addresses[i] = DeviceList[i].Address;
                }
                ReadingInfo.Enabled[i] = DeviceList[i].Enabled;
            }

            ReadingInfo.RecordsMutex = new Mutex();
            ReadingInfo.RecordsAvailEvent = new ManualResetEvent(false);
        }

        protected abstract void LoadParams();

        public override void Finalise()
        {
            base.Finalise();
            LogMessage("Finalise - Name = " + DeviceManagerSettings.Name + " - manager stopping", LogEntryType.StatusChange);
        }
    }

    public abstract class DeviceManager_Listener<TDevice, TLiveRec, THistRec, TManagerParams> : DeviceManager_PassiveController<TDevice, TLiveRec, THistRec, TManagerParams> where TDevice : PassiveDevice
    {
        protected int DeviceReaderId;

        protected CompositeAlgorithm_xml DeviceAlgorithm;

        public DeviceManager_Listener(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
            //DeviceManagerDevice dmDevice = new DeviceManagerDevice(mmSettings);

            AlgorithmParams aParams;
            aParams.Protocol = Protocol;
            aParams.EndianConverter16Bit = Protocol.EndianConverter16Bit;
            aParams.EndianConverter32Bit = Protocol.EndianConverter32Bit;
            mmSettings.CheckListenerDeviceId();
            aParams.BlockList = mmSettings.ListenerDeviceSettings.BlockList;
            aParams.AlgorithmList = mmSettings.ListenerDeviceSettings.AlgorithmList;
            aParams.DeviceName = mmSettings.ListenerDeviceSettings.Description;
            aParams.ErrorLogger = this.ErrorLogger;

            DeviceAlgorithm = new CompositeAlgorithm_xml(aParams); 
            DeviceManager_Listener_Reader<TManagerParams> DeviceReader = GetReader(genThreadManager);
            DeviceReaderId = genThreadManager.AddThread(DeviceReader);
        }

        protected abstract void ProcessOneLiveRecord(TDevice device, TLiveRec liveRec);
        protected abstract void ProcessOneHistoryRecord(TDevice device, THistRec liveRec);
        protected abstract DeviceManager_Listener_Reader<TManagerParams> GetReader(GenThreadManager threadManager);        

        public override bool DoWork()
        {
            int index;

            DateTime lastZero = DateTime.MinValue;

            if (ReadingInfo.RecordsAvailEvent.WaitOne(10000))
            {
                ReadingInfo.RecordsAvailEvent.Reset();

                for (index = 0; index < DeviceList.Count; index++)
                {
                    while (ReadingInfo.LiveRecords[index].Count > 0)
                    {
                        try
                        {
                            ProcessOneLiveRecord(DeviceList[index], ReadingInfo.LiveRecords[index][0]);
                        }
                        catch (Exception e)
                        {
                            LogMessage("DoWork.ProcessOneLiveRecord - Exception: " + e.Message, LogEntryType.ErrorMessage);
                            // discard record causing error - attempt to continue
                        }

                        ReadingInfo.RecordsMutex.WaitOne();
                        ReadingInfo.LiveRecords[index].RemoveAt(0);
                        ReadingInfo.RecordsMutex.ReleaseMutex();
                    }
                }

                for (index = 0; index < DeviceList.Count; index++)
                {
                    while (ReadingInfo.HistoryRecords[index].Count > 0)
                    {
                        try
                        {
                            TDevice device = DeviceList[index];
                            if (device.DeviceSettings.UseHistory)
                                ProcessOneHistoryRecord(device, ReadingInfo.HistoryRecords[index][0]);
                        }
                        catch (Exception e)
                        {
                            LogMessage("DoWork.ProcessOneHistoryRecord - Exception: " + e.Message, LogEntryType.ErrorMessage);
                            // discard record causing error - attempt to continue
                        }

                        ReadingInfo.RecordsMutex.WaitOne();
                        ReadingInfo.HistoryRecords[index].RemoveAt(0);
                        ReadingInfo.RecordsMutex.ReleaseMutex();
                    }
                    
                }
            }
            
            return true;
        }

        public override void Initialise()
        {
            base.Initialise();
            GenThreadManager.StartThread(DeviceReaderId);
        }
    }

    public class DeviceManager_Inverter : DeviceManager_ActiveController<ActiveDevice_Generic>
    {
        public DeviceManager_Inverter(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
        }

        protected override ActiveDevice_Generic NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            if (dmDevice.DeviceType == DeviceType.Inverter)
            {
                ActiveDevice_Generic device = new ActiveDevice_Generic(this, dmDevice);
                return device;
            }
            else
                throw new NotImplementedException();
        }

    }
}
