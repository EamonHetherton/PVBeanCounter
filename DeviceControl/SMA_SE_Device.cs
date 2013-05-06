/*
* Copyright (c) 2013 Dennis Mackay-Fisher
*
* This file is part of PVService
* 
* PVService is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PVService is distributed in the hope that it will be useful,
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
using PVBCInterfaces;
using PVSettings;
using Algorithms;
using DeviceDataRecorders;
using MackayFisher.Utilities;

namespace Device
{
    public struct SMA_SE_Record
    {
        public DateTime TimeStampe;
        public int Seconds;
        public int Watts;
    }

    public class SMA_SE_EnergyParams : DeviceParamsBase
    {
        public SMA_SE_EnergyParams()
            : base()
        {
        }
    }

    public class SMA_SE_Device : MeterDevice<SMA_SE_Record, SMA_SE_Record, SMA_SE_EnergyParams>
    {
        private FeatureSettings Feature_YieldAC;

        public SMA_SE_Device(DeviceControl.DeviceManager_SMA_SunnyExplorer deviceManager, DeviceManagerDeviceSettings deviceSettings, string model, string serialNo)
            : base(deviceManager, deviceSettings, "SMA", model, serialNo)
        {
            DeviceParams = new EnergyParams();
            DeviceParams.DeviceType = PVSettings.DeviceType.EnergyMeter;            
            DeviceParams.QueryInterval = deviceSettings.QueryIntervalInt;
            DeviceParams.RecordingInterval = deviceSettings.DBIntervalInt;

            DeviceParams.CalibrationFactor = deviceSettings.CalibrationFactor;
            Feature_YieldAC = DeviceSettings.GetFeatureSettings(FeatureType.YieldAC, deviceSettings.Feature);
        }

        protected override DeviceDetailPeriodsBase CreateNewPeriods(FeatureSettings featureSettings)
        {
            return new DeviceDetailPeriods_EnergyMeter(this, featureSettings, PeriodType.Day, TimeSpan.FromTicks(0));
        }

        public DeviceDataRecorders.DeviceDetailPeriods_EnergyMeter Days 
        {
            get
            {
                return (DeviceDataRecorders.DeviceDetailPeriods_EnergyMeter)FindOrCreateFeaturePeriods(Feature_YieldAC.FeatureType, Feature_YieldAC.FeatureId);
            }
        }

        private bool ProcessOneReading(SMA_SE_Record liveReading, bool isLive)
        {
            if (FaultDetected)
                return false;

            bool res = false;

            int? minPower = null;
            int? maxPower = null;

            String stage = "Identity";
            try
            {
                stage = "Reading";

                TimeSpan duration;
                Double estEnergy = 0.0;
                try
                {
                    duration = TimeSpan.FromSeconds(liveReading.Seconds);
                    estEnergy = (((double)liveReading.Watts) * duration.TotalHours) / 1000.0; // watts to KWH
                }
                catch (Exception e)
                {
                    LogMessage("ProcessOneReading - Error calculating EstimateEnergy - probably no PowerAC retrieved - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    return false;
                }

                if (maxPower.HasValue)
                {
                    if (maxPower.Value < liveReading.Watts)
                        maxPower = liveReading.Watts;
                }
                else
                    maxPower = liveReading.Watts;

                if (minPower.HasValue)
                {
                    if (minPower.Value > liveReading.Watts)
                        minPower = liveReading.Watts;
                }
                else
                    minPower = liveReading.Watts;
               
                DeviceDetailPeriods_EnergyMeter days = (DeviceDetailPeriods_EnergyMeter)FindOrCreateFeaturePeriods(Feature_YieldAC.FeatureType, Feature_YieldAC.FeatureId);
                    
                EnergyReading reading = new EnergyReading();

                reading.Initialise(days, liveReading.TimeStampe, 
                    TimeSpan.FromSeconds((double)DeviceInterval), false);  // SE is always 5 minute readings
                LastRecordTime = liveReading.TimeStampe;

                reading.EnergyToday = null;
                reading.EnergyTotal = null;
                reading.Power = liveReading.Watts;
                reading.MinPower = minPower;
                reading.MaxPower = maxPower;
                reading.Mode = null;
                reading.Volts = null;
                reading.Amps = null;
                reading.Frequency = null;
                reading.ErrorCode = null;
                reading.EnergyDelta = estEnergy; 

                minPower = null;
                maxPower = null;

                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("ProcessOneReading - Reading - Time: " + liveReading.TimeStampe
                        + " - EnergyDelta: " + reading.EnergyDelta
                        + " - Power: " + reading.Power + " - " + (isLive ? "Live" : "History")
                        , LogEntryType.Trace);

                stage = "record";

                reading.AddReadingMatch = true;
                days.AddRawReading(reading, true);
                
                List<OutputReadyNotification> notificationList = new List<OutputReadyNotification>();
                BuildOutputReadyFeatureList(notificationList, FeatureType.YieldAC, 0, reading.ReadingEnd);
                if (isLive)
                    UpdateConsolidations(notificationList);

                if (isLive && EmitEvents)
                {
                    stage = "energy";
                    EnergyEventStatus status = FindFeatureStatus(FeatureType.YieldAC, 0);
                    status.SetEventReading(DateTime.Now, 0.0, liveReading.Watts, (int)duration.TotalSeconds, true);
                    DeviceManager.ManagerManager.EnergyEvents.ScanForEvents();
                }

                stage = "errors";
            }
            catch (Exception e)
            {
                LogMessage("ProcessOneReading - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            return res;
        }

        public override bool ProcessOneLiveReading(SMA_SE_Record liveReading)
        {
            // used for the latest reading
            return ProcessOneReading(liveReading, true);
        }

        public override bool ProcessOneHistoryReading(SMA_SE_Record histReading)
        {
            // used for readings older than the latest
            return ProcessOneReading(histReading, false);
        }

        public override void SplitReadingSub(ReadingBase oldReading, DateTime splitTime, ReadingBase newReading1, ReadingBase newReading2)
        {
            if (((EnergyReading)newReading1).EnergyToday.HasValue)
                ((EnergyReading)newReading1).EnergyToday -= ((EnergyReading)newReading2).EnergyDelta;
            if (((EnergyReading)newReading1).EnergyTotal.HasValue)
                ((EnergyReading)newReading1).EnergyTotal -= ((EnergyReading)newReading2).EnergyDelta;
        }
    }
}
