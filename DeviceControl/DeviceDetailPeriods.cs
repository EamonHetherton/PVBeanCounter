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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GenericConnector;
using PVBCInterfaces;
using MackayFisher.Utilities;
using PVSettings;

namespace DeviceDataRecorders
{
    public abstract class DeviceDetailPeriodsBase
    {
        public const double DiscardInterval = 3600.0;

        public System.Threading.Mutex PeriodsMutex;

        public PeriodType PeriodType { get; private set; }
        public TimeSpan PeriodStartOffset { get; private set; }
        public FeatureType FeatureType { get; private set; }
        public uint FeatureId { get; private set; }
        public uint DeviceFeatureId { get; set; }

        public FeatureSettings FeatureSettings;
        public DeviceManagerDeviceSettings DeviceSettings;
        public Device.DeviceBase Device;

        private List<DeviceDetailPeriodBase> Periods;

        public DeviceDetailPeriodsBase(Device.DeviceBase device, FeatureSettings featureSettings, PeriodType periodType, TimeSpan periodStartOffset)
        {
            PeriodsMutex = new System.Threading.Mutex();
            Device = device;
            DeviceSettings = Device.DeviceManagerDeviceSettings;
            PeriodType = periodType;
            PeriodStartOffset = periodStartOffset;
            FeatureSettings = featureSettings;
            FeatureType = FeatureSettings.FeatureType;
            FeatureId = FeatureSettings.FeatureId;

            Periods = new List<DeviceDetailPeriodBase>();
            
            SetDeviceFeature(featureSettings, false, DeviceSettings.DeviceSettings.IsThreePhase); 
        }

        private void SetDeviceIdentityBindings(bool isInsert, GenCommand cmd, int id, FeatureType featureType, int featureId,
            bool? isAC, bool? isThreePhase, int? stringNumber, int? phaseNumber)
        {
            if (!isInsert)
                cmd.AddParameterWithValue("@Id", id);
            else
            {
                cmd.AddParameterWithValue("@Device_Id", Device.DeviceId.Value);
                cmd.AddParameterWithValue("@FeatureType", (int)featureType);
                cmd.AddParameterWithValue("@FeatureId", (int)featureId);
            }
            cmd.AddParameterWithValue("@MeasureType", featureType.ToString());
            cmd.AddNullableBooleanParameterWithValue("@IsAC", isAC);
            cmd.AddNullableBooleanParameterWithValue("@IsThreePhase", isThreePhase);
            cmd.AddParameterWithValue("@StringNumber", stringNumber);
            cmd.AddParameterWithValue("@PhaseNumber", phaseNumber);
        }

