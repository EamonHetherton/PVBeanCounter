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

        public PeriodType PeriodType { get; private set; }
        public TimeSpan PeriodStartOffset { get; private set; }
        public FeatureType FeatureType { get; private set; }
        public uint FeatureId { get; private set; }

        public FeatureSettings FeatureSettings;
        public DeviceManagerDeviceSettings DeviceSettings;
        public Device.DeviceBase Device;

        private List<DeviceDetailPeriodBase> Periods;

        public DeviceDetailPeriodsBase(Device.DeviceBase device, FeatureSettings featureSettings, PeriodType periodType, TimeSpan periodStartOffset)
        {
            Device = device;
            DeviceSettings = Device.DeviceManagerDeviceSettings;
            PeriodType = periodType;
            PeriodStartOffset = periodStartOffset;
            FeatureSettings = featureSettings;
            FeatureType = FeatureSettings.Type;
            FeatureId = FeatureSettings.Id;

            Periods = new List<DeviceDetailPeriodBase>();
        }

        public PeriodEnumerator GetPeriodEnumerator(DateTime startTime, DateTime endTime)
        {
            PeriodEnumerator pEnum = new PeriodEnumerator(PeriodType, PeriodStartOffset, startTime, endTime);
            return pEnum;
        }

        public DeviceDetailPeriodBase FindOrCreate(DateTime periodStart)
        {
            DeviceDetailPeriodBase periodReadings = Find(periodStart);

            if (periodReadings == null)
            {
                periodReadings = NewPeriodReadingsGeneric(periodStart, Device.DeviceParams);
                Add(periodReadings);
            }

            return periodReadings;
        }

        public void Add(DeviceDetailPeriodBase newRec)
        {
            Periods.Add(newRec);
        }

        public bool Remove(DeviceDetailPeriodBase oldRec)
        {
            return Periods.Remove(oldRec);
        }

        public DeviceDetailPeriodBase Find(DateTime periodStart)
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

        protected abstract DeviceDetailPeriodBase NewPeriodReadingsGeneric(DateTime periodStart, DeviceDataRecorders.DeviceParamsBase deviceParams);

        public void DiscardOldPeriods()
        {
            GenConnection con = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();
                for (int i = 0; i < Periods.Count; )
                {
                    DeviceDetailPeriodBase period = Periods[i];
                    if (period.End <= DateTime.Today)
                    {
                        if (period.UpdatePending)
                            period.UpdateDatabase(con, null);
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
                GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
        }

        public void UpdateDatabase(GenConnection con = null, DateTime? activeReadingTime = null)
        {
            GlobalSettings.SystemServices.GetDatabaseMutex();
            bool conIsLocal = false;

            try
            {
                if (con == null)
                {
                    con = GlobalSettings.TheDB.NewConnection();
                    conIsLocal = true;
                }
                foreach (DeviceDetailPeriodBase item in Periods)
                {
                    if (activeReadingTime.HasValue && activeReadingTime.Value.Date != item.Start)
                        item.UpdateDatabase(con, null);
                    else
                        item.UpdateDatabase(con, activeReadingTime);
                }
            }
            finally
            {
                if (conIsLocal && con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                GlobalSettings.SystemServices.ReleaseDatabaseMutex();
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
                pReadings.SplitReadingGeneric(nextReading, pReadings.End, out thisReading, out nextReading);
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

        public TPeriodReadings NewPeriodReadings(DateTime periodStart, DeviceDataRecorders.DeviceParamsBase deviceParams)
        {
            return (TPeriodReadings)NewPeriodReadingsGeneric(periodStart, deviceParams);
        }

    }

    public class DeviceDetailPeriods_EnergyMeter : DeviceDetailPeriods<DeviceDetailPeriod_EnergyMeter, EnergyReading, EnergyReading>
    {
        public DeviceDetailPeriods_EnergyMeter(Device.DeviceBase device, FeatureSettings featureSettings, PeriodType periodType, TimeSpan periodStartOffset)
            : base(device, featureSettings, periodType, periodStartOffset)
        {
        }

        protected override DeviceDetailPeriodBase NewPeriodReadingsGeneric(DateTime periodStart, DeviceDataRecorders.DeviceParamsBase deviceParams)
        {
            DeviceDetailPeriod_EnergyMeter periodReadings = new DeviceDetailPeriod_EnergyMeter(this, PeriodType, periodStart, FeatureSettings, deviceParams);
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

        protected override DeviceDetailPeriodBase NewPeriodReadingsGeneric(DateTime periodStart, DeviceDataRecorders.DeviceParamsBase deviceParams)
        {
            DeviceDetailPeriod_EnergyConsolidation periodReadings = new DeviceDetailPeriod_EnergyConsolidation(this, PeriodType, periodStart, FeatureSettings, deviceParams);
            periodReadings.LoadPeriodFromConsolidations();
            return periodReadings;
        }

    }
}
