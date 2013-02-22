/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using System.Threading;
using System.Reflection;
using DeviceControl;
//using System.Windows.Forms;
using PVBCInterfaces;

namespace PVService
{
    public enum ExecutionState
    {
        Startup,
        Running,
        SuspendPending,
        Suspended,
        RestartPending,
        ShutdownPending
    }

    public class ExecutionManager 
    {
        //volatile private ApplicationSettings ApplicationSettings;

        public static ExecutionManager ThisExecutionManager;

        public GenThreadManagement.GenThreadManager GenThreadManager { get; private set; }

        public ManagerManager ManagerManager
        {
            get;
            private set;
        }

        //public MackayFisher.Utilities.SystemServices SystemServices;

        private Thread ExecutionManagerThread;

        ExecutionTimeLine ExecutionTimeLine;

        public ExecutionState ExecutionState
        {
            get;
            private set;
        }

        bool SuspendRequested;
        bool ShutdownRequested;

        DateTime LastLogChange = DateTime.MinValue;

        // The following loads SystemServices with appropriate parameter settings
        private void LoadLogSettings()
        {
            GlobalSettings.SystemServices.LogError = GlobalSettings.ApplicationSettings.LogError;
            GlobalSettings.SystemServices.LogFormat = GlobalSettings.ApplicationSettings.LogFormat;
            GlobalSettings.SystemServices.LogInformation = GlobalSettings.ApplicationSettings.LogInformation;
            GlobalSettings.SystemServices.LogStatus = GlobalSettings.ApplicationSettings.LogStatus;
            GlobalSettings.SystemServices.LogTrace = GlobalSettings.ApplicationSettings.LogTrace;
            GlobalSettings.SystemServices.LogMeterTrace = GlobalSettings.ApplicationSettings.LogMeterTrace;
            GlobalSettings.SystemServices.LogMessageContent = GlobalSettings.ApplicationSettings.LogMessageContent;
            GlobalSettings.SystemServices.LogDatabase = GlobalSettings.ApplicationSettings.LogDatabase;
            GlobalSettings.SystemServices.LogEvent = GlobalSettings.ApplicationSettings.LogEvent;
        }

        public ExecutionManager()
        {
            ThisExecutionManager = this;
            GlobalSettings.ApplicationSettings = new ApplicationSettings("settings.xml");
            GlobalSettings.SystemServices = new MackayFisher.Utilities.SystemServices(GlobalSettings.ApplicationSettings.BuildFileName(GlobalSettings.ApplicationSettings.LogFile));
            LastLogChange = DateTime.Today;
            GlobalSettings.ApplicationSettings.SetSystemServices(GlobalSettings.SystemServices);
            LoadLogSettings();

            ExecutionState = ExecutionState.Startup;
            SuspendRequested = false;

            GenThreadManager = new GenThreadManagement.GenThreadManager(GlobalSettings.SystemServices);
            ExecutionTimeLine = new ExecutionTimeLine();

            ManagerManager = new ManagerManager(this);
        }