        public void SetDeviceFeature(FeatureSettings feature, 
            bool? isConsumption = null, bool? isThreePhase = null,
            int? stringNumber = null, int? phaseNumber = null)
        {
            GenConnection con = null;
            GlobalSettings.SystemServices.GetDatabaseMutex();

            string sqlInsert =
                "insert into devicefeature (Device_Id, FeatureType, FeatureId, MeasureType, " +
                    "IsAC, IsThreePhase, StringNumber, PhaseNumber) " +
                "values (@Device_Id, @FeatureType, @FeatureId, @MeasureType, " +
                    "@IsAC, @IsThreePhase, @StringNumber, @PhaseNumber) ";

            string sqlUpdate =
                "update devicefeature set " +
                    "MeasureType = @MeasureType, " +
                    "IsAC = @IsAC, " +
                    "IsThreePhase = @IsThreePhase, " +
                    "StringNumber = @StringNumber, " +
                    "PhaseNumber = @PhaseNumber " +
                "where Id = @Id ";

            try
            {
                con = GlobalSettings.TheDB.NewConnection();

                if (!Device.DeviceId.HasValue)
                    Device.GetDeviceId(con);

                string curMeasureType = "";
                bool? curIsConsumption = null;
                bool? curIsAC = null;
                bool? curIsThreePhase = null;
                int? curStringNumber = null;
                int? curPhaseNumber = null;

                uint? deviceFeatureId = Device.ReadDeviceFeature(con, feature, ref curMeasureType, ref curIsConsumption, ref curIsAC, ref curIsThreePhase, ref curStringNumber, ref curPhaseNumber);
                if (deviceFeatureId.HasValue)
                {
                    DeviceFeatureId = deviceFeatureId.Value;
                    if (curMeasureType != feature.FeatureType.ToString()
                        || curIsConsumption != isConsumption
                        || curIsAC != feature.IsAC
                        || curIsThreePhase != isThreePhase
                        || curStringNumber != stringNumber
                        || curPhaseNumber != phaseNumber)
                    {
                        GenCommand cmd = new GenCommand(sqlUpdate, con);
                        SetDeviceIdentityBindings(false, cmd, (int)DeviceFeatureId, feature.FeatureType, (int)feature.FeatureId, feature.IsAC, isThreePhase, stringNumber, phaseNumber);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    GenCommand cmd = new GenCommand(sqlInsert, con);
                    SetDeviceIdentityBindings(true, cmd, -1, feature.FeatureType, (int)feature.FeatureId, feature.IsAC, isThreePhase, stringNumber, phaseNumber);
                    cmd.ExecuteNonQuery();
                    deviceFeatureId = Device.ReadDeviceFeature(con, feature, ref curMeasureType, ref curIsConsumption, ref curIsAC, ref curIsThreePhase, ref curStringNumber, ref curPhaseNumber);
                    if (deviceFeatureId.HasValue)
                        DeviceFeatureId = deviceFeatureId.Value;
                    else
                        throw new Exception("SetDeviceFeature - Read failed after insert");
                }

            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("SetDeviceIdentity", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
        }

        public PeriodEnumerator GetPeriodEnumerator(DateTime startTime, DateTime endTime)
        {
            PeriodEnumerator pEnum = new PeriodEnumerator(PeriodType, PeriodStartOffset, startTime, endTime);
            return pEnum;
        }

        public DeviceDetailPeriodBase FindOrCreate(DateTime periodStart)
        {
            bool haveMutex = false;
            try
            {
                PeriodsMutex.WaitOne();
                haveMutex = true;
                DeviceDetailPeriodBase periodReadings = Find(periodStart);

                if (periodReadings == null)
                {
                    periodReadings = NewPeriodReadingsGeneric(periodStart);
                    Add(periodReadings);
                }
                else
                    periodReadings.UpdateReadings();

                return periodReadings;
            }
            finally
            {
                if (haveMutex)
                    PeriodsMutex.ReleaseMutex();
            }
        }

        private void Add(DeviceDetailPeriodBase newRec)
        {
            Periods.Add(newRec);
        }

        public bool Remove(DeviceDetailPeriodBase oldRec)
        {
            bool haveMutex = false;
            try
            {
                PeriodsMutex.WaitOne();
                haveMutex = true;
                return Periods.Remove(oldRec);
            }
            finally
            {
                if (haveMutex)
                    PeriodsMutex.ReleaseMutex();
            }
        }

        private DeviceDetailPeriodBase Find(DateTime periodStart)
        {
            foreach (DeviceDetailPeriodBase item in Periods)
            {
                if (item.Start == periodStart)
                {
                    item.LastFindTime = DateTime.Now;
                    return item;
                }
            }
            return null;
        }

        protected abstract DeviceDetailPeriodBase NewPeriodReadingsGeneric(DateTime periodStart);

        public void DiscardOldPeriods()
        {
            GenConnection con = null;
            bool havePeriodsMutex = false;
            bool haveDBMutex = false;

            try
            {
                PeriodsMutex.WaitOne();
                havePeriodsMutex = true;
                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveDBMutex = true;
                con = GlobalSettings.TheDB.NewConnection();
                for (int i = 0; i < Periods.Count; )
                {
                    DeviceDetailPeriodBase period = Periods[i];
                    if (period.End <= DateTime.Today)
                    {
                        if (period.UpdatePending)
                            period.UpdateDatabase(con, null, false, null);
                        if (period.LastFindTime < DateTime.Now.AddSeconds(DiscardInterval))
                            Periods.RemoveAt(i);
                        else
                            i++;
                    }
                    else
                        i++;
                }
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                if (haveDBMutex)
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                if (havePeriodsMutex)
                    PeriodsMutex.ReleaseMutex();
            }
        }

        public void UpdateDatabase(GenConnection con, DateTime? activeReadingTime, bool purgeUnMatched, DateTime? consolidateTo)
        {
            bool haveDBMutex = false;            
            bool conIsLocal = false;
            bool havePeriodsMutex = false;

            //GlobalSettings.LogMessage("DeviceDetailPeriodsBase", "UpdateDatabase - Starting", LogEntryType.Trace);
            try
            {
                PeriodsMutex.WaitOne();
                havePeriodsMutex = true;
                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveDBMutex = true;
                if (con == null)
                {
                    con = GlobalSettings.TheDB.NewConnection();
                    conIsLocal = true;
                }
                //GlobalSettings.LogMessage("DeviceDetailPeriodsBase", "UpdateDatabase - Before Loop", LogEntryType.Trace);
                foreach (DeviceDetailPeriodBase item in Periods)
                {
                    if (activeReadingTime.HasValue && activeReadingTime.Value.Date != item.Start)
                        item.UpdateDatabase(con, null, purgeUnMatched, consolidateTo);
                    else
                        item.UpdateDatabase(con, activeReadingTime, purgeUnMatched, consolidateTo);
                    //GlobalSettings.LogMessage("DeviceDetailPeriodsBase", "UpdateDatabase - After item.UpdateDatabase", LogEntryType.Trace);
                }
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DeviceDetailPeriodsBase", "UpdateDatabase - Exception: " + e.Message, LogEntryType.Trace);
                throw e;
            }
            finally
            {
                //GlobalSettings.LogMessage("DeviceDetailPeriodsBase", "UpdateDatabase - Finally", LogEntryType.Trace);
                if (conIsLocal && con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                if (haveDBMutex)
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                if (havePeriodsMutex)
                    PeriodsMutex.ReleaseMutex();
            }
        }

        // Add raw reading is used when the reading may cross a period boundary, requiring the reading to be split at those boundaries
        // or when auto selection of the required period is required
        public void AddRawReading(ReadingBase reading, bool fromHistory = false)
        {
            DateTime startTime = reading.ReadingEnd - reading.Duration;
            PeriodEnumerator pEnum = GetPeriodEnumerator(startTime, reading.ReadingEnd);
            ReadingBase nextReading = reading;
            foreach (PeriodBase p in pEnum)
            {
                DeviceDetailPeriodBase pReadings = FindOrCreate(p.Start);
                if (nextReading.ReadingEnd <= pReadings.End)
                {
                    pReadings.AddReading(nextReading, fromHistory ? AddReadingType.History : AddReadingType.NewReading);
                    break;
                }
                ReadingBase thisReading;
                pReadings.SplitReading(nextReading, pReadings.End, out thisReading, out nextReading);
                pReadings.AddReading(thisReading, fromHistory ? AddReadingType.History : AddReadingType.NewReading);
            }
        }
    }

    public abstract class DeviceDetailPeriods<TPeriodReadings, TDeviceReading, TDeviceHistory> : DeviceDetailPeriodsBase
        where TPeriodReadings : DeviceDetailPeriod<TDeviceReading, TDeviceHistory>
        where TDeviceReading : ReadingBaseTyped<TDeviceReading, TDeviceHistory>, IComparable<TDeviceReading>
        where TDeviceHistory : ReadingBase
    {
        

        public DeviceDetailPeriods(Device.DeviceBase device, FeatureSettings featureSettings, PeriodType periodType, TimeSpan periodStartOffset)
            : base(device, featureSettings, periodType, periodStartOffset)
        {           
            
        }

        public new TPeriodReadings FindOrCreate(DateTime periodStart)
        {
            return (TPeriodReadings)base.FindOrCreate(periodStart);
        }

        /*
        public TPeriodReadings NewPeriodReadings(DateTime periodStart, DeviceDataRecorders.DeviceParamsBase deviceParams)
        {
            return (TPeriodReadings)NewPeriodReadingsGeneric(periodStart, deviceParams);
        }
        */
    }

    public class DeviceDetailPeriods_EnergyMeter : DeviceDetailPeriods<DeviceDetailPeriod_EnergyMeter, EnergyReading, EnergyReading>
    {
        public DeviceDetailPeriods_EnergyMeter(Device.DeviceBase device, FeatureSettings featureSettings, PeriodType periodType, TimeSpan periodStartOffset)
            : base(device, featureSettings, periodType, periodStartOffset)
        {
        }

        protected override DeviceDetailPeriodBase NewPeriodReadingsGeneric(DateTime periodStart)
        {
            DeviceDetailPeriod_EnergyMeter periodReadings = new DeviceDetailPeriod_EnergyMeter(this, PeriodType, periodStart, FeatureSettings);
            periodReadings.LoadPeriodFromDatabase();
            return periodReadings;
        }
    }

    public class DeviceDetailPeriods_EnergyConsolidation : DeviceDetailPeriods<DeviceDetailPeriod_EnergyConsolidation, EnergyReading, EnergyReading>
    {
        public DeviceDetailPeriods_EnergyConsolidation(Device.DeviceBase device, FeatureSettings featureSettings, PeriodType periodType, TimeSpan periodStartOffset)
            : base(device, featureSettings, periodType, periodStartOffset)
        {
        }

        protected override DeviceDetailPeriodBase NewPeriodReadingsGeneric(DateTime periodStart)
        {
            DeviceDetailPeriod_EnergyConsolidation periodReadings = new DeviceDetailPeriod_EnergyConsolidation(this, PeriodType, periodStart, FeatureSettings);
            periodReadings.LoadPeriodFromConsolidations();
            return periodReadings;
        }

    }
}
