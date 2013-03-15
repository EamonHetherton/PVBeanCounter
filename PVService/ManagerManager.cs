/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using System.Data.Common;
using GenericConnector;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using DeviceControl;
using OutputManagers;
using PVBCInterfaces;

namespace PVService
{
    public class ManagerManager : IDeviceManagerManager, IOutputManagerManager
    {
        public bool LiveLoadForced { get; private set; }

        public ExecutionManager ExecutionManager;
        public bool RunMonitors { get; private set; }
        public IEvents EnergyEvents { get; private set; }

        public EventManager EventManager { get; private set; }
        public List<DeviceControl.DeviceManagerBase> RunningDeviceManagers { get; private set; }
        public List<DeviceControl.DeviceManagerBase> ConsolidationDeviceManagers { get; private set; }
        public List<OutputManagers.PVOutputManager> RunningOutputManagers { get; private set; }

        private Object OutputReadyLock;

        public void SetOutputReady(String systemId)
        {
            bool found = false;
            lock (OutputReadyLock)
            {
                foreach(OutputManagers.PVOutputManager omgr in RunningOutputManagers)
                {
                    if (omgr.SystemId == systemId)
                    {
                        found = true;
                        omgr.OutputReadyEvent.Set();
                        break;
                    }                    
                }
            }
            if (!found)
                LogMessage("SetPVOutputReady - Cannot locate running PVOutput SystemId: " + systemId, LogEntryType.ErrorMessage);
            
        }

        public ManagerManager(ExecutionManager executionStateManager)
        {
            EnergyEvents = new DeviceControl.EnergyEvents(GlobalSettings.ApplicationSettings, this);
            ExecutionManager = executionStateManager;
            RunMonitors = false;

            // SQLite and MS Access requires explicit concurrency control
            // MySQL and SQL Server isolate concurrent queries from intra-command db changes (such as delete from table where...)
            GlobalSettings.SystemServices.UseDatabaseMutex = (GlobalSettings.ApplicationSettings.DatabaseType != "MySql" && GlobalSettings.ApplicationSettings.DatabaseType != "SQL Server");
            LiveLoadForced = false;

            RunningOutputManagers = new List<OutputManagers.PVOutputManager>();
            RunningDeviceManagers = new List<DeviceControl.DeviceManagerBase>();
            ConsolidationDeviceManagers = new List<DeviceControl.DeviceManagerBase>();
            EventManager = null;
            OutputReadyLock = new Object();
        }

        private static void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage("ManagerManager", message, logEntryType);
        }

        /*
        protected void CheckNextFileDates(int managerId, InverterManagerSettings managerSettings)
        {
            GenConnection connection = GlobalSettings.TheDB.NewConnection();

            if (managerSettings.FirstFullDay == null)
                return;

            Int32 result = -1;
            GenCommand cmd;

            string insCmd;
            if (managerSettings.ResetFirstFullDay)
                insCmd =
                    "update invertermanager set NextFileDate = @NextFileDate " +
                    "where Id = @ManagerId ";
            else
                insCmd =
                    "update invertermanager set NextFileDate = @NextFileDate " +
                    "where Id = @ManagerId " +
                    "and NextFileDate < @NextFileDate";

            try
            {
                cmd = new GenCommand(insCmd, connection);
                cmd.AddParameterWithValue("@NextFileDate", managerSettings.FirstFullDay);
                cmd.AddParameterWithValue("@ManagerId", managerId);

                result = cmd.ExecuteNonQuery();
                if (managerSettings.ResetFirstFullDay)
                    LogMessage("NextFullDay reset to " + managerSettings.FirstFullDay.ToString() +
                        " for manager Id " + managerId, LogEntryType.Information);
                else
                    LogMessage("NextFullDay set to no older than " + managerSettings.FirstFullDay.ToString() +
                        " for manager Id " + managerId, LogEntryType.Information);
            }
            catch (GenException e)
            {
                throw new Exception("CheckNextFileDates - DB Error setting NextFileDate: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("CheckNextFileDates - Error setting NextFileDate: " + e.Message, e);
            }
            finally
            {
                connection.Close();
                connection.Dispose();
            }
        }
        */

