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
using PVBCInterfaces;
using PVSettings;
using Algorithms;
using DeviceDataRecorders;
using MackayFisher.Utilities;

namespace Device
{


    public struct CC128_LiveRecord
    {
        public DateTime MeterTime;
        public DateTime TimeStampe;
        public int Watts;
        public double Temperature;
    }

    public struct CC128_HistoryRecord
    {
        public uint Sensor;
        public DateTime Time;
        public Double Energy;           // original energy reading, or value from fill gaps correction
        public Double? Calculated;      // energy value above adjusted by calibration and / or history pro-rata adjustments
        public int Duration;
        public Double? Temperature;
        public int Count;
        public int? MinPower;
        public int? MaxPower;
        public bool? InRange;
    }

    public class CC128EnergyParams : EnergyParams
    {        
        public CC128EnergyParams()
            : base()
        {            
        }
    }

    public class CC128_Device : MeterDevice<CC128_LiveRecord, CC128_HistoryRecord, CC128EnergyParams>
    {
        public FeatureSettings Feature_EnergyAC { get; protected set; }

        public CC128_Device(DeviceControl.DeviceManager_CC128 deviceManager, DeviceManagerDeviceSettings deviceSettings)
            : base(deviceManager, deviceSettings, "CurrentCost", "CC128", "")
        {
            DeviceParams = new CC128EnergyParams();
            DeviceParams.DeviceType = PVSettings.DeviceType.EnergyMeter;            
            DeviceParams.QueryInterval = deviceSettings.QueryIntervalInt;
            DeviceParams.RecordingInterval = deviceSettings.DBIntervalInt;

            DeviceParams.CalibrationFactor = deviceSettings.CalibrationFactor;
            Feature_EnergyAC = deviceSettings.DeviceSettings.GetFeatureSettings(FeatureType.EnergyAC, deviceSettings.Feature);
        }

        protected override DeviceDetailPeriodsBase CreateNewPeriods(FeatureSettings featureSettings)
        {
            return new DeviceDetailPeriods_EnergyMeter(this, featureSettings, PeriodType.Day, TimeSpan.FromTicks(0));
        }

        public override bool ProcessOneLiveReading(CC128_LiveRecord liveReading)      
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
                try
                {
                    duration = EstimateEnergy((double)liveReading.Watts, curTime, 6.0F);
                }
                catch (Exception e)
                {
                    LogMessage("ProcessOneLiveReading - Error calculating EstimateEnergy - probably no PowerAC retrieved - Exception: " + e.Message, LogEntryType.ErrorMessage);
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

                if (!DeviceId.HasValue)
                    SetDeviceFeature(Feature_EnergyAC, PVSettings.MeasureType.Energy, null, true, true, false);

                //if (dbWrite)
                {
                    days = (DeviceDetailPeriods_EnergyMeter)FindOrCreateFeaturePeriods(Feature_EnergyAC.Type, Feature_EnergyAC.Id);
                    
                    EnergyReading reading = new EnergyReading();
                    
                    reading.Initialise(days, liveReading.TimeStampe, 
                        TimeSpan.FromSeconds(
                        LastRecordTime.HasValue ? (double)(liveReading.TimeStampe - LastRecordTime.Value).TotalSeconds : (double)DeviceInterval), false, (EnergyParams)DeviceParams);
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
                    reading.Temperature = (double)liveReading.Temperature;
                    reading.ErrorCode = null;
                    reading.EnergyDelta = EstEnergy; // EstEnergy is an accumulation from the contributing 6 sec power values

                    EstEnergy = 0.0;
                    minPower = null;
                    maxPower = null;
                                        
                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("ProcessOneLiveReading - Reading - Time: " + liveReading.TimeStampe + " - Duration: " + (int) reading.Duration.TotalSeconds + " - EnergyToday: " + reading.EnergyToday
                            + " - EnergyTotal: " + reading.EnergyTotal
                            + " - EstEnergy: " + EstEnergy
                            + " - Power: " + reading.Power
                            + " - Mode: " + reading.Mode
                            + " - FreqAC: " + reading.Frequency
                            + " - Volts: " + reading.Volts
                            + " - Current: " + reading.Amps
                            + " - Temperature: " + reading.Temperature
                            , LogEntryType.Trace);

                    stage = "record";

                    days.AddRawReading(reading);

                    if (IsNewdatabaseInterval(reading.ReadingEnd))
                    {
                        days.UpdateDatabase(null, reading.ReadingEnd);
                        stage = "consolidate";
                        List<OutputReadyNotification> notificationList = new List<OutputReadyNotification>();
                        BuildOutputReadyFeatureList(notificationList, FeatureType.EnergyAC, 0, reading.ReadingEnd);
                        UpdateConsolidations(notificationList);
                    }
                }

                if (EmitEvents)
                {
                    stage = "energy";
                    EnergyEventStatus status = FindFeatureStatus(FeatureType.EnergyAC, 0);
                    status.SetEventReading(curTime, 0.0, liveReading.Watts, (int)duration.TotalSeconds, true);
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

        public override bool ProcessOneHistoryReading(CC128_HistoryRecord histReading)
        {
            String stage = "Initial";
            try
            {
                DeviceDetailPeriods_EnergyMeter days = (DeviceDetailPeriods_EnergyMeter)FindOrCreateFeaturePeriods(Feature_EnergyAC.Type, Feature_EnergyAC.Id);
                DeviceDetailPeriod_EnergyMeter day = days.FindOrCreate(histReading.Time.Date);

                EnergyReading hist = new EnergyReading(days, histReading.Time, TimeSpan.FromSeconds(7200.0), (EnergyParams)DeviceParams);
                hist.EnergyDelta = histReading.Energy;
                hist.Temperature = histReading.Temperature;

                day.AdjustFromHistory(hist, 7200.0F);

                return true;
            }
            catch (Exception e)
            {
                LogMessage("ProcessOneHistoryReading - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }            
        }

    }
}