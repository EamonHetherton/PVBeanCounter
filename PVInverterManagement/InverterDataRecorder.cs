/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;
using Device;

namespace PVInverterManagement
{
    public class InverterDataRecorder : IInverterDataRecorder, IDataRecorder
    {
        public class InverterInfo
        {
            public UInt64 Address;
            public String Manufacturer = "";
            public String Model = "";
            public String SerialNo = "";
            public String Firmware = "";
            public bool Found = false;
            public int Phases = -1;
            public int? InverterId = null;
            public Double? VARating = null;
            public Double EstEnergy = 0.0;        // Current interval energy estimate based upon spot power readings - reset when cmsdata record is written
            public DateTime? LastEstTime = null;
            public DateTime? LastRecordTime = null;
            public Double EstMargin = 0.01;
            public bool UseEnergyTotal = true;
            public bool HasPhoenixtecStartOfDayEnergyDefect;
            public Double CrazyDayStartMinutes = 90.0;
            public bool StartEnergyResolved = false;    // Used with HasPhoenixtecStartOfDayEnergyDefect
            public bool EnergyDropFound = false;    // Used with HasPhoenixtecStartOfDayEnergyDefect


            public InverterInfo(UInt64 address, bool startOfDayEnergyDefect = false)
            {
                Address = address;
                HasPhoenixtecStartOfDayEnergyDefect = startOfDayEnergyDefect;
            }

            public TimeSpan EstimateEnergy(Double powerWatts, DateTime curTime)
            {
                TimeSpan duration;
                if (LastEstTime.HasValue)
                    duration = (curTime - LastEstTime.Value);
                else
                    duration = TimeSpan.FromSeconds(0.0);
                //Double prevEstimate = EstEnergy;
                //Double increment = (powerWatts * duration.TotalHours) / 1000.0;
                //Double newEstimate = prevEstimate + increment;
                Double newEnergy = (powerWatts * duration.TotalHours) / 1000.0; // watts to KWH
                EstEnergy += newEnergy;
                /*
                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.SystemServices.LogMessage("EstimateEnergy", 
                        "Previous: " + prevEstimate + 
                        " - Increment: " + increment + 
                        " - Calculated: " + newEstimate + 
                        " - Recorded: " + EstEnergy, LogEntryType.Trace);
                */
                LastEstTime = curTime;

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.SystemServices.LogMessage("InverterDataRecorder", "EstimateEnergy - Power: " + powerWatts + 
                        " - Duration: " + duration.TotalSeconds + " - Energy: " + newEnergy + " - TotalEnergy: " + EstEnergy, LogEntryType.Trace);

                return duration;
            }
        }

        public struct InverterReading
        {
            public DateTime Time;
            public Double? Temp;
            public Double? EnergyToday;
            public Double? VoltsPV;
            public Double? VoltsPV1;
            public Double? VoltsPV2;
            public Double? VoltsPV3;
            public Double? CurrentPV1;
            public Double? CurrentPV2;
            public Double? CurrentPV3;
            public Double? CurrentAC;
            public Double? VoltsAC;
            public Double? FreqAC;
            public Single? PowerAC;
            public UInt16? ImpedanceAC;
            public Double? EnergyTotal;
            public UInt32? Hours;
            public Int16? Mode;
            public Single? PowerPV;
            public Double? EstEnergy;
            public UInt32? ErrorCode;

            public InverterReading(DateTime time)
            {
                Time = time;
                Temp = null;
                EnergyToday = null;
                EnergyTotal = null;
                VoltsPV = null;
                VoltsPV1 = null;
                VoltsPV2 = null;
                VoltsPV3 = null;
                CurrentPV1 = null;
                CurrentPV2 = null;
                CurrentPV3 = null;
                CurrentAC = null;
                VoltsAC = null;
                FreqAC = null;
                PowerAC = null;
                ImpedanceAC = null;
                Hours = null;
                Mode = null;
                PowerPV = null;
                EstEnergy = null;
                ErrorCode = null;
            }
        }

        public virtual int IntervalSeconds { get { return 300; } } // Interval size in OutputHistory (not the thread run interval)

        private DateTime ProcessingDay;

        private String SiteId;

        private List<InverterInfo> Inverters;

        private int PrevInterval = -1; // OutputHistory update interval number
        
        public InverterManagerSettings InverterManagerSettings { get; private set; }
        public IManagerManager InverterManagerManager { get; private set; }

        private SystemServices SystemServices;
        
        public PVHistoryUpdate HistoryUpdater = null;

        // public int InverterManagerID { get; private set; }

        public List<DeviceStatus> InverterStatusList;

        private DateTime? NextFileDate = null;      // specifies the next DateTime to be used for extract

        public InverterDataRecorder(InverterManagerSettings imSettings, IManagerManager ManagerManager, List<InverterInfo> inverterList)
        {
            // InverterManagerID = inverterManagerId;
            InverterStatusList = new List<DeviceStatus>();
            SystemServices = GlobalSettings.SystemServices;
            InverterManagerSettings = imSettings;
            InverterManagerManager = ManagerManager;

            SiteId = null;

            Inverters = inverterList;
            ProcessingDay = DateTime.MinValue;

            HistoryUpdater = new PVHistoryUpdate(this);
        }

        private void LogMessage(String component, String message, LogEntryType logEntryType)
        {
            SystemServices.LogMessage("InverterDataRecorder." + component, message, logEntryType);
        }

        private void ResetInverterDay(InverterDataRecorder.InverterInfo iInfo)
        {
            iInfo.StartEnergyResolved = false;
            iInfo.EnergyDropFound = false;
            iInfo.UseEnergyTotal = true;
        }

