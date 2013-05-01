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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;
using DeviceDataRecorders;
using Algorithms;

namespace Device
{
    public struct EstimateEnergy
    {
        private Double _EstimateEnergySumOfDeltas;
        private Double _ActualEnergySumOfDeltas;
        private Double _LastActualEnergy;
        private Double _LastEnergyDelta_Power;
        DateTime? LastEstTime;
        bool EnergyDropFound;

        bool HasStartOfDayDefect;
        TimeSpan QueryInterval;
        bool FirstActualFound;
        Double EnergyMargin;
        int CrazyDayStartMinutes;

        bool StartupStatusChecked;  // at initial energy calculation - extract startup state from current DeviceDetailPeriod

        Device.DeviceBase _Device;

        public EstimateEnergy(Device.DeviceBase device)
        {
            _Device = device;
            _EstimateEnergySumOfDeltas = 0.0;
            _ActualEnergySumOfDeltas = 0.0;
            _LastEnergyDelta_Power = 0.0;
            _LastActualEnergy = 0.0;
            EnergyMargin = 0.005;
            CrazyDayStartMinutes = 90;
            HasStartOfDayDefect = _Device.DeviceSettings.HasStartOfDayEnergyDefect;
            QueryInterval = TimeSpan.FromSeconds(_Device.DeviceManagerDeviceSettings.QueryIntervalInt);
            LastEstTime = null;
            EnergyDropFound = false;
            FirstActualFound = false;

            StartupStatusChecked = false;
        }

        private void CheckStartupStatus(DateTime curTime)
        {
            // retrieve period collection for the primary feature
            DeviceDetailPeriods_EnergyMeter days =
                (DeviceDetailPeriods_EnergyMeter)_Device.FindOrCreateFeaturePeriods(_Device.DeviceSettings.FeatureList[0].FeatureType, 
                _Device.DeviceSettings.FeatureList[0].FeatureId);
            // locate the required day period list
            DeviceDetailPeriod_EnergyMeter day = days.FindOrCreate(curTime);
        }

        public TimeSpan EstimateFromPower(Double powerWatts, DateTime curTime, Double? actualEnergy = null, Double? actualEnergyPrecision = null)
        {
            if (!StartupStatusChecked)
            {
                CheckStartupStatus(curTime);
                StartupStatusChecked = true;
            }

            TimeSpan duration;
            
            if (LastEstTime.HasValue)
                duration = (curTime - LastEstTime.Value);
            else
                duration = QueryInterval;
            LastEstTime = curTime;
            
            _LastEnergyDelta_Power = (powerWatts * duration.TotalHours) / 1000.0; // watts to KWH
            _EstimateEnergySumOfDeltas += _LastEnergyDelta_Power;

            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.SystemServices.LogMessage("EstimateEnergy", "EstimateFromPower - Time: " + curTime + " - Power: " + powerWatts +
                    " - Duration: " + duration.TotalSeconds + " - Energy Delta: " + _LastEnergyDelta_Power, LogEntryType.Trace);

            if (actualEnergyPrecision.HasValue)
                EnergyMargin = actualEnergyPrecision.Value / 2.0; // +/- half the precision value

            if (actualEnergy.HasValue)
            {
                // if HasStartOfDayDefect initial 
                if (!HasStartOfDayDefect || FirstActualFound)
                {
                    if (actualEnergy >= _LastActualEnergy)
                        _ActualEnergySumOfDeltas += (actualEnergy.Value - _LastActualEnergy);
                    else
                    {
                        EnergyDropFound = true;
                        _ActualEnergySumOfDeltas += _LastEnergyDelta_Power; // last power estimate is best substitute for the missing actual delta
                    }
                }
                else
                    FirstActualFound = true;

                _LastActualEnergy = actualEnergy.Value;

                // Ensure energy estimate based on power remains within the margin of the last reported actual energy sum of deltas
                if (_EstimateEnergySumOfDeltas > (_ActualEnergySumOfDeltas + EnergyMargin))
                    _EstimateEnergySumOfDeltas = (_ActualEnergySumOfDeltas + EnergyMargin);
                else if (_EstimateEnergySumOfDeltas < (_ActualEnergySumOfDeltas - EnergyMargin))
                    _EstimateEnergySumOfDeltas = _ActualEnergySumOfDeltas;
            }

            return duration;
        }

