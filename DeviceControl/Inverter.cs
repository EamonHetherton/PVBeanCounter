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
using DeviceDataRecorders;
using Algorithms;

namespace Device
{
    public class Inverter : ActiveDevice
    {
        private InverterAlgorithm InverterAlgorithm { get { return (InverterAlgorithm)DeviceAlgorithm; } }

        private FeatureSettings Feature_YieldAC;

        public Inverter(DeviceControl.DeviceManager_ActiveController<Device.Inverter> deviceManager, DeviceManagerDeviceSettings deviceSettings, PVSettings.DeviceType deviceType, string manufacturer, string model, string serialNo = "")
            : base(deviceManager, deviceSettings, new InverterAlgorithm(deviceSettings, deviceManager.Protocol, deviceManager.ErrorLogger), manufacturer, model, serialNo)
        {
            DeviceParams.DeviceType = deviceType;
            DeviceParams.QueryInterval = deviceSettings.QueryIntervalInt;
            DeviceParams.RecordingInterval = deviceSettings.DBIntervalInt;

            ResetDevice();
            Feature_YieldAC = DeviceSettings.GetFeatureSettings(FeatureType.YieldAC, deviceSettings.Feature);
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
                    duration = EstimateEnergy((double)InverterAlgorithm.PowerAC1.Value, curTime, (float)QueryInterval.TotalSeconds);
                }
                catch (Exception e)
                {
                    LogMessage("DoExtractReadings - Error calculating EstimateEnergy - probably no PowerAC retrieved - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    return false;
                }

                int curPower = InverterAlgorithm.PowerAC1.HasValue ? (int)InverterAlgorithm.PowerAC1.Value : 0;

                if (dbWrite)
                {
                    
                    DeviceDetailPeriods_EnergyMeter days = (DeviceDetailPeriods_EnergyMeter)FindOrCreateFeaturePeriods(Feature_YieldAC.FeatureType, Feature_YieldAC.FeatureId);
                    EnergyReading reading = new EnergyReading();
                    
                    reading.Initialise(days, curTime, 
                        LastRecordTime.HasValue ? (curTime - LastRecordTime.Value) : TimeSpan.FromSeconds(DeviceInterval), false);
                    LastRecordTime = curTime;

                    reading.EnergyToday = (double?)InverterAlgorithm.EnergyTodayAC;
                    reading.EnergyTotal = (double?)InverterAlgorithm.EnergyTotalAC;
                    if (reading.EnergyTotal.HasValue && InverterAlgorithm.EnergyTotalACHigh.HasValue)
                        reading.EnergyTotal += (Double)(InverterAlgorithm.EnergyTotalACHigh.Value * 65536);
                    reading.Power = (int?)InverterAlgorithm.PowerAC1;
                    reading.EnergyDelta = EstEnergy;
                    reading.Mode = (Int16?)InverterAlgorithm.Status;
                    //reading.PowerPV = (float?)InverterAlgorithm.PowerPV;
                    //reading.VoltsPV1 = (double?)InverterAlgorithm.VoltsPV1;
                    //reading.CurrentPV1 = (double?)InverterAlgorithm.CurrentPV1;
                    //reading.VoltsPV2 = (double?)InverterAlgorithm.VoltsPV2;
                    //reading.CurrentPV2 = (double?)InverterAlgorithm.CurrentPV2;
                    reading.Frequency = (float?)InverterAlgorithm.Frequency;
                    reading.Volts = (float?)InverterAlgorithm.VoltsAC1;
                    reading.Amps = (float?)InverterAlgorithm.CurrentAC1;                    
                    reading.Temperature = (double?)InverterAlgorithm.Temperature;
                    reading.ErrorCode = (uint?)InverterAlgorithm.ErrorCode;
                    if (InverterAlgorithm.ErrorCodeHigh.HasValue)
                        if (reading.ErrorCode.HasValue)
                            reading.ErrorCode += (UInt32)(InverterAlgorithm.ErrorCodeHigh.Value * 65536);
                        else
                            reading.ErrorCode = (UInt32)(InverterAlgorithm.ErrorCodeHigh.Value * 65536);

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("DoExtractReadings - Reading - EnergyToday: " + reading.EnergyToday
                            + " - EnergyTotal: " + reading.EnergyTotal
                            + " - CalculatedDelta: " + EstEnergy
                            + " - Power: " + reading.Power
                            + " - Mode: " + reading.Mode                            
                            //+ " - VoltsPV1: " + reading.VoltsPV1
                            //+ " - CurrentPV1: " + reading.CurrentPV1
                            //+ " - VoltsPV2: " + reading.VoltsPV2
                            //+ " - CurrentPV2: " + reading.CurrentPV2
                            + " - FreqAC: " + reading.Frequency
                            + " - Volts: " + reading.Volts
                            + " - Current: " + reading.Amps
                            + " - Temperature: " + reading.Temperature
                            , LogEntryType.Trace);

                    stage = "record";
                    
                    days.AddRawReading(reading);

                    if (IsNewdatabaseInterval(reading.ReadingEnd))
                    {
                        days.UpdateDatabase(null, reading.ReadingEnd, false, true);
                        List<OutputReadyNotification> notificationList = new List<OutputReadyNotification>();
                        BuildOutputReadyFeatureList(notificationList, FeatureType.YieldAC, 0, reading.ReadingEnd);
                        UpdateConsolidations(notificationList);
                    }
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