        public void ReleaseErrorLoggers()
        {
            foreach (DeviceControl.DeviceManagerBase rmm in RunningDeviceManagers)
            {
                rmm.CloseErrorLogger();
            }
        }

        private DeviceManagerBase ConstructDeviceManager(DeviceManagerSettings dmSettings)
        {
            DeviceManagerBase deviceManager;

            if (dmSettings.ManagerType == DeviceManagerType.SMA_SunnyExplorer)
                deviceManager = new DeviceManager_SMA_SunnyExplorer(ExecutionManager.GenThreadManager, dmSettings, this);
            else if (dmSettings.ManagerType == DeviceManagerType.CC128)
                deviceManager = new DeviceManager_CC128(ExecutionManager.GenThreadManager, dmSettings, this);
            else if (dmSettings.ManagerType == DeviceManagerType.Consolidation)
                deviceManager = new DeviceManager_EnergyConsolidation(ExecutionManager.GenThreadManager, dmSettings, this);
            else
                deviceManager = new DeviceManager_ActiveController<Device.Inverter>(ExecutionManager.GenThreadManager, dmSettings, this);

            return deviceManager;
        }

        private void StartDeviceManager(DeviceManagerBase deviceManager)
        {
            int genThreadId = ExecutionManager.GenThreadManager.AddThread(deviceManager, null, null, true);
            ExecutionManager.GenThreadManager.StartThread(genThreadId);            
        }

        public Device.DeviceBase FindDeviceFromSettings(DeviceManagerDeviceSettings deviceSettings)
        {
            foreach (DeviceManagerBase dm in RunningDeviceManagers)
                foreach (Device.DeviceBase d in dm.GenericDeviceList)
                    if (d.DeviceManagerDeviceSettings == deviceSettings)
                        return d;
            foreach (DeviceManagerBase dm in ConsolidationDeviceManagers)
                foreach (Device.DeviceBase d in dm.GenericDeviceList)
                    if (d.DeviceManagerDeviceSettings == deviceSettings)
                        return d;
            return null;
        }

        public Device.EnergyConsolidationDevice FindPVOutputConsolidationDevice(String systemId)
        {
            foreach (DeviceControl.DeviceManagerBase cdm in ConsolidationDeviceManagers)
            {
                Device.EnergyConsolidationDevice d = ((DeviceManager_EnergyConsolidation)cdm).GetPVOutputConsolidationDevice( systemId);
                if (d != null)
                    return d;
            }
            return null;
        }

        private int StartDeviceManagers()
        {
            int cnt = 0;
            try
            {
                LogMessage("Loading Device Managers", LogEntryType.Trace);

                foreach (DeviceManagerSettings dmSettings in GlobalSettings.ApplicationSettings.DeviceManagerList)
                {
                    DeviceManagerBase dm = null;
                    if (dmSettings.ManagerType == DeviceManagerType.Consolidation || dmSettings.Enabled)
                        dm = ConstructDeviceManager(dmSettings);

                    if (dm == null)
                        continue;

                    if (dmSettings.ManagerType == DeviceManagerType.Consolidation)
                    {
                        ConsolidationDeviceManagers.Add(dm);
                        continue;
                    }

                    if (!dmSettings.Enabled)
                        continue;

                    RunningDeviceManagers.Add(dm);
                    cnt++;
                }

                foreach (DeviceManagerBase dm in RunningDeviceManagers)
                {
                    foreach(Device.DeviceBase d in dm.GenericDeviceList)
                        d.BindConsolidations(this);                    
                }
                foreach (DeviceManagerBase dm in ConsolidationDeviceManagers)
                {
                    foreach (Device.DeviceBase d in dm.GenericDeviceList)
                        d.BindConsolidations(this);
                }
                foreach (DeviceManagerBase dm in RunningDeviceManagers)
                    StartDeviceManager(dm);
                LogMessage("Device Managers Loaded", LogEntryType.Trace);
            }
            catch (Exception e)
            {
                LogMessage("Exception starting Device Managers: " + e.Message, LogEntryType.ErrorMessage);
            }
            return cnt;
        }

