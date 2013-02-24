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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;
using PVBCInterfaces;

namespace Algorithms
{
    /*
    public abstract class DeviceBase 
    {
        protected DeviceManagerDeviceSettings DeviceSettings;



        //public abstract void SetDeviceInfo(DeviceInfo deviceInfo);

        public DeviceBase(IDeviceManager deviceManager, DeviceManagerDeviceSettings deviceSettings)
        {
            LastRunTime = DateTime.MinValue;
            FaultDetected = false;
            DeviceManager = deviceManager;
            DeviceSettings = deviceSettings;
            HasStartOfDayEnergyDefect = DeviceSettings.DeviceSettings.HasStartOfDayEnergyDefect;
            EmitEvents = GlobalSettings.ApplicationSettings.EmitEvents;

            QueryInterval = TimeSpan.FromSeconds(DeviceSettings.QueryIntervalInt);
            DatabaseInterval = DeviceSettings.DBIntervalInt;

            Address = DeviceSettings.Address;
            DefaultSerialNo = DeviceSettings.SerialNo;
        }
    }

    public abstract class PassiveDevice : DeviceBase
    {
        public PassiveDevice(IDeviceManager deviceManager, DeviceManagerDeviceSettings deviceSettings)
            : base(deviceManager, deviceSettings)
        {
        }
    }
    */

    public struct AlgorithmParams
    {
        public Protocol Protocol;
        public string DeviceName;
        public ObservableCollection<BlockSettings> BlockList;
        public ObservableCollection<ActionSettings> AlgorithmList;
        public EndianConverter16Bit EndianConverter16Bit;
        public EndianConverter32Bit EndianConverter32Bit;
        public ErrorLogger ErrorLogger;
    }

    public abstract class DeviceAlgorithm 
    {
        protected List<VariableEntry> VariableEntries;

        protected List<DeviceBlock> DeviceBlocks;
        protected List<AlgorithmAction> Algorithms;

        public ByteVar InverterAddress { get; set; }
        public ByteVar ModbusCommand { get; set; }
        public ByteVar RegisterCount { get; set; }
        public ByteVar FirstModbusAddress { get; set; }

        public ByteVar DeviceDataSize { get; set; }
        public ByteVar DeviceData { get; set; }
        public ByteVar DeviceDataValueSize { get; set; }
        public ByteVar DeviceDataValue { get; set; }

        //public EndianConverter16Bit EndianConverter16Bit { get; private set; }
        //public EndianConverter32Bit EndianConverter32Bit { get; private set; }

        public bool FaultDetected { get; protected set; }

        public Protocol Protocol { get; private set; }
            

        //public ICommunicationDeviceManagerBase DeviceManager { get; private set; }
        //public DeviceManagerDeviceSettings DeviceSettings { get; private set; }

        public UInt64 Address { get; set; }

        public void SetAddress(decimal value) { Address = (UInt64)value; }
        public decimal GetAddress() { return (decimal)Address; }

        public bool HasStartOfDayEnergyDefect { get; protected set; }

        public AlgorithmParams Params;

        //public DeviceAlgorithm(ICommunicationDeviceManager deviceManager, DeviceManagerDeviceSettings deviceSettings, IProtocol protocol)
        public DeviceAlgorithm(AlgorithmParams algorithmParams)
        {
            DoInitialise(algorithmParams);
        }

        public DeviceAlgorithm(DeviceManagerDeviceSettings deviceSettings, Protocol protocol, ErrorLogger errorLogger)
        {
            //DeviceManager = deviceManager;
            Protocol = protocol;
            AlgorithmParams aParams;
            aParams.EndianConverter16Bit = protocol.EndianConverter16Bit;
            aParams.EndianConverter32Bit = protocol.EndianConverter32Bit;
            aParams.Protocol = Protocol;
            aParams.AlgorithmList = deviceSettings.DeviceSettings.AlgorithmList;
            aParams.BlockList = deviceSettings.DeviceSettings.BlockList;
            aParams.DeviceName = deviceSettings.Name;
            aParams.ErrorLogger = errorLogger;

            DoInitialise(aParams);
        }

        private void DoInitialise(AlgorithmParams algorithmParams)
        {
            FaultDetected = false;
            VariableEntries = new List<VariableEntry>();
            Params = algorithmParams;            

            SetupVariables();

            LoadVariables();
            LoadDevice();
        }

        //public ICommunicationDeviceManager CommunicationDeviceManager { get { return (ICommunicationDeviceManager)base.DeviceManager; } protected set { base.DeviceManager = value; } }

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage(Params.DeviceName, message, logEntryType);
        }

