/*
* Copyright (c) 2010, 2013 Dennis Mackay-Fisher
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GenThreadManagement;
using GenericConnector;
using MackayFisher.Utilities;
using DeviceControl;
using Device;
using DeviceDataRecorders;
using PVBCInterfaces;
using PVSettings;

namespace OutputManagers
{
    public interface IOutputManagerManager
    {
        bool LiveLoadForced { get; }
        void ReleaseErrorLoggers();
        bool RunMonitors { get; }
        void StartService(bool fullStartup);
        void StopService();
        IEvents EnergyEvents { get; }
        List<PVOutputManager> RunningOutputManagers { get; }
        Device.EnergyConsolidationDevice FindPVOutputConsolidationDevice(String systemId);
    }

    public class PVOutputManager : GenThread
    {
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

        private const String CmdSelectOldestDay = "select min(ReadingEnd) from devicereading_energy";

        // caution - the following query was one of few I could get to work with SQLite
        // SQLite date handling is pure crap and totally incompatible with any other version of SQL I have ever seen!!!
        // I could not get it to work at all with a view. I was forced to write the query against a base table
        private const String CmdSelectDayOutput = "select SUM(OutputKwh) " +
            "from outputhistory " +
            "where OutputTime >= @OutputDay and OutputTime < @NextOutputDay";

        public override String ThreadName { get { return "PVOutput/" + Settings.SystemId; } }

        private PvOutputSiteSettings Settings;

        public String SystemId { get; private set; }
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

        private int RequestCount;
        private int RequestHour;
        private bool PVOutputLimitReported;
        private bool PVOutputCurrentDayLimitReported;

        private int BackloadCount = 0;
        private bool Complete;
        private DateTime? IncompleteTime;
        private bool InitialOutputCycle;

        DateTime? LastDeleteOld = null;

        private DateTime LastYieldReady;
        private DateTime LastConsumeReady;
        private int PVInterval;

        private IOutputManagerManager ManagerManager;

        public ManualResetEvent OutputReadyEvent { get; private set; }

        private static Object OutputProcessLock;

        private Device.EnergyConsolidationDevice ConsolidationDevice = null;

        private void LogMessage(String routine, String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage(routine, message, logEntryType);
        }

        public PVOutputManager(GenThreadManager genThreadManager, IOutputManagerManager managerManager, PvOutputSiteSettings settings)
            : base(genThreadManager, GlobalSettings.SystemServices)
        {
            ManagerManager = managerManager;
            Settings = settings;
            SystemId = settings.SystemId;
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

            OutputReadyEvent = new ManualResetEvent(true);
            LastYieldReady = DateTime.MinValue;
            LastConsumeReady = DateTime.MinValue;
            PVInterval = settings.DataInterval == "" ? 10 : Convert.ToInt32(settings.DataInterval);
            // Common lock object for all OutputManagers
            if (OutputProcessLock == null)
                OutputProcessLock = new Object();
            ConsolidationDevice = ManagerManager.FindPVOutputConsolidationDevice(SystemId);
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
                cmdLoadUpd.AddParameterWithValue("@SiteId", SystemId);
                cmdLoadUpd.AddParameterWithValue("@OutputDay", outputDay);
                cmdLoadUpd.AddParameterWithValue("@OutputTime", outputTime);

                int rows = cmdLoadUpd.ExecuteNonQuery();
                if (rows != 1)
                    LogMessage("UpdatePVOutputLogLoaded", "Update rows - expected: 1 - actual: " + rows +
                        " - Day: " + outputDay + " - Time: " + outputTime, LogEntryType.ErrorMessage);
            }
            catch (GenException e)
            {
                throw new Exception("UpdatePVOutputLogLoaded - Database Error: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("UpdatePVOutputLogLoaded - Error : " + e.Message, e);
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
                cmdIns.AddParameterWithValue("@SiteId", SystemId);
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
                throw new Exception("InsertPVOutputLog - Database Error: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("InsertPVOutputLog - Error : " + e.Message, e);
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
                cmdInsConsume.AddParameterWithValue("@SiteId", SystemId);
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
                throw new Exception("InsertPVOutputLogConsumption - Database Error: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("InsertPVOutputLogConsumption - Error : " + e.Message, e);
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

                cmdUpdConsume.AddParameterWithValue("@SiteId", SystemId);
                cmdUpdConsume.AddParameterWithValue("@OutputDay", outputDay);
                cmdUpdConsume.AddParameterWithValue("@OutputTime", outputTime);

                int rows = cmdUpdConsume.ExecuteNonQuery();
                if (rows != 1)
                    LogMessage("UpdatePVOutputLogConsumption", "Update rows - expected: 1 - actual: " + rows +
                        " - Day: " + outputDay + " - Time: " + outputTime, LogEntryType.ErrorMessage);
            }
            catch (GenException e)
            {
                throw new Exception("UpdatePVOutputLogConsumption - Database Error: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("UpdatePVOutputLogConsumption - Error : " + e.Message, e);
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
                cmdUpd.AddParameterWithValue("@SiteId", SystemId);
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
                throw new Exception("UpdatePVOutputLog - Database Error: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("UpdatePVOutputLog - Error : " + e.Message, e);
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
                cmdDelLog.AddParameterWithValue("@SiteId", SystemId);
                cmdDelLog.AddParameterWithValue("@LimitDay", DateTime.Today.AddDays(-(PVLiveDays + 1)));
                cmdDelLog.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                throw new Exception("DeleteOldLogEntries - Database Error: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("DeleteOldLogEntries - Error : " + e.Message, e);
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

        private void RecordYield(DateTime readingTime, int timeVal, long energy, long power, bool intervalHasEnergy, Double? temperature)
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
                cmdCheck.AddParameterWithValue("@SiteId", SystemId);
                cmdCheck.AddParameterWithValue("@OutputDay", readingTime.Date);
                cmdCheck.AddParameterWithValue("@OutputTime", timeVal);

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
                else if (intervalHasEnergy) // only add new records if energy > 0
                    insert = true;

                drCheck.Close();
                drCheck.Dispose();
                drCheck = null;
                con.Close();
                con.Dispose();
                con = null;

                if (insert)
                    InsertPVOutputLog(readingTime.Date, timeVal, energy, power, temperature);
                else if (update)
                    UpdatePVOutputLog(readingTime.Date, timeVal, energy, power, temperature);
            }
            catch (Exception e)
            {
                LogMessage("RecordYield", "Time: " + readingTime + " - timeVal: " + timeVal + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
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

        private void PrepareYieldLoadList()
        {            
            try
            {
                if (ConsolidationDevice == null)
                {
                    ConsolidationDevice = ManagerManager.FindPVOutputConsolidationDevice(SystemId);
                    if (ConsolidationDevice == null)
                    {
                        GlobalSettings.LogMessage("PrepareYieldLoadList", "Cannot find ConsolidationDevice for: " + SystemId, LogEntryType.Information);
                        return;
                    }
                }

                for(DateTime day = DateTime.Today; day >= PVDateLimit; day = day.AddDays(-1.0))
                {
                    DeviceDetailPeriod_EnergyConsolidation yieldPeriod = (DeviceDetailPeriod_EnergyConsolidation) ConsolidationDevice.FindOrCreateFeaturePeriod(FeatureType.YieldAC, 0, day);

                    long energy = 0;
                    long power = 0;

                    foreach (EnergyReading reading in yieldPeriod.GetReadings())
                    {
                        int timeVal = (int)reading.ReadingEnd.TimeOfDay.TotalSeconds;

                        // do not have instantaneous power - do have min and max power 
                        // values averaged over a 10 minute period - alternate between the two
                        if (((int)((int)(timeVal / 600) % 2)) == 0)                        
                            power = reading.MaxPower.HasValue ? reading.MaxPower.Value : reading.Power.HasValue ? reading.Power.Value : reading.AveragePower;  
                        else
                            power = reading.MinPower.HasValue ? reading.MaxPower.Value : reading.Power.HasValue ? reading.Power.Value : reading.AveragePower;  
                        
                        energy += (long)(reading.EnergyDelta * 1000.0);
                        RecordYield(reading.ReadingEnd, timeVal, energy, power, reading.EnergyDelta > 0.0, reading.Temperature);
                    }
                }

                if (LastDeleteOld == null || LastDeleteOld.Value < DateTime.Now.AddDays(-1.0))
                {
                    DeleteOldLogEntries();
                    LastDeleteOld = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                throw new Exception("PrepareYieldLoadList - Error : " + e.Message, e);
            }           
        }

        private void RecordConsumption(DateTime readingTime, int timeVal, long energy, long power, Double? temperature)
        {
            // Check for an existing record in the pvoutputlog table. 
            // If it exists update it if the consumption energy or power values have changed.
            // If it does not exist, add the record if the consumption energy is not zero
            

            // LogMessage("RecordConsumption - " + readingTime + " - Energy: " + energy + " - Power: " + power, LogEntryType.Trace);

            GenCommand cmdCheck = null;
            GenConnection con = null;
            GenDataReader drCheck = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdCheck = new GenCommand(CmdCheckStr, con);
                cmdCheck.AddParameterWithValue("@SiteId", SystemId);
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

        private void PrepareConsumptionLoadList()
        {
            try
            {
                if (ConsolidationDevice == null)
                {
                    ConsolidationDevice = ManagerManager.FindPVOutputConsolidationDevice(SystemId);
                    if (ConsolidationDevice == null)
                    {
                        GlobalSettings.LogMessage("PrepareConsumptionLoadList", "Cannot find ConsolidationDevice for: " + SystemId, LogEntryType.Information);
                        return;
                    }
                }

                for (DateTime day = DateTime.Today; day >= PVDateLimit; day = day.AddDays(-1.0))
                {
                    DeviceDetailPeriod_EnergyConsolidation consumptionPeriod = (DeviceDetailPeriod_EnergyConsolidation)ConsolidationDevice.FindOrCreateFeaturePeriod(FeatureType.ConsumptionAC, 0, day);

                    long energy = 0;
                    long power = 0;

                    foreach (EnergyReading reading in consumptionPeriod.GetReadings())
                    {
                        int timeVal = (int)reading.ReadingEnd.TimeOfDay.TotalSeconds;
                        // do not have instantaneous power - do have min and max power 
                        // values averaged over a 10 minute period - alternate between the two
                        if (((int)((int)(timeVal / 600) % 2)) == 0)
                            power = reading.MaxPower.HasValue ? reading.MaxPower.Value : reading.Power.HasValue ? reading.Power.Value : reading.AveragePower;
                        else
                            power = reading.MinPower.HasValue ? reading.MaxPower.Value : reading.Power.HasValue ? reading.Power.Value : reading.AveragePower;

                        energy += (long)(reading.EnergyDelta * 1000.0);
                        RecordConsumption(reading.ReadingEnd, timeVal, energy, power, reading.Temperature);
                    }
                }

                if (LastDeleteOld == null || LastDeleteOld.Value < DateTime.Now.AddDays(-1.0))
                {
                    DeleteOldLogEntries();
                    LastDeleteOld = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                throw new Exception("PrepareConsumptionLoadList - Error : " + e.Message, e);
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
                request.Headers.Add("X-Pvoutput-SystemId:" + SystemId);
                request.UserAgent = "PVBC/" + GlobalSettings.ApplicationSettings.ApplicationVersion + " (SystemId:" + SystemId + ")";

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
                    LogMessage("SendPVOutputBatch", "Server Error - Sid: " + SystemId + " - Message: " + postData +
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
                cmdLoadSel.AddParameterWithValue("@SiteId", SystemId);
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
            catch (GenException e)
            {
                throw new Exception("LoadPVOutputBatch: " + e.Message);
            }
            catch (Exception e)
            {
                throw new Exception("LoadPVOutputBatch: " + e.Message, e);
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
                throw new Exception("GetOldestDay - Database exception: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("GetOldestDay: " + e.Message, e);
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
            request.Headers.Add("X-Pvoutput-SystemId:" + SystemId);

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
                    LogMessage("BackloadMissingDay", "Server Error: API Key: " + APIKey + " - Sid: " + SystemId + " - Message: " + postData +
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
                        throw new Exception("BackloadMissingDays - Database exception: " + e.Message, e);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("BackloadMissingDays: " + e.Message, e);
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

            String requestData = "key=" + APIKey + "&sid=" + SystemId +
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
                LogMessage("BackloadPVOutput", "Server Error: API Key:" + APIKey + " Sid:" + SystemId + "Message:" + requestData +
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

        private void PVForceLiveLoad()
        {
            ObservableCollection<PVOutputDaySettings> list = Settings.PvOutputDayList;

            GenCommand cmdFrcLoad = null;
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                cmdFrcLoad = new GenCommand(CmdForceLoad, con);

                cmdFrcLoad.AddParameterWithValue("@SiteId", SystemId);

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
                            throw new Exception("PVForceLiveLoad - Database exception: " + e.Message, e);
                        }
                        catch (Exception e)
                        {
                            throw new Exception("PVForceLiveLoad: " + e.Message, e);
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

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(0.0); } }

        public override TimeSpan InitialPause { get { return TimeSpan.FromSeconds(10.0); } }

        public override void Initialise()
        {
            base.Initialise();
            PVForceLiveLoad();
            LogMessage("Initialise", "pvoutput.org update started", LogEntryType.StatusChange);
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

        public override bool DoWork()
        {
            const int maxCount = 4;
            bool cycle = true;

            try
            {
                //check for shutdown every 5 seconds
                // if previous attempts did not complete, try again after 3 minutes
                bool haveOutputReadyEvent = OutputReadyEvent.WaitOne(TimeSpan.FromSeconds(15));
                OutputReadyEvent.Reset();

                lock(OutputProcessLock) // ensure only one PVOutputManager does this at any one time
                {
                    if (haveOutputReadyEvent || InitialOutputCycle
                    || (!Complete && ((DateTime.Now - IncompleteTime.Value) >= TimeSpan.FromMinutes(3.0))))
                    {
                        LogMessage("DoWork", "Running update", LogEntryType.Trace);

                        GlobalSettings.SystemServices.GetDatabaseMutex();
                        try
                        {
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
                                    PrepareYieldLoadList();                                    
                                    PrepareConsumptionLoadList();                                   

                                    LogMessage("DoWork", "Running LoadPVOutputBatch", LogEntryType.Trace);
                                    Complete = LoadPVOutputBatch();
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
