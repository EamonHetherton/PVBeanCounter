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
        public DeviceDetailPeriod_EnergyMeter(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature, DeviceDataRecorders.DeviceParamsBase deviceParams)
            : base(deviceDetailPeriods, periodType, periodStart, feature, deviceParams)
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
            
            GenConnection con = existingCon;
            try
            {
                if (con == null)
                    con = GlobalSettings.TheDB.NewConnection();

                GenCommand cmd;
                cmd = new GenCommand(SelectDeviceReading_Energy, con);

                BindSelectIdentity(cmd);

                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();

                while (dr.Read())
                {

                    DateTime endTime = dr.GetDateTime(0);
                    DateTime startTime = dr.GetDateTime(1);

                    // select included extra readings that probably do not apply
                    // allows for readings to cross period boundaries by up to PeriodOverlapLimit
                    // discard rows with no overlap at all
                    if (endTime <= Start)
                        continue;
                    if (startTime >= End)
                        continue;

                    EnergyReading newRec = new EnergyReading();
                    newRec.Initialise(DeviceDetailPeriods, endTime, startTime, true, (EnergyParams)DeviceParams);

                    newRec.EnergyTotal = dr.GetNullableDouble(2, EnergyReading.EnergyPrecision);
                    newRec.EnergyToday = dr.GetNullableDouble(3, EnergyReading.EnergyPrecision);
                    newRec.EnergyDelta = dr.GetDouble(4, EnergyReading.EnergyPrecision);
                    newRec.CalibrationDelta = dr.GetNullableDouble(5, EnergyReading.EnergyPrecision);
                    newRec.HistEnergyDelta = dr.GetNullableDouble(6, EnergyReading.EnergyPrecision);
                    newRec.Mode = dr.GetNullableInt32(7);
                    newRec.ErrorCode = dr.GetNullableInt64(8);
                    newRec.Power = dr.GetNullableInt32(9);
                    newRec.Volts = dr.GetNullableFloat(10, EnergyReading.EnergyPrecision);
                    newRec.Amps = dr.GetNullableFloat(11, EnergyReading.EnergyPrecision);
                    newRec.Frequency = dr.GetNullableFloat(12, EnergyReading.EnergyPrecision);
                    newRec.Temperature = dr.GetNullableFloat(13, EnergyReading.EnergyPrecision);
                    newRec.MinPower = dr.GetNullableInt32(14);
                    newRec.MaxPower = dr.GetNullableInt32(15);

                    newRec.SetRestoreComplete();

                    AddReading(newRec, AddReadingType.Database);
                }

                dr.Close();
                dr.Dispose();
                dr = null;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.LoadPeriodFromDatabase", "Exception: " + e.Message, LogEntryType.ErrorMessage);
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

        public override void SplitReading(EnergyReading oldReading, DateTime splitTime, out EnergyReading newReading1, out EnergyReading newReading2)
        {
            ReadingBase newReading1a;
            ReadingBase newReading2a;
            base.SplitReadingGeneric((ReadingBase)oldReading, splitTime, out newReading1a, out newReading2a);

            newReading1 = (EnergyReading)newReading1a;
            newReading2 = (EnergyReading)newReading2a;
            if (newReading1.EnergyToday.HasValue)
                newReading1.EnergyToday -= newReading2.EnergyDelta;
            if (newReading1.EnergyTotal.HasValue)
                newReading1.EnergyTotal -= newReading2.EnergyDelta;
        }

        protected override EnergyReading NewReading(DateTime outputTime, TimeSpan duration, EnergyReading pattern = null)
        {
            EnergyReading newEnergyReading;

            if (pattern == null)
                newEnergyReading = new EnergyReading(DeviceDetailPeriods, outputTime, duration, (EnergyParams)DeviceParams);
            else
            {
                newEnergyReading = pattern.Clone(outputTime, duration);
                newEnergyReading.ResetEnergyDelta();
                newEnergyReading.HistEnergyDelta = null;
            }

            newEnergyReading.EnergyCalibrationFactor = DeviceParams.CalibrationFactor;

            return newEnergyReading;
        }
    }

    public class DeviceDetailPeriod_EnergyConsolidation : DeviceDetailPeriod_Consolidation<EnergyReading, EnergyReading>, IComparable<DeviceDetailPeriod_EnergyConsolidation>
    {
        public DeviceDetailPeriod_EnergyConsolidation(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, 
            FeatureSettings feature, DeviceDataRecorders.DeviceParamsBase deviceParams)
            : base(deviceDetailPeriods, periodType, periodStart, feature, deviceParams)
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
                newEnergyReading = new EnergyReading(DeviceDetailPeriods, outputTime, duration,  (EnergyParams)DeviceParams);
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

        public override void SplitReading(EnergyReading oldReading, DateTime splitTime, out EnergyReading newReading1, out EnergyReading newReading2)
        {
            ReadingBase newReading1a;
            ReadingBase newReading2a;
            base.SplitReadingGeneric((ReadingBase)oldReading, splitTime, out newReading1a, out newReading2a);

            newReading1 = (EnergyReading)newReading1a;
            newReading1.IsConsolidationReading = true;
            newReading2 = (EnergyReading)newReading2a;
            newReading2.IsConsolidationReading = true;
            if (newReading1.EnergyToday.HasValue)
                newReading1.EnergyToday -= newReading2.EnergyDelta;
            if (newReading1.EnergyTotal.HasValue)
                newReading1.EnergyTotal -= newReading2.EnergyDelta;
        }

    }

}

