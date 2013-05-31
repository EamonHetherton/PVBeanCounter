/*
* Copyright (c) 2013 Dennis Mackay-Fisher
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
using System.Threading;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using DeviceStream;
using GenThreadManagement;
using GenericConnector;
using PVBCInterfaces;
using Device;
using System.Diagnostics;


namespace DeviceControl
{
    public class DeviceManager_Owl : DeviceManagerTyped<Owl_Device>
    {
        private bool DevicesEnabled = false;

        private GenDatabase OwlDb;

        private bool ExtractHasRun = false;
        private DateTime lastDay = DateTime.MinValue;

        public DeviceManager_Owl(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
            OwlDb = null;

            foreach (DeviceBase dev in DeviceList)
                DevicesEnabled |= dev.Enabled;
        }

        public override DateTime NextRunTime(DateTime? currentTime = null)
        {
            return base.NextRunTime_Original(currentTime);
        }

        public override void Initialise()
        {
            base.Initialise();
            GlobalSettings.LogMessage("Initialise", "OwlManager is starting", LogEntryType.StatusChange);

            OwlDb = new GenDatabase("", DeviceManagerSettings.OwlDatabase, "", "", "SQLite", "Proprietary",
                "System.Data.SQLite", "", "", DeviceManagerSettings.ApplicationSettings.DefaultDirectory, GlobalSettings.SystemServices);
        }

        public override TimeSpan Interval { get { return TimeSpan.FromMinutes(5.0); } }

        public override String ThreadName { get { return "DeviceMgr_Owl"; } }

        protected override Owl_Device NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            return new Owl_Device(this, dmDevice, "", "");
        }

        private Owl_Device NewDevice(DeviceManagerDeviceSettings dmDevice, string model, string serialNo)
        {
            return new Owl_Device(this, dmDevice, model, serialNo);
        }

        public override TimeSpan? StartHourOffset { get { return TimeSpan.FromMinutes(1.0); } }

        private bool ReadFromOwl(Owl_Device device, DateTime date)
        {
            GenConnection owlCon = null;
            GenCommand readCmd = null;
            GenDataReader reader = null;

            try
            {
                Owl_Record prevRec;
                prevRec.Seconds = 0;
                prevRec.EnergyKwh = 0.0;
                prevRec.TimeStampe = DateTime.MinValue;
                bool havePrevious = false;

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

                readCmd.AddParameterWithValue("@addr", device.Address);
                readCmd.AddParameterWithValue("@year", date.Year);
                readCmd.AddParameterWithValue("@month", date.Month);
                readCmd.AddParameterWithValue("@day", date.Day);

                reader = (GenDataReader)readCmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!IsRunning)
                        break;

                    Owl_Record rec;
                    DateTime time = new DateTime(date.Year, date.Month, date.Day);
                    time += TimeSpan.FromHours(reader.GetInt32(0)) + TimeSpan.FromMinutes(reader.GetInt32(1));

                    rec.TimeStampe = time;
                    rec.EnergyKwh = reader.GetDouble(2);
                    rec.Seconds = 60;

                    if (havePrevious)
                        device.ProcessOneHistoryReading(prevRec);
                    else
                        havePrevious = true;
                    prevRec = rec;
                }
                if (havePrevious)
                    if (date.Date == DateTime.Today && prevRec.TimeStampe.TimeOfDay > DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(15.0)))
                        device.ProcessOneLiveReading(prevRec);
                    else
                        device.ProcessOneHistoryReading(prevRec);
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("ReadFromOwl", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                }
                if (readCmd != null)
                    readCmd.Dispose();
                if (owlCon != null)
                {
                    owlCon.Close();
                    owlCon.Dispose();
                }
            }

            return true;
        }

        private void RunExtracts()
        {
            List<DateTime> dateList;
            
            try
            {
                dateList = FindEmptyDays(false, ExtractHasRun);
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtracts: " + e.Message, e);
            }

            // Add today's date to the end of the list if it is not there already
            if (dateList.Count > 0)
            {
                // Always reload yesterday at startup - in case yesterday was not fully captured - early shutdown
                // older incomplete days require manual intervention
                if (lastDay != DateTime.Today)
                    if (!dateList.Contains(DateTime.Today.AddDays(-1)))
                        dateList.Add(DateTime.Today.AddDays(-1));

                if (!dateList.Contains(DateTime.Today))
                    dateList.Add(DateTime.Today);
            }
            else
                dateList.Add(DateTime.Today);

            foreach (DateTime date in dateList)
            {
                for (int i = 0; i < DeviceList.Count; i++)
                    if (DeviceList[i].Enabled && (!DeviceList[i].FirstFullDay.HasValue || DeviceList[i].FirstFullDay <= date))
                        ReadFromOwl(DeviceList[i], date);
            }
            ExtractHasRun = true;
            lastDay = DateTime.Today;
        }

        public override bool DoWork()
        {
            String state = "start";
            int res = 0;

            try
            {
                if (!DevicesEnabled || !InvertersRunning)
                    return true;  // if all devices disabled or inverters not running always succeed

                state = "before RunExtracts";
                RunExtracts();

                state = "before FindNewStartDate";
                DateTime? newNextFileDate = FindNewStartDate();
                if ((NextFileDate != newNextFileDate) && (newNextFileDate != null))
                    NextFileDate = newNextFileDate;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DoWork", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            return res > 0;
        }

    }
}

