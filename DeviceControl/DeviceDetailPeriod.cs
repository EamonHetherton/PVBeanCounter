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
    public enum PeriodType
    {
        Day,
        Week,
        Month,
        Quarter,
        HalfYear,
        Year
    }

    public enum AddReadingType
    {
        NewReading = 0,
        History,
        Consolidation,
        Database,
        FillGaps
    }

    public class PeriodBase
    {
        public int DatabaseIntervalSeconds { get; private set; }  // DatabaseIntervalSeconds is the interval for database recording

        public PeriodType PeriodType { get; private set; }
        public DateTime Start
        {
            get
            {
                return StartInternal;
            }
            private set
            {
                StartInternal = value;
                StartYearInternal = StartInternal.Year;
                StartMonthInternal = StartInternal.Month;
                StartDayInternal = StartInternal.Day;
                StartTimeInternal = StartInternal.TimeOfDay;
            }
        }
        public DateTime End { get; private set; }
        public TimeSpan Duration { get; private set; }
        public TimeSpan IntervalDuration { get; private set; }

        private DateTime StartInternal;
        private Int32 StartYearInternal;
        private Int32 StartMonthInternal;
        private Int32 StartDayInternal;
        private TimeSpan StartTimeInternal;
        public TimeSpan Offset { get; private set; }

        public PeriodBase(PeriodType periodType, DateTime periodStart, int databaseInterval)
        {
            PeriodType = periodType;
            Start = periodStart;
            SetDuration();
            Offset = CalcStandardStartOffset(periodType, periodStart);
            DatabaseIntervalSeconds = databaseInterval;
            IntervalDuration = TimeSpan.FromSeconds(databaseInterval);
        }

        public DateTime GetPeriodStart(DateTime readingTime, bool IsReadingEnd = true)
        {
            return GetPeriodStart(PeriodType, Offset, readingTime, IsReadingEnd);
        }

        public int GetIntervalNo(DateTime readingTime, bool isReadingEnd = true)
        {
            return GetIntervalNo(PeriodType, Offset, readingTime, DatabaseIntervalSeconds, isReadingEnd);
        }

        public static DateTime GetPeriodStart(PeriodType periodType, TimeSpan periodOffset, DateTime readingTime, bool isReadingEnd = true)
        {
            TimeSpan offset = CalcStandardStartOffset(periodType, readingTime - periodOffset);
            if (isReadingEnd && offset == TimeSpan.Zero)            
                return CalcRelativeDateTime(periodType, readingTime, -1);  // for an end time this is the end of the last possible reading in the previous period        
            else
                return readingTime - offset;
        }

        public static int GetIntervalNo(PeriodType periodType, TimeSpan periodOffset, DateTime readingTime, int intervalSeconds, bool isReadingEnd = true)
        {
            DateTime periodStart = GetPeriodStart(periodType, periodOffset, readingTime, isReadingEnd);
            TimeSpan offset = readingTime - periodStart;
            Decimal seconds = Math.Round((Decimal)(offset.TotalSeconds), 3);  // round to nearest millisecond
            int res = (int)Math.Truncate(seconds / intervalSeconds);
            if (!isReadingEnd)
                return res;

            Decimal rem = seconds % intervalSeconds;
            if (rem == 0)
            {
                if (res > 0)
                    res--;
                else
                    throw new Exception("DeviceDetailPeriod.GetIntervalNo - End of period reading produced incorrect periodStart" 
                        + " - periodType: " + periodType + " - periodOffset: " + periodOffset
                        + " - readingTime: " + readingTime + " - periodStart: " + periodStart);
            }
            return res;
        }

        // Calculates the timespan from the standard period start datetime of the period containing the specified datetime, to the specified datetime
        public static TimeSpan CalcStandardStartOffset(PeriodType periodType, DateTime dateTime)
        {
            TimeSpan offset;
            if (periodType == PeriodType.Day)
            {
                offset = dateTime.TimeOfDay;
            }
            else if (periodType == PeriodType.Week)
            {
                offset = dateTime.TimeOfDay + TimeSpan.FromDays((int)dateTime.DayOfWeek);
            }
            else if (periodType == PeriodType.Month)
            {
                DateTime startMonth = new DateTime(dateTime.Year, dateTime.Month, 1);
                offset = dateTime - startMonth;
            }
            else if (periodType == PeriodType.Quarter)
            {
                int quarter = (dateTime.Month - 1) / 3;
                DateTime startMonth = new DateTime(dateTime.Year, (quarter * 3) + 1, 1);
                offset = dateTime - startMonth;
            }
            else if (periodType == PeriodType.HalfYear)
            {
                int halfYear = (dateTime.Month - 1) / 6;
                DateTime startMonth = new DateTime(dateTime.Year, (halfYear * 6) + 1, 1);
                offset = dateTime - startMonth;
            }
            else if (periodType == PeriodType.Year)
            {
                DateTime startMonth = new DateTime(dateTime.Year, 1, 1);
                offset = dateTime - startMonth;
            }
            else
                throw new NotImplementedException("Period.CalcStandardStartOffset - no implementation for: " + periodType);

            return offset;

        }

        private void SetDuration()
        {
            if (PeriodType == PeriodType.Day)
                Duration = TimeSpan.FromDays(1.0);
            else if (PeriodType == PeriodType.Week)
                Duration = TimeSpan.FromDays(7.0);
            else if (PeriodType == PeriodType.Month)
            {
                End = StartInternal.AddMonths(1);
                Duration = End - Start;
                return;
            }
            else if (PeriodType == PeriodType.Quarter)
            {
                End = StartInternal.AddMonths(3);
                Duration = End - Start;
                return;
            }
            else if (PeriodType == PeriodType.HalfYear)
            {
                End = StartInternal.AddMonths(6);
                Duration = End - Start;
                return;
            }
            else if (PeriodType == PeriodType.Year)
            {
                End = StartInternal.AddYears(1);
                Duration = End - Start;
                return;
            }
            else
                throw new NotImplementedException("Period.SetDuration - no implementation for: " + PeriodType);

            End = StartInternal + Duration;
        }

        // Calculate a Period start time periodCount periods from the current Period
        private DateTime CalcRelativeStart(int periodCount)
        {
            return CalcRelativeDateTime(PeriodType, Start, periodCount);
        }

        public static DateTime CalcRelativeDateTime(PeriodType periodType, DateTime dateTime, int periodCount)
        {
            if (periodType == PeriodType.Day)
                return dateTime.AddDays(periodCount);
            else if (periodType == PeriodType.Week)
                return dateTime.AddDays(periodCount * 7);
            else if (periodType == PeriodType.Month)
                return dateTime.AddMonths(periodCount);
            else if (periodType == PeriodType.Quarter)
                return dateTime.AddMonths(periodCount * 3);
            else if (periodType == PeriodType.HalfYear)
                return dateTime.AddMonths(periodCount * 6);
            else if (periodType == PeriodType.Year)
                return dateTime.AddYears(periodCount);
            else
                throw new NotImplementedException("CalcRelativeDateTime - no implementation for: " + periodType);
        }

        // Input is an interval spec, output is the fraction of the interval that overlaps
        public Double GetIntervalOverlapFactor(DateTime intervalStart, DateTime intervalEnd)
        {
            TimeSpan duration = intervalEnd - intervalStart;
            TimeSpan overlap = duration;

            if (intervalStart < Start)
                if (intervalEnd > Start)
                    overlap -= Start - intervalStart;
                else
                    return 0.0;

            if (intervalEnd > End)
                if (intervalStart < End)
                    overlap -= intervalEnd - End;
                else
                    return 0.0;

            return overlap.TotalSeconds / duration.TotalSeconds;
        }

        public void GetIntervalInfo(DateTime dateTime, out DateTime PeriodStart, out int Interval)
        {
            DateTime periodStart = GetPeriodStart(dateTime, false);
            TimeSpan offset = dateTime - periodStart;
            int interval = (int)(((int)offset.TotalSeconds) / DatabaseIntervalSeconds);
            //IsIntervalStart = (TimeSpan.FromSeconds(interval * DatabaseIntervalSeconds) == offset);
            Interval = interval;
            PeriodStart = periodStart;
        }

        public static void GetIntervalInfo(PeriodType periodType, TimeSpan periodOffset, int intervalSeconds, DateTime dateTime, out DateTime PeriodStart, out int Interval, out bool IsIntervalStart)
        {
            DateTime periodStart = GetPeriodStart(periodType, periodOffset, dateTime, false);
            TimeSpan offset = dateTime - periodStart;
            int interval = (int)(((int)offset.TotalSeconds) / intervalSeconds);
            IsIntervalStart = (TimeSpan.FromSeconds(interval * intervalSeconds) == offset);
            Interval = interval;
            PeriodStart = periodStart;
        }
    }

    public abstract class DeviceDetailPeriodBase : PeriodBase
    {
        public DeviceDetailPeriodsBase DeviceDetailPeriods { get; private set; }

        public FeatureType FeatureType { get; private set; }
        public uint FeatureId { get; private set; }

        public int DeviceIntervalSeconds { get; private set; }  // DeviceIntervalSeconds is the expected raw reading interval. It may vary due to device behaviour

        public bool UpdatePending { get; protected set; }

        protected DeviceDataRecorders.DeviceParamsBase DeviceParams;
        public DateTime LastFindTime { get; set; }
        protected TimeSpan PeriodOverlapLimit = TimeSpan.FromHours(4.0);

        private int? _DeviceId = null;
        public int? DeviceId 
        {
            get
            {
                if (_DeviceId.HasValue)
                    return _DeviceId;
                else
                {
                    _DeviceId = DeviceDetailPeriods.Device.DeviceId;
                    return _DeviceId;
                }
            }
        }

        protected ReadingsCollection ReadingsGeneric;

        public DeviceDetailPeriodBase(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature)
            : base(periodType, periodStart, deviceDetailPeriods.Device.DeviceParams.RecordingInterval)
        {
            DeviceDetailPeriods = deviceDetailPeriods;
            DeviceParams = DeviceDetailPeriods.Device.DeviceParams;
            FeatureType = feature.FeatureType;
            FeatureId = feature.FeatureId;
            DeviceIntervalSeconds = DeviceParams.QueryInterval;
            UpdatePending = false;
            LastFindTime = DateTime.Now;

            ReadingsGeneric = new ReadingsCollection(this); 
        }

        public void PeriodIsDirty()
        {
            UpdatePending = true;
        }

        public DateTime GetIntervalDateTime(DateTime end)
        {
            // convert to interval and back to DateTime to get the standardised DateTime value for the specified interval
            return GetDateTime(GetIntervalNo(end, false));
        }

        public DateTime GetDateTime(int interval)
        {
            // where intervalSeconds == 6
            // interval == 0 represents an interval end time of 6 seconds; represents time >= 0 to < 6
            // interval == 0 represents an interval start time of 0 seconds
            // interval == 1 represents an interval end time of 12 seconds; represents time >= 6 to < 12
            // interval == 1 represents an interval end time of 6 seconds
            return Start.AddSeconds((interval + 1) * DatabaseIntervalSeconds);
        }

        public DateTime GetStartDateTime(int interval)
        {
            return Start.AddSeconds(interval * DatabaseIntervalSeconds);
        }

        public virtual void UpdateReadings()
        {
            // normal devices (not consolidations) are always up to date
            // consolidations override this to ensure current day readings are up to date
        }

        public void SetAddReadingMatch(bool? value, DateTime fromTime, DateTime toTime)
        {
            foreach (ReadingBase reading in ReadingsGeneric.ReadingList)
                if (reading.ReadingEnd >= fromTime && reading.ReadingEnd <= toTime)
                    reading.AddReadingMatch = value;
        }

        public void UpdateDatabase(GenConnection con, DateTime? activeReadingTime, bool purgeUnmatched, DateTime? consolidateTo)
        {
            //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Entry", LogEntryType.Trace);
            int activeInterval = -1;
            if (activeReadingTime.HasValue)  // this is used on the active day (today) to limit Normalise to the completed intervals only
                activeInterval = GetIntervalNo(activeReadingTime.Value);
            //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Before Normalise", LogEntryType.Trace);
            ReadingsGeneric.FillSmallGaps(Start, End, false);
            //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Before Loop", LogEntryType.Trace);
            if (consolidateTo.HasValue)
                ReadingsGeneric.ConsolidateIntervals(consolidateTo.Value);
            ReadingsGeneric.CheckReadingsIntegrity();

            for (int i = 0; i < ReadingsGeneric.Count; )
            {
                //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Looping", LogEntryType.Trace);
                ReadingBase reading = ReadingsGeneric.ReadingList[i];
                if (reading.UpdatePending)
                {
                    //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - UpdatePending", LogEntryType.Trace);
                    if (activeReadingTime.HasValue
                    && activeReadingTime.Value.Date == Start
                    && activeInterval == GetIntervalNo(reading.ReadingEnd))
                    {
                        i++;
                        continue;
                    }
                    //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Before PersistReading", LogEntryType.Trace);
                    reading.PersistReading(con, DeviceId.Value);
                    i++;
                }
                else if (purgeUnmatched && reading.AddReadingMatch.HasValue ? !reading.AddReadingMatch.Value : false) // remove any old reading that was not found in this update
                {
                    //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - BeforeDeleteReading", LogEntryType.Trace);
                    reading.DeleteReading(con, DeviceId.Value);
                    ReadingsGeneric.RemoveReadingAt(i);
                }
                else
                    i++;
                //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Before Purge", LogEntryType.Trace);
                if (purgeUnmatched)
                    reading.AddReadingMatch = null; // reset ready for next update - only when purge is active
            }
            UpdatePending = false;
        }

        public void AddReading(ReadingBase reading, AddReadingType addReadingType = AddReadingType.NewReading, ConsolidateDeviceSettings.OperationType operation = ConsolidateDeviceSettings.OperationType.Add)
        {
            //String stage = "Initial";
            if (reading.ReadingEnd <= Start)
                throw new Exception("DeviceDetailPeriod.AddReading - Reading belongs on a previous day - ReadingEnd: " + reading.ReadingEnd + " - This day: " + Start);
            if (reading.ReadingStart >= End)
                throw new Exception("DeviceDetailPeriod.AddReading - Reading belongs on a subsequent day - ReadingEnd: " + reading.ReadingEnd + " - This day: " + Start);

            ReadingsGeneric.AddReading(reading, 
                (addReadingType == AddReadingType.NewReading) ? ReadingsCollection.AddReadingMode.Insert :
                (addReadingType == AddReadingType.History) ? ReadingsCollection.AddReadingMode.InsertReplace :
                (addReadingType == AddReadingType.FillGaps) ? ReadingsCollection.AddReadingMode.FillGaps : ReadingsCollection.AddReadingMode.Insert);

            reading.RegisterPeriodInvolvement(this);
            if (reading.UpdatePending)
                PeriodIsDirty();
        }

        public abstract ReadingBase NewReadingGeneric(DateTime outputTime, TimeSpan duration, ReadingBase pattern = null);

        public abstract void SplitReading(ReadingBase oldReading, DateTime splitTime, out ReadingBase newReading1, out ReadingBase newReading2);

        protected void CalcFromPrevious(ReadingBase stopHere)
        {
            // perform reading adjustments based upon a delta with the previous reading
            ReadingBase previous = null;
            foreach (ReadingBase r in ReadingsGeneric.ReadingList)
            {
                r.CalcFromPreviousGeneric(previous);
                if (r.IsSameReadingGeneric(stopHere))  // used prior to Clone to provide valid EnergyDelta values
                    return;
                previous = r;
            }
        }
    }

    public abstract class DeviceDetailPeriod<TDeviceReading, TDeviceHistory>: DeviceDetailPeriodBase
        where TDeviceReading : ReadingBaseTyped<TDeviceReading, TDeviceHistory>, IComparable<TDeviceReading>
        where TDeviceHistory : ReadingBase
    {          
        public DeviceDetailPeriod(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature) 
            : base(deviceDetailPeriods, periodType, periodStart, feature)
        {         
        }

        // returns the types readings - they are sorted at time of return
        public List<TDeviceReading> GetReadings()
        {
            bool haveMutex = false;
            try
            {
                List<TDeviceReading> readings = new List<TDeviceReading>();
                ReadingsGeneric.RecordsMutex.WaitOne();
                haveMutex = true;
                foreach (ReadingBase r in ReadingsGeneric.ReadingList)
                {
                    readings.Add((TDeviceReading)r);
                }
                return readings;
            }
            finally
            {
                if (haveMutex)
                    ReadingsGeneric.RecordsMutex.ReleaseMutex();
            }
        }

        protected void SplitReadingCore(ReadingBase oldReading, DateTime splitTime, out ReadingBase newReading1, out ReadingBase newReading2)
        {
            // Ensure Delta calculations are up to date prior to Clone
            TimeSpan newDuration2 = oldReading.ReadingEnd - splitTime;
            if (DeviceParams.UseCalculateFromPrevious)
                CalcFromPrevious(oldReading);
            newReading1 = oldReading.CloneGeneric(splitTime, oldReading.Duration - newDuration2);
            newReading2 = oldReading.CloneGeneric(oldReading.ReadingEnd, newDuration2);
        }

        public override void SplitReading(ReadingBase oldReading, DateTime splitTime, out ReadingBase newReading1, out ReadingBase newReading2)
        {
            ReadingBase reading1;
            ReadingBase reading2;
            SplitReadingCore((ReadingBase)oldReading, splitTime, out reading1, out reading2);
            DeviceDetailPeriods.Device.SplitReadingSub(oldReading, splitTime, reading1, reading2);
            newReading1 = reading1;
            newReading2 = reading2;
        }

        public void ClearReadings()
        {            
            ReadingsGeneric.ClearReadings();
        }

        protected virtual void AdjustHistory(TDeviceReading prevReading, TDeviceReading reading, TDeviceReading nextReading, TDeviceHistory histRecord)
        {
        }

        protected abstract TDeviceReading NewReading(DateTime outputTime, TimeSpan duration, TDeviceReading pattern = null);

        public override ReadingBase NewReadingGeneric(DateTime readingEnd, TimeSpan duration, ReadingBase pattern = null)
        {
            return (ReadingBase)NewReading(readingEnd, duration, (TDeviceReading)pattern);
        }

        public virtual int CompareTo(DeviceDetailPeriod<TDeviceReading, TDeviceHistory> other)
        {
            if (other.Start < Start)
                return -1;
            if (other.Start > Start)
                return 1;
            if (other.FeatureType < FeatureType)
                return -1;
            if (other.FeatureType > FeatureType)
                return 1;
            if (other.FeatureId < FeatureId)
                return -1;
            if (other.FeatureId > FeatureId)
                return 1;
            return 0;
        }

        private TDeviceReading MergeReadings(DateTime consolidatedEndTime, DateTime startTime, DateTime endTime, bool accumulateDuration)
        {
            TDeviceReading newReading;
            if (accumulateDuration)
                newReading = NewReading(consolidatedEndTime, TimeSpan.Zero, default(TDeviceReading));
            else
                newReading = NewReading(consolidatedEndTime, endTime - startTime, default(TDeviceReading));

            foreach(TDeviceReading reading in ReadingsGeneric.ReadingList)
            {
                TDeviceReading thisReading = reading;
                if (thisReading.ReadingEnd <= startTime)
                    continue;
                if (thisReading.ReadingStart >= endTime)
                    break;

                if (accumulateDuration && reading.IsHistoryReading()) // no accumulation of gap fill readings when accumulateDuration
                    continue;

                if ((consolidatedEndTime - newReading.ReadingEnd).TotalSeconds > DatabaseIntervalSeconds)
                    throw new Exception("DeviceDetailPeriod.MergeReadings - reading: " + newReading.ReadingEnd + " - too old for: " + consolidatedEndTime);

                // Trim overhangs
                if (thisReading.ReadingStart < startTime)
                    thisReading = reading.Clone(thisReading.ReadingEnd, thisReading.ReadingEnd - startTime);
                if (thisReading.ReadingEnd > endTime)
                    thisReading = reading.Clone(endTime, endTime - thisReading.ReadingStart);

                newReading.AccumulateReading(thisReading, true, accumulateDuration);

                // If the Output time on an existing reading aligns with an interval end, this entry may already be in the DB
                // mark new entry with existing status as they share the same DB key
                if (reading.ReadingEnd == consolidatedEndTime)
                    newReading.InDatabase = reading.InDatabase;
            }
            
            //if (newReading.GetModeratedSeconds(3) > DatabaseIntervalSeconds)
            //    throw new Exception("DeviceDetailPeriod.MergeReadings - Duration too large: " + newReading.Duration);
            return newReading;
        }

        private void FillLargeGaps(TDeviceReading actualTotal, TDeviceHistory histRecord, TimeSpan gapsRemaining, DateTime startTime, int startInterval, int endInterval)
        {
            int i = 0;  // position in Readings
            TDeviceReading reading = default(TDeviceReading);
            TDeviceReading prevReading = default(TDeviceReading);
            TDeviceReading nextReading = default(TDeviceReading);
            DateTime prevEndTime = startTime;
            TimeSpan histGap = histRecord.Duration;

            while (i < ReadingsGeneric.Count)
            {
                reading = (TDeviceReading)ReadingsGeneric.ReadingList[i];
                if ((i + 1) < ReadingsGeneric.Count)
                    nextReading = (TDeviceReading)ReadingsGeneric.ReadingList[i + 1];
                else
                    nextReading = default(TDeviceReading);

                int readingInterval = GetIntervalNo(reading.ReadingEnd);
                // exit at end of relevant intervals
                if (readingInterval > endInterval)
                    break;
                // ignore entries before relevant intervals
                if (readingInterval < startInterval)
                {
                    i++;
                    continue;
                }

                if (reading.IsHistoryReading())
                {
                    if (GlobalSettings.SystemServices.LogTrace)
                        GlobalSettings.SystemServices.LogMessage("FillLargeGaps", "IsHistoryReading - ReadingStart: " + reading.ReadingStart + " - ReadingEnd: " + reading.ReadingEnd
                            + " - actualTotal: " + actualTotal + " - histRecord: " + histRecord.GetReadingLogDetails(), LogEntryType.Trace);
                    // this reading was created in a previous Gap Fill operation and needs a history value based on duration
                    reading.HistoryAdjust_Average(actualTotal, histRecord);
                }

                histGap -= reading.Duration;

                TimeSpan gap = reading.ReadingStart - prevEndTime;

                if (gap > ReadingsGeneric.SmallGapUpperLimit)
                {
                    TDeviceReading newRec = NewReading(reading.ReadingStart, gap, (nextReading != null) ? nextReading : ((prevReading != null) ? prevReading : default(TDeviceReading)));
                    newRec.HistoryAdjust_Average(actualTotal, histRecord);
                    AddReading(newRec);
                    if (GlobalSettings.SystemServices.LogTrace)
                        GlobalSettings.SystemServices.LogMessage("FillLargeGaps", "ResidualGap - ReadingStart: " + newRec.ReadingStart + " - ReadingEnd: " + newRec.ReadingEnd
                            + " - actualTotal: " + actualTotal + " - histRecord: " + newRec.GetReadingLogDetails(), LogEntryType.Trace);
                    i++;                    
                }
                histGap -= gap;

                prevEndTime = reading.ReadingEnd;
                prevReading = reading;
                i++;
            }

            DateTime endTime = GetDateTime(endInterval);
            if ((prevEndTime + histGap) != endTime)
                throw new Exception("DeviceDetailPeriod.FillLargeGaps - end gap mismatch - endTime: " + endTime + " - prevEndTime: " + prevEndTime + " - histGap: " + histGap);

            if (histGap > ReadingsGeneric.SmallGapUpperLimit)
            {                
                TDeviceReading newRec = NewReading(endTime, histGap, prevReading == null ? default(TDeviceReading) : prevReading);
                newRec.HistoryAdjust_Average(actualTotal, histRecord);
                AddReading(newRec);
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.SystemServices.LogMessage("FillLargeGaps", "Final ResidualGap - ReadingStart: " + newRec.ReadingStart + " - ReadingEnd: " + newRec.ReadingEnd
                        + " - actualTotal: " + actualTotal + " - histRecord: " + newRec.GetReadingLogDetails(), LogEntryType.Trace);
            }
        }

        private void ProrataRemainingHistory(TDeviceReading actualTotal, TDeviceHistory histRecord, DateTime startTime, int startInterval, int endInterval)
        {
            int i = 0;  // position in Readings
            TDeviceReading reading = default(TDeviceReading);
            int count = ReadingsGeneric.ReadingList.Count;
            while (i < count)
            {
                reading = (TDeviceReading)ReadingsGeneric.ReadingList[i];

                int readingInterval = GetIntervalNo(reading.ReadingEnd);
                // exit at end of relevant intervals
                if (readingInterval > endInterval)
                    break;
                // ignore entries before relevant intervals
                if (readingInterval < startInterval)
                {
                    i++;
                    continue;
                }

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.SystemServices.LogMessage("ProrataRemainingHistory", "ReadingStart: " + reading.ReadingStart + " - ReadingEnd: " + reading.ReadingEnd
                        + " - actualTotal: " + actualTotal + " - histRecord: " + reading.GetReadingLogDetails(), LogEntryType.Trace);
                reading.HistoryAdjust_Prorata(actualTotal, histRecord); ;
                
                i++;
            }
        }

        public void AdjustFromHistory(TDeviceHistory histReading)
        {
            String stage = "initial";
            try
            {
                DateTime startTime = histReading.ReadingStart;
                int endInterval = GetIntervalNo(histReading.ReadingEnd);
                int startInterval = GetIntervalNo(histReading.ReadingStart, false);

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.SystemServices.LogMessage("AdjustFromHistory", stage + " - End: " + histReading.ReadingEnd
                        + " - Duration: " + histReading.Duration + " - startInterval: " + startInterval + " - endInterval: " + endInterval, LogEntryType.Trace);

                stage = "validation";
                if (histReading.ReadingEnd <= Start || (histReading.ReadingEnd - Start).TotalHours > 24.0)
                    throw new Exception("DeviceDetailPeriod.AdjustFromHistory - End time is wrong day - endTime: " + histReading.ReadingEnd + " - Day: " + Start);

                if (GetDateTime(endInterval) != histReading.ReadingEnd)
                    throw new Exception("DeviceDetailPeriod.AdjustFromHistory - End time does not align with interval boundary - endTime: " + histReading.ReadingEnd + " - interval: " + DatabaseIntervalSeconds);

                if (startTime < Start)
                    throw new Exception("DeviceDetailPeriod.AdjustFromHistory - startTime is wrong day - startTime: " + startTime + " - Day: " + Start);

                if (GetDateTime(startInterval)
                    != startTime + TimeSpan.FromSeconds(DatabaseIntervalSeconds))
                    throw new Exception("DeviceDetailPeriod.AdjustFromHistory - startTime does not align with interval boundary - endTime: " + histReading.ReadingEnd + " - interval: " + DatabaseIntervalSeconds);

                stage = "ClearHistory";
                // clear old history entries
                ClearHistory(histReading.ReadingStart, histReading.ReadingEnd);
                if (DeviceParams.UseCalculateFromPrevious)
                {
                    stage = "CalcFromPrevious";
                    CalcFromPrevious(default(TDeviceReading));
                }

                stage = "FillSmallGaps";
                // fill any gaps up to 30 secs with prorata adjacent values - creates actuals not calculated values
                TimeSpan remainingGaps = ReadingsGeneric.FillSmallGaps(histReading.ReadingStart, histReading.ReadingEnd, true);

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.SystemServices.LogMessage("AdjustFromHistory", stage + " - End: " + histReading.ReadingEnd
                        + " - Duration: " + histReading.Duration + " - remainingGaps(ticks): " + remainingGaps.Ticks , LogEntryType.Trace);

                // obtain actual total - uses Consolidate
                stage = "MergeReadings 1";                
                TDeviceReading actualTotal = MergeReadings(GetDateTime(endInterval), histReading.ReadingStart, histReading.ReadingEnd, true);
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.SystemServices.LogMessage("AdjustFromHistory", stage + " - End: " + histReading.ReadingEnd
                        + " - actualTotal: " + actualTotal + " - histRecord: " + histReading.GetReadingLogDetails(), LogEntryType.Trace);
                // fill all remaining gaps with prorata history value
                // FillLargeGaps also restores hist values to previous history readings cleared by ClearHistory
                //if (remainingGaps > ReadingsGeneric.SmallGapUpperLimit)
                {
                    stage = "FillLargeGaps";
                    FillLargeGaps(actualTotal, histReading, remainingGaps, startTime, startInterval, endInterval);

                    stage = "MergeReadings 2";
                    // recalculate actualTotal to capture large gap additions
                    actualTotal = actualTotal = MergeReadings(GetDateTime(endInterval), histReading.ReadingStart, histReading.ReadingEnd, false); // include large gap additions
                    if (GlobalSettings.SystemServices.LogTrace)
                        GlobalSettings.SystemServices.LogMessage("AdjustFromHistory", stage + " - End: " + histReading.ReadingEnd
                            + " - actualTotal: " + actualTotal + " - histRecord: " + histReading.GetReadingLogDetails(), LogEntryType.Trace);
                }
                stage = "ProrataRemainingHistory";
                // apportion outstanding history values by prorata adjustment 
                if (actualTotal.Compare(histReading) != 0)
                {
                    if (GlobalSettings.SystemServices.LogTrace)
                        GlobalSettings.SystemServices.LogMessage("AdjustFromHistory", stage + " - End: " + histReading.ReadingEnd
                            + " - actualTotal: " + actualTotal + " - startTime: " + startTime 
                            + " - startInterval: " + startInterval + " - endInterval: " + endInterval, LogEntryType.Trace);

                    ProrataRemainingHistory(actualTotal, histReading, startTime, startInterval, endInterval);
                }
            }
            catch (Exception e)
            {
                throw new Exception("AdjustFromHistory - Stage: " + stage + " - Exception: " + e.Message, e);
            }
        }

        private void ClearHistory(DateTime readingStart, DateTime readingEnd)
        {
            foreach (ReadingBase reading in ReadingsGeneric.ReadingList)
            {
                if (reading.ReadingEnd > readingStart) 
                    if (reading.ReadingStart < readingEnd)
                        reading.ClearHistory();
                    else
                        break;
            }
        }

        protected void BindSelectIdentity(GenCommand cmd)
        {
            if (DeviceDetailPeriods == null)
                GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.BindSelectIdentity", "*******DeviceDetailPeriods is null", LogEntryType.ErrorMessage);
            if (Start == null)
                GlobalSettings.LogMessage("DeviceDetailPeriod_EnergyMeter.BindSelectIdentity", "*******Start is null", LogEntryType.ErrorMessage);

            int tmpInt = (int)DeviceDetailPeriods.DeviceFeatureId;
            cmd.AddParameterWithValue("@DeviceFeature_Id", tmpInt); // SQLServer won't accept uint in this binding
            cmd.AddParameterWithValue("@PeriodStart", Start - PeriodOverlapLimit);
            cmd.AddParameterWithValue("@NextPeriodStart", Start.AddDays(1.0) + PeriodOverlapLimit);
        }

        public void ConsolidateReading(TDeviceReading reading, bool useTemperature, int nextInterval, ConsolidateDeviceSettings.OperationType operation = ConsolidateDeviceSettings.OperationType.Add)
        {
            if (GlobalSettings.SystemServices.LogDetailTrace)
                GlobalSettings.LogMessage("ConsolidateReading", "TRACE Start: " + reading.ReadingStart + " - End: " + reading.ReadingEnd, LogEntryType.DetailTrace);
            // discard readings that are not relevant to this consolidation period
            if (reading.ReadingEnd <= Start)
                return;
            if (reading.ReadingStart >= End)
                return;

            // trim readings that span the start of period boundary
            if (reading.ReadingStart < Start)
            {
                if (GlobalSettings.SystemServices.LogDetailTrace)
                    GlobalSettings.LogMessage("ConsolidateReading", "TRACE Start Trim", LogEntryType.DetailTrace);
                if ((Start - reading.ReadingStart) > PeriodOverlapLimit)
                    throw new Exception("ConsolidateReading - Period overlap exceeds limit - ReadingStart: " + reading.ReadingStart + " - Period Start: " + Start);
                if (DeviceParams.EnforceRecordingInterval)
                {
                    ReadingBase discardReading;
                    ReadingBase keepReading;
                    SplitReading((ReadingBase)reading, Start, out discardReading, out keepReading);
                    reading = (TDeviceReading)keepReading;
                    if (GlobalSettings.SystemServices.LogDetailTrace)
                        GlobalSettings.LogMessage("ConsolidateReading", "TRACE keep Start: " + reading.ReadingStart + " - End: " + reading.ReadingEnd, LogEntryType.DetailTrace);
                }
            }
            // trim readings that span the end of period boundary
            if (reading.ReadingEnd > End)
            {
                if (GlobalSettings.SystemServices.LogDetailTrace)
                    GlobalSettings.LogMessage("ConsolidateReading", "TRACE End Trim", LogEntryType.DetailTrace);
                if ((reading.ReadingEnd - End) > PeriodOverlapLimit)
                    throw new Exception("ConsolidateReading - Period overlap exceeds limit - ReadingEnd: " + reading.ReadingStart + " - Period End: " + Start);
                if (DeviceParams.EnforceRecordingInterval)
                {
                    ReadingBase discardReading;
                    ReadingBase keepReading;
                    SplitReading(reading, End, out keepReading, out discardReading);
                    reading = (TDeviceReading)keepReading;
                    if (GlobalSettings.SystemServices.LogDetailTrace)
                        GlobalSettings.LogMessage("ConsolidateReading", "TRACE keep Start: " + reading.ReadingStart + " - End: " + reading.ReadingEnd, LogEntryType.DetailTrace);
                }
            }

            int interval;
            //bool isIntervalStart;
            DateTime start;
            DateTime currentStart = reading.ReadingStart;

            if (GlobalSettings.SystemServices.LogDetailTrace)
                GlobalSettings.LogMessage("ConsolidateReading", "TRACE Enter consolidation loop - currentStart: " + currentStart, LogEntryType.DetailTrace);
            do // if source reading spans multiple consolidation readings - divide at consolidation interval boundaries
            {
                GetIntervalInfo(currentStart, out start, out interval);
                if (GlobalSettings.SystemServices.LogDetailTrace)
                    GlobalSettings.LogMessage("ConsolidateReading", "TRACE GetIntervalInfo returns - start: " + start + " - interval: " + interval, LogEntryType.DetailTrace);
                if (start != Start)
                    throw new Exception("ConsolidateReading - consolidation mismatch - Calc start: " + start + " - Required start: " + Start);
                DateTime intervalEnd = start + TimeSpan.FromSeconds((interval + 1) * DatabaseIntervalSeconds);

                int index = ReadingsGeneric.IndexOfKey(intervalEnd);
                if (GlobalSettings.SystemServices.LogDetailTrace)
                    GlobalSettings.LogMessage("ConsolidateReading", "TRACE IndexOfKey intervalEnd: " + intervalEnd + " - Returns index: " + index, LogEntryType.DetailTrace);
                TDeviceReading toReading;
                if (index < 0)
                {
                    toReading = NewReading(intervalEnd, TimeSpan.FromSeconds(DatabaseIntervalSeconds), null);
                    if (GlobalSettings.SystemServices.LogDetailTrace)
                        GlobalSettings.LogMessage("ConsolidateReading", "TRACE index < 0 Adding Start: " + toReading.ReadingStart + " - End: " + toReading.ReadingEnd, LogEntryType.DetailTrace);
                    ReadingsGeneric.AddReading(toReading);
                    toReading.RegisterPeriodInvolvement(this);
                }
                else
                {
                    toReading = (TDeviceReading)ReadingsGeneric.ReadingList[index];
                    if (GlobalSettings.SystemServices.LogDetailTrace)
                        GlobalSettings.LogMessage("ConsolidateReading", "TRACE reading Start: " + toReading.ReadingStart + " - End: " + toReading.ReadingEnd, LogEntryType.DetailTrace);
                }

                if (currentStart == reading.ReadingStart && intervalEnd >= reading.ReadingEnd)  // no division required - reading fits in one consolidation interval
                {
                    if (GlobalSettings.SystemServices.LogDetailTrace)
                        GlobalSettings.LogMessage("ConsolidateReading", "TRACE AccumulateReading no split" , LogEntryType.DetailTrace);
                    // note - AccumulateDuration must be false for multi device consolidations - it is only used for single device reading merge
                    toReading.AccumulateReading(reading, useTemperature, false, false, operation == ConsolidateDeviceSettings.OperationType.Subtract ? -1.0 : 1.0);
                }
                else
                {
                    TimeSpan duration;
                    if (intervalEnd >= reading.ReadingEnd)
                        duration = reading.ReadingEnd - currentStart; // tail end of reading - may be less tha one interval duration
                    else
                        duration = intervalEnd - currentStart; // beginning or middle of spanned reading
                    TDeviceReading intervalReading = reading.Clone(intervalEnd, duration); // get time adjusted reading
                    if (GlobalSettings.SystemServices.LogDetailTrace)
                        GlobalSettings.LogMessage("ConsolidateReading", "TRACE AccumulateReading with split Start: " 
                            + intervalReading.ReadingStart + " - End: " + intervalReading.ReadingEnd, LogEntryType.DetailTrace);
                    // note - AccumulateDuration must be false for multi device consolidations - it is only used for single device reading merge
                    toReading.AccumulateReading(intervalReading, useTemperature, false, false, operation == ConsolidateDeviceSettings.OperationType.Subtract ? -1.0 : 1.0);
                }

                currentStart = intervalEnd; // prepare for next interval iteration
                if (GlobalSettings.SystemServices.LogDetailTrace)
                    GlobalSettings.LogMessage("ConsolidateReading", "TRACE end one loop - currentStart: " + currentStart, LogEntryType.DetailTrace);
            }
            while (currentStart < reading.ReadingEnd);
            if (GlobalSettings.SystemServices.LogDetailTrace)
                GlobalSettings.LogMessage("ConsolidateReading", "TRACE Exit consolidation loop", LogEntryType.DetailTrace);
        }
    }

    public abstract class DeviceDetailPeriod_Physical<TDeviceReading, TDeviceHistory> : DeviceDetailPeriod<TDeviceReading, TDeviceHistory>
        where TDeviceReading : ReadingBaseTyped<TDeviceReading, TDeviceHistory>, IComparable<TDeviceReading>
        where TDeviceHistory : ReadingBase
    {
        public DeviceDetailPeriod_Physical(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature)
            : base(deviceDetailPeriods, periodType, periodStart, feature)
        {
            
        }

        public abstract void LoadPeriodFromDatabase(GenConnection existingCon = null);
    }

    public abstract class DeviceDetailPeriod_Consolidation<TDeviceReading, TDeviceHistory> : DeviceDetailPeriod<TDeviceReading, TDeviceHistory>
        where TDeviceReading : ReadingBaseTyped<TDeviceReading, TDeviceHistory>, IComparable<TDeviceReading>
        where TDeviceHistory : ReadingBase
    {
        public DeviceDetailPeriod_Consolidation(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature)
            : base(deviceDetailPeriods, periodType, periodStart, feature)
        {

        }

        public void LoadPeriodFromConsolidations()
        {
            try
            {
                if (GlobalSettings.SystemServices.LogDetailTrace)
                    GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE ClearReadings", LogEntryType.DetailTrace);
                ClearReadings();
                // for each device feature that consolidates to this device (owner of this period)
                if (GlobalSettings.SystemServices.LogDetailTrace)
                    GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE Enter devLink source devices loop", LogEntryType.DetailTrace);
                foreach (Device.DeviceLink devLink in ((Device.ConsolidationDevice)DeviceDetailPeriods.Device).SourceDevices)
                {
                    if (GlobalSettings.SystemServices.LogDetailTrace)
                        GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE FeatureType: " + FeatureType.ToString() + " - FeatureId: " + FeatureId, LogEntryType.DetailTrace);
                    // to link must match this consolidation - consolidate the matching from link
                    if (FeatureType == devLink.ToFeatureType && FeatureId == devLink.ToFeatureId)
                    {
                        if (GlobalSettings.SystemServices.LogDetailTrace)
                            GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE devLink Matched From FeatureType: " + devLink.FromFeatureType.ToString() 
                                + " - FeatureId: " + devLink.FromFeatureId, LogEntryType.DetailTrace);
                        if (!devLink.FromDevice.DeviceId.HasValue)
                            devLink.FromDevice.GetDeviceId(null);
                        if (GlobalSettings.SystemServices.LogDetailTrace)
                            GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE Find Feature Periods", LogEntryType.DetailTrace);
                        DeviceDetailPeriodsBase periods = devLink.FromDevice.FindOrCreateFeaturePeriods(devLink.FromFeatureType, devLink.FromFeatureId);
                        // step through all periods in the period container
                        if (GlobalSettings.SystemServices.LogDetailTrace)
                            GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE GetPeriodEnumerator - Start: " + Start + " - End: " + End, LogEntryType.DetailTrace);
                        PeriodEnumerator pEnum = periods.GetPeriodEnumerator(Start, End);
                        if (GlobalSettings.SystemServices.LogDetailTrace)
                            GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE Enter period loop", LogEntryType.DetailTrace);
                        foreach (PeriodBase p in pEnum)
                        {
                            if (GlobalSettings.SystemServices.LogDetailTrace)
                                GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE Period - Start: " + p.Start + " - End: " + p.End, LogEntryType.DetailTrace);
                            // locate a period with a specific start date
                            // Note - if it does not exist an empty one will be created
                            if (GlobalSettings.SystemServices.LogDetailTrace)
                                GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE FindOrCreatePeriod", LogEntryType.DetailTrace);
                            DeviceDetailPeriod<TDeviceReading, TDeviceHistory> period = (DeviceDetailPeriod<TDeviceReading, TDeviceHistory>)periods.FindOrCreate(p.Start);
                            // step through the readings in one period and consolidate into this period
                            int nextInterval = -1;
                            DateTime start;
                            TDeviceReading prevReading = null;
                            if (GlobalSettings.SystemServices.LogDetailTrace)
                                GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE GetReadings", LogEntryType.DetailTrace);
                            foreach (TDeviceReading r in period.GetReadings())
                            {
                                if (GlobalSettings.SystemServices.LogDetailTrace)
                                    GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE Reading - Start: " + r.ReadingStart + " - End: " + r.ReadingEnd, LogEntryType.DetailTrace);
                                GetIntervalInfo(r.ReadingStart, out start, out nextInterval);
                                if (GlobalSettings.SystemServices.LogDetailTrace)
                                    GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE GetIntervalInfo returned - start: " 
                                        + start + " - nextInterval: " + nextInterval + " - prevReading is " + ((prevReading == null) ? "null" : "not null"), LogEntryType.DetailTrace);
                                if (prevReading != null)
                                    ConsolidateReading(prevReading, devLink.UseTemperature, nextInterval, devLink.Operation);
                                
                                prevReading = r;                                
                            }
                            if (GlobalSettings.SystemServices.LogDetailTrace)
                                GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE GetReadings loop exits", LogEntryType.DetailTrace);
                            if (prevReading != null)
                            {
                                if (GlobalSettings.SystemServices.LogDetailTrace)
                                    GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE Final ConsolidateReading", LogEntryType.DetailTrace);
                                ConsolidateReading(prevReading, devLink.UseTemperature, nextInterval, devLink.Operation);
                            }
                        }
                        devLink.SourceUpdated = false;
                        if (GlobalSettings.SystemServices.LogDetailTrace)
                            GlobalSettings.LogMessage("LoadPeriodFromConsolidations", "TRACE Period loop exits", LogEntryType.DetailTrace);
                    }
                }
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DeviceDetailPeriod_Consolidation.LoadPeriodFromConsolidations", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }

        public bool SourceUpdated
        {
            get
            {
                foreach (Device.DeviceLink l in ((Device.ConsolidationDevice)DeviceDetailPeriods.Device).SourceDevices)
                {
                    if (l.SourceUpdated && l.ToFeatureType == FeatureType && l.ToFeatureId == FeatureId)  
                        return true;
                }
                return false;
            }
        }

        public override void UpdateReadings()
        {
            // consolidations override this to ensure current day readings are up to date
            // Always reevaluate yesterday on first run after switch to new day
            if (SourceUpdated)
                LoadPeriodFromConsolidations();
        }
    }

}