        public Double EstimateEnergySumOfDeltas { get { return _EstimateEnergySumOfDeltas; } }

        public Double ActualEnergySumOfDeltas { get { return _ActualEnergySumOfDeltas; } }

        public Double LastEnergyDelta_Power { get { return _LastEnergyDelta_Power; } }

        public Double LastActualEnergy { get { return _LastActualEnergy; } }
    }

    public class ActiveDevice_Generic : ActiveDevice
    {
        private InverterAlgorithm InverterAlgorithm { get { return (InverterAlgorithm)DeviceAlgorithm; } }

        private bool HasStartOfDayEnergyDefect;
        
        private bool UseEnergyTotal;                // Use EnergyTotal unless EnergyToday is available
        private bool UseEnergyTotalSet;
        //private bool DayIsEmpty;

        private EstimateEnergy EstEnergy;           // sum of energy deltas based upon spot power readings and checked with actuals
        
        public ActiveDevice_Generic(DeviceControl.DeviceManager_ActiveController<Device.ActiveDevice_Generic> deviceManager, DeviceManagerDeviceSettings deviceSettings)
            : base(deviceManager, deviceSettings, new InverterAlgorithm(deviceSettings, deviceManager.Protocol, deviceManager.ErrorLogger))
        {
            DeviceParams.DeviceType = deviceSettings.DeviceType;
            DeviceParams.QueryInterval = deviceSettings.QueryIntervalInt;
            DeviceParams.RecordingInterval = deviceSettings.DBIntervalInt;

            UseEnergyTotal = true;
            UseEnergyTotalSet = false;
            //DayIsEmpty = true;

            HasStartOfDayEnergyDefect = DeviceSettings.HasStartOfDayEnergyDefect;
            EstEnergy = new Device.EstimateEnergy(this);

            ResetDevice();
        }

        public override void ResetStartOfDay()
        {
            base.ResetStartOfDay();
            UseEnergyTotal = true;
            EstEnergy = new Device.EstimateEnergy(this);
            //DayIsEmpty = true;
        }

        protected override DeviceDetailPeriodsBase CreateNewPeriods(FeatureSettings featureSettings)
        {
            return new DeviceDetailPeriods_EnergyMeter(this, featureSettings, PeriodType.Day, TimeSpan.FromTicks(0));
        }

        public override DateTime NextRunTime
        {
            get
            {
                DateTime nextTime = LastRunTime + QueryInterval;
                TimeSpan? inverterStart = GlobalSettings.ApplicationSettings.InverterStartTime;
                if (inverterStart.HasValue && inverterStart.Value > nextTime.TimeOfDay)
                    return DateTime.Today + inverterStart.Value;

                TimeSpan? inverterStop = GlobalSettings.ApplicationSettings.InverterStopTime;
                if (inverterStop.HasValue && inverterStop.Value < nextTime.TimeOfDay)
                    return DateTime.Today + inverterStart.Value + TimeSpan.FromDays(1.0);

                return nextTime;
            }
        }

        private void ResetDevice()
        {
            DeviceId = null;

            Address = DeviceManagerDeviceSettings.Address;

            Manufacturer = "";
            Model = "";
            DeviceIdentifier = "";

            InverterAlgorithm.SetModel(Manufacturer);
            InverterAlgorithm.SetManufacturer(Model);
            InverterAlgorithm.SetSerialNo(DeviceIdentifier);
            InverterAlgorithm.SetAddress(Address);

            ClearAttributes();
        }