        public bool CheckStartOfDay()
        {
            if (ProcessingDay != DateTime.Today)
            {
                // this resets attributes that only apply to last day processed
                foreach (InverterDataRecorder.InverterInfo iInfo in Inverters)
                    ResetInverterDay(iInfo);

                ProcessingDay = DateTime.Today;
                return true;
            }
            else
                return false;
        }


        private static String SelectAllInverters =
            "select i.Id, i.SerialNumber, i.SiteId " +
            "from inverter as i ";

        private void UpdateAllOutputHistory(DateTime day)
        {
            String stage = "start";

            GenConnection con = null;
            Device.DeviceIdentity inverter;
            bool haveMutex = false;

            try
            {
                SystemServices.GetDatabaseMutex();
                haveMutex = true;

                LogMessage("UpdateAllOutputHistory", "Updating day " + day, LogEntryType.Trace);
                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "new command";
                GenCommand cmd = new GenCommand(SelectAllInverters, con);

                stage = "execute";
                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();

                stage = "enter loop";
                while (dr.Read())
                {
                    inverter = new Device.DeviceIdentity();
                    inverter.DeviceId = dr.GetInt32(0);
                    inverter.SerialNo = dr.GetString(1);
                    if (dr.IsDBNull(2))
                        inverter.SiteId = null;
                    else
                        inverter.SiteId = dr.GetString(2);

                    bool useEnergyTotal = UseEnergyTotal(inverter, day);

                    InverterInfo iInfo = null;

                    foreach (InverterInfo info in Inverters)
                        if (info.SerialNo == inverter.SerialNo)
                        {
                            iInfo = info;
                            break;
                        }

                    if (iInfo == null)
                        iInfo = new InverterInfo(0); // any address will do, not used here and not retained

                    ResetInverterDay(iInfo);

                    if (iInfo.HasPhoenixtecStartOfDayEnergyDefect && iInfo.CrazyDayStartMinutes > 0.0)
                    {
                        // if defect present use deltas if drop found otherwise do not start with deltas
                        bool? energyDropFound = CheckForEnergyDrop(iInfo, inverter, day, useEnergyTotal);
                        if (energyDropFound.HasValue)
                        {
                            iInfo.StartEnergyResolved = true;
                            iInfo.EnergyDropFound = energyDropFound.Value;
                        }
                    }
                    else
                    {
                        // inverters without defect do not start with deltas
                        iInfo.StartEnergyResolved = true;
                        iInfo.EnergyDropFound = false;
                    }
                    UpdateOneOutputHistory(iInfo, inverter, day, iInfo.UseEnergyTotal,
                        iInfo.EnergyDropFound || !iInfo.StartEnergyResolved || iInfo.UseEnergyTotal);

                    ResetInverterDay(iInfo);
                }

                stage = "end loop";
                dr.Close();
                dr.Dispose();
                dr = null;
            }
            catch (Exception e)
            {
                LogMessage("UpdateAllOutputHistory", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }

                if (haveMutex)
                    SystemServices.ReleaseDatabaseMutex();
            }
        }

        private void UpdateOneOutputItem(Device.EnergyReadingSet readingSet, DateTime currentIntervalTime,
            DateTime prevIntervalTime, Double energy, Double? power, Double minPower, Double maxPower, Double? temperature = null)
        {
            Device.EnergyReading reading;
            int intervalCount = ((int)(currentIntervalTime - prevIntervalTime).TotalSeconds) / IntervalSeconds;
            DateTime intervalTime = prevIntervalTime + TimeSpan.FromSeconds(IntervalSeconds);

            Double kwhOutput = Math.Round(energy / intervalCount, 4);
            Double minPowerKwh;
            Double maxPowerKwh;

            if (power == null)
            {
                minPowerKwh = kwhOutput * 3600 / IntervalSeconds;
                maxPowerKwh = minPowerKwh;
            }
            else
            {
                minPowerKwh = minPower / 1000.0;
                maxPowerKwh = maxPower / 1000.0;
            }

            while (intervalTime <= currentIntervalTime)
            {
                reading = new Device.EnergyReading();

                reading.Duration = IntervalSeconds;
                reading.KWHOutput = kwhOutput;
                reading.OutputTime = intervalTime;
                reading.MinPower = minPowerKwh;
                reading.MaxPower = maxPowerKwh;
                reading.Temperature = temperature;
                readingSet.Readings.Add(reading);
                intervalTime += TimeSpan.FromSeconds(IntervalSeconds);
            }
        }

        private static String SelectCMSDataCountToday =
            "select count(*) " +
            "from cmsdata " +
            "where Inverter_Id = @InverterId " +
            "and OutputTime > @StartTime " +
            "and OutputTime <= @EndTime " +
            "and EnergyToday > 0 ";

