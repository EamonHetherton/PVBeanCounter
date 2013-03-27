/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;
using PVBCInterfaces;
using Algorithms;
using GenericConnector;
using DeviceDataRecorders;

namespace Device
{
    public struct DeviceIdentity
    {
        // require DeviceID or Make, Model and SerialNo
        public int? DeviceId;
        public String Make;       // inverter manufacturer
        public String Model;      // inverter model
        public String SerialNo; // inverter unique id - could be serial number if available

        public DeviceIdentity(String make, String model, String serialNo, int? deviceId = null)
        {
            DeviceId = deviceId;
            Make = make;
            Model = model;
            SerialNo = serialNo;
        }
    }

    public struct OutputReadyNotification
    {
        public FeatureType FeatureType;
        public uint FeatureId;
        public DateTime ReadingEnd;
    }

    public abstract class DeviceBase 
    {
        public DeviceControl.DeviceManagerBase DeviceManager { get; protected set; }
        public DeviceManagerDeviceSettings DeviceManagerDeviceSettings;
        public DeviceSettings DeviceSettings;
        
        public int? DeviceId { get; protected set; }
        public String Manufacturer { get; set; }
        public String Model { get; set; }
        public String SerialNo { get; set; }

        public abstract DateTime NextRunTime { get; }

        public String DeviceIdentifier { get; protected set; }  // used for serial number on real devices; 
                                                                // generic identifier for consolidation devices
        protected String DefaultSerialNo;

        public DeviceType DeviceType { get { return DeviceParams.DeviceType; } }
        public int? DeviceType_Id { get; private set; }

        public int DatabaseInterval { get; protected set; }

        public bool ResetFirstFullDay { get; set; }
        public DateTime? NextFileDate { get; set; }

        public DateTime? LastRecordTime { get; protected set; }
        public DateTime LastRunTime { get; set; }

        protected bool EmitEvents;

        public DeviceDataRecorders.DeviceParamsBase DeviceParams;  

        public List<DeviceLink> TargetDevices;

        private List<FeaturePeriods> FeaturePeriodsList;

        public List<EnergyEventStatus> EventStatusList;

        public bool Enabled { get; private set; }

        public static DateTime NormaliseReadingTime(DateTime readingTime, int decimals = 1)
        {
            return readingTime.Date + TimeSpan.FromSeconds((int)Math.Round(readingTime.TimeOfDay.TotalSeconds, decimals));
        }

        public DeviceBase(DeviceControl.DeviceManagerBase deviceManager, DeviceManagerDeviceSettings deviceSettings)
        {
            DeviceParams = new DeviceDataRecorders.DeviceParamsBase();
            DeviceType_Id = null;
            DeviceId = null;

            NextFileDate = null;
            ResetFirstFullDay = false;

            LastRecordTime = null;
            LastRunTime = DateTime.MinValue;

            EmitEvents = GlobalSettings.ApplicationSettings.EmitEvents;
            TargetDevices = new List<DeviceLink>();
            FeaturePeriodsList = new List<FeaturePeriods>();

            DeviceManager = deviceManager;
            DeviceManagerDeviceSettings = deviceSettings;
            DeviceSettings = deviceSettings.DeviceSettings;
            DeviceIdentifier = deviceSettings.SerialNo;
            Enabled = deviceSettings.Enabled;

            BuildFeatureEvents();
        }

        private void BuildFeatureEvents()
        {
            EventStatusList = new List<EnergyEventStatus>();
            foreach (FeatureSettings fs in DeviceSettings.FeatureList)
            {
                EnergyEventStatus status = new EnergyEventStatus(this, fs.FeatureType, fs.FeatureId, DeviceManagerDeviceSettings.QueryIntervalInt, DeviceManagerDeviceSettings.DeviceEvents);
                EventStatusList.Add(status);
            }
        }

