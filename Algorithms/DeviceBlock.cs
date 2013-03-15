/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;
using PVBCInterfaces;

namespace Algorithms
{
    public class BlockMessage
    {
        public String Name;
        public List<Register> MessageItems;
        public byte[] Payload;
        public ByteVar PayloadByteVar;
        public UInt32 PayloadSize;
        public ByteVar PayloadSizeByteVar;
        public Message Message;
        public MessageType MessageType;
        public PVSettings.DynamicDataMap DynamicDataMap;
    }

    public abstract class DeviceBlock
    {
        // A Modbus Block represents a collection of modbus registers available in a single Modbus command request
        public List<Register> BlockAllRegisters = null;  // All registers in the block - top level and inside messages
        public List<Register> BlockLevelRegisters = null; // Block level registers only - inside messages not included 
        public List<BlockMessage> BlockMessages = null;  // All messages defined in the block

        public DeviceAlgorithm Device;
        
        //public ICommunicationDeviceManager DeviceManager;
        public BlockSettings BlockSettings;

        public String Name { get; private set; }
        public String Type { get; private set; }

        public Conversation Conversation { get; private set; }


        public bool AlarmFound { get; protected set; }
        public bool ErrorFound { get; protected set; }
        public bool OnDbWriteOnly { get; protected set; }
        public int Base { get; private set; }

        public byte[] RegisterData;

        public Protocol Protocol;
        public ProtocolSettings.ProtocolType ProtocolType { get; private set; }

        protected Int16 retryCount;

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage("DeviceBlock", message, logEntryType);
        }

        public DeviceBlock(DeviceAlgorithm device, BlockSettings blockSettings, Protocol protocol)
        {
            AlarmFound = false;
            ErrorFound = false;

            Device = device;
            //DeviceManager = Device.DeviceManager;
            
            //DeviceManager = Device.Manager;
            BlockSettings = blockSettings;
            retryCount = BlockSettings.TimeoutRetries;
            Protocol = protocol;
            ProtocolType = Protocol.Type;
            Conversation = Protocol.GetConversation(BlockSettings.Conversation);
            Name = BlockSettings.Name;
            Type = BlockSettings.Type;
            OnDbWriteOnly = BlockSettings.OnDbWriteOnly;
            LoadRegisters();
            int? temp = BlockSettings.Base;
            Base = temp.HasValue ? temp.Value : -1;
        }

        protected abstract void LoadRegisters();

        public virtual bool SendBlock(bool continueOnFailure)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("SendBlock - Starting - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);

            bool res = false;
            res = SendBlock_Special(continueOnFailure);

            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("SendBlock - Complete - Name: " + Name + " - Result: " + (res ? "true" : "false"), LogEntryType.Trace);
            return res;
        }

        public bool GetBlock(bool ContinueOnFailure, bool dbWrite)
        {
            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("GetBlock - Starting - Name: " + Name + " - Type: " + Type, LogEntryType.Trace);
            bool res = false;
            AlarmFound = false;
            ErrorFound = false;

            res = GetBlock_Special(ContinueOnFailure, dbWrite);

            if (res)
            {
                foreach (Register item in BlockAllRegisters)
                {
                    if (item.IsErrorFlag)
                    {
                        string error = item.ErrorStatus;
                        ErrorFound |= (error != "OK");
                    }
                    if (item.IsAlarmFlag)
                    {
                        string alarm = item.AlarmStatus;
                        AlarmFound |= (alarm != "OK");
                    }
                }
            }

            if (ErrorFound)
            {
                Device.ExecuteAlgorithmType("ErrorLog", false, dbWrite);
            }
            if (AlarmFound)
            {
                Device.ExecuteAlgorithmType("AlarmLog", false, dbWrite);
            }

            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("GetBlock - Complete - Name: " + Name + " - Result: " + (res ? "true" : "false"), LogEntryType.Trace);
            return res;
        }

