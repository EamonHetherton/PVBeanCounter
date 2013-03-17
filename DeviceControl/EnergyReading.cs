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

// Was PVReading

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GenericConnector;
using MackayFisher.Utilities;
using PVSettings;
using PVBCInterfaces;

namespace DeviceDataRecorders
{
    public class DeviceParamsBase 
    {
        public PVSettings.DeviceType DeviceType { get; set; }
        public virtual int QueryInterval { get; set; }
        public int RecordingInterval { get; set; }
        public bool EnforceRecordingInterval { get; set; }
        public bool UseCalculateFromPrevious { get; set; }

        public float CalibrationFactor { get; set; }
        
        public DeviceParamsBase()
        {
            CalibrationFactor = 1.0F;
            DeviceType = PVSettings.DeviceType.Unknown;
            QueryInterval = 6;
            RecordingInterval = 60;
            EnforceRecordingInterval = true;
            UseCalculateFromPrevious = false;
        }
    }

    public class EnergyParams : DeviceParamsBase
    {
        
        public EnergyParams()
        {
        }
    }

    public class EnergyReading : ReadingBaseTyped<EnergyReading, EnergyReading>, IComparable<EnergyReading>
    {
        public static int EnergyPrecision = 5;

        public EnergyReading(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, TimeSpan duration, EnergyParams deviceParams)
            : base()
        {
            Initialise(deviceDetailPeriods, readingEnd, duration, false, deviceParams);
        }

        public EnergyReading(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, TimeSpan duration, bool readFromDb, EnergyParams deviceParams)
            : base()
        {
            Initialise(deviceDetailPeriods, readingEnd, duration, readFromDb, deviceParams);
        }

        public EnergyReading() // Must call Initialise after this
            : base()
        {
        }

        public void Initialise(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, TimeSpan duration, bool readFromDb, EnergyParams deviceParams)
        {
            InitialiseBase(deviceDetailPeriods, readingEnd, duration, readFromDb, deviceParams);
            AveragePowerInternal = null;
            UseInternalCalibration = false;
            EnergyCalibrationFactor = DeviceParams.CalibrationFactor;
        }

        public void Initialise(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, DateTime readingStart, bool readFromDb, EnergyParams deviceParams)
        {
            InitialiseBase(deviceDetailPeriods, readingEnd, readingStart, readFromDb, deviceParams);
            AveragePowerInternal = null;
            UseInternalCalibration = false;
            EnergyCalibrationFactor = DeviceParams.CalibrationFactor;
        }

        private float EnergyCalibrationFactorInternal = 1.0F;
        public float EnergyCalibrationFactor
        {
            get { return EnergyCalibrationFactorInternal; }
            set
            {
                EnergyCalibrationFactorInternal = value;
                if (value != 1.0F)
                {
                    UseInternalCalibration = true;
                    Calibrate();
                }
                else UseInternalCalibration = false;

            }
        }

        public bool UseInternalCalibration { get; private set; }

        protected override void DurationChanged()
        {
            base.DurationChanged();
            if (EnergyDeltaInternal.HasValue)
                AveragePower = (int)(EnergyDeltaInternal.Value * 1000.0 / DurationInternal.TotalHours);
            else
                AveragePowerInternal = null;
        }

        // Comparer allows readings to be sorted
        public static int Compare(EnergyReading x, EnergyReading y)
        {
            if (x == null)
                if (y == null)
                    return 0; // both null
                else
                    return -1; // x is null y is not null
            else if (y == null)
                return 1;  // y is null x is not null

            if (x.ReadingEndInternal > y.ReadingEndInternal)
                return 1;
            else if (x.ReadingEndInternal < y.ReadingEndInternal)
                return -1;

            return 0; // equal
        }

        public Int32 CompareTo(EnergyReading other)
        {
            if (ReadingEnd < other.ReadingEnd)
                return -1;
            else if (ReadingEnd > other.ReadingEnd)
                return 1;
            else return 0;
        }

        protected override bool IsSameReading(EnergyReading other)
        {
            return (this == other);
        }

