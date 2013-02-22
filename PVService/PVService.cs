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
using GenericConnector;
using PVSettings;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Reflection;
using System.IO;
using MackayFisher.Utilities;
using System.Threading;
using PVBCInterfaces;
using DeviceControl;

namespace PVService
{
    public partial class PVService : ServiceBase
    {
        private ExecutionManager ExecutionManager;

        private void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.SystemServices.LogMessage("PVService", message, logEntryType);
        }

        public PVService()
        {
            InitializeComponent();

            ExecutionManager = new ExecutionManager();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                LogMessage("Start requested", LogEntryType.StatusChange);
                if (!ExecutionManager.StartService(this))
                    base.Stop();
            }
            catch (Exception e)
            {
                LogMessage("OnStart - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }

        protected override void OnStop()
        {
            try
            {
                LogMessage("Stop requested", LogEntryType.StatusChange);
                ExecutionManager.StopService(true);
            }
            catch (Exception e)
            {
                LogMessage("OnStop - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }

        protected override void OnShutdown()
        {
            try
            {
                LogMessage("Shutdown initiated", LogEntryType.StatusChange);
                ExecutionManager.StopService(true);
            }
            catch (Exception e)
            {
                LogMessage("OnShutdown - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        protected override void OnContinue()
        {
            base.OnContinue();
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            try
            {
                LogMessage("Received power event: " + powerStatus, LogEntryType.StatusChange);
                switch (powerStatus)
                {
                    case PowerBroadcastStatus.QuerySuspend:
                        ExecutionManager.StopService(false);
                        break;
                    case PowerBroadcastStatus.Suspend:
                        ExecutionManager.StopService(false);
                        break;
                    case PowerBroadcastStatus.ResumeAutomatic:
                        if (ExecutionManager.ExecutionState != ExecutionState.Running)
                            ExecutionManager.StartService(this);
                        break;
                    case PowerBroadcastStatus.ResumeSuspend:
                    case PowerBroadcastStatus.ResumeCritical:
                    case PowerBroadcastStatus.QuerySuspendFailed:
                        if (ExecutionManager.ExecutionState != ExecutionState.Running)
                            ExecutionManager.StartService(this);
                        break;
                };
                return true;
            }
            catch (Exception e)
            {
                LogMessage("OnPowerEvent - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }

        private enum CustomCommands 
        { 
            ReloadSettings = 128,
            ReleaseErrorLogger = 129 
        };

        protected override void OnCustomCommand(int command)
        {
            try
            {
                if (command == (int)CustomCommands.ReloadSettings)
                {
                    LogMessage("PVService - reloading settings", LogEntryType.StatusChange);
                    ExecutionManager.ReloadSettings();
                }
                else if (command == (int)CustomCommands.ReleaseErrorLogger)
                {
                    LogMessage("PVService - release error log requested", LogEntryType.StatusChange);
                    ExecutionManager.ReleaseErrorLoggers();
                }
                else
                    base.OnCustomCommand(command);
            }
            catch (Exception e)
            {
                LogMessage("OnCustomCommand - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }
    }
}
