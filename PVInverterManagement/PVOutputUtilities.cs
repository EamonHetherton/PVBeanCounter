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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Globalization;
using GenericConnector;
using System.Net;
using System.IO;
using System.Threading;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class PvOutputManager : GenThread
    {
        private const String CmdStr = "select OutputDay, OutputTime, Energy, Power, MinPower, " +
            "RealMaxPower, RealMinPower, MaxTemp " +
            "from pvoutput_v " +
            "where SiteId = @SiteId " +
            "and OutputDay >= @DateLimit " +
            "order by OutputDay, OutputTime";

        private const String Cmd5MinStr = "select OutputDay, OutputTime, Energy, Power, MinPower, " +
            "RealMaxPower, RealMinPower, MaxTemp " +
            "from pvoutput5min_v " +
            "where SiteId = @SiteId " +
            "and OutputDay >= @DateLimit " +
            "order by OutputDay, OutputTime";

        private const String CmdCheckStr =
            "select Energy, Power, ImportEnergy, ImportPower " +
            "from pvoutputlog " +
            "where SiteId = @SiteId " +
            "and OutputDay = @OutputDay " +
            "and OutputTime = @OutputTime";

        private const String CmdUpdate =
            "update pvoutputlog set Energy = @Energy, Power = @Power, Loaded = 0 " +
            "where SiteId = @SiteId " +
            "and OutputDay = @OutputDay " +
            "and OutputTime = @OutputTime;";

        private const String CmdUpdate_Temp =
            "update pvoutputlog set Energy = @Energy, Power = @Power, Loaded = 0, Temperature = @Temperature " +
            "where SiteId = @SiteId " +
            "and OutputDay = @OutputDay " +
            "and OutputTime = @OutputTime;";

        private const String CmdUpdateConsume =
            "update pvoutputlog set ImportEnergy = @Energy, ImportPower = @Power, Loaded = 0 " +
            "where SiteId = @SiteId " +
            "and OutputDay = @OutputDay " +
            "and OutputTime = @OutputTime;";

        private const String CmdUpdateConsume_Temp =
            "update pvoutputlog set ImportEnergy = @Energy, ImportPower = @Power, Loaded = 0, Temperature = @Temperature " +
            "where SiteId = @SiteId " +
            "and OutputDay = @OutputDay " +
            "and OutputTime = @OutputTime;";

        private const String CmdInsert =
            "insert into pvoutputlog ( SiteId, OutputDay, OutputTime, Energy, Power, Loaded) " +
            "values ( @SiteId, @OutputDay, @OutputTime, @Energy, @Power, 0)";

        private const String CmdInsert_Temp =
            "insert into pvoutputlog ( SiteId, OutputDay, OutputTime, Energy, Power, Loaded, Temperature) " +
            "values ( @SiteId, @OutputDay, @OutputTime, @Energy, @Power, 0, @Temperature)";

        private const String CmdInsertConsume =
            "insert into pvoutputlog ( SiteId, OutputDay, OutputTime, Loaded, ImportEnergy, ImportPower) " +
            "values ( @SiteId, @OutputDay, @OutputTime, 0, @Energy, @Power)";

        private const String CmdInsertConsume_Temp =
            "insert into pvoutputlog ( SiteId, OutputDay, OutputTime, Loaded, ImportEnergy, ImportPower, Temperature) " +
            "values ( @SiteId, @OutputDay, @OutputTime, 0, @Energy, @Power, @Temperature)";

        private const String CmdLoadSelect = "select SiteId, OutputDay, OutputTime, Energy, Power, " +
            "ImportEnergy, ImportPower, Temperature " +
            "from pvoutputlog " +
            "where SiteId = @SiteId " +
            "and Loaded = 0  and (Energy > 0  or ImportEnergy > 0)" +
            "and OutputDay >= @FirstDay " +
            "order by SiteId, OutputDay desc, OutputTime asc ";

        private const String CmdLoadUpdate = "update pvoutputlog set Loaded = 1 " +
            "where SiteId = @SiteId " +
            "and OutputDay = @OutputDay " +
            "and OutputTime = @OutputTime";

        // Changed ForceLoad from update to delete to allow removal of records at incorrect times (i.e. yield at night)
        private const String CmdForceLoad = "delete from pvoutputlog " +
            "where SiteId = @SiteId " +
            "and OutputDay = @OutputDay ";

        private const String CmdDeleteLog = "delete from pvoutputlog " +
            "where SiteId = @SiteId " +
            "and OutputDay < @LimitDay";

        private const String CmdSelectOldestDay = "select min(OutputTime) from outputhistory";

        // caution - the following query was one of few I could get to work with SQLite
        // SQLite date handling is pure crap and totally incompatible with any other version of SQL I have ever seen!!!
        // I could not get it to work at all with a view. I was forced to write the query against a base table
        private const String CmdSelectDayOutput = "select SUM(OutputKwh) " +
            "from outputhistory " +
            "where OutputTime >= @OutputDay and OutputTime < @NextOutputDay";

        public override String ThreadName { get { return "PVOutput/" + Settings.SystemId; } }

        private PvOutputSiteSettings Settings;

        public String OutputSiteId { get; private set; }
        private String APIKey;

        private bool ErrorReported = false;

        const int PVOutputDelay = 1100;
        const int PVOutputr1Multiple = 2;
        const int PVOutputr2Multiple = 30;
        const int PVOutputHourLimit = 60;
        const int PVOutputr1Size = 10;
        const int PVOutputr2Size = 30;

        const int PVLiveDaysDefault = 2;
        private int PVLiveDays;
        private DateTime PVDateLimit;

        private DateTime LastYieldReady;
        private DateTime LastConsumeReady;
        private int PVInterval;

        private int BackloadCount = 0;
        private bool Complete;
        private DateTime? IncompleteTime;
        private bool InitialOutputCycle;
        private DateTime LastInverterCheck = DateTime.MinValue;

        DateTime? LastDeleteOld = null;

        private int RequestCount;
        private int RequestHour;
        private bool PVOutputLimitReported;
        private bool PVOutputCurrentDayLimitReported;
        private int?[] MeterIds;

        private IManagerManager ManagerManager;

        public ManualResetEvent OutputReadyEvent { get; private set; }

        private Object OutputProcessLock;

        private void LogMessage(String routine, String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage(routine, message, logEntryType);
        }

        public PvOutputManager(GenThreadManager genThreadManager, IManagerManager managerManager, PvOutputSiteSettings settings)
            : base(genThreadManager, GlobalSettings.SystemServices)
        {
            ManagerManager = managerManager;
            Settings = settings;
            OutputSiteId = settings.SystemId;
            APIKey = settings.APIKey;
            if (settings.LiveDays == null)
                PVLiveDays = PVLiveDaysDefault;
            else
                PVLiveDays = settings.LiveDays.Value;

            RequestCount = 0;
            RequestHour = (int)DateTime.Now.TimeOfDay.TotalHours;
            PVOutputLimitReported = false;
            PVOutputCurrentDayLimitReported = false;
            Complete = true;
            IncompleteTime = null;
            InitialOutputCycle = true;

            MeterIds = new int?[GlobalSettings.ApplicationSettings.MeterManagerList.Count];
            for (int i = 0; i < GlobalSettings.ApplicationSettings.MeterManagerList.Count; i++)
                MeterIds[i] = null;

            OutputReadyEvent = new ManualResetEvent(true);
            LastYieldReady = DateTime.MinValue;
            PVInterval = settings.DataInterval == "" ? 10 : Convert.ToInt32(settings.DataInterval);
            OutputProcessLock = new Object();
        }

        private enum OutputReady
        {
            NotReady = 0,
            YieldReady = 1,
            ConsumeReady = 2,
            BothReady = 3
        }

        private OutputReady CheckOutputReady()
        {
            int inverterRequiredCount = 0;
            int inverterReadyCount = 0;
            int inverterLateCount = 0;
            /*
            foreach (RunningInverterManager rim in ManagerManager.RunningInverterManagers)
            {
                foreach (DeviceStatus status in ((InverterManager)rim.InverterManager).InverterDataRecorder.InverterStatusList)
                {
                    if (status.SiteId == OutputSiteId)
                    {
                        inverterRequiredCount++;
                        if (status.LastOutput > status.OutputRecorded)
                            inverterReadyCount++;
                        else if (status.NextOutput < DateTime.Now)
                            inverterLateCount++;
                    }
                }
            }
            */

            /*
            foreach (IDeviceManager mm in ManagerManager.RunningDeviceManagers)
            {
                if (mm.DataRecorder != null)
                    foreach (DeviceStatus status in mm.DataRecorder.InverterStatusList)
                    {
                        if (status.SiteId == OutputSiteId)
                        {
                            inverterRequiredCount++;
                            if (status.LastOutput > status.OutputRecorded)
                                inverterReadyCount++;
                            else if (status.NextOutput < DateTime.Now)
                                inverterLateCount++;
                        }
                    }
            }
            */
            int consumeReadyCount = 0;
            /*
            foreach (MeterManager mm in ManagerManager.RunningMeterManagers)
            {                
                foreach (MeterManager.SensorInfo info in mm.SensorStatusList)
                {
                    if (info.SiteId == OutputSiteId)
                    {
                        if (info.LastOutput > info.OutputRecorded)
                            consumeReadyCount++;
                    }
                }
            }
            */
            OutputReady result;

            LogMessage("CheckOutputReady", "inverterRequiredCount: " + inverterRequiredCount + 
                " - inverterReadyCount: " + inverterReadyCount + " - inverterLateCount: " + inverterLateCount +
                " - consumeReadyCount: " + consumeReadyCount, LogEntryType.Trace);

            if (inverterRequiredCount == (inverterReadyCount + inverterLateCount) && inverterReadyCount > 0) // Yield Ready
                if (consumeReadyCount > 0)
                    result = OutputReady.BothReady;
                else
                    result = OutputReady.YieldReady;
            else if (consumeReadyCount > 0 && inverterRequiredCount == inverterLateCount)
                result = OutputReady.ConsumeReady;
            else
            {
                result = OutputReady.NotReady;
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("CheckOutputReady", "Not ready", LogEntryType.Trace);
                return result;
            }

            // Triggers pvoutput if yield recorded or over 10 minutes since last yield and 4 Consume entries have been skipped
            // This ensures that yield appears first at pvoutput unless there is a large gap between yield and consume captures
            // At night this allows consume visibility at pvoutput every 5 minutes           

            bool consumeReady = false;

            if (result == OutputReady.YieldReady || result == OutputReady.BothReady)
                LastYieldReady = DateTime.Now;
            else
            {
                // only trigger on consumption if at the edge of a PVOutput upload interval         
                int lastInterval = ((int)LastConsumeReady.TimeOfDay.TotalMinutes) / PVInterval;
                LastConsumeReady = DateTime.Now;
                int thisInterval = ((int)LastConsumeReady.TimeOfDay.TotalMinutes) / PVInterval;
                consumeReady = (lastInterval != thisInterval);                
            }

            if (result == OutputReady.YieldReady
                || result == OutputReady.BothReady
                || consumeReady && ((int)(LastConsumeReady - LastYieldReady).TotalMinutes) > 10)
            {
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("CheckOutputReady", "Result: " + result.ToString(), LogEntryType.Trace);

                return result;
            }
            else
            {
                result = OutputReady.NotReady;
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("CheckOutputReady", "No yield - mid consume interval", LogEntryType.Trace);
                return result;
            }
        }

        private void SetOutputRecorded()
        {
            /*
            foreach (RunningInverterManager rim in ManagerManager.RunningInverterManagers)
            {
                foreach (DeviceStatus status in ((InverterManager)rim.InverterManager).InverterDataRecorder.InverterStatusList)
                {
                    if (status.SiteId == OutputSiteId)
                    {
                        if (status.LastOutput > status.OutputRecorded)
                            status.OutputRecorded = status.LastOutput;
                    }
                }
            }
            */
            /*
            foreach (IDeviceManager mm in ManagerManager.RunningDeviceManagers)
            {
                if (mm.DataRecorder != null)
                    foreach (DeviceStatus status in mm.DataRecorder.InverterStatusList)
                    {
                        if (status.SiteId == OutputSiteId)
                        {
                            if (status.LastOutput > status.OutputRecorded)
                                status.OutputRecorded = status.LastOutput;
                        }
                    }
            }
            */
            /*
            foreach (MeterManager mm in ManagerManager.RunningMeterManagers)
            {
                foreach (MeterManager.SensorInfo info in mm.SensorStatusList)
                {
                    if (info.SiteId == OutputSiteId)
                    {
                        if (info.LastOutput > info.OutputRecorded)
                            info.OutputRecorded = info.LastOutput;
                    }
                }
            }
             */
        }

        private String GetConsumptionAppliances(MeterManagerSettings mmSettings)
        {
            String list = "";
            foreach (MeterApplianceSettings app in (mmSettings.ApplianceList))
            {
                if (app.ConsumptionSiteId == Settings.SystemId)
                    if (list == "")
                        list = app.ApplianceNo.ToString();
                    else
                        list += ", " + app.ApplianceNo.ToString();
            }
            return list;            
        }

        private bool CheckResetRequestCount()
        {
            // delay hour tick over by 1 minute to avoid time sync issues with pvoutput
            int curHour = (int)(DateTime.Now.AddMinutes(-1)).TimeOfDay.TotalHours;
            if (curHour != RequestHour)
            {
                RequestHour = curHour;
                RequestCount = 0;
                PVOutputLimitReported = false;
                PVOutputCurrentDayLimitReported = false;
                return true;
            }
            return false;
        }
   
        private int? GetMeterId(String managerType, int instanceNo)
        {
            GenConnection ConSelect = null;
            int? id = null;

            try
            {
                ConSelect = GlobalSettings.TheDB.NewConnection();

                GenCommand cmd = new GenCommand("select Id from Meter where MeterType = @MeterType and InstanceNo = @InstanceNo ", ConSelect);
                cmd.AddParameterWithValue("@MeterType", managerType);
                cmd.AddParameterWithValue("@InstanceNo", instanceNo);
                GenDataReader reader = (GenDataReader)cmd.ExecuteReader();

                if (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                        id = reader.GetInt32(0);
                }
                reader.Close();
            }
            catch (Exception e)
            {
                LogMessage("GetMeterId", "MenagerType: " + managerType + " - Instance - " + 
                    instanceNo + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (ConSelect != null)
                {
                    ConSelect.Close();
                    ConSelect.Dispose();
                }
            }

            return id;
        }

        private void SetContext()
        {     
            /*
            if (MeterId < 0 )
            {
                MeterId = GetMeterID();
            }
            */

            if (GlobalSettings.TheDB.GenDBType == GenDBType.SQLite)
                PVDateLimit = DateTime.Today.AddDays(-(PVLiveDays));
            else
                PVDateLimit = DateTime.Today.AddDays(-(PVLiveDays - 1));
        }

        public void Dispose()
        {    
        }

        private void InsertPVOutputLog(DateTime outputDay, Int32 outputTime, long energy, long power, Double? temperature)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("InsertPVOutputLog", "Inserting: " + outputDay + " " + outputTime + " " + energy + " " + power, LogEntryType.Trace);

            GenCommand cmdIns = null;
            GenConnection con = null;

            try
            {
                bool useTemp = !Settings.UseCCTemperature;
                con = GlobalSettings.TheDB.NewConnection();
                if (useTemp)
                    cmdIns = new GenCommand(CmdInsert_Temp, con);
                else
                    cmdIns = new GenCommand(CmdInsert, con);
                cmdIns.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdIns.AddParameterWithValue("@OutputDay", outputDay);
                cmdIns.AddParameterWithValue("@OutputTime", outputTime);
                cmdIns.AddParameterWithValue("@Energy", (Double)energy);
                cmdIns.AddParameterWithValue("@Power", (Double)power);

                if (useTemp)
                    if (temperature == null)
                        cmdIns.AddParameterWithValue("@Temperature", DBNull.Value);
                    else
                        cmdIns.AddParameterWithValue("@Temperature", Math.Round(temperature.Value, 1));

                cmdIns.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "InsertPVOutputLog: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "InsertPVOutputLog: " + e.Message);
            }
            finally
            {
                if (cmdIns != null)
                    cmdIns.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void InsertPVOutputLogConsumption(DateTime outputDay, Int32 outputTime, long energy, long power, Double? temperature)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("InsertPVOutputLogConsumption", "Inserting: " + outputDay + " " + outputTime + " " + energy, LogEntryType.MeterTrace);

            GenCommand cmdInsConsume = null;
            GenConnection con = null;

            try
            {
                bool useTemp = Settings.UseCCTemperature;
                con = GlobalSettings.TheDB.NewConnection();
                if (useTemp)
                    cmdInsConsume = new GenCommand(CmdInsertConsume_Temp, con);
                else
                    cmdInsConsume = new GenCommand(CmdInsertConsume, con);
                cmdInsConsume.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdInsConsume.AddParameterWithValue("@OutputDay", outputDay);
                cmdInsConsume.AddParameterWithValue("@OutputTime", outputTime);
                cmdInsConsume.AddParameterWithValue("@Energy", (Double)energy);
                cmdInsConsume.AddParameterWithValue("@Power", (Double)power);

                if (useTemp)
                    if (temperature == null)
                        cmdInsConsume.AddParameterWithValue("@Temperature", DBNull.Value);
                    else
                        cmdInsConsume.AddParameterWithValue("@Temperature", Math.Round(temperature.Value, 1));

                cmdInsConsume.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "InsertPVOutputLogConsumption: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "InsertPVOutputLogConsumption: " + e.Message);
            }
            finally
            {
                if (cmdInsConsume != null)
                    cmdInsConsume.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void UpdatePVOutputLogConsumption(DateTime outputDay, Int32 outputTime, long energy, long power, Double? temperature)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("UpdatePVOutputLogConsumption", "Updating: " + outputDay + " " + outputTime + " " + energy, LogEntryType.MeterTrace);

            GenCommand cmdUpdConsume = null;
            GenConnection con = null;

            try
            {
                bool useTemp = Settings.UseCCTemperature;
                con = GlobalSettings.TheDB.NewConnection();
                if (useTemp)
                    cmdUpdConsume = new GenCommand(CmdUpdateConsume_Temp, con);
                else
                    cmdUpdConsume = new GenCommand(CmdUpdateConsume, con);
                cmdUpdConsume.AddParameterWithValue("@Energy", (Double)energy);
                cmdUpdConsume.AddParameterWithValue("@Power", (Double)power);

                if (useTemp)
                    if (temperature == null)
                        cmdUpdConsume.AddParameterWithValue("@Temperature", DBNull.Value);
                    else
                        cmdUpdConsume.AddParameterWithValue("@Temperature", Math.Round(temperature.Value, 1));

                cmdUpdConsume.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdUpdConsume.AddParameterWithValue("@OutputDay", outputDay);
                cmdUpdConsume.AddParameterWithValue("@OutputTime", outputTime);

                int rows = cmdUpdConsume.ExecuteNonQuery();
                if (rows != 1)
                    LogMessage("UpdatePVOutputLogConsumption", "Update rows - expected: 1 - actual: " + rows +
                        " - Day: " + outputDay + " - Time: " + outputTime, LogEntryType.ErrorMessage);
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "UpdatePVOutputLogConsumption: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "UpdatePVOutputLogConsumption: " + e.Message);
            }
            finally
            {
                if (cmdUpdConsume != null)
                    cmdUpdConsume.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void UpdatePVOutputLog(DateTime outputDay, Int32 outputTime, long energy, long power, Double? temperature)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("UpdatePVOutputLog", "Updating: " + outputDay + " " + outputTime + " " + energy + " " + power, LogEntryType.Trace);

            GenCommand cmdUpd = null;
            GenConnection con = null;

            try
            {
                bool useTemp = !Settings.UseCCTemperature;
                con = GlobalSettings.TheDB.NewConnection();
                if (useTemp)
                    cmdUpd = new GenCommand(CmdUpdate_Temp, con);
                else
                    cmdUpd = new GenCommand(CmdUpdate, con);
                cmdUpd.AddParameterWithValue("@Energy", (Double)energy);
                cmdUpd.AddParameterWithValue("@Power", (Double)power);
                cmdUpd.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdUpd.AddParameterWithValue("@OutputDay", outputDay);
                cmdUpd.AddParameterWithValue("@OutputTime", outputTime);

                if (useTemp)
                    if (temperature == null)
                        cmdUpd.AddParameterWithValue("@Temperature", DBNull.Value);
                    else
                        cmdUpd.AddParameterWithValue("@Temperature", Math.Round(temperature.Value, 1));

                int rows = cmdUpd.ExecuteNonQuery();
                if (rows != 1)
                    LogMessage("UpdatePVOutputLog", "Update rows - expected: 1 - actual: " + rows +
                        " - Day: " + outputDay + " - Time: " + outputTime, LogEntryType.ErrorMessage);
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "UpdatePVOutputLog: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "UpdatePVOutputLog: " + e.Message);
            }
            finally
            {
                if (cmdUpd != null)
                    cmdUpd.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void DeleteOldLogEntries()
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("DeleteOldLogEntries", "Deleting entries over 14 days old", LogEntryType.Trace);

            GenCommand cmdDelLog = null;
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdDelLog = new GenCommand(CmdDeleteLog, con);
                cmdDelLog.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdDelLog.AddParameterWithValue("@LimitDay", DateTime.Today.AddDays(-(PVLiveDays+1)));
                cmdDelLog.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "DeleteOldLogEntries: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "DeleteOldLogEntries: " + e.Message);
            }
            finally
            {
                if (cmdDelLog != null)
                    cmdDelLog.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void UpdatePVOutputLogLoaded(DateTime outputDay, Int32 outputTime)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("UpdatePVOutputLogLoaded", "Updating: " + outputDay + " " + outputTime, LogEntryType.Trace);

            GenCommand cmdLoadUpd = null;
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdLoadUpd = new GenCommand(CmdLoadUpdate, con);
                cmdLoadUpd.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdLoadUpd.AddParameterWithValue("@OutputDay", outputDay);
                cmdLoadUpd.AddParameterWithValue("@OutputTime", outputTime);

                int rows = cmdLoadUpd.ExecuteNonQuery();
                if (rows != 1)
                    LogMessage("UpdatePVOutputLogLoaded", "Update rows - expected: 1 - actual: " + rows +
                        " - Day: " + outputDay + " - Time: " + outputTime, LogEntryType.ErrorMessage);
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "UpdatePVOutputLogLoaded: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "UpdatePVOutputLogLoaded: " + e.Message);
            }
            finally
            {
                if (cmdLoadUpd != null)
                    cmdLoadUpd.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void PVForceLiveLoad()
        {
            ObservableCollection<PVOutputDaySettings> list = Settings.PvOutputDayList;

            GenCommand cmdFrcLoad = null;
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdFrcLoad = new GenCommand(CmdForceLoad, con);

                cmdFrcLoad.AddParameterWithValue("@SiteId", OutputSiteId);

                foreach (PVOutputDaySettings day in list)
                {
                    if (day.ForceLoad)
                    {
                        DateTime? date = day.Day;

                        if (GlobalSettings.SystemServices.LogTrace)
                            LogMessage("PVForceLiveLoad", "Updating: " + date, LogEntryType.Information);

                        try
                        {
                            if (cmdFrcLoad.Parameters.Count < 2)
                                cmdFrcLoad.AddParameterWithValue("@OutputDay", date.Value);
                            else
                                cmdFrcLoad.Parameters["@OutputDay"].Value = date.Value;

                            cmdFrcLoad.ExecuteNonQuery();
                        }
                        catch (GenException e)
                        {
                            throw new PVException(PVExceptionType.UnexpectedDBError, "PVForceLiveLoad: " + e.Message);
                        }
                        catch (Exception e)
                        {
                            throw new PVException(PVExceptionType.UnexpectedError, "PVForceLiveLoad: " + e.Message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (cmdFrcLoad != null)
                    cmdFrcLoad.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void RecordConsumption(DateTime readingTime, long energy, long power, Double? temperature)
        {
            // Check for an existing record in the pvoutputlog table. 
            // If it exists update it if the consumption energy or power values have changed.
            // If it does not exist, add the record if the consumption energy is not zero
            int timeVal = (int)readingTime.TimeOfDay.TotalSeconds;

            // LogMessage("RecordConsumption - " + readingTime + " - Energy: " + energy + " - Power: " + power, LogEntryType.Trace);

            GenCommand cmdCheck = null;
            GenConnection con = null;
            GenDataReader drCheck = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdCheck = new GenCommand(CmdCheckStr, con);
                cmdCheck.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdCheck.AddParameterWithValue("@OutputDay", readingTime.Date);
                cmdCheck.AddParameterWithValue("@OutputTime", timeVal);

                drCheck = (GenDataReader)cmdCheck.ExecuteReader();

                if (drCheck.Read())
                {
                    LogMessage("RecordConsumption", "Record found - Time: " + readingTime + " - timeVal: " + timeVal + " - Energy: " + energy + " - Power: " + power, LogEntryType.MeterTrace);
                    if (drCheck.IsDBNull(2) 
                        || ((long)Math.Round(drCheck.GetDouble(2)) != energy) 
                        || ((long)Math.Round(drCheck.GetDouble(3)) != power))
                    {
                        if (!drCheck.IsDBNull(2))
                            LogMessage("RecordConsumption", "Update - Time: " + readingTime + " - Energy: " + (long)Math.Round(drCheck.GetDouble(2)) + " - " + energy +
                                " - Percent: " + ((energy - drCheck.GetDouble(2)) / energy).ToString("P", CultureInfo.InvariantCulture) +
                                " - Power: " + (long)Math.Round(drCheck.GetDouble(3)) + " - " + power, LogEntryType.MeterTrace);
                        else
                            LogMessage("RecordConsumption", "Update - Time: " + readingTime + " - Energy: null - " + (int)(energy),
                                LogEntryType.MeterTrace);

                        UpdatePVOutputLogConsumption(readingTime.Date, timeVal, energy, power, temperature);
                    }
                }
                else
                {
                    LogMessage("RecordConsumption", "Record not found - Time: " + readingTime + " - timeVal: " + timeVal + " - Energy: " + energy + " - Power: " + power, LogEntryType.MeterTrace);
                    if (energy > 0.0) // only add new records if energy > 0
                        InsertPVOutputLogConsumption(readingTime.Date, timeVal, energy, power, temperature);
                }
            }
            catch (Exception e)
            {
                LogMessage("RecordConsumption", "Time: " + readingTime + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (drCheck != null)
                {
                    drCheck.Close();
                    drCheck.Dispose();
                }
                if (cmdCheck != null)
                    cmdCheck.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private class Days
        {
            private List<DayRecord> DayRecords;
            private PvOutputManager PvOutputManager;

            public Days(PvOutputManager pvom)
            {
                DayRecords = new List<DayRecord>();
                PvOutputManager = pvom;
            }

            private DayRecord LocateOrCreateDayRecord(DateTime readingTime)
            {
                try
                {
                    foreach (DayRecord rec in DayRecords)
                        if (rec.Day == readingTime.Date)
                            return rec;

                    DayRecord newRec = new DayRecord(readingTime, PvOutputManager);
                    DayRecords.Add(newRec);
                    return newRec;
                }
                catch (Exception e)
                {
                    PvOutputManager.LogMessage("Days.LocateOrCreateDayRecord", "readingTime: " + readingTime + 
                        " - Exception: " + e.Message, LogEntryType.MeterTrace);
                    throw new PVException(PVExceptionType.UnexpectedError, e.Message);
                }
            }

            public void RecordConsumption(int duration, DateTime readingTime, int appliance, long energy, long minPower, long maxPower, Double? temperature)
            {
                DayRecord day = LocateOrCreateDayRecord(readingTime);
                day.UpdateIntervalRecords(duration, readingTime, appliance, energy, minPower, maxPower, temperature);
            }

            public void WriteConsumption()
            {
                foreach (DayRecord rec in DayRecords)
                    rec.WriteConsumption();
            }
        }

        private class DayRecord
        {
            public DateTime Day;
            private List<IntervalRecord> IntervalRecords;
            PvOutputManager PvOutputManager;

            public DayRecord(DateTime day, PvOutputManager pvom)
            {
                Day = day.Date;
                IntervalRecords = new List<IntervalRecord>();
                PvOutputManager = pvom;
            }

            public void UpdateIntervalRecords(int duration, DateTime readingTime, int appliance, long energy, long minPower, long maxPower, Double? temperature)
            {
                String stage = "scan intervals";
                try
                {
                    IntervalRecord lastRec = null;
                    foreach (IntervalRecord rec in IntervalRecords)
                    {
                        // find last record
                        lastRec = rec;
                        // if records affected by this interval already exist (another appliance) update the existing records
                        if (rec.IntervalEnd >= readingTime)
                            rec.RecordConsumption(readingTime, appliance, energy, minPower, maxPower, temperature);
                    }

                    if (lastRec == null || lastRec.IntervalEnd < readingTime)
                    {
                        // new record required (first or only appliance)
                        stage = "new interval";
                        IntervalRecord newRec = new IntervalRecord(duration, readingTime, lastRec == null ? 0 : lastRec.Energy, PvOutputManager);
                        newRec.RecordConsumption(readingTime, appliance, energy, minPower, maxPower, temperature);
                        stage = "add interval";
                        IntervalRecords.Add(newRec);
                    }
                }
                catch (Exception e)
                {
                    PvOutputManager.LogMessage("DayRecord.UpdateIntervalRecords", "readingTime: " + readingTime + " - Stage: " + stage +
                        " - Count: " + IntervalRecords.Count + " - Exception: " + e.Message, LogEntryType.MeterTrace);
                    throw new PVException(PVExceptionType.UnexpectedError, e.Message);
                }
            }

            public void WriteConsumption()
            {
                bool useMin = true;
                foreach (IntervalRecord rec in IntervalRecords)
                {
                    rec.WriteConsumption(useMin);
                    useMin = !useMin;
                }
            }
        }
        
        private class IntervalRecord
        {
            public int Duration;
            public DateTime IntervalEnd;
            public long Energy;
            public long ThisEnergy;

            private Double? Temperature;

            private List<ApplianceRecord> Appliances;

            private PvOutputManager PvOutputManager;

            public long MinPower
            {
                get
                {
                    long power = 0;
                    foreach (ApplianceRecord rec in Appliances)
                        power += rec.MinPower;
                    return power;
                }
            }

            public long MaxPower
            {
                get
                {
                    long power = 0;
                    foreach (ApplianceRecord rec in Appliances)
                        power += rec.MaxPower;
                    return power;
                }
            }

            public IntervalRecord(int duration, DateTime readingTime, long energyStart, PvOutputManager pvom)
            {
                Duration = duration;
                IntervalEnd = readingTime;
                Energy = energyStart;
                ThisEnergy = 0;
                Temperature = null;

                Appliances = new List<ApplianceRecord>();

                PvOutputManager = pvom;
            }

            public ApplianceRecord LocateOrCreateApplianceRecord(int appliance)
            {
                try
                {
                    foreach (ApplianceRecord rec in Appliances)
                        if (rec.Appliance == appliance)
                            return rec;

                    ApplianceRecord newRec = new ApplianceRecord(appliance, PvOutputManager);
                    Appliances.Add(newRec);
                    return newRec;
                }
                catch (Exception e)
                {
                    PvOutputManager.LogMessage("IntervalRecord.LocateOrCreateApplianceRecord", "IntervalEnd: " + IntervalEnd + 
                        " - Appliance: " + appliance + " - Energy: " + Energy + " - Exception: " + e.Message, LogEntryType.MeterTrace);
                    throw new PVException(PVExceptionType.UnexpectedError, e.Message);
                }
            }

            public void RecordConsumption(DateTime readingTime, int appliance, long energy, long minPower, long maxPower, Double? temperature)
            {
                try
                {
                    if (IntervalEnd >= readingTime) // update energy on target interval and all that follow
                        Energy += energy;
                    if (IntervalEnd == readingTime)
                    {
                        // update for target interval
                        ThisEnergy += energy;

                        if (temperature.HasValue)
                            Temperature = temperature.Value;

                        ApplianceRecord appRec = LocateOrCreateApplianceRecord(appliance);
                        appRec.RecordPower(minPower, maxPower);

                        PvOutputManager.LogMessage("IntervalRecord.RecordConsumption", "Time: " + IntervalEnd + " - Appliance: " + appliance +
                                    " - Energy: " + Energy + " - Power: " + minPower + " - " + maxPower, LogEntryType.MeterTrace);
                    }
                }
                catch (PVException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    PvOutputManager.LogMessage("IntervalRecord.RecordConsumption", "IntervalEnd: " + IntervalEnd +
                        " - Appliance: " + appliance + " - Energy: " + energy + " - Exception: " + e.Message, LogEntryType.MeterTrace);
                    throw new PVException(PVExceptionType.UnexpectedError, e.Message);
                }
            }

            public void WriteConsumption(bool useMin)
            {
                if (PvOutputManager.Settings.ConsumptionPowerMinMax || Duration == 0)
                    PvOutputManager.RecordConsumption(IntervalEnd, Energy, (useMin ? MinPower : MaxPower), Temperature);
                else
                    PvOutputManager.RecordConsumption(IntervalEnd, Energy, (ThisEnergy * 3600 / Duration), Temperature);

            }
        }

        private class ApplianceRecord
        {
            public int Appliance;
            public long MinPower;
            public long MaxPower;
            private bool MinMaxUsed;

            public ApplianceRecord(int appliance, PvOutputManager pvom)
            {
                Appliance = appliance;
                MinPower = 0;
                MaxPower = 0;
                MinMaxUsed = false;
            }

            public void RecordPower(long minPower, long maxPower)
            {
                if (MinMaxUsed)
                {
                    if (MinPower > minPower)
                        MinPower = minPower;
                    if (MaxPower < maxPower)
                        MaxPower = maxPower;
                }
                else
                {
                    MinPower = minPower;
                    MaxPower = maxPower;
                    MinMaxUsed = true;
                }
            }
        }

        private DateTime CalculateIntervalEnd(DateTime time, int intervalSeconds)
        {
            return time.Date + TimeSpan.FromSeconds(intervalSeconds * ((int)((time.TimeOfDay.TotalSeconds + (intervalSeconds - 1)) / intervalSeconds)));
        }

        private void PrepareMeterLoadList()
        {
            String stage = "initial";

            try
            {
                Days days = new Days(this);
                int i = 0;
                foreach (MeterManagerSettings mmSettings in GlobalSettings.ApplicationSettings.MeterManagerList)
                {
                    if (mmSettings.Enabled)
                    { 
                        // store meter id at first pass to avoid duplicate queries
                        if (MeterIds[i] == null)
                            MeterIds[i] = GetMeterId(mmSettings.ManagerType, mmSettings.InstanceNo);
                        if (MeterIds[i] != null)
                            PrepareOneMeterLoadList(days, MeterIds[i].Value, GetConsumptionAppliances(mmSettings));
                    }
                    i++;
                }

                stage = "WriteConsumption";
                days.WriteConsumption();
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "PrepareMeterLoadList - Stage: " + stage + " - Exception: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "PrepareMeterLoadList - Stage: " + stage + " - Exception: " + e.Message);
            }
        }

        private void PrepareOneMeterLoadList(Days days, int meterId, String consumptionAppliances)
        {
            String stage = "initial";

            int intervalSeconds;
            if (Settings.DataInterval == "5")
                intervalSeconds = 5 * 60;
            else
                intervalSeconds = 10 * 60;

            // First reading ends at midnight - First reading in day at pvoutput is 12:00AM
            DateTime minStartTime = PVDateLimit.AddSeconds(0 - intervalSeconds);

            GenDataReader dr = null;
            
            DateTime curInterval = DateTime.Now;
            DateTime lastInterval;
            DateTime followingInterval = DateTime.Now;
            DateTime endTime = DateTime.Now;

            Double? temperature = null;

            GenConnection con = GlobalSettings.TheDB.NewConnection();

            String CmdMeter = 
                "select ReadingTime, Appliance, Energy * 1000, MinPower, MaxPower, Duration, Calculated * 1000, Temperature " +
                "from MeterReading " +
                "where Meter_Id = @Meter " +
                "and ReadingTime >= @DateLimit " +
                "and Appliance in ( " + consumptionAppliances + " ) " +
                "order by ReadingTime, Appliance ";

            GenCommand CmdSelMeter = new GenCommand(CmdMeter, con);
            CmdSelMeter.AddParameterWithValue("@Meter", meterId);
            CmdSelMeter.AddParameterWithValue("@DateLimit", PVDateLimit);

            try
            {
                stage = "CmdSelMeter";
                dr = (GenDataReader)CmdSelMeter.ExecuteReader();

                while (dr.Read() && ManagerManager.RunMonitors)
                {
                    stage = "extract row";
                    endTime = dr.GetDateTime(0);
                    int appliance = dr.GetInt32(1);
                    Double recEnergy = Math.Round(dr.IsDBNull(2) ? 0.0 : dr.GetDouble(2));
                    int thisMinPower = (dr.IsDBNull(3) ? 0 : dr.GetInt32(3));
                    int thisMaxPower = (dr.IsDBNull(4) ? 0 : dr.GetInt32(4));
                    int recDuration = dr.GetInt32(5);
                    Double? calculated = (dr.IsDBNull(6) ? (double?)null : Math.Round(dr.GetDouble(6)));
                    temperature = (dr.IsDBNull(7) ? (double?)null : Math.Round(dr.GetDouble(7),1));

                    if (calculated != null)
                        recEnergy = calculated.Value;

                    if (thisMinPower == 0 || thisMaxPower == 0)
                    {
                        thisMinPower = (int)(recEnergy * 3600 / recDuration);
                        thisMaxPower = thisMinPower;
                    }
                    else if (calculated != null)
                        if (recEnergy != 0.0)
                        {
                            thisMinPower = (int)(thisMinPower * calculated.Value / recEnergy);
                            thisMaxPower = (int)(thisMaxPower * calculated.Value / recEnergy);
                        }
                        else
                        {
                            thisMinPower = (int)(recEnergy * 3600 / recDuration);
                            thisMaxPower = thisMinPower;
                        }

                    DateTime startTime = endTime.AddSeconds(-recDuration);

                    LogMessage("PrepareOneMeterLoadList", "Record - Time: " + endTime + " - Energy: " + recEnergy, LogEntryType.MeterTrace);

                    // trim current record if part is outside PVDateLimit range
                    if (startTime < minStartTime)
                    {
                        stage = "trim values";
                        int newDuration = recDuration - ((int)(minStartTime - startTime).TotalSeconds);
                        double newEnergy = (recEnergy * newDuration) / recDuration;

                        recEnergy = newEnergy;
                        recDuration = newDuration;
                        startTime = minStartTime;
                    }

                    stage = "calc interval";
                    // identify intervals covered by this record
                    lastInterval = CalculateIntervalEnd(endTime, intervalSeconds);
                    curInterval = CalculateIntervalEnd(startTime + TimeSpan.FromSeconds(1), intervalSeconds);
                    DateTime nowInterval = CalculateIntervalEnd(DateTime.Now, intervalSeconds);
                    // ignore readings for or after current interval - they are too early
                    if (nowInterval <= lastInterval)
                        lastInterval = nowInterval - TimeSpan.FromSeconds(intervalSeconds);

                    DateTime prevTime = startTime;

                    // if this record covers multiple intervals write all 
                    while (curInterval <= lastInterval)
                    {
                        stage = "loop interval";
                        int duration;

                        if (curInterval < endTime)
                        {
                            duration = (int)(curInterval - prevTime).TotalSeconds;
                            prevTime = curInterval;
                        }
                        else
                        {
                            duration = (int)(endTime - prevTime).TotalSeconds;
                            prevTime = endTime;
                        }

                        double curEnergy = recEnergy * duration / recDuration;

                        stage = "RecordConsumption";
                        days.RecordConsumption(intervalSeconds, curInterval, appliance, (long)Math.Round(curEnergy), thisMinPower, thisMaxPower, temperature);
                        
                        curInterval += TimeSpan.FromSeconds(intervalSeconds);
                    }
                }
                dr.Close();
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "PrepareOneMeterLoadList - Stage: " + stage + " - Exception: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "PrepareOneMeterLoadList - Stage: " + stage + " - Exception: " + e.Message);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();

                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void RecordYield(DateTime day, Int32 time, long energy, long power, Double intervalEnergy, Double? temperature)
        {
            // Check for an existing record in the pvoutputlog table. 
            // If it exists update it if the yield energy or power values have changed.
            // If it does not exist, add the record if the yield energy is not zero

            GenCommand cmdCheck = null;
            GenConnection con = null;
            GenDataReader drCheck = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdCheck = new GenCommand(CmdCheckStr, con);
                cmdCheck.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdCheck.AddParameterWithValue("@OutputDay", day);
                cmdCheck.AddParameterWithValue("@OutputTime", time);

                //LogMessage("check params ok", LogEntryType.Information);

                drCheck = (GenDataReader)cmdCheck.ExecuteReader();
                bool update = false;
                bool insert = false;

                if (drCheck.Read())
                {
                    if (drCheck.IsDBNull(0) 
                        || (((long)Math.Round(drCheck.GetDouble(0))) != energy) 
                        || (((long)Math.Round(drCheck.GetDouble(1))) != power))
                    {
                        if (!drCheck.IsDBNull(0))
                            LogMessage("RecordYield", "Energy: " + (long)Math.Round(drCheck.GetDouble(0)) + " - " + energy +
                                " - Power: " + (long)Math.Round(drCheck.GetDouble(1)) + " - " + power, LogEntryType.Trace);
                        else
                            LogMessage("RecordYield", "Energy: null - " + energy, LogEntryType.Trace);

                        update = true;
                    }
                }
                else if (intervalEnergy > 0.0) // only add new records if energy > 0
                    insert = true;

                drCheck.Close();
                drCheck.Dispose();
                drCheck = null;
                con.Close();
                con.Dispose();
                con = null;

                if (insert)
                    InsertPVOutputLog(day, time, energy, power, temperature);
                else if (update)
                    UpdatePVOutputLog(day, time, energy, power, temperature);
            }
            catch (Exception e)
            {
                LogMessage("RecordYield", "Date: " + day + " - Time: " + time + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (drCheck != null)
                {
                    drCheck.Close();
                    drCheck.Dispose();
                }

                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private void PrepareLoadList()
        {
            long energy = 0;
            DateTime day = DateTime.Today;
            bool first = true;
            GenDataReader dr = null;

            GenCommand cmd = null;
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();

                if (Settings.DataInterval == "5")
                {
                    cmd = new GenCommand(Cmd5MinStr, con);
                }
                else
                {
                    cmd = new GenCommand(CmdStr, con);
                }
                cmd.AddParameterWithValue("@SiteId", OutputSiteId);
                cmd.AddParameterWithValue("@DateLimit", PVDateLimit);
                
                dr = (GenDataReader)cmd.ExecuteReader();
                
                while (dr.Read() && ManagerManager.RunMonitors)
                {
                    if (first || (day != dr.GetDateTime(0)))
                    {
                        first = false;
                        day = dr.GetDateTime(0);
                        //LogMessage("dr0 datetime ok", LogEntryType.Information);
                        energy = 0;
                    }

                    long power;

                    //LogMessage("dr1 type " + dr.GetFieldType(1), LogEntryType.Information);

                    // MS Access has decided to return integers as a Double !!!
                    int timeVal;
                    if (dr.GetFieldType(1).ToString() == "System.Double")
                        timeVal = (int)(dr.GetDouble(1));
                    else
                        timeVal = dr.GetInt32(1);
                    
                    //LogMessage("dr1 int32 0 ok", LogEntryType.Information);

                    // do not have instantaneous power - do have min and max power 
                    // values averaged over a 10 minute period - alternate between the two
                    if (((int)((int)(timeVal / 600) % 2)) == 0)
                    {
                        //LogMessage("dr1 int32 1 ok", LogEntryType.Information);
                        if (dr.IsDBNull(5))
                            power = (long)Math.Round(dr.GetDouble(3));    // simulated max power based on source interval values
                        else
                            power = (long)Math.Round(dr.GetDouble(5));    // actual max power from devices with instaneous power readings
                        //LogMessage("dr3 double ok", LogEntryType.Information);
                    }
                    else
                    {
                        //LogMessage("dr1 int32 2 ok", LogEntryType.Information);
                        if (dr.IsDBNull(6))
                            power = (long)Math.Round(dr.GetDouble(4));    // simulated min power based on source interval values
                        else
                            power = (long)Math.Round(dr.GetDouble(6));    // actual min power from devices with instaneous power readings
                        //LogMessage("dr4 double ok", LogEntryType.Information);
                    }

                    energy += (long)Math.Round(dr.GetDouble(2));
                    //LogMessage("dr2 double ok", LogEntryType.Information);

                    Double? temperature;
                    if (dr.IsDBNull(7))
                        temperature = null;   
                    else
                        temperature = dr.GetDouble(7);

                    RecordYield(day, timeVal, energy, power, dr.GetDouble(2), temperature);
                }
                dr.Close();

                if (ManagerManager.RunMonitors
                && (LastDeleteOld == null || LastDeleteOld.Value < DateTime.Now.AddDays(-1.0)))
                {
                    DeleteOldLogEntries();
                    LastDeleteOld = DateTime.Now;
                }
            }
            catch (PVException e)
            {
                throw e;
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "PrepareLoadList: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "PrepareLoadList: " + e.Message);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();

                if (cmd != null)
                    cmd.Dispose();

                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private bool UpdateBatchSuccess(String response)
        {
            int pos = 0;
            bool updated = false;
            int count = 0;

            try
            {
                DateTime statusDate;
                TimeSpan statusTime;
                CultureInfo provider = CultureInfo.InvariantCulture;

                String subStr;

                while ((pos+15) <= response.Length) // stop when no more full result messages left
                {
                    subStr = response.Substring(pos, 8);
                    statusDate = DateTime.ParseExact(subStr, "yyyyMMdd", provider);

                    pos += 9; // move past date and comma
                    subStr = response.Substring(pos, 5);
                    statusTime = TimeSpan.ParseExact(subStr, @"hh\:mm", provider);

                    pos += 6; // move past time and comma
                    char ch = response[pos];

                    if (ch == '0' || ch == '1' || ch == '2')
                    {
                        UpdatePVOutputLogLoaded(statusDate, (Int32)statusTime.TotalSeconds);
                        updated = true;
                        count++;
                    }
                    else
                        LogMessage("UpdateBatchSuccess", "Update rejected  Date: " + statusDate + " : Time: " + statusTime + " : " + statusTime.TotalSeconds, LogEntryType.Information);

                    pos += 2; // move past result code and ;
                }
            }
            catch (Exception e)
            {
                LogMessage("UpdateBatchSuccess", "Response: " + response + " : Pos: " + pos +
                    " Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        
            int adjustCount;
            if (Settings.APIVersion == "r1")
                adjustCount = ((count + PVOutputr1Multiple - 1) / PVOutputr1Multiple);
            else
                adjustCount = ((count + PVOutputr2Multiple - 1) / PVOutputr2Multiple);
            
            RequestCount += adjustCount;

            return updated;
        }

        private bool SendPVOutputBatch(String batchMessage, int size, int hourAvail)
        {
            String postData = "";
            String serverResponse = "";

            try
            {
                CheckResetRequestCount();

                String version = Settings.APIVersion;

                if (version == "" || version == null)
                    version = "r2";

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create("http://pvoutput.org/service/" + version + "/addbatchstatus.jsp ");
                request.ProtocolVersion = HttpVersion.Version10;
                request.KeepAlive = false;
                request.SendChunked = false;
                request.Method = "POST";
            
                postData = "data=" + batchMessage;

                LogMessage("SendPVOutputBatch", "Size: " + size + " - Avail: " + hourAvail + "postData: " + postData, LogEntryType.Trace);

                byte[] byteArray = Encoding.UTF8.GetBytes(postData);

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                request.Headers.Add("X-Pvoutput-Apikey:" + APIKey);
                request.Headers.Add("X-Pvoutput-SystemId:" + OutputSiteId);
                request.UserAgent = "PVBC/" + GlobalSettings.ApplicationSettings.ApplicationVersion + " (SystemId:" + OutputSiteId + ")";

                Stream dataStream = request.GetRequestStream();

                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                dataStream = response.GetResponseStream();

                StreamReader reader = new StreamReader(dataStream);

                serverResponse = reader.ReadToEnd();

                LogMessage("SendPVOutputBatch", "serverResponse: " + serverResponse, LogEntryType.Trace);

                reader.Close();
                dataStream.Close();
                response.Close();

                ErrorReported = false;
            }
            catch (Exception e)
            {
                if (e is WebException && ((WebException)e).Status == WebExceptionStatus.ProtocolError)
                {
                    WebResponse errResp = ((WebException)e).Response;
                    using (Stream respStream = errResp.GetResponseStream())
                    {
                        StreamReader ereader = new StreamReader(respStream);
                        String errorResponse = ereader.ReadToEnd();
                        if (!PVOutputLimitReported)
                            LogMessage("SendPVOutputBatch", "Protocol Error: " + postData +
                                " Error: " + errorResponse, LogEntryType.Information);
                        ereader.Close();
                        respStream.Close();

                        PVOutputLimitReported = true;
                        RequestCount = PVOutputHourLimit;
                    }
                }
                else if (!ErrorReported)
                {
                    LogMessage("SendPVOutputBatch", "Server Error - Sid: " + OutputSiteId + " - Message: " + postData +
                        " - Error: " + e.Message, LogEntryType.ErrorMessage);
                    ErrorReported = true;
                }
            }

            if (serverResponse != "")
                return UpdateBatchSuccess(serverResponse);
            else
                return false;
        }

        private bool LoadPVOutputBatch()
        {
            GenDataReader dr = null;
            bool logRequired = false;
            int messageStatusCount = 0;
            String postData = "";
            ErrorReported = false;
            DateTime lastTime = DateTime.Now;

            int messageLimit;
            int availRequests;

            if (Settings.APIVersion == "r1")
                messageLimit = PVOutputr1Size;
            else
                messageLimit = PVOutputr2Size;

            GenCommand cmdLoadSel = null;
            GenConnection con = null;

            bool complete = true;
            int sentCount = 0;

            try
            {
                if (Settings.APIVersion == "r1")
                    availRequests = (PVOutputHourLimit - RequestCount) * PVOutputr1Multiple;    
                else
                    availRequests = (PVOutputHourLimit - RequestCount) * PVOutputr2Multiple;

                con = GlobalSettings.TheDB.NewConnection();
                cmdLoadSel = new GenCommand(CmdLoadSelect, con);
                cmdLoadSel.AddParameterWithValue("@SiteId", OutputSiteId);
                cmdLoadSel.AddParameterWithValue("@FirstDay", PVDateLimit);

                dr = (GenDataReader)cmdLoadSel.ExecuteReader();

                DateTime prevDate = DateTime.Today;

                while (dr.Read() && ManagerManager.RunMonitors)
                {
                    DateTime date = dr.GetDateTime(1).Date;

                    if (messageStatusCount == messageLimit 
                        || (messageStatusCount > 0 && date != prevDate) // force new batch at date change - pvoutput day total update requirement
                        || (messageStatusCount >= availRequests && messageStatusCount > 0))
                    {
                        // pvoutput enforces 1 per second now

                        int sleep = PVOutputDelay - (int)((DateTime.Now - lastTime).TotalMilliseconds);
                        if (sleep > 0)
                            Thread.Sleep(sleep);
                        if (SendPVOutputBatch(postData, messageStatusCount, availRequests))
                            logRequired = true;
                        else
                        {
                            // error encountered exit with incomplete status
                            messageStatusCount = 0;
                            complete = false;
                            break;
                        }
                        lastTime = DateTime.Now;
                        availRequests -= messageStatusCount;
                        sentCount += messageStatusCount;
                        messageStatusCount = 0;
                        postData = "";
                    }

                    prevDate = date;

                    if (RequestCount >= PVOutputHourLimit)
                    {
                        // hour quota exhausted - exit with incomplete status
                        if (!PVOutputLimitReported)
                        {
                            LogMessage("LoadPVOutputBatch", "Reached pvoutput request limit - pending updates delayed", LogEntryType.Information);
                            PVOutputLimitReported = true;
                        }
                        complete = false;
                        break;
                    }

                    int hourUpdatesRequired = (60 - (int)DateTime.Now.Minute) / 5;
                    if (RequestCount >= (PVOutputHourLimit - hourUpdatesRequired))
                    {
                        // approaching hour quota - only process data for today
                        if (date != DateTime.Today)
                        {
                            if (!PVOutputCurrentDayLimitReported)
                            {
                                LogMessage("LoadPVOutputBatch", "Reached pvoutput request limit - pending updates delayed", LogEntryType.Information);
                                PVOutputCurrentDayLimitReported = true;
                            }
                            complete = false;
                            continue;
                        }
                    }

                    if (messageStatusCount > 0)
                        postData += ";";

                    postData += dr.GetDateTime(1).ToString("yyyyMMdd") +
                            "," + TimeSpan.FromSeconds(dr.GetInt32(2)).ToString(@"hh\:mm");

                    if (Settings.UploadYield)
                        if (dr.IsDBNull(3)) // is energy generated null
                            postData += ",,";
                        else
                            postData += "," + ((Int32)dr.GetDouble(3)).ToString() + "," + ((Int32)dr.GetDouble(4)).ToString();
                    else
                        postData += ",-1,-1";  // causes pvoutput to ignore yield (no overwrite)

                    if (Settings.UploadConsumption)
                        if (dr.IsDBNull(5)) // is energy consumed null
                            postData += ",,";
                        else
                            postData +=
                            "," + ((Int32)dr.GetDouble(5)).ToString() + "," + ((Int32)dr.GetDouble(6)).ToString();
                    else
                        postData += ",-1,-1";  // causes pvoutput to ignore consumption (no overwrite)                      

                    if (Settings.APIVersion != "r1")
                        if (!dr.IsDBNull(7)) // is temperature imported null 
                            postData +=
                            "," + (dr.GetDouble(7)).ToString("F");

                    messageStatusCount++;
                }

                dr.Close();

                if (messageStatusCount > 0)
                {
                    // pvoutput enforces 1 per second now
                    int sleep = PVOutputDelay - (int)((DateTime.Now - lastTime).TotalMilliseconds);
                    if (sleep > 0)
                        Thread.Sleep(sleep);
                    if (SendPVOutputBatch(postData, messageStatusCount, availRequests))
                        logRequired = true;

                    sentCount += messageStatusCount;
                }
            }
            catch (PVException e)
            {
                throw e;
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "LoadPVOutputBatch: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "LoadPVOutputBatch: " + e.Message);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();
                            
                if (cmdLoadSel != null)
                    cmdLoadSel.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }

            if (logRequired)
            {
                LogMessage("LoadPVOutputBatch", "pvoutput.org batch updated - DataPoints: " + sentCount + 
                    " - Hour Total: " + RequestCount + " - Limit: " + PVOutputHourLimit, LogEntryType.Information);
            }
            return complete;
        }

        private DateTime? GetOldestDay()
        {
            GenDataReader dr = null;
            DateTime? oldestDay = null;

            GenCommand cmdSelOldestDay = null;
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdSelOldestDay = new GenCommand(CmdSelectOldestDay, con);
                dr = (GenDataReader)cmdSelOldestDay.ExecuteReader();

                if (dr.Read())
                {
                    oldestDay = dr.IsDBNull(0) ? (DateTime?)null : dr.GetDateTime(0);
                }

                dr.Close();
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "GetOldestDay: " + e.Message);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "GetOldestDay: " + e.Message);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();

                if (cmdSelOldestDay != null)
                    cmdSelOldestDay.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }

            return oldestDay;
        }

        private void BackloadMissingDay(DateTime missingDay, Double outputKwh)
        {
            CheckResetRequestCount();

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create("http://pvoutput.org/service/r1/addoutput.jsp ");
            request.ProtocolVersion = HttpVersion.Version10;
            request.KeepAlive = false;
            request.SendChunked = false;
            request.Method = "POST";

            // today and last 6 days are autoupdated by live update
            String postData = "d=" + missingDay.Date.ToString("yyyyMMdd") + "&g=" + (outputKwh*1000).ToString();

            LogMessage("BackloadMissingDay", "Upload day: postData: " + postData, LogEntryType.Trace);

            byte[] byteArray = Encoding.UTF8.GetBytes(postData);

            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;

            request.Headers.Add("X-Pvoutput-Apikey:" + APIKey);
            request.Headers.Add("X-Pvoutput-SystemId:" + OutputSiteId);

            Stream dataStream = request.GetRequestStream();

            String serverResponse = "";

            try
            {
                dataStream.Write(byteArray, 0, byteArray.Length);
                dataStream.Close();

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                dataStream = response.GetResponseStream();

                StreamReader reader = new StreamReader(dataStream);

                serverResponse = reader.ReadToEnd();

                reader.Close();
                dataStream.Close();
                response.Close();

                ErrorReported = false;
            }
            catch (Exception e)
            {
                if (e is WebException && ((WebException)e).Status == WebExceptionStatus.ProtocolError)
                {
                    WebResponse errResp = ((WebException)e).Response;
                    using (Stream respStream = errResp.GetResponseStream())
                    {
                        StreamReader ereader = new StreamReader(respStream);
                        String errorResponse = ereader.ReadToEnd();
                        LogMessage("BackloadMissingDay", "Protocol Error: " + postData +
                            " Error: " + errorResponse, LogEntryType.Information);
                        ereader.Close();
                        respStream.Close();
                    }
                }
                else if (!ErrorReported)
                {
                    LogMessage("BackloadMissingDay", "Server Error: API Key: " + APIKey + " - Sid: " + OutputSiteId + " - Message: " + postData +
                        " - Error: " + e.Message, LogEntryType.ErrorMessage);
                    ErrorReported = true;
                }
            }
            RequestCount++;
        }

        private void BackloadMissingDays(String dayList)
        {
            DateTime lastTime = DateTime.Now;
            int pos = 0;
            GenDataReader dr = null;
            CultureInfo provider = CultureInfo.InvariantCulture;

            LogMessage("BackloadMissingDays", "dayList: " + dayList, LogEntryType.Trace);

            GenCommand cmdSelDayOutput = null;
            GenConnection con = null;

            bool hasParams = false;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdSelDayOutput = new GenCommand(CmdSelectDayOutput, con);

                while (pos <= (dayList.Length - 8) && ManagerManager.RunMonitors)
                {
                    String dateString = dayList.Substring(pos, 8);
                    pos += 9;
                    ErrorReported = false;

                    try
                    {
                        DateTime missingDay = DateTime.ParseExact(dateString, "yyyyMMdd", provider);

                        if (hasParams)
                        {
                            cmdSelDayOutput.Parameters["@OutputDay"].Value = missingDay;
                            cmdSelDayOutput.Parameters["@NextOutputDay"].Value = missingDay.AddDays(1);
                        }
                        else
                        {
                            cmdSelDayOutput.AddParameterWithValue("@OutputDay", missingDay);
                            cmdSelDayOutput.AddParameterWithValue("@NextOutputDay", missingDay.AddDays(1));
                            hasParams = true;
                        }

                        dr = (GenDataReader)cmdSelDayOutput.ExecuteReader();

                        bool valueFound = false;
                        if (dr.Read())
                        {
                            Type tp = dr.GetFieldType(0);
                            String tpStr = tp.ToString();
                            if (!dr.IsDBNull(0))
                            {
                                int delay = PVOutputDelay - (int)((DateTime.Now - lastTime).TotalMilliseconds);
                                if (delay > 0)
                                    Thread.Sleep(delay);
                                BackloadMissingDay(missingDay, dr.GetDouble(0));
                                valueFound = true;
                                lastTime = DateTime.Now;
                            }
                        }

                        if (!valueFound)
                            LogMessage("BackloadMissingDays", "Day not found in database: " + missingDay, LogEntryType.Information);

                        dr.Close();
                    }
                    catch (GenException e)
                    {
                        throw new PVException(PVExceptionType.UnexpectedDBError, "BackloadMissingDays: " + e.Message);
                    }
                    catch (Exception e)
                    {
                        throw new PVException(PVExceptionType.UnexpectedError, "BackloadMissingDays: " + e.Message);
                    }
                    finally
                    {
                        if (dr != null)
                            dr.Dispose();
                    }
                }
            }
            finally 
            {
                if (cmdSelDayOutput != null)
                    cmdSelDayOutput.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        public void BackloadPVOutput()
        {  
            DateTime? oldestDay = GetOldestDay();
            DateTime maxDay = DateTime.Today.AddDays(-14);

            // last 14 days are autoupdated by live update
            if (oldestDay == null || oldestDay > maxDay)
            {
                LogMessage("BackloadPVOutput", "No suitable days in database: oldestDay: " + oldestDay + " : maxDay : " + maxDay, LogEntryType.Trace);
                return;
            }

            String requestData = "key=" + APIKey + "&sid=" + OutputSiteId +
                "&df=" + oldestDay.Value.Date.ToString("yyyyMMdd") + "&dt=" + maxDay.ToString("yyyyMMdd");
            LogMessage("BackloadPVOutput", "Get missing days - requestData: " + requestData, LogEntryType.Trace);

            WebRequest request = WebRequest.Create("http://pvoutput.org/service/r1/getmissing.jsp" + "?" + requestData);
            request.Method = "GET";

            String serverResponse = "";

            try
            {
                WebResponse response = request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                serverResponse = reader.ReadToEnd();
                //serverResponse = "20110204";

                reader.Close();
                dataStream.Close();
                response.Close();
            }
            catch (Exception e)
            {
                LogMessage("BackloadPVOutput", "Server Error: API Key:" + APIKey + " Sid:" + OutputSiteId + "Message:" + requestData +
                    " Error: " + e.Message, LogEntryType.Information);

                if (e is WebException && ((WebException)e).Status == WebExceptionStatus.ProtocolError)
                {
                    WebResponse errResp = ((WebException)e).Response;
                    using (Stream respStream = errResp.GetResponseStream())
                    {
                        StreamReader ereader = new StreamReader(respStream);
                        String errorResponse = ereader.ReadToEnd();
                        LogMessage("BackloadPVOutput", "Protocol Error: " + errorResponse, LogEntryType.Information);
                        ereader.Close();
                        respStream.Close();
                    }
                }
            }

            if (serverResponse != "")
                BackloadMissingDays(serverResponse);
            else
                LogMessage("BackloadPVOutput", "Empty missing day response from pvoutput.org", LogEntryType.Trace);            
        }

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(0.0); } }

        public override TimeSpan InitialPause { get { return TimeSpan.FromSeconds(10.0); } }

        public override void Initialise()
        {
            base.Initialise();
            PVForceLiveLoad();
            LogMessage("Initialise", "pvoutput.org update started", LogEntryType.StatusChange);
        }

        private void UpdateToSingleInverterSiteId(GenDatabase db)
        {
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();

                String cmdStr =
                    "update inverter set SiteId = @SiteId " +
                    "where SiteId is null or SiteId <> @SiteId ";

                GenCommand cmd = new GenCommand(cmdStr, con);
                cmd.AddParameterWithValue("@SiteId", OutputSiteId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                LogMessage("UpdateInverterSiteId", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        public override bool DoWork()
        {
            const int maxCount = 4;
            bool cycle = true;

            try
            {
                //check for shutdown every 5 seconds
                // if previous attempts did not complete, try again after 3 minutes
                bool isReady = OutputReadyEvent.WaitOne(TimeSpan.FromSeconds(5));
                OutputReadyEvent.Reset();

                lock(OutputProcessLock) // ensure only one PVOutputManager does this at any one time
                {
                    OutputReady outputReady = OutputReady.NotReady;

                    if (isReady) // energy event has been recorded relevant to this PVOutput System
                        isReady = ((outputReady = CheckOutputReady()) != OutputReady.NotReady); // check that all requirements are met

                    if (isReady || InitialOutputCycle
                    || (!Complete && ((DateTime.Now - IncompleteTime.Value) >= TimeSpan.FromMinutes(3.0))))
                    {
                        LogMessage("DoWork", "Running update", LogEntryType.Trace);

                        GlobalSettings.SystemServices.GetDatabaseMutex();
                        try
                        {
                            if (GlobalSettings.ApplicationSettings.PvOutputSystemList.Count == 1
                            && LastInverterCheck.AddMinutes(5.0) < DateTime.Now)
                            {
                                UpdateToSingleInverterSiteId(GlobalSettings.TheDB);
                                LastInverterCheck = DateTime.Now;
                            }

                            CheckResetRequestCount();

                            if (!PVOutputLimitReported)
                            {
                                // Adjust the queries to reflect currect time and settings
                                SetContext();

                                if (Settings.AutoBackload && BackloadCount < maxCount)
                                {
                                    LogMessage("DoWork", "Auto Backload starting - " + (BackloadCount + 1) + " of " + maxCount, LogEntryType.Trace);
                                    BackloadPVOutput();
                                    BackloadCount++;
                                    LogMessage("DoWork", "Auto Backload completed", LogEntryType.Information);
                                }

                                if (ManagerManager.RunMonitors)
                                {
                                    if (outputReady == OutputReady.YieldReady || outputReady == OutputReady.BothReady || InitialOutputCycle)
                                    {
                                        LogMessage("DoWork", "Preparing Yield Load List", LogEntryType.Trace);
                                        PrepareLoadList();
                                    }
                                    if (outputReady == OutputReady.ConsumeReady || outputReady == OutputReady.BothReady || InitialOutputCycle)
                                    {
                                        LogMessage("DoWork", "Preparing Consumption Load List", LogEntryType.Trace);
                                        PrepareMeterLoadList();
                                    }

                                    LogMessage("DoWork", "Running LoadPVOutputBatch", LogEntryType.Trace);
                                    Complete = LoadPVOutputBatch();

                                    SetOutputRecorded();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            LogMessage("DoWork", "Exception performing update: " + e.Message, LogEntryType.ErrorMessage);
                            cycle = false;
                        }
                        finally
                        {
                            // Incomplete time reset after every attempt
                            if (Complete)
                                IncompleteTime = null;
                            else if (IncompleteTime == null)
                                IncompleteTime = DateTime.Now;

                            GlobalSettings.SystemServices.ReleaseDatabaseMutex();

                            InitialOutputCycle = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogMessage("DoWork", "Exception monitoring: " + e.Message, LogEntryType.ErrorMessage);
                cycle = false;
            }
            return cycle;
        }

        public override void Finalise()
        {
            LogMessage("Finalise", "pvoutput.org update stopped", LogEntryType.StatusChange);
            base.Finalise();
        }
    }
}