        protected override bool IsSameReadingValues(EnergyReading other)
        {
            if (ReadingEndInternal != other.ReadingEndInternal
             || DurationInternal != other.DurationInternal
             || FeatureType != other.FeatureType
             || FeatureId != other.FeatureId)
                return false;

            if (VoltsInternal != other.VoltsInternal
                || AmpsInternal != other.AmpsInternal
                || FrequencyInternal != other.FrequencyInternal
                || PowerInternal != other.PowerInternal
                || EnergyDeltaInternal != other.EnergyDeltaInternal
                || CalibrationDeltaInternal != other.CalibrationDeltaInternal)
                return false;

            return true;
        }

        public Double TotalReadingDelta 
        { 
            get 
            {
                return EnergyDelta +
                    (CalibrationDelta.HasValue ? CalibrationDelta.Value : 0.0) +
                    (HistEnergyDelta.HasValue ? HistEnergyDelta.Value : 0.0); 
            } 
        }

        public Double? CalibrateableReadingDelta
        {
            get
            {
                if (!EnergyDeltaInternal.HasValue && !HistEnergyDelta.HasValue)
                    return null;
                return EnergyDelta +
                    (HistEnergyDelta.HasValue ? HistEnergyDelta.Value : 0.0);
            }
        }

        private float? VoltsInternal = null;
        public float? Volts
        {
            get
            {
                return VoltsInternal;
            }

            set
            {
                VoltsInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private float? AmpsInternal = null;
        public float? Amps
        {
            get
            {
                return AmpsInternal;
            }

            set
            {
                AmpsInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private float? FrequencyInternal = null;
        public float? Frequency
        {
            get
            {
                return FrequencyInternal;
            }

            set
            {
                FrequencyInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private int? PowerInternal = null;
        public int? Power 
        {
            get
            {
                if (PowerInternal.HasValue)
                    return PowerInternal;
                else
                    return AveragePower;
            }

            set
            {
                PowerInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        public int? AveragePowerInternal = null;
        public int AveragePower 
        {
            get
            {
                if (!AveragePowerInternal.HasValue)
                    return (int)((1000.0 * EnergyDelta) / Duration.TotalHours);
                return AveragePowerInternal.Value;
            }
            private set
            {
                AveragePowerInternal = value;
            }
        }
        
        /*
        private bool EnergyDeltaCalculatedInternal = true;
        public bool EnergyDeltaCalculated
        {
            get { return EnergyDeltaCalculatedInternal; }
            set
            {
                EnergyDeltaCalculatedInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }
        */
        private Double? EnergyDeltaInternal = null;
        public virtual Double EnergyDelta
        {
            get
            {
                return EnergyDeltaInternal.HasValue ? EnergyDeltaInternal.Value : 0.0; ;                
            }
            set
            {
                EnergyDeltaInternal = Math.Round(value, EnergyPrecision);               
                AveragePower = (int)(EnergyDeltaInternal * 1000.0 / DurationInternal.TotalHours);
                
                if (!AttributeRestoreMode)
                {
                    if (UseInternalCalibration)
                        Calibrate();
                    UpdatePending = true;
                }
            }
        }

        public void ResetEnergyDelta()
        {
            EnergyDeltaInternal = null;
            if (!AttributeRestoreMode)
            {
                if (UseInternalCalibration)
                    Calibrate();
                UpdatePending = true;
            }
        }

        private Double? CalibrationDeltaInternal = null;
        public Double? CalibrationDelta
        {
            get
            {
                return CalibrationDeltaInternal;
            }
            set
            {
                bool change = false;
                if (value.HasValue && CalibrationDeltaInternal.HasValue)
                {
                    if (CalibrationDeltaInternal.Value != Math.Round(value.Value, 3))
                        change = true;
                }
                else
                    change = value.HasValue != CalibrationDeltaInternal.HasValue;

                if (change)
                {
                    CalibrationDeltaInternal = (value.HasValue ? (Double?)Math.Round(value.Value, EnergyPrecision) : null);
                    if (!AttributeRestoreMode) UpdatePending = true;
                }
            }
        }

        private void Calibrate()
        {
            if (!UseInternalCalibration)
                return;

            Double? newCalc = CalibrateableReadingDelta;
            if (newCalc.HasValue)
                newCalc = newCalc.Value * EnergyCalibrationFactorInternal - newCalc.Value;
            else
                newCalc = null;

            if (newCalc != CalibrationDeltaInternal)
            {
                CalibrationDelta = newCalc;
            }
        }

        private Double? HistEnergyDeltaInternal = null;
        public Double? HistEnergyDelta
        {
            get
            {
                return HistEnergyDeltaInternal;
            }
            set
            {
                HistEnergyDeltaInternal = (value.HasValue ? (Double?)Math.Round(value.Value, EnergyPrecision) : null);
                if (!AttributeRestoreMode)
                {
                    if (UseInternalCalibration)
                        Calibrate();
                    UpdatePending = true;
                }
            }
        }

        private Double? EnergyTotalInternal = null;
        public Double? EnergyTotal
        {
            get
            {
                return EnergyTotalInternal;
            }
            set
            {
                EnergyTotalInternal = (value.HasValue ? (Double?)Math.Round(value.Value, EnergyPrecision) : null);
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private Double? EnergyTodayInternal = null;
        public Double? EnergyToday
        {
            get
            {
                return EnergyTodayInternal;
            }
            set
            {
                EnergyTodayInternal = (value.HasValue ? (Double?)Math.Round(value.Value, EnergyPrecision) : null);
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private Int32? ModeInternal = null;
        public Int32? Mode
        {
            get
            {
                return ModeInternal;
            }
            set
            {
                ModeInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private Int64? ErrorCodeInternal = null;
        public Int64? ErrorCode
        {
            get
            {
                return ErrorCodeInternal;
            }
            set
            {
                ErrorCodeInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private int? MinPowerInternal = null;
        public virtual int? MinPower
        {
            get
            {
                return MinPowerInternal;
            }

            set
            {
                MinPowerInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private int? MaxPowerInternal = null;
        public virtual int? MaxPower
        {
            get
            {
                return MaxPowerInternal;
            }

            set
            {
                MaxPowerInternal = value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        private Double? TemperatureInternal = null;
        public virtual Double? Temperature
        {
            get
            {
                return TemperatureInternal;
            }

            set
            {
                TemperatureInternal = (value.HasValue ? (Double?)Math.Round(value.Value, EnergyPrecision) : null);
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        public override void ClearHistory()
        {
            HistEnergyDeltaInternal = null;
        }

        public override EnergyReading FillSmallGap(DateTime outputTime, TimeSpan duration, bool isNext)
        {
            EnergyReading newRec = Clone(outputTime, duration);

            if (DeviceParams.UseCalculateFromPrevious)
            {                
                if (isNext)
                {
                    // new EnergyDelta is taken from this reading
                    EnergyDelta -= newRec.EnergyDelta;
                    // must reduce new EnergyToday by this EnergyDelta
                    if (newRec.EnergyToday.HasValue)
                        newRec.EnergyToday -= EnergyDelta;
                    // must reduce new EnergyTotal by this EnergyDelta
                    if (newRec.EnergyTotal.HasValue)
                        newRec.EnergyTotal -= EnergyDelta;
                }
                else
                {
                    // must increase new EnergyToday by new EnergyDelta
                    if (newRec.EnergyToday.HasValue)
                        newRec.EnergyToday += newRec.EnergyDelta;
                    // must increase new EnergyTotal by this EnergyDelta
                    if (newRec.EnergyTotal.HasValue)
                        newRec.EnergyTotal += newRec.EnergyDelta;
                }
            }

            return newRec;
        }

        protected override void CalcFromPrevious(EnergyReading prevReading)
        {
            // Following test not required - handled at source
            //if (DeviceParams.UseCalculateFromPrevious)            
            if (prevReading == null)
            {
                if (EnergyTodayInternal.HasValue)
                    EnergyDelta = EnergyTodayInternal.Value;
                else
                    EnergyDelta = 0.0;
            }
            else if (prevReading.EnergyToday.HasValue && EnergyTodayInternal.HasValue)
                EnergyDelta = EnergyTodayInternal.Value - prevReading.EnergyToday.Value;
            else if (prevReading.EnergyTotal.HasValue && EnergyTotalInternal.HasValue)
                EnergyDelta = EnergyTotalInternal.Value - prevReading.EnergyTotal.Value;
        }

        public override void HistoryAdjust_Average(EnergyReading actualTotal, EnergyReading histRecord)
        {
            Double prorataEnergy = histRecord.EnergyDelta -
                (actualTotal.EnergyDelta  
                + (actualTotal.HistEnergyDeltaInternal.HasValue ? actualTotal.HistEnergyDeltaInternal.Value : 0.0));
            double prorataSeconds = histRecord.Duration.TotalSeconds;
            double actualSeconds = actualTotal.GetModeratedSeconds(3);

            if (prorataSeconds != actualSeconds)
                throw new Exception("EnergyReading.HistoryAdjust_Average - prorataSeconds != acualTotal.Seconds - outputTime: " + ReadingEnd + 
                    " - prorataSeconds: " + prorataSeconds + " - actualTotal.Seconds: " + actualSeconds);

            Double adjust = (prorataEnergy * Duration.TotalSeconds) / prorataSeconds;
            if (Math.Round(adjust, EnergyPrecision - 2) != 0.0) // damp out adjustment oscilliations
                if (HistEnergyDelta.HasValue)                    
                    HistEnergyDelta += adjust;                
                else 
                    HistEnergyDelta = adjust;            
        }

        public override void HistoryAdjust_Prorata(EnergyReading actualTotal, EnergyReading histRecord)
        {
            double thisEnergyDelta = CalibrateableReadingDelta.HasValue ? CalibrateableReadingDelta.Value : 0.0;
            if (thisEnergyDelta <= 0.0)
                return;

            Double prorataEnergy = histRecord.EnergyDelta -
                (actualTotal.EnergyDelta
                + (actualTotal.HistEnergyDeltaInternal.HasValue ? actualTotal.HistEnergyDeltaInternal.Value : 0.0));
            double prorataSeconds = histRecord.Duration.TotalSeconds;
            double scaleFactor = prorataEnergy / thisEnergyDelta;
            double actualSeconds = actualTotal.GetModeratedSeconds(3);

            if (prorataSeconds != actualSeconds)
                throw new Exception("EnergyReading.HistoryAdjust_Prorata - prorataSeconds != acualTotal.Seconds - outputTime: " + ReadingEnd +
                    " - prorataSeconds: " + prorataSeconds + " - actualTotal.Seconds: " + actualSeconds);

            Double adjust = thisEnergyDelta * scaleFactor;
            if (Math.Round(adjust, EnergyPrecision - 2) != 0.0) // damp out adjustment oscilliations
                if (HistEnergyDelta.HasValue)
                    HistEnergyDelta += adjust;
                else
                    HistEnergyDelta = adjust;
        }

        public override int Compare(EnergyReading other, int? precision = null)
        {
            int thisPrecision = (precision.HasValue ? precision.Value : EnergyPrecision) - 2;
            double otherEnergy = Math.Round(other.CalibrateableReadingDelta.HasValue ? other.CalibrateableReadingDelta.Value : 0.0, thisPrecision);
            double thisEnergy = Math.Round(CalibrateableReadingDelta.HasValue ? CalibrateableReadingDelta.Value : 0.0, thisPrecision);

            if (otherEnergy == thisEnergy)
                return 0;
            if (otherEnergy < thisEnergy)
                return -1;
            else
                return 1;
        }

        public override EnergyReading Clone(DateTime outputTime, TimeSpan duration)
        {          
            EnergyReading newRec = new EnergyReading(DeviceDetailPeriods,  outputTime, duration, true, (EnergyParams)DeviceParams);

            Double factor = (Double)duration.TotalSeconds / DurationInternal.TotalSeconds;
            
            newRec.EnergyTotalInternal = EnergyTotalInternal;
            newRec.EnergyTodayInternal = EnergyTodayInternal;
            if (EnergyDeltaInternal.HasValue)
                newRec.EnergyDeltaInternal = EnergyDeltaInternal * factor;
            else
                newRec.EnergyDeltaInternal = null;
            if (CalibrationDeltaInternal.HasValue)
                newRec.CalibrationDeltaInternal = CalibrationDeltaInternal * factor;
            if (HistEnergyDeltaInternal.HasValue)
                newRec.HistEnergyDeltaInternal = HistEnergyDeltaInternal * factor;
            newRec.PowerInternal = PowerInternal;
            newRec.ModeInternal = ModeInternal;
            newRec.ErrorCodeInternal = ErrorCodeInternal;
            newRec.VoltsInternal = VoltsInternal;
            newRec.AmpsInternal = AmpsInternal;
            newRec.FrequencyInternal = FrequencyInternal;
            newRec.TemperatureInternal = TemperatureInternal;
            newRec.MinPowerInternal = MinPowerInternal;
            newRec.MaxPowerInternal = MaxPowerInternal;

            // if time is not changed, database presence has not changed
            if (outputTime == ReadingEnd)
                newRec.InDatabase = InDatabase;
            else
                newRec.InDatabase = false;

            newRec.UpdatePending = true;
            
            newRec.SetRestoreComplete();
            Calibrate();
            return newRec;
        }

        public override void AccumulateReading(EnergyReading reading, Double operationFactor = 1.0)
        {
            Duration += reading.DurationInternal;
            if (reading.Amps.HasValue)
                Amps = reading.Amps.Value;

            if (reading.Power.HasValue)
                Power = reading.Power.Value;

            if (reading.MinPower.HasValue)
            {
                if (MinPower.HasValue)
                    MinPower = Math.Min(MinPower.Value, reading.MinPower.Value);
                else
                    MinPower = reading.MinPower;
            }

            if (reading.MaxPower.HasValue)
            {
                if (MaxPower.HasValue)
                    MaxPower = Math.Max(MaxPower.Value, reading.MaxPower.Value);
                else
                    MaxPower = reading.MaxPower;
            }

            if (reading.Volts.HasValue)
                Volts = reading.Volts.Value;

            if (reading.ErrorCode.HasValue)
                ErrorCode = reading.ErrorCode.Value;

            if (reading.Mode.HasValue)
                Mode = reading.Mode.Value;

            if (reading.Temperature.HasValue)
                Temperature = reading.Temperature.Value;

            if (reading.EnergyToday.HasValue)
                EnergyToday = reading.EnergyToday.Value;
            if (reading.EnergyTotal.HasValue)
                EnergyTotal = reading.EnergyTotal.Value;
            
            EnergyDelta += reading.EnergyDelta * operationFactor;
                
            if (reading.CalibrationDelta.HasValue)
                if (CalibrationDelta.HasValue)
                    CalibrationDelta += reading.CalibrationDelta.Value * operationFactor;
                else
                    CalibrationDelta = reading.CalibrationDelta.Value * operationFactor;

            if (reading.HistEnergyDelta.HasValue)
                if (HistEnergyDelta.HasValue)
                    HistEnergyDelta += reading.HistEnergyDelta.Value * operationFactor;
                else
                    HistEnergyDelta = reading.HistEnergyDelta.Value * operationFactor;

            if (reading.EnergyToday.HasValue)
                if (EnergyToday.HasValue)
                    EnergyToday = Math.Max(EnergyToday.Value, reading.EnergyToday.Value);
                else
                    EnergyToday = reading.EnergyToday;

            if (reading.EnergyTotal.HasValue)
                if (EnergyTotal.HasValue)
                    EnergyTotal = Math.Max(EnergyTotal.Value, reading.EnergyTotal.Value);
                else
                    EnergyTotal = reading.EnergyTotal;
        }

        #region Persistance

        private static String InsertDeviceReading_AC =
            "INSERT INTO devicereading_energy " +
                "( ReadingEnd, DeviceFeature_Id, ReadingStart, EnergyTotal, EnergyToday, EnergyDelta, " +
                "CalcEnergyDelta, HistEnergyDelta, Mode, ErrorCode, Power, Volts, " +
                "Amps, Frequency, Temperature, " +
                "MinPower," +
                "MaxPower) " +
            "VALUES " +
                "(@ReadingEnd, @DeviceFeature_Id, @ReadingStart, @EnergyTotal, @EnergyToday, @EnergyDelta, " +
                "@CalcEnergyDelta, @HistEnergyDelta, @Mode, @ErrorCode, @Power, @Volts, " +
                "@Amps, @Frequency, @Temperature, " +
                "@MinPower, " +
                "@MaxPower) ";

        private static String UpdateDeviceReading_AC =
            "UPDATE devicereading_energy set " +
                "ReadingStart = @ReadingStart, " +
                "EnergyTotal = @EnergyTotal, " +
                "EnergyToday = @EnergyToday, " +
                "EnergyDelta = @EnergyDelta, " +
                "CalcEnergyDelta = @CalcEnergyDelta, " +
                "HistEnergyDelta = @HistEnergyDelta, " +
                "Mode = @Mode, " +
                "ErrorCode = @ErrorCode, " +
                "Power = @Power, " +                
                "Volts = @Volts, " +                
                "Amps = @Amps, " +                
                "Frequency = @Frequency, " +
                "Temperature = @Temperature, " +
                "MinPower = @MinPower, " +              
                "MaxPower = @MaxPower " +
            "WHERE " +
                "ReadingEnd = @ReadingEnd " +
                "AND DeviceFeature_Id = @DeviceFeature_Id ";

        private static String DeleteDeviceReading_AC =
            "DELETE from devicereading_energy " +
             "WHERE " +
                "ReadingEnd = @ReadingEnd " +
                "AND DeviceFeature_Id = @DeviceFeature_Id ";

        private void SetParametersId(GenCommand cmd, int deviceFeature_Id)
        {
            string stage = "Id";
            try
            {
                cmd.AddParameterWithValue("@DeviceFeature_Id", deviceFeature_Id);
                cmd.AddParameterWithValue("@ReadingEnd", ReadingEndInternal);
            }
            catch (Exception e)
            {
                throw new Exception("SetParametersId - Stage: " + stage + " - Exception: " + e.Message);
            }
        }

        private void SetParameters(GenCommand cmd, int deviceId)
        {
            string stage = "Device_Id";
            try
            {
                SetParametersId(cmd, DeviceDetailPeriods.FeatureSettings.Id);
                stage = "ReadingStart";
                cmd.AddParameterWithValue("@ReadingStart", ReadingStartInternal);
                stage = "EnergyTotal";
                cmd.AddRoundedParameterWithValue("@EnergyTotal", EnergyTotalInternal, EnergyPrecision);
                stage = "EnergyToday";
                cmd.AddRoundedParameterWithValue("@EnergyToday", EnergyTodayInternal, EnergyPrecision);
                stage = "EnergyDelta";
                cmd.AddRoundedParameterWithValue("@EnergyDelta", EnergyDeltaInternal, EnergyPrecision);
                stage = "CalcEnergyDelta";
                // use rounding - 6 dp more than adequate - SQLite stores all values as text, too many digits wastes DB space
                cmd.AddRoundedParameterWithValue("@CalcEnergyDelta", CalibrationDeltaInternal, EnergyPrecision);
                stage = "HistEnergyDelta";
                cmd.AddRoundedParameterWithValue("@HistEnergyDelta", HistEnergyDeltaInternal, EnergyPrecision);
                stage = "Mode";
                cmd.AddParameterWithValue("@Mode", ModeInternal);

                stage = "ErrorCode";
                if (ErrorCodeInternal.HasValue)
                    cmd.AddParameterWithValue("@ErrorCode", (long)ErrorCodeInternal.Value);
                else
                    cmd.AddParameterWithValue("@ErrorCode", null);

                stage = "Power";
                cmd.AddParameterWithValue("@Power", PowerInternal);
                
                stage = "Volts";
                cmd.AddRoundedParameterWithValue("@Volts", VoltsInternal, 2);
                
                stage = "Amps";
                cmd.AddRoundedParameterWithValue("@Amps", AmpsInternal, 2);
                
                stage = "Frequency";
                cmd.AddRoundedParameterWithValue("@Frequency", FrequencyInternal, 1);
                stage = "Temp";
                cmd.AddRoundedParameterWithValue("@Temperature", TemperatureInternal, 2);

                stage = "MinPower";
                cmd.AddParameterWithValue("@MinPower", MinPowerInternal);
                
                stage = "MaxPower";
                cmd.AddParameterWithValue("@MaxPower", MaxPowerInternal);
            }
            catch (Exception e)
            {
                throw new Exception("SetParameters - Stage: " + stage + " - Exception: " + e.Message);
            }
        }

        private bool PersistReadingSub(bool forceAlternate, GenConnection existingCon, int deviceId)
        {
            string stage = "Init";
            GenConnection con = null;
            bool haveMutex = false;
            bool externalCon = (existingCon != null);
            bool useInsert = !InDatabase;
            if (forceAlternate)
                useInsert = !useInsert;

            try
            {
                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveMutex = true;
                if (externalCon)
                    con = existingCon;
                else                    
                    con = GlobalSettings.TheDB.NewConnection();

                GenCommand cmd;
                                
                if (useInsert)
                    cmd = new GenCommand(InsertDeviceReading_AC, con);
                else
                    cmd = new GenCommand(UpdateDeviceReading_AC, con);

                SetParameters(cmd, deviceId);
                
                stage = "Execute";
                cmd.ExecuteNonQuery();

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("EnergyReading.PersistReadingSub", "ReadingEnd: " + ReadingEndInternal + " - " + 
                        (useInsert ? "Insert" : "Update") + " - EnergyTotal: " + EnergyTotal +
                        " - EnergyToday: " + EnergyToday +
                        " - Power: " + Power +
                        " - Calc Adjust: " + CalibrationDelta +
                        " - Hist Adjust: " + HistEnergyDelta, LogEntryType.Trace);
                UpdatePending = false;
                InDatabase = true;
                return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("EnergyReading.PersistReadingSub - ", (useInsert ? "Insert" : "Update") + " - Stage: " + stage + " - Exception: " + e.Message, 
                    forceAlternate ? LogEntryType.ErrorMessage : LogEntryType.Trace);
                if (!forceAlternate)
                    PersistReadingSub(true, con, deviceId);
            }
            finally
            {
                if (existingCon == null && con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                if (haveMutex)
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
            return false;
        }

        public override bool DeleteReading(GenConnection existingCon, int deviceId)
        {
            if (!InDatabase)
                return false;
            
            string stage = "Init";
            GenConnection con = null;
            bool haveMutex = false;
            bool externalCon = (existingCon != null);
            
            try
            {
                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveMutex = true;
                if (externalCon)
                    con = existingCon;
                else
                    con = GlobalSettings.TheDB.NewConnection();

                GenCommand cmd = new GenCommand(DeleteDeviceReading_AC, con);

                SetParametersId(cmd, DeviceDetailPeriods.FeatureSettings.Id);

                stage = "Execute";
                cmd.ExecuteNonQuery();

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("EnergyReading.DeleteReading", "ReadingEnd: " + ReadingEndInternal + " - EnergyTotal: " + EnergyTotal +
                        " - EnergyToday: " + EnergyToday +
                        " - Power: " + Power +
                        " - Calc Adjust: " + CalibrationDelta +
                        " - Hist Adjust: " + HistEnergyDelta, LogEntryType.Trace);
                UpdatePending = false;
                InDatabase = false;
                return true;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("EnergyReading.DeleteReadingSub", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (existingCon == null && con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                if (haveMutex)
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
            return false;
        }

        public override bool PersistReading(GenConnection con, int deviceId)
        {
            return PersistReadingSub(false, con, deviceId);
        }

        #endregion Persistance
    }       
}