        public void StartService(bool fullStartup)
        {
            GlobalSettings.TheDB = new GenDatabase(GlobalSettings.ApplicationSettings.Host, GlobalSettings.ApplicationSettings.Database,
                GlobalSettings.ApplicationSettings.UserName, GlobalSettings.ApplicationSettings.Password,
                GlobalSettings.ApplicationSettings.DatabaseType, GlobalSettings.ApplicationSettings.ProviderType,
                GlobalSettings.ApplicationSettings.ProviderName, GlobalSettings.ApplicationSettings.OleDbName,
                GlobalSettings.ApplicationSettings.ConnectionString, GlobalSettings.ApplicationSettings.DefaultDirectory, GlobalSettings.SystemServices);

            if (fullStartup)
            {
                LogMessage("StartService: connecting to database: " + GlobalSettings.TheDB.ConnectionString, LogEntryType.Information);
                VersionManager vm = new VersionManager();
                GenConnection con = GlobalSettings.TheDB.NewConnection();
                vm.UpdateVersion(con, GlobalSettings.ApplicationSettings.DatabaseType);
                con.Close();
                con.Dispose();
            }

            RunMonitors = true;

            if (GlobalSettings.ApplicationSettings.EmitEvents)
                StartEventManager();

            StartDeviceManagers();

            if (GlobalSettings.ApplicationSettings.EnablePVOutput)
                StartOutputManagers();
        }
      
        public void StopService()
        {
            try
            {
                RunMonitors = false;
                ExecutionManager.GenThreadManager.StopThreads();                
                RunningOutputManagers.Clear();
                RunningDeviceManagers.Clear();
                ConsolidationDeviceManagers.Clear();
                EventManager = null;
            }
            catch (Exception e)
            {
                LogMessage("StopService: Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }

        private bool StartEventManager()
        {
            try
            {
                LogMessage("Loading Event Manager", LogEntryType.Information);
                EventManager = new EventManager(ExecutionManager.GenThreadManager, EnergyEvents);

                int emId = ExecutionManager.GenThreadManager.AddThread(EventManager);
                ExecutionManager.GenThreadManager.StartThread(emId);

                LogMessage("Event Manager Loaded", LogEntryType.StatusChange);

                return true;
            }
            catch (Exception e)
            {
                LogMessage("Starting Event Manager - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
        }

        private int StartOutputManagers()
        {
            int count = 0;

            String managerName = "";
            try
            {
                LogMessage("Loading PvOutput Managers", LogEntryType.Information);

                for (int i = 0; i < GlobalSettings.ApplicationSettings.PvOutputSystemList.Count; i++)
                {
                    PvOutputSiteSettings managerSettings = GlobalSettings.ApplicationSettings.PvOutputSystemList[i];
                    managerName = managerSettings.Name;

                    if (!managerSettings.Enable)
                        continue;

                    OutputManagers.PVOutputManager pvOutputManager = new OutputManagers.PVOutputManager(ExecutionManager.GenThreadManager, this, managerSettings);

                    int PVOutputId = ExecutionManager.GenThreadManager.AddThread(pvOutputManager);
                    ExecutionManager.GenThreadManager.StartThread(PVOutputId);

                    LogMessage("PVOutput Manager - " + managerName + " - Loaded", LogEntryType.Trace);

                    count++;
                    RunningOutputManagers.Add(pvOutputManager);
                }

                if (count > 0)
                    LogMessage(count + " PVOutput Managers Loaded", LogEntryType.StatusChange);
                else
                    LogMessage("No PVOutput Managers Loaded", LogEntryType.Information);

                return count;
            }
            catch (Exception e)
            {
                LogMessage("Starting PvOutput Managers - Type: " + managerName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedError, "StartInverterManagers: " + e.Message, e);
            }
        }
    }
}
