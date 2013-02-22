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
using GenericConnector;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class PVHistoryUpdate
    {
        private InverterDataRecorder InverterDataRecorder;
   
        IEvents EnergyEvents;

        public PVHistoryUpdate(IDataRecorder inverterDataRecorder)
        {
            InverterDataRecorder = (InverterDataRecorder)inverterDataRecorder;
            EnergyEvents = InverterDataRecorder.InverterManagerManager.EnergyEvents;
        }

        private void LogMessage(String component, String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage("PVHistoryUpdate." + component, message, logEntryType);
        }

        private int DeleteOldRecords(Int32 inverterId, DateTime fromTime, DateTime toTime, GenConnection connection)
        {
            int result;
            GenCommand cmd = null;

            string delCmd =
                "delete from outputhistory " +
                "where Inverter_Id = @InverterId " +
                "and OutputTime >= @FromTime " +
                "and OutputTime < @ToTime " +
                ";";
            try
            {
                cmd = new GenCommand(delCmd, connection);
                cmd.AddParameterWithValue("@InverterId", inverterId);
                cmd.AddParameterWithValue("@FromTime", fromTime);
                cmd.AddParameterWithValue("@ToTime", toTime);
                result = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "DeleteOldRecords - Error deleting existing outputhistory rows: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }

            return result;
        }

        private void InsertReading(Int32 inverterId, GenCommand cmd, Device.EnergyReading reading, bool useMinMaxPower)
        {
            int result;

            try
            {
                if (cmd.Parameters.Count == 0)
                {
                    cmd.AddParameterWithValue("@InverterId", inverterId);
                    cmd.AddParameterWithValue("@ReadingTime", reading.OutputTime);
                    cmd.AddRoundedParameterWithValue("@ReadingKwh", reading.KWHOutput, 3);
                    cmd.AddParameterWithValue("@Duration", reading.Duration);
                    if (useMinMaxPower)
                    {
                        cmd.AddRoundedParameterWithValue("@MinPower", reading.MinPower.Value, 3);
                        cmd.AddRoundedParameterWithValue("@MaxPower", reading.MaxPower.Value, 3);
                    }
                    cmd.AddRoundedParameterWithValue("@Temperature", reading.Temperature, 1);
                }
                else
                {
                    cmd.Parameters["@InverterId"].Value = inverterId;
                    cmd.Parameters["@ReadingTime"].Value = reading.OutputTime;
                    cmd.Parameters["@ReadingKwh"].Value = Math.Round(reading.KWHOutput, 3);
                    cmd.Parameters["@Duration"].Value = reading.Duration;
                    if (useMinMaxPower)
                    {
                        cmd.Parameters["@MinPower"].Value = Math.Round(reading.MinPower.Value, 3);
                        cmd.Parameters["@MaxPower"].Value = Math.Round(reading.MaxPower.Value, 3);
                    }
                    if (reading.Temperature.HasValue)
                        cmd.Parameters["@Temperature"].Value = Math.Round(reading.Temperature.Value, 1);
                    else
                        cmd.Parameters["@Temperature"].Value = DBNull.Value;
                }

                result = cmd.ExecuteNonQuery();

                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("InsertReading", "KWHOutput: " + reading.KWHOutput +
                        " - Time: " + reading.OutputTime +
                        " - MinPower: " + reading.MinPower +
                        " - MaxPower: " + reading.MaxPower, LogEntryType.Trace);
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedDBError, "InsertReading - Error inserting a new reading: " + e.Message, e);
            }

            if (result != 1)
                throw new PVException(PVExceptionType.UnexpectedDBError, "InsertReading - Expected to insert 1 row, result was " + result);
        }

        private GenCommand GetInsertReadingCommand(GenConnection connection, bool useMinMaxPower)
        {
            GenCommand cmd;

            string insCmd;

            if (useMinMaxPower)
                insCmd =
                "insert into outputhistory (Inverter_Id, OutputTime, OutputKwh, Duration, MinPower, MaxPower, Temperature) " +
                "values ( @InverterId, @ReadingTime, @ReadingKwh, @Duration, @MinPower, @MaxPower, @Temperature ) ";
            else
                insCmd =
                "insert into outputhistory (Inverter_Id, OutputTime, OutputKwh, Duration, Temperature) " +
                "values ( @InverterId, @ReadingTime, @ReadingKwh, @Duration, @Temperature ) ";

            cmd = new GenCommand(insCmd, connection);
            return cmd;
        }

        public int UpdateReadingSet(Device.EnergyReadingSet readingSet, GenConnection connection, bool emitEvent = true) 
            // emitEvent is false when the inverter manager sends its own events at a finer resolution
        {   
            DateTime? fromTime = null;
            DateTime? toTime = null;
            bool useMinMaxPower = true;

            Int32 inverterId;

            int count = 0;
            String siteId = null;

            if (readingSet.Device.DeviceId == null)
            {
                InverterDataRecorder.InsertInverter(readingSet.Device.Make, readingSet.Device.Model, readingSet.Device.SerialNo, connection);
                inverterId = InverterDataRecorder.GetInverterId(readingSet.Device.Make, readingSet.Device.Model, readingSet.Device.SerialNo, connection, true, out siteId);
                readingSet.Device.DeviceId = inverterId;
            }
            else
            {
                inverterId = readingSet.Device.DeviceId.Value;
                siteId = readingSet.Device.SiteId;
            }

            foreach (Device.EnergyReading reading in readingSet.Readings)
            {
                if (fromTime == null || reading.OutputTime < fromTime)
                    fromTime = reading.OutputTime;
                if (toTime == null || reading.OutputTime > toTime)
                    toTime = reading.OutputTime;
                useMinMaxPower &= reading.MinPower.HasValue;
                useMinMaxPower &= reading.MaxPower.HasValue;
            }            

            DeleteOldRecords(inverterId, fromTime.Value.Date, toTime.Value.Date + TimeSpan.FromDays(1), connection);

            GenCommand cmd = GetInsertReadingCommand(connection, useMinMaxPower);

            foreach (Device.EnergyReading reading in readingSet.Readings)
            {
                if (reading.KWHOutput > 0.0)
                {
                    InsertReading(inverterId, cmd, reading, useMinMaxPower);
                    count++;
                }
            }

            InverterDataRecorder.SetDeviceUpdated(inverterId, siteId);

            if (emitEvent && GlobalSettings.ApplicationSettings.EmitEvents && readingSet.Readings.Count > 0 && fromTime.Value.Date == DateTime.Today)
            {
                Device.EnergyReading reading = readingSet.Readings.Last();
                EnergyEvents.NewEnergyReading(PVSettings.HierarchyType.Yield,
                    InverterDataRecorder.InverterManagerSettings.Description, readingSet.Device.SerialNo, "", reading.OutputTime, reading.KWHOutput, null, reading.Duration);
            }

            cmd.Dispose();
            
            return count;
        }

        private void CheckReadingSet(Device.EnergyReadingSet readingSet)
        {
            /*
            * This detects a specific dataset problem found in some SMA sourced datasets where there is a single missing reading
            * followed by a catchup reading with a high power level. pvoutput can reject this catchup as having too high a power level
            * This code will detect the scenario and replace the single missing reading
            */
            int prevDuration = 0;

            for (int i = 0; i < readingSet.Readings.Count; i++)
            {
                Device.EnergyReading reading = readingSet.Readings[i];

                if (i > 0)
                {
                    int duration = reading.Duration;

                    // only act on jumps from 0 to > 1.5KW 
                    // this should prevent removal of zero values caused by dark cloud - a natural jump from 0 to 1.5KW would be very unusual
                    // only act on single missing values where duration is double expected
                    if (i > 1 && duration == (prevDuration * 2) && reading.KWOutput > 1.5)
                    {
                        reading.KWOutput = reading.KWOutput / 2.0;
                        reading.Duration = prevDuration;

                        Device.EnergyReading extraReading = new Device.EnergyReading();
                        extraReading.OutputTime = reading.OutputTime.AddSeconds(-prevDuration);
                        extraReading.KWOutput = reading.KWOutput;
                        extraReading.Duration = prevDuration;
                        readingSet.Readings.Insert(i, extraReading);
                        i++;
                    }
                }
                prevDuration = reading.Duration;
            }
        }

        private Boolean UpdateHistory(String fileName, GenConnection connection)
        {
            SunnyExplorerCSVParser csvParser = new SunnyExplorerCSVParser(); 
            
            System.Collections.Generic.List<Device.EnergyReadingSet> readings; 

            Boolean res = csvParser.ExtractRecords(fileName, out readings);

            if (res)
            {
                foreach (Device.EnergyReadingSet readingSet in readings)
                {
                    CheckReadingSet(readingSet);
                    UpdateReadingSet(readingSet, connection);
                }
                return true;
            }
            return false;            
        }

        public int UpdateFromDirectory(String inDirectory, String filePattern, String moveToDirectory, GenConnection connection)
        {
            int res = 0;
            DirectoryInfo directoryInInfo = new DirectoryInfo(inDirectory);

            if (!directoryInInfo.Exists)
                throw new PVException(PVExceptionType.DirectoryMissing, "UpdateFromDirectory - Directory: " + inDirectory + " :does not exist");

            DirectoryInfo directoryMoveToInfo = new DirectoryInfo(moveToDirectory);
            if (!directoryMoveToInfo.Exists)
                try
                {
                    directoryMoveToInfo.Create();
                }
                catch (Exception e)
                {
                    throw new PVException(PVExceptionType.CannotCreateDirectory, 
                        "UpdateFromDirectory - Error creating directory: " + moveToDirectory + " - Exception: " + e.Message, e);
                }

           foreach (FileInfo fileInfo in directoryInInfo.EnumerateFiles(filePattern))
            {
                bool updated = false;
                
                updated = UpdateHistory(fileInfo.FullName, connection);             

                if (updated)
                {
                    res++;
                    try
                    {
                        String newName = System.IO.Path.Combine(directoryMoveToInfo.FullName, fileInfo.Name);
                        FileInfo moveToFileInfo = new FileInfo(newName);
                        if (moveToFileInfo.Exists)
                            moveToFileInfo.Delete();
                        moveToFileInfo = null;
                            
                        fileInfo.MoveTo(newName);
                    }
                    catch (Exception e)
                    {
                        throw new PVException(PVExceptionType.CannotMoveFile, 
                            "UpdateFromDirectory - Error moving file: " + fileInfo.FullName + " :to: " + moveToDirectory + " - Exception: " + e.Message, e);
                    }
                }
            }

            return res;
        }
    }

}
