/*
* Copyright (c) 2013 Dennis Mackay-Fisher
*
* This file is part of PVService
* 
* PVService is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PVService is distributed in the hope that it will be useful,
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
using System.Threading.Tasks;
using GenericConnector;
using MackayFisher.Utilities;
using PVSettings;

namespace DeviceDataRecorders
{
    // PeriodBase provides all underlying period configuration
    public class ReadingsCollection
    {
        // the readings below are sorted on ReadingEnd
        private SortedList<DateTime, ReadingBase> Readings;

        private DeviceDetailPeriodBase DDP;

        public System.Threading.Mutex RecordsMutex;

        public ReadingsCollection(DeviceDetailPeriodBase deviceDetailPeriod)
        {
            DDP = deviceDetailPeriod;
            ClearReadings();
            RecordsMutex = new System.Threading.Mutex();
        }

        public IList<ReadingBase> ReadingList { get { return Readings.Values; } }
        public int IndexOfKey(DateTime intervalEnd) { return Readings.IndexOfKey(intervalEnd); }
        public int Count { get { return Readings.Count; } }

        public TimeSpan SmallGapUpperLimit = TimeSpan.FromSeconds(120.0);
        public TimeSpan SmallGapLowerLimit = TimeSpan.FromSeconds(1.0);
     
        // the list must pass the following at all times
        // zero duration readings are illegal as well as all reading or reading/period overlaps
        public void CheckReadingsIntegrity()
        {
            ReadingBase prevReading = null;
            for(int i = 0; i < Readings.Count; i++)
            {
                ReadingBase reading = Readings.Values[i];

                if (reading.ReadingStart < DDP.Start)
                    throw new Exception("CheckReadingsIntegrity - ReadingStart belongs in previous period - This ReadingStart: "
                            + reading.ReadingStart + " - This period start: " + DDP.Start);

                if (reading.ReadingEnd > DDP.End)
                    throw new Exception("CheckReadingsIntegrity - ReadingEnd belongs in next period - This ReadingEnd: "
                            + reading.ReadingEnd + " - This period end: " + DDP.End);

                if (reading.ReadingEnd <= reading.ReadingStart)
                    throw new Exception("CheckReadingsIntegrity - ReadingEnd <= ReadingStart - This ReadingEnd: "
                            + reading.ReadingEnd + " - This ReadingStart: " + reading.ReadingStart);

                if (prevReading != null)
                {
                    if (reading.ReadingEnd <= prevReading.ReadingEnd)
                        throw new Exception("CheckReadingsIntegrity - ReadingEnd overlaps previous - This ReadingEnd: " 
                            + reading.ReadingEnd + " - Previous ReadingEnd: " + prevReading.ReadingEnd);
                    if (reading.ReadingStart < prevReading.ReadingEnd)
                        throw new Exception("CheckReadingsIntegrity - ReadingStart overlaps previous - This ReadingStart: "
                            + reading.ReadingStart + " - Previous ReadingEnd: " + prevReading.ReadingEnd);
                }
                prevReading = reading;
            }
        }

        // Check if reading is compatible with this period - no period overlaps and correct Start/End structure
        // exception on invalid reading dates
        public bool CheckReadingPeriodCompatibility(ReadingBase reading)
        {
            if (reading.ReadingEnd <= reading.ReadingStart)
                throw new Exception("CheckReadingPeriodCompatibility - Reading dates are invalid - ReadingStart: "
                            + reading.ReadingStart + " - ReadingEnd: " + reading.ReadingEnd);

            if (reading.ReadingStart < DDP.Start)
                return false;
            if (reading.ReadingEnd > DDP.End)
                return false;            

            return true;
        }

        // Find any readings that overlap the specified range
        // Assumes valid list structure
        // Does not include immediately adjacent values
        private List<ReadingBase> FindReadingsInRange(DateTime start, DateTime end)
        {
            List<ReadingBase> list = new List<ReadingBase>();
            foreach (ReadingBase reading in Readings.Values)
            {
                if (reading.ReadingStart >= start)
                {
                    if (reading.ReadingStart < end)
                        list.Add(reading);
                }
                else if (reading.ReadingEnd > start)
                    list.Add(reading);
            }
            return list;
        }

        public void RemoveReadingAt(int i)
        {
            bool haveMutex = false;
            try
            {
                RecordsMutex.WaitOne();
                haveMutex = true;
                Readings.Values[i].DeregisterPeriodInvolvement(DDP);
                Readings.RemoveAt(i);
            }
            finally
            {
                if (haveMutex)
                    RecordsMutex.ReleaseMutex();
            }
        }

        private ReadingBase FillSmallGap(ReadingBase reading, DateTime outputTime)
        {
            TimeSpan duration;
            ReadingBase newRec;
            if (outputTime < reading.ReadingStart)
            {
                // just alter this reading by moving ReadingStart (ReadingEnd is the DB key for the record; ReadingStart is just an attribute)
                reading.ReadingStart = outputTime;
                return null;
            }
            else if (outputTime > reading.ReadingEnd)
            {
                duration = outputTime - reading.ReadingEnd;
                newRec = reading.CloneGeneric(outputTime, duration);
                newRec.GapAdjustAdjacent(reading, true);
                return newRec;
            }
            
            throw new Exception("EnergyReading.FillSmallGap - Invalid outputTime: " + outputTime);            
        }

        public TimeSpan FillSmallGaps(DateTime readingStart, DateTime readingEnd, bool fillEndGap)
        {
            int i = 0;  // position in Readings
            ReadingBase reading = null;
            ReadingBase prevReading = null;
            ReadingBase nextReading = null;
            DateTime prevEndTime = readingStart;
            TimeSpan gap = TimeSpan.Zero;
            TimeSpan remainingGaps = TimeSpan.Zero;

            bool readingsAdded = false;
            String stage = "initial";
            bool haveMutex = false;

            try
            {                
                RecordsMutex.WaitOne();
                haveMutex = true;

                while (i < Readings.Count)
                {
                    reading = Readings.Values[i];
                    if ((i + 1) < Readings.Count)
                        nextReading = Readings.Values[i + 1];
                    else
                        nextReading = null;

                    // exit when end of range reached
                    if (reading.ReadingEnd >= readingEnd)
                    {
                        // prevent fillEndGap from acting
                        prevReading = reading;

                        break;
                    }

                    // ignore readings before start time
                    if (reading.ReadingStart < readingStart)
                    {
                        i++;
                        continue;
                    }

                    gap = reading.ReadingStart - prevEndTime;

                    // Fill this gap with reading based on adjacent readings
                    if (gap >= SmallGapLowerLimit) 
                        if (gap <= SmallGapUpperLimit)
                        {
                            stage = "FillSmallGap 2";
                            FillSmallGap(reading, prevEndTime);
                            readingsAdded = true;
                        }
                        else
                            remainingGaps += gap;  // tiny gaps excluded

                    prevEndTime = reading.ReadingEnd;
                    prevReading = reading;
                    i++;
                }

                // fill end gap if present and small enough
                stage = "FillEndGap";
                if (fillEndGap)
                    if (prevReading != null)
                    {
                        if (prevReading.ReadingEnd < readingEnd)
                        {
                            gap = readingEnd - prevReading.ReadingEnd;

                            if (gap >= SmallGapLowerLimit)
                                if (gap <= SmallGapUpperLimit)
                                {
                                    ReadingBase newRec = FillSmallGap(prevReading, readingEnd);
                                    AddReading(newRec);
                                    readingsAdded = true;
                                }
                                else
                                    remainingGaps += gap;
                        }
                    }
                    else
                    {
                        remainingGaps += (readingEnd - readingStart);
                    }

                stage = "Check";
                // Check interval alignment as gap fill can cross a boundary
                if (readingsAdded)
                    CheckReadingsIntegrity();
            }
            catch (Exception e)
            {
                throw new Exception("FillSmallGaps - Stage: " + stage + " - Exception: " + e.Message, e);
            }
            finally
            {
                if (haveMutex)
                    RecordsMutex.ReleaseMutex();
            }

            return remainingGaps;
        }

        private void FillGaps(List<ReadingBase> readings, ReadingBase fillReading)
        {
            ReadingBase prevReading = null;
            foreach (ReadingBase reading in readings)
            {
                if (prevReading == null)
                {
                    // Gap to be filled is at start of fillReading
                    if (reading.ReadingStart > fillReading.ReadingStart)
                    {
                        ReadingBase new1;
                        DDP.SplitReading(fillReading, reading.ReadingStart, out new1, out fillReading);
                        Readings.Add(new1.ReadingEnd, new1);
                    }
                }
                // Gap to be filled is in middle of fillRreading
                else if (prevReading.ReadingEnd < reading.ReadingStart)
                {
                    ReadingBase new1;
                    DDP.SplitReading(fillReading, prevReading.ReadingEnd, out new1, out fillReading); // Discard this - it shrinks fillReading
                    DDP.SplitReading(fillReading, reading.ReadingStart, out new1, out fillReading);
                    Readings.Add(new1.ReadingEnd, new1);
                }
                prevReading = reading;
            }

            // use all of fillReading
            if (prevReading == null)
                Readings.Add(fillReading.ReadingEnd, fillReading);
            // Gap to be filled is at end of fillReading
            else if (prevReading.ReadingEnd < fillReading.ReadingEnd)
            {
                ReadingBase new1;
                DDP.SplitReading(fillReading, prevReading.ReadingEnd, out new1, out fillReading); 
                Readings.Add(fillReading.ReadingEnd, fillReading);
            }
        }

        private void DeleteReadings(List<ReadingBase> readings, DateTime? fromTime = null, DateTime? toTime = null)
        {
            GenConnection con = null;
            try
            {                
                foreach (ReadingBase reading in readings)
                {
                    ReadingBase localReading = reading;
                    bool deleteReqd = false;
                    bool removed = false;
                    if (fromTime.HasValue && localReading.ReadingStart < fromTime && localReading.ReadingEnd > fromTime)
                    {
                        ReadingBase new1;

                        DDP.SplitReading(localReading, fromTime.Value, out new1, out localReading);
                        Readings.Remove(reading.ReadingEnd);
                        removed = true;
                        deleteReqd = true;
                        Readings.Add(new1.ReadingEnd, new1);
                    }
                    if (toTime.HasValue && localReading.ReadingStart < toTime && localReading.ReadingEnd > toTime)
                    {
                        ReadingBase new1;
                        DDP.SplitReading(localReading, toTime.Value, out new1, out localReading);
                        if (!removed)
                            Readings.Remove(localReading.ReadingEnd);

                        deleteReqd = false; // following replacement record must be updated not deleted
                        Readings.Add(localReading.ReadingEnd, localReading);
                    }
                    else if ((!fromTime.HasValue || localReading.ReadingStart >= fromTime.Value) && (!toTime.HasValue || localReading.ReadingEnd <= toTime.Value))
                    {
                        Readings.Remove(localReading.ReadingEnd);
                        deleteReqd = true;
                    }

                    if (deleteReqd && reading.InDatabase)
                    {
                        if (con == null)
                        {
                            GlobalSettings.SystemServices.GetDatabaseMutex();
                            con = GlobalSettings.TheDB.NewConnection();
                        }
                        reading.DeleteReading(con, DDP.DeviceId.Value);
                    }
                }
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                }                
            }
        }

        public enum AddReadingMode
        {
            Insert,
            InsertReplace,
            FillGaps
        }

        public void AddReading(ReadingBase reading, AddReadingMode mode = AddReadingMode.Insert)
        {
            bool haveMutex = false;
            try
            {
                RecordsMutex.WaitOne();
                haveMutex = true;

                if (!CheckReadingPeriodCompatibility(reading))
                    throw new Exception("AddReading - Reading invalid for this period - ReadingStart: "
                                + reading.ReadingStart + " - ReadingEnd: " + reading.ReadingEnd
                                + " - Period Start: " + DDP.Start + " - PeriodEnd: " + DDP.End);

                List<ReadingBase> impactedReadings = FindReadingsInRange(reading.ReadingStart, reading.ReadingEnd);
                if (impactedReadings.Count == 0)
                    Readings.Add(reading.ReadingEnd, reading);
                else if (mode == AddReadingMode.Insert) // insertion not allowed to colide with existing
                    throw new Exception("AddReading - Insert Mode collision - ReadingStart: "
                                + reading.ReadingStart + " - ReadingEnd: " + reading.ReadingEnd
                                + " - First impact start: " + impactedReadings[0].ReadingStart + " - First impact end: " + impactedReadings[0].ReadingEnd);
                else if (mode == AddReadingMode.InsertReplace)
                {
                    if (impactedReadings.Count == 1 && impactedReadings[0].ReadingEnd == reading.ReadingEnd && impactedReadings[0].ReadingStart == reading.ReadingStart)
                    {
                        // use update rather than delete and insert if record already in database
                        ReadingBase oldReading = impactedReadings[0];
                        if (reading.IsSameReadingValuesGeneric(oldReading))
                        {
                            oldReading.AddReadingMatch = reading.AddReadingMatch;
                            RecordsMutex.ReleaseMutex();
                            return;
                        }
                        reading.InDatabase = oldReading.InDatabase;
                        Readings.Remove(oldReading.ReadingEnd);
                    }
                    else
                    {
                        DeleteReadings(impactedReadings, reading.ReadingStart, reading.ReadingEnd); // delete any under footprint
                        reading.InDatabase = false;
                    }
                    Readings.Add(reading.ReadingEnd, reading); // add new or replacement
                }
                else if (mode == AddReadingMode.FillGaps)
                    FillGaps(impactedReadings, reading);

                CheckReadingsIntegrity();
            }
            finally
            {
                if (haveMutex)
                    RecordsMutex.ReleaseMutex();
            }
        }

        public void ClearReadings()
        {
            bool haveMutex = false;
            try
            {
                RecordsMutex.WaitOne();
                haveMutex = true;
                if (Readings != null)
                    foreach (ReadingBase reading in Readings.Values)
                        reading.DeregisterPeriodInvolvement(DDP);
                Readings = new SortedList<DateTime, ReadingBase>();
            }
            finally
            {
                if (haveMutex)
                    RecordsMutex.ReleaseMutex();
            }
        }


        // AlignIntervals will slice readings at Interval boundaries
        private void AlignIntervals(bool sliceGapFillReadings)
        {
            int i = 0;  // position in Readings
            ReadingBase reading;
            
            while (i < Readings.Count)
            {
                try
                {
                    reading = Readings.Values[i];

                    if (!sliceGapFillReadings && reading.IsHistoryReading())
                    {
                        // do not split GapFillReadings (from history) - they are expected to span multiple intervals and conform with required alignment
                        i++;
                        continue;
                    }                    

                    // last interval in current reading
                    int readingEndInterval = DDP.GetIntervalNo(reading.ReadingEnd);  // end time interval of current reading
                    //startTime = reading.ReadingEnd -reading.Duration;  // start time of current reading

                    // Ensure no readings cross an interval boundary

                    // get the first interval in the current reading
                    int readingStartInterval = DDP.GetIntervalNo(reading.ReadingStart, false); // start time interval of current reading

                    while (readingStartInterval < readingEndInterval)  // true if interval boundary is crossed
                    {
                        ReadingBase newReading1;
                        ReadingBase newReading2;
                        // split the reading at end of first interval in reading
                        DateTime intervalEndTime = DDP.GetDateTime(readingStartInterval);
                        DDP.SplitReading(reading, intervalEndTime, out newReading1, out newReading2);

                        // remove old and replace with two new readings
                        RemoveReadingAt(i);
                        AddReading(newReading1);
                        AddReading(newReading2);
                        i++;

                        // setup for next cycle
                        reading = newReading2;
                        readingStartInterval = DDP.GetIntervalNo(reading.ReadingStart, false);
                    }

                    i++;
                }
                catch (Exception e)
                {
                    GlobalSettings.LogMessage("ReadingsCollection.AlignIntervals", "Exception: " + e.Message);
                    throw e;
                }
            }
        }
       
        public void ConsolidateIntervals(DateTime consolidateTo)
        {
            AlignIntervals(false); // This chops on interval boundaries for all but GapFillReading (history) readings

            int i = 0;  // position in Readings
            
            ReadingBase reading;
            ReadingBase prevReading = null;
            int prevReadingInterval = -1;
            
            int currentInterval = -1;

            ReadingBase accumReading = null;
            bool replaceReadings = false;
            bool suppressAccum = false; // set to true when GapFillReading occurs in the interval - no accum allowed

            bool haveMutex = false;
            try
            {
                RecordsMutex.WaitOne();
                haveMutex = true;

                while (i < Readings.Count)
                {              
                    reading = Readings.Values[i];

                    int readingInterval = DDP.GetIntervalNo(reading.ReadingEnd);  // end time interval of current reading
                    int readingStartInterval = DDP.GetIntervalNo(reading.ReadingStart, false);

                    if (readingInterval != readingStartInterval || reading.IsHistoryReading())
                    {
                        // reading crosses interval boundary - do not consolidate - probably history record
                        // GapFillReading should not be merged with regular readings - needs to retain the history signature for future history adjustments
                        accumReading = null;
                        replaceReadings = false;
                        currentInterval = readingInterval;
                        suppressAccum = true;
                        i++;                        
                        continue;
                    }

                    if (readingInterval > currentInterval) // new interval detected
                    {
                        suppressAccum = false;
                        if (replaceReadings)
                        {
                            // finalise previous consolidated interval
                            AddReading(accumReading, AddReadingMode.InsertReplace);
                            replaceReadings = false;
                        }

                        if (reading.ReadingStart >= consolidateTo)
                        {
                            RecordsMutex.ReleaseMutex();
                            return;
                        }

                        // end time is end of interval on first reading in interval - no accum required
                        if (DDP.GetDateTime(readingInterval) == reading.ReadingEnd)
                        {
                            replaceReadings = false;
                            i++;
                            currentInterval = readingInterval;
                            continue;
                        }
                        accumReading = DDP.NewReadingGeneric(DDP.GetDateTime(readingInterval), DDP.IntervalDuration);
                        replaceReadings = true;
                        currentInterval = readingInterval;
                    }
                    i++;

                    if (!suppressAccum)
                    {
                        if (prevReading != null)
                            accumReading.AccumulateReading(prevReading, true, readingInterval != prevReadingInterval);
                        prevReading = reading;
                        prevReadingInterval = readingInterval;
                    }
                }
                if (prevReading != null)
                    accumReading.AccumulateReading(prevReading, true, true);

                if (replaceReadings)
                {
                    // finalise last consolidated interval
                    AddReading(accumReading, AddReadingMode.InsertReplace);
                    replaceReadings = false;
                }
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("ReadingsCollection.ConsolidateIntervals", "Exception: " + e.Message);
                throw e;
            }
            finally
            {
                if (haveMutex)
                    RecordsMutex.ReleaseMutex();
            }
        }
    }
}
