/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using System.Xml.Linq;
using System.Threading;
using GenericConnector;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class CCManager : MeterManager
    {
        public struct CCSyncInfo
        {
            public Mutex MeterRecordsMutex;
            public ManualResetEvent RecordsAvailEvent;
            public List<CCLiveRecord>[] LiveRecords;
            public List<MeterRecord> HistoryRecords;
        }

        private class ReadingInfo
        {
            public Double Total = 0.0;
            public int Duration = 0;
            public List<AdjustHist> Records;
            public DateTime StartTime;
            public DateTime EndTime;

            public ReadingInfo()
            {
                Records = new List<AdjustHist>();
            }
        }

        private CCSyncInfo SyncInfo;

        //private bool IsStarting;

        private int CCMeterReaderId;

        // max allowed difference between CC Meter Time and computer time in Minutes
        public const int MeterTimeSyncTolerance = 10;
        // Time sync difference that triggers warnings
        public const int MeterTimeSyncWarning = 5;
        private DateTime? MeterTimeSyncWarningTime = null;
        // Is current CC Meter Time in Sync with computer time
        private bool MeterTimeInSync = false;

        private CCMeterManagerSettings Settings { get { return (CCMeterManagerSettings) MeterManagerSettings; } }

        public CCManager(GenThreadManager genThreadManager, IManagerManager managerManager, int meterManagerId, CCMeterManagerSettings settings) 
            : base(genThreadManager, settings, managerManager, meterManagerId)
        {
            SyncInfo.LiveRecords = new List<CCLiveRecord>[10];

            for (int i = 0; i < 10; i++)
                SyncInfo.LiveRecords[i] = new List<CCLiveRecord>();

            SyncInfo.HistoryRecords = new List<MeterRecord>();
            SyncInfo.MeterRecordsMutex = new Mutex();
            SyncInfo.RecordsAvailEvent = new ManualResetEvent(false);

            // IsStarting = true;

            CCMeterReader ccMeterReader = new CCMeterReader(genThreadManager, settings, SyncInfo);
            CCMeterReaderId = genThreadManager.AddThread(ccMeterReader);
        }

        protected override String MeterManagerType
        {
            get
            {
                return "CurrentCost";
            }
        }

        private void SplitRecord(ref AdjustHist rec, ref AdjustHist newRec, int newDuration)
        {
            newRec = rec;

            rec.Record.Energy = rec.Record.Energy * newDuration / rec.Record.Duration;

            if (rec.Record.Calculated != null)
                rec.Record.Calculated = rec.Record.Calculated * newDuration / rec.Record.Duration;

            rec.Record.Duration = newDuration;
            rec.Modify = true;

            newRec.Modify = true;
            newRec.IsNew = true;
            newRec.Record.Time = newRec.Record.Time.AddSeconds(-newDuration);
            newRec.Record.Duration -= newDuration;

            newRec.Record.Energy -= rec.Record.Energy;

            if (newRec.Record.Calculated != null)
                newRec.Record.Calculated -= rec.Record.Calculated;
        }

        private ReadingInfo LoadReadingRange(int appliance, DateTime startTime, DateTime endTime)
        {
            GenCommand cmd;
            string selReadingCmd =
                "select ReadingTime, Duration, Energy, Temperature, MinPower, MaxPower, Calculated " +
                "from meterreading " +
                "where Meter_Id = @MeterId " +
                "and Appliance = @Appliance " +
                "and ReadingTime > @StartTime " +
                "and ReadingTime <= @EndScanTime " +
                "order by ReadingTime " +
                "";
            cmd = new GenCommand(selReadingCmd, Connection);

            cmd.AddParameterWithValue("@MeterId", MeterManagerId);
            cmd.AddParameterWithValue("@Appliance", appliance);
            // ensure records immediately before and after are also loaded - for overlap management
            cmd.AddParameterWithValue("StartTime", startTime.AddHours(-2));
            cmd.AddParameterWithValue("@EndScanTime", endTime.AddHours(2));

            GenDataReader dr = (GenDataReader)cmd.ExecuteReader();
            bool atEnd = false;

            ReadingInfo info = new ReadingInfo();

            info.StartTime = startTime;
            info.EndTime = endTime;

            while (dr.Read() && !atEnd)
            {
                AdjustHist newRec;

                newRec.Record.Count = 1;

                newRec.Record.Time = dr.GetDateTime(0);

                if (newRec.Record.Time <= startTime)
                    continue;

                newRec.Record.Duration = dr.GetInt32(1);
                newRec.Record.Energy = dr.GetDouble(2);
                
                newRec.Record.Temperature = (dr.IsDBNull(3) ? (double?)null : dr.GetDouble(3));
                newRec.Record.MinPower = (dr.IsDBNull(4) ? (int?)null : dr.GetInt32(4));
                newRec.Record.MaxPower = (dr.IsDBNull(5) ? (int?)null : dr.GetInt32(5));
                newRec.Record.Calculated = (dr.IsDBNull(6) ? (double?)null : dr.GetDouble(6));

                newRec.Record.Sensor = appliance;
                newRec.Record.InRange = false;

                newRec.Modify = false;
                newRec.IsNew = false;

                // prorata trim startTime overlap
                if (newRec.Record.Time > startTime && newRec.Record.Time.AddSeconds(-newRec.Record.Duration) < startTime)
                {
                    AdjustHist extraRec = newRec;

                    if (appliance == 0)
                        LogRecordTracer("LoadReadingRange", "Splitting at Start", newRec);

                    int newDuration = (int)(newRec.Record.Time - startTime).TotalSeconds;

                    SplitRecord( ref newRec, ref extraRec, newDuration);
                    extraRec.Record.InRange = false;
                    info.Records.Add(extraRec);

                    if (appliance == 0)
                        LogRecordTracer("LoadReadingRange", "Add Start Split Record", extraRec);

                    LogMessage("LoadReadingRange", "Split reading at start time: " + startTime, LogEntryType.MeterTrace);
                }

                if (newRec.Record.Time > endTime)
                {
                    atEnd = true;

                    if (newRec.Record.Time.AddSeconds(-newRec.Record.Duration) < endTime)
                    {
                        AdjustHist extraRec = newRec;

                        if (appliance == 0)
                            LogRecordTracer("LoadReadingRange", "Splitting at End", newRec);

                        int newDuration = (int)((newRec.Record.Time - endTime).TotalSeconds);

                        SplitRecord(ref newRec, ref extraRec, newDuration);
                        extraRec.Record.InRange = true;
                        info.Records.Add(extraRec);

                        newRec.Record.InRange = true;

                        if (appliance == 0)
                            LogRecordTracer("LoadReadingRange", "Add End Split Record", extraRec);

                        LogMessage("LoadReadingRange", "Split reading at end time: " + endTime, LogEntryType.MeterTrace);

                        //info.TotalEnergy += extraRec.Record.Energy;
                        
                        if (extraRec.Record.Calculated == null)
                            info.Total += extraRec.Record.Energy;
                        else
                            info.Total += extraRec.Record.Calculated.Value;
                        
                        info.Duration += extraRec.Record.Duration;

                        if (appliance == 0)
                            LogDurationTracer("LoadReadingRange", "Split End Duration", extraRec.Record.Duration, info.Duration);
                    }
                    else
                        newRec.Record.InRange = false;
                }
                else
                {
                    atEnd = (newRec.Record.Time == endTime);

                    //info.TotalEnergy += newRec.Record.Energy;
                    
                    if (newRec.Record.Calculated == null)
                        info.Total += newRec.Record.Energy;
                    else
                        info.Total += newRec.Record.Calculated.Value;

                    info.Duration += newRec.Record.Duration;

                    newRec.Record.InRange = true;

                    if (appliance == 0)
                        LogDurationTracer("LoadReadingRange", "Duration", newRec.Record.Duration, info.Duration);
                }

                info.Records.Add(newRec);

                if (appliance == 0)
                    LogRecordTracer("LoadReadingRange", "Add Record", newRec);
            }

            dr.Close();
            dr.Dispose();
            dr = null;

            return info;
        }

        private void FixOverlaps(List<AdjustHist> records)
        {
            int recNo = 0;

            while (recNo < records.Count)
            {
                int recNoNext = recNo + 1;
                if (recNoNext < records.Count)
                {
                    int nextDuration = records[recNoNext].Record.Duration;
                    long gapSeconds = (long)(records[recNoNext].Record.Time.AddSeconds(-nextDuration) - records[recNo].Record.Time).TotalSeconds;

                    if (gapSeconds < 0)
                    {
                        AdjustHist rec = records[recNoNext];
                        LogMessage("FixOverlaps", "Overlap found - End Time: " + rec.Record.Time + 
                            " - Duration: " + nextDuration + " - Overlaps : " + records[recNo].Record.Time, 
                            LogEntryType.ErrorMessage);
                        
                        rec.Record.Duration -= (int)gapSeconds;
                        if (rec.Record.Duration > 0)
                        {
                            rec.Modify = true;
                            records[recNoNext] = rec;
                        }
                        else
                        {
                            records.RemoveAt(recNoNext);
                            recNo--;
                        }
                    }
                }
                recNo++;
            }
        }

        private AdjustHist NewAdjustHist(int appliance, int duration, double energy, double? calculated, double? temperature, DateTime endTime)
        {
            AdjustHist rec;

            rec.IsNew = true;
            rec.Modify = true;
                        
            rec.Record.Count = 1;
            rec.Record.Energy = energy;
            rec.Record.Calculated = calculated;
            rec.Record.Duration = duration;
            rec.Record.MaxPower = (int)(rec.Record.Energy  * 1000 * 3600 / duration);
            rec.Record.MinPower = rec.Record.MaxPower;
            rec.Record.Sensor = appliance;
            rec.Record.Temperature = temperature;
            rec.Record.Time = endTime;
            rec.Record.InRange = true;

            return rec;
        }

        private void FillTimeGaps(ReadingInfo info, int appliance, Double historyEnergy, Double? historyCalculated)
        {
            int recNo = 0;
            DateTime curTime = info.StartTime;
            
            // raw reading rate per second
            Double historyEnergyPerSecond = historyEnergy / 7200;

            // calibrated rate per second
            Double? historyCalculatedPerSecond = historyCalculated / 7200.0;

            Double currentEnergyPerSecond;

            while (recNo < info.Records.Count)
            {
                AdjustHist rec = info.Records[recNo];
                
                currentEnergyPerSecond = rec.Record.Energy / rec.Record.Duration;

                if (rec.Record.Time <= info.StartTime)
                {
                    recNo++;
                    continue;
                }

                if (appliance == 0)
                    LogRecordTracer("FillTimeGaps", "Input", rec);

                DateTime prevTime = rec.Record.Time.AddSeconds(-rec.Record.Duration);

                if (prevTime > info.EndTime)
                    prevTime = info.EndTime;

                int timeGap = (int)((prevTime - curTime).TotalSeconds);

                if (timeGap > 0)
                {
                    double energy;
                    double? calculated;

                    // gap records receive both a raw energy value and a calibrated value (if calibrations are in use)
                    energy = Math.Round(historyEnergyPerSecond * timeGap, 5);
                    if (historyCalculatedPerSecond == null)
                        calculated = null;
                    else
                        calculated = Math.Round(historyCalculatedPerSecond.Value * timeGap, 5);

                             
                    AdjustHist newRec = NewAdjustHist(appliance, timeGap, energy, calculated, rec.Record.Temperature, prevTime);

                    if (appliance == 0)
                        LogRecordTracer("FillTimeGaps", "Filling Gap", newRec);

                    info.Records.Insert(recNo++, newRec);

                    LogMessage("FillTimeGaps", "Added reading at time: " + prevTime + " - Energy: " + energy + " - Duration: " + timeGap, LogEntryType.MeterTrace);

                    // adjust reading set total - use calibrated value if calibrations are in use
                    if (calculated == null)
                        info.Total += energy;
                    else
                        info.Total += calculated.Value;

                    info.Duration += timeGap;
                }
                else
                {
                    if (timeGap < 0)
                        LogMessage("FillTimeGaps", "Time alignment error: " + rec.Record.Time, LogEntryType.ErrorMessage);
                }

                curTime = rec.Record.Time;
                recNo++;

                if (rec.Record.Time >= info.EndTime)
                    break;
            }

            if (curTime < info.EndTime)
            {
                int timeGap = (int)((info.EndTime - curTime).TotalSeconds);
                double energy;
                double? calculated;

                energy = Math.Round(historyEnergyPerSecond * timeGap, 5);
                if (historyCalculatedPerSecond == null)
                    calculated = null;
                else
                    calculated = Math.Round(historyCalculatedPerSecond.Value * timeGap, 5);
                
                AdjustHist rec = NewAdjustHist(appliance, timeGap, energy, calculated, null, info.EndTime);

                if (appliance == 0)
                    LogRecordTracer("FillTimeGaps", "Filling End Gap", rec);

                info.Duration += timeGap;

                // adjust reading set total - use calibrated value if calibrations are in use
                if (calculated == null)
                    info.Total += energy;
                else
                    info.Total += calculated.Value;

                info.Records.Insert(recNo, rec);

                LogMessage("FillTimeGaps", "Added reading at end time: " + info.EndTime + " - Energy: " + energy + " - Duration: " + timeGap, LogEntryType.MeterTrace);
            }
        }

        private  void ProrataEnergyAdjust(ReadingInfo info, Double historyEnergy)
        {
            // cannot prorata adjust zero values
            if (info.Total == 0.0)
                return;

            int recNo = 0;
            DateTime curTime = info.StartTime;

            // calculate adjustment required to match value in history record
            Double energyGap = historyEnergy - info.Total;
            // calculate multiplier to be applied to each value to achieve adjustment
            Double multiplier = historyEnergy / info.Total;

            while (recNo < info.Records.Count)
            {
                AdjustHist rec = info.Records[recNo];

                // extra records outside th erange may be present if the last record in range was split
                if (rec.Record.Time > info.EndTime)
                    break;

                if (rec.Record.Time <= info.StartTime)
                {
                    recNo++;
                    continue;
                }

                // use old calibrated value if calibrations in use
                double oldEnergy = rec.Record.Calculated == null ? rec.Record.Energy : rec.Record.Calculated.Value;
                // apply multiplier to obtain new value
                rec.Record.Calculated = Math.Round(oldEnergy * multiplier, 5);
                
                rec.Modify = true;

                // adjust record set total by adjustment amount
                double adjust = (rec.Record.Calculated.Value - oldEnergy);
                info.Total += adjust;

                info.Records[recNo++] = rec;

                // adjust remaining energy gap
                energyGap -= adjust;
            }
            if (Math.Abs(energyGap) >= 0.001)
                LogMessage("ProrataEnergyAdjust", "Energy gap exceeds 0.001 - " + info.StartTime, LogEntryType.ErrorMessage);
        }

        private void AdjustLiveWithHistory(MeterRecord histRec)
        {
            DateTime endTime = histRec.Time;
            DateTime startTime = endTime.AddHours(-2);

            double calibrate = Settings.ApplianceList[histRec.Sensor].Calibrate;

            LogMessage("AdjustLiveWithHistory", "Sensor: " + histRec.Sensor + 
                " - StartTime: " + startTime + " - End Time: " + endTime, LogEntryType.MeterTrace);

            ReadingInfo liveInfo;
            try
            {
                liveInfo = LoadReadingRange(histRec.Sensor, startTime, endTime);
                FixOverlaps(liveInfo.Records);
            }
            catch (Exception e)
            {
                LogMessage("AdjustLiveWithHistory", "LoadReadingRange threw Exception: " + e.Message, LogEntryType.ErrorMessage);
                return;
            }

            LogMessage("AdjustLiveWithHistory", "Start Time: " + startTime + " - End Time: " + endTime +
                " - Hist Energy: " + histRec.Energy + " - Live Energy: " + liveInfo.Total + 
                " - Live Duration: " + liveInfo.Duration, LogEntryType.MeterTrace);

            if (liveInfo.Duration > 7200) // seconds in 2 hours - 60 * 60 * 2
                LogMessage("AdjustLiveWithHistory", "Error - Live duration exceeds 2 hours: " + liveInfo.Duration + 
                    " - Start Time: " + startTime + " - End Time: " + endTime, LogEntryType.ErrorMessage);

            int extraSeconds = 7200 - liveInfo.Duration;
            
            if (extraSeconds > 0)
            {
                if (histRec.Sensor == 0)
                    LogDurationTracer("AdjustLiveWithHistory", "Target Duration", 0, extraSeconds);

                FillTimeGaps(liveInfo, histRec.Sensor, histRec.Energy, histRec.Calculated);
                
                if (liveInfo.Duration != 7200)
                {
                    LogMessage("AdjustLiveWithHistory", "FillTimeGaps failure - Start: " + startTime + 
                        " - End: " + endTime + " - Expected duration: 7200 - Actual: " + 
                        liveInfo.Duration, LogEntryType.ErrorMessage);
                }
            }

            Double historyEnergy = (histRec.Calculated == null ? histRec.Energy : histRec.Calculated.Value);
            Double energyGap = Math.Abs(historyEnergy - liveInfo.Total);                

            if (energyGap >= 0.001 && historyEnergy > 0.0)
            {                    
                LogMessage("AdjustLiveWithHistory", "Prorata adjustment from history - End Time: " + endTime + 
                    " - Live Info Energy: " + liveInfo.Total + " - History: " + historyEnergy + 
                    " - Gap: " + energyGap, LogEntryType.MeterTrace); 

                ProrataEnergyAdjust(liveInfo, historyEnergy);
                    
                LogMessage("AdjustLiveWithHistory", "Prorata result from history - End Time: " + endTime +
                    " - Live Info Energy: " + liveInfo.Total , LogEntryType.MeterTrace); 
            }

            UpdateReadingRecords(liveInfo.Records);
        }

        private void ProcessOneRecord(int sensor, CCLiveRecord record)
        {
            SyncInfo.MeterRecordsMutex.WaitOne();
            if (Settings.UseComputerTime)
                record.SelTime = record.TimeStampe;
            else
                record.SelTime = record.MeterTime;
            SyncInfo.MeterRecordsMutex.ReleaseMutex();

            bool curSyncStatus = MeterTimeInSync;
            int timeError = Convert.ToInt32(Math.Abs((record.MeterTime - record.TimeStampe).TotalMinutes));
            MeterTimeInSync = timeError <= MeterTimeSyncTolerance;

            // Issue warnings every hour if above warning threshold but below max tolerance
            if (MeterTimeInSync)
                if (timeError >= MeterTimeSyncWarning)
                {
                    if (MeterTimeSyncWarningTime == null || (MeterTimeSyncWarningTime + TimeSpan.FromHours(1.0)) < DateTime.Now)
                    {
                        LogMessage("ProcessOneRecord", "Meter time variance at WARNING threshold: " + timeError +
                        " minutes - History update is unreliable", LogEntryType.Information);
                        MeterTimeSyncWarningTime = DateTime.Now;
                    }
                }
                else
                    MeterTimeSyncWarningTime = null;

            // Log transitions across max tolerance threshold
            if (MeterTimeInSync != curSyncStatus)
                if (MeterTimeInSync)
                    LogMessage("ProcessOneRecord", "Meter time variance within tolerance: " + timeError +
                        " minutes - History adjust available", LogEntryType.Information);
                else
                    LogMessage("ProcessOneRecord", "Meter time variance exceeds tolerance: " + timeError +
                        " minutes - History adjust disabled", LogEntryType.Information);

            if (SensorStatusList[sensor].initialise)
            {
                SensorStatusList[sensor].CurrentMinute = GetMinute(record.SelTime.Value);
                SensorStatusList[sensor].PreviousTime = record.SelTime.Value;
                SensorStatusList[sensor].initialise = false;
            }

            DateTime thisMinute = GetMinute(record.SelTime.Value);
            DateTime currentMinute = SensorStatusList[sensor].CurrentMinute;

            int duration;

            if (thisMinute > currentMinute)
            {
                duration = (int)(currentMinute - SensorStatusList[sensor].PreviousTime).TotalSeconds;

                LogMessage("ProcessOneRecord", "End Minute - Sensor: " + sensor + " - Watts: " + record.Watts +
               " - curMin: " + SensorStatusList[sensor].CurrentMinute + " - prevTime: " + SensorStatusList[sensor].PreviousTime +
               " - Dur: " + duration);

                UpdateSensorList(currentMinute, currentMinute, sensor.ToString(), duration, record.Watts, record.Temperature);

                GlobalSettings.SystemServices.GetDatabaseMutex();
                try
                {
                    InsertMeterReading(SensorStatusList[sensor].Record);
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                }

                ResetSensor(SensorStatusList[sensor], thisMinute);
            }
            else if (record.SelTime.Value < SensorStatusList[sensor].PreviousTime)
            {
                LogMessage("ProcessOneRecord", "Time Warp Error: moved back in time - new time: " +
                    record.SelTime.Value + " - prev time: " + SensorStatusList[sensor].PreviousTime, LogEntryType.ErrorMessage);
                
                // discard timewarp records
                return;
            }

            duration = (int)(record.SelTime.Value - SensorStatusList[sensor].PreviousTime).TotalSeconds;

            LogMessage("ProcessOneRecord", "Sensor: " + sensor + " - Time: " + record.SelTime.Value + " - Watts: " + record.Watts +
                " - curMin: " + SensorStatusList[sensor].CurrentMinute + " - prevTime: " + SensorStatusList[sensor].PreviousTime +
                " - Dur: " + duration);

            UpdateSensorList(thisMinute, record.SelTime.Value, sensor.ToString(), duration, record.Watts, record.Temperature);
        }

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(0.0); } }

        public override void Initialise()
        {
            base.Initialise();

            // IsStarting = true;

            LogMessage("Initialise", "CCManager is starting", LogEntryType.StatusChange);

            GenThreadManager.StartThread(CCMeterReaderId);
        }

        public override bool DoWork()
        {
            int sensor;

            DateTime lastZero = DateTime.MinValue;

            if (SyncInfo.RecordsAvailEvent.WaitOne(3000))
            {
                SyncInfo.RecordsAvailEvent.Reset();

                for (sensor = 0; sensor < 10; sensor++)
                {
                    //bool sensorStarting = IsStarting;

                    while (SyncInfo.LiveRecords[sensor].Count > 0)
                    {
                        try
                        {
                            ProcessOneRecord(sensor, SyncInfo.LiveRecords[sensor][0]);
                        }
                        catch (Exception e)
                        {
                            LogMessage("DoWork", "ProcessOneRecord - Exception: " + e.Message, LogEntryType.ErrorMessage);
                            // discard record causing error - attempt to continue
                        }

                        SyncInfo.MeterRecordsMutex.WaitOne();
                        SyncInfo.LiveRecords[sensor].RemoveAt(0);
                        SyncInfo.MeterRecordsMutex.ReleaseMutex();
                    }
                }

                //IsStarting = false;

                GlobalSettings.SystemServices.GetDatabaseMutex();
                try
                {
                    while (SyncInfo.HistoryRecords.Count > 0)
                    {
                        sensor = SyncInfo.HistoryRecords[0].Sensor;

                        double calibrate = Settings.ApplianceList[sensor].Calibrate;
                        MeterRecord rec = SyncInfo.HistoryRecords[0];

                        // apply calibration to history records
                        if (calibrate != 1.0)
                            rec.Calculated = Math.Round(rec.Energy * calibrate, 4);

                        if (Settings.ApplianceList[sensor].StoreHistory)
                            InsertMeterHistory(rec);

                        if (Settings.ApplianceList[sensor].AdjustHistory && MeterTimeInSync)
                            try
                            {
                                AdjustLiveWithHistory(rec);
                            }
                            catch (Exception e)
                            {
                                LogMessage("DoWork", "AdjustLiveWithHistory threw exception: " + e.Message, LogEntryType.ErrorMessage);
                            }

                        SyncInfo.MeterRecordsMutex.WaitOne();
                        SyncInfo.HistoryRecords.RemoveAt(0);
                        SyncInfo.MeterRecordsMutex.ReleaseMutex();
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                }
            }
            return true;
        }

        public override void Finalise()
        {
            LogMessage("Finalise", "CCManager is stopping", LogEntryType.StatusChange);
            base.Finalise();
        }

    }
}
