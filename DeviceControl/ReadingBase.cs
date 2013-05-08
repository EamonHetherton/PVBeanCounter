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
    public abstract class ReadingBase
    {
        private bool InDatabaseInternal = false;
        public bool InDatabase // true when this value already exists in DB
        {
            get { return InDatabaseInternal; }
            set { InDatabaseInternal = value; }
        }

        // AddReading sets this true for all readings that match an existing reading or are new readings
        // Used to allow auto removal of history readings that no longer exist in the external source e.g. SMA Cunny Explorer data
        public bool? AddReadingMatch = null; 

        private bool IsConsolidationReadingInternal = false;

        public bool IsConsolidationReading
        {
            get
            {
                return IsConsolidationReadingInternal;
            }
            set
            {
                IsConsolidationReadingInternal = value;
                if (value)  // consolidation readings are not stored in the database
                    UpdatePendingInternal = false;
            }
        }

        private bool UpdatePendingInternal = false;

        public bool UpdatePending   // true when database writes are outstanding
        {
            get { return UpdatePendingInternal; }
            protected set
            {
                if (IsConsolidationReading)  // consolidation readings are not stored in the database
                    UpdatePendingInternal = false;
                else
                {
                    UpdatePendingInternal = value;
                    if (value)
                        foreach (DeviceDetailPeriodBase period in RegisteredPeriods)
                            period.PeriodIsDirty();
                }
            }
        }
        public bool AttributeRestoreMode { get; protected set; }    // true when loading values from persistent store

        protected DeviceDataRecorders.DeviceParamsBase DeviceParams;

        private List<DeviceDetailPeriodBase> RegisteredPeriods;

        public DeviceDetailPeriodsBase DeviceDetailPeriods { get; private set; }

        public ReadingBase() // must call InitialiseBase after this
        {
            DeviceParams = null;
            DeviceDetailPeriods = null;
            RegisteredPeriods = new List<DeviceDetailPeriodBase>();
        }

        public void RegisterPeriodInvolvement(DeviceDetailPeriodBase period)
        {
            foreach (DeviceDetailPeriodBase rPeriod in RegisteredPeriods)
                if (rPeriod == period)
                    return;
            RegisteredPeriods.Add(period);
        }

        public void DeregisterPeriodInvolvement(DeviceDetailPeriodBase period)
        {
            for (int i = 0; i < RegisteredPeriods.Count; )
            {
                if (RegisteredPeriods[i] == period)
                    RegisteredPeriods.RemoveAt(i);
                else
                    i++;
            }
        }

        protected void InitialiseBase(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, TimeSpan duration, bool readFromDb)
        {
            DeviceDetailPeriods = deviceDetailPeriods;
            AttributeRestoreMode = readFromDb;
            ReadingEndInternal.Value = readingEnd;
            Duration = duration;
            InDatabase = readFromDb;
            UpdatePending = !readFromDb;
            DeviceParams = DeviceDetailPeriods.Device.DeviceParams;
        }

        protected void InitialiseBase(DeviceDetailPeriodsBase deviceDetailPeriods, DateTime readingEnd, DateTime readingStart, bool readFromDb)
        {
            DeviceDetailPeriods = deviceDetailPeriods;
            AttributeRestoreMode = readFromDb;
            ReadingEndInternal.Value = readingEnd;
            ReadingStartInternal.Value = readingStart;
            DurationInternal = ReadingEndInternal.Value - ReadingStartInternal.Value;
            InDatabase = readFromDb;
            UpdatePending = !readFromDb;
            DeviceParams = DeviceDetailPeriods.Device.DeviceParams;
        }
        
        protected DBDateTimeGeneric ReadingStartInternal;
        public virtual DateTime ReadingStart
        {
            get
            {
                return ReadingStartInternal.Value;
            }
            set
            {
                if (value > ReadingEndInternal.Value)
                    throw new Exception("ReadingBase.ReadingStart - Attempt to set start after end");
                ReadingStartInternal.Value = value;
                DurationInternal = ReadingEndInternal.Value - value;
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        public FeatureType FeatureType { get { return DeviceDetailPeriods.FeatureType; } }
        public uint FeatureId { get { return DeviceDetailPeriods.FeatureId; } }
        
        protected DBDateTimeGeneric ReadingEndInternal;
        public virtual DateTime ReadingEnd
        {
            get
            {
                return ReadingEndInternal.Value;
            }
        }

        protected TimeSpan DurationInternal = TimeSpan.Zero;
        public virtual TimeSpan Duration
        {
            get
            {
                return DurationInternal;
            }
            set
            {
                DurationInternal = value;
                DurationChanged();
                if (!AttributeRestoreMode) UpdatePending = true;
            }
        }

        public virtual double GetModeratedSeconds(int precision)
        {
            return Math.Round(DurationInternal.TotalSeconds, precision);
        }

        protected virtual void DurationChanged()
        {
            ReadingStartInternal.Value = ReadingEndInternal.Value - DurationInternal;
            DurationInternal = ReadingEndInternal.Value - ReadingStartInternal.Value; // consistency after possible date precision adjustments - DBDateTimeGeneric
        }

        public virtual void SetRestoreComplete()
        {
            AttributeRestoreMode = false;
        }

        public abstract bool PersistReading(GenConnection con, int deviceId);
        public abstract void ClearHistory();
        public abstract bool DeleteReading(GenConnection con, int deviceId);
        public abstract void CalcFromPreviousGeneric(ReadingBase prevReading);
        public abstract bool IsSameReadingGeneric(ReadingBase other);
        public abstract bool IsSameReadingValuesGeneric(ReadingBase other);
        public abstract bool IsHistoryReading();
        public abstract ReadingBase CloneGeneric(DateTime outputTime, TimeSpan duration);
        public abstract void GapAdjustAdjacent(ReadingBase adjacentReading, bool adjacentIsBeforeThis);
        public abstract void AccumulateReading(ReadingBase reading, bool useTemperature, bool updatePower, bool accumulateDuration = false, Double operationFactor = 1.0);

        public String GetReadingLogIdDetails()
        {
            return "Feature: " + DeviceDetailPeriods.FeatureType.ToString() + " - ReadingEnd: " + ReadingEnd + " - ReadingStart: " + ReadingStart;
        }

        public String GetReadingLogDetails()
        {
            return GetReadingLogIdDetails() + " - " + GetReadingLogTypeDetails();
        }

        public abstract String GetReadingLogTypeDetails();
    }

    public abstract class ReadingBaseTyped<TDeviceReading, TDeviceHistory> : ReadingBase where TDeviceReading : ReadingBase
    {
        public ReadingBaseTyped()
            : base()
        {
        }      
        
        public abstract void HistoryAdjust_Average(TDeviceReading actualTotal, TDeviceHistory histRecord);
        public abstract void HistoryAdjust_Prorata(TDeviceReading actualTotal, TDeviceHistory histRecord);

        public override bool IsSameReadingGeneric(ReadingBase other)
        {
            return IsSameReading((TDeviceReading)other);
        }
        protected abstract bool IsSameReading(TDeviceReading other);
        public override bool IsSameReadingValuesGeneric(ReadingBase other)
        {
            return IsSameReadingValues((TDeviceReading)other);
        }
        protected abstract bool IsSameReadingValues(TDeviceReading other);
        public override void CalcFromPreviousGeneric(ReadingBase prevReading)
        {
            CalcFromPrevious((TDeviceReading)prevReading);
        }
        protected abstract void CalcFromPrevious(TDeviceReading prevReading);
        public override ReadingBase CloneGeneric(DateTime outputTime, TimeSpan duration)
        {
            return Clone(outputTime, duration);
        }
        public abstract TDeviceReading Clone(DateTime outputTime, TimeSpan duration);
        public abstract int Compare(TDeviceHistory other, int? precision = null);
    }
}
