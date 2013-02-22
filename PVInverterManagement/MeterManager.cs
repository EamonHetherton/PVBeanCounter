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
using GenericConnector;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVSettings;
using PVBCInterfaces;
using Device;

namespace PVInverterManagement
{
    public abstract class MeterManager : GenThread, IDeviceManager
    {
        public class SensorInfo
        {
            public MeterRecord Record;
            public DateTime CurrentMinute;
            public DateTime PreviousTime;
            public String InverterId;
            public String ApplianceNo;
            public bool IsInverterYield;
            public bool IsConsumption;
            public bool UseEvents;
            public DateTime LastEventTime;
            public String SiteId;
            public DateTime OutputRecorded;
            public DateTime LastOutput;
            public bool initialise;
        }

        public struct MeterRecord
        {
            public int Sensor;
            public DateTime Time;
            public Double Energy;           // original energy reading, or value from fill gaps correction
            public Double? Calculated;      // energy value above adjusted by calibration and / or history pro-rata adjustments
            public int Duration;
            public Double? Temperature;
            public int Count;
            public int? MinPower;
            public int? MaxPower;
            public bool? InRange;
        }

        public struct AdjustHist
        {
            public MeterRecord Record;
            public bool Modify;
            public bool IsNew;
        }

        protected struct DayCount
        {
            public DateTime Day;
            public int Count;
            public Double Sum;
        }

        protected int MeterManagerId;
        public IManagerManager ManagerManager { get; private set; }

        public ErrorLogger ErrorLogger { get; private set; }

        protected GenConnection Connection = null;
        private GenCommand InsertReadingCommand = null;
        private GenCommand UpdateReadingCommand = null;
        private GenCommand InsertHistoryCommand = null;

        private MeterManagerSettings MeterManagerSettingsInternal;

        public List<SensorInfo> SensorStatusList = new List<SensorInfo>();

        public override String ThreadName { get { return MeterManagerSettingsInternal.StandardName; } }

        public IDataRecorder DataRecorder { get; set; }

        private List<IDevice> DeviceListInternal;
        public List<IDevice> DeviceList { get { return DeviceListInternal; } }

        public String ManagerTypeName { get { return MeterManagerType; } }
        public int InstanceNo { get { return MeterManagerSettingsInternal.InstanceNo; } }
        public String ManagerId { get { return MeterManagerSettingsInternal.StandardName; } }

        public MeterManager(GenThreadManager genThreadManager, MeterManagerSettings settings, IManagerManager managerManager, int meterManagerId)
            : base(genThreadManager, GlobalSettings.SystemServices)
        {
            MeterManagerId = meterManagerId;
            ManagerManager = managerManager;
            MeterManagerSettingsInternal = settings;
            ErrorLogger = null;
            DeviceListInternal = new List<IDevice>();

            DataRecorder = null;
        }

        // use the following when device identity is known
        public IDevice GetDeviceInfo(GenConnection con, ulong address, String make, String model, String serialNo)
        {

            foreach (IDevice knownDevice in DeviceList)
            {
                if (knownDevice.Manufacturer == make && knownDevice.Model == model && knownDevice.SerialNo == serialNo)
                    return knownDevice;
            }

            String siteId;
            int deviceId = DataRecorder.GetDeviceId(make, model, serialNo, con, true, out siteId);

            PseudoDevice device = new PseudoDevice(this);
            device.Make = make;
            device.Model = model;
            device.SerialNo = serialNo;
            device.DeviceId = deviceId;
            device.Address = address;
            
            DeviceList.Add(device);
            return device;
        }

        protected MeterManagerSettings MeterManagerSettings { get { return MeterManagerSettingsInternal; } }

        public override void Initialise()
        {
            base.Initialise();

            Connection = GlobalSettings.TheDB.NewConnection();
            SetupCommands();

            int sensor = 0;
            while (sensor < MeterManagerSettingsInternal.ApplianceList.Count)
            {
                SensorStatusList.Add(NewSensor(sensor));
                sensor++;
            }
        }

        public void CloseErrorLogger()
        {
        }

