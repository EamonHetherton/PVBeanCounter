using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Algorithms;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace Device
{
    public class InverterAlgorithm : DeviceAlgorithm
    {
        // Add String Variables below

        public String Model { get; private set; }
        public String Manufacturer { get; private set; }
        public String SerialNo { get; private set; }

        public void SetModel(string value) { Model = value; }
        public void SetManufacturer(string value) { Manufacturer = value; }
        public void SetSerialNo(string value) { SerialNo = value; }

        public String GetSerialNo() { return SerialNo; }

        // End String Variables

        // Add Numeric Variables below

        public decimal? EnergyTotalAC { get; private set; }
        public decimal? EnergyTotalACHigh { get; private set; }
        public decimal? EnergyTodayAC { get; private set; }
        public decimal? Status { get; private set; }
        public decimal? PowerPV { get; private set; }
        public decimal? VoltsPV1 { get; private set; }
        public decimal? CurrentPV1 { get; private set; }
        public decimal? PowerPV1 { get; private set; }
        public decimal? VoltsPV2 { get; private set; }
        public decimal? CurrentPV2 { get; private set; }
        public decimal? PowerPV2 { get; private set; }
        public decimal? VoltsPV3 { get; private set; }
        public decimal? CurrentPV3 { get; private set; }
        public decimal? PowerPV3 { get; private set; }
        public decimal? Frequency { get; private set; }
        public decimal? VoltsAC1 { get; private set; }
        public decimal? CurrentAC1 { get; private set; }
        public decimal? PowerAC1 { get; private set; }
        public decimal? PowerAC1High { get; private set; }
        public decimal? VoltsAC2 { get; private set; }
        public decimal? CurrentAC2 { get; private set; }
        public decimal? PowerAC2 { get; private set; }
        public decimal? VoltsAC3 { get; private set; }
        public decimal? CurrentAC3 { get; private set; }
        public decimal? PowerAC3 { get; private set; }
        public decimal? TimeTotal { get; private set; }
        public decimal? TimeTotalHigh { get; private set; }
        public decimal? Temperature { get; private set; }
        public decimal? ErrorCodeHigh { get; private set; }
        public decimal? ErrorCode { get; private set; }
        public decimal? PowerRating { get; private set; }

        public void SetEnergyTotalAC(decimal value) { EnergyTotalAC = value; }
        public void SetEnergyTotalACHigh(decimal value) { EnergyTotalACHigh = value; }
        public void SetEnergyTodayAC(decimal value) { EnergyTodayAC = value; }
        public void SetStatus(decimal value) { Status = value; }
        public void SetPowerAC(decimal value) { PowerAC1 = value; }
        public void SetPowerPV(decimal value) { PowerPV = value; }
        public void SetVoltsPV1(decimal value) { VoltsPV1 = value; }
        public void SetCurrentPV1(decimal value) { CurrentPV1 = value; }
        public void SetPowerPV1(decimal value) { PowerPV1 = value; }
        public void SetVoltsPV2(decimal value) { VoltsPV2 = value; }
        public void SetCurrentPV2(decimal value) { CurrentPV2 = value; }
        public void SetPowerPV2(decimal value) { PowerPV2 = value; }
        public void SetVoltsPV3(decimal value) { VoltsPV2 = value; }
        public void SetCurrentPV3(decimal value) { CurrentPV2 = value; }
        public void SetPowerPV3(decimal value) { PowerPV2 = value; }
        public void SetFrequency(decimal value) { Frequency = value; }
        public void SetVoltsAC1(decimal value) { VoltsAC1 = value; }
        public void SetCurrentAC1(decimal value) { CurrentAC1 = value; }
        public void SetPowerAC1(decimal value) { PowerAC1 = value; }
        public void SetPowerAC1High(decimal value) { PowerAC1High = value; }
        public void SetVoltsAC2(decimal value) { VoltsAC2 = value; }
        public void SetCurrentAC2(decimal value) { CurrentAC2 = value; }
        public void SetPowerAC2(decimal value) { PowerAC2 = value; }
        public void SetVoltsAC3(decimal value) { VoltsAC3 = value; }
        public void SetCurrentAC3(decimal value) { CurrentAC3 = value; }
        public void SetPowerAC3(decimal value) { PowerAC3 = value; }
        public void SetTimeTotal(decimal value) { TimeTotal = value; }
        public void SetTimeTotalHigh(decimal value) { TimeTotalHigh = value; }
        public void SetTemperature(decimal value) { Temperature = value; }
        public void SetErrorCodeHigh(decimal value) { ErrorCodeHigh = value; }
        public void SetErrorCode(decimal value) { ErrorCode = value; }
        public void SetPowerRating(decimal value) { PowerRating = value; }

        // End Numeric Variables

        // Add Bytes Variables below

        public byte[] AlarmRegisters { get; private set; }
        public byte[] ErrorRegisters { get; private set; }
        public bool HaveErrorRegisters { get; private set; }

        public void SetAlarmRegisters(byte[] value) { AlarmRegisters = value; }
        public void SetErrorRegisters(byte[] value) { ErrorRegisters = value; HaveErrorRegisters = true;  }
        // End Bytes Variables

        public Double EnergyMargin { get; private set; }
        public Double EnergyTotalEnergyMargin { get; private set; }
        public Double EnergyTodayEnergyMargin { get; private set; }

        public InverterAlgorithm(DeviceManagerDeviceSettings deviceSettings, Protocol protocol, ErrorLogger errorLogger)
            :base(deviceSettings, protocol, errorLogger)
        {
            EnergyMargin = 0.01;
            HaveErrorRegisters = false;
        }

        protected override void LoadVariables()
        {
            VariableEntry var;

            var = new VariableEntry_Numeric("Address", SetAddress, GetAddress);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("EnergyTotalAC", SetEnergyTotalAC);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("EnergyTotalACHigh", SetEnergyTotalACHigh);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("EnergyTodayAC", SetEnergyTodayAC);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Status", SetStatus);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerAC", SetPowerAC);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerPV", SetPowerPV);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("VoltsPV1", SetVoltsPV1);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("CurrentPV1", SetCurrentPV1);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerPV1", SetPowerPV1);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("VoltsPV2", SetVoltsPV2);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("CurrentPV2", SetCurrentPV2);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerPV2", SetPowerPV2);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("VoltsPV3", SetVoltsPV3);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("CurrentPV3", SetCurrentPV3);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerPV3", SetPowerPV3);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Frequency", SetFrequency);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("VoltsAC1", SetVoltsAC1);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("CurrentAC1", SetCurrentAC1);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerAC1", SetPowerAC1);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerAC1High", SetPowerAC1High);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("VoltsAC2", SetVoltsAC2);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("CurrentAC2", SetCurrentAC2);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerAC2", SetPowerAC2);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("VoltsAC3", SetVoltsAC3);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("CurrentAC3", SetCurrentAC3);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerAC3", SetPowerAC3);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("TimeTotal", SetTimeTotal);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("TimeTotalHigh", SetTimeTotalHigh);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Temperature", SetTemperature);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("ErrorCode", SetErrorCode);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("ErrorCodeHigh", SetErrorCodeHigh);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("PowerRating", SetPowerRating);
            VariableEntries.Add(var);

            var = new VariableEntry_String("Model", SetModel);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Manufacturer", SetManufacturer);
            VariableEntries.Add(var);
            var = new VariableEntry_String("SerialNo", SetSerialNo, GetSerialNo);
            VariableEntries.Add(var);

            var = new VariableEntry_Bytes("AlarmRegisters", SetAlarmRegisters);
            VariableEntries.Add(var);
            var = new VariableEntry_Bytes("ErrorRegisters", SetErrorRegisters);
            VariableEntries.Add(var);
        }

        public override void ClearAttributes()
        {
            EnergyTotalAC = null;
            EnergyTotalACHigh = null;
            EnergyTodayAC = null;
            Status = null;
            PowerPV = null;
            VoltsPV1 = null;
            CurrentPV1 = null;
            PowerPV1 = null;
            VoltsPV2 = null;
            CurrentPV2 = null;
            PowerPV2 = null;
            VoltsPV3 = null;
            CurrentPV3 = null;
            PowerPV3 = null;
            Frequency = null;
            VoltsAC1 = null;
            CurrentAC1 = null;
            PowerAC1 = null;
            PowerAC1High = null;
            VoltsAC2 = null;
            CurrentAC2 = null;
            PowerAC2 = null;
            VoltsAC3 = null;
            CurrentAC3 = null;
            PowerAC3 = null;
            TimeTotal = null;
            TimeTotalHigh = null;
            Temperature = null;
            ErrorCode = null;
            ErrorCodeHigh = null;

            AlarmRegisters = new byte[2];
            ErrorRegisters = new byte[2];
            HaveErrorRegisters = false;
        }

        private void LoadEnergyMargin()
        {
            // Energy Margin limits the Energy Estimate deviation from inverter value
            // Inverter identity is sometimes used to dynamically alter details such as Scale / ScaleFactor (refer JFY inverters)
            Register eToday = FindRegister("", "", "EnergyTodayAC");
            Register eTotal = FindRegister("", "", "EnergyTotalAC");
            if (eToday != null)
                EnergyMargin = (Double)((RegisterNumber)eToday).ScaleFactor;
            else
            {                
                if (eTotal != null)
                    EnergyMargin = (Double)((RegisterNumber)eTotal).ScaleFactor;
            }

            EnergyTodayEnergyMargin = eToday == null ? EnergyMargin : (Double)((RegisterNumber)eToday).ScaleFactor;
            EnergyTotalEnergyMargin = eTotal == null ? EnergyMargin : (Double)((RegisterNumber)eTotal).ScaleFactor;
        }

        public bool ExtractIdentity()
        {
            if (FaultDetected)
                return false;

            bool res = false;
            bool alarmFound = false;
            bool errorFound = false;
            String stage = "Identity";
            try
            {                
                res = LoadBlockType("Identity", false, true, ref alarmFound, ref errorFound);
                if (!res)
                    return false;

                res = ExecuteAlgorithmType("Identity", false, true);
                if (!res)
                    return false;

                UpdateDynamicDataMap();
                LoadEnergyMargin();
                
                if (GlobalSettings.SystemServices.LogTrace)
                    LogMessage("DoExtractReadings - Identity - Manufacturer: " + Manufacturer.Trim()
                        + " - Model: " + Model.Trim()
                        + " - SerialNo: " + SerialNo.Trim()
                        + " - Energy Margin: " + EnergyMargin, LogEntryType.Trace);
            }
            catch (Exception e)
            {
                LogMessage("InverterAlgorithm.ExtractItentity - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }
            return res;
        }

        public bool ExtractReading(bool dbWrite, ref bool alarmFound, ref bool errorFound)
        {
            alarmFound = false;
            errorFound = false;
            bool res = false;
            if (FaultDetected)
                return res;
            
            String stage = "Identity";
            try
            {
                stage = "Reading";

                res = LoadBlockType("Reading", true, dbWrite, ref alarmFound, ref errorFound);
                if (!res)
                    return false;

                // Execute optional Reading Algorithms
                res = ExecuteAlgorithmType("Reading", false, dbWrite);
                if (!res)
                    return false;

                if (alarmFound)
                {
                    // Execute optional Alarm Log Algorithms
                    res = ExecuteAlgorithmType("AlarmLog", false, dbWrite);
                    if (!res)
                        return false;
                }

                if (errorFound)
                {
                    // Execute optional Error Log Algorithms
                    res = ExecuteAlgorithmType("ErrorLog", false, dbWrite);
                    if (!res)
                        return false;
                }

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