        public EnergyEventStatus FindFeatureStatus(FeatureType featureType, uint featureId)
        {
            foreach (EnergyEventStatus status in EventStatusList)
                if (featureType == status.FeatureType && featureId == status.FeatureId)
                    return status;
            return null;
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

        public void BindConsolidations(DeviceControl.IDeviceManagerManager mm)
        {
            foreach(ConsolidateDeviceSettings cds in DeviceManagerDeviceSettings.ConsolidateToDevices)
                if (cds.ConsolidateToDevice != null)
                {
                    ConsolidationDevice d = (ConsolidationDevice) mm.FindDeviceFromSettings(cds.ConsolidateToDevice);
                    if (d == null)
                        LogMessage("BindConsolidations - Cannot find device: " + cds.ConsolidateToDevice.Name, LogEntryType.ErrorMessage);
                    else if (!cds.ConsolidateFromFeatureType.HasValue || !cds.ConsolidateFromFeatureId.HasValue)
                        LogMessage("BindConsolidations - Missing Feature info: " + cds.ConsolidateFromDevice.Name, LogEntryType.ErrorMessage);
                    else
                        d.AddSourceDevice
                            (new DeviceLink( this, cds.ConsolidateFromFeatureType.Value, cds.ConsolidateFromFeatureId.Value, 
                                d, cds.ConsolidateToFeatureType, cds.ConsolidateToFeatureId, 
                                cds.Operation, FindFeatureStatus(cds.ConsolidateFromFeatureType.Value, cds.ConsolidateFromFeatureId.Value)));
                }
        }

        public virtual void ResetStartOfDay()
        {
        }

        public struct FeaturePeriods
        {
            public FeatureType Type;
            public uint Id;
            public DeviceDetailPeriodsBase Periods;
        }

        public virtual DeviceDetailPeriodBase FindOrCreateFeaturePeriod(FeatureType featureType, uint featureId, DateTime periodStart)
        {
            DeviceDetailPeriodsBase periodsBase = FindOrCreateFeaturePeriods(featureType, featureId);
            return periodsBase.FindOrCreate(periodStart);
        }

        public DeviceDetailPeriodsBase FindOrCreateFeaturePeriods(FeatureType featureType, uint featureId)
        {
            if (FeatureSettings.MeasureTypeFromFeatureType(featureType) == MeasureType.Energy)
            {
                foreach (FeaturePeriods fd in FeaturePeriodsList)
                    if (fd.Type == featureType && fd.Id == featureId)
                        return fd.Periods;
                FeaturePeriods fdNew;
                fdNew.Type = featureType;
                fdNew.Id = featureId;
                FeatureSettings fs = DeviceSettings.GetFeatureSettings(featureType, featureId);
                fdNew.Periods = CreateNewPeriods(fs);
                FeaturePeriodsList.Add(fdNew);
                return fdNew.Periods;
            }
            throw new NotSupportedException("Feature: " + featureType.ToString() + " - not supported");
        }

        protected abstract DeviceDataRecorders.DeviceDetailPeriodsBase CreateNewPeriods(FeatureSettings featureSettings);

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage(this.GetType().ToString(), message, logEntryType);
        }

        public bool FindInTargetList(ConsolidationDevice device)
        {
            foreach (DeviceLink dev in TargetDevices)
                if (dev.ToDevice == device)
                    return true;
                else if (dev.ToDevice.FindInTargetList(device))
                    return true;
            return false;
        }

        public void AddTargetDevice(DeviceLink deviceLink)
        {
            TargetDevices.Add(deviceLink);
        }

