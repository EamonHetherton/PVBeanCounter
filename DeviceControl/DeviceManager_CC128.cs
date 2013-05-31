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
using System.Xml.Linq;
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

namespace DeviceControl
{
    public class CC128ManagerParams : DeviceParamsBase
    {
        public int HistoryHours { get; set; }

        public CC128ManagerParams()
        {
            //DeviceType = PVSettings.DeviceType.EnergyMeter;
            //QueryInterval = 6;
            //RecordingInterval = 60;
            HistoryHours = 24;
            //EnforceRecordingInterval = true;
        }
    }

    public class DeviceManager_CC128 : DeviceManager_Listener<MeterDevice<CC128_LiveRecord, CC128_HistoryRecord, CC128EnergyParams>, CC128_LiveRecord, CC128_HistoryRecord, CC128ManagerParams>
    {
        // max allowed difference between CC Meter Time and computer time in Minutes
        public const int MeterTimeSyncTolerance = 10;
        // Time sync difference that triggers warnings
        public const int MeterTimeSyncWarning = 5;
        private DateTime? MeterTimeSyncWarningTime = null;
        // Is current CC Meter Time in Sync with computer time
        private bool MeterTimeInSync = false;

        private CC128ManagerParams ManagerParams;

        public DeviceManager_CC128(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings, IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
        }

        public override void Initialise()
        {
            base.Initialise();
            DeviceManager_Listener_Reader<CC128ManagerParams> DeviceReader = GetReader(GenThreadManager);
            int deviceReaderId = GenThreadManager.AddThread(DeviceReader);
            GenThreadManager.StartThread(deviceReaderId);
        }

        protected override void LoadParams()
        {
            ManagerParams = new CC128ManagerParams();
            //ManagerParams.DeviceType = PVSettings.DeviceType.EnergyMeter;
            //ManagerParams.RecordingInterval = DeviceManagerSettings.DBIntervalInt;
            //ManagerParams.QueryInterval = DeviceManagerSettings.MessageIntervalInt;
            ManagerParams.HistoryHours = DeviceManagerSettings.HistoryHours.Value;
        }

        protected override DeviceManager_Listener_Reader<CC128ManagerParams> GetReader(GenThreadManager threadManager)
        {
            return (DeviceManager_Listener_Reader<CC128ManagerParams>)new DeviceManager_CC128_Reader(this, threadManager, DeviceManagerSettings, ReadingInfo, ManagerParams);
        }

        protected void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.DetailTrace)
        {
            GlobalSettings.LogMessage(routine, message, logEntryType);
        }

        protected override MeterDevice<CC128_LiveRecord, CC128_HistoryRecord, CC128EnergyParams> NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            return new CC128_Device(this, dmDevice);
        }

        protected override void ProcessOneLiveRecord(MeterDevice<CC128_LiveRecord, CC128_HistoryRecord, CC128EnergyParams> device, CC128_LiveRecord liveRecord)
        {
            // DMF *******
            // Remove the following when not using DEBUG
            // Debug causes a rush of records less than a millisecond apart when execution is paused for multiple "intervals"
#if DEBUG
            liveRecord.TimeStampe = liveRecord.MeterTime;
#endif
            // DMF *******

            bool curSyncStatus = MeterTimeInSync;
            int timeError = Convert.ToInt32(Math.Abs((liveRecord.MeterTime - liveRecord.TimeStampe).TotalMinutes));
            MeterTimeInSync = timeError <= MeterTimeSyncTolerance;

            // Issue warnings every hour if above warning threshold but below max tolerance
            if (MeterTimeInSync)
                if (timeError >= MeterTimeSyncWarning)
                {
                    if (MeterTimeSyncWarningTime == null || (MeterTimeSyncWarningTime + TimeSpan.FromHours(1.0)) < DateTime.Now)
                    {
                        LogMessage("ProcessOneRecord", "Meter time variance at WARNING threshold: " + timeError +
                        " minutes - History update is unreliable", LogEntryType.Information);
                        MeterTimeSyncWarningTime = DateTime.Now;
                    }
                }
                else
                    MeterTimeSyncWarningTime = null;

            // Log transitions across max tolerance threshold
            if (MeterTimeInSync != curSyncStatus)
                if (MeterTimeInSync)
                    LogMessage("ProcessOneRecord", "Meter time variance within tolerance: " + timeError +
                        " minutes - History adjust available", LogEntryType.Information);
                else
                    LogMessage("ProcessOneRecord", "Meter time variance exceeds tolerance: " + timeError +
                        " minutes - History adjust disabled", LogEntryType.Information);

            device.ProcessOneLiveReading(liveRecord);
        }

        protected override void ProcessOneHistoryRecord(MeterDevice<CC128_LiveRecord, CC128_HistoryRecord, CC128EnergyParams> device, CC128_HistoryRecord histRecord)
        {
            if (MeterTimeInSync && device.DeviceManagerDeviceSettings.UpdateHistory)
                device.ProcessOneHistoryReading(histRecord);
        }
    }
}
