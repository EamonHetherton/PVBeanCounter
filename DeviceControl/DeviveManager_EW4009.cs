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

    public class DeviceManager_EW4009 : DeviceManager_PassiveController<EW4009_Device, CC128_LiveRecord, CC128_HistoryRecord, CC128ManagerParams>
    {
        // max allowed difference between CC Meter Time and computer time in Minutes
        public const int MeterTimeSyncTolerance = 10;
        // Time sync difference that triggers warnings
        public const int MeterTimeSyncWarning = 5;
        private DateTime? MeterTimeSyncWarningTime = null;
        // Is current CC Meter Time in Sync with computer time
        private bool MeterTimeInSync = false;

        //private int DbInterval;
        private DateTime? LastRecordTime = null;
        private CC128ManagerParams ManagerParams;

        private CompositeAlgorithm_EW4009 DeviceAlgorithm;
        private int DatabaseInterval;

        public DeviceManager_EW4009(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
            AlgorithmParams aParams;
            aParams.Protocol = Protocol;
            aParams.EndianConverter16Bit = Protocol.EndianConverter16Bit;
            aParams.EndianConverter32Bit = Protocol.EndianConverter32Bit;
            mmSettings.CheckListenerDeviceId();
            aParams.BlockList = mmSettings.ListenerDeviceSettings.BlockList;
            aParams.AlgorithmList = mmSettings.ListenerDeviceSettings.AlgorithmList;
            aParams.DeviceName = mmSettings.ListenerDeviceSettings.Description;
            aParams.ErrorLogger = ErrorLogger;
            DeviceAlgorithm = new CompositeAlgorithm_EW4009(aParams);

            DatabaseInterval = mmSettings.DBIntervalInt;
        }

        public override void Initialise()
        {
            base.Initialise();
        }

        protected override void LoadParams()
        {
            ManagerParams = new CC128ManagerParams();
            ManagerParams.DeviceType = PVSettings.DeviceType.EnergyMeter;
            ManagerParams.RecordingInterval = DeviceManagerSettings.DBIntervalInt;
            ManagerParams.QueryInterval = DeviceManagerSettings.MessageIntervalInt;
            ManagerParams.HistoryHours = DeviceManagerSettings.HistoryHours.Value;
        }

        protected void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.DetailTrace)
        {
            GlobalSettings.LogMessage(routine, message, logEntryType);
        }

        protected override EW4009_Device NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            return new EW4009_Device(this, dmDevice);
        }

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
                            EW4009_Device device = DeviceList[index];
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

        protected void ProcessOneLiveRecord(MeterDevice<CC128_LiveRecord, CC128_HistoryRecord, CC128EnergyParams> device, CC128_LiveRecord liveRecord)
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

            DateTime curTime = DateTime.Now;
            bool dbWrite = (LastRecordTime == null
                || DeviceBase.IntervalCompare(ManagerParams.RecordingInterval, LastRecordTime.Value, curTime) != 0);

            device.ProcessOneLiveReading(liveRecord);
        }

        protected void ProcessOneHistoryRecord(MeterDevice<CC128_LiveRecord, CC128_HistoryRecord, CC128EnergyParams> device, CC128_HistoryRecord histRecord)
        {
            if (MeterTimeInSync && device.DeviceManagerDeviceSettings.UpdateHistory)
                device.ProcessOneHistoryReading(histRecord);
        }
    }
}
