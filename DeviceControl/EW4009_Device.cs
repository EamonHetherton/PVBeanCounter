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
using PVBCInterfaces;
using PVSettings;
using Algorithms;
using DeviceDataRecorders;
using MackayFisher.Utilities;

namespace Device
{
    public struct EW4009_LiveRecord
    {
        public DateTime TimeStampe;
        public int Watts;
    }

    public class EW4009_Device : MeterDevice<EW4009_LiveRecord, EW4009_LiveRecord, CC128EnergyParams>
    {
        public FeatureSettings Feature_EnergyAC { get; protected set; }

        private Double EstEnergy = 0.0;

        public EW4009_Device(DeviceControl.DeviceManager_EW4009 deviceManager, DeviceManagerDeviceSettings deviceSettings)
            : base(deviceManager, deviceSettings, "Watts Clever", "EW4009", "")
        {
            DeviceParams = new CC128EnergyParams();
            DeviceParams.QueryInterval = deviceSettings.QueryIntervalInt;
            DeviceParams.RecordingInterval = deviceSettings.DBIntervalInt;

            DeviceParams.CalibrationFactor = deviceSettings.CalibrationFactor;
            Feature_EnergyAC = deviceSettings.DeviceSettings.GetFeatureSettings(FeatureType.EnergyAC, deviceSettings.Feature);
        }

        protected override DeviceDetailPeriodsBase CreateNewPeriods(FeatureSettings featureSettings)
        {
            return new DeviceDetailPeriods_EnergyMeter(this, featureSettings, PeriodType.Day, TimeSpan.FromTicks(0));
        }

        public override bool ProcessOneLiveReading(EW4009_LiveRecord liveReading)
        {
            if (FaultDetected)
                return false;

            bool res = false;

            int? minPower = null;
            int? maxPower = null;

            DeviceDetailPeriods_EnergyMeter days = null;

            String stage = "Identity";
            try
            {
                stage = "Reading";

                DateTime curTime = DateTime.Now;
                //bool dbWrite = (LastRecordTime == null
                //    || DeviceInfo.IntervalCompare(DatabaseInterval, LastRecordTime.Value, curTime) != 0);

                TimeSpan duration;
                int power = liveReading.Watts >= DeviceManagerDeviceSettings.ZeroThreshold ? liveReading.Watts : 0;
                try
                {
                    duration = EstimateEnergy((double)power, curTime, 6.0F);
                }
                catch (Exception e)
                {
                    LogMessage("ProcessOneLiveReading - Error calculating EstimateEnergy - probably no PowerAC retrieved - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    return false;
                }

                if (maxPower.HasValue)
                {
                    if (maxPower.Value < power)
                        maxPower = power;
                }
                else
                    maxPower = power;

                if (minPower.HasValue)
                {
                    if (minPower.Value > power)
                        minPower = power;
                }
                else
                    minPower = power;

                days = (DeviceDetailPeriods_EnergyMeter)FindOrCreateFeaturePeriods(Feature_EnergyAC.FeatureType, Feature_EnergyAC.FeatureId);

                EnergyReading reading = new EnergyReading();
                //liveReading.TimeStampe = DeviceBase.NormaliseReadingTime(liveReading.TimeStampe);

                if (LastRecordTime.HasValue)
                    reading.Initialise(days, liveReading.TimeStampe, LastRecordTime.Value, false);
                else
                    reading.Initialise(days, liveReading.TimeStampe, TimeSpan.FromSeconds(DeviceInterval), false);

                LastRecordTime = liveReading.TimeStampe;

                reading.EnergyCalibrationFactor = DeviceManagerDeviceSettings.CalibrationFactor;
                reading.EnergyToday = null;
                reading.EnergyTotal = null;
                reading.Power = power;
                reading.MinPower = minPower;
                reading.MaxPower = maxPower;
                reading.Mode = null;
                reading.Volts = null;
                reading.Amps = null;
                reading.Frequency = null;
                reading.Temperature = null;
                reading.ErrorCode = null;
                reading.EnergyDelta = EstEnergy; // EstEnergy is an accumulation from the contributing 6 sec power values
                //if (DeviceManagerDeviceSettings.CalibrationFactor != 1.0F)
                //    reading.CalibrationDelta = reading.CalibrateableReadingDelta * DeviceManagerDeviceSettings.CalibrationFactor;

                EstEnergy = 0.0;
                minPower = null;
                maxPower = null;

                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("ProcessOneLiveReading - Reading - Time: " + liveReading.TimeStampe + " - Duration: " + (int)reading.Duration.TotalSeconds 
                        + " - EstEnergy: " + EstEnergy
                        + " - Power: " + reading.Power                        
                        , LogEntryType.Trace);

                stage = "record";

                days.AddRawReading(reading);

                if (IsNewdatabaseInterval(reading.ReadingEnd))
                {
                    days.UpdateDatabase(null, reading.ReadingEnd, false, PreviousDatabaseIntervalEnd);
                    stage = "consolidate";
                    List<OutputReadyNotification> notificationList = new List<OutputReadyNotification>();
                    BuildOutputReadyFeatureList(notificationList, FeatureType.EnergyAC, 0, reading.ReadingEnd);
                    UpdateConsolidations(notificationList);
                }

                if (EmitEvents)
                {
                    stage = "events";
                    EnergyEventStatus status = FindFeatureStatus(FeatureType.EnergyAC, 0);
                    status.SetEventReading(curTime, 0.0, power, (int)duration.TotalSeconds, true);
                    DeviceManager.ManagerManager.EnergyEvents.ScanForEvents();
                }

                stage = "errors";
            }
            catch (Exception e)
            {
                LogMessage("ProcessOneLiveReading - Stage: " + stage + " - Time: " + liveReading.TimeStampe + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            return res;
        }

        // curTime is supplied if duration is to be calculated on the fly (live readings)
        // curTime is null if duration is from a history record - standardDuration contains the correct duration
        private DateTime? LastEstTime = null;
        private TimeSpan EstimateEnergy(Double powerWatts, DateTime curTime, float standardDuration)
        {
            TimeSpan duration;
            if (LastEstTime.HasValue)
                duration = (curTime - LastEstTime.Value);
            else
                duration = TimeSpan.FromSeconds(standardDuration);
            LastEstTime = curTime;

            Double newEnergy = (powerWatts * duration.TotalHours) / 1000.0; // watts to KWH
            EstEnergy += newEnergy;

            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.SystemServices.LogMessage("EstimateEnergy", "Time: " + curTime + " - Power: " + powerWatts +
                    " - Duration: " + duration.TotalSeconds + " - Energy: " + newEnergy, LogEntryType.Trace);

            return duration;
        }

        public override bool ProcessOneHistoryReading(EW4009_LiveRecord histReading)
        {
            throw new NotImplementedException("EW4009_Device.ProcessOneHistoryReading - Not Implemented");
        }

        public override void SplitReadingSub(ReadingBase oldReading, DateTime splitTime, ReadingBase newReading1, ReadingBase newReading2)
        {
        }
    }
}