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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;
using DeviceDataRecorders;
using Device;
using Algorithms;

namespace PVInverterManagement
{
    public class Inverter : ActiveDevice
    {
        private bool IdentityLoaded = false;

        private InverterAlgorithm InverterAlgorithm { get { return (InverterAlgorithm)DeviceAlgorithm; } }

        public Inverter(ICommunicationDeviceManager deviceManager, DeviceManagerDeviceSettings deviceSettings, IProtocol protocol)
            : base(deviceManager, deviceSettings, protocol, new InverterAlgorithm(deviceManager, deviceSettings, protocol) )
        {
            DeviceSettings = deviceSettings;
            ResetDevice();

            Address = DeviceSettings.Address;
        }

        public override DateTime NextRunTime 
        { 
            get 
            { 
                DateTime nextTime = LastRunTime + QueryInterval;
                TimeSpan? inverterStart = GlobalSettings.ApplicationSettings.InverterStartTime;
                if (inverterStart.HasValue && inverterStart.Value > nextTime.TimeOfDay)
                    return DateTime.Today + inverterStart.Value;
                
                TimeSpan? inverterStop = GlobalSettings.ApplicationSettings.InverterStopTime;
                if (inverterStop.HasValue && inverterStop.Value < nextTime.TimeOfDay)
                        return DateTime.Today + inverterStart.Value + TimeSpan.FromDays(1.0);

                return nextTime;
            } 
        }

        public override PVSettings.DeviceType DeviceType { get { return PVSettings.DeviceType.Inverter; } }

        private void ResetDevice()
        {
            IdentityLoaded = false;

            Address = DeviceSettings.Address;

            Make = "";
            Model = "";
            SerialNo = "";

            InverterAlgorithm.SetModel(Make);
            InverterAlgorithm.SetManufacturer(Model);
            InverterAlgorithm.SetSerialNo(SerialNo);

            ClearAttributes();
        }