        private int? ReadDeviceTypeId(GenConnection con)
        {
            string sql =
                "select Id " +
                "from devicetype " +
                "where Manufacturer = @Manufacturer " +
                "and Model = @Model ";

            try
            {                
                GenCommand cmd = new GenCommand(sql, con);

                cmd.AddParameterWithValue("@Manufacturer", Manufacturer);
                cmd.AddParameterWithValue("@Model", Model);

                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();

                if (dr.Read())
                {
                    int id = dr.GetInt32(0);
                    dr.Close();
                    dr.Dispose();
                    return id;
                }

                dr.Close();
                dr.Dispose();

                return null;
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("ReadDeviceId", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            return null;
        }

        private int InsertDeviceTypeId(GenConnection con)
        {
            string sql =
                "insert into devicetype (Manufacturer, Model, DeviceType) " +
                "values (@Manufacturer, @Model, @DeviceType) ";
            
            GenCommand cmd = new GenCommand(sql, con);

            cmd.AddParameterWithValue("@Manufacturer", Manufacturer);
            cmd.AddParameterWithValue("@Model", Model);
            cmd.AddParameterWithValue("@DeviceType", DeviceParams.DeviceType.ToString());

            cmd.ExecuteNonQuery();

            int? id = ReadDeviceTypeId(con);

            if (id.HasValue)
                return id.Value;
            else
                throw new Exception("InsertDeviceTypeId cannot find created Id");           
        }

        private int InsertDeviceId(GenConnection con)
        {
            string sql =
                "insert into device (SerialNumber, DeviceType_Id) " +
                "values (@SerialNumber, @DeviceType_Id) ";
            
            GenCommand cmd = new GenCommand(sql, con);

            cmd.AddParameterWithValue("@SerialNumber", DeviceIdentifier);
            cmd.AddParameterWithValue("@DeviceType_Id", DeviceType_Id.Value);

            cmd.ExecuteNonQuery();

            int? id = ReadDeviceId(con);

            if (id.HasValue)
                return id.Value;
            else
                throw new Exception("InsertDeviceId cannot find created Id");            
        }

        private int? ReadDeviceId(GenConnection con)
        {
            string sql =
                "select Id " +
                "from device " +
                "where SerialNumber = @SerialNumber " +
                "and DeviceType_Id = @DeviceType_Id ";

            try
            {
                if (!DeviceType_Id.HasValue)
                {
                    DeviceType_Id = ReadDeviceTypeId(con);
                    if (!DeviceType_Id.HasValue)
                        DeviceType_Id = InsertDeviceTypeId(con);
                }

                GenCommand cmd = new GenCommand(sql, con);

                cmd.AddParameterWithValue("@SerialNumber", DeviceIdentifier);
                cmd.AddParameterWithValue("@DeviceType_Id", DeviceType_Id);

                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();

                if (dr.Read())
                {
                    int id = dr.GetInt32(0);
                    dr.Close();
                    dr.Dispose();
                    return id;
                }

                dr.Close();
                dr.Dispose();

                return null;
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("ReadDeviceId", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            
            return null;
        }

        public int GetDeviceId(GenConnection con)
        {
            bool localCon = false;
            try
            {
                if (DeviceId.HasValue)
                    return DeviceId.Value;

                if (con == null)
                {                    
                    con = GlobalSettings.TheDB.NewConnection();
                    localCon = true;
                    GlobalSettings.SystemServices.GetDatabaseMutex();
                }

                int? id = ReadDeviceId(con);
                if (!id.HasValue)
                    id = InsertDeviceId(con);

                DeviceId = id;
                return id.Value;
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("GetDeviceId", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
            finally
            {
                if (localCon && con != null)
                {
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                    con.Close();
                    con.Dispose();                    
                }
            }
        }

        public uint? ReadDeviceFeature(GenConnection con, FeatureSettings feature,
            ref String featureType, ref bool? isConsumption, ref bool? isAC, ref bool? isThreePhase,
            ref int? stringNumber, ref int? phaseNumber)
        {
            const string sqlRead =
                "select Id, MeasureType, IsConsumption, IsAC, IsThreePhase, StringNumber, PhaseNumber " +
                "from devicefeature " +
                "where Device_Id = @Device_Id " +
                "and FeatureType = @FeatureType " +
                "and FeatureId = @FeatureId ";

            try
            {
                GenCommand cmd = new GenCommand(sqlRead, con);

                cmd.AddParameterWithValue("@Device_Id", DeviceId.Value);
                cmd.AddParameterWithValue("@FeatureType", (int)feature.FeatureType);
                cmd.AddParameterWithValue("@FeatureId", (int)feature.FeatureId);

                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();

                if (dr.Read())
                {
                    //DeviceDetailPeriodsBase periods = FindOrCreateFeaturePeriods(feature.FeatureType, feature.FeatureId);
                    uint deviceFeatureId = (uint)dr.GetInt32(0);
 
                    featureType = dr.GetString(1);
                    isConsumption = dr.GetBoolFromChar(2);
                    isAC = dr.GetBoolFromChar(3);
                    isThreePhase = dr.GetBoolFromChar(4);
                    stringNumber = dr.GetNullableInt32(5);
                    phaseNumber = dr.GetNullableInt32(6);

                    dr.Close();
                    dr.Dispose();
                    return deviceFeatureId;
                }
                else
                {
                    dr.Close();
                    dr.Dispose();
                    return null;
                }
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("ReadDeviceFeature", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }

        private int PreviousDatabaseInterval = -1;
        private int CurrentDatabaseInterval = -1;
        private DateTime PreviousDay = DateTime.MinValue;
        private DateTime CurrentDay = DateTime.MinValue;

        public bool IsNewdatabaseInterval(DateTime checkTime)
        {
            int thisInterval = DeviceDataRecorders.PeriodBase.GetIntervalNo(PeriodType.Day, TimeSpan.Zero, checkTime, DatabaseInterval);
            if (CurrentDatabaseInterval == -1)
            {
                CurrentDatabaseInterval = thisInterval;
                CurrentDay = PeriodBase.GetPeriodStart(PeriodType.Day, TimeSpan.Zero, checkTime, true);
            }
            if (CurrentDay != checkTime.Date || thisInterval != CurrentDatabaseInterval)
            {
                PreviousDay = CurrentDay;
                CurrentDay = PeriodBase.GetPeriodStart(PeriodType.Day, TimeSpan.Zero, checkTime, true);
                PreviousDatabaseInterval = CurrentDatabaseInterval;
                CurrentDatabaseInterval = thisInterval;
                return true;
            }
            return false;            
        }

        protected DateTime? PreviousDatabaseIntervalEnd
        {
            get
            {
                if (PreviousDatabaseInterval == -1)
                    return null;
                
                return PreviousDay + TimeSpan.FromSeconds((PreviousDatabaseInterval + 1) * DatabaseInterval);
            }
        }

        public static void BuildOutputReadyFeatureList(List<OutputReadyNotification> featureList, FeatureType featureType, uint featureId, DateTime readingEnd)
        {
            OutputReadyNotification notify;
            notify.FeatureType = featureType;
            notify.FeatureId = featureId;
            notify.ReadingEnd = readingEnd;
            featureList.Add(notify);
        }

        protected void UpdateConsolidations(List<Device.OutputReadyNotification> notificationList)
        {
            // update each consolidation feature to indicate what is ready
            foreach(Device.OutputReadyNotification notify in notificationList)
                for(int i = 0; i < TargetDevices.Count; i++)
                {
                    DeviceLink link = TargetDevices[i];
                    if (notify.FeatureType == link.FromFeatureType && notify.FeatureId == link.FromFeatureId && link.LastReadyTime < notify.ReadingEnd)
                    {
                        link.LastReadyTime = notify.ReadingEnd;
                        link.SourceUpdated = true;
                        TargetDevices[i] = link;
                    }
                }

            // do this separately as there may be multiple notifications relating to any one device 
            // and all DevLink entries must be ready before notify
            //
            // notify each affected consolidation device once only
            List<ConsolidationDevice> notified = new List<ConsolidationDevice>();
            for (int i = 0; i < TargetDevices.Count; i++)
                if (!notified.Contains(TargetDevices[i].ToDevice))
                {
                    TargetDevices[i].ToDevice.NotifyConsolidation();
                    notified.Add(TargetDevices[i].ToDevice);
                }            
        }
    }

    public abstract class PhysicalDevice : DeviceBase 
    {
        public int DeviceInterval { get; private set; }
        public TimeSpan QueryInterval { get; private set; }

        public override DateTime NextRunTime { get { return LastRunTime + QueryInterval; } }
        public DateTime? FirstFullDay { get { return DeviceSettings != null ? DeviceManagerDeviceSettings.FirstFullDay : null; } }

        public UInt64 Address { get; protected set; }

        public bool HasStartOfDayEnergyDefect { get; set; }
        public Double CrazyDayStartMinutes { get; set; }
        public bool StartEnergyResolved { get; set; }    // Used with HasPhoenixtecStartOfDayEnergyDefect
        public bool EnergyDropFound { get; set; }   // Used with HasPhoenixtecStartOfDayEnergyDefect
        public bool UseEnergyTotal { get; set; }

        public Double EstEnergy { get; set; }        // Current interval energy estimate based upon spot power readings - reset when cmsdata record is written
        public Double EstMargin { get; set; }
        protected DateTime? LastEstTime = null;

        public bool FaultDetected { get; protected set; }

        public PhysicalDevice(DeviceControl.DeviceManagerBase deviceManager, DeviceManagerDeviceSettings deviceSettings, string manufacturer, string model, string serialNo)
                : base(deviceManager, deviceSettings)
        {
            Manufacturer = manufacturer == "" ? deviceSettings.Manufacturer : manufacturer;
            Model = model == "" ? deviceSettings.Model : model;
            SerialNo = serialNo == "" ? deviceSettings.SerialNo : serialNo;
            Address = deviceSettings.Address;

            EstEnergy = 0.0;
            EstMargin = 0.01;

            DatabaseInterval = deviceSettings.DBIntervalInt;
            DeviceInterval = deviceSettings.QueryIntervalInt;
            QueryInterval = TimeSpan.FromSeconds(deviceSettings.QueryIntervalInt);

            StartEnergyResolved = false;
            EnergyDropFound = false;
            UseEnergyTotal = true;

            CrazyDayStartMinutes = 90.0;
            HasStartOfDayEnergyDefect = false;
            FaultDetected = false;

            if (DeviceSettings == null) // legacy device
            {
                HasStartOfDayEnergyDefect = false;
                QueryInterval = TimeSpan.FromSeconds(6.0);
                DatabaseInterval = 60;
                DefaultSerialNo = "";
            }
            else
            {
                HasStartOfDayEnergyDefect = DeviceSettings.HasStartOfDayEnergyDefect;
                QueryInterval = TimeSpan.FromSeconds(DeviceManagerDeviceSettings.QueryIntervalInt);
                DatabaseInterval = DeviceManagerDeviceSettings.DBIntervalInt;
                Address = DeviceManagerDeviceSettings.Address;
                DefaultSerialNo = DeviceManagerDeviceSettings.SerialNo;
            }            
        }

        public override void ResetStartOfDay()
        {
            base.ResetStartOfDay();
            StartEnergyResolved = false;
            EnergyDropFound = false;
            UseEnergyTotal = true;
        }


        // curTime is supplied if duration is to be calculated on the fly (live readings)
        // curTime is null if duration is from a history record - standardDuration contains the correct duration
        protected TimeSpan EstimateEnergy(Double powerWatts, DateTime? curTime, float standardDuration)
        {
            TimeSpan duration;
            if (curTime.HasValue)
            {
                if (LastEstTime.HasValue)
                    duration = (curTime.Value - LastEstTime.Value);
                else
                    duration = TimeSpan.FromSeconds(standardDuration);
                LastEstTime = curTime;
            }
            else
                duration = TimeSpan.FromSeconds(standardDuration);
            
            Double newEnergy = (powerWatts * duration.TotalHours) / 1000.0; // watts to KWH
            EstEnergy += newEnergy;
            
            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.SystemServices.LogMessage("Device", "EstimateEnergy - Time: " + curTime + " - Power: " + powerWatts +
                    " - Duration: " + duration.TotalSeconds + " - Energy: " + newEnergy, LogEntryType.Trace);

            return duration;
        }
    }

    public abstract class PassiveDevice : PhysicalDevice 
    {
        public PassiveDevice(DeviceControl.DeviceManagerBase deviceManager, DeviceManagerDeviceSettings deviceSettings, string manufacturer, string model, string serialNo)
                : base(deviceManager, deviceSettings, manufacturer, model, serialNo)
            {
            }
    }

    public abstract class ActiveDevice : PhysicalDevice 
    {
        protected DeviceAlgorithm DeviceAlgorithm = null;

        public ActiveDevice(DeviceControl.DeviceManagerBase deviceManager, DeviceManagerDeviceSettings deviceSettings, DeviceAlgorithm deviceAlgorithm,
            string manufacturer, string model, string serialNo)
            : base(deviceManager, deviceSettings, manufacturer, model, serialNo)
        {
            DeviceAlgorithm = deviceAlgorithm;
        }

        public void ClearAttributes()
        {
            DeviceAlgorithm.ClearAttributes();
        }

        public abstract bool DoExtractReadings();
    }
}
