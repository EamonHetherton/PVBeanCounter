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

        public EnergyReading(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, TimeSpan duration)
            : base()
        {
            Initialise(deviceDetailPeriods, readingEnd, duration, false);
        }

        public EnergyReading(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, TimeSpan duration, bool readFromDb)
            : base()
        {
            Initialise(deviceDetailPeriods, readingEnd, duration, readFromDb);
        }

        public EnergyReading() // Must call Initialise after this
            : base()
        {
        }

        public void Initialise(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, TimeSpan duration, bool readFromDb)
        {
            InitialiseBase(deviceDetailPeriods, readingEnd, duration, readFromDb);
            UseInternalCalibration = false;
            EnergyCalibrationFactor = DeviceParams.CalibrationFactor;
        }

        public void Initialise(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, DateTime readingStart, bool readFromDb)
        {
            InitialiseBase(deviceDetailPeriods, readingEnd, readingStart, readFromDb);
            UseInternalCalibration = false;
            EnergyCalibrationFactor = DeviceParams.CalibrationFactor;
        }

        public override String GetReadingLogTypeDetails()
        {
            return "EnergyDelta: " + EnergyDelta + " - CalibrationDelta: " + CalibrationDelta + " - HistEnergyDelta: " + HistEnergyDelta;
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

            if (x.ReadingEndInternal.Value > y.ReadingEndInternal.Value)
                return 1;
            else if (x.ReadingEndInternal.Value < y.ReadingEndInternal.Value)
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
            if (ReadingEndInternal.Value != other.ReadingEndInternal.Value
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

        public override void GapAdjustAdjacent(ReadingBase adjacentReading, bool adjacentIsBeforeThis)
        {           
            if (DeviceParams.UseCalculateFromPrevious)
            {
                EnergyReading reading = (EnergyReading)adjacentReading;
                // this EnergyDelta was taken from adjacent reading
                reading.EnergyDelta -= EnergyDelta;

                if (adjacentIsBeforeThis)
                {
                    // must increase new EnergyToday by new EnergyDelta
                    if (reading.EnergyToday.HasValue)
                        reading.EnergyToday -= EnergyDelta;
                    // must increase new EnergyTotal by this EnergyDelta
                    if (reading.EnergyTotal.HasValue)
                        reading.EnergyTotal -= EnergyDelta;
                }
                else
                {                   
                    // must reduce new EnergyToday by this EnergyDelta
                    if (EnergyToday.HasValue)
                        EnergyToday -= EnergyDelta;
                    // must reduce new EnergyTotal by this EnergyDelta
                    if (EnergyTotal.HasValue)
                        EnergyTotal -= EnergyDelta;
                }
            }
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

        public Double CalibrateableReadingDelta
        {
            get
            {
                if (HistEnergyDelta.HasValue)
                    return EnergyDelta + HistEnergyDelta.Value;
                else
                    return EnergyDelta;
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

        
        public int AveragePower 
        {
            get
            {                
                return (int)((1000.0 * TotalReadingDelta) / Duration.TotalHours);
            }
        }
        
        private Double? EnergyDeltaInternal = null;

        /*
        private Double? EnergyDeltaInternal
        {
            get { return _EnergyDeltaInternal; }
            set
            {
                _EnergyDeltaInternal = value;
                if (FeatureType == PVSettings.FeatureType.YieldDC && value.HasValue)
                {
                    int i;
                    i = 0;
                }
            }
        }
        */

        public Double? EnergyDeltaNullable
        {
            set
            {
                EnergyDeltaInternal = value;
            }
        }

        public Double EnergyDelta
        {
            get
            {
                return EnergyDeltaInternal.HasValue ? EnergyDeltaInternal.Value : 0.0; ;                
            }
            set
            {                
                EnergyDeltaInternal = Math.Round(value, EnergyPrecision);       
                                
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

        private void Calibrate(bool trace = false)
        {
            //if (trace)
            //    GlobalSettings.LogMessage("EnergyReading.Calibrate", "Start", LogEntryType.Trace);

            if (!UseInternalCalibration)
            {
                //if (trace)
                //    GlobalSettings.LogMessage("EnergyReading.Calibrate", "Return", LogEntryType.Trace);
                return;
            }

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

        private bool _IsHistoryReading = false;
        public override bool IsHistoryReading() { return _IsHistoryReading; }

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
                if (value.HasValue) _IsHistoryReading = true;
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
            UpdatePending = true;
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

        public override int Compare(EnergyReading other, int? precision = null)
        {
            int thisPrecision = (precision.HasValue ? precision.Value : EnergyPrecision) - 2;
            double otherEnergy = Math.Round(other.CalibrateableReadingDelta, thisPrecision);
            double thisEnergy = Math.Round(CalibrateableReadingDelta, thisPrecision);

            if (otherEnergy == thisEnergy)
                return 0;
            if (otherEnergy < thisEnergy)
                return -1;
            else
                return 1;
        }

        public override EnergyReading Clone(DateTime outputTime, TimeSpan duration)
        {          
            EnergyReading newRec = new EnergyReading(DeviceDetailPeriods,  outputTime, duration, true);

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

        public override void AccumulateReading(ReadingBase readingGeneric, bool useTemperature, bool updatePower, bool accumulateDuration = false, Double operationFactor = 1.0)
        {
            EnergyReading reading = (EnergyReading)readingGeneric;
            if (accumulateDuration)
                Duration += reading.DurationInternal;
            if (reading.Amps.HasValue)
                Amps = reading.Amps.Value;

            if (updatePower)
            {
                if (reading.Power.HasValue)
                    if (Power.HasValue)
                        Power += reading.Power.Value;
                    else
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
            }

            if (reading.Volts.HasValue)
                Volts = reading.Volts.Value;

            if (reading.ErrorCode.HasValue)
                ErrorCode = reading.ErrorCode.Value;

            if (reading.Mode.HasValue)
                Mode = reading.Mode.Value;

            if (reading.Frequency.HasValue)
                Frequency = reading.Frequency;

            if (useTemperature && reading.Temperature.HasValue)
                Temperature = reading.Temperature.Value;

            if (reading.EnergyToday.HasValue)
                EnergyToday = reading.EnergyToday.Value;
            if (reading.EnergyTotal.HasValue)
                EnergyTotal = reading.EnergyTotal.Value;
            
            if (reading.EnergyDeltaInternal.HasValue)
                if (EnergyDeltaInternal.HasValue)
                    EnergyDeltaInternal += reading.EnergyDeltaInternal.Value * operationFactor;
                else
                    EnergyDeltaInternal = reading.EnergyDeltaInternal.Value * operationFactor;
                
            if (reading.CalibrationDeltaInternal.HasValue)
                if (CalibrationDeltaInternal.HasValue)
                    CalibrationDeltaInternal += reading.CalibrationDeltaInternal.Value * operationFactor;
                else
                    CalibrationDeltaInternal = reading.CalibrationDeltaInternal.Value * operationFactor;

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

        #region HistoryCalcs

        public override void HistoryAdjust_Average(EnergyReading actualTotal, EnergyReading histRecord)
        {
            Double histEnergy = histRecord.EnergyDelta;
            double histSeconds = histRecord.Duration.TotalSeconds;
            Double actualEnergy = actualTotal.EnergyDelta;
            double actualSeconds = actualTotal.GetModeratedSeconds(3);

            double secondsDiff = Math.Round(histSeconds - actualSeconds, 1);
            if (secondsDiff < 0.0)
                throw new Exception("EnergyReading.HistoryAdjust_Average - histSeconds < actualSeconds - outputTime: " + ReadingEnd +
                    " - histSeconds: " + histSeconds + " - actualSeconds: " + actualSeconds);

            if (secondsDiff == 0.0)
                return;

            Double adjust = ((histEnergy - actualEnergy) * Duration.TotalSeconds) / secondsDiff;

            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.SystemServices.LogMessage("EnergyReading.HistoryAdjust_Average", "Hist ReadingEnd: " + histRecord.ReadingEnd + " Feature: " + histRecord.FeatureId + "hist.Duration: " + histSeconds + " - actual.Duration: " + actualSeconds
                    + " - histEnergy: " + histEnergy + " - actualEnergy: " + actualEnergy + " - this.Duration: " + Duration.TotalSeconds
                    + " - adjust: " + adjust, LogEntryType.Trace);

            if (Math.Round(adjust, EnergyPrecision - 2) != 0.0) // damp out adjustment oscilliations
                if (HistEnergyDelta.HasValue)
                    HistEnergyDelta += adjust;
                else
                    HistEnergyDelta = adjust;
        }

        public override void HistoryAdjust_Prorata(EnergyReading actualTotal, EnergyReading histRecord)
        {
            double thisReadingDelta = CalibrateableReadingDelta;
            if (thisReadingDelta <= 0.0)
                return;

            Double actualEnergy = actualTotal.CalibrateableReadingDelta;
            if (actualEnergy <= 0.0)
                return;

            Double histEnergy = histRecord.EnergyDelta;
            double histSeconds = histRecord.Duration.TotalSeconds;
            double scaleFactor = histEnergy / actualEnergy;
            double actualSeconds = actualTotal.GetModeratedSeconds(3);

            if (histSeconds < actualSeconds)
                throw new Exception("EnergyReading.HistoryAdjust_Prorata - histSeconds < acualTotal.Seconds - outputTime: " + ReadingEnd + " - FeatureId: " + FeatureId +
                    " - histSeconds: " + histSeconds + " - actualTotal.Seconds: " + actualSeconds);

            Double adjust = (thisReadingDelta * scaleFactor) - thisReadingDelta;

            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.SystemServices.LogMessage("EnergyReading.HistoryAdjust_Prorata", "outputTime: " + ReadingEnd 
                    + " - FeatureId: " + FeatureId + " - actualEnergy: " + actualEnergy
                    + " - histEnergy: " + histEnergy + " - thisEnergyDelta: " + thisReadingDelta
                    + " - adjust: " + adjust + " - scaleFactor: " + scaleFactor, LogEntryType.Trace);

            if (Math.Round(adjust, EnergyPrecision - 2) != 0.0) // damp out adjustment oscilliations
                if (HistEnergyDelta.HasValue)
                    HistEnergyDelta += adjust;
                else
                    HistEnergyDelta = adjust;
        }

        #endregion HistoryCalcs

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

        private void SetParametersId(GenCommand cmd, Int32 deviceFeature_Id)
        {
            string stage = "Id";
            try
            {
                Int32 tempId = deviceFeature_Id;
                DateTime dateTime = ReadingEndInternal.Value;
                cmd.AddParameterWithValue("@DeviceFeature_Id", tempId);
                cmd.AddParameterWithValue("@ReadingEnd", dateTime);
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
                SetParametersId(cmd, (Int32)DeviceDetailPeriods.DeviceFeatureId);
                
                stage = "ReadingStart";
                cmd.AddParameterWithValue("@ReadingStart", ReadingStartInternal.Value);
                
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
            //GlobalSettings.LogMessage("EnergyReading", "PersistReadingSub - Starting", LogEntryType.Trace);
            string stage = "Init";
            GenConnection con = null;
            bool haveMutex = false;
            bool externalCon = (existingCon != null);
            bool useInsert = !InDatabase;
            if (forceAlternate)
                useInsert = !useInsert;

            try
            {
                //GlobalSettings.LogMessage("EnergyReading", "PersistReadingSub - Before Connection", LogEntryType.Trace);
                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveMutex = true;
                if (externalCon)
                    con = existingCon;
                else                    
                    con = GlobalSettings.TheDB.NewConnection();

                //GlobalSettings.LogMessage("EnergyReading", "PersistReadingSub - Before Command", LogEntryType.Trace);
                GenCommand cmd;
                                
                if (useInsert)
                    cmd = new GenCommand(InsertDeviceReading_AC, con);
                else
                    cmd = new GenCommand(UpdateDeviceReading_AC, con);

                //GlobalSettings.LogMessage("EnergyReading", "PersistReadingSub - Before SetParameters", LogEntryType.Trace);
                SetParameters(cmd, deviceId);
                
                stage = "Execute";
                //GlobalSettings.LogMessage("EnergyReading", "PersistReadingSub - Before Execute", LogEntryType.Trace);
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

                SetParametersId(cmd, (int)DeviceDetailPeriods.DeviceFeatureId);

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