        protected BlockMessage GetBlockMessage(String messageName)
        {
            foreach (BlockMessage msg in BlockMessages)
                if (msg.Name == messageName)
                    return msg;

            BlockMessage blockMsg = new BlockMessage();
            blockMsg.Name = messageName;
            blockMsg.Message = Protocol.GetMessage(Conversation.Name, blockMsg.Name);
            if (blockMsg.Message == null)
            {
                LogMessage("NonModbus LoadRegisters - Cannot find message: " + blockMsg.Name, LogEntryType.ErrorMessage);
                blockMsg.MessageType = MessageType.Unknown;
                blockMsg.PayloadByteVar = null;
                blockMsg.PayloadSizeByteVar = null;
            }
            else
            {
                blockMsg.MessageType = blockMsg.Message.MessageType;
                blockMsg.PayloadByteVar = (ByteVar)blockMsg.Message.GetVariable("%Payload_");
                blockMsg.PayloadSizeByteVar = (ByteVar)blockMsg.Message.GetVariable("%PayloadSize_");
            }

            blockMsg.MessageItems = new List<Register>();
            blockMsg.PayloadSize = 0;
            blockMsg.Payload = null;

            BlockMessages.Add(blockMsg);
            return blockMsg;
        }

        public void LoadDynamicRegisters(BlockMessage blockMsgData)
        {
            BlockMessage blockMsgDataSize = GetBlockMessage("ReceiveDataSize");

            XmlDocument doc = new XmlDocument();
            UInt16 size = 0;

            foreach (DynamicDataMap.MapItem item in blockMsgData.DynamicDataMap.Items)
            {
                Register registerItem = Device.GetRegister(this, item.templateRegisterSettings);

                if (registerItem == null)
                    LogMessage("LoadDynamicRegisters - Cannot find content: " + item.templateRegisterSettings.Content, LogEntryType.ErrorMessage);
                else
                {
                    registerItem.PayloadPosition = item.Position;
                    UInt16 minSize = (UInt16)(item.Position.Value + registerItem.CurrentSize);
                    if (size < minSize)
                        size = minSize;

                    BlockAllRegisters.Add(registerItem);
                    blockMsgData.MessageItems.Add(registerItem);
                }
            }

            blockMsgData.PayloadSize = size;
            if (blockMsgDataSize.PayloadSizeByteVar != null)
            {
                UInt32 sizeSize = (UInt32)blockMsgDataSize.PayloadSizeByteVar.Length;
                if (sizeSize != 1 && sizeSize != 2 && sizeSize != 4)
                {
                    LogMessage("LoadDynamicRegisters - Payload size must be 1, 2 or 4 bytes", LogEntryType.ErrorMessage);
                }
            }
            blockMsgData.Payload = new byte[blockMsgData.PayloadSize];
            blockMsgData.Payload.Initialize();
        }

        protected abstract bool SendBlock_Special(bool continueOnFailure);

