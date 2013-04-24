/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace PVService
{
    public interface NextActionInfo  // packages detail regarding the expected next action at a specified DateTime
    {
        DateTime CurrentDateTime { get; }
        ExecutionState TargetState { get; }
        DateTime? SuspendDateTime { get; }
        DateTime? ResumeDateTime { get; }
        DateTime? NextResumeDateTime { get; }
        SuspendPowerState SuspendType { get; }
    }

    public class TheNextActionInfo  // packages detail regarding the expected next action at a specified DateTime
    : NextActionInfo
    {
        public DateTime CurrentDateTime { get; set; }
        public ExecutionState TargetState { get; set; }
        public DateTime? SuspendDateTime { get; set; }
        public DateTime? ResumeDateTime { get; set; }
        public DateTime? NextResumeDateTime { get; set; }
        public SuspendPowerState SuspendType { get; set; }
    }

    public enum TimeLineState
    {
        Running,
        Idle,
        Sleeping,
        Hibernating,
        Unspecified
    }

    public enum TimeLineLayer
    {
        EveningSuspend,
        IntervalSuspend,
        MeterHistory
    }

    public enum TimeLineSource
    {
        Default,
        EveningSuspend,
        CCMeter,
        IntervalSuspend
    }

    public struct TimeLineMinute
    {
        public TimeLineState State;
        public TimeLineLayer Layer;
        public bool Mandatory;
    }

    public struct TimeLinePattern
    {
        public TimeSpan StartTime;
        public TimeSpan EndTime;
        public TimeLineMinute Minute;
    }

    public struct TimeLine
    {
        public TimeSpan StartTime;
        public TimeSpan EndTime;
        public int Duration;
        public bool IsDefault;
        public TimeLineMinute[] Minute;
    }

    public class ExecutionTimeLine
    {
        public static System.Threading.Mutex TimeLineMutex = new System.Threading.Mutex();

        public bool EnableIntervalSuspend { get; private set; }
        public bool EnableEveningSuspend { get; private set; }

        private const int MinutesInDay = 60 * 24;

        private TimeLine FullDayTimeLine;

        public ExecutionTimeLine()
        {
            EnableIntervalSuspend = false;
            EnableEveningSuspend = false;
        }

        protected void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.Trace)
        {
            GlobalSettings.SystemServices.LogMessage("ExecutionTimeLine: " + routine, message, logEntryType);
        }

        private static TimeLine BuildTimeLine(List<TimeLinePattern> patterns, TimeLineMinute defaultMinute)
        {
            TimeLine timeLine;

            timeLine.IsDefault = false;

            if (patterns.Count == 0)
            {
                timeLine.StartTime = TimeSpan.FromMinutes(0);
                timeLine.EndTime = TimeSpan.FromMinutes(0);
                timeLine.Duration = 0;
                timeLine.Minute = null;
                return timeLine;
            }

            timeLine.StartTime = patterns[0].StartTime;
            timeLine.EndTime = patterns[patterns.Count - 1].EndTime;
            timeLine.Duration = ((int)(timeLine.EndTime - timeLine.StartTime).TotalMinutes) + 1;
            timeLine.Minute = new TimeLineMinute[timeLine.Duration];

            int curPos = 0;

            foreach (TimeLinePattern pattern in patterns)
            {
                int index = (int)(pattern.StartTime - timeLine.StartTime).TotalMinutes;
                int count = ((int)(pattern.EndTime - pattern.StartTime).TotalMinutes) + 1;

                while (curPos < index)
                    timeLine.Minute[curPos++] = defaultMinute;

                while (curPos < (index + count))
                    timeLine.Minute[curPos++] = pattern.Minute;
            }

            return timeLine;
        }

        private TimeLine CreateBaseTimeLine(TimeSpan? dayStart, TimeSpan? dayEnd, TimeLineState suspendState = TimeLineState.Idle)
        {
            TimeLineMinute suspendMinute;
            suspendMinute.State = suspendState;
            suspendMinute.Mandatory = true;
            suspendMinute.Layer = TimeLineLayer.EveningSuspend;

            TimeLineMinute defaultMinute;
            defaultMinute.State = TimeLineState.Running;
            defaultMinute.Mandatory = false;
            defaultMinute.Layer = TimeLineLayer.EveningSuspend;

            TimeLine timeLine;
            timeLine.StartTime = TimeSpan.FromMinutes(0);
            timeLine.EndTime = TimeSpan.FromMinutes(MinutesInDay - 1);
            timeLine.Duration = MinutesInDay;
            timeLine.Minute = new TimeLineMinute[MinutesInDay];
            timeLine.IsDefault = true;

            int pos = 0;
            int endPos;

            if (dayStart != null)
            {
                endPos = (int)dayStart.Value.TotalMinutes;
                timeLine.IsDefault &= !(pos < endPos);
                while (pos < endPos)
                    timeLine.Minute[pos++] = suspendMinute;
            }

            if (dayEnd == null)
                endPos = MinutesInDay;
            else
                endPos = (int)dayEnd.Value.TotalMinutes;

            while (pos < endPos)
                timeLine.Minute[pos++] = defaultMinute;

            timeLine.IsDefault &= !(pos < MinutesInDay);
            while (pos < MinutesInDay)
                timeLine.Minute[pos++] = suspendMinute;

            return timeLine;
        }

        private static TimeLine AddRepeatingPattern(TimeLine timeLineIn, List<TimeLinePattern> patterns, TimeLineMinute defaultMinute, TimeSpan startTime, TimeSpan endTime)
        {
            TimeLine timeLine = timeLineIn;
            if (patterns.Count == 0)
                return timeLine;
            timeLine.IsDefault = false;
            TimeLine pattern = BuildTimeLine(patterns, defaultMinute);

            int pos = (int)startTime.TotalMinutes;
            int endPos = (int)endTime.TotalMinutes;
            int patPos = 0;

            while (pos < endPos)
            {
                // do not overwrite mandatory state
                // do not use an Unspecified state
                if (!timeLine.Minute[pos].Mandatory && pattern.Minute[patPos].State != TimeLineState.Unspecified)
                    timeLine.Minute[pos++] = pattern.Minute[patPos++];
                else
                {
                    pos++;
                    patPos++;
                }
                if (patPos >= pattern.Minute.Length)
                    patPos = 0;
            }

            return timeLine;
        }

        private static TimeLineState GetTimeLineState(String state)
        {
            if (state == "idle")
                return TimeLineState.Idle;
            else if (state == "sleep")
                return TimeLineState.Sleeping;
            else if (state == "hibernate")
                return TimeLineState.Hibernating;
            else if (state == "run")
                return TimeLineState.Running;
            return TimeLineState.Unspecified;
        }

        private String TimeLineStateToString(TimeLineState state)
        {
            if (state == TimeLineState.Hibernating)
                return "Hibernating";
            else if (state == TimeLineState.Idle)
                return "Idle";
            else if (state == TimeLineState.Running)
                return "Running";
            else if (state == TimeLineState.Sleeping)
                return "Sleeping";
            else if (state == TimeLineState.Unspecified)
                return "Unspecified";
            return "Unknown";
        }

        public void DumpTimeLine()
        {
            int pos = 0;
            TimeLineMinute curMin = FullDayTimeLine.Minute[pos];
            TimeSpan startTime = TimeSpan.FromMinutes(0);

            LogMessage("", "----------------------------------------------------------------------------------------------------------------------------------------", LogEntryType.Format);
            LogMessage("", "Timeline Content", LogEntryType.Format);
            LogMessage("", "----------------------------------------------------------------------------------------------------------------------------------------", LogEntryType.Format);

            while (++pos < FullDayTimeLine.Duration)
            {
                TimeLineMinute newMin = FullDayTimeLine.Minute[pos];
                if (newMin.Mandatory != curMin.Mandatory || newMin.State != curMin.State)
                {
                    LogMessage("", "Start: " + startTime + " - End: " + TimeSpan.FromMinutes(pos - 1) +
                        " - " + TimeLineStateToString(curMin.State) + " - " + curMin.Layer + (curMin.Mandatory ? " - Mandatory" : ""), LogEntryType.Format);
                    curMin = newMin;
                    startTime = TimeSpan.FromMinutes(pos);
                }
            }

            LogMessage("", "Start: " + startTime + " - End: " + TimeSpan.FromMinutes(pos - 1) +
                " - " + TimeLineStateToString(curMin.State) + " - " + curMin.Layer + (curMin.Mandatory ? " - Mandatory" : ""), LogEntryType.Format);
            LogMessage("", "----------------------------------------------------------------------------------------------------------------------------------------", LogEntryType.Format);
        }

        public TimeLine BuildFullDayTimeLine(bool startup = false)
        {
            TimeLineMutex.WaitOne();

            //System.Threading.Thread.Sleep(30000);

            EnableIntervalSuspend = GlobalSettings.ApplicationSettings.EnableIntervalSuspend;
            EnableEveningSuspend = GlobalSettings.ApplicationSettings.EnableEveningSuspend;

            TimeSpan? dayStart = null;
            TimeSpan? dayEnd = null;
            TimeLineState eveningIdleState = TimeLineState.Idle;

            if (GlobalSettings.ApplicationSettings.EnableEveningSuspend)
            {
                dayStart = GlobalSettings.ApplicationSettings.ServiceStartTime;
                dayEnd = GlobalSettings.ApplicationSettings.ServiceStopTime;
                eveningIdleState = GetTimeLineState(GlobalSettings.ApplicationSettings.EveningSuspendType);
            }

            TimeLine timeLine = CreateBaseTimeLine(dayStart, dayEnd, eveningIdleState);

            if (GlobalSettings.ApplicationSettings.EnableIntervalSuspend)
            {
                TimeSpan? intervalStart = GlobalSettings.ApplicationSettings.IntervalStartTime;
                TimeSpan? intervalEnd = GlobalSettings.ApplicationSettings.IntervalStopTime;

                if (intervalStart == null)
                    intervalStart = dayStart;
                if (intervalEnd == null)
                    intervalEnd = dayEnd;

                if (intervalStart == null)
                    intervalStart = TimeSpan.FromMinutes(0);
                if (intervalEnd == null)
                    intervalEnd = TimeSpan.FromMinutes(MinutesInDay - 1);

                TimeSpan? wakeInterval = GlobalSettings.ApplicationSettings.ServiceWakeInterval;
                TimeSpan? suspendInterval = GlobalSettings.ApplicationSettings.ServiceSuspendInterval;

                if (wakeInterval != null)
                {
                    TimeLineMinute suspendMinute;
                    suspendMinute.State = GetTimeLineState(GlobalSettings.ApplicationSettings.ServiceSuspendType);
                    suspendMinute.Layer = TimeLineLayer.IntervalSuspend;
                    suspendMinute.Mandatory = false;

                    TimeLineMinute runningMinute;
                    runningMinute.State = TimeLineState.Running;
                    runningMinute.Mandatory = true;
                    runningMinute.Layer = TimeLineLayer.IntervalSuspend;

                    List<TimeLinePattern> patterns = new List<TimeLinePattern>();

                    TimeLinePattern wakePattern;
                    wakePattern.StartTime = TimeSpan.FromMinutes(0);
                    wakePattern.EndTime = wakeInterval.Value - TimeSpan.FromMinutes(1);
                    wakePattern.Minute = runningMinute;

                    patterns.Add(wakePattern);

                    if (suspendInterval != null)
                    {
                        TimeLinePattern suspendPattern;
                        suspendPattern.StartTime = wakePattern.EndTime + TimeSpan.FromMinutes(1);
                        suspendPattern.EndTime = wakePattern.EndTime + suspendInterval.Value;
                        suspendPattern.Minute = suspendMinute;
                        patterns.Add(suspendPattern);
                    }

                    timeLine = AddRepeatingPattern(timeLine, patterns, runningMinute, intervalStart.Value, intervalEnd.Value);
                    /*
                    if (((CCMeterManagerSettings)GlobalSettings.ApplicationSettings.MeterManagerList[0]).CCUsesHistoryUpdate
                        && GlobalSettings.ApplicationSettings.MeterHistoryTimeLineAdjust)
                    {
                        TimeLineMinute dontCareMinute;
                        dontCareMinute.State = TimeLineState.Unspecified;
                        dontCareMinute.Mandatory = false;
                        dontCareMinute.Layer = TimeLineLayer.MeterHistory;

                        TimeLineMinute meterMinute;
                        meterMinute.State = TimeLineState.Running;
                        meterMinute.Mandatory = true;
                        meterMinute.Layer = TimeLineLayer.MeterHistory;

                        int historyStartMinute = GlobalSettings.ApplicationSettings.MeterHistoryStartMinute == null ? -2 : GlobalSettings.ApplicationSettings.MeterHistoryStartMinute.Value;
                        int historyEndMinute = GlobalSettings.ApplicationSettings.MeterHistoryEndMinute == null ? 29 : GlobalSettings.ApplicationSettings.MeterHistoryEndMinute.Value;

                        patterns = new List<TimeLinePattern>();

                        TimeLinePattern dontCarePattern;
                        dontCarePattern.StartTime = TimeSpan.FromMinutes(0);
                        dontCarePattern.EndTime = TimeSpan.FromMinutes(60 + historyStartMinute - 1); // 2 hourhistory update cycle
                        dontCarePattern.Minute = dontCareMinute;
                        patterns.Add(dontCarePattern);

                        wakePattern.StartTime = TimeSpan.FromMinutes(60 + historyStartMinute);
                        wakePattern.EndTime = TimeSpan.FromMinutes(60 + historyEndMinute);
                        wakePattern.Minute = meterMinute;
                        patterns.Add(wakePattern);

                        dontCarePattern.StartTime = TimeSpan.FromMinutes(60 + historyEndMinute + 1);
                        dontCarePattern.EndTime = TimeSpan.FromMinutes(119); // 2 hourhistory update cycle
                        dontCarePattern.Minute = dontCareMinute;
                        patterns.Add(dontCarePattern);

                        timeLine = AddRepeatingPattern(timeLine, patterns, dontCareMinute, TimeSpan.FromMinutes(0), TimeSpan.FromMinutes(MinutesInDay - 1));
                    }
                     * */
                }
            }

            FullDayTimeLine = timeLine;

            TimeLineMutex.ReleaseMutex();

            return timeLine;
        }

        private void ScanTimeLine(int startMinute, DateTime startDate, ref DateTime? suspendDateTime, ref DateTime? resumeDateTime, ref DateTime? nextResumeDateTime)
        {
            TimeLineMinute curState = FullDayTimeLine.Minute[startMinute];
            bool isRunning = curState.State == TimeLineState.Running;
            bool doMore = true;
            int index = startMinute++;
            int dayCount = 0;

            do
            {
                // circular wrap around at end of day
                if (index == MinutesInDay)
                {
                    index = 0;
                    dayCount++;
                    if (dayCount > 2) // should not happen
                        break;
                }

                TimeLineMinute newState = FullDayTimeLine.Minute[index];

                if (newState.State != curState.State)
                {
                    if (suspendDateTime == null
                    && (curState.State == TimeLineState.Running)
                    && (newState.State == TimeLineState.Sleeping || newState.State == TimeLineState.Hibernating || newState.State == TimeLineState.Idle))
                        suspendDateTime = startDate + TimeSpan.FromMinutes(dayCount * MinutesInDay + index);


                    if ((curState.State == TimeLineState.Sleeping || curState.State == TimeLineState.Hibernating || curState.State == TimeLineState.Idle)
                    && (newState.State == TimeLineState.Running))
                        if (resumeDateTime == null)
                            resumeDateTime = startDate + TimeSpan.FromMinutes(dayCount * MinutesInDay + index);
                        else
                        {
                            nextResumeDateTime = startDate + TimeSpan.FromMinutes(dayCount * MinutesInDay + index);
                            doMore = false;
                        }

                    curState = newState;
                }
                index++;
            }
            while (doMore);
        }

        public NextActionInfo GetNextActionInfo(DateTime dateTime)
        {
            TimeLineMutex.WaitOne();

            TheNextActionInfo actionInfo = new TheNextActionInfo();

            TimeSpan time = dateTime.TimeOfDay;
            int curIndex = (int)time.TotalMinutes;

            actionInfo.CurrentDateTime = dateTime;

            TimeLineMinute minute = FullDayTimeLine.Minute[curIndex];

            if (minute.State == TimeLineState.Running)
            {
                actionInfo.TargetState = ExecutionState.Running;
                actionInfo.SuspendType = SuspendPowerState.Idle;
            }
            else if (minute.State == TimeLineState.Idle)
            {
                actionInfo.TargetState = ExecutionState.Suspended;
                actionInfo.SuspendType = SuspendPowerState.Idle;
            }
            else if (minute.State == TimeLineState.Sleeping)
            {
                actionInfo.TargetState = ExecutionState.Suspended;
                actionInfo.SuspendType = SuspendPowerState.Suspend;
            }
            else
            {
                actionInfo.TargetState = ExecutionState.Suspended;
                actionInfo.SuspendType = SuspendPowerState.Hibernate;
            }

            DateTime? suspendDateTime = null;
            DateTime? resumeDateTime = null;
            DateTime? nextResumeDateTime = null;

            if (FullDayTimeLine.IsDefault)
            {
                // default wake points 2 hours apart
                //used when Auto Resume from Manual Suspend is selected 
                //and no suspend / resume schedule is in operation
                resumeDateTime = dateTime.Date + TimeSpan.FromHours((int)(dateTime.Hour + 2));
                nextResumeDateTime = resumeDateTime + TimeSpan.FromHours(2);
            }
            else
                ScanTimeLine(curIndex, dateTime.Date, ref suspendDateTime, ref resumeDateTime, ref nextResumeDateTime);

            actionInfo.SuspendDateTime = suspendDateTime;
            actionInfo.ResumeDateTime = resumeDateTime;
            actionInfo.NextResumeDateTime = nextResumeDateTime;

            TimeLineMutex.ReleaseMutex();

            return actionInfo;
        }

    }
}

