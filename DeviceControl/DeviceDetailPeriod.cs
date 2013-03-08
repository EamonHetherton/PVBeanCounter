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
        Consolidation
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
        }

        public DateTime GetPeriodStart(DateTime readingTime, bool IsReadingEnd = true)
        {
            return GetPeriodStart(PeriodType, Offset, readingTime, IsReadingEnd);
        }

        public static DateTime GetPeriodStart(PeriodType periodType, TimeSpan periodOffset, DateTime readingTime, bool IsReadingEnd = true)
        {
            TimeSpan offset = CalcStandardStartOffset(periodType, readingTime - periodOffset);
            if (IsReadingEnd)
            {
                if (offset == TimeSpan.Zero)
                    return CalcRelativeDateTime(periodType, readingTime, -1);
                else
                    return readingTime;
            }
            else
                return readingTime - offset;
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

        public void GetIntervalInfo(DateTime dateTime, out DateTime PeriodStart, out uint Interval, out bool IsIntervalStart)
        {
            DateTime periodStart = GetPeriodStart(dateTime, false);
            TimeSpan offset = dateTime - periodStart;
            uint interval = (uint)(((uint)offset.TotalSeconds) / DatabaseIntervalSeconds);
            IsIntervalStart = (TimeSpan.FromSeconds(interval * DatabaseIntervalSeconds) == offset);
            Interval = interval;
            PeriodStart = periodStart;
        }

        public static void GetIntervalInfo(PeriodType periodType, TimeSpan periodOffset, int intervalSeconds, DateTime dateTime, out DateTime PeriodStart, out uint Interval, out bool IsIntervalStart)
        {
            DateTime periodStart = GetPeriodStart(periodType, periodOffset, dateTime, false);
            TimeSpan offset = dateTime - periodStart;
            uint interval = (uint)(((uint)offset.TotalSeconds) / intervalSeconds);
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

        protected TimeSpan SmallGapLimit = TimeSpan.FromSeconds(120.0);
        protected DeviceDataRecorders.DeviceParamsBase DeviceParams;
        public DateTime LastFindTime { get; set; }
        protected TimeSpan PeriodOverlapLimit = TimeSpan.FromHours(4.0);

        public int? DeviceId { get; private set; }

        protected SortedList<DateTime, ReadingBase> ReadingsGeneric;

        public DeviceDetailPeriodBase(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature, DeviceDataRecorders.DeviceParamsBase deviceParams)
            : base(periodType, periodStart, deviceParams.RecordingInterval)
        {
            DeviceDetailPeriods = deviceDetailPeriods;
            DeviceId = DeviceDetailPeriods.Device.DeviceId;
            DeviceParams = deviceParams;
            FeatureType = feature.Type;
            FeatureId = feature.Id;
            DeviceIntervalSeconds = DeviceParams.QueryInterval;
            UpdatePending = false;
            LastFindTime = DateTime.Now;

            ReadingsGeneric = new SortedList<DateTime, ReadingBase>(); 
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

        public int GetIntervalNo(DateTime intervalTime, bool isEndTime = true)
        {
            if (isEndTime)
            {
                if (Start == intervalTime || Start != intervalTime.Date && intervalTime != Start.AddDays(1.0))
                    throw new Exception("DeviceDetailDay.GetIntervalNo - Wrong Day");
            }
            else if (Start != intervalTime.Date)
                throw new Exception("DeviceDetailDay.GetIntervalNo - Wrong Day");

            return ReadingBase.GetIntervalNo(intervalTime.TimeOfDay, DatabaseIntervalSeconds, isEndTime);
        }

        protected abstract void Normalise(GenConnection con, int activeInterval);

        public virtual void UpdateReadings()
        {
            // normal devices (not consolidations) are always up to date
            // consolidations override this to ensure current day readings are up to date
        }

        public void UpdateDatabase(GenConnection con, DateTime? activeReadingTime)
        {
            int activeInterval = -1;
            if (activeReadingTime.HasValue)  // this is used on the active day (today) to limit Normalise to the completed intervals only
                activeInterval = GetIntervalNo(activeReadingTime.Value);

            Normalise(con, activeInterval);

            foreach (ReadingBase reading in ReadingsGeneric.Values)
            {
                if (reading.UpdatePending)
                {
                    if (activeReadingTime.HasValue
                    && activeReadingTime.Value.Date == Start
                    && activeInterval == GetIntervalNo(reading.ReadingEnd))
                        continue;
                    reading.PersistReading(con, DeviceId.Value);
                }
            }
            UpdatePending = false;
        }

        public void AddReading(ReadingBase reading, AddReadingType addReadingType = AddReadingType.NewReading, ConsolidateDeviceSettings.OperationType operation = ConsolidateDeviceSettings.OperationType.Add)
        {
            if (reading.ReadingEnd <= Start)
                return;
            if (reading.ReadingStart >= End)
                return;

            if (ReadingsGeneric.ContainsKey(reading.ReadingEnd) && addReadingType != AddReadingType.History)
                throw new Exception("AddReading - Duplicate reading found - ReadingEnd: " + reading.ReadingEnd);

            if (reading.Duration.Ticks == 0 )
                throw new Exception("AddReading - Zero duration found - ReadingEnd: " + reading.ReadingEnd);

            GenConnection con = null;
            try
            {
                if (reading.ReadingStart < Start)
                {
                    if ((Start - reading.ReadingStart) > PeriodOverlapLimit)
                        throw new Exception("AddReading - Period overlap exceeds limit - ReadingStart: " + reading.ReadingStart + " - Period Start: " + Start);
                    if (DeviceParams.EnforceRecordingInterval)
                    {
                        ReadingBase discardReading;
                        SplitReadingGeneric(reading, Start, out discardReading, out reading);
                        if (con == null)
                        {
                            con = GlobalSettings.TheDB.NewConnection();
                            GlobalSettings.SystemServices.GetDatabaseMutex();
                        }
                        discardReading.PersistReading(con, DeviceId.Value);
                    }
                }
                if (reading.ReadingEnd > End)
                {
                    if ((reading.ReadingEnd - End) > PeriodOverlapLimit)
                        throw new Exception("AddReading - Period overlap exceeds limit - ReadingEnd: " + reading.ReadingStart + " - Period End: " + Start);
                    if (DeviceParams.EnforceRecordingInterval)
                    {
                        ReadingBase discardReading;
                        SplitReadingGeneric(reading, End, out reading, out discardReading);
                        if (con == null)
                        {
                            con = GlobalSettings.TheDB.NewConnection();
                            GlobalSettings.SystemServices.GetDatabaseMutex();
                        }
                        discardReading.PersistReading(con, DeviceId.Value);
                    }
                }
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                    con = null;
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                }
            }

            // OutputTime of midnight is last reading of the old day not first reading of new day
            if (reading.ReadingEnd.Date != Start.Date && reading.ReadingEnd != Start.Date.AddDays(1.0))
                throw new Exception("DeviceDetailDay.AddReading - All readings must be on the same day - Expected: " + Start + " - Found: " + reading.ReadingEnd.Date);

            try
            {
                ReadingsGeneric.Add(reading.ReadingEnd, reading);
            }
            catch (ArgumentException eOrig)
            {
                // duplicates should not occur from live sources
                if (addReadingType == AddReadingType.NewReading)
                    throw eOrig;
                // if past date replace existing - must be a history reload request
                // if current day replace existing if is most recent
                if (reading.ReadingStart.Date < DateTime.Today
                || reading.ReadingStart.Date == DateTime.Today && ReadingsGeneric.Values[ReadingsGeneric.Count - 1].ReadingEnd == reading.ReadingEnd)
                    try
                    {
                        ReadingBase old = ReadingsGeneric.Values[ReadingsGeneric.IndexOfKey(reading.ReadingEnd)];
                        if (!reading.IsSameReadingValuesGeneric(old))
                        {
                            ReadingsGeneric.Remove(reading.ReadingEnd);
                            ReadingsGeneric.Add(reading.ReadingEnd, reading);
                            reading.InDatabase = old.InDatabase;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception("Exception after removal of detected existing reading - ReadingEnd: " + reading.ReadingEnd + " - Exception: ", e);
                    }
            }
            reading.RegisterPeriodInvolvement(this);
            if (reading.UpdatePending)
                PeriodIsDirty();
        }

        public virtual void SplitReadingGeneric(ReadingBase oldReading, DateTime splitTime, out ReadingBase newReading1, out ReadingBase newReading2)
        {
            // Ensure Delta calculations are up to date prior to Clone
            TimeSpan newDuration2 = oldReading.ReadingEnd - splitTime;
            if (DeviceParams.UseCalculateFromPrevious)
                CalcFromPrevious(oldReading);
            newReading1 = oldReading.CloneGeneric(splitTime, oldReading.Duration - newDuration2);
            newReading2 = oldReading.CloneGeneric(oldReading.ReadingEnd, newDuration2);
        }

        protected void CalcFromPrevious(ReadingBase stopHere)
        {
            // perform reading adjustments based upon a delta with the previous reading
            ReadingBase previous = null;
            foreach (ReadingBase r in ReadingsGeneric.Values)
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
        public DeviceDetailPeriod(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature, DeviceDataRecorders.DeviceParamsBase deviceParams) 
            : base(deviceDetailPeriods, periodType, periodStart, feature, deviceParams)
        {         
        }

        // returns the types readings - they are sorted at time of return
        public List<TDeviceReading> GetReadings()
        {
            List<TDeviceReading> readings = new List<TDeviceReading>();
            foreach (ReadingBase r in ReadingsGeneric.Values)
            {
                readings.Add((TDeviceReading)r);
            }
            return readings;
        }

        public abstract void SplitReading(TDeviceReading oldReading, DateTime splitTime, out TDeviceReading newReading1, out TDeviceReading newReading2);

        public void RemoveReadingAt(int index)
        {
            ReadingsGeneric.Values[index].DeregisterPeriodInvolvement(this);
            ReadingsGeneric.RemoveAt(index);
        }

        public void ClearReadings()
        {
            foreach (TDeviceReading reading in ReadingsGeneric.Values)
                reading.RegisterPeriodInvolvement(this);
            ReadingsGeneric.Clear();
        }

        protected virtual void AdjustHistory(TDeviceReading prevReading, TDeviceReading reading, TDeviceReading nextReading, TDeviceHistory histRecord)
        {
        }

        protected abstract TDeviceReading NewReading(DateTime outputTime, TimeSpan duration, TDeviceReading pattern = null);

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

        private void AlignIntervals()
        {
            int i = 0;  // position in Readings
            
            TDeviceReading reading;
            DateTime? lastTime = null;

            while (i < ReadingsGeneric.Count)
            {
                reading = (TDeviceReading)ReadingsGeneric.Values[i];
                // detect out of sequence reading and discard
                if (lastTime.HasValue && reading.ReadingEnd <= lastTime.Value)
                {
                    RemoveReadingAt(i);
                    continue;
                }

                //DateTime startTime = reading.ReadingStart;

                // detect and discard overlapped values; split at overlap and discard the overlap
                if (lastTime.HasValue && reading.ReadingStart < lastTime.Value)
                {
                    TDeviceReading newReading1;
                    TDeviceReading newReading2;
                    // newReading1 is the overlapped value
                    SplitReading(reading, lastTime.Value, out newReading1, out newReading2);
                    RemoveReadingAt(i);
                    AddReading(newReading2);
                    reading = newReading2;
                }
                lastTime = reading.ReadingEnd;

                int endInterval = GetIntervalNo(reading.ReadingEnd);  // end time interval of current reading
                //startTime = reading.ReadingEnd -reading.Duration;  // start time of current reading

                // Ensure no readings cross an interval boundary

                // get the first interval in the current reading
                int startInterval = GetIntervalNo(reading.ReadingStart, false); // start time interval of current reading

                while (startInterval < endInterval)  // true if interval boundary is crossed
                {
                    TDeviceReading newReading1;
                    TDeviceReading newReading2;
                    // split the reading at end of first interval in reading
                    DateTime intervalEndTime = GetDateTime(startInterval);
                    SplitReading(reading, intervalEndTime, out newReading1, out newReading2);
                    
                    // remove old and replace with two new readings
                    RemoveReadingAt(i);
                    AddReading(newReading1);
                    AddReading(newReading2);
                    i++;

                    // setup for next cycle
                    reading = newReading2;                   
                    startInterval = GetIntervalNo(reading.ReadingStart, false);
                }

                i++;
            }            
        }

        private void ReplaceWithMerged(GenConnection con, int interval, int start, int count)
        {
            DateTime intervalEnd = GetDateTime(interval);
            TDeviceReading newReading = MergeReadings(intervalEnd, start, count);

            for (int j = 0; j < count; j++)
            {
                TDeviceReading reading = (TDeviceReading)ReadingsGeneric.Values[start];
                if (reading.InDatabase)
                {
                    GlobalSettings.LogMessage("DeviceDetailPeriod.ReplaceWithMerged", 
                        "Unexpected reading deletion - OutputTime: " + reading.ReadingEnd , LogEntryType.Information);
                    ReadingsGeneric.Values[start].DeleteReading(con, DeviceId.Value);
                }
                RemoveReadingAt(start);
            }
            AddReading(newReading);
        }

        private TDeviceReading MergeIntervals(DateTime consolidatedEndTime, int startInterval, int endInterval)
        {
            DateTime startOutputTime = GetDateTime(startInterval);
            DateTime endOutputTime = GetDateTime(endInterval);

            int? startIndex = null;
            int? endIndex = null;

            for(int i = 0; i < ReadingsGeneric.Keys.Count; i++)
            {
                DateTime key = ReadingsGeneric.Keys[i];
                if (!startIndex.HasValue)
                {
                    if (key >= startOutputTime && key <= endOutputTime)
                    {
                        startIndex = i;
                        break;
                    }
                }
            }
            for (int i = ReadingsGeneric.Keys.Count - 1; i >= 0; i--)
            {
                DateTime key = ReadingsGeneric.Keys[i];
                if (!endIndex.HasValue)
                {
                    if (key <= endOutputTime && key >= startOutputTime)
                    {
                        startIndex = i;
                        break;
                    }
                }
            }

            if (startIndex.HasValue && endIndex.HasValue)
                return MergeReadings(consolidatedEndTime, startIndex.Value, (endIndex.Value - startIndex.Value) + 1);
            else
                return NewReading(consolidatedEndTime, TimeSpan.FromSeconds((1 + endInterval - startInterval) * DatabaseIntervalSeconds), default(TDeviceReading));
        }

        private TDeviceReading MergeReadings(DateTime consolidatedEndTime, int startReading, int readingCount)
        {
            int endInterval = startReading + readingCount - 1;
            TDeviceReading newReading = NewReading(consolidatedEndTime, TimeSpan.Zero, default(TDeviceReading));
            for (int i = startReading; i <= endInterval; i++)
            {
                TDeviceReading reading = (TDeviceReading)ReadingsGeneric.Values[i];               

                if ((consolidatedEndTime - newReading.ReadingEnd).TotalSeconds > DatabaseIntervalSeconds)
                    throw new Exception("DeviceDetailPeriod.MergeReadings - reading: " + newReading.ReadingEnd + " - too old for: " + consolidatedEndTime);

                newReading.AccumulateReading(reading);

                // If the Output time on an existing reading aligns with an interval end, this entry may already be in the DB
                // mark new entry with existing status as they share the same DB key
                if (reading.ReadingEnd == consolidatedEndTime)
                    newReading.InDatabase = reading.InDatabase;
            }
            
            if (newReading.GetModeratedSeconds(3) > DatabaseIntervalSeconds)
                throw new Exception("DeviceDetailPeriod.MergeReadings - Duration too large: " + newReading.Duration);
            return newReading;
        }

        protected override void Normalise(GenConnection con, int activeInterval)
        {
            // correct overlaps; ensure readings do not span database intervals
            AlignIntervals();

            // detect sequences of readings in the same interval and consolidate
            int? interval = null;

            int i = 0;  // position in Readings
            int start = i;  // first in interval
            int count = 0;  // count in interval
            TDeviceReading reading;
            DateTime endTime = DateTime.MinValue;

            while (i < ReadingsGeneric.Count)
            {
                reading = (TDeviceReading)ReadingsGeneric.Values[i];
                int thisInterval = GetIntervalNo(reading.ReadingEnd);

                if (thisInterval >= activeInterval)  // Do not normalise readings in intervals that are still subject to change
                    break;

                if (!interval.HasValue)
                {
                    interval = thisInterval;
                    count++;  // first interval
                }
                else
                {
                    if (thisInterval != interval.Value)
                    {
                        if (count > 1 || endTime != GetDateTime(interval.Value))
                            ReplaceWithMerged(con, interval.Value, start, count);         
                        start += 1;
                        i = start;
                        interval = thisInterval; ;
                        count = 1;
                    }
                    else
                        count++;
                }
                i++;
                endTime = reading.ReadingEnd;
            }
            if (count > 1 || count > 0 && endTime != GetDateTime(interval.Value))
                ReplaceWithMerged(con, interval.Value, start, count);

            if (DeviceParams.UseCalculateFromPrevious)
                CalcFromPrevious(default(TDeviceReading));

            // fill any gaps up to 30 secs with prorata adjacent values - creates actuals not calculated values
            if (interval.HasValue)
                FillSmallGaps(0, interval.Value, false); // do not fill past last normalised reading
        }

        private TimeSpan FillSmallGaps(int startInterval, int endInterval, bool fillEndGap)
        {
            int i = 0;  // position in Readings
            TDeviceReading reading = default(TDeviceReading);
            TDeviceReading prevReading = default(TDeviceReading);
            TDeviceReading nextReading = default(TDeviceReading);
            DateTime prevEndTime = GetDateTime(startInterval-1); // decrement interval to return interval start time
            TimeSpan gap = TimeSpan.Zero;
            TimeSpan remainingGaps = TimeSpan.Zero;
            
            bool readingsAdded = false;

            while (i < ReadingsGeneric.Count)
            {
                reading = (TDeviceReading)ReadingsGeneric.Values[i];
                if ((i+1) < ReadingsGeneric.Count)
                    nextReading = (TDeviceReading)ReadingsGeneric.Values[i + 1];
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

                //DateTime thisStartTime = reading.ReadingEnd - reading.Duration;
                gap = reading.ReadingStart - prevEndTime;

                // Fill this gap with reading based on adjacent readings
                if (gap > TimeSpan.Zero && gap <= SmallGapLimit)
                {
                    TDeviceReading newRec;
                    if (nextReading != null)
                    {
                        newRec = nextReading.FillSmallGap(reading.ReadingStart, gap, true);                        
                        AddReading(newRec);
                        reading = newRec;
                        i++;
                        readingsAdded = true;
                    }
                    else if (prevReading != null)
                    {
                        newRec = prevReading.FillSmallGap(reading.ReadingStart, gap, false);
                        AddReading(newRec);
                        reading = newRec;
                        i++;
                        readingsAdded = true;
                    }
                    else
                        remainingGaps += gap;
                }
                else
                    remainingGaps += gap;

                prevEndTime = reading.ReadingEnd;
                prevReading = reading;
                i++;
            }

            // fill end gap if present and small enough
            if (fillEndGap)
            {
                DateTime endTime = GetDateTime(endInterval);
                if (prevReading == null)
                    // no readings - gap is the full history interval
                    gap = TimeSpan.FromSeconds((1 + endInterval - startInterval) * DeviceParams.RecordingInterval);
                else
                    gap = endTime - prevReading.ReadingEnd;

                if (gap > TimeSpan.Zero && gap <= SmallGapLimit)
                {
                    TDeviceReading newRec = prevReading.FillSmallGap(endTime, gap, false);
                    AddReading(newRec);
                    readingsAdded = true;
                }
                else
                    remainingGaps += gap;
            }

            // Check interval alignment as gap fill can cross a boundary
            if (readingsAdded)
                AlignIntervals();
            return remainingGaps;
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
                reading = (TDeviceReading)ReadingsGeneric.Values[i];
                if ((i + 1) < ReadingsGeneric.Count)
                    nextReading = (TDeviceReading)ReadingsGeneric.Values[i + 1];
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

                histGap -= reading.Duration;

                //DateTime thisStartTime = reading.ReadingEnd - reading.Duration;
                TimeSpan gap = reading.ReadingStart - prevEndTime;

                if (gap > TimeSpan.Zero)
                {
                    TDeviceReading newRec = NewReading(reading.ReadingStart, gap, (nextReading != null) ? nextReading : ((prevReading != null) ? prevReading : default(TDeviceReading)));
                    newRec.HistoryAdjust_Average(actualTotal, histRecord);
                    AddReading(newRec);
                    reading = newRec;
                    histGap -= gap;
                    i++;                    
                }

                prevEndTime = reading.ReadingEnd;
                prevReading = reading;
                i++;
            }

            DateTime endTime = GetDateTime(endInterval);
            if ((prevEndTime + histGap) != endTime)
                throw new Exception("DeviceDetailPeriod.FillLargeGaps - end gap mismatch - endTime: " + endTime + " - prevEndTime: " + prevEndTime + " - histGap: " + histGap);

            if (histGap > TimeSpan.Zero)
            {                
                TDeviceReading newRec = NewReading(endTime, histGap, prevReading == null ? default(TDeviceReading) : prevReading);
                newRec.HistoryAdjust_Average(actualTotal, histRecord);
                AddReading(newRec);
            }
        }

        private void ProrataRemainingHistory(TDeviceReading actualTotal, TDeviceHistory histRecord, DateTime startTime, int startInterval, int endInterval)
        {
            int i = 0;  // position in Readings
            TDeviceReading reading = default(TDeviceReading);

            while (i < ReadingsGeneric.Count)
            {
                reading = (TDeviceReading)ReadingsGeneric.Values[i];

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

                reading.HistoryAdjust_Prorata(actualTotal, histRecord); ;
                
                i++;
            }
        }

        public void AdjustFromHistory(TDeviceHistory histRecord, float historySeconds)
        {
            DateTime startTime = histRecord.ReadingEnd.AddSeconds(-historySeconds);
            int endInterval = GetIntervalNo(histRecord.ReadingEnd);
            int startInterval = GetIntervalNo(startTime, false);

            if (histRecord.ReadingEnd <= Start || (histRecord.ReadingEnd - Start).TotalHours > 24.0)
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - End time is wrong day - endTime: " + histRecord.ReadingEnd + " - Day: " + Start);

            if (GetDateTime(endInterval) != histRecord.ReadingEnd)
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - End time does not align with interval boundary - endTime: " + histRecord.ReadingEnd + " - interval: " + DatabaseIntervalSeconds);

            if (startTime < Start)
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - startTime is wrong day - startTime: " + startTime + " - Day: " + Start);

            if (GetDateTime(startInterval) 
                != startTime + TimeSpan.FromSeconds(DatabaseIntervalSeconds))
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - startTime does not align with interval boundary - endTime: " + histRecord.ReadingEnd + " - interval: " + DatabaseIntervalSeconds);

            // clear old calculated
            ClearHistory(startInterval, endInterval);
            if (DeviceParams.UseCalculateFromPrevious)
                CalcFromPrevious(default(TDeviceReading));
            // fill any gaps up to 30 secs with prorata adjacent values - creates actuals not calculated values
            TimeSpan remainingGaps = FillSmallGaps(startInterval, endInterval, true);
            // obtain actual total - uses Consolidate
            TDeviceReading actualTotal = MergeIntervals(GetDateTime(endInterval), startInterval, endInterval);
            // fill all remaining gaps with prorata history value
            if (remainingGaps > TimeSpan.Zero)
            {
                FillLargeGaps(actualTotal, histRecord, remainingGaps, startTime, startInterval, endInterval);
                // recalculate actualTotal to capture large gap additions
                actualTotal = MergeIntervals(GetDateTime(endInterval), startInterval, endInterval);
            }
            // apportion outstanding history values by prorata adjustment 
            if (actualTotal.Compare(histRecord) != 0)
                ProrataRemainingHistory(actualTotal, histRecord, startTime, startInterval, endInterval);
        }

        private void ClearHistory(int startInterval, int endInterval)
        {
            DateTime startOutputTime = GetDateTime(startInterval);
            DateTime endOutputTime = GetDateTime(endInterval);

            foreach (ReadingBase reading in ReadingsGeneric.Values)
            {
                if (reading.ReadingEnd >= startOutputTime) 
                    if (reading.ReadingEnd <= endOutputTime)
                        reading.ClearHistory();
                    else
                        break;
            }
        }

        protected void BindSelectIdentity(GenCommand cmd)
        {
            cmd.AddParameterWithValue("@Device_Id", DeviceId.Value);
            cmd.AddParameterWithValue("@FeatureType", (int)FeatureType);
            cmd.AddParameterWithValue("@FeatureId", (int)FeatureId);
            cmd.AddParameterWithValue("@PeriodStart", Start - PeriodOverlapLimit);
            cmd.AddParameterWithValue("@NextPeriodStart", Start.AddDays(1.0) + PeriodOverlapLimit);
        }
    }

    public abstract class DeviceDetailPeriod_Physical<TDeviceReading, TDeviceHistory> : DeviceDetailPeriod<TDeviceReading, TDeviceHistory>
        where TDeviceReading : ReadingBaseTyped<TDeviceReading, TDeviceHistory>, IComparable<TDeviceReading>
        where TDeviceHistory : ReadingBase
    {
       

        public DeviceDetailPeriod_Physical(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature, DeviceDataRecorders.DeviceParamsBase deviceParams)
            : base(deviceDetailPeriods, periodType, periodStart, feature, deviceParams)
        {
            
        }

        public abstract void LoadPeriodFromDatabase(GenConnection existingCon = null);
    }

    public abstract class DeviceDetailPeriod_Consolidation<TDeviceReading, TDeviceHistory> : DeviceDetailPeriod<TDeviceReading, TDeviceHistory>
        where TDeviceReading : ReadingBaseTyped<TDeviceReading, TDeviceHistory>, IComparable<TDeviceReading>
        where TDeviceHistory : ReadingBase
    {
        public DeviceDetailPeriod_Consolidation(DeviceDetailPeriodsBase deviceDetailPeriods, PeriodType periodType, DateTime periodStart, FeatureSettings feature, DeviceDataRecorders.DeviceParamsBase deviceParams)
            : base(deviceDetailPeriods, periodType, periodStart, feature, deviceParams)
        {

        }

        public void LoadPeriodFromConsolidations()
        {
            try
            {
                ClearReadings();
                // for each device feature that consolidates to this device (owner of this period)
                foreach (Device.DeviceLink devLink in ((Device.ConsolidationDevice)DeviceDetailPeriods.Device).SourceDevices)
                {
                    // to link must match this consolidation - consolidate the matching from link
                    if (FeatureType == devLink.ToFeatureType && FeatureId == devLink.ToFeatureId)
                    {
                        if (!devLink.FromDevice.DeviceId.HasValue)
                            devLink.FromDevice.GetDeviceId(null);
                        DeviceDetailPeriodsBase periods = devLink.FromDevice.FindOrCreateFeaturePeriods(devLink.FromFeatureType, devLink.FromFeatureId);
                        // step through all periods in the period container
                        PeriodEnumerator pEnum = periods.GetPeriodEnumerator(Start, End);
                        foreach (PeriodBase p in pEnum)
                        {
                            // locate a period with a specific start date
                            // Note - if it does not exist and empty one will be created
                            DeviceDetailPeriod<TDeviceReading, TDeviceHistory> period = (DeviceDetailPeriod<TDeviceReading, TDeviceHistory>)periods.FindOrCreate(p.Start);
                            // step through the readings in one period and consolidate into this period
                            List<TDeviceReading> readings = period.GetReadings();
                            foreach (TDeviceReading r in readings)
                                ConsolidateReading(r, devLink.Operation);
                        }
                        devLink.SourceUpdated = false;
                    }
                }
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DeviceDetailPeriod_Consolidation.LoadPeriodFromConsolidations", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }

        public void ConsolidateReading(TDeviceReading reading, ConsolidateDeviceSettings.OperationType operation = ConsolidateDeviceSettings.OperationType.Add)
        {
            // discard readings that are not relevant to this consolidation period
            if (reading.ReadingEnd <= Start)
                return;
            if (reading.ReadingStart >= End)
                return;
           
            // trim readings that span the start of period boundary
            if (reading.ReadingStart < Start)
            {
                if ((Start - reading.ReadingStart) > PeriodOverlapLimit)
                    throw new Exception("AddReading - Period overlap exceeds limit - ReadingStart: " + reading.ReadingStart + " - Period Start: " + Start);
                if (DeviceParams.EnforceRecordingInterval)
                {
                    TDeviceReading discardReading;
                    SplitReading(reading, Start, out discardReading, out reading);
                }
            }
            // trim readings that span the end of period boundary
            if (reading.ReadingEnd > End)
            {
                if ((reading.ReadingEnd - End) > PeriodOverlapLimit)
                    throw new Exception("AddReading - Period overlap exceeds limit - ReadingEnd: " + reading.ReadingStart + " - Period End: " + Start);
                if (DeviceParams.EnforceRecordingInterval)
                {
                    TDeviceReading discardReading;
                    SplitReading(reading, End, out reading, out discardReading);
                }
            }

            uint interval;
            bool isIntervalStart;
            DateTime start;
            GetIntervalInfo(reading.ReadingStart, out start, out interval, out isIntervalStart);
            if (start != Start)
                throw new Exception("DeviceDetailPeriod_Consolidation.ConsolidateReading - consolidation mismatch - Calc start: " + start + " - Required start: " + Start);
            DateTime intervalEnd = start + TimeSpan.FromSeconds((interval + 1) * DatabaseIntervalSeconds);

            int index = ReadingsGeneric.IndexOfKey(intervalEnd);
            TDeviceReading toReading;
            if (index < 0)
            {
                toReading = NewReading(intervalEnd, TimeSpan.FromSeconds(DatabaseIntervalSeconds), null);
                ReadingsGeneric.Add(intervalEnd, toReading);
                toReading.RegisterPeriodInvolvement(this);
            }
            else
                toReading = (TDeviceReading)ReadingsGeneric.Values[index];

            toReading.AccumulateReading(reading, operation == ConsolidateDeviceSettings.OperationType.Subtract ? -1.0 : 1.0);                
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
