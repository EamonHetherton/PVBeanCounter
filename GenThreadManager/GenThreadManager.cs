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
* along with PV Scheduler.
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
    public class GenThreadManager
    {
        private class GenThreadInfo
        {
            public String Name = "";
            public IGenThread GenThread = null;
            public GenThreadInfo ParentGenThreadInfo = null;
            public Thread Thread = null;
            public DateTime? StartTime = null;
            public int StartCount = 0;
            public bool AutoRestart = false;
        }

        private List<GenThreadInfo> Threads;
        private SystemServices SystemServices;

        private int RunningCount = 0;
        private ManualResetEvent AllStopped;

        private Object thisLock = new Object();

        public GenThreadManager(SystemServices systemServices)
        {
            Threads = new List<GenThreadInfo>();
            SystemServices = systemServices;
            AllStopped = new ManualResetEvent(true);
        }

        public int AddThread(IGenThread genThread, String threadNameSuffix = null, IGenThread parentGenThread = null, bool autoRestart = false)
        {
            // add thread to thread list
            GenThreadInfo info = new GenThreadInfo();
            info.Name = threadNameSuffix == null ? genThread.ThreadName : (genThread.ThreadName + "_" + threadNameSuffix);
            info.GenThread = genThread;
            if (parentGenThread != null)
                info.ParentGenThreadInfo = FindThread(parentGenThread);
            info.Thread = new Thread(info.GenThread.RunThread);
            info.Thread.Name = info.Name;
            info.StartTime = null;
            info.AutoRestart = autoRestart;
            info.StartCount = 0;

            Threads.Add(info);

            return info.GenThread.ThreadNo;
        }

        private void RestartThread(GenThreadInfo info)
        {
            info.Thread = new Thread(info.GenThread.RunThread);
            info.Thread.Name = info.Name;
            StartThread(info);
        }

        public void DoAutoRestart()
        {
            foreach (GenThreadInfo info in Threads)
            {
                if (info.AutoRestart && info.Thread != null 
                    && (info.Thread.ThreadState == ThreadState.Stopped || info.Thread.ThreadState == ThreadState.Aborted))
                    RestartThread(info);
            }
        }

        private GenThreadInfo FindThread(IGenThread genThread)
        {
            foreach (GenThreadInfo info in Threads)
                if (info.GenThread == genThread)
                    return info;
            return null;
        }

        private GenThreadInfo FindThread(int threadNo)
        {
            foreach (GenThreadInfo info in Threads)
                if (info.GenThread.ThreadNo == threadNo)
                    return info;
            return null;
        }

        public void StartThread(int id)
        {
            // start one thread
            GenThreadInfo info = FindThread(id);
            StartThread(info);
        }

        private void StartThread(GenThreadInfo info)
        {
            // GenThread will exit when ExitEvent is Set
            info.GenThread.ExitEvent.Reset();
            info.GenThread.IsRunning = true;
            info.Thread.Start();
            info.StartTime = DateTime.Now;
            info.StartCount++;
        }

        private void StopThread(GenThreadInfo info, bool forceStop = false, bool deleteThread = false)
        {
            // stop one thread if still marked as running
            if (info.Thread != null )
            {
                // tell GenThread to exit - polite request to stop (including child threads)
                info.AutoRestart = false;
                info.GenThread.IsRunning = false;
                info.GenThread.ExitEvent.Set();              

                // force the thread to stop - used to clean up stopped threads and threads with bad behaviour
                if (forceStop)
                {
                    if (!info.Thread.Join(5000))
                    {
                        SystemServices.LogMessage("GenThreadManager.StopThread", "Killing thread: " + info.Name, LogEntryType.ErrorMessage);
                        info.Thread.Interrupt();
                    }
                }
            }
            if (deleteThread && forceStop)
                Threads.Remove(info);
        }

        public void StopThread(int id, bool forceStop = false, bool deleteThread = false)
        {
            // stop one thread
            GenThreadInfo info = FindThread(id);
            if (info != null)
                StopThread(info, forceStop, deleteThread);
        }

        // Following is called as thread starts
        internal void ThreadHasStarted()
        {
            lock (thisLock)
            {
                // signal that at least 1 thread is running
                RunningCount++;
                AllStopped.Reset();
            }
            SystemServices.LogMessage("GenThreadManager.ThreadHasStarted", "Started", LogEntryType.Trace);
        }

        // following is called as thread stops
        internal void ThreadHasStopped()
        {
            SystemServices.LogMessage("GenThreadManager.ThreadHasStopped", "Stopping", LogEntryType.Trace);
            lock (thisLock)
            {
                // decrement running thread count
                RunningCount--;
                if (RunningCount < 1)
                {
                    // signal that all threads have stopped
                    RunningCount = 0;
                    AllStopped.Set();
                }
            }
        }

        public void StopThreads()
        {
            // stop any threads that are still running

            //First pass - request stop for each thread that has no parent thread
            //Threads with parent threads should be stopped by the parent
            foreach (GenThreadInfo info in Threads)
            {
                if ( info.ParentGenThreadInfo == null && info.Thread != null)
                    StopThread(info);
            }

            // Wait for up to 15 seconds for all threads to stop
            AllStopped.WaitOne(15000);
            
            // Remove all threads forcing any threads still running to stop
            while (Threads.Count > 0)
            {
                GenThreadInfo info = Threads[0];
                if (info.Thread != null)
                    StopThread(info, true);
                Threads.Remove(info);
            }

            lock (thisLock)
            {
                AllStopped.Set();
                RunningCount = 0;
            }
        }
    }
}
