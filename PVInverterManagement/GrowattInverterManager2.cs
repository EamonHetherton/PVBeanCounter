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
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class GrowattInverterManager2 : SerialInverterManager, IConverseCheckSum16
    {
        public override String InverterManagerType { get { return "Growatt"; } }
        public override String ConversationFileName { get { return "Growatt_Conversations_v1.txt"; } }

        public GrowattInverterManager2(GenThreadManagement.GenThreadManager genThreadManager, int imid,
            InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager)
        {           
        }

        protected override void LoadLocalConverse()
        {
            Converse = new Converse(GlobalSettings.SystemServices, this);
        }

        // IConverseCalculations implementation

        public UInt16 GetCheckSum16(List<byte[]> message)
        {
            UInt16 checkSum = 0;
            ushort pos = 0;

            foreach (byte[] bytes in message)
            {
                ushort i;
                for (i = 0; i < bytes.Length; i++)
                {
                    // LogMessage("GW GetChecksum16: i: " + i + " - length: " + bytes.Length + " - checksum: " + checkSum, LogEntryType.MeterTrace);
                    checkSum += (ushort)(((ushort)bytes[i]) ^ pos);
                    // LogMessage("GW GetChecksum16: pos: " + pos + " - byte: " + ((ushort)bytes[i]) + " - checksum: " + checkSum, LogEntryType.MeterTrace);
                    pos++;
                }
            }
            return checkSum;
        }

        // End IConverseCalculations

        private string ModelFromBytes(Byte[] bytes)
        {
            if (bytes.Length < 2)
                return "Unknown";

            String hex = SystemServices.BytesToHex(ref bytes, 1, "", "");
            LogMessage("ModelFromBytes", "Hex String: " + hex, LogEntryType.Trace);
            string result = String.Format("P{0} U{1} M{2} S{3}", hex[0], hex[1], hex[2], hex[3]);

            return result;
        }

        protected override bool InitialiseInverter()
        {
            StopPortReader();
            if (!StartPortReader())
                return false;

            byte[] addr = new byte[1];

            int found = 0;

            ByteVar InverterSerialNo = (ByteVar)Converse.GetSessionVariable("SerialNo");
            ByteVar InverterAddress = (ByteVar)Converse.GetSessionVariable("InvAdr");
            ByteVar InverterFirmware = (ByteVar)Converse.GetSessionVariable("FW");
            ByteVar InverterModel = (ByteVar)Converse.GetSessionVariable("Model");
            ByteVar InverterManufacturer = (ByteVar)Converse.GetSessionVariable("Manuf");
            ByteVar InverterPhaseInd = (ByteVar)Converse.GetSessionVariable("PhaseInd");
            ByteVar InverterVARating = (ByteVar)Converse.GetSessionVariable("VARating");
            ByteVar InverterNPV = (ByteVar)Converse.GetSessionVariable("NPV");

            foreach (DeviceInfo iInfo in DeviceList)
            {
                addr[0] = (byte)iInfo.Address;
                InverterAddress.SetBytes(ref addr, 0, 1);
                iInfo.EstMargin = 0.1; // Growatt energy readings are accurate to 0.1 kwh

                bool result = Converse.DoConversation("getSerial");
                if (!result)
                {
                    LogMessage("InitialiseInverter", "Error in 'getSerial' conversation - Address: " +
                        iInfo.Address, LogEntryType.ErrorMessage);
                    iInfo.Found = false;
                }
                else
                {

                    iInfo.Identity.SerialNo = InverterSerialNo.ToString().Trim();
                    iInfo.VARating = ((Double)InverterVARating.GetUInt32()) / 10.0;

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("InitialiseInverter", "Address: " + iInfo.Address +
                            " - SerialNo: " + iInfo.Identity.SerialNo + " - VARating: " + iInfo.VARating, LogEntryType.Trace);

                    result = Converse.DoConversation("getInverterDetails");
                    if (!result)
                    {
                        LogMessage("InitialiseInverter", "Error in 'getInverterDetails' conversation - Address: " +
                            iInfo.Address, LogEntryType.ErrorMessage);
                        iInfo.Found = false;
                    }
                    else
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            LogMessage("InitialiseInverter", "PhaseInd: " + InverterPhaseInd.GetHexString() +
                                " - VARating: " + InverterVARating.GetHexString() +
                                " - NPV: " + InverterNPV.GetHexString() +
                                " - Firmware: " + InverterFirmware.GetHexString() +
                                " - Model: " + InverterModel.GetHexString() +
                                " - Manfacturer: " + InverterManufacturer.ToString(), LogEntryType.Trace);
                        found++;
                        iInfo.Found = true;
                        iInfo.Phases = InverterPhaseInd.GetByte() == '3' ? 3 : 1;
                        iInfo.Identity.Make = InverterManufacturer.ToString().Trim();
                        iInfo.Identity.Model = ModelFromBytes(InverterModel.GetBytes());

                        if (GlobalSettings.SystemServices.LogTrace)
                            LogMessage("InitialiseInverter", "Phases: " + iInfo.Phases +
                                " - Manufacturer: " + iInfo.Identity.Make + " - Model: " + iInfo.Identity.Model, LogEntryType.Trace);
                    }
                }
            }

            InverterInitialised = true;

            return (found > 0);
        }

        protected override int ExtractInverterYield(IDevice device)
        {
            String stage = "ExtractInverterYield";
            bool result;
            try
            {
                byte[] addr = new byte[1];
                addr[0] = (byte)device.Address;
                ByteVar InverterAddress = (ByteVar)Converse.GetSessionVariable("InvAdr");
                InverterAddress.SetBytes(ref addr, 0, 1);

                bool timeout;
                String instName;
                if (device.Phases == 3)
                    instName = "getInstValues3Ph";
                else
                    instName = "getInstValues1Ph";

                stage = "DoConversation " + instName;

                result = Converse.DoConversation(instName, out timeout);
                if (!result)
                {
                    if (timeout)
                        LogMessage("ExtractInverterYield", "Error in '" + instName + "' conversation - Address: " +
                            device.Address + " - timeout", LogEntryType.Trace);
                    else
                        LogMessage("ExtractInverterYield", "Error in '" + instName + "' conversation - Address: " +
                            device.Address + " - mismatch", LogEntryType.ErrorMessage);

                    return 0;
                }

                ByteVar InverterVPV1 = (ByteVar)Converse.GetSessionVariable("VPV1");
                ByteVar InverterVPV2 = (ByteVar)Converse.GetSessionVariable("VPV2");
                ByteVar InverterPPV = (ByteVar)Converse.GetSessionVariable("PPV");
                ByteVar InverterVAC = (ByteVar)Converse.GetSessionVariable("VAC");
                ByteVar InverterIAC = (ByteVar)Converse.GetSessionVariable("IAC");
                ByteVar InverterFAC = (ByteVar)Converse.GetSessionVariable("FAC");
                ByteVar InverterPACLow = (ByteVar)Converse.GetSessionVariable("PACLo");
                ByteVar InverterPACHigh = (ByteVar)Converse.GetSessionVariable("PACHi");
                ByteVar InverterErrorRegisters = (ByteVar)Converse.GetSessionVariable("ErrorRegisters");
                ByteVar InverterErrCode = (ByteVar)Converse.GetSessionVariable("ErrCode");
                ByteVar InverterTemp = (ByteVar)Converse.GetSessionVariable("Temp");
                ByteVar InverterStatus = (ByteVar)Converse.GetSessionVariable("Status");
                //ByteVar InverterATest = (ByteVar)Converse.GetVariable("ATest");

                DateTime curTime = DateTime.Now;
                DeviceReading reading = new DeviceReading(curTime);

                reading.FreqAC = ((Double)InverterFAC.GetUInt16()) / 100.0;
                reading.CurrentAC = ((Double)InverterIAC.GetUInt16()) / 10.0;
                reading.Temp = ((Double)InverterTemp.GetUInt16()) / 10.0;
                reading.VoltsPV1 = ((Double)InverterVPV1.GetUInt16()) / 10.0;
                reading.VoltsPV2 = ((Double)InverterVPV2.GetUInt16()) / 10.0;
                reading.VoltsAC = ((Double)InverterVAC.GetUInt16()) / 10.0;
                reading.Mode = InverterStatus.GetByte();
                reading.PowerPV = (Single)(((Single)InverterPPV.GetUInt16()) / 10.0);
                reading.ErrorCode = InverterErrCode.GetUInt16();

                if (device.Phases == 3)
                {
                    UInt32 tmp = InverterPACHigh.GetUInt16();
                    reading.PowerAC = (Single)(((tmp << 16) + InverterPACLow.GetUInt16()) / 10.0); // power is in watts * 10?
                }
                else
                    reading.PowerAC = (Single)(InverterPACLow.GetUInt16() / 10.0); // power is in watts * 10?

                stage = "CheckMinute";

                
                reading.Time = curTime;

                TimeSpan duration = device.EstimateEnergy(reading.PowerAC.Value, curTime);

                bool recorded = false;

                if (device.LastRecordTime == null || DeviceInfo.IntervalCompare(EffectiveDatabasePeriod, device.LastRecordTime.Value, curTime) != 0)
                {
                    stage = "DoConversation " + "getTotals";

                    result = Converse.DoConversation("getTotals", out timeout);
                    if (!result)
                    {
                        if (timeout)
                            LogMessage("ExtractInverterYield", "Error in 'getTotals' conversation - timeout", LogEntryType.ErrorMessage);
                        else
                            LogMessage("ExtractInverterYield", "Error in 'getTotals' conversation - mismatch", LogEntryType.ErrorMessage);

                        return 0;
                    }

                    // ByteVar InverterEPVToday = (ByteVar)Converse.GetVariable("EPVToday");
                    // ByteVar InverterEPVTotal = (ByteVar)Converse.GetVariable("EPVTotal");
                    ByteVar InverterEACToday = (ByteVar)Converse.GetSessionVariable("EACToday");
                    ByteVar InverterEACTotal = (ByteVar)Converse.GetSessionVariable("EACTotal");
                    ByteVar InverterTimeTotal = (ByteVar)Converse.GetSessionVariable("TimeTotal");

                    reading.EnergyToday = InverterEACToday.GetUInt16() / 10.0;
                    reading.EnergyTotal = InverterEACTotal.GetUInt32() / 10.0;
                    reading.Hours = InverterTimeTotal.GetUInt32() / 10;

                    if (GlobalSettings.SystemServices.LogTrace)
                        LogMessage("ExtractInverterYield", "Address: " + device.Address +
                            " - EnergyToday: " + reading.EnergyToday +
                            " - EnergyTotal: " + reading.EnergyTotal, LogEntryType.Trace);

                    Double eToday = reading.EnergyToday.Value;

                    stage = "RecordReading";
                    InverterDataRecorder.RecordReading(device, reading);
                    recorded = true;
                }

                if (GlobalSettings.SystemServices.LogTrace)
                    if (recorded)
                        LogMessage("ExtractInverterYield", "Address: " + device.Address +
                            " - FreqAC: " + reading.FreqAC +
                            " - CurrentAC: " + reading.CurrentAC +
                            " - Temp: " + reading.Temp +
                            " - VoltsPV: " + reading.VoltsPV1 +
                            " - VoltsPV2: " + reading.VoltsPV2 +
                            " - PowerPV: " + reading.PowerPV +
                            " - VoltsAC: " + reading.VoltsAC +
                            " - PowerAC: " + reading.PowerAC +
                            " - Mode: " + reading.Mode +
                            " - EstEnergy: " + device.EstEnergy +
                            " - ErrCode: " + reading.ErrorCode +
                            " - Hours: " + reading.Hours +
                            " - EnergyToday: " + reading.EnergyToday +
                            " - EnergyTotal: " + reading.EnergyTotal, LogEntryType.Trace);
                    else
                        LogMessage("ExtractInverterYield", "Address: " + device.Address +
                            " - FreqAC: " + reading.FreqAC +
                            " - CurrentAC: " + reading.CurrentAC +
                            " - Temp: " + reading.Temp +
                            " - VoltsPV: " + reading.VoltsPV1 +
                            " - VoltsPV2: " + reading.VoltsPV2 +
                            " - PowerPV: " + reading.PowerPV +
                            " - VoltsAC: " + reading.VoltsAC +
                            " - PowerAC: " + reading.PowerAC +
                            " - Mode: " + reading.Mode +
                            " - EstEnergy: " + device.EstEnergy +
                            " - ErrCode: " + reading.ErrorCode, LogEntryType.Trace);
               
                if (EmitEvents)
                    InverterManagerManager.EnergyEvents.NewEnergyReading(PVSettings.HierarchyType.Yield,
                        InverterManagerSettings.Description, device.SerialNo, "", reading.Time, 
                        null, (int)reading.PowerAC.Value, (int)duration.TotalSeconds);

                if (reading.ErrorCode != 0) 
                    ErrorLogger.LogError(device.Address.ToString(), reading.Time, reading.ErrorCode.ToString(), 2, InverterErrorRegisters.GetBytes());
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
