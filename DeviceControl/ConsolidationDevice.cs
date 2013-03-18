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
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;
using GenericConnector;
using DeviceDataRecorders;

namespace Device
{
    public class DeviceLink
    {
        public DeviceBase FromDevice;
        public FeatureType FromFeatureType;
        public uint FromFeatureId;
        public ConsolidationDevice ToDevice;
        public FeatureType ToFeatureType;
        public uint ToFeatureId;
        public ConsolidateDeviceSettings.OperationType Operation;

        public DateTime LastReadyTime = DateTime.MinValue;
        public EnergyEventStatus FromEventStatus;
        public EnergyEventStatus ToEventStatus = null;

        public bool SourceUpdated = true;

        public DeviceLink(DeviceBase fromDevice, FeatureType fromFeatureType, uint fromFeatureId, 
            ConsolidationDevice toDevice, FeatureType toFeatureType, uint toFeatureId,
            ConsolidateDeviceSettings.OperationType operation, EnergyEventStatus fromEventStatus)
        {
            FromDevice = fromDevice;
            FromFeatureType = fromFeatureType;
            FromFeatureId = fromFeatureId;
            ToDevice = toDevice;
            ToFeatureType = toFeatureType;
            ToFeatureId = toFeatureId;
            Operation = operation;
            FromEventStatus = fromEventStatus;
            FromEventStatus.ToDeviceLinks.Add(this);
        }
    }

    public abstract class ConsolidationDevice : DeviceBase
    {
        public List<DeviceLink> SourceDevices;
        public PeriodType PeriodType { get; private set; }

        public ConsolidationDevice(DeviceControl.DeviceManagerBase deviceManager, DeviceManagerDeviceSettings settings, PeriodType periodType = PeriodType.Day)
            : base(deviceManager, settings)
        {
            Manufacturer = settings.Manufacturer;
            Model = settings.Model;
            DeviceIdentifier = settings.SerialNo;
            SourceDevices = new List<DeviceLink>();
            PeriodType = periodType;
        }

        public bool SourceUpdated
        {
            get
            {
                foreach (DeviceLink l in SourceDevices)
                {
                    if (l.SourceUpdated)
                        return true;
                }
                return false;
            }
        }

        public override DateTime NextRunTime
        {
            get { throw new NotSupportedException(); }
        }

        public void AddSourceDevice(DeviceLink deviceLink)
        {
            // ensure it references this object
            deviceLink.ToDevice = this;
            if (deviceLink.FromDevice.GetType().IsSubclassOf(typeof(ConsolidationDevice)) && FindInTargetList((ConsolidationDevice)deviceLink.FromDevice))
            {
                GlobalSettings.LogMessage("Consolidation.AddSourceDevice", " Recursion detected - Device: " +
                    deviceLink.FromDevice.DeviceIdentifier + " - Already in target colsolidation hierarchy on " + 
                    this.DeviceIdentifier + " - ignored", LogEntryType.ErrorMessage);
                return;
            }
            deviceLink.ToEventStatus = FindFeatureStatus(deviceLink.ToFeatureType, deviceLink.ToFeatureId);
            deviceLink.ToEventStatus.ToDeviceLinks.Add(deviceLink);
            SourceDevices.Add(deviceLink);
            deviceLink.FromDevice.AddTargetDevice(deviceLink);
        }

        public void NotifyConsolidation()
        {
            // Called by subordinate device when that device has a complete set of readings

            List<OutputReadyNotification> notifyList = new List<OutputReadyNotification>(); ;
            bool ready = true;
            bool found = false;
            bool someReady = false;
            DateTime lastTime = LastRecordTime.HasValue ? LastRecordTime.Value : DateTime.MinValue;
            DateTime newTime = lastTime;
            // scan source links - are all source features updated
            // find lowest new date in the features - this is the new LastRecordTime if all are updated
            foreach (DeviceLink sLink in SourceDevices)
            {
                found = true;
                if (sLink.LastReadyTime > lastTime)
                {
                    ready &= true;
                    someReady = true;
                    if (newTime == lastTime)
                        newTime = sLink.LastReadyTime;
                    else if (sLink.LastReadyTime < newTime)
                        newTime = sLink.LastReadyTime;
                }
                else
                    ready = false;
            }

            // found some features and all found are ready
            if (found && ready 
                || someReady && (LastRecordTime.HasValue ? LastRecordTime.Value : DateTime.MinValue) < DateTime.Now.AddMinutes(-15.0))
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("NotifyConsolidation - Found: " + found + " - Ready: " + ready + " - Some Ready: " + someReady +
                        " - LastRecordTime: " + LastRecordTime, LogEntryType.Trace);

                LastRecordTime = newTime;
                if (DeviceManagerDeviceSettings.ConsolidationType == ConsolidationType.PVOutput && DeviceManagerDeviceSettings.PVOutputSystem != "")
                    DeviceManager.ManagerManager.SetOutputReady(DeviceManagerDeviceSettings.PVOutputSystem);
                // push update notifications up the consolidation hierarchy
                foreach (DeviceLink sLink in SourceDevices)                
                    BuildOutputReadyFeatureList(notifyList, sLink.ToFeatureType, sLink.ToFeatureId, newTime);
                UpdateConsolidations(notifyList);
            }
        }

        public override DeviceDetailPeriodBase FindOrCreateFeaturePeriod(FeatureType featureType, uint featureId, DateTime periodStart)
        {
            DeviceDetailPeriodsBase periodsBase = FindOrCreateFeaturePeriods(featureType, featureId);
            return periodsBase.FindOrCreate(periodStart);
        }
    }

    public class EnergyConsolidationParams : EnergyParams
    {        
        public override int QueryInterval { get{ return RecordingInterval; } }
        
        public EnergyConsolidationParams(int recordingInterval)
        {
            DeviceType = PVSettings.DeviceType.Unknown;
            RecordingInterval = recordingInterval;
            EnforceRecordingInterval = true; // Consolidations are always aligned to formal intervals
        }
    }

    public class EnergyConsolidationDevice : ConsolidationDevice
    {
        public EnergyConsolidationDevice(DeviceControl.DeviceManagerBase deviceManager, DeviceManagerDeviceSettings settings, PeriodType periodType = PeriodType.Day)
            : base(deviceManager, settings, periodType)
        {
            int interval;
            if (settings.ConsolidationType == ConsolidationType.PVOutput)
            {
                PvOutputSiteSettings pvo = GlobalSettings.ApplicationSettings.FindPVOutputBySystemId(settings.PVOutputSystem);
                if (pvo == null)
                    throw new Exception("EnergyConsolidationDevice - Cannot find PVOutput system - " + settings.PVOutputSystem);
                interval = pvo.DataIntervalSeconds;
            }
            else
                interval = settings.DBIntervalInt;

            DeviceParams = new EnergyConsolidationParams(interval);
        }

        protected override DeviceDetailPeriodsBase CreateNewPeriods(FeatureSettings featureSettings)
        {
            return new DeviceDetailPeriods_EnergyConsolidation(this, featureSettings, PeriodType, TimeSpan.FromTicks(0));
        }

    }

}