        // The suspend / resume management thread loops in this function
        // it watches the current settings and the system clock
        // it moves the service in and out of a suspended state as required by the parameters
        private void ManageExecutionState()
        {
            try
            {
                ExecutionState reportedExecutionState = ExecutionState.Startup;
                DateTime? reportedTime = null;

                DateTime lastTime = DateTime.Now;
                bool fullStartRequired = true;

                ExecutionTimeLine.BuildFullDayTimeLine(true);
                ExecutionTimeLine.DumpTimeLine();
                DumpLogSettings();

                if (GlobalSettings.ApplicationSettings.EmitEvents)
                    ManagerManager.EnergyEvents.NewStatusEvent("Run State", ExecutionState.ToString());

                // exit when external shutdown requested
                while (ExecutionState != ExecutionState.ShutdownPending)
                {
                    NextActionInfo actionInfo = null;
                    try
                    {
                        DateTime today = DateTime.Today;
                        if (LastLogChange < today)
                        {
                            String stage = "initial";
                            try
                            {
                                if (GlobalSettings.ApplicationSettings.NewLogEachDay)
                                {
                                    String newLogName = GlobalSettings.ApplicationSettings.BuildFileName(GlobalSettings.ApplicationSettings.LogFile);
                                    GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "Starting new log - " + newLogName, LogEntryType.Information);

                                    stage = "OpenLogFile";
                                    GlobalSettings.SystemServices.OpenLogFile(newLogName);

                                    stage = "ManageOldLogFiles";
                                    GlobalSettings.SystemServices.ManageOldLogFiles(GlobalSettings.ApplicationSettings.DefaultDirectory,
                                        GlobalSettings.ApplicationSettings.BuildFileName("Archive"), GlobalSettings.ApplicationSettings.LogRetainDays);

                                    stage = "DumpTimeLine";
                                    ExecutionTimeLine.DumpTimeLine();

                                    stage = "DumpLogSettings";
                                    DumpLogSettings();
                                }
                                stage = "ReleaseErrorLoggers";
                                ManagerManager.ReleaseErrorLoggers();
                            }
                            catch (Exception e)
                            {
                                GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "NewLogEachDay - Stage: " + stage +
                                    " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                            }
                            finally
                            {
                                LastLogChange = today;
                            }
                        }

                        actionInfo = ExecutionTimeLine.GetNextActionInfo(DateTime.Now);
                        ExecutionState targetState = actionInfo.TargetState;

                        if (SuspendRequested && ExecutionState != ExecutionState.Suspended)
                        {
                            GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "External suspend", LogEntryType.Information);
                            targetState = ExecutionState.Suspended;
                        }
                        else if (ShutdownRequested)
                        {
                            GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "Shutdown Requested", LogEntryType.Information);
                            targetState = ExecutionState.ShutdownPending;
                        }

                        //if (ExecutionState != ExecutionState.ShutdownPending)
                        {
                            DateTime? dt;
                            String runDesc = "";

                            if (targetState == ExecutionState.Running)
                            {
                                dt = actionInfo.SuspendDateTime;
                                runDesc = "Running";
                            }
                            else if (targetState == ExecutionState.Suspended)
                            {
                                dt = actionInfo.ResumeDateTime;
                                if (actionInfo.SuspendType == SuspendPowerState.Idle)
                                    runDesc = "Idle";
                                else if (actionInfo.SuspendType == SuspendPowerState.Suspend)
                                    runDesc = "Sleeping";
                                else
                                    runDesc = "Hibernating";
                            }
                            else
                            {
                                dt = null;
                                if (targetState == ExecutionState.ShutdownPending)
                                    runDesc = "Shutdown";
                                else
                                    runDesc = targetState.ToString();
                            }

                            if (reportedExecutionState != targetState || reportedTime != dt)
                            {
                                if (dt != null)
                                    GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "State: " + runDesc +
                                        " Until:" + dt.Value, LogEntryType.Information);
                                else
                                    GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "State: " + runDesc,
                                        LogEntryType.Information);
                                reportedExecutionState = ExecutionState;
                                reportedTime = dt;
                            }
                        }

                        bool suspendPending = false;

                        if (targetState != ExecutionState)
                        {
                            if (targetState != ExecutionState.Running
                                && ExecutionState == ExecutionState.Running)
                                ManagerManager.StopService();

                            if (targetState == ExecutionState.Suspended)
                            {
                                // If not an external request - set the wakeup timer
                                if (!SuspendRequested && !ShutdownRequested)
                                {
                                    ExecutionState = ExecutionState.SuspendPending;
                                    if (actionInfo.ResumeDateTime != null)
                                        if (actionInfo.SuspendType != SuspendPowerState.Idle)
                                        {
                                            suspendPending = true;
                                            RequestSuspend(actionInfo.ResumeDateTime.Value, actionInfo.NextResumeDateTime, actionInfo.SuspendType);
                                        }
                                }
                            }
                            else if (targetState == ExecutionState.Running)
                            {
                                // One user reported DB errors at resume from hibernation 
                                // this allows a delay before manager thread restart
                                if (ExecutionState == ExecutionState.Suspended && GlobalSettings.ApplicationSettings.WakeDelay > 0)
                                {
                                    GlobalSettings.SystemServices.LogMessage("ManageExecutionState",
                                        "Using wake delay: " + GlobalSettings.ApplicationSettings.WakeDelay + " seconds", LogEntryType.Information);
                                    Thread.Sleep(GlobalSettings.ApplicationSettings.WakeDelay * 1000);
                                }
                                ManagerManager.StartService(fullStartRequired);
                                fullStartRequired = false;
                            }
                            ExecutionState = targetState;
                        }