        protected void SetupVariables()
        {
            InverterAddress = (ByteVar)Params.Protocol.GetSessionVariable("%Address", null);
            ModbusCommand = (ByteVar)Params.Protocol.GetSessionVariable("%CommandId", null);
            RegisterCount = (ByteVar)Params.Protocol.GetSessionVariable("%Registers", null);
            FirstModbusAddress = (ByteVar)Params.Protocol.GetSessionVariable("%FirstAddress", null);
            DeviceData = (ByteVar)Params.Protocol.GetSessionVariable("%Data", null);
            DeviceDataSize = (ByteVar)Params.Protocol.GetSessionVariable("%DataSize", null);
            DeviceDataValue = (ByteVar)Params.Protocol.GetSessionVariable("%DataValue", null);
            DeviceDataValueSize = (ByteVar)Params.Protocol.GetSessionVariable("%DataValueSize", null);
        }

        private void LoadDevice()
        {
            try
            {
                DeviceBlocks = new List<DeviceBlock>();
                foreach (BlockSettings blockSettings in Params.BlockList)
                {
                    DeviceBlock block;
                    if (Params.Protocol.Type == ProtocolSettings.ProtocolType.Modbus)
                        block = new DeviceBlock_Modbus(this, blockSettings, Params.Protocol);
                    else if (Params.Protocol.Type == ProtocolSettings.ProtocolType.Phoenixtec)
                        block = new DeviceBlock_Phoenixtec(this, blockSettings, Params.Protocol);
                    else
                        block = new DeviceBlock_RequestResponse(this, blockSettings, Params.Protocol);
                    DeviceBlocks.Add(block);
                }
            }
            catch (Exception e)
            {
                FaultDetected = true;
                LogMessage("Device.LoadDevice - Loading DeviceBlocks - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                Algorithms = new List<AlgorithmAction>();
                foreach (ActionSettings settings in Params.AlgorithmList)
                {
                    AlgorithmAction algorithm = new Algorithm(this, settings);
                    Algorithms.Add(algorithm);
                }
            }
            catch (Exception e)
            {
                FaultDetected = true;
                LogMessage("Device.LoadDevice - Loading Algorithms - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
        }

        protected bool LoadBlockType(String type, bool mandatory, bool dbWrite, ref bool alarmFound, ref bool errorFound)
        {
            bool found = false;
            errorFound = false;
            alarmFound = false;
            foreach (DeviceBlock block in DeviceBlocks)
            {
                if (block.Type == type && (dbWrite || !block.OnDbWriteOnly))
                {
                    if (!block.GetBlock(false, true))
                        return false;
                    found = true;
                    errorFound |= block.ErrorFound;
                    alarmFound |= block.AlarmFound;
                }
            }

            return (!mandatory || found);
        }

        protected internal bool ExecuteAlgorithmType(String type, bool mandatory, bool dbWrite)
        {
            bool found = false;

            foreach (Algorithm algorithm in Algorithms)
            {
                if (algorithm.Type == type && (dbWrite || !algorithm.OnDbWriteOnly))
                {
                    if (!algorithm.Execute())
                        return false;
                    found = true;
                }
            }

            return (!mandatory || found);
        }

        protected internal bool ExecuteAlgorithm(String name, bool mandatory = false)
        {
            foreach (Algorithm algorithm in Algorithms)
            {
                if (algorithm.Name == name)
                    return algorithm.Execute();
            }

            return (!mandatory);
        }

        protected internal DeviceBlock FindBlock(String blockName)
        {
            foreach (DeviceBlock block in DeviceBlocks)
                if (block.Name == blockName)
                    return block;

            return null;
        }

        protected internal VariableEntry FindVariable(String itemName)
        {
            foreach (VariableEntry var in VariableEntries)
            {
                if (var.Name == itemName)
                    return var;
            }
            return null;
        }

        protected internal Register GetRegister(DeviceBlock block, RegisterSettings settings)
        {
            String itemName = settings.Content;

            if (itemName != "")
            {
                VariableEntry var = FindVariable(itemName);

                if (var != null)
                {
                    if (var.GetType() == typeof(VariableEntry_Numeric))
                        return new RegisterNumber(block, settings, ((VariableEntry_Numeric)var).SetValueDelegate, ((VariableEntry_Numeric)var).GetValueDelegate);
                    else if (var.GetType() == typeof(VariableEntry_String))
                        return new RegisterString(block, settings, ((VariableEntry_String)var).SetValueDelegate, ((VariableEntry_String)var).GetValueDelegate);
                    else if (var.GetType() == typeof(VariableEntry_Bytes))
                        return new RegisterBytes(block, settings, ((VariableEntry_Bytes)var).SetValueDelegate, ((VariableEntry_Bytes)var).GetValueDelegate);
                }
                // some devices do not need external exposure of the Register entries - eg CC128
                LogMessage("Device.GetRegister - Cannot find 'Content': " + itemName, LogEntryType.Trace);
            }

            RegisterSettings.RegisterValueType type = settings.Type;

            if (type == RegisterSettings.RegisterValueType.rv_bytes)
                return new RegisterBytes(block, settings, null);
            if (type == RegisterSettings.RegisterValueType.rv_string)
                return new RegisterString(block, settings, null);

            return new RegisterNumber(block, settings, null);
        }

        protected internal Register FindRegister(String blockType, String blockName, String itemName)
        {
            foreach (DeviceBlock block in DeviceBlocks)
            {
                if (blockType != "" && blockType != block.Type)
                    continue;
                if (blockName != "" && blockName != block.Name)
                    continue;
                foreach (Register item in block.BlockAllRegisters)
                {
                    if (itemName == item.Name)
                        return item;
                }
            }
            return null;
        }

        protected void UpdateDynamicDataMap()
        {
            // extract list of data item positions from the format message response
            DeviceBlock dynamicFormatBlock = null;
            DeviceBlock dynamicDataBlock = null;
            foreach (DeviceBlock block in DeviceBlocks)
            {
                if (block.Name == "%GetDynamicFormat")
                    dynamicFormatBlock = block;
                else if (block.Name == "%GetDynamicData")
                    dynamicDataBlock = block;
            }

            if (dynamicFormatBlock == null || dynamicDataBlock == null)
                return;

            ByteVar deviceDataMap = (ByteVar)Params.Protocol.GetSessionVariable("%Payload_2", dynamicFormatBlock.Conversation);
            if (deviceDataMap == null)
                return;

            BlockMessage dataBlockMessage = null;
            foreach (BlockMessage blockMsg in dynamicDataBlock.BlockMessages)
                if (blockMsg.Name == "ReceiveData")
                    dataBlockMessage = blockMsg;

            if (dataBlockMessage == null || dataBlockMessage.DynamicDataMap == null)
                return;

            byte[] mapBytes = deviceDataMap.GetBytes();
            // assign positions to candidate items
            for (int i = 0; i < mapBytes.Length; i++)
                dataBlockMessage.DynamicDataMap.SetItemPosition((Int16)mapBytes[i], (UInt16)(i * 2));

            // build list of expected data items
            dataBlockMessage.DynamicDataMap.BuildItemArray();
            // build list of data item registers and associate with the dynamic data retrieval message            
            dynamicDataBlock.LoadDynamicRegisters(dataBlockMessage);
        }

        protected abstract void LoadVariables();

        public abstract void ClearAttributes();

        public void LogAlarm(String type, DateTime alarmTime, IDeviceManager deviceManager)
        {
            Register alarmFlag = null;
            foreach (DeviceBlock block in DeviceBlocks)
            {
                if (block.Type == type)
                {
                    foreach (Register item in block.BlockAllRegisters)
                        if (item.IsAlarmFlag)
                            alarmFlag = item;
                }
            }
            if (alarmFlag == null)
                return;
            string alarm = alarmFlag.ValueString;
            foreach (DeviceBlock block in DeviceBlocks)
            {
                if (block.Type == type)
                {
                    foreach (Register item in block.BlockAllRegisters)
                        if (item.IsAlarmDetail)
                            deviceManager.ErrorLogger.LogMessage("alarm", ((int)Address).ToString(), alarmTime, alarm, 2, item.GetRegisterDataBytes());
                }
            }
        }

        public void LogError(String type, DateTime errorTime, IDeviceManager deviceManager)
        {
            Register errorFlag = null;
            foreach (DeviceBlock block in DeviceBlocks)
            {
                if (block.Type == type)
                {
                    foreach (Register item in block.BlockAllRegisters)
                        if (item.IsErrorFlag)
                            errorFlag = item;
                }
            }
            if (errorFlag == null)
                return;
            string error = errorFlag.ValueString;
            foreach (DeviceBlock block in DeviceBlocks)
            {
                if (block.Type == type)
                {
                    foreach (Register item in block.BlockAllRegisters)
                        if (item.IsErrorDetail)
                            deviceManager.ErrorLogger.LogMessage("error", ((int)Address).ToString(), errorTime, error, 2, item.GetRegisterDataBytes());
                }
            }
        }
    }
}
