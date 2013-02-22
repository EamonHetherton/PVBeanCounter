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
using System.Xml.Linq;
using System.Threading;
using GenericConnector;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class OwlMeterManager : MeterManager
    {
        private OwlMeterManagerSettings Settings { get { return (OwlMeterManagerSettings)MeterManagerSettings; } }

        private GenDatabase OwlDb;

        private DateTime? NextMeterDay = null;
        private bool UseReload = false;
        private DateTime? ReloadFromDate = null;

        public OwlMeterManager(GenThreadManager genThreadManager, IManagerManager managerManager, int meterManagerId, OwlMeterManagerSettings settings) 
            : base(genThreadManager, settings, managerManager, meterManagerId)
        {
            OwlDb = null;
            
        }

        protected override String MeterManagerType {get{ return "Owl"; }}

        public override TimeSpan? StartHourOffset
        {
            get
            {
                return TimeSpan.FromMinutes(1.0);
            }
        }

        public override TimeSpan Interval { get { return TimeSpan.FromMinutes(5.0); } }

        protected List<DayCount> GetMissingDays(int addr)
        {
            LogMessage("GetMissingDays", "Starting - addr: " + addr); 
            GenConnection owlCon = null;
            GenCommand cmd = null;
            GenDataReader reader = null;
            
            List<DayCount> owlDayList = new List<DayCount>();

            try
            {
                String getDays;

                if (NextMeterDay == null)
                    getDays =
                        "select year, month, day, count(*), sum(ch1_kw_avg / 1000.0) " +
                        "from energy_history " +
                        "where addr = @addr " +
                        "and ch1_kw_avg > 0.0 " +
                        "group by year, month, day " +
                        "order by year, month, day ";
                else
                    getDays =
                        "select year, month, day, count(*), sum(ch1_kw_avg / 1000.0) " +
                        "from energy_history " +
                        "where addr = @addr " +
                        "and year >= @Year " +
                        "and (month >= @Month or year > @Year) " +
                        "and (day >= @Day or month > @Month or year > @Year) " +
                        "and ch1_kw_avg > 0.0 " +
                        "group by year, month, day " +
                        "order by year, month, day ";


                owlCon = OwlDb.NewConnection();
                cmd = new GenCommand(getDays, owlCon);
                cmd.AddParameterWithValue("@addr", addr);
                if (NextMeterDay != null)
                {
                    DateTime useDate;
                    if (UseReload && ReloadFromDate != null)
                        useDate = ReloadFromDate.Value;
                    else
                        useDate = NextMeterDay.Value;
                    cmd.AddParameterWithValue("@Year", useDate.Year);
                    cmd.AddParameterWithValue("@Month", useDate.Month);
                    cmd.AddParameterWithValue("@Day", useDate.Day);
                }

                reader = (GenDataReader)cmd.ExecuteReader();
                
                while (reader.Read())
                {
                    DateTime day = new DateTime(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
                    DayCount rec;
                    rec.Day = day;
                    rec.Count = reader.GetInt32(3);
                    rec.Sum = reader.GetDouble(4); 
                    owlDayList.Add(rec);
                }
            }
            catch (Exception e)
            {
                LogMessage("GetMissingDays", "Select Owl dates - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedDBError, "GetMissingDays - Select Owl dates - Exception: " + e.Message);
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
                if (owlCon != null)
                {
                    owlCon.Close();
                    owlCon.Dispose();
                    owlCon = null;
                }
            }

            LogMessage("GetMissingDays", "Owl days retrieved - count: " + owlDayList.Count);

            if (UseReload)
                LogMessage("GetMissingDays", "Reload requested - Ignore existing meter data", LogEntryType.Information );
            else
            {
                List<DayCount> meterDateList = GetMeterDays(addr, NextMeterDay);

                LogMessage("GetMissingDays", "PVBC days retrieved - count: " + meterDateList.Count);

                try
                {
                    foreach (DayCount existingDay in meterDateList)
                    {
                        if (!IsRunning)
                            break;

                        foreach (DayCount day in owlDayList)
                        {
                            // different storage technologies may introduce unwanted differences between the two Sum values
                            if (day.Day == existingDay.Day && day.Count == existingDay.Count && Math.Round(day.Sum, 3) == Math.Round(existingDay.Sum, 3))
                            {
                                owlDayList.Remove(day);
                                break;
                            }
                        }
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
            }
            LogMessage("GetMissingDays", "Complete - Owl list count: " + owlDayList.Count);
            return owlDayList;
        }

        private List<MeterRecord> ReadFromOwl(int addr, DateTime date)
        {
            List<MeterRecord> records = new List<MeterRecord>();
            GenConnection owlCon = null;
            GenCommand readCmd = null;
            GenDataReader reader = null;

            try
            {
                owlCon = OwlDb.NewConnection();

                String readOwlStr = "select hour, min, ch1_kw_avg / 1000.0 " +
                    "from energy_history " +
                    "where addr = @addr " +
                    "and year = @year " +
                    "and month = @month " +
                    "and day = @day " +
                    "and ch1_kw_avg > 0.0 " +
                    "order by year, month, day, hour, min ";

                readCmd = new GenCommand(readOwlStr, owlCon);

                readCmd.AddParameterWithValue("@addr", addr);
                readCmd.AddParameterWithValue("@year", date.Year);
                readCmd.AddParameterWithValue("@month", date.Month);
                readCmd.AddParameterWithValue("@day", date.Day);

                reader = (GenDataReader)readCmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!IsRunning)
                        break;

                    MeterRecord rec;
                    DateTime time = new DateTime(date.Year, date.Month, date.Day);
                    time += TimeSpan.FromHours(reader.GetInt32(0)) + TimeSpan.FromMinutes(reader.GetInt32(1));

                    rec.Sensor = addr;
                    rec.Time = time;
                    rec.Duration = 60;
                    rec.Energy = reader.GetDouble(2);
                    rec.Count = 1;
                    rec.Calculated = null;
                    rec.Temperature = null;
                    rec.InRange = null;
                    rec.MinPower = null;
                    rec.MaxPower = null;

                    records.Add(rec);
                }
            }
            catch (Exception e)
            {
                LogMessage("ReadFromOwl", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                return null;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                }
                if (readCmd!= null)
                    readCmd.Dispose();
                if (owlCon != null)
                {
                    owlCon.Close();
                    owlCon.Dispose();
                }
            }

            return records;
        }

        private void WriteOwlReadings(DateTime day, int addr, List<MeterRecord> records)
        {
            string deleteDayCmd =
                "delete from meterreading " +
                "where Meter_Id = @MeterId " +
                "and Appliance = @Appliance " +
                "and ReadingTime >= @StartTime " +
                "and ReadingTime < @EndTime ";

            GenCommand deleteCmd = null;
            try
            {
                deleteCmd = new GenCommand(deleteDayCmd, Connection);
                deleteCmd.AddParameterWithValue("@MeterId", MeterManagerId);
                deleteCmd.AddParameterWithValue("@Appliance", addr);
                deleteCmd.AddParameterWithValue("@StartTime", day);
                deleteCmd.AddParameterWithValue("@EndTime", day + TimeSpan.FromDays(1.0));
                deleteCmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                LogMessage("WriteOwlReadings", "Exception deleting day: " + day + " - addr: " + addr + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedDBError,
                    "WriteOwlReadings - Exception deleting day: " + day + " - addr: " + addr, e);
            }
            finally
            {
                if (deleteCmd != null)
                    deleteCmd.Dispose();
            }

            foreach (MeterRecord rec in records)
                InsertMeterReading(rec);

            if (day == DateTime.Today && records.Count > 0 && MeterManagerSettings.ApplicationSettings.EmitEvents)
            {
                MeterRecord rec = records[records.Count-1];                
                SensorInfo info = GetSensorInfo(addr.ToString());
                if (info.LastEventTime  < rec.Time)
                {
                    EmitEvent(rec.Time, info.ApplianceNo, rec.Duration, null, rec.Energy);
                    info.LastEventTime = rec.Time;
                }
            }
        }

        public override void Initialise()
        {
            base.Initialise();            
            LogMessage("Initialise", "OwlManager is starting", LogEntryType.StatusChange);

            OwlDb = new GenDatabase("", Settings.OwlDatabase, "", "", "SQLite", "Proprietary",
                "System.Data.SQLite", "", "", Settings.ApplicationSettings.DefaultDirectory, GlobalSettings.SystemServices);

            NextMeterDay = GetMeterNextDate();

            UseReload = Settings.ReloadDays;
            if (UseReload)
                ReloadFromDate = Settings.ReloadDaysFromDate;
        }

        public override bool DoWork()
        {
            bool result = true;

            DateTime? NewNextMeterDay = NextMeterDay;

            GlobalSettings.SystemServices.GetDatabaseMutex();
            try
            {

                foreach (MeterApplianceSettings app in Settings.ApplianceList)
                {
                    if (!IsRunning)
                    {
                        result = false;
                        break;
                    }
                    int addr = Convert.ToInt32(app.ApplianceNo);
                    List<DayCount> dayList = GetMissingDays(addr);

                    foreach (DayCount day in dayList)
                    {
                        if (!IsRunning)
                        {
                            result = false;
                            break;
                        }
                        LogMessage("DoWork", "Loading day: " + day.Day);
                        List<MeterRecord> readings = ReadFromOwl(addr, day.Day);
                        if (!IsRunning)
                        {
                            result = false;
                            break;
                        }
                        WriteOwlReadings(day.Day, addr, readings);

                        if (NewNextMeterDay == null || NewNextMeterDay < day.Day)
                            NewNextMeterDay = day.Day;
                    }
                }

                if (NewNextMeterDay != NextMeterDay)
                {
                    UpdateMeterNextDate(NewNextMeterDay.Value);
                    NextMeterDay = NewNextMeterDay;
                }

                UseReload = false;
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
            return result;
        }

        public override void Finalise()
        {
            LogMessage("Finalise", "OwlManager is stopping", LogEntryType.StatusChange);
            base.Finalise();
        }

    }
}