        private bool UseEnergyTotal(Device.DeviceIdentity inverter, DateTime day)
        {
            String stage = "start";
            GenConnection con = null;

            try
            {
                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "new check command";
                GenCommand cmdCheck = new GenCommand(SelectCMSDataCountToday, con);
                stage = "check parameters";
                cmdCheck.AddParameterWithValue("@InverterId", inverter.DeviceId.Value);
                cmdCheck.AddParameterWithValue("@StartTime", day.Date);
                cmdCheck.AddParameterWithValue("@EndTime", day.Date + TimeSpan.FromDays(1.0));

                stage = "execute check";
                GenDataReader drCheck = (GenDataReader)cmdCheck.ExecuteReader();

                bool useTotal = true;

                stage = "read check";
                if (drCheck.Read())
                {
                    if (drCheck.IsDBNull(0))
                        useTotal = true;
                    else
                        useTotal = drCheck.GetInt32(0) == 0;
                }
                stage = "cleanup check";
                drCheck.Close();
                drCheck.Dispose();
                drCheck = null;
                cmdCheck.Dispose();
                cmdCheck = null;

                return useTotal;
            }
            catch (Exception e)
            {
                LogMessage("UseEnergyToday", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
            return false;
        }

        private static String SelectCMSData =
            "select OutputTime, EnergyTotal, EnergyToday, PowerAC, EstEnergy, Temperature " +
            "from cmsdata " +
            "where Inverter_Id = @InverterId " +
            "and OutputTime > @StartTime " +
            "and OutputTime <= @EndTime " +
            "order by OutputTime ";

        private void UpdateOneOutputHistory(InverterInfo iInfo, Device.DeviceIdentity inverter, DateTime day, bool useEnergyTotal, bool useDeltaAtStart)
        {
            String stage = "start";

            GenConnection con = null;
            Device.EnergyReadingSet readingSet;

            try
            {
                LogMessage("UpdateOneOutputHistory", "Updating day " + day + " - for inverter id " + inverter.DeviceId.Value, LogEntryType.Trace);

                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "Inverter";
                readingSet = new Device.EnergyReadingSet(inverter, 100);
                readingSet.Device = inverter;

                stage = "new command";
                GenCommand cmd = new GenCommand(SelectCMSData, con);
                stage = "parameters";
                cmd.AddParameterWithValue("@InverterId", inverter.DeviceId.Value);
                cmd.AddParameterWithValue("@StartTime", day.Date);
                cmd.AddParameterWithValue("@EndTime", day.Date + TimeSpan.FromDays(1.0));

                stage = "execute";
                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();
                bool isFirst = true;

                Double prevInvEnergy = 0.0;
                Double currentInvEnergy = 0.0;
                Double currentInvDelta = 0.0;

                Double currentEnergyEstimate = 0.0;     // total of interval estimates for today
                Double prevEnergyEstimate = 0.0;     // previous total of interval estimates for today
                Double currentEstimateDelta = 0.0;

                Double energyRecorded = 0.0;
                Double prevEnergyRecorded = 0.0;

                Double? power = null;
                Double minPower = 0.0;
                Double maxPower = 0.0;
                Double? temperature = null;
                DateTime prevIntervalTime = DateTime.Today;
                DateTime currentIntervalTime = DateTime.Today;

                stage = "enter loop";
                while (dr.Read())
                {
                    stage = "loop 1";
                    DateTime thisTime = dr.GetDateTime("OutputTime");
                    DateTime thisIntervalTime = thisTime.Date + TimeSpan.FromMinutes(((((int)thisTime.TimeOfDay.TotalMinutes) + 4) / 5) * 5);

                    Double thisInvEnergy = 0.0;

                    bool todayNull = dr.IsDBNull("EnergyToday");
                    Double estEnergy = dr.IsDBNull("EstEnergy") ? 0.0 : dr.GetDouble("EstEnergy");

                    if (useEnergyTotal)
                    {
                        if (dr.IsDBNull("EnergyTotal"))
                            LogMessage("UpdateOneOutputHistory", "useEnergyTotal specified but not available - Time: " + thisIntervalTime, LogEntryType.ErrorMessage);
                        else
                        {
                            thisInvEnergy = dr.GetDouble("EnergyTotal");
                            useDeltaAtStart = true;     // no start of day value available - must use deltas only
                        }
                    }
                    else if (dr.IsDBNull("EnergyToday"))
                        LogMessage("UpdateOneOutputHistory", "useEnergyTotal not specified but energy today not available - Time: " + thisIntervalTime, LogEntryType.ErrorMessage);
                    else
                        thisInvEnergy = dr.GetDouble("EnergyToday");

                    if (isFirst && useDeltaAtStart)
                    {
                        // first energy reading contains energy from previous days - must use deltas only from this point on
                        // CMS inverters with the start of day defect on this day and other inverters without EToday values start this way
                        isFirst = false;
                        prevInvEnergy = thisInvEnergy;
                        currentInvEnergy = thisInvEnergy;
                        prevEnergyEstimate = thisInvEnergy;
                        currentEnergyEstimate = thisInvEnergy;
                        currentIntervalTime = thisIntervalTime;
                        prevIntervalTime = currentIntervalTime - TimeSpan.FromSeconds(IntervalSeconds);
                    }
                    else
                    {
                        if (isFirst)
                        {
                            currentIntervalTime = thisIntervalTime;
                            prevIntervalTime = currentIntervalTime - TimeSpan.FromSeconds(IntervalSeconds);
                            isFirst = false;
                        }

                        if (currentInvEnergy < prevInvEnergy)
                        {
                            // Cannot report negative energy - try to preserve previous delta as part of next delta
                            if (useDeltaAtStart)
                            {
                                if (SystemServices.LogTrace)
                                    LogMessage("UpdateOneOutputHistory", "Energy Today has reduced - was: " + prevInvEnergy.ToString() +
                                    " - now: " + currentInvEnergy.ToString() + " - Time: " + currentIntervalTime, LogEntryType.Trace);
                            }
                            else
                                LogMessage("UpdateOneOutputHistory", "Energy Today has reduced - was: " + prevInvEnergy.ToString() +
                                    " - now: " + currentInvEnergy.ToString() + " - Time: " + currentIntervalTime, LogEntryType.ErrorMessage);

                            prevInvEnergy = currentInvEnergy - currentInvDelta;
                            currentEnergyEstimate = currentInvEnergy; // estimates are synced with inv values at energy reduction
                            prevEnergyEstimate = currentInvEnergy - currentEstimateDelta;
                            useDeltaAtStart = false; // activate estimate range checks and report energy reductions after the first on a day
                        }
                        else
                        {
                            currentInvDelta = currentInvEnergy - prevInvEnergy;
                            currentEstimateDelta = currentEnergyEstimate - prevEnergyEstimate;
                            // if a PVBC restart occurs energy estimate will be reset to 0 - retain previous estimate
                            // this is important with inverters using low resolution energy reporting
                            // missing estimates can cause a step shaped energy curve and peaked average power curve
                            if (currentEstimateDelta < 0.0)
                                currentEstimateDelta = estEnergy; // restart will cause at least one energy estimate value to be discarded - substitute the current estimate
                        }

                        if (thisIntervalTime != currentIntervalTime)
                        {
                            if (currentEnergyEstimate < currentInvEnergy)
                                currentEnergyEstimate = currentInvEnergy; // estimate lags - catchup
                            else if (currentEnergyEstimate > (currentInvEnergy + iInfo.EstMargin))
                                currentEnergyEstimate = (currentInvEnergy + iInfo.EstMargin);

                            currentEstimateDelta = currentEnergyEstimate - prevEnergyEstimate;
                            // if a PVBC restart occurs energy estimate will be reset to 0 - retain previous estimate
                            // this is important with inverters using low resolution energy reporting
                            // missing estimates can cause a step shaped energy curve and peaked average power curve
                            if (currentEstimateDelta < 0.0)
                                currentEstimateDelta = estEnergy; // restart will cause at least one energy estimate value to be discarded - substitute the current estimate

                            prevEnergyRecorded = energyRecorded;
                            energyRecorded += currentEstimateDelta;

                            UpdateOneOutputItem(readingSet, currentIntervalTime, prevIntervalTime,
                                currentEstimateDelta, power, minPower, maxPower, temperature);

                            prevInvEnergy = currentInvEnergy;
                            prevEnergyEstimate = currentEnergyEstimate;
                            prevIntervalTime = currentIntervalTime;
                            currentIntervalTime = thisIntervalTime;
                            currentInvEnergy = thisInvEnergy;

                            minPower = 0.0;
                            maxPower = 0.0;
                        }

                        currentInvEnergy = thisInvEnergy;
                        currentEnergyEstimate += estEnergy;
                    }

                    if (dr.IsDBNull("PowerAC"))
                        power = null;
                    else
                        power = dr.GetDouble("PowerAC");

                    if (power != null)
                    {
                        if (power.Value < minPower || minPower == 0.0)
                            minPower = power.Value;
                        if (power.Value > maxPower)
                            maxPower = power.Value;
                    }

                    if (dr.IsDBNull("Temperature"))
                        temperature = null;
                    else
                        temperature = dr.GetDouble("Temperature");
                }

                // write out last if it has an energy value
                if (currentEnergyEstimate > prevEnergyEstimate)
                    UpdateOneOutputItem(readingSet, currentIntervalTime, prevIntervalTime,
                        currentEnergyEstimate - prevEnergyEstimate, power, minPower, maxPower, temperature);

                stage = "end loop";
                dr.Close();
                dr.Dispose();
                dr = null;
                stage = "history update";

                LogMessage("UpdateOneOutputHistory", "Day: " + day + " - count: " + readingSet.Readings.Count, LogEntryType.Trace);

                if (readingSet.Readings.Count > 0)
                    HistoryUpdater.UpdateReadingSet(readingSet, con, false);
            }
            catch (Exception e)
            {
                LogMessage("UpdateOneOutputHistory", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
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

        private bool? CheckForEnergyDrop(InverterInfo iInfo, Device.DeviceIdentity inverter, DateTime day, bool useEnergyTotal)
        {
            String stage = "start";
            bool? useDeltas = null;

            GenConnection con = null;

            try
            {
                LogMessage("CheckForEnergyDrop", "Day " + day + " - for inverter id " + inverter.DeviceId.Value, LogEntryType.Trace);

                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "new command";
                GenCommand cmd = new GenCommand(SelectCMSData, con);
                stage = "parameters";
                cmd.AddParameterWithValue("@InverterId", inverter.DeviceId.Value);
                cmd.AddParameterWithValue("@StartTime", day.Date);
                cmd.AddParameterWithValue("@EndTime", day.Date + TimeSpan.FromDays(1.0));

                stage = "execute";
                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();
                bool isFirst = true;

                Double prevInvEnergy = 0.0;
                DateTime firstIntervalTime = DateTime.Today;

                stage = "enter loop";
                while (dr.Read())
                {
                    stage = "loop 1";
                    DateTime thisIntervalTime = dr.GetDateTime("OutputTime");

                    Double thisInvEnergy = 0.0;
                    bool todayNull = dr.IsDBNull("EnergyToday");

                    if (useEnergyTotal)
                    {
                        if (!dr.IsDBNull("EnergyTotal"))
                            thisInvEnergy = dr.GetDouble("EnergyTotal");
                    }
                    else if (!dr.IsDBNull("EnergyToday"))
                        thisInvEnergy = dr.GetDouble("EnergyToday");

                    if (isFirst)
                    {
                        prevInvEnergy = thisInvEnergy;
                        firstIntervalTime = thisIntervalTime;
                        isFirst = false;
                    }
                    else if (thisInvEnergy < prevInvEnergy)
                    {
                        useDeltas = true;
                        break;
                    }
                    else if (thisIntervalTime - firstIntervalTime > TimeSpan.FromMinutes(iInfo.CrazyDayStartMinutes))
                    {
                        useDeltas = false;
                        break;
                    }
                    else
                        prevInvEnergy = thisInvEnergy;
                }

                stage = "end loop";
                dr.Close();
                dr.Dispose();
                dr = null;

                LogMessage("CheckForEnergyDrop", "Day: " + day + " - result: " + useDeltas, LogEntryType.Trace);
            }
            catch (Exception e)
            {
                LogMessage("CheckForEnergyDrop", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
            return useDeltas;
        }

        private void UpdateOutputHistory(InverterInfo iInfo, Device.DeviceIdentity inverter, DateTime day, bool useEnergyTotal, bool useDeltaAtStart)
        {
            String stage = "start";
            bool haveMutex = false;

            LogMessage("UpdateOutputHistory", "Day: " + day, LogEntryType.Trace);

            try
            {
                SystemServices.GetDatabaseMutex();
                haveMutex = true;
                stage = "new connection";

                UpdateOneOutputHistory(iInfo, inverter, day, useEnergyTotal, useDeltaAtStart);
            }
            catch (Exception e)
            {
                LogMessage("UpdateOutputHistory", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (haveMutex)
                    SystemServices.ReleaseDatabaseMutex();
            }
        }

        private DeviceStatus LocateInverterStatus(int id, String siteId)
        {
            foreach (DeviceStatus status in InverterStatusList)
            {
                if (status.Id == id)
                {
                    status.SiteId = siteId;
                    return status;
                }
            }

            DeviceStatus newStatus = new DeviceStatus(id, siteId, IntervalSeconds);
            InverterStatusList.Add(newStatus);
            return newStatus;
        }

        private int GetInverterTypeId(String make, String model, GenConnection connection, bool autoInsert)
        {
            GenCommand cmd = null;
            GenDataReader dr = null;

            string selCmd =
                "select it.Id " +
                "from invertertype it " +
                "where it.Model = @Model " +
                "and it.Manufacturer = @Make;";

            try
            {
                cmd = new GenCommand(selCmd, connection);
                cmd.AddParameterWithValue("@Model", model);
                cmd.AddParameterWithValue("@Make", make);

                dr = (GenDataReader)cmd.ExecuteReader();
                if (dr.HasRows)
                {
                    if (dr.Read())
                    {
                        int i = dr.GetInt32(0);
                        dr.Close();
                        return i;
                    }
                }
                else if (autoInsert)
                {
                    dr.Close();
                    return InsertInverterType(make, model, connection);
                }

                dr.Close();
                throw new GenException(GenExceptionType.NoRowsReturned, "GetInverterTypeId: No rows returned");
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.UniqueConstraintRowExists)
                    return 0;       // failure due to key conflict - row already exists

                throw new PVException(PVExceptionType.UnexpectedDBError, "GetInverterTypeId - DB Error reading an inverter type: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "GetInverterTypeId - Error reading an inverter type: " + e.Message, e);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();
                if (cmd != null)
                    cmd.Dispose();
            }
        }

        private int InsertInverterType(String make, String model, GenConnection connection)
        {
            Int32 result = -1;
            GenCommand cmd = null;

            string insCmd =
                "insert into invertertype (Manufacturer, Model) " +
                "values ( @Make, @Model )";

            try
            {
                cmd = new GenCommand(insCmd, connection);
                cmd.AddParameterWithValue("@Make", make);
                cmd.AddParameterWithValue("@Model", model);

                result = cmd.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.UniqueConstraintRowExists)
                    return 0;       // failure due to key conflict - row already exists

                throw new PVException(PVExceptionType.UnexpectedDBError, "InsertInverterType - DB Error inserting an inverter type: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "InsertInverterType - Error inserting an inverter type: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }


            //retrieve the new system generated InverterType_Id
            return GetInverterTypeId(make, model, connection, false);
        }

        public int InsertInverter(String make, String model, String serialNo, GenConnection connection)
        {
            Int32 inverterTypeId = GetInverterTypeId(make, model, connection, true);

            Int32 result = -1;
            GenCommand cmd = null;

            string insCmd =
                // "insert into inverter (InverterType_Id, SerialNumber, InverterManager_Id, SiteId) " +
                // "values ( @InverterTypeId, @SerialNo, @InverterMgrId, @SiteId) ";
                "insert into inverter (InverterType_Id, SerialNumber, SiteId) " +
                "values ( @InverterTypeId, @SerialNo, @SiteId) ";
            try
            {
                cmd = new GenCommand(insCmd, connection);
                cmd.AddParameterWithValue("@InverterTypeId", inverterTypeId);
                cmd.AddParameterWithValue("@SerialNo", serialNo);
                // cmd.AddParameterWithValue("@InverterMgrId", InverterManagerID);

                if (GlobalSettings.ApplicationSettings.PvOutputSiteList.Count == 1)
                    cmd.AddParameterWithValue("@SiteId", GlobalSettings.ApplicationSettings.PvOutputSiteList[0].SiteId);
                else
                    cmd.AddParameterWithValue("@SiteId", DBNull.Value);

                result = cmd.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.UniqueConstraintRowExists)
                    return 0;       // failure due to key conflict - row already exists

                throw new PVException(PVExceptionType.UnexpectedDBError, "InsertInverter - DB Error inserting an inverter: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "InsertInverter - Error inserting an inverter: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }

            //retrieve the new system generated InverterType_Id
            String siteId;
            return GetInverterId(make, model, serialNo, connection, false, out siteId);
        }

        /*
        public void UpdateInverterManagerId(int id, GenConnection connection)
        {
            GenCommand cmd = null;

            string updCmd =
                "update inverter set InverterManager_Id = @InverterManagerId " +
                "where Id = @Id;";
            try
            {
                cmd = new GenCommand(updCmd, connection);
                cmd.AddParameterWithValue("@InverterManagerId", InverterManagerID);
                cmd.AddParameterWithValue("@Id", id);

                cmd.ExecuteNonQuery();
            }
            catch (PVException e)
            {
                throw e;
            }
            catch (GenException e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "UpdateInverterManagerId - DB Error updating an inverter: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "UpdateInverterManagerId - Error updating an inverter: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
        }
        */

        public int GetInverterId(String make, String model, String serialNo, GenConnection connection, bool autoInsert, out String siteId)
        {
            GenCommand cmd = null;
            GenDataReader dr = null;

            string selCmd =
                "select i.Id, i.SiteId " +
                "from invertertype it, inverter i " +
                "where it.Id = i.inverterType_Id " +
                "and i.SerialNumber = @SerialNo " +
                "and it.Model = @Model " +
                "and it.Manufacturer = @Make;";
            try
            {
                cmd = new GenCommand(selCmd, connection);
                cmd.AddParameterWithValue("@SerialNo", serialNo);
                cmd.AddParameterWithValue("@Model", model);
                cmd.AddParameterWithValue("@Make", make);

                dr = (GenDataReader)cmd.ExecuteReader();

                if (dr.HasRows)
                {
                    if (dr.Read())
                    {
                        int i = dr.GetInt32(0);
                        String sid = null;

                        if (dr.IsDBNull(1))
                        {
                            if (GlobalSettings.ApplicationSettings.PvOutputSiteList.Count == 1)
                                sid = GlobalSettings.ApplicationSettings.PvOutputSiteList[0].SiteId;
                            else
                                sid = null;
                        }
                        else
                        {
                            sid = dr.GetString(1).Trim();
                        }

                        dr.Close();

                        siteId = sid;
                        LocateInverterStatus(i, sid);
                        return i;
                    }
                }
                else if (autoInsert)
                {
                    dr.Close();

                    if (GlobalSettings.ApplicationSettings.PvOutputSiteList.Count == 1)
                        siteId = GlobalSettings.ApplicationSettings.PvOutputSiteList[0].SiteId;
                    else
                        siteId = null;

                    int id = InsertInverter(make, model, serialNo, connection);
                    LocateInverterStatus(id, siteId);
                    return id;
                }

                dr.Close();

                throw new GenException(GenExceptionType.NoRowsReturned, "GetInverterId");
            }
            catch (PVException e)
            {
                throw e;
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.NoRowsReturned)
                    throw e;
                throw new PVException(PVExceptionType.UnexpectedDBError, "GetInverterId - DB Error reading an inverter: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "GetInverterId - Error reading an inverter: " + e.Message, e);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();
                if (cmd != null)
                    cmd.Dispose();
            }
        }

        private static String InsertCMSData =
            "INSERT INTO cmsdata " +
            "( Inverter_Id, OutputTime, EnergyTotal, EnergyToday, Temperature, VoltsPV, " +
            "VoltsPV1, VoltsPV2, VoltsPV3, CurrentPV1, CurrentPV2, CurrentPV3, " +
            "CurrentAC, VoltsAC, FrequencyAC, PowerAC, ImpedanceAC, Hours, Mode, " +
            "PowerPV, EstEnergy, ErrorCode) " +
            "VALUES " +
            "(@Inverter_Id, @OutputTime, @EnergyTotal, @EnergyToday, @Temperature, @VoltsPV, " +
            "@VoltsPV1, @VoltsPV2, @VoltsPV3, @CurrentPV1, @CurrentPV2, @CurrentPV3, " +
            "@CurrentAC, @VoltsAC, @FrequencyAC, @PowerAC, @ImpedanceAC, @Hours, @Mode, " +
            "@PowerPV, @EstEnergy, @ErrorCode)";

        public void RecordReading(InverterDataRecorder.InverterInfo iInfo, InverterDataRecorder.InverterReading reading)
        {
            string stage = "Init";
            GenConnection con = null;
            bool haveMutex = false;

            try
            {
                reading.EstEnergy = iInfo.EstEnergy;

                SystemServices.GetDatabaseMutex();
                haveMutex = true;
                con = GlobalSettings.TheDB.NewConnection();

                if (iInfo.InverterId == null)
                {
                    stage = "GetInverterId";
                    iInfo.InverterId = GetInverterId(iInfo.Manufacturer,
                                                iInfo.Model,
                                                iInfo.SerialNo, con, true, out SiteId);
                }

                GenCommand cmd = new GenCommand(InsertCMSData, con);
                cmd.AddParameterWithValue("@Inverter_Id", iInfo.InverterId.Value);
                stage = "Time";
                cmd.AddParameterWithValue("@OutputTime", reading.Time);
                stage = "EnergyTotal";
                cmd.AddRoundedParameterWithValue("@EnergyTotal", reading.EnergyTotal, 3);
                stage = "EnergyToday";
                cmd.AddRoundedParameterWithValue("@EnergyToday", reading.EnergyToday, 3);
                stage = "Temp";
                cmd.AddRoundedParameterWithValue("@Temperature", reading.Temp, 2);
                stage = "VoltsPV";
                cmd.AddRoundedParameterWithValue("@VoltsPV", reading.VoltsPV, 2);
                stage = "VoltsPV1";
                cmd.AddRoundedParameterWithValue("@VoltsPV1", reading.VoltsPV1, 2);
                stage = "VoltsPV2";
                cmd.AddRoundedParameterWithValue("@VoltsPV2", reading.VoltsPV2, 2);
                stage = "VoltsPV3";
                cmd.AddRoundedParameterWithValue("@VoltsPV3", reading.VoltsPV3, 2);
                stage = "CurrentPV1";
                cmd.AddRoundedParameterWithValue("@CurrentPV1", reading.CurrentPV1, 2);
                stage = "CurrentPV2";
                cmd.AddRoundedParameterWithValue("@CurrentPV2", reading.CurrentPV2, 2);
                stage = "CurrentPV3";
                cmd.AddRoundedParameterWithValue("@CurrentPV3", reading.CurrentPV3, 2);
                stage = "CurrentAC";
                cmd.AddRoundedParameterWithValue("@CurrentAC", reading.CurrentAC, 2);
                stage = "VoltsAC";
                cmd.AddRoundedParameterWithValue("@VoltsAC", reading.VoltsAC, 2);
                stage = "FreqAC";
                cmd.AddRoundedParameterWithValue("@FrequencyAC", reading.FreqAC, 1);
                stage = "PowerPV";
                cmd.AddRoundedParameterWithValue("@PowerPV",  reading.PowerPV, 2);
                stage = "PowerAC";
                cmd.AddRoundedParameterWithValue("@PowerAC", reading.PowerAC, 2);
                stage = "Mode";
                cmd.AddParameterWithValue("@Mode", reading.Mode);

                stage = "EstEnergy";
                // use rounding - 6 dp more than adequate - SQLite stores all values as text, too many digits wastes DB space
                cmd.AddRoundedParameterWithValue("@EstEnergy", reading.EstEnergy, 6);

                stage = "ErrorCode";
                if (reading.ErrorCode.HasValue)
                    cmd.AddParameterWithValue("@ErrorCode", (long)reading.ErrorCode.Value);
                else
                    cmd.AddParameterWithValue("@ErrorCode", null);

                stage = "ImpedanceAC";
                if (reading.ImpedanceAC.HasValue)
                    cmd.AddParameterWithValue("@ImpedanceAC", (long)reading.ImpedanceAC.Value);
                else
                    cmd.AddParameterWithValue("@ImpedanceAC", null);

                stage = "Hours";
                if (reading.Hours.HasValue)
                    cmd.AddParameterWithValue("@Hours", (long)reading.Hours.Value);
                else
                    cmd.AddParameterWithValue("@Hours", null);

                stage = "Execute";
                cmd.ExecuteNonQuery();

                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("RecordReading", "EnergyTotal: " + reading.EnergyTotal + 
                        " - EnergyToday: " + reading.EnergyToday + 
                        " - PowerAC: " + reading.PowerAC +
                        " - Estimate: " + reading.EstEnergy, LogEntryType.Trace);

                iInfo.LastRecordTime = reading.Time;
                iInfo.EstEnergy = 0.0;
            }
            catch (Exception e)
            {
                LogMessage("RecordReading", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                if (haveMutex)
                    SystemServices.ReleaseDatabaseMutex();
            }
        }

        public static int IntervalCompare(int intervalSeconds, DateTime time1, DateTime time2)
        {
            if (time1.Date < time2.Date)
                return -1;

            if (time1.Date > time2.Date)
                return 1;

            TimeSpan diff = time1 - time2;
            int interval1 = (int)(time1.TimeOfDay.TotalSeconds / intervalSeconds);
            int interval2 = (int)(time2.TimeOfDay.TotalSeconds / intervalSeconds);

            if (interval1 < interval2)
                return -1;
            if (interval1 > interval2)
                return 1;

            return 0;
        }

        private List<DateTime> FindCompleteDays(int id, DateTime? startDate)
        {
            GenConnection connection = null;
            String cmdStr;
            GenCommand cmd;

            try
            {
                connection = GlobalSettings.TheDB.NewConnection();

                if (GlobalSettings.SystemServices.LogTrace && startDate != null)
                    GlobalSettings.SystemServices.LogMessage("FindCompleteDays", "limit day: " + startDate, LogEntryType.Trace);

                // hack for SQLite - I suspect it does a string compare that results in startDate being excluded from the list
                // drop back 1 day for SQLite - the possibility of an extra day in this list does not damage the final result
                // (in incomplete days that is)
                if (connection.DBType == GenDBType.SQLite && startDate != null)
                {
                    startDate -= TimeSpan.FromDays(1);
                    if (GlobalSettings.SystemServices.LogTrace && startDate != null)
                        GlobalSettings.SystemServices.LogMessage("FindCompleteDays", "SQLite adjusted limit day: " + startDate, LogEntryType.Trace);
                }

                // This implementation treats a day as complete if any inverter under the inverter manager reports a full day

                if (startDate == null)
                    cmdStr =
                    "select distinct oh.OutputDay " +
                    "from dayoutput_v oh " +
                    "where oh.Inverter_Id = @InverterId " +
                    "order by oh.OutputDay;";
                else
                    cmdStr =
                    "select distinct oh.OutputDay " +
                    "from dayoutput_v oh " +
                    "where oh.OutputDay >= @StartDate " +
                    "and oh.Inverter_Id = @InverterId " +
                    "order by oh.OutputDay;";

                cmd = new GenCommand(cmdStr, connection);
                if (startDate != null)
                    cmd.AddParameterWithValue("@StartDate", startDate);
                cmd.AddParameterWithValue("@InverterId", id);
                GenDataReader dataReader = (GenDataReader)cmd.ExecuteReader();

                List<DateTime> dateList = new List<DateTime>(7);
                int cnt = 0;

                bool yesterdayFound = false;
                bool todayFound = false;
                DateTime today = DateTime.Today;
                DateTime yesterday = today.AddDays(-1);

                while (dataReader.Read())
                {
                    DateTime day = dataReader.GetDateTime(0);

                    yesterdayFound |= (day == yesterday);
                    todayFound |= (day == today);

                    if (day < yesterday)
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            GlobalSettings.SystemServices.LogMessage("FindCompleteDays", "day: " + day, LogEntryType.Trace);
                        dateList.Add(dataReader.GetDateTime(0));
                        cnt++;
                    }
                }

                if (todayFound && yesterdayFound)
                    dateList.Add(yesterday);

                dataReader.Close();

                return dateList;
            }
            catch (Exception e)
            {
                throw new Exception("FindCompleteDays: error executing query: " + e.Message, e);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        public void SetDeviceUpdated(int id, String siteId)
        {
            SystemServices.LogMessage("SetInverterUpdated", "id: " + id + " - siteId: " + siteId, LogEntryType.Trace);
            DeviceStatus status = LocateInverterStatus(id, siteId);
            status.LastOutput = DateTime.Now;
            if (siteId != null)
                InverterManagerManager.SetPVOutputReady(siteId);
        }

        public List<DateTime> FindIncompleteDays(int id, bool ignoreDateReset = false)
        {
            DateTime? startDate = NextFileDate;
            List<DateTime> completeDays;

            if (!InverterManagerSettings.ResetFirstFullDay || ignoreDateReset)
                completeDays = FindCompleteDays(id, startDate);
            else
                completeDays = new List<DateTime>();

            try
            {
                // ensure we have a usable startDate
                if (startDate == null)
                    if (completeDays.Count > 0)
                        startDate = completeDays[0];
                    else
                        startDate = DateTime.Today;

                int numDays = (1 + (DateTime.Today - startDate.Value).Days);
                List<DateTime> incompleteDays = new List<DateTime>(numDays);

                for (int i = 0; i < numDays; i++)
                {
                    DateTime day = startDate.Value.AddDays(i);

                    if (!completeDays.Contains(day))
                    {
                        if (SystemServices.LogTrace)
                            SystemServices.LogMessage("FindInCompleteDays", "day: " + day, LogEntryType.Trace);
                        incompleteDays.Add(day);
                    }
                }

                return incompleteDays;
            }

            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "FindIncompleteDays: error : " + e.Message, e);
            }
        }

        public DateTime FindNewStartDate()
        {
            List<DateTime> dateList;

            try
            {
                dateList = FindIncompleteDays(true);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "FindNewStartDate: " + e.Message, e);
            }

            DateTime newStartDate;

            if (dateList.Count > 0)
                newStartDate = dateList[0];
            else
                newStartDate = DateTime.Today.Date;

            return newStartDate;
        }

        public void UpdatePastDates()
        {
            List<DateTime> dateList = FindIncompleteDays();

            foreach (DateTime date in dateList)
                if (date < DateTime.Today)
                    UpdateAllOutputHistory(date);

            DateTime nextDate = FindNewStartDate();

            if (NextFileDate == null || NextFileDate != nextDate)
            {
                bool haveMutex = false;
                try
                {
                    SystemServices.GetDatabaseMutex();
                    haveMutex = true;
                    UpdateNextFileDate(nextDate);
                    NextFileDate = nextDate;
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    if (haveMutex)
                        SystemServices.ReleaseDatabaseMutex();
                }
            }
        }

        public void UpdateNextFileDate(InverterInfo dInfo, DateTime nextFileDate)
        {
            GenConnection connection = null;
            Int32 result = -1;
            GenCommand cmd = null;

            string updCmd =
                "update inverter set NextFileDate = @NextFileDate " +
                "where Id = @InverterId;";

            try
            {
                connection = GlobalSettings.TheDB.NewConnection();
                cmd = new GenCommand(updCmd, connection);
                cmd.AddParameterWithValue("@NextFileDate", nextFileDate);
                cmd.AddParameterWithValue("@InverterId", dInfo.InverterId.Value);

                result = cmd.ExecuteNonQuery();
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "UpdateNextFileDate: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
                if (connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        public void UpdateOutputHistoryInterval()
        {
            //state = "before UpdateOutputHistory";
            int newInterval = ((int)DateTime.Now.TimeOfDay.TotalSeconds) / IntervalSeconds;
            if (SystemServices.LogTrace)
                LogMessage("UpdateOutputHistoryInterval", "NewInterval: " + newInterval +
                    " - PrevInterval: " + PrevInterval, LogEntryType.Trace);
            if (newInterval == PrevInterval)
                return;
            
            foreach (InverterDataRecorder.InverterInfo iInfo in Inverters)
            {
                if (iInfo.Found && iInfo.InverterId != null)
                {
                    Device.DeviceIdentity inverter = new Device.DeviceIdentity();
                    inverter.DeviceId = iInfo.InverterId;
                    inverter.SerialNo = iInfo.SerialNo;
                    inverter.SiteId = SiteId;

                    // If energy today is present energy total is not required for current day
                    iInfo.UseEnergyTotal = UseEnergyTotal(inverter, ProcessingDay);

                    if (!iInfo.StartEnergyResolved)
                        if (iInfo.HasPhoenixtecStartOfDayEnergyDefect && iInfo.CrazyDayStartMinutes > 0.0)
                        {
                            // if defect present use deltas if drop found otherwise do not start with deltas
                            bool? energyDropFound = CheckForEnergyDrop(iInfo, inverter, ProcessingDay, iInfo.UseEnergyTotal);
                            if (energyDropFound.HasValue)
                            {
                                iInfo.StartEnergyResolved = true;
                                iInfo.EnergyDropFound = energyDropFound.Value;
                            }
                        }
                        else
                        {
                            // inverters without defect do not start with deltas
                            iInfo.StartEnergyResolved = true;
                            iInfo.EnergyDropFound = false;
                        }
                    UpdateOutputHistory(iInfo, inverter, ProcessingDay, iInfo.UseEnergyTotal,
                        iInfo.EnergyDropFound || !iInfo.StartEnergyResolved || iInfo.UseEnergyTotal);
                }
            }

            PrevInterval = newInterval;
        }

    }
}
