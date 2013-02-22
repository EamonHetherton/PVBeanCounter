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
using GenericConnector;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using Conversations;
using DeviceStream;
using DeviceDataRecorders;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class MeterInverterManager : InverterManager
    {
        private int IntervalsInDay;
        private int?[] MeterIds;

        public override String InverterManagerType
        {
            get
            {
                return "Meter";
            }
        }

        public MeterInverterManager(GenThreadManager genThreadManager, int imid,
            InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager)
        {
            IntervalsInDay = 24 * 3600 / IntervalSeconds;
            // create list of meter ids to match entries in MeterManagerList
            MeterIds = new int?[GlobalSettings.ApplicationSettings.MeterManagerList.Count];
            for (int i = 0; i < MeterIds.GetLength(0); i++)
                MeterIds[i] = null;

            System.Threading.Thread.Sleep(20000);
        }

        private int? GetMeterId(String meterType, int instanceNo)
        {
            GenConnection con = null;
            int? id = null;

            try
            {
                con = GlobalSettings.TheDB.NewConnection();

                String cmdStr =
                    "select Id " +
                    "from meter " +
                    "where MeterType = @MeterType " +
                    "and InstanceNo = @InstanceNo ";

                GenCommand cmd = new GenCommand(cmdStr, con);
                cmd.AddParameterWithValue("@MeterType", meterType);
                cmd.AddParameterWithValue("@InstanceNo", instanceNo);
                GenDataReader dataReader = (GenDataReader)cmd.ExecuteReader();

                if (dataReader.Read())
                    id = dataReader.GetInt32(0);

                dataReader.Close();

            }
            catch (Exception e)
            {
                LogMessage("GetMeterId", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
            return id;
        }


        private void PopulateEmptyReadings(EnergyReadingSet readingSet, DateTime day)
        {
            EnergyReading reading;

            readingSet.Readings.Clear();
            // create a full day of readings
            for (int interval = 0; interval < IntervalsInDay; interval++)
            {
                reading = new EnergyReading(0, day + TimeSpan.FromSeconds((interval + 1) * IntervalSeconds), 0, IntervalSeconds, false);                
                reading.EnergyDelta = 0.0;
                reading.MinPower = 0;
                reading.MaxPower = 0;                
                readingSet.Readings.Add(reading);
            }
        }

        private EnergyReading FindReadingInSet(EnergyReadingSet readingSet, DateTime time)
        {
            foreach (EnergyReading reading in readingSet.Readings)
            {
                if (time == reading.OutputTime)
                    return reading;

                if (time < reading.OutputTime)
                    if (time > (reading.OutputTime - TimeSpan.FromSeconds(IntervalSeconds)))
                        return reading;
                    else
                        return null;
            }
            return null;
        }

        private static String SelectMeterData =
            "select r.ReadingTime, r.Duration, r.Energy, r.Calculated, r.MinPower, r.MaxPower, r.Temperature " +
            "from meterreading r " +
            "where r.Meter_Id = @MeterId " +
            "and r.Appliance = @Appliance " +
            "and r.ReadingTime > @StartTime " +
            "and r.ReadingTime <= @EndTime " +
            "order by r.ReadingTime ";

        private void UpdateApplianceOutputHistory(GenCommand cmd, DateTime day, String yieldAppliance, EnergyReadingSet readingSet)
        {
            bool readerOpen = false;
            GenDataReader dr = null;

            try
            {
                if (cmd.Parameters.Count == 1)
                {
                    cmd.AddParameterWithValue("@Appliance", Convert.ToInt32(yieldAppliance));
                    cmd.AddParameterWithValue("@StartTime", day.Date);
                    cmd.AddParameterWithValue("@EndTime", day.Date + TimeSpan.FromDays(2.0));
                }
                else
                {
                    cmd.Parameters["@Appliance"].Value = Convert.ToInt32(yieldAppliance);
                    cmd.Parameters["@StartTime"].Value = day.Date;
                    cmd.Parameters["@EndTime"].Value = day.Date + TimeSpan.FromDays(2.0);
                }

                dr = (GenDataReader)cmd.ExecuteReader();

                readerOpen = true;

                Int32 minPower;
                Int32 maxPower;

                DateTime lastTime = day + TimeSpan.FromDays(1.0);

                EnergyReading reading = null;

                Double threshold;

                if (InverterManagerSettings.YieldThreshold == null)
                    threshold = 0.010;
                else
                    threshold = InverterManagerSettings.YieldThreshold.Value / 1000.0;

                while (dr.Read())
                {
                    DateTime readingEndTime = dr.GetDateTime("ReadingTime");
                    int readingDuration = dr.GetInt32("Duration");
                    DateTime readingStartTime = readingEndTime - TimeSpan.FromSeconds(readingDuration);

                    //discard at end of record set
                    if (readingStartTime >= lastTime)
                        break;
                    //skip at start of record set
                    if (readingEndTime <= day)
                        continue;

                    DateTime lastIntervalTime;
                    int lastInterval;
                    if (readingEndTime <= lastTime)
                    {
                        lastInterval = (int)(readingEndTime.TimeOfDay.TotalSeconds - 1) / IntervalSeconds;
                        lastIntervalTime = readingEndTime.Date + TimeSpan.FromSeconds((lastInterval + 1) * IntervalSeconds);
                    }
                    else
                    {
                        lastInterval = (int)(lastTime.TimeOfDay.TotalSeconds - 1) / IntervalSeconds;
                        lastIntervalTime = readingEndTime.Date + TimeSpan.FromSeconds((lastInterval + 1) * IntervalSeconds);
                    }

                    DateTime firstIntervalTime;
                    int firstInterval;
                    if (readingStartTime > day)
                    {
                        firstInterval = (int)(readingStartTime.TimeOfDay.TotalSeconds) / IntervalSeconds;
                        firstIntervalTime = readingStartTime.Date + TimeSpan.FromSeconds((firstInterval + 1) * IntervalSeconds);
                    }
                    else
                    {
                        firstInterval = 0;
                        firstIntervalTime = readingStartTime.Date + TimeSpan.FromSeconds((firstInterval + 1) * IntervalSeconds);
                    }

                    Double readingEnergy;
                    if (dr.IsDBNull("Calculated"))
                        readingEnergy = dr.GetDouble("Energy");
                    else
                        readingEnergy = dr.GetDouble("Calculated");

                    Double? temperature;
                    if (!InverterManagerSettings.UseMeterTemperature || dr.IsDBNull("Temperature"))
                        temperature = null;
                    else
                        temperature = dr.GetDouble("Temperature");

                    // calculate power for this reading
                    Int32 readingPower = (Int32)(readingEnergy * 3600.0 * 1000.0 / readingDuration);

                    // set to zero if below the yield noise threshold
                    if (readingPower < threshold)
                    {
                        readingPower = 0;
                        readingEnergy = 0.0;
                        minPower = readingPower;
                        maxPower = readingPower;
                    }
                    else
                    {
                        if (dr.IsDBNull("MinPower"))
                            minPower = readingPower;
                        else
                            minPower = dr.GetInt32("MinPower");
                        if (dr.IsDBNull("MaxPower"))
                            maxPower = readingPower;
                        else
                            maxPower = dr.GetInt32("MaxPower");
                    }

                    if (readingEnergy > 0.0)
                    {
                        for (int interval = firstInterval; interval <= lastInterval; interval++)
                        {
                            DateTime intervalEndTime = day + TimeSpan.FromSeconds((interval + 1) * IntervalSeconds);
                            DateTime intervalStartTime = intervalEndTime - TimeSpan.FromSeconds(IntervalSeconds);

                            reading = FindReadingInSet(readingSet, intervalEndTime);

                            if (reading != null)
                            {
                                int seconds = IntervalSeconds;
                                if (readingStartTime > intervalStartTime)
                                    seconds -= (int)(readingStartTime - intervalStartTime).TotalSeconds;
                                if (readingEndTime < intervalEndTime)
                                    seconds -= (int)(intervalEndTime - readingEndTime).TotalSeconds;

                                Double intervalEnergy = readingEnergy * seconds / readingDuration;
                                reading.EnergyDelta += intervalEnergy;

                                reading.Temperature = temperature;

                                if (minPower < reading.MinPower || reading.MinPower == 0.0)
                                    reading.MinPower = minPower;
                                if (maxPower > reading.MaxPower)
                                    reading.MaxPower = maxPower;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogMessage("UpdateApplianceOutputHistory", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (readerOpen)
                {
                    dr.Close();
                    dr.Dispose();
                    dr = null;
                }
            }
        }

        protected override int ExtractYield()
        {
            String stage = "Initial";
            GenConnection con = null;
            
            int res = 0;

            try
            {
                stage = "Get connection";
                con = GlobalSettings.TheDB.NewConnection();
                // one reading set per inverter
                List<EnergyReadingSet> readingSets = new List<EnergyReadingSet>();

                // create one reading set per inverter
                int i = 0;
                foreach (MeterManagerSettings mmSettings in GlobalSettings.ApplicationSettings.MeterManagerList)
                {
                    stage = "Get MeterId";
                    // store meter id at first pass to avoid duplicate queries
                    if (MeterIds[i] == null)
                        MeterIds[i] = GetMeterId(mmSettings.ManagerType, mmSettings.InstanceNo);
                    if (MeterIds[i] != null)
                        foreach (MeterApplianceSettings app in (mmSettings.ApplianceList))
                        {
                            stage = "Build Inverter List";
                            if (app.Inverter != "")
                            {
                                EnergyReadingSet readingSet = null;

                                // locate existing inverter reading set
                                foreach (EnergyReadingSet rs in readingSets)
                                    if (rs.Device.SerialNo == app.Inverter)
                                    {
                                        readingSet = rs;
                                        break;
                                    }

                                // create new inverter reading set
                                if (readingSet == null)
                                {
                                    IDevice inverter = GetKnownDevice(con, 0, mmSettings.LongName, "Unknown", app.Inverter);
                                    readingSet = new EnergyReadingSet(inverter, 100);
                                    readingSets.Add(readingSet);
                                }
                            }
                        }
                    i++;
                }

                i = 0;

                foreach (MeterManagerSettings mmSettings in GlobalSettings.ApplicationSettings.MeterManagerList)
                {
                    stage = "Meter loop";
                    if (MeterIds[i] != null)
                    {
                        stage = "New command";
                        GenCommand cmd = new GenCommand(SelectMeterData, con);
                        cmd.AddParameterWithValue("@MeterId", MeterIds[i]);
                        foreach (MeterApplianceSettings app in (mmSettings.ApplianceList))
                        {
                            stage = "Appliance loop";
                            if (app.IsInverterYield)
                            {
                                EnergyReadingSet readingSet = null;

                                // locate existing inverter reading set
                                stage = "Locate readingset";
                                foreach (EnergyReadingSet rs in readingSets)
                                    if (rs.Device.SerialNo == app.Inverter)
                                    {
                                        /*
                                        if (!rs.Device.Identity.DeviceId.HasValue)
                                        {
                                            string siteId;
                                            rs.Device.Identity.DeviceId = InverterDataRecorder.GetInverterId(
                                                rs.Device.Identity.Make, rs.Device.Identity.Model, rs.Device.Identity.SerialNo,
                                                con, true, out siteId);
                                        }
                                        */
                                        readingSet = rs;
                                        break;
                                    }

                                // add entries for day / appliance to inverter reading set
                                    
                                if (readingSet != null)
                                {
                                    stage = "Incomplete days";
                                    List<DateTime> dateList;
                                    dateList = InverterDataRecorder.FindInverterIncompleteDays(readingSet.Device);
                                    if (!dateList.Contains(DateTime.Today))
                                        dateList.Add(DateTime.Today);

                                    foreach (DateTime day in dateList)
                                    {
                                        stage = "Empty readings";
                                        PopulateEmptyReadings(readingSet, day);
                                        stage = "Call update";
                                        UpdateApplianceOutputHistory(cmd, day, app.ApplianceNo, readingSet);
                                        stage = "HistoryUpdater";
                                        res += InverterDataRecorder.HistoryUpdater.UpdateReadingSet(readingSet, con);
                                    }

                                    stage = "Update dates";
                                    DateTime? newNextFileDate = InverterDataRecorder.FindNewInverterStartDate(readingSet.Device);

                                    if ((readingSet.Device.NextFileDate != newNextFileDate) && (newNextFileDate != null))
                                    {
                                        InverterDataRecorder.UpdateNextInverterFileDate(readingSet.Device, newNextFileDate.Value);
                                        readingSet.Device.NextFileDate = newNextFileDate;
                                    }
                                }
                                else
                                    LogMessage("ExtractYield", "Inverter has been lost - careless - Appliance: " +
                                        app.ApplianceNo + " - Inverter: " + app.Inverter, LogEntryType.ErrorMessage);
                            }
                        }
                    }
                    i++;
                }
            }
            catch (Exception e)
            {
                LogMessage("ExtractYield", "Stage - " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
            return res;
        }

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(300); } }

        public override TimeSpan? StartHourOffset { get { return TimeSpan.FromSeconds(0.0); } }

    }
}
