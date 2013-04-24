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
using System.Threading;
using MackayFisher.Utilities;

namespace GenThreadManagement
{
    public interface IGenThread
    {
        bool DoWork();
        void Finalise();
        void Initialise();
        TimeSpan InitialPause { get; }
        TimeSpan Interval { get; }
        DateTime NextRunTime(DateTime? currentTime = null);
        TimeSpan? StartHourOffset { get; }
        string ThreadName { get; }
        int ThreadNo { get; }
        void RunThread();
        ManualResetEvent ExitEvent { get; }
        bool IsRunning { get; set; }
    }

    public abstract class GenThread : IGenThread
    {
        private static int nextThreadNo = 0;
        protected DateTime? PrevRunTime = null;
        protected bool InitialRun = true;

        public int ThreadNo { get; internal set; }

        public ManualResetEvent ExitEvent { get; private set; }
        private SystemServices SystemServices;
        protected GenThreadManager GenThreadManager;

        public bool IsRunning { get; set; }

        public GenThread(GenThreadManager genThreadManager, SystemServices systemServices)
        {
            SystemServices = systemServices;
            ConstructorCommon(systemServices);
            GenThreadManager = genThreadManager;
            IsRunning = false;
        }

        private void ConstructorCommon(SystemServices systemServices)
        {
            //SystemServices = systemServices;
            ExitEvent = new ManualResetEvent(false);
            ThreadNo = nextThreadNo++;
        }

        protected DateTime NextRunTimeStamp { get; private set; }

        public virtual DateTime NextRunTime(DateTime? currentTime = null)
        {
            DateTime now = currentTime == null ? DateTime.Now : currentTime.Value;
            DateTime nextTime;

            if (InitialRun)
                return now + InitialPause;

            if (StartHourOffset == null)
                if (PrevRunTime == null)
                    return now + Interval;
                else
                {
                    DateTime next =  PrevRunTime.Value + Interval;
                    if (next < now)
                        return now;
                    else
                        return next;
                }
                    
            // ensure hour offset is less than Inverval
            TimeSpan hourOffset = TimeSpan.FromSeconds((int)(StartHourOffset.Value.TotalSeconds % Interval.TotalSeconds));

            int intervalNum = ((int)now.TimeOfDay.TotalSeconds) / ((int)Interval.TotalSeconds);

            nextTime = now.Date + TimeSpan.FromSeconds((intervalNum + 1) * Interval.TotalSeconds) + hourOffset;

            return nextTime;
        }

        public abstract TimeSpan Interval { get; }

        public virtual TimeSpan? StartHourOffset
        {
            get
            {
                return null;
            }
        }

        public virtual TimeSpan InitialPause
        {
            get
            {
                return TimeSpan.FromMinutes(0.0);
            }
        }

        public virtual void Initialise()
        {
        }

        public abstract bool DoWork();

        public abstract String ThreadName { get; }

        public virtual void Finalise()
        {
        }

        public void RunThread()
        {
            GenThreadManager.ThreadHasStarted();

            SystemServices.LogMessage("GenThread.RunThread", "Thread starting - " + ThreadName, LogEntryType.Trace);

            try
            {
                Initialise();
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("GenThread.RunThread", "Thread: " + ThreadName + " - Initialise Exception: " + e.Message, LogEntryType.ErrorMessage);
                return;
            }  

            bool running = true;
            IsRunning = true;

            SystemServices.LogMessage("GenThread.RunThread", "Thread started - " + ThreadName, LogEntryType.Trace);

            while (IsRunning)
            {
                try
                {
                    DateTime nextTime = NextRunTime();
                    NextRunTimeStamp = nextTime;

                    InitialRun = false;
                    int wait = (int)(nextTime - DateTime.Now).TotalMilliseconds;
                    if (wait < 0)
                        wait = 0;
                   
                    // exit if ExitEvent is signaled
                    if (ExitEvent.WaitOne(wait))
                    {
                        IsRunning = false;
                        break;
                    }

                    running = DoWork();
                    PrevRunTime = nextTime;
                }
                catch (ThreadInterruptedException)
                {
                    running = false;
                }
                catch (Exception e)
                {
                    SystemServices.LogMessage("GenThread.RunThread", "Thread - " + ThreadName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    running = false;
                }
            }

            try
            {
                SystemServices.LogMessage("GenThread.RunThread", "Thread stopping - " + ThreadName, LogEntryType.Trace);
                Finalise();
                SystemServices.LogMessage("GenThread.RunThread", "Thread stopped - " + ThreadName, LogEntryType.Trace);
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("GenThread.RunThread", "Thread: " + ThreadName + " - Finalise Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            GenThreadManager.ThreadHasStopped();
        }
    }  
}