        static internal DateTime GetMinute(DateTime time)
        {
            // returns the minute the time adds to
            DateTime val = time.Date + TimeSpan.FromMinutes((int)time.TimeOfDay.TotalMinutes);
            if (val < time)
                return val.AddMinutes(1.0);
            else
                return val;
        }

        public override void Finalise()
        {
            InsertReadingCommand.Dispose();
            UpdateReadingCommand.Dispose();
            InsertHistoryCommand.Dispose();
            Connection.Close();
            Connection.Dispose();
            InsertReadingCommand = null;
            UpdateReadingCommand = null;
            InsertHistoryCommand = null;
            Connection = null;

            base.Finalise();
        }

        protected SensorInfo GetSensorInfo(String applianceNo)
        {
            int sensor = 0;
            while (sensor < SensorStatusList.Count)
            {
                if (SensorStatusList.ElementAt(sensor).ApplianceNo == applianceNo)
                    return SensorStatusList.ElementAt(sensor);
                sensor++;
            }
            throw new PVException(PVExceptionType.UnexpectedError, "GetSensorInfo cannot find appliance: " + applianceNo);
        }

        protected void UpdateSensorList(DateTime minute, DateTime time, String applianceNo, int duration, int watts, Double? temperature = null)
        {
            SensorInfo info = GetSensorInfo(applianceNo);
            Double energy = ((double)(watts * duration)) / (3600000.0); // 1 minute in kwh = small number

            info.Record.Duration += duration;
            info.Record.Energy += energy;
            info.Record.Temperature = ((info.Record.Temperature * info.Record.Count)
                + temperature) / (info.Record.Count + 1);
            info.Record.Count++;
            info.Record.Time = minute;
            info.PreviousTime = time;

            if (info.Record.MinPower == null || info.Record.MinPower > watts)
                info.Record.MinPower = watts;
            if (info.Record.MaxPower == null || info.Record.MaxPower < watts)
                info.Record.MaxPower = watts;

            if (MeterManagerSettingsInternal.ApplicationSettings.EmitEvents
            && info.UseEvents)
                EmitEvent(time, info.ApplianceNo, duration, watts, energy);
        }

        private SensorInfo NewSensor(int sensor)
        {
            DateTime now = DateTime.Now;
            SensorInfo info = new SensorInfo();
            info.ApplianceNo = MeterManagerSettingsInternal.ApplianceList[sensor].ApplianceNo.ToString();
            info.Record.Duration = 0;
            info.Record.Sensor = sensor;
            info.Record.Temperature = 0;
            info.Record.Time = GetMinute(now);
            info.Record.Count = 0;
            info.Record.Energy = 0;
            info.Record.MinPower = null;
            info.Record.MaxPower = null;
            info.Record.Calculated = null;
            info.Record.InRange = null;
            info.CurrentMinute = info.Record.Time;
            info.PreviousTime = now;
            info.LastEventTime = DateTime.MinValue;
            info.IsConsumption = MeterManagerSettingsInternal.ApplianceList[sensor].IsConsumption;
            info.IsInverterYield = MeterManagerSettingsInternal.ApplianceList[sensor].IsInverterYield;
            info.InverterId = MeterManagerSettingsInternal.ApplianceList[sensor].Inverter;
            info.UseEvents = MeterManagerSettingsInternal.ApplianceList[sensor].StoreReading;
            info.SiteId = MeterManagerSettingsInternal.ApplianceList[sensor].ConsumptionSiteId;
            info.OutputRecorded = DateTime.Now;
            info.LastOutput = info.OutputRecorded;
            info.initialise = true;
            return info;
        }

        protected void ResetSensor(SensorInfo info, DateTime time)
        {
            info.Record.Duration = 0;
            info.Record.Temperature = 0;
            info.Record.Time = GetMinute(time);
            info.Record.Count = 0;
            info.Record.Energy = 0;
            info.Record.MinPower = null;
            info.Record.MaxPower = null;
            info.Record.Calculated = null;
            info.Record.InRange = null;
            info.CurrentMinute = info.Record.Time;
            info.PreviousTime = info.Record.Time.AddMinutes(-1.0);
        }

