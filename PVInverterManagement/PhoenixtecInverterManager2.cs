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
using Conversations;
using DeviceStream;
using GenThreadManagement;
using DeviceDataRecorders;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public abstract class PhoenixtecInverterManager2 : SerialInverterManager
    {
        public enum DataUnits
        {
            DegreesCentegrade,
            DegreesFarenheit,
            KiloWattHours,
            WattHours,
            KiloWatts,
            Watts,
            Volts,
            Amps,
            mOhms,
            Ohms,
            Hertz,
            Hours,
            Identifier
        }

        public struct DataInfo
        {
            public byte Id;
            public byte AlternateId;
            public double Multiplier;
            public DataUnits Units;
            public byte? Position;

            public DataInfo(byte id)
            {
                Id = id;
                AlternateId = 0xFF;
                Multiplier = 0.0;
                Units = DataUnits.Identifier;
                Position = null;
            }

            public DataInfo(byte id, DataUnits units, double multiplier = 0.1, byte alternateId = 0xFF, byte? position = null)
            {
                Id = id;
                AlternateId = alternateId;
                Multiplier = multiplier;
                Units = units;
                Position = position;
            }
        }

        public struct DataIds
        {
            public DataInfo Temp;
            public DataInfo EnergyToday;
            public DataInfo VoltsPV;
            public DataInfo CurrentAC;
            public DataInfo VoltsAC;
            public DataInfo FreqAC;
            public DataInfo PowerAC;
            public DataInfo ImpedanceAC;
            public DataInfo EnergyTotalHigh;
            public DataInfo EnergyTotalLow;
            public DataInfo HoursHigh;
            public DataInfo HoursLow;
            public DataInfo Mode;
            public DataInfo VoltsPV1;
            public DataInfo VoltsPV2;
            public DataInfo VoltsPV3;
            public DataInfo CurrentPV1;
            public DataInfo CurrentPV2;
            public DataInfo CurrentPV3;
            public DataInfo ErrorGV;
            public DataInfo ErrorGF;
            public DataInfo ErrorGZ;
            public DataInfo ErrorTemp;
            public DataInfo ErrorPV1;
            public DataInfo ErrorGFC1;
            public DataInfo ErrorModeHigh;
            public DataInfo ErrorModeLow;

            public DataIds(int dummy)
            {
                Temp = new DataInfo(0xFF);
                EnergyToday = new DataInfo(0xFF);
                VoltsPV = new DataInfo(0xFF);
                CurrentAC = new DataInfo(0xFF);
                VoltsAC = new DataInfo(0xFF);
                FreqAC = new DataInfo(0xFF);
                PowerAC = new DataInfo(0xFF);
                ImpedanceAC = new DataInfo(0xFF);
                EnergyTotalHigh = new DataInfo(0xFF);
                EnergyTotalLow = new DataInfo(0xFF);
                HoursHigh = new DataInfo(0xFF);
                HoursLow = new DataInfo(0xFF);
                Mode = new DataInfo(0xFF);
                VoltsPV1 = new DataInfo(0xFF);
                VoltsPV2 = new DataInfo(0xFF);
                VoltsPV3 = new DataInfo(0xFF);
                CurrentPV1 = new DataInfo(0xFF);
                CurrentPV2 = new DataInfo(0xFF);
                CurrentPV3 = new DataInfo(0xFF);
                ErrorGV = new DataInfo(0xFF);
                ErrorGF = new DataInfo(0xFF);
                ErrorGZ = new DataInfo(0xFF);
                ErrorTemp = new DataInfo(0xFF);
                ErrorPV1 = new DataInfo(0xFF);
                ErrorGFC1 = new DataInfo(0xFF);
                ErrorModeHigh = new DataInfo(0xFF);
                ErrorModeLow = new DataInfo(0xFF);
            }
        };

        protected DataIds IMDataIds;

        protected ByteVar InverterSerialNo;
        protected ByteVar InverterCapacity;
        protected ByteVar InverterFirmware;
        protected ByteVar InverterModel;
        protected ByteVar InverterManufacturer;

        public PhoenixtecInverterManager2(GenThreadManagement.GenThreadManager genThreadManager, int imid,
            InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager)
        {
            IMDataIds = InverterDataIds;
        }

        protected override void LoadLocalInverters(InverterManagerSettings imSettings)
        {
            bool sodd = HasPhoenixtecStartOfDayEnergyDefect;
            Double crazyDayStartMinutes = 0.0;
            if (sodd)
                crazyDayStartMinutes = imSettings.CrazyDayStartMinutes.HasValue ? imSettings.CrazyDayStartMinutes.Value : 90.0;
            
            IDevice gi = NewUnknownDevice(0);
            gi.HasStartOfDayEnergyDefect = sodd;
            gi.CrazyDayStartMinutes = crazyDayStartMinutes;
        }

        protected abstract DataIds InverterDataIds { get; }

        protected void SetupVariables()
        {
            InverterSerialNo = (ByteVar)Converse.GetSessionVariable("SerialNo");
            InverterCapacity = (ByteVar)Converse.GetSessionVariable("Capacity");
            InverterFirmware = (ByteVar)Converse.GetSessionVariable("Firmware");
            InverterModel = (ByteVar)Converse.GetSessionVariable("Model");
            InverterManufacturer = (ByteVar)Converse.GetSessionVariable("Manufacturer");
        }

        protected bool ExtractFormatNew()
        {
            bool result = Converse.DoConversation("req_data_format_p1");
            if (!result)
            {
                LogMessage("ExtractFormatNew", "Error in 'req_data_format_p1' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            ByteVar formatSize = (ByteVar)Converse.GetSessionVariable("FormatSize");
            if (formatSize == null)
            {
                LogMessage("ExtractFormatNew", "Variable FormatSize not found", LogEntryType.ErrorMessage);
                return false;
            }
            ByteVar formatArray = (ByteVar)Converse.GetSessionVariable("FormatArray");
            if (formatArray == null)
            {
                LogMessage("ExtractFormatNew", "Variable FormatArray not found", LogEntryType.ErrorMessage);
                return false;
            }

            UInt16 listSize = formatSize.GetByte();

            switch (listSize)
            {
                case 20:
                    break;
                case 21:
                    break;
                case 27:
                    break;
                case 45:
                    break;
                case 48:  // KLNE
                    break;
                default:
                    LogMessage("ExtractFormatNew", "Unexpected format entry count: " + listSize, LogEntryType.Trace);
                    break;
            }

            formatArray.Resize(listSize);

            result = Converse.DoConversation("req_data_format_p2");
            if (!result)
            {
                LogMessage("ExtractFormatNew", "Error in 'req_data_format_p2' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            byte[] formatByte = new byte[1];

            String formats = "";
            for (byte i = 0; i < listSize; i++)
            {
                byte p = (byte)(i * 2);
                String name = "Val" + i.ToString("00") + "Type";
                formatByte[0] = formatArray.GetByte(i);
                formats += ":" + name + ":= " + SystemServices.BytesToHex(ref formatByte, 1, " ", "") + " ";

                byte type = formatByte[0];
                if (type == IMDataIds.Temp.Id)
                    IMDataIds.Temp.Position = p;
                else if (type == IMDataIds.EnergyToday.Id)
                    IMDataIds.EnergyToday.Position = p;
                else if (type == IMDataIds.VoltsPV.Id)
                    IMDataIds.VoltsPV.Position = p;
                else if (type == IMDataIds.VoltsPV1.Id)
                    IMDataIds.VoltsPV1.Position = p;
                else if (type == IMDataIds.VoltsPV2.Id)
                    IMDataIds.VoltsPV2.Position = p;
                else if (type == IMDataIds.VoltsPV3.Id)
                    IMDataIds.VoltsPV3.Position = p;
                else if (type == IMDataIds.CurrentPV1.Id)
                    IMDataIds.CurrentPV1.Position = p;
                else if (type == IMDataIds.CurrentPV2.Id)
                    IMDataIds.CurrentPV2.Position = p;
                else if (type == IMDataIds.CurrentPV3.Id)
                    IMDataIds.CurrentPV3.Position = p;
                else if (type == IMDataIds.CurrentAC.Id)
                    IMDataIds.CurrentAC.Position = p;
                else if (type == IMDataIds.VoltsAC.Id)
                    IMDataIds.VoltsAC.Position = p;
                else if (type == IMDataIds.FreqAC.Id)
                    IMDataIds.FreqAC.Position = p;
                else if (type == IMDataIds.PowerAC.Id || type == IMDataIds.PowerAC.AlternateId)
                    IMDataIds.PowerAC.Position = p;
                else if (type == IMDataIds.ImpedanceAC.Id)
                    IMDataIds.ImpedanceAC.Position = p;
                else if (type == IMDataIds.EnergyTotalHigh.Id || type == IMDataIds.EnergyTotalHigh.AlternateId)
                    IMDataIds.EnergyTotalHigh.Position = p;
                else if (type == IMDataIds.EnergyTotalLow.Id || type == IMDataIds.EnergyTotalLow.AlternateId)
                    IMDataIds.EnergyTotalLow.Position = p;
                else if (type == IMDataIds.HoursHigh.Id || type == IMDataIds.HoursHigh.AlternateId)
                    IMDataIds.HoursHigh.Position = p;
                else if (type == IMDataIds.HoursLow.Id || type == IMDataIds.HoursLow.AlternateId)
                    IMDataIds.HoursLow.Position = p;
                else if (type == IMDataIds.Mode.Id || type == IMDataIds.Mode.AlternateId)
                    IMDataIds.Mode.Position = p;
                else if (type == IMDataIds.ErrorModeHigh.Id)
                    IMDataIds.ErrorModeHigh.Position = p;
                else if (type == IMDataIds.ErrorModeLow.Id)
                    IMDataIds.ErrorModeLow.Position = p;
            }
            LogMessage("ExtractFormatNew", "Formats: " + formats, LogEntryType.Trace);
            return true;
        }

        protected virtual DataIds CustomInitialise()
        {
            return IMDataIds;
        }

        protected override bool InitialiseInverter()
        {
            StopPortReader();
            if (!StartPortReader())
                return false;

            SetupVariables();

            IDevice device = DeviceList[0];
            device.Found = false;

            bool result = Converse.DoConversation("initialise");
            if (!result)
            {
                LogMessage("InitialiseInverter", "Error in 'initialise' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            result = Converse.DoConversation("serial");
            if (!result)
            {
                LogMessage("InitialiseInverter", "Error in 'serial' conversation", LogEntryType.ErrorMessage);               
                return false;
            }

            LogMessage("InitialiseInverter", "SerialNo: " + InverterSerialNo.ToString(), LogEntryType.Trace);

            result = Converse.DoConversation("conf_serial");
            if (!result)
            {
                LogMessage("InitialiseInverter", "Error in 'conf_serial' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            result = Converse.DoConversation("req_version");
            if (!result)
            {
                LogMessage("InitialiseInverter", "Error in 'req_version' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            LogMessage("InitialiseInverter", "Capacity: " + InverterCapacity.ToString() + " - Firmware: " + InverterFirmware.ToString() +
                " - Model: " + InverterModel.ToString() + " - Manfacturer: " + InverterManufacturer.ToString(), LogEntryType.Trace);

            if (!ExtractFormatNew())
                return false;

            IMDataIds = CustomInitialise();

            // alter default estimate margin based upon decimal places in the available energy value
            if (IMDataIds.EnergyToday.Position.HasValue)
                device.EstMargin = IMDataIds.EnergyToday.Multiplier;
            else if (IMDataIds.EnergyTotalLow.Position.HasValue)
                device.EstMargin = IMDataIds.EnergyTotalLow.Multiplier;

            device.Found = true;
            device.Phases = 1;
            device.SerialNo = InverterSerialNo.ToString().Trim();
            device.Manufacturer = InverterManufacturer.ToString().Trim();
            device.Model = InverterModel.ToString().Trim();
            device.Firmware = InverterFirmware.ToString().Trim();

            InverterInitialised = true;
            
            return true;
        }

        protected override int ExtractInverterYield(IDevice device)
        {
            UInt16 rawTemp;
            UInt16 rawEnergyToday;
            UInt16 rawVoltsPV;
            UInt16 rawCurrentAC;
            UInt16 rawVoltsAC;
            UInt16 rawFreqAC;
            UInt16 rawPowerAC;
            UInt16 rawImpedanceAC;
            UInt16 rawEnergyTotalHigh = 0;
            UInt16 rawEnergyTotalLow;
            UInt16 rawHoursHigh = 0;
            UInt16 rawHoursLow;
            UInt16 rawMode;
            UInt32 rawErrorMode;

            bool result;

            String stage = "ExtractInverterYield";
            
            try
            {                
                DeviceReading reading;
                bool timeout;

                stage = "DoConversation " + "req_data_p1";
                result = Converse.DoConversation("req_data_p1", out timeout);
                if (!result)
                {
                    if (timeout)
                        LogMessage("ExtractInverterYield", "Error in 'req_data_p1' conversation - timeout", LogEntryType.Trace);
                    else
                        LogMessage("ExtractInverterYield", "Error in 'req_data_p1' conversation - mismatch", LogEntryType.ErrorMessage);
                    InverterInitialised = false;
                    return 0;
                }

                stage = "Get " + "DataSize";

                ByteVar dataSize = (ByteVar)Converse.GetSessionVariable("DataSize");
                if (dataSize == null)
                {
                    LogMessage("ExtractInverterYield", "Variable DataSize not found", LogEntryType.ErrorMessage);
                    return 0;
                }

                stage = "Get " + "DataArray";

                ByteVar dataArray = (ByteVar)Converse.GetSessionVariable("DataArray");
                if (dataArray == null)
                {
                    LogMessage("ExtractInverterYield", "Variable DataArray not found", LogEntryType.ErrorMessage);
                    return 0;
                }

                stage = "GetByte";

                UInt16 listSize = dataSize.GetByte();

                switch (listSize)
                {
                    case 40:
                        break;
                    case 42:
                        break;
                    case 54:
                        break;
                    case 90:
                        break;
                    default:
                        LogMessage("ExtractInverterYield", "Unexpected data entry count: " + listSize, LogEntryType.Trace);
                        break;
                }

                stage = "Resize";

                dataArray.Resize(listSize);

                stage = "DoConversation " + "req_data_p2";

                result = Converse.DoConversation("req_data_p2", out timeout);
                if (!result)
                {
                    if (timeout)
                        LogMessage("ExtractInverterYield", "Error in 'req_data_p2' conversation - timeout", LogEntryType.ErrorMessage);
                    else
                        LogMessage("ExtractInverterYield", "Error in 'req_data_p2' conversation - mismatch", LogEntryType.ErrorMessage);
                    InverterInitialised = false;
                    return 0;
                }

                int valueCount = listSize / 2;

                stage = "Set Values";
                if (GlobalSettings.SystemServices.LogTrace)
                    for (int i = 0; i < valueCount; i++)
                    {
                        LogMessage("ExtractInverterYield", "Val[" + i + "]: " + dataArray.GetUInt16(i * 2).ToString(), LogEntryType.Trace);
                    }

                if (IMDataIds.Temp.Position.HasValue)
                {
                    rawTemp = dataArray.GetUInt16(IMDataIds.Temp.Position.Value);
                    reading.Temp = rawTemp * IMDataIds.Temp.Multiplier;
                    LogMessage("ExtractInverterYield", "Temp: " + reading.Temp.ToString(), LogEntryType.Trace);
                }
                else
                    reading.Temp = null;
                if (IMDataIds.EnergyToday.Position.HasValue)
                {
                    rawEnergyToday = dataArray.GetUInt16(IMDataIds.EnergyToday.Position.Value);
                    reading.EnergyToday = rawEnergyToday * IMDataIds.EnergyToday.Multiplier; 
                    LogMessage("ExtractInverterYield", "Energy Today: " + reading.EnergyToday.Value.ToString("##0.00"), LogEntryType.Trace);
                }
                else
                    reading.EnergyToday = null;
                if (IMDataIds.VoltsPV.Position.HasValue)
                {
                    rawVoltsPV = dataArray.GetUInt16(IMDataIds.VoltsPV.Position.Value);
                    reading.VoltsPV = rawVoltsPV * IMDataIds.VoltsPV.Multiplier;
                    LogMessage("ExtractInverterYield", "PV Volts: " + reading.VoltsPV.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.VoltsPV = null;
                if (IMDataIds.VoltsPV1.Position.HasValue)
                {
                    rawVoltsPV = dataArray.GetUInt16(IMDataIds.VoltsPV1.Position.Value);
                    reading.VoltsPV1 = rawVoltsPV * IMDataIds.VoltsPV1.Multiplier;
                    LogMessage("ExtractInverterYield", "PV Volts1: " + reading.VoltsPV1.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.VoltsPV1 = null;
                if (IMDataIds.VoltsPV2.Position.HasValue)
                {
                    rawVoltsPV = dataArray.GetUInt16(IMDataIds.VoltsPV2.Position.Value);
                    reading.VoltsPV2 = rawVoltsPV * IMDataIds.VoltsPV2.Multiplier;
                    LogMessage("ExtractInverterYield", "PV Volts2: " + reading.VoltsPV2.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.VoltsPV2 = null;
                if (IMDataIds.VoltsPV3.Position.HasValue)
                {
                    rawVoltsPV = dataArray.GetUInt16(IMDataIds.VoltsPV3.Position.Value);
                    reading.VoltsPV3 = rawVoltsPV * IMDataIds.VoltsPV3.Multiplier;
                    LogMessage("ExtractInverterYield", "PV Volts3: " + reading.VoltsPV3.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.VoltsPV3 = null;
                if (IMDataIds.CurrentPV1.Position.HasValue)
                {
                    rawCurrentAC = dataArray.GetUInt16(IMDataIds.CurrentPV1.Position.Value);
                    reading.CurrentPV1 = rawCurrentAC * IMDataIds.CurrentPV1.Multiplier;
                    LogMessage("ExtractInverterYield", "PV Amps1: " + reading.CurrentPV1.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.CurrentPV1 = null;
                if (IMDataIds.CurrentPV2.Position.HasValue)
                {
                    rawCurrentAC = dataArray.GetUInt16(IMDataIds.CurrentPV2.Position.Value);
                    reading.CurrentPV2 = rawCurrentAC * IMDataIds.CurrentPV2.Multiplier;
                    LogMessage("ExtractInverterYield", "PV Amps2: " + reading.CurrentPV2.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.CurrentPV2 = null;
                if (IMDataIds.CurrentPV3.Position.HasValue)
                {
                    rawCurrentAC = dataArray.GetUInt16(IMDataIds.CurrentPV3.Position.Value);
                    reading.CurrentPV3 = rawCurrentAC * IMDataIds.CurrentPV3.Multiplier;
                    LogMessage("ExtractInverterYield", "PV Amps3: " + reading.CurrentPV3.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.CurrentPV3 = null;
                if (IMDataIds.CurrentAC.Position.HasValue)
                {
                    rawCurrentAC = dataArray.GetUInt16(IMDataIds.CurrentAC.Position.Value);
                    reading.CurrentAC = rawCurrentAC * IMDataIds.CurrentAC.Multiplier;
                    LogMessage("ExtractInverterYield", "AC Amps: " + reading.CurrentAC.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.CurrentAC = null;
                if (IMDataIds.VoltsAC.Position.HasValue)
                {
                    rawVoltsAC = dataArray.GetUInt16(IMDataIds.VoltsAC.Position.Value);
                    reading.VoltsAC = rawVoltsAC * IMDataIds.VoltsAC.Multiplier;
                    LogMessage("ExtractInverterYield", "AC Volts: " + reading.VoltsAC.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.VoltsAC = null;
                if (IMDataIds.FreqAC.Position.HasValue)
                {
                    rawFreqAC = dataArray.GetUInt16(IMDataIds.FreqAC.Position.Value);
                    reading.FreqAC = rawFreqAC * IMDataIds.FreqAC.Multiplier;
                    LogMessage("ExtractInverterYield", "AC Freq: " + reading.FreqAC.Value.ToString("##0.00"), LogEntryType.Trace);
                }
                else
                    reading.FreqAC = null;
                if (IMDataIds.PowerAC.Position.HasValue)
                {
                    rawPowerAC = dataArray.GetUInt16(IMDataIds.PowerAC.Position.Value);
                    reading.PowerAC = (float)(rawPowerAC * IMDataIds.PowerAC.Multiplier);
                    LogMessage("ExtractInverterYield", "AC Power: " + reading.PowerAC.Value.ToString(), LogEntryType.Trace);
                }
                else
                    reading.PowerAC = null;
                if (IMDataIds.ImpedanceAC.Position.HasValue)
                {
                    rawImpedanceAC = dataArray.GetUInt16(IMDataIds.ImpedanceAC.Position.Value);
                    reading.ImpedanceAC = (ushort)(rawImpedanceAC * IMDataIds.ImpedanceAC.Multiplier);
                    LogMessage("ExtractInverterYield", "AC Impedance: " + reading.ImpedanceAC.Value.ToString(), LogEntryType.Trace);
                }
                else
                    reading.ImpedanceAC = null;
                if (IMDataIds.EnergyTotalHigh.Position.HasValue)
                {
                    rawEnergyTotalHigh = dataArray.GetUInt16(IMDataIds.EnergyTotalHigh.Position.Value);
                    LogMessage("ExtractInverterYield", "Energy Total(high bits): " + rawEnergyTotalHigh.ToString(), LogEntryType.Trace);
                }
                else
                    rawEnergyTotalHigh = 0;
                if (IMDataIds.EnergyTotalLow.Position.HasValue)
                {
                    rawEnergyTotalLow = dataArray.GetUInt16(IMDataIds.EnergyTotalLow.Position.Value);
                    LogMessage("ExtractInverterYield", "Energy Total(low bits): " + rawEnergyTotalLow.ToString(), LogEntryType.Trace);
                }
                else
                    rawEnergyTotalLow = 0;
                if (IMDataIds.EnergyTotalLow.Position.HasValue)
                {
                    UInt32 tmp = rawEnergyTotalHigh;
                    tmp = (tmp << 16) + rawEnergyTotalLow;
                    reading.EnergyTotal = tmp * IMDataIds.EnergyTotalLow.Multiplier;
                    LogMessage("ExtractInverterYield", "Energy Total: " + reading.EnergyTotal.Value.ToString("###0.0"), LogEntryType.Trace);
                }
                else
                    reading.EnergyTotal = null;
                if (IMDataIds.HoursHigh.Position.HasValue)
                {
                    rawHoursHigh = dataArray.GetUInt16(IMDataIds.HoursHigh.Position.Value);
                    LogMessage("ExtractInverterYield", "Hours(high bits): " + rawHoursHigh.ToString(), LogEntryType.Trace);
                }
                else
                    rawHoursHigh = 0;
                if (IMDataIds.HoursLow.Position.HasValue)
                {
                    rawHoursLow = dataArray.GetUInt16(IMDataIds.HoursLow.Position.Value);
                    LogMessage("ExtractInverterYield", "Hours(low bits): " + rawHoursLow.ToString(), LogEntryType.Trace);
                }
                else
                    rawHoursLow = 0;
                if (IMDataIds.HoursLow.Position.HasValue)
                {
                    reading.Hours = rawHoursHigh;
                    reading.Hours = (uint)(((reading.Hours << 16) + rawHoursLow) * IMDataIds.HoursLow.Multiplier);
                    LogMessage("ExtractInverterYield", "Hours: " + reading.Hours.Value.ToString(), LogEntryType.Trace);
                }
                else
                    reading.Hours = null;
                if (IMDataIds.Mode.Position.HasValue)
                {
                    rawMode = dataArray.GetUInt16(IMDataIds.Mode.Position.Value);
                    reading.Mode = (short)(rawMode * IMDataIds.Mode.Multiplier);
                    LogMessage("ExtractInverterYield", "Mode: " + reading.Mode.Value.ToString(), LogEntryType.Trace);
                }
                else
                    reading.Mode = null;
                if (IMDataIds.ErrorModeLow.Position.HasValue)
                {
                    rawErrorMode = dataArray.GetUInt16(IMDataIds.ErrorModeLow.Position.Value);
                    
                    if (IMDataIds.ErrorModeHigh.Position.HasValue)
                    {
                        UInt32 tmp = dataArray.GetUInt16(IMDataIds.ErrorModeHigh.Position.Value);
                        rawErrorMode = ((tmp << 16) + rawErrorMode);
                    }
                    reading.ErrorCode = (uint)(rawErrorMode * IMDataIds.ErrorModeLow.Multiplier);
                    LogMessage("ExtractInverterYield", "ErrorMode: " + reading.ErrorCode.Value.ToString(), LogEntryType.Trace);
                }
                else
                    reading.ErrorCode = null;

                reading.PowerPV = null;
                reading.EstEnergy = null;
                
                stage = "CheckMinute";

                DateTime curTime = DateTime.Now;
                reading.Time = curTime;

                TimeSpan duration = device.EstimateEnergy(reading.PowerAC.Value, curTime);

                if (device.LastRecordTime == null || DeviceInfo.IntervalCompare(EffectiveDatabasePeriod, device.LastRecordTime.Value, curTime) != 0)
                {
                    stage = "RecordReading";
                    InverterDataRecorder.RecordReading(device, reading);
                }

                if (GlobalSettings.SystemServices.LogTrace)                    
                    LogMessage("ExtractInverterYield", "FreqAC: " + reading.FreqAC +
                        " - CurrentAC: " + reading.CurrentAC +
                        " - Temp: " + reading.Temp +
                        " - VoltsPV: " + reading.VoltsPV +
                        " - VoltsPV1: " + reading.VoltsPV1 +
                        " - VoltsPV2: " + reading.VoltsPV2 +
                        " - VoltsPV3: " + reading.VoltsPV3 +
                        " - CurrentPV1: " + reading.CurrentPV1 +
                        " - CurrentPV2: " + reading.CurrentPV2 +
                        " - CurrentPV3: " + reading.CurrentPV3 +
                        " - VoltsAC: " + reading.VoltsAC +
                        " - ImpedanceAC: " + reading.ImpedanceAC +
                        " - PowerAC: " + reading.PowerAC +
                        " - Mode: " + reading.Mode +
                        " - EstEnergy: " + device.EstEnergy +
                        " - EnergyToday: " + reading.EnergyToday +
                        " - EnergyTotal: " + reading.EnergyTotal +
                        " - ErrorMode: " + reading.ErrorCode, LogEntryType.Trace);   
 
                if (EmitEvents)
                    InverterManagerManager.EnergyEvents.NewEnergyReading(PVSettings.HierarchyType.Yield,
                        InverterManagerSettings.Description, device.SerialNo, "", reading.Time, 
                        null, (int)reading.PowerAC.Value, (int)duration.TotalSeconds);

                if (reading.ErrorCode != 0)
                    ErrorLogger.LogError("", reading.Time, reading.ErrorCode.ToString());
            }
            catch (Exception e)
            {
                LogMessage("ExtractInverterYield", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return 0;
            }

            return 1;
        }
    }
}
