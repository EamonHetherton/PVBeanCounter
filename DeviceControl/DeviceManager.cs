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

        // Allows devices to bypass other intermediate level overrides (including the one below) of NextRunTime
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
                TDevice device = NewDevice( dmDeviceSettings);

                DeviceList.Add(device);
            }
        }

        protected List<DateTime> FindDaysWithValues(DateTime? startDate)
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

                foreach (TDevice device in DeviceList)
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
    }

    public abstract class CommunicationDeviceManager<TDevice> : DeviceManagerTyped<TDevice> where TDevice : DeviceBase
    {
        internal SerialStream Stream { get; private set; }
        private bool ReaderStarted = false;

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(6); } }

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
            StopPortReader();
            StartPortReader();
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
                    if (PortName == null || PortName.Trim() == "")
                    {
                        LogMessage("StartPortReader - Port Name required for serial communication with device", LogEntryType.ErrorMessage);
                        Stream = null;
                        Protocol.SetDeviceStream(null);
                        return false;
                    }
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

    public abstract class DeviceManager_ActiveController<TDevice> : CommunicationDeviceManager<TDevice> where TDevice : ActiveDevice
    {
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

                // Some device managers create their own list of devices without the need for explicit config (eg SMA Sunny Explorer)
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
        public DeviceManager_Listener(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
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