        public void SetSensorUpdated(String applianceNo,  String siteId)
        {
            SensorInfo status = GetSensorInfo(applianceNo);
            status.LastOutput = DateTime.Now;
            ManagerManager.SetPVOutputReady(siteId);
            LogMessage("SetSensorUpdated", "Appliance: " + applianceNo + " - SiteId: " + siteId);
        }

        protected void EmitEvent(DateTime time, String applianceId, int duration, int? watts, Double? energy)
        {
            try
            {
                ManagerManager.EnergyEvents.NewEnergyReading(HierarchyType.Meter, "Meter", MeterManagerSettingsInternal.StandardName, applianceId, time, energy, watts, duration);
            }
            catch (Exception e)
            {
                LogMessage("EmitEvent", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }      

        protected void LogRecordTracer(String routine, String action, AdjustHist rec)
        {
            LogMessage(routine, action + " - Time: " + rec.Record.Time +
                " - Duration: " + rec.Record.Duration, LogEntryType.MeterTrace);
        }

        protected void LogDurationTracer(String routine, String action, int duration, int totalDuration)
        {
            LogMessage(routine, action + " : " + duration + " - Total: " + totalDuration, LogEntryType.MeterTrace);
        }

        protected void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.MeterTrace)
        {
            GlobalSettings.LogMessage(MeterManagerType + ": " + routine, message, logEntryType);
        }

        protected abstract String MeterManagerType { get; }

        protected void UpdateMeterNextDate(DateTime day)
        {
            LogMessage("UpdateMeterNextDate", "Date: " + day);
            GenCommand cmd = null;

            try
            {
                cmd = new GenCommand("update meter set NextDate = @NextDate where Id = @MeterId ", Connection);
                cmd.AddParameterWithValue("@NextDate", day);
                cmd.AddParameterWithValue("@MeterId", MeterManagerId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                LogMessage("UpdateMeterNextDate", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedDBError, "UpdateMeterNextDate - Exception: " + e.Message);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
        }

        protected DateTime? GetMeterNextDate()
        {
            GenCommand cmd = null;
            GenDataReader reader = null;
            DateTime? firstDay = null;
            List<DateTime> dateList = null;

            try
            {
                dateList = new List<DateTime>();

                cmd = new GenCommand("select NextDate from meter where Id = @MeterId ", Connection);
                cmd.AddParameterWithValue("@MeterId", MeterManagerId);
                reader = (GenDataReader)cmd.ExecuteReader(System.Data.CommandBehavior.SingleRow);

                if (reader.Read())
                    firstDay = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
            }
            catch (Exception e)
            {
                LogMessage("GetMissingDays", "Select NextDate - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedDBError, "GetMissingDays - Select NextDate - Exception: " + e.Message);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                    reader = null;
                }
                if (cmd != null)
                {
                    cmd.Dispose();
                    cmd = null;
                }
            }
            return firstDay;
        }

        protected List<DayCount> GetMeterDays(int addr, DateTime? firstDay)
        {
            GenCommand cmd = null;
            GenDataReader reader = null;
            List<DayCount> dayList = null;

            try
            {
                dayList = new List<DayCount>();
                String getExistingDays;
                if (firstDay == null)
                    getExistingDays =
                    "select ReadingTime, Count(*), sum(Energy) " +
                    "from meterreading " +
                    "where Meter_Id = @MeterId " +
                    "and Appliance = @Appliance " +
                    "group by ReadingTime " +
                    "order by ReadingTime ";
                else
                    getExistingDays =
                        "select ReadingTime, Count(*), sum(Energy) " +
                        "from meterreading " +
                        "where Meter_Id = @MeterId " +
                        "and Appliance = @Appliance " +
                        "and ReadingTime >= @StartDate " +
                        "group by ReadingTime " +
                        "order by ReadingTime ";

                cmd = new GenCommand(getExistingDays, Connection);
                cmd.AddParameterWithValue("@MeterId", MeterManagerId);
                cmd.AddParameterWithValue("@Appliance", addr);
                if (firstDay != null)
                    cmd.AddParameterWithValue("@StartDate", firstDay);

                reader = (GenDataReader)cmd.ExecuteReader();
                DateTime? prevDay = null;
                while (reader.Read())
                {
                    if (!IsRunning)
                        break;
                    DateTime existingDay = reader.GetDateTime(0).Date;
                    if (existingDay == prevDay)
                        continue;
                    prevDay = existingDay;
                    DayCount rec;
                    rec.Day = existingDay;
                    rec.Count = reader.GetInt32(1);
                    rec.Sum = reader.GetDouble(2);
                    
                    dayList.Add(rec);
                }
            }
            catch (Exception e)
            {
                LogMessage("GetMissingDays", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedDBError, "GetMissingDays - Exception: " + e.Message);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                }
                if (cmd != null)
                    cmd.Dispose();
            }
            return dayList;
        }

        private void SetupCommands()
        {
            string insCmd =
                "insert into meterreading (Meter_Id, Appliance, ReadingTime, Duration, Energy, Calculated, Temperature, MinPower, MaxPower) " +
                "values ( @MeterId, @Appliance, @ReadingTime, @Duration, @Energy, @Calculated, @Temperature, @MinPower, @MaxPower) ";

            InsertReadingCommand = new GenCommand(insCmd, Connection);

            string updCmd =
                "update meterreading set Duration = @Duration, Energy = @Energy, Calculated = @Calculated, Temperature = @Temperature, MinPower = @MinPower, MaxPower = @MaxPower " +
                "where Meter_Id = @MeterId and Appliance = @Appliance and ReadingTime = @ReadingTime ";

            UpdateReadingCommand = new GenCommand(updCmd, Connection);

            string insHistCmd =
                "insert into meterhistory (Meter_Id, Appliance, ReadingTime, HistoryType, Duration, Energy) " +
                "values ( @MeterId, @Appliance, @ReadingTime, 'h', @Duration, @Energy) ";

            InsertHistoryCommand = new GenCommand(insHistCmd, Connection);
        }

        internal void SetCmdParameters(GenCommand cmd, MeterRecord rec, bool isHistory = false)
        {
            if (cmd.Parameters.Count == 0)
            {
                cmd.AddParameterWithValue("@MeterId", MeterManagerId);
                cmd.AddParameterWithValue("@Appliance", rec.Sensor);
                cmd.AddParameterWithValue("@ReadingTime", rec.Time);
                cmd.AddParameterWithValue("@Duration", rec.Duration);
                cmd.AddParameterWithValue("@Energy", rec.Energy);
                if (!isHistory)
                {
                    cmd.AddParameterWithValue("@Calculated", rec.Calculated == null ? Convert.DBNull : rec.Calculated);
                    cmd.AddParameterWithValue("@Temperature", rec.Temperature == null ? Convert.DBNull : rec.Temperature);
                    cmd.AddParameterWithValue("@MinPower", rec.MinPower == null ? Convert.DBNull : rec.MinPower);
                    cmd.AddParameterWithValue("@MaxPower", rec.MaxPower == null ? Convert.DBNull : rec.MaxPower);
                }
            }
            else
            {
                cmd.Parameters["@MeterId"].Value = MeterManagerId;
                cmd.Parameters["@Appliance"].Value = rec.Sensor;
                cmd.Parameters["@ReadingTime"].Value = rec.Time;
                cmd.Parameters["@Duration"].Value = rec.Duration;
                cmd.Parameters["@Energy"].Value = rec.Energy;
                if (!isHistory)
                {
                    cmd.Parameters["@Calculated"].Value = rec.Calculated == null ? Convert.DBNull : rec.Calculated;
                    cmd.Parameters["@Temperature"].Value = rec.Temperature == null ? Convert.DBNull : rec.Temperature;
                    cmd.Parameters["@MinPower"].Value = rec.MinPower == null ? Convert.DBNull : rec.MinPower;
                    cmd.Parameters["@MaxPower"].Value = rec.MaxPower == null ? Convert.DBNull : rec.MaxPower;
                }
            }
        }

        internal void InsertMeterHistory(MeterRecord rec)
        {
            int result = 1;

            try
            {
                SetCmdParameters(InsertHistoryCommand, rec, true);

                if (GlobalSettings.SystemServices.LogMeterTrace)
                    LogMessage("InsertMeterHistory", "Adding meter history to DB: time: " + rec.Time
                        + " : MeterID: " + MeterManagerId + " : Sensor: " + rec.Sensor
                        + " : Energy: " + rec.Energy + " : duration: " + rec.Duration);

                result = InsertHistoryCommand.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                if (e.Type != GenExceptionType.UniqueConstraintRowExists)
                    LogMessage("InsertMeterHistory", "GenException inserting meter history - Time: " + rec.Time +
                    " - Duration: " + rec.Duration +
                    " - Energy: " + rec.Energy +
                    " - Calculated: " + rec.Calculated +
                    " - Message: " + e.Message, LogEntryType.MeterTrace);
            }
            catch (Exception e)
            {
                LogMessage("InsertMeterHistory", "Exception inserting meter history - Time: " + rec.Time +
                    " - Duration: " + rec.Duration +
                    " - Energy: " + rec.Energy +
                    " - Calculated: " + rec.Calculated +
                    " - Message: " + e.Message, LogEntryType.MeterTrace);
            }
        }

        internal void InsertMeterReading(MeterRecord rec)
        {
            // do not store zero value readings
            if (rec.Energy == 0.0)
                return;

            int result = 1;

            // apply reading calibration now if required

            MeterApplianceSettings appliance = MeterManagerSettings.GetAppliance(rec.Sensor.ToString());

            double calibrate = appliance.Calibrate;

            if (calibrate != 1.0)
                rec.Calculated = Math.Round(rec.Energy * calibrate, 4);

            rec.Energy = Math.Round(rec.Energy, 4);
            if (rec.Temperature.HasValue)
                rec.Temperature = Math.Round(rec.Temperature.Value, 1);

            try
            {
                SetCmdParameters(InsertReadingCommand, rec);

                if (GlobalSettings.SystemServices.LogMeterTrace)
                    LogMessage("InsertMeterReading", "Adding reading to DB: time: " + rec.Time
                        + " : MeterID: " + MeterManagerId + " : Sensor: " + rec.Sensor
                        + " : Energy: " + rec.Energy + " : duration: " + rec.Duration
                        + " : Calculated: " + rec.Calculated);

                result = InsertReadingCommand.ExecuteNonQuery();

                if (appliance.IsConsumption)
                {
                    LogMessage("InsertMeterReading", "calling SetSensorUpdated - Appliance: " + appliance.ApplianceNo + " - SiteId: " + appliance.ConsumptionSiteId);
                    SetSensorUpdated(appliance.ApplianceNo, appliance.ConsumptionSiteId);
                }
            }
            catch (Exception e)
            {
                LogMessage("InsertMeterReading", "Exception inserting a new meter reading - Time: " + rec.Time +
                    " - Duration: " + rec.Duration +
                    " - Energy: " + rec.Energy +
                    " - Calculated: " + rec.Calculated +
                    " - Message: " + e.Message, LogEntryType.ErrorMessage);
            }

            if (result != 1)
                throw new PVException(PVExceptionType.UnexpectedDBError, "InsertMeterReading - Expected to insert 1 row, result was " + result);
        }

        internal void UpdateReadingRecords(List<AdjustHist> records)
        {
            String mode = "";

            AdjustHist rec;

            foreach (AdjustHist curRec in records)
            {
                rec = curRec;

                try
                {
                    if (rec.IsNew)
                    {
                        mode = "Insert record";
                        SetCmdParameters(InsertReadingCommand, rec.Record);
                        InsertReadingCommand.ExecuteNonQuery();
                    }
                    else if (rec.Modify)
                    {
                        mode = "Update record";
                        SetCmdParameters(UpdateReadingCommand, rec.Record);
                        UpdateReadingCommand.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    LogMessage("UpdateReadingRecords", mode + " Exception - Time: " + rec.Record.Time +
                        " - Duration: " + rec.Record.Duration +
                        " - Energy: " + rec.Record.Energy +
                        " - Calculated: " + rec.Record.Calculated +
                        " - Message: " + e.Message);
                }
            }
        }
    }
}
