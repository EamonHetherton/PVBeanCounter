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
using GenericConnector;
using PVBCInterfaces;
using MackayFisher.Utilities;
using PVSettings;

namespace DeviceDataRecorders
{
    public class DeviceDetailPeriod_EnergyMeter : DeviceDetailPeriod_Physical<EnergyReading, EnergyReading>, IComparable<DeviceDetailPeriod_EnergyMeter>
    {
        public DeviceDetailPeriod_EnergyMeter(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature)
            : base(deviceDetailPeriods, periodType, periodStart, feature)
        {
        }

        public int CompareTo(DeviceDetailPeriod_EnergyMeter other)
        {
            return base.CompareTo(other);
        }

        private static String SelectDeviceReading_Energy =
            "select " +
                "ReadingEnd, ReadingStart, EnergyTotal, EnergyToday, EnergyDelta, " +
                "CalcEnergyDelta, HistEnergyDelta, Mode, ErrorCode, Power, Volts, Amps, Frequency, Temperature, " +
                "MinPower, MaxPower " +
            "FROM devicereading_energy " +
            "WHERE " +
                "ReadingEnd > @PeriodStart " +
                "AND ReadingEnd <= @NextPeriodStart " +
                "AND DeviceFeature_Id = @DeviceFeature_Id ";