        public override bool DoExtractReadings()
        {
            if (FaultDetected)
                return false;

            bool res = false;
            bool alarmFound = false;
            bool errorFound = false;
            String stage = "Identity";
            try
            {
                ClearAttributes();

                if (!IdentityLoaded)
                {
                    res = InverterAlgorithm.ExtractIdentity();                   
                    if (!res)
                        return false;

                    Make = InverterAlgorithm.Manufacturer.Trim();
                    Model = InverterAlgorithm.Model.Trim();
                    SerialNo = InverterAlgorithm.SerialNo.Trim();
                    if (SerialNo == "")
                        SerialNo = DefaultSerialNo;
                    if (SerialNo == "")
                    {
                        LogMessage("DoExtractReadings - Identity - Manufacturer: " + Make
                            + " - Model: " + Model
                            + " : No Serial Number - Cannot record", LogEntryType.ErrorMessage);
                        return false;
                    }
                    IdentityLoaded = true;

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("DoExtractReadings - Identity - Manufacturer: " + Make
                            + " - Model: " + Model
                            + " - SerialNo: " + SerialNo
                            + " - Energy Margin: " + InverterAlgorithm.EnergyMargin, LogEntryType.Trace);
                }                

                stage = "Reading";

                DateTime curTime = DateTime.Now;
                bool dbWrite = (LastRecordTime == null
                    || DataRecorder.IntervalCompare(DatabaseInterval, LastRecordTime.Value, curTime) != 0);
                res = InverterAlgorithm.ExtractReading(dbWrite, ref alarmFound, ref errorFound);
                if (!res)
                    return false;

                TimeSpan duration;
                try
                {
                    duration = EstimateEnergy((double)InverterAlgorithm.PowerAC1.Value, curTime);
                }
                catch (Exception e)
                {
                    LogMessage("DoExtractReadings - Error calculating EstimateEnergy - probably no PowerAC retrieved - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    return false;
                }
              
                if (dbWrite)
                {
                    DeviceReading reading = new DeviceReading(curTime);

                    reading.EnergyToday = (double?)InverterAlgorithm.EnergyTodayAC;
                    reading.EnergyTotal = (double?)InverterAlgorithm.EnergyTotalAC;
                    if (reading.EnergyTotal.HasValue && InverterAlgorithm.EnergyTotalACHigh.HasValue)
                        reading.EnergyTotal += (Double)(InverterAlgorithm.EnergyTotalACHigh.Value * 65536);
                    reading.PowerAC = (float?)InverterAlgorithm.PowerAC1;
                    reading.Mode = (Int16?)InverterAlgorithm.Status;
                    reading.PowerPV = (float?)InverterAlgorithm.PowerPV;
                    reading.VoltsPV1 = (double?)InverterAlgorithm.VoltsPV1;
                    reading.CurrentPV1 = (double?)InverterAlgorithm.CurrentPV1;
                    reading.VoltsPV2 = (double?)InverterAlgorithm.VoltsPV2;
                    reading.CurrentPV2 = (double?)InverterAlgorithm.CurrentPV2;
                    reading.FreqAC = (double?)InverterAlgorithm.Frequency;
                    reading.VoltsAC = (double?)InverterAlgorithm.VoltsAC1;
                    reading.CurrentAC = (double?)InverterAlgorithm.CurrentAC1;
                    reading.Hours = (uint?)InverterAlgorithm.TimeTotal;
                    if (reading.Hours.HasValue && InverterAlgorithm.TimeTotalHigh.HasValue)
                        reading.Hours += (UInt32)(InverterAlgorithm.TimeTotalHigh.Value * 65536);
                    reading.Temp = (double?)InverterAlgorithm.Temperature;
                    reading.ErrorCode = (uint?)InverterAlgorithm.ErrorCode;
                    if (InverterAlgorithm.ErrorCodeHigh.HasValue)
                        if (reading.ErrorCode.HasValue)
                            reading.ErrorCode += (UInt32)(InverterAlgorithm.ErrorCodeHigh.Value * 65536);
                        else
                            reading.ErrorCode = (UInt32)(InverterAlgorithm.ErrorCodeHigh.Value * 65536);                    

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("DoExtractReadings - Reading - EnergyToday: " + reading.EnergyToday
                            + " - EnergyTotal: " + reading.EnergyTotal
                            + " - EstEnergy: " + EstEnergy
                            + " - PowerAC: " + reading.PowerAC
                            + " - Mode: " + reading.Mode
                            + " - PowerPV: " + reading.PowerPV
                            + " - VoltsPV1: " + reading.VoltsPV1
                            + " - CurrentPV1: " + reading.CurrentPV1
                            + " - VoltsPV2: " + reading.VoltsPV2
                            + " - CurrentPV2: " + reading.CurrentPV2
                            + " - FreqAC: " + reading.FreqAC
                            + " - VoltsAC: " + reading.VoltsAC
                            + " - CurrentAC: " + reading.CurrentAC
                            + " - TimeTotal: " + reading.Hours
                            + " - Temperature: " + reading.Temp
                            , LogEntryType.Trace);

                    stage = "record";
                
                    //stage = "RecordReading";
                    DeviceManager.DataRecorder.RecordReading(this, reading);
                }

                if (EmitEvents)
                {
                    stage = "energy";
                    DeviceManager.ManagerManager.EnergyEvents.NewEnergyReading(PVSettings.HierarchyType.Yield,
                        DeviceManager.ThreadName, SerialNo, "", curTime,
                        null, (int)InverterAlgorithm.PowerAC1.Value, (int)duration.TotalSeconds);
                }

                stage = "errors";
                if (alarmFound)
                    InverterAlgorithm.LogAlarm("Reading", curTime, DeviceManager);
                if (errorFound)
                    InverterAlgorithm.LogError("Reading", curTime, DeviceManager);
                
            }
            catch (Exception e)
            {
                LogMessage("DoExtractReadings - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            return res;
        }
    }
}