                        if (!suspendPending)
                            if (actionInfo.ResumeDateTime != null && GlobalSettings.ApplicationSettings.ManualSuspendAutoResume)
                                // This will resume automatically from a manual suspend at a future scheduled start time
                                GlobalSettings.SystemServices.SetupWakeEvent(actionInfo.ResumeDateTime, actionInfo.NextResumeDateTime);
                            else
                                // Ensure timers are cleared - resume from manual suspend not required
                                GlobalSettings.SystemServices.SetupWakeEvent(null, null);

                        SuspendRequested = false;

                        if (ExecutionState == ExecutionState.Running && (ExecutionTimeLine.EnableEveningSuspend || ExecutionTimeLine.EnableIntervalSuspend))
                        {
                            // without this the OS will put the computer to sleep 2 minutes after resume via wake timer
                            SystemServices.SetThreadExecutionState(true, false, true, false);
                        }
                        else
                        {
                            // this allows the computer to sleep 2 minutes after resume via wake timer
                            SystemServices.SetThreadExecutionState(true, false, false, false);
                        }

                        if (ExecutionState != ExecutionState.ShutdownPending)
                        {
                            Thread.Sleep(30000);                           

                            if (GlobalSettings.SystemServices.ErrorLogCount > 20)
                            {
                                GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "Too many errors to continue: " + GlobalSettings.SystemServices.ErrorLogCount,
                                        LogEntryType.ErrorMessage);
                                InternalStopRequest = true;
                                ManagerManager.StopService();
                                PVService.Stop();
                            }
                            else
                                // Restart all stopped threads with autoRestart set
                                GenThreadManager.DoAutoRestart();
                        }
                    }
                    catch (ThreadInterruptedException)
                    {
                    }
                    catch (Exception e)
                    {
                        GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "Exception: " + e.Message + e.GetType().ToString(), LogEntryType.ErrorMessage);
                        InternalStopRequest = true;
                        ManagerManager.StopService();
                        PVService.Stop();
                    }

                    if (GlobalSettings.ApplicationSettings.EmitEvents)
                        ManagerManager.EnergyEvents.NewStatusEvent("Run State", ExecutionState.ToString());
                }
                GlobalSettings.SystemServices.KillWakeTimers();
                GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "Shutdown", LogEntryType.StatusChange);
                ShutdownRequested = false;
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("ManageExecutionState", "Ending - Exception: " + e.Message, LogEntryType.ErrorMessage);
                InternalStopRequest = true;
                ManagerManager.StopService();
                PVService.Stop();
            }
            if (GlobalSettings.ApplicationSettings.EmitEvents)
                ManagerManager.EnergyEvents.NewStatusEvent("Run State", "Shutdown");
        }

        private void RequestSuspend(DateTime wakeTime, DateTime? nextWakeTime, SuspendPowerState suspendType)
        {
            DateTime now = DateTime.Now;
            TimeSpan duration = ((DateTime)wakeTime) - now;

            // Following sleep added to that computer does not suspend immediately after PVBC service startup
            // Waits 20 seconds - just enough to change my monitor settings after starting the service
            if (duration > TimeSpan.FromMinutes(2.0))
            {
                Thread.Sleep(20000);
                now = DateTime.Now;
                duration = ((DateTime)wakeTime) - now;
            }

            if (suspendType == SuspendPowerState.Hibernate)
            {
                if (duration < TimeSpan.FromSeconds(180))
                    duration = TimeSpan.FromSeconds(180);  // avoid timer expiring before suspend complete, hibernate needs longer time
            }
            else if (duration < TimeSpan.FromSeconds(60))
                duration = TimeSpan.FromSeconds(60);

            GlobalSettings.SystemServices.LogMessage("RequestSuspend", "Suspending for " + duration.TotalMinutes + " minutes", LogEntryType.Information);

            GlobalSettings.SystemServices.SetupSuspendForDuration(suspendType, now + duration, nextWakeTime);
        }

        private PVService PVService;

        public bool StartService(PVService _PVService)
        {
            PVService = _PVService;
            #if (DEBUG)
                Thread.Sleep(20000);
            #endif
            CheckEnvironment checkEnvironment = new CheckEnvironment(GlobalSettings.ApplicationSettings, GlobalSettings.SystemServices);
            String message;

            if (!checkEnvironment.CheckDatabaseExists(out message))
            {
                GlobalSettings.SystemServices.LogMessage("StartService", "Verifying database availability - " + message, LogEntryType.ErrorMessage);
                return false;
            }

            if (ExecutionManagerThread == null || !ExecutionManagerThread.IsAlive)
            {
                ExecutionManagerThread = new Thread(new ThreadStart(ManageExecutionState));
                ExecutionManagerThread.Name = "ExecutionManager";
                ExecutionManagerThread.Start();
            }

            return true;
        }

        private bool InternalStopRequest = false;

        public void StopService(bool fullShutdown)
        {
            // requesting either suspend or full shutdown
            // note that suspend requests may be rejected in PVService and not passed here
            ShutdownRequested = fullShutdown;
            SuspendRequested = !fullShutdown;

            if (SuspendRequested && ExecutionState == ExecutionState.Suspended || InternalStopRequest)
                return; // already in suspended state or request is internal

            if (ExecutionManagerThread != null && ExecutionManagerThread.IsAlive)
            {
                // interrupt the manager to get an immediate response
                ExecutionManagerThread.Interrupt();
                // If suspending - wait for suspend to complete 
                // - the system is waiting for this call to complete before actually suspending
                if (!fullShutdown)
                    while (ExecutionState != ExecutionState.Suspended)
                        Thread.Sleep(300);
            }
        }

        private void DumpLogSettings()
        {
            GlobalSettings.SystemServices.LogMessage("", "", LogEntryType.Format);
            GlobalSettings.SystemServices.LogMessage("", "----------------------------------------------------------------------------------------------------------------------------------------", LogEntryType.Format);
            GlobalSettings.SystemServices.LogMessage("", "", LogEntryType.Format);
            GlobalSettings.SystemServices.LogMessage("", "PVService - version:" + Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                LogEntryType.StatusChange);
            GlobalSettings.SystemServices.LogMessage("", "Log Settings", LogEntryType.Format);
            GlobalSettings.SystemServices.LogMessage("", "----------------------------------------------------------------------------------------------------------------------------------------", LogEntryType.Format);

            GlobalSettings.SystemServices.LogMessage("", "          Log Trace: " + (GlobalSettings.SystemServices.LogTrace ? "On" : "Off"), LogEntryType.Format);
            GlobalSettings.SystemServices.LogMessage("", "    Log Meter Trace: " + (GlobalSettings.SystemServices.LogMeterTrace ? "On" : "Off"), LogEntryType.Format);
            GlobalSettings.SystemServices.LogMessage("", "Log Message Content: " + (GlobalSettings.SystemServices.LogMessageContent ? "On" : "Off"), LogEntryType.Format);
            GlobalSettings.SystemServices.LogMessage("", "       Log Database: " + (GlobalSettings.SystemServices.LogDatabase ? "On" : "Off"), LogEntryType.Format);

            GlobalSettings.SystemServices.LogMessage("", "----------------------------------------------------------------------------------------------------------------------------------------", LogEntryType.Format);
        }

        // the windows service control manager can request that settings be reloaded
        // all system components reference the ApplicationSettings object for setting details
        // database settings are only sampled at service startup
        // suspend / resume settings are taken from the current settings object
        // changes to suspend / resume, log content and pvoutput.org settings take immediate effect
        public void ReloadSettings()
        {
            GlobalSettings.SystemServices.LogMessage("ReloadSettings", "ReloadSettings", LogEntryType.Trace);
            //load settings into settings object - mutex protexted call
            GlobalSettings.ApplicationSettings.ReloadSettings();
            //load log message settings into SystemServices
            LoadLogSettings();
            ExecutionTimeLine.BuildFullDayTimeLine();

            ExecutionTimeLine.DumpTimeLine();
            DumpLogSettings();

            GlobalSettings.SystemServices.LogMessage("ReloadSettings", "ReloadSettings Complete", LogEntryType.Trace);
        }

        public void ReleaseErrorLoggers()
        {
            GlobalSettings.SystemServices.LogMessage("ReleaseErrorLoggers", "ReleaseErrorLog", LogEntryType.Trace);

            ManagerManager.ReleaseErrorLoggers();

            GlobalSettings.SystemServices.LogMessage("ReleaseErrorLoggers", "ReleaseErrorLog Complete", LogEntryType.Trace);
        }
    }
}