        private void ExtractFeatureReading(FeatureType featureType, uint featureId, DateTime curTime, bool newInterval)
        {
                                
            DeviceDetailPeriods_EnergyMeter days = (DeviceDetailPeriods_EnergyMeter)FindOrCreateFeaturePeriods(featureType, featureId);
            EnergyReading reading = new EnergyReading();

            if (LastRecordTime.HasValue)
                reading.Initialise(days, curTime, LastRecordTime.Value, false);
            else
                reading.Initialise(days, curTime, TimeSpan.FromSeconds(DeviceInterval), false);
                    
            reading.Mode = (Int16?)InverterAlgorithm.Status;
            reading.Temperature = (double?)InverterAlgorithm.Temperature;
            reading.ErrorCode = (uint?)InverterAlgorithm.ErrorCode;

            if (InverterAlgorithm.ErrorCodeHigh.HasValue)
                if (reading.ErrorCode.HasValue)
                    reading.ErrorCode += (UInt32)(InverterAlgorithm.ErrorCodeHigh.Value * 65536);
                else
                    reading.ErrorCode = (UInt32)(InverterAlgorithm.ErrorCodeHigh.Value * 65536);

            if (featureType == FeatureType.YieldAC || featureType == FeatureType.EnergyAC || featureType == FeatureType.ConsumptionAC)
            {
                if (featureId == 0)
                {
                    
                    reading.EnergyToday = (double?)InverterAlgorithm.EnergyTodayAC;
                    reading.EnergyTotal = (double?)InverterAlgorithm.EnergyTotalAC;
                    if (reading.EnergyTotal.HasValue && InverterAlgorithm.EnergyTotalACHigh.HasValue)
                        reading.EnergyTotal += (Double)(InverterAlgorithm.EnergyTotalACHigh.Value * 65536);

                    reading.EnergyDelta = EstEnergy.EstimateEnergySumOfDeltas;

                    reading.Power = (int?)InverterAlgorithm.PowerAC1;
                    reading.Volts = (float?)InverterAlgorithm.VoltsAC1;
                    reading.Amps = (float?)InverterAlgorithm.CurrentAC1;
                    reading.Frequency = (float?)InverterAlgorithm.Frequency;                   

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("ExtractFeatureReading - FeatureType: " + featureType + " - FeatureId: " + featureId
                            + " - EnergyToday: " + reading.EnergyToday
                            + " - EnergyTotal: " + reading.EnergyTotal
                            + " - CalculatedEnergy: " + EstEnergy.EstimateEnergySumOfDeltas
                            + " - Power: " + reading.Power
                            + " - Mode: " + reading.Mode
                            + " - FreqAC: " + reading.Frequency
                            + " - Volts: " + reading.Volts
                            + " - Current: " + reading.Amps
                            + " - Temperature: " + reading.Temperature
                            , LogEntryType.Trace);
                }
                else
                {
                    if (featureId == 1)
                    {
                        reading.Power = (int?)InverterAlgorithm.PowerAC2;
                        reading.Volts = (float?)InverterAlgorithm.VoltsAC2;
                        reading.Amps = (float?)InverterAlgorithm.CurrentAC2;
                        reading.Frequency = (float?)InverterAlgorithm.Frequency;
                    }
                    else if (featureId == 2)
                    {
                        reading.Power = (int?)InverterAlgorithm.PowerAC3;
                        reading.Volts = (float?)InverterAlgorithm.VoltsAC3;
                        reading.Amps = (float?)InverterAlgorithm.CurrentAC3;
                        reading.Frequency = (float?)InverterAlgorithm.Frequency;
                    }
                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("ExtractFeatureReading - FeatureType: " + featureType + " - FeatureId: " + featureId
                            + " - Power: " + reading.Power
                            + " - Mode: " + reading.Mode
                            + " - FreqAC: " + reading.Frequency
                            + " - Volts: " + reading.Volts
                            + " - Current: " + reading.Amps
                            + " - Temperature: " + reading.Temperature
                            , LogEntryType.Trace);
                }
            }
            else if (featureType == FeatureType.YieldDC || featureType == FeatureType.EnergyDC)
            {
                if (featureId == 0)
                {
                    reading.Volts = (float?)InverterAlgorithm.VoltsPV1;
                    reading.Amps = (float?)InverterAlgorithm.CurrentPV1;
                }
                else if (featureId == 1)
                {
                    reading.Volts = (float?)InverterAlgorithm.VoltsPV2;
                    reading.Amps = (float?)InverterAlgorithm.CurrentPV2;
                }
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("ExtractFeatureReading - FeatureType: " + featureType + " - FeatureId: " + featureId
                        + " - Mode: " + reading.Mode
                        + " - Volts: " + reading.Volts
                        + " - Current: " + reading.Amps
                        + " - Temperature: " + reading.Temperature
                        , LogEntryType.Trace);
            }
                                           
            days.AddRawReading(reading);

            if (newInterval)
            {
                days.UpdateDatabase(null, reading.ReadingEnd, false, PreviousDatabaseIntervalEnd);
                List<OutputReadyNotification> notificationList = new List<OutputReadyNotification>();
                BuildOutputReadyFeatureList(notificationList, featureType, featureId, reading.ReadingEnd);
                UpdateConsolidations(notificationList);
            }
        }