        protected abstract bool GetBlock_Special(bool ContinueOnFailure, bool dbWrite);
    }

    public class DeviceBlock_Modbus : DeviceBlock
    {
        public UInt16? Registers { get; private set; }
        public UInt16? FirstRegister { get; private set; }
        public UInt16? LastRegister { get; private set; }

        public byte? CommandId { get; private set; }
        bool IsKLNEModbus;

        public DeviceBlock_Modbus(DeviceAlgorithm device, BlockSettings blockSettings, Protocol protocol)
            : base(device, blockSettings, protocol)
        {
            CommandId = BlockSettings.CommandId;
            IsKLNEModbus = protocol.ProtocolSettings.Name == "KLNEModbus";
        }

        protected bool RetrieveDeviceRegisters(bool continueOnFailure)
        {
            LogMessage("RetrieveDeviceRegisters - Block: " + Name + " - Type: " + Type, LogEntryType.Trace);
            UInt16 firstRegister = (UInt16)(Base + FirstRegister.Value);
            if (IsKLNEModbus)
            {
                byte[] bytes = EndianConverter.GetBCDFromDecimal(Device.Address, 6, 0, false);
                Device.InverterAddress.SetBytes(ref bytes, 0, 6);
            }
            else
                Device.InverterAddress.SetByte((byte)Device.Address);

            Device.ModbusCommand.SetByte(CommandId.Value);
            Device.RegisterCount.SetBytes(Registers.Value);
            Device.FirstModbusAddress.SetBytes(firstRegister);

            int count = 0;
            bool timeOut;
            bool result;
            do
            {
                result = Protocol.DoConversation(Conversation.Name, out timeOut, continueOnFailure);
            }
            while (!result && timeOut && count++ < retryCount);

            if (!result)
            {
                LogMessage("RetrieveDeviceRegisters - Error in '" + Conversation + "' conversation", LogEntryType.ErrorMessage);
                RegisterData = new byte[2];
                return false;
            }

            RegisterData = Device.DeviceData.GetBytes();

            return true;
        }

        protected override void LoadRegisters()
        {
            bool isFirst = true;
            BlockAllRegisters = new List<Register>();

            Registers = null;
            FirstRegister = null;
            LastRegister = null;

            foreach (RegisterSettings register in BlockSettings.RegisterList)
            {
                Register registerItem = Device.GetRegister(this, register);

                if (registerItem != null)
                {
                    UInt16? id = register.Id;
                    BlockAllRegisters.Add(registerItem);
                    if (id.HasValue)
                    {
                        if (!FirstRegister.HasValue)
                        {
                            FirstRegister = 0;
                            LastRegister = 0;
                        }
                        if (FirstRegister.Value > id.Value || isFirst)
                        {
                            FirstRegister = id.Value;
                            isFirst = false;
                        }
                        UInt16 endRegister = (UInt16)(id.Value + register.RegisterCount - 1);
                        if (LastRegister < endRegister)
                            LastRegister = endRegister;
                    }
                }
            }

            if (FirstRegister.HasValue)
                Registers = (UInt16)((LastRegister - FirstRegister) + 1);
        }

        protected void PrepareRegisterData()
        {
            UInt16 sendSize;

            if (Registers.HasValue)
                sendSize = (UInt16)(Registers.Value * 2);  // modbus is always all response or all query, not mixed in one block
            else
                sendSize = 0;

            RegisterData = new byte[sendSize];
            RegisterData.Initialize();

            foreach (Register item in BlockAllRegisters)
            {
                if (item.Binding == null)
                    item.StoreItemValue(ref RegisterData);
            }
        }

        protected bool UpdateDeviceRegisters(bool continueOnFailure)
        {
            LogMessage("UpdateDeviceRegisters - Block: " + Name + " - Type: " + Type, LogEntryType.Trace);
            PrepareRegisterData();
            UInt16 firstRegister = (UInt16)(Base + FirstRegister.Value);
            if (IsKLNEModbus)
            {
                byte[] bytes = EndianConverter.GetBCDFromDecimal(Device.Address, 6, 0, false);
                Device.InverterAddress.SetBytes(ref bytes, 0, 6);
            }
            else
                Device.InverterAddress.SetByte((byte)Device.Address);

            Device.RegisterCount.SetBytes(Registers.Value);
            Device.FirstModbusAddress.SetBytes(firstRegister);
            byte size = (byte)(Registers.Value * 2);
            Device.DeviceDataValueSize.SetByte(size);
            Device.DeviceDataValue.SetBytes(ref RegisterData, 0, size);

            int count = 0;
            bool timeOut;
            bool result;
            do
            {
                result = Protocol.DoConversation(Conversation.Name, out timeOut, continueOnFailure);
            }
            while (!result && timeOut && count++ < retryCount);

            if (!result)
            {
                LogMessage("UpdateDeviceRegisters - Error in '" + Conversation + "' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            return true;
        }

        protected override bool SendBlock_Special(bool continueOnFailure)
        {
            return UpdateDeviceRegisters(continueOnFailure);
        }

        protected override bool GetBlock_Special(bool ContinueOnFailure, bool dbWrite)
        {
            bool res = RetrieveDeviceRegisters(ContinueOnFailure);
            if (res)
                foreach (Register item in BlockAllRegisters)
                    if (item.Binding == null && item.Message != PVSettings.BlockSettings.Message.Send)
                        item.GetItemValue(ref RegisterData);
            return res;
        }
    }

    public abstract class DeviceBlock_NonModbus : DeviceBlock
    {
        public DeviceBlock_NonModbus(DeviceAlgorithm device, BlockSettings blockSettings, Protocol protocol)
            : base(device, blockSettings, protocol)
        {
        }

        private UInt16 LoadRegisterList(ObservableCollection<RegisterSettings> registers, List<Register> list)
        {
            UInt16 size = 0;
            foreach (RegisterSettings register in registers)
            {
                Register registerItem = Device.GetRegister(this, register);
                if (registerItem == null)
                    LogMessage("LoadRegisters - Cannot find content: " + register.Content, LogEntryType.ErrorMessage);
                else
                {
                    if (registerItem.BindingName == "")
                    {
                        if (registerItem.PayloadPosition.HasValue)  // items with position specified in config
                        {
                            UInt16 minSize = (UInt16)(registerItem.PayloadPosition.Value + registerItem.CurrentSize);
                            if (size < minSize)
                                size = minSize;
                        }
                        else  // items with position determined by order in configuration
                        {
                            registerItem.PayloadPosition = size;
                            size += registerItem.CurrentSize;
                        }
                    }
                    BlockAllRegisters.Add(registerItem);
                    if (list != null)
                        list.Add(registerItem);
                }
            }
            return size;
        }

        protected override void LoadRegisters()
        {
            BlockAllRegisters = new List<Register>();
            BlockLevelRegisters = new List<Register>();
            BlockMessages = new List<BlockMessage>();

            // Load block level registers
            LoadRegisterList(BlockSettings.RegisterList, BlockLevelRegisters);

            // Load registers defined at message scope level
            foreach (BlockMessageSettings msg in BlockSettings.MessageList)
            {
                BlockMessage blockMsg = GetBlockMessage(msg.Name);

                blockMsg.PayloadSize = LoadRegisterList(msg.RegisterList, blockMsg.MessageItems);
                if (blockMsg.PayloadSizeByteVar != null)
                {
                    UInt32 sizeSize = (UInt32)blockMsg.PayloadSizeByteVar.Length;
                    if (sizeSize != 1 && sizeSize != 2 && sizeSize != 4)
                    {
                        LogMessage("NonModbus LoadRegisters - Payload size must be 1, 2 or 4 bytes", LogEntryType.ErrorMessage);
                    }
                }
                blockMsg.Payload = new byte[blockMsg.PayloadSize];
                blockMsg.Payload.Initialize();
                blockMsg.DynamicDataMap = msg.DynamicDataMap;
            }
        }

        protected void SetupProtocolBindings()
        {
            foreach (Register item in BlockAllRegisters)
            {
                if (!item.BoundToSend && !item.BoundToRead && !item.BoundToFind)
                    continue;
                Type type = item.Binding.GetType();

                if (type == typeof(StringVar))
                {
                    ((StringVar)item.Binding).Value = item.ValueString;
                }
                else if (type == typeof(ByteVar))
                {
                    byte[] bytes = item.ValueBytes;
                    ((ByteVar)item.Binding).SetBytes(ref bytes, 0, bytes.Length);
                }
            }
        }

        protected void PrepareRegisterData()
        {
            foreach (BlockMessage msg in BlockMessages)
            {
                if (msg.MessageType == MessageType.Send || msg.MessageType == MessageType.Find || msg.MessageType == MessageType.Read)
                {
                    msg.Payload.Initialize();
                    foreach (Register item in msg.MessageItems)
                    {
                        if (item.Message == PVSettings.BlockSettings.Message.Send
                            || item.Message == PVSettings.BlockSettings.Message.Both)
                            item.StoreItemValue(ref msg.Payload);
                    }
                    if (msg.PayloadSizeByteVar != null)
                    {
                        UInt32 sizeSize = (UInt32)msg.PayloadSizeByteVar.Length;
                        if (sizeSize == 1)
                            msg.PayloadSizeByteVar.SetByte((byte)msg.PayloadSize);
                        else if (sizeSize == 2)
                            msg.PayloadSizeByteVar.SetBytes((UInt16)msg.PayloadSize);
                        else if (sizeSize == 4)
                            msg.PayloadSizeByteVar.SetBytes((UInt32)msg.PayloadSize);
                    }
                    if (msg.PayloadByteVar != null)
                        msg.PayloadByteVar.SetBytes(ref msg.Payload, 0, (int)msg.PayloadSize);
                }
            }
        }

        protected bool SendRequest()
        {
            LogMessage("SendRequest - Block: " + Name + " - Type: " + Type, LogEntryType.Trace);
            PrepareRegisterData();
            SetupProtocolBindings();

            int count = 0;
            bool timeOut;
            bool result;
            do
            {
                result = Protocol.DoConversation(Conversation.Name, out timeOut);
            }
            while (!result && timeOut && count++ < retryCount);

            if (!result)
            {
                LogMessage("SendRequest - Error in '" + Conversation + "' conversation", LogEntryType.ErrorMessage);
                return false;
            }

            return true;
        }

        protected bool DoQueryResponse(bool continueOnFailure)
        {
            LogMessage("DoQueryResponse - Block: " + Name + " - Type: " + Type, LogEntryType.Trace);
            PrepareRegisterData();
            SetupProtocolBindings();

            int count = 0;
            bool timeOut;
            bool result;
            do
            {
                result = Protocol.DoConversation(Conversation.Name, out timeOut, continueOnFailure);
            }
            while (!result && timeOut && count++ < retryCount);

            if (!result)
            {
                LogMessage("DoQueryResponse - Failure in '" + Conversation + "' conversation", LogEntryType.Information);
                RegisterData = new byte[2];
                return false;
            }

            foreach (BlockMessage msg in BlockMessages)
                if ((msg.MessageType == MessageType.Extract || msg.MessageType == MessageType.ExtractDynamic) && msg.PayloadByteVar != null)
                    msg.Payload = msg.PayloadByteVar.GetBytes();

            foreach (Register item in BlockAllRegisters)
            {
                if (!item.BoundToExtract)
                    continue;

                Type type = item.Binding.GetType();

                if (type == typeof(StringVar))
                    item.ValueString = ((StringVar)item.Binding).Value;
                else if (type == typeof(ByteVar))
                    item.ValueBytes = ((ByteVar)item.Binding).GetBytes();
                else if (type == typeof(DynamicByteVar))
                    item.ValueBytes = ((DynamicByteVar)item.Binding).GetBytes();
            }

            foreach (BlockMessage msg in BlockMessages)
                if (msg.MessageType == MessageType.Extract || msg.MessageType == MessageType.ExtractDynamic)
                    foreach (Register item in msg.MessageItems)
                        if (item.Binding == null)
                            item.GetItemValue(ref msg.Payload);

            if (GlobalSettings.SystemServices.LogTrace)
                LogMessage("DoQueryResponse - Success", LogEntryType.Trace);

            return true;
        }
    }

    public class DeviceBlock_RequestResponse : DeviceBlock_NonModbus
    {
        public DeviceBlock_RequestResponse(DeviceAlgorithm device, BlockSettings blockSettings, Protocol protocol)
            : base(device, blockSettings, protocol)
        {
        }

        protected override bool SendBlock_Special(bool continueOnFailure)
        {
            return SendRequest();
        }

        protected override bool GetBlock_Special(bool ContinueOnFailure, bool dbWrite)
        {
            bool res = DoQueryResponse(ContinueOnFailure);
            return res;
        }
    }

    public class DeviceBlock_Phoenixtec : DeviceBlock_NonModbus
    {
        public DeviceBlock_Phoenixtec(DeviceAlgorithm device, BlockSettings blockSettings, Protocol protocol)
            : base(device, blockSettings, protocol)
        {
        }

        protected override bool SendBlock_Special(bool continueOnFailure)
        {
            return SendRequest();
        }

        protected override bool GetBlock_Special(bool ContinueOnFailure, bool dbWrite)
        {
            return DoQueryResponse(ContinueOnFailure);
        }
    }
}
