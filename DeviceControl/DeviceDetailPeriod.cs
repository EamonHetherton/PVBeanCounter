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

        //protected abstract void Normalise(GenConnection con, int activeInterval);

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

        public void UpdateDatabase(GenConnection con, DateTime? activeReadingTime, bool purgeUnmatched, bool consolidate)
        {
            //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Entry", LogEntryType.Trace);
            int activeInterval = -1;
            if (activeReadingTime.HasValue)  // this is used on the active day (today) to limit Normalise to the completed intervals only
                activeInterval = GetIntervalNo(activeReadingTime.Value);
            //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Before Normalise", LogEntryType.Trace);
            ReadingsGeneric.FillSmallGaps(Start, End, false);
            //GlobalSettings.LogMessage("DeviceDetailPeriodBase", "UpdateDatabase - Before Loop", LogEntryType.Trace);
            if (consolidate)
                ReadingsGeneric.ConsolidateIntervals();
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

        public void ConsolidateReadings()
        {
            ReadingsGeneric.ConsolidateIntervals();
        }
        
        /*
        public void AddReadingOld(ReadingBase reading, AddReadingType addReadingType = AddReadingType.NewReading, ConsolidateDeviceSettings.OperationType operation = ConsolidateDeviceSettings.OperationType.Add)
        {
            String stage = "Initial";
            if (reading.ReadingEnd <= Start)
                return;
            if (reading.ReadingStart >= End)
                return;

            stage = "duplicate check";
            if (addReadingType != AddReadingType.History && ReadingsGeneric.ContainsKey(reading.ReadingEnd))
                throw new Exception("AddReading - Duplicate reading found - ReadingEnd: " + reading.ReadingEnd);

            stage = "duration check";
            if (reading.Duration.Ticks == 0 )
                throw new Exception("AddReading - Zero duration found - ReadingEnd: " + reading.ReadingEnd);

            GenConnection con = null;
            try
            {
                stage = "ReadingStart";
                if (reading.ReadingStart < Start)
                {
                    if ((Start - reading.ReadingStart) > PeriodOverlapLimit)
                        throw new Exception("AddReading - Period overlap exceeds limit - ReadingStart: " + reading.ReadingStart + " - Period Start: " + Start);

                    if (DeviceParams == null)
                        GlobalSettings.LogMessage("DeviceDetailPeriod.AddReading", "*******DeviceParams is null 1", LogEntryType.ErrorMessage);

                    if (DeviceParams.EnforceRecordingInterval)
                    {
                        ReadingBase discardReading;
                        stage += " - Split";
                        SplitReadingGeneric(reading, Start, out discardReading, out reading);
                        if (con == null)
                        {
                            con = GlobalSettings.TheDB.NewConnection();
                            GlobalSettings.SystemServices.GetDatabaseMutex();
                        }

                        if (discardReading == null)
                            GlobalSettings.LogMessage("DeviceDetailPeriod.AddReading", "*******discardReading is null 1", LogEntryType.ErrorMessage);

                        if (DeviceId == null)
                            GlobalSettings.LogMessage("DeviceDetailPeriod.AddReading", "*******DeviceId is null 1", LogEntryType.ErrorMessage);

                        discardReading.PersistReading(con, DeviceId.Value);
                    }
                }
                stage = "ReadingEnd";
                if (reading.ReadingEnd > End)
                {
                    if ((reading.ReadingEnd - End) > PeriodOverlapLimit)
                        throw new Exception("AddReading - Period overlap exceeds limit - ReadingEnd: " + reading.ReadingStart + " - Period End: " + Start);

                    if (DeviceParams == null)
                        GlobalSettings.LogMessage("DeviceDetailPeriod.AddReading", "*******DeviceParams is null 2", LogEntryType.ErrorMessage);

                    if (DeviceParams.EnforceRecordingInterval)
                    {
                        ReadingBase discardReading;
                        stage += " - Split";
                        SplitReadingGeneric(reading, End, out reading, out discardReading);
                        if (con == null)
                        {
                            con = GlobalSettings.TheDB.NewConnection();
                            GlobalSettings.SystemServices.GetDatabaseMutex();
                        }

                        if (discardReading == null)
                            GlobalSettings.LogMessage("DeviceDetailPeriod.AddReading", "*******discardReading is null 2", LogEntryType.ErrorMessage);

                        if (DeviceId == null)
                            GlobalSettings.LogMessage("DeviceDetailPeriod.AddReading", "*******DeviceId is null 2", LogEntryType.ErrorMessage);

                        discardReading.PersistReading(con, DeviceId.Value);
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("AddReading Stage: " + stage + " - Exception: " + e.Message, e);
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
                if (addReadingType != AddReadingType.Database)
                    reading.AddReadingMatch = true; // new reading - must set to true - this is a keeper
            }
            catch (ArgumentException eOrig)
            {
                ReadingBase old = ReadingsGeneric.Values[ReadingsGeneric.IndexOfKey(reading.ReadingEnd)];
                old.AddReadingMatch = true;
                // duplicates should not occur from live sources
                if (addReadingType == AddReadingType.NewReading || addReadingType == AddReadingType.Database)
                    throw eOrig;
                // if past date replace existing - must be a history reload request
                // if current day replace existing if is most recent
                if (reading.ReadingStart.Date < DateTime.Today
                || reading.ReadingStart.Date == DateTime.Today && ReadingsGeneric.Values[ReadingsGeneric.Count - 1].ReadingEnd == reading.ReadingEnd)
                    try
                    {                        
                        if (!reading.IsSameReadingValuesGeneric(old))
                        {
                            ReadingsGeneric.Remove(reading.ReadingEnd);
                            ReadingsGeneric.Add(reading.ReadingEnd, reading);
                            reading.InDatabase = old.InDatabase;
                            reading.AddReadingMatch = true; // matched an existing reading - this is a keeper
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
        */

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
        public IList<TDeviceReading> GetReadings()
        {
            /*
            List<TDeviceReading> readings = new List<TDeviceReading>();
            foreach (ReadingBase r in ReadingsGeneric.ReadingList)
            {
                readings.Add((TDeviceReading)r);
            }
            */
            return (IList<TDeviceReading>)ReadingsGeneric.ReadingList;
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
            SplitReadingSub((TDeviceReading)oldReading, splitTime, (TDeviceReading)reading1, (TDeviceReading)reading2);
            newReading1 = reading1;
            newReading2 = reading2;
        }

        protected abstract void SplitReadingSub(TDeviceReading oldReading, DateTime splitTime, TDeviceReading newReading1, TDeviceReading newReading2);

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

        private TDeviceReading MergeReadings(DateTime consolidatedEndTime, DateTime startTime, DateTime endTime)
        {
            
            TDeviceReading newReading = NewReading(consolidatedEndTime, endTime - startTime, default(TDeviceReading));
            foreach(TDeviceReading reading in ReadingsGeneric.ReadingList)
            {
                TDeviceReading thisReading = reading;
                if (thisReading.ReadingEnd <= startTime)
                    continue;
                if (thisReading.ReadingStart >= endTime)
                    break;

                if ((consolidatedEndTime - newReading.ReadingEnd).TotalSeconds > DatabaseIntervalSeconds)
                    throw new Exception("DeviceDetailPeriod.MergeReadings - reading: " + newReading.ReadingEnd + " - too old for: " + consolidatedEndTime);

                // Trim overhangs
                if (thisReading.ReadingStart < startTime)
                    thisReading = reading.Clone(thisReading.ReadingEnd, thisReading.ReadingEnd - startTime);
                if (thisReading.ReadingEnd > endTime)
                    thisReading = reading.Clone(endTime, endTime - thisReading.ReadingStart);

                newReading.AccumulateReading(thisReading);

                // If the Output time on an existing reading aligns with an interval end, this entry may already be in the DB
                // mark new entry with existing status as they share the same DB key
                if (reading.ReadingEnd == consolidatedEndTime)
                    newReading.InDatabase = reading.InDatabase;
            }
            
            if (newReading.GetModeratedSeconds(3) > DatabaseIntervalSeconds)
                throw new Exception("DeviceDetailPeriod.MergeReadings - Duration too large: " + newReading.Duration);
            return newReading;
        }

        /*
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
        */

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

                reading.HistoryAdjust_Prorata(actualTotal, histRecord); ;
                
                i++;
            }
        }

        public void AdjustFromHistory(TDeviceHistory histRecord)
        {
            DateTime startTime = histRecord.ReadingStart;
            int endInterval = GetIntervalNo(histRecord.ReadingEnd);
            int startInterval = GetIntervalNo(histRecord.ReadingStart, false);

            if (histRecord.ReadingEnd <= Start || (histRecord.ReadingEnd - Start).TotalHours > 24.0)
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - End time is wrong day - endTime: " + histRecord.ReadingEnd + " - Day: " + Start);

            if (GetDateTime(endInterval) != histRecord.ReadingEnd)
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - End time does not align with interval boundary - endTime: " + histRecord.ReadingEnd + " - interval: " + DatabaseIntervalSeconds);

            if (startTime < Start)
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - startTime is wrong day - startTime: " + startTime + " - Day: " + Start);

            if (GetDateTime(startInterval) 
                != startTime + TimeSpan.FromSeconds(DatabaseIntervalSeconds))
                throw new Exception("DeviceDetailPeriod.AdjustFromHistory - startTime does not align with interval boundary - endTime: " + histRecord.ReadingEnd + " - interval: " + DatabaseIntervalSeconds);

            // clear old history entries
            ClearHistory(histRecord.ReadingStart, histRecord.ReadingEnd);
            if (DeviceParams.UseCalculateFromPrevious)
                CalcFromPrevious(default(TDeviceReading));
            // fill any gaps up to 30 secs with prorata adjacent values - creates actuals not calculated values
            TimeSpan remainingGaps = ReadingsGeneric.FillSmallGaps(histRecord.ReadingStart, histRecord.ReadingEnd, true); 
            // obtain actual total - uses Consolidate
            TDeviceReading actualTotal = MergeReadings(GetDateTime(endInterval), histRecord.ReadingStart, histRecord.ReadingEnd);
            // fill all remaining gaps with prorata history value
            if (remainingGaps > TimeSpan.Zero)
            {
                FillLargeGaps(actualTotal, histRecord, remainingGaps, startTime, startInterval, endInterval);
                // recalculate actualTotal to capture large gap additions
                actualTotal = actualTotal = MergeReadings(GetDateTime(endInterval), histRecord.ReadingStart, histRecord.ReadingEnd);
            }
            // apportion outstanding history values by prorata adjustment 
            if (actualTotal.Compare(histRecord) != 0)
                ProrataRemainingHistory(actualTotal, histRecord, startTime, startInterval, endInterval);
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

            cmd.AddParameterWithValue("@DeviceFeature_Id", DeviceDetailPeriods.DeviceFeatureId);
            cmd.AddParameterWithValue("@PeriodStart", Start - PeriodOverlapLimit);
            cmd.AddParameterWithValue("@NextPeriodStart", Start.AddDays(1.0) + PeriodOverlapLimit);
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
                    throw new Exception("ConsolidateReading - Period overlap exceeds limit - ReadingStart: " + reading.ReadingStart + " - Period Start: " + Start);
                if (DeviceParams.EnforceRecordingInterval)
                {
                    ReadingBase discardReading;
                    ReadingBase keepReading;
                    SplitReading((ReadingBase)reading, Start, out discardReading, out keepReading);
                    reading = (TDeviceReading)keepReading;
                }
            }
            // trim readings that span the end of period boundary
            if (reading.ReadingEnd > End)
            {
                if ((reading.ReadingEnd - End) > PeriodOverlapLimit)
                    throw new Exception("ConsolidateReading - Period overlap exceeds limit - ReadingEnd: " + reading.ReadingStart + " - Period End: " + Start);
                if (DeviceParams.EnforceRecordingInterval)
                {
                    ReadingBase discardReading;
                    ReadingBase keepReading;
                    SplitReading(reading, End, out keepReading, out discardReading);
                    reading = (TDeviceReading)keepReading;
                }
            }

            uint interval;
            bool isIntervalStart;
            DateTime start;
            GetIntervalInfo(reading.ReadingStart, out start, out interval, out isIntervalStart);
            if (start != Start)
                throw new Exception("ConsolidateReading - consolidation mismatch - Calc start: " + start + " - Required start: " + Start);
            DateTime intervalEnd = start + TimeSpan.FromSeconds((interval + 1) * DatabaseIntervalSeconds);

            int index = ReadingsGeneric.IndexOfKey(intervalEnd);
            TDeviceReading toReading;
            if (index < 0)
            {
                toReading = NewReading(intervalEnd, TimeSpan.FromSeconds(DatabaseIntervalSeconds), null);
                ReadingsGeneric.AddReading(toReading);
                toReading.RegisterPeriodInvolvement(this);
            }
            else
                toReading = (TDeviceReading)ReadingsGeneric.ReadingList[index];

            toReading.AccumulateReading(reading, operation == ConsolidateDeviceSettings.OperationType.Subtract ? -1.0 : 1.0);
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
                            foreach (TDeviceReading r in period.GetReadings())
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

        /*
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
                    ReadingBase discardReading;
                    ReadingBase keepReading;
                    SplitReading((ReadingBase)reading, Start, out discardReading, out keepReading);
                    reading = (TDeviceReading)keepReading;
                }
            }
            // trim readings that span the end of period boundary
            if (reading.ReadingEnd > End)
            {
                if ((reading.ReadingEnd - End) > PeriodOverlapLimit)
                    throw new Exception("AddReading - Period overlap exceeds limit - ReadingEnd: " + reading.ReadingStart + " - Period End: " + Start);
                if (DeviceParams.EnforceRecordingInterval)
                {
                    ReadingBase discardReading;
                    ReadingBase keepReading;
                    SplitReading(reading, End, out keepReading, out discardReading);
                    reading = (TDeviceReading)keepReading;
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
                ReadingsGeneric.AddReading(toReading);
                toReading.RegisterPeriodInvolvement(this);
            }
            else
                toReading = (TDeviceReading)ReadingsGeneric.ReadingList[index];

            toReading.AccumulateReading(reading, operation == ConsolidateDeviceSettings.OperationType.Subtract ? -1.0 : 1.0);                
        }
        */

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