        public override bool DoExtractReadings()
        {
            if (FaultDetected)
                return false;

            bool res = false;
            bool alarmFound = false;
            bool errorFound = false;
            String stage = "Identity";
            try
            {
                ClearAttributes();

                if (!DeviceId.HasValue)
                {
                    res = InverterAlgorithm.ExtractIdentity();
                    if (!res)
                        return false;

                    Manufacturer = InverterAlgorithm.Manufacturer.Trim();
                    Model = InverterAlgorithm.Model.Trim();
                    DeviceIdentifier = InverterAlgorithm.SerialNo.Trim();
                    if (DeviceIdentifier == "")
                        DeviceIdentifier = DefaultSerialNo;
                    if (DeviceIdentifier == "")
                    {
                        LogMessage("DoExtractReadings - Identity - Manufacturer: " + Manufacturer
                            + " - Model: " + Model
                            + " : No Serial Number - Cannot record", LogEntryType.ErrorMessage);
                        return false;
                    }

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("DoExtractReadings - Identity - Manufacturer: " + Manufacturer
                            + " - Model: " + Model
                            + " - SerialNo: " + DeviceIdentifier
                            + " - DeviceId: " + DeviceId
                            + " - Energy Margin: " + InverterAlgorithm.EnergyMargin, LogEntryType.Trace);
                }

                stage = "Reading";

                DateTime curTime = DateTime.Now;
                bool dbWrite = (LastRecordTime == null
                    || DeviceBase.IntervalCompare(DatabaseInterval, LastRecordTime.Value, curTime) != 0);
                res = InverterAlgorithm.ExtractReading(dbWrite, ref alarmFound, ref errorFound);
                if (!res)
                    return false;

                TimeSpan duration;
                try
                {
                    if (!UseEnergyTotalSet)
                    {
                        UseEnergyTotal = !InverterAlgorithm.EnergyTodayAC.HasValue; // once set for a device it must not change
                        UseEnergyTotalSet = true;
                    }
                    else if (UseEnergyTotal == InverterAlgorithm.EnergyTodayAC.HasValue)
                    {
                        LogMessage("DoExtractReadings - UseEnergyTotal has flipped - was " + UseEnergyTotal, LogEntryType.ErrorMessage);
                        return false;
                    }

                    duration = EstEnergy.EstimateFromPower((double)InverterAlgorithm.PowerAC1.Value, curTime,
                        (double)(UseEnergyTotal ? InverterAlgorithm.EnergyTotalAC.Value : InverterAlgorithm.EnergyTodayAC.Value));
                }
                catch (Exception e)
                {
                    LogMessage("DoExtractReadings - Error calculating EstimateEnergy - probably no PowerAC retrieved - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    return false;
                }

                int curPower = (int)((InverterAlgorithm.PowerAC1.HasValue ? InverterAlgorithm.PowerAC1.Value : 0)
                    + (InverterAlgorithm.PowerAC2.HasValue ? (int)InverterAlgorithm.PowerAC2.Value : 0)
                    + (InverterAlgorithm.PowerAC3.HasValue ? (int)InverterAlgorithm.PowerAC3.Value : 0));

                GenericConnector.DBDateTimeGeneric readingEnd = new GenericConnector.DBDateTimeGeneric();
                readingEnd.Value = curTime; // reproduce reading date precision adjustment
                curTime = readingEnd.Value;

                if (dbWrite)
                {                                        
                    bool newInterval = IsNewdatabaseInterval(curTime);
                    foreach (FeatureSettings fs in DeviceSettings.FeatureList)
                        ExtractFeatureReading(fs.FeatureType, fs.FeatureId, curTime, newInterval);
                    LastRecordTime = curTime;
                }

                if (EmitEvents)
                {
                    stage = "energy";
                    EnergyEventStatus status = FindFeatureStatus(FeatureType.YieldAC, 0);
                    status.SetEventReading(curTime, 0.0, curPower, (int)duration.TotalSeconds, true);
                    DeviceManager.ManagerManager.EnergyEvents.ScanForEvents();
                }

                stage = "errors";
                if (alarmFound)
                    InverterAlgorithm.LogAlarm("Reading", curTime, DeviceManager);
                if (errorFound)
                    InverterAlgorithm.LogError("Reading", curTime, DeviceManager);

            }
            catch (Exception e)
            {
                LogMessage("DoExtractReadings - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            return res;
        }
    }
}