        public override void LoadPeriodFromDatabase(GenConnection existingCon = null)
        {
            ClearReadings();

            String stage = "Initial";
            GenConnection con = existingCon;
            try
            {
                if (con == null)
                    con = GlobalSettings.TheDB.NewConnection();

                if (con == null)
                    GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.LoadPeriodFromDatabase", "*******con is null", LogEntryType.ErrorMessage);

                stage = "Before cmd setup";
                GenCommand cmd;
                cmd = new GenCommand(SelectDeviceReading_Energy, con);

                if (cmd == null)
                    GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.LoadPeriodFromDatabase", "*******cmd is null", LogEntryType.ErrorMessage);

                stage = "Before BindSelectIdentity";
                BindSelectIdentity(cmd);

                stage = "Before cmd.ExecuteReader";
                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();

                if (dr == null)
                    GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.LoadPeriodFromDatabase", "*******dr is null", LogEntryType.ErrorMessage);

                stage = "Before while";
                while (dr.Read())
                {
                    stage = "loop start";

                    DateTime endTime = dr.GetDateTime(0);
                    DateTime startTime = dr.GetDateTime(1);

                    // select included extra readings that probably do not apply
                    // allows for readings to cross period boundaries by up to PeriodOverlapLimit
                    // discard rows with no overlap at all
                    stage = "Before endTime check";
                    if (endTime <= Start)
                        continue;
                    stage = "Before startTime check";
                    if (startTime >= End)
                        continue;

                    stage = "Before new EnergyReading";
                    EnergyReading newRec = new EnergyReading();

                    if (newRec == null)
                        GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.LoadPeriodFromDatabase", "*******newRec is null", LogEntryType.ErrorMessage);

                    stage = "Before Initialise";
                    newRec.Initialise(DeviceDetailPeriods, endTime, startTime, true);

                    stage = "EnergyTotal";
                    newRec.EnergyTotal = dr.GetNullableDouble(2, EnergyReading.EnergyPrecision);
                    stage = "EnergyToday";
                    newRec.EnergyToday = dr.GetNullableDouble(3, EnergyReading.EnergyPrecision);
                    stage = "EnergyDelta";
                    newRec.EnergyDeltaNullable = dr.GetNullableDouble(4, EnergyReading.EnergyPrecision);
                    stage = "CalibrationDelta";
                    newRec.CalibrationDelta = dr.GetNullableDouble(5, EnergyReading.EnergyPrecision);
                    stage = "HistEnergyDelta";
                    newRec.HistEnergyDelta = dr.GetNullableDouble(6, EnergyReading.EnergyPrecision);
                    stage = "Mode";
                    newRec.Mode = dr.GetNullableInt32(7);
                    stage = "ErrorCode";
                    newRec.ErrorCode = dr.GetNullableInt64(8);
                    stage = "Power";
                    newRec.Power = dr.GetNullableInt32(9);
                    stage = "Volts";
                    newRec.Volts = dr.GetNullableFloat(10, EnergyReading.EnergyPrecision);
                    stage = "Amps";
                    newRec.Amps = dr.GetNullableFloat(11, EnergyReading.EnergyPrecision);
                    stage = "Frequency";
                    newRec.Frequency = dr.GetNullableFloat(12, EnergyReading.EnergyPrecision);
                    stage = "Temperature";
                    newRec.Temperature = dr.GetNullableFloat(13, EnergyReading.EnergyPrecision);
                    stage = "MinPower";
                    newRec.MinPower = dr.GetNullableInt32(14);
                    stage = "MaxPower";
                    newRec.MaxPower = dr.GetNullableInt32(15);

                    stage = "Before SetRestoreComplete";
                    newRec.SetRestoreComplete();

                    stage = "Before AddReading";
                    AddReading(newRec, AddReadingType.Database);
                }

                stage = "Before close and dispose";
                dr.Close();
                dr.Dispose();
                dr = null;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.LoadPeriodFromDatabase", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (existingCon == null && con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        protected override void SplitReadingSub(EnergyReading oldReading, DateTime splitTime, EnergyReading newReading1, EnergyReading newReading2)
        {
            if (newReading1.EnergyToday.HasValue)
                newReading1.EnergyToday -= newReading2.EnergyDelta;
            if (newReading1.EnergyTotal.HasValue)
                newReading1.EnergyTotal -= newReading2.EnergyDelta;
        }

        protected override EnergyReading NewReading(DateTime outputTime, TimeSpan duration, EnergyReading pattern = null)
        {
            EnergyReading newEnergyReading;

            if (pattern == null)
                newEnergyReading = new EnergyReading(DeviceDetailPeriods, outputTime, duration);
            else
            {
                newEnergyReading = pattern.Clone(outputTime, duration);
                newEnergyReading.ResetEnergyDelta();
                newEnergyReading.HistEnergyDelta = null;
            }

            newEnergyReading.EnergyCalibrationFactor = DeviceParams.CalibrationFactor;

            return newEnergyReading;
        }

        /*
        private void NormalisePeriod( bool useDeltaAtStart)
        {
            String stage = "start";

            try
            {
                GlobalSettings.SystemServices.LogMessage("NormalisePeriod", "Period Start: " + Start + " - for Device Id: " + DeviceId, LogEntryType.Trace);

                bool isFirst = true;

                Double prevInvEnergy = 0.0;
                Double currentInvEnergy = 0.0;
                Double currentInvDelta = 0.0;

                Double currentEnergyEstimate = 0.0;     // total of interval estimates for today
                Double prevEnergyEstimate = 0.0;     // previous total of interval estimates for today
                Double currentEstimateDelta = 0.0;

                Double energyRecorded = 0.0;
                Double prevEnergyRecorded = 0.0;

                Int32? power = null;
                Int32 minPower = 0;
                Int32 maxPower = 0;

                Double? temperature = null;
                DateTime prevIntervalTime = DateTime.Today;
                DateTime currentIntervalTime = DateTime.Today;

                bool useEnergyTotal = true; // = this.DeviceDetailPeriods.Device.DeviceSettings.HasStartOfDayEnergyDefect;

                stage = "enter loop";
                foreach(EnergyReading reading in this.ReadingsGeneric.ReadingList)
                {
                    stage = "loop 1";
                    DateTime thisTime = reading.ReadingEnd;

                    //DateTime thisIntervalTime = thisTime.Date + TimeSpan.FromMinutes(((((int)thisTime.TimeOfDay.TotalMinutes) + 4) / 5) * 5);

                    Double thisInvEnergy = 0.0;

                    bool todayNull = !reading.EnergyToday.HasValue;
                    Double estEnergy; //= dr.IsDBNull("EstEnergy") ? 0.0 : dr.GetDouble("EstEnergy");
                    estEnergy = reading.TotalReadingDelta;

                    useEnergyTotal &= !reading.EnergyToday.HasValue;  

                    if (useEnergyTotal)
                    {
                        if (!reading.EnergyTotal.HasValue)
                            GlobalSettings.SystemServices.LogMessage("NormalisePeriod", "useEnergyTotal specified but not available - Time: " + thisTime, LogEntryType.ErrorMessage);
                        else
                        {
                            thisInvEnergy = reading.EnergyTotal.Value;
                            useDeltaAtStart = true;     // no start of day value available - must use deltas only
                        }
                    }
                    else 
                        thisInvEnergy = reading.EnergyToday.Value;

                    if (isFirst && useDeltaAtStart)
                    {
                        // first energy reading contains energy from previous days - must use deltas only from this point on
                        // CMS inverters with the start of day defect on this day and other inverters without EToday values start this way
                        isFirst = false;
                        prevInvEnergy = thisInvEnergy;
                        currentInvEnergy = thisInvEnergy;
                        prevEnergyEstimate = thisInvEnergy;
                        currentEnergyEstimate = thisInvEnergy;
                        currentIntervalTime = thisIntervalTime;
                        prevIntervalTime = currentIntervalTime - TimeSpan.FromSeconds(IntervalSeconds);
                    }
                    else
                    {
                        if (isFirst)
                        {
                            currentIntervalTime = thisIntervalTime;
                            prevIntervalTime = currentIntervalTime - TimeSpan.FromSeconds(IntervalSeconds);
                            isFirst = false;
                        }

                        if (currentInvEnergy < prevInvEnergy)
                        {
                            // Cannot report negative energy - try to preserve previous delta as part of next delta
                            if (useDeltaAtStart)
                            {
                                if (GlobalSettings.SystemServices.LogTrace)
                                    GlobalSettings.SystemServices.LogMessage("NormalisePeriod", "Energy Today has reduced - was: " + prevInvEnergy.ToString() +
                                    " - now: " + currentInvEnergy.ToString() + " - Time: " + currentIntervalTime, LogEntryType.Trace);
                            }
                            else
                                GlobalSettings.SystemServices.LogMessage("NormalisePeriod", "Energy Today has reduced - was: " + prevInvEnergy.ToString() +
                                    " - now: " + currentInvEnergy.ToString() + " - Time: " + currentIntervalTime, LogEntryType.ErrorMessage);

                            prevInvEnergy = currentInvEnergy - currentInvDelta;
                            currentEnergyEstimate = currentInvEnergy; // estimates are synced with inv values at energy reduction
                            prevEnergyEstimate = currentInvEnergy - currentEstimateDelta;
                            useDeltaAtStart = false; // activate estimate range checks and report energy reductions after the first on a day
                        }
                        else
                        {
                            currentInvDelta = currentInvEnergy - prevInvEnergy;
                            currentEstimateDelta = currentEnergyEstimate - prevEnergyEstimate;
                            // if a PVBC restart occurs energy estimate will be reset to 0 - retain previous estimate
                            // this is important with inverters using low resolution energy reporting
                            // missing estimates can cause a step shaped energy curve and peaked average power curve
                            if (currentEstimateDelta < 0.0)
                                currentEstimateDelta = estEnergy; // restart will cause at least one energy estimate value to be discarded - substitute the current estimate
                        }

                        if (thisIntervalTime != currentIntervalTime)
                        {
                            if (currentEnergyEstimate < currentInvEnergy)
                                currentEnergyEstimate = currentInvEnergy; // estimate lags - catchup
                            else if (currentEnergyEstimate > (currentInvEnergy + device.EstMargin))
                                currentEnergyEstimate = (currentInvEnergy + device.EstMargin);

                            currentEstimateDelta = currentEnergyEstimate - prevEnergyEstimate;
                            // if a PVBC restart occurs energy estimate will be reset to 0 - retain previous estimate
                            // this is important with inverters using low resolution energy reporting
                            // missing estimates can cause a step shaped energy curve and peaked average power curve
                            if (currentEstimateDelta < 0.0)
                                currentEstimateDelta = estEnergy; // restart will cause at least one energy estimate value to be discarded - substitute the current estimate

                            prevEnergyRecorded = energyRecorded;
                            energyRecorded += currentEstimateDelta;

                            UpdateOneOutputItem(readingSet, currentIntervalTime, prevIntervalTime,
                                currentEstimateDelta, power, minPower, maxPower, temperature);

                            prevInvEnergy = currentInvEnergy;
                            prevEnergyEstimate = currentEnergyEstimate;
                            prevIntervalTime = currentIntervalTime;
                            currentIntervalTime = thisIntervalTime;
                            currentInvEnergy = thisInvEnergy;

                            minPower = 0;
                            maxPower = 0;
                        }

                        currentInvEnergy = thisInvEnergy;
                        currentEnergyEstimate += estEnergy;
                    }

                    if (dr.IsDBNull("PowerAC"))
                        power = null;
                    else
                        power = dr.GetInt32("PowerAC");

                    if (power != null)
                    {
                        if (power.Value < minPower || minPower == 0)
                            minPower = power.Value;
                        if (power.Value > maxPower)
                            maxPower = power.Value;
                    }

                    if (dr.IsDBNull("Temperature"))
                        temperature = null;
                    else
                        temperature = dr.GetDouble("Temperature");
                }

                // write out last if it has an energy value
                if (currentEnergyEstimate > prevEnergyEstimate)
                    UpdateOneOutputItem(readingSet, currentIntervalTime, prevIntervalTime,
                        currentEnergyEstimate - prevEnergyEstimate, power, minPower, maxPower, temperature);

                //GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "Day: " + day + " - count: " + readingSet.Readings.Count, LogEntryType.Trace);

                //if (readingSet.Readings.Count > 0)
                //    HistoryUpdater.UpdateReadingSet(readingSet, con, false);
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("NormalisePeriod", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }
        */
    }

    public class DeviceDetailPeriod_EnergyConsolidation : DeviceDetailPeriod_Consolidation<EnergyReading, EnergyReading>, IComparable<DeviceDetailPeriod_EnergyConsolidation>
    {
        public DeviceDetailPeriod_EnergyConsolidation(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, 
            FeatureSettings feature)
            : base(deviceDetailPeriods, periodType, periodStart, feature)
        {
        }

        public int CompareTo(DeviceDetailPeriod_EnergyConsolidation other)
        {
            return base.CompareTo(other);
        }

        protected override EnergyReading NewReading(DateTime outputTime, TimeSpan duration, EnergyReading pattern = null)
        {
            EnergyReading newEnergyReading;

            if (pattern == null)
            {
                newEnergyReading = new EnergyReading(DeviceDetailPeriods, outputTime, duration);
                newEnergyReading.IsConsolidationReading = true;
            }
            else
            {
                newEnergyReading = pattern.Clone(outputTime, duration);
                newEnergyReading.IsConsolidationReading = true;
                newEnergyReading.ResetEnergyDelta();
                newEnergyReading.HistEnergyDelta = null;
            }

            newEnergyReading.EnergyCalibrationFactor = DeviceParams.CalibrationFactor;

            return newEnergyReading;
        }

        protected override void SplitReadingSub(EnergyReading oldReading, DateTime splitTime, EnergyReading newReading1, EnergyReading newReading2)
        {            
            newReading1.IsConsolidationReading = true;            
            newReading2.IsConsolidationReading = true;
            if (newReading1.EnergyToday.HasValue)
                newReading1.EnergyToday -= newReading2.EnergyDelta;
            if (newReading1.EnergyTotal.HasValue)
                newReading1.EnergyTotal -= newReading2.EnergyDelta;
        }
    }

}

