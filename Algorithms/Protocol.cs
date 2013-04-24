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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Conversations;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace Algorithms
{
    public class Protocol : Converse
    {
        public ProtocolSettings.ProtocolType Type { get; private set; }
        public ProtocolSettings ProtocolSettings { get; private set; }

        public Protocol(ProtocolSettings protocolSettings, IConverseCheckSum16 converseCheckSum = null)
            : base(GlobalSettings.SystemServices, null)
        {
            ProtocolSettings = protocolSettings;
            Type = ProtocolSettings.Type;
            if (converseCheckSum == null)
            {
                if (ProtocolSettings.CheckSum == "ModbusCheckSum16" || ProtocolSettings.CheckSum == "ModbusCRC16")
                    Calculations = new Conversations.ModbusConverseCalculations();
                else if (ProtocolSettings.CheckSum == "CRC16")
                    Calculations = new Conversations.CRC16ConverseCalculations();
                else if (ProtocolSettings.CheckSum == "CheckSum8")
                    Calculations = new Conversations.FroniusConverseCalculations();
                else
                    new Conversations.ConverseCalculations();
            }
            else
                Calculations = converseCheckSum;
            LoadConverse();
        }

        private void LoadConverse()
        {
            if (ProtocolSettings.Endian16Bit == "Little")
                EndianConverter16Bit = new EndianConverter16Bit(EndianConverter.LittleEndian16Bit);
            else
                EndianConverter16Bit = new EndianConverter16Bit(EndianConverter.BigEndian16Bit);
            if (ProtocolSettings.Endian32Bit == "Little")
                EndianConverter32Bit = new EndianConverter32Bit(EndianConverter.LittleEndian32Bit);
            else if (ProtocolSettings.Endian32Bit == "BigLittle")
                EndianConverter32Bit = new EndianConverter32Bit(EndianConverter.BigLittleEndian32Bit);
            else
                EndianConverter32Bit = new EndianConverter32Bit(EndianConverter.BigEndian32Bit);

            if (ProtocolSettings.CheckSumEndian16Bit == ProtocolSettings.Endian16Bit)
                CheckSumEndianConverter16Bit = EndianConverter16Bit;
            else if (ProtocolSettings.CheckSumEndian16Bit == "Little")
                CheckSumEndianConverter16Bit = new EndianConverter16Bit(EndianConverter.LittleEndian16Bit);
            else
                CheckSumEndianConverter16Bit = new EndianConverter16Bit(EndianConverter.BigEndian16Bit);

            foreach (ConversationSettings settings in ProtocolSettings.Conversations)
                LoadConversation(settings);
        }

        private Message LoadMessage(Conversation conv, MessageSettings settings)
        {
            MessageType type = settings.Type;
            if (type == MessageType.ValueCount)
                return null;

            Message message = new Message(conv, type, settings.Name);

            foreach (MessageSettings.ElementListValue element in settings.ElementList)
            {
                if (element.Type == "BYTE")
                {
                    if (element.ValueString != null)
                    {
                        message.Elements.Add(new Literal(element.ValueString, conv, element.ExcludeFromChecksum));
                    }
                    else
                    {
                        UseVariable useVar = new UseVariable(conv, element.Name, element.Type, element.Size, element.ExcludeFromChecksum);
                        message.Elements.Add(useVar);
                        if (element.SizeName != null)
                        {
                            try
                            {
                                ByteVar sizeVar = (ByteVar)conv.GetVariable(element.SizeName);
                                sizeVar.AddResizeVariable((ByteVar)useVar.Variable);
                            }
                            catch (Exception) // exceptions are caused by incorrect element types - both must be ByteVar
                            {
                            }
                        }
                    }
                }
                else if (element.Type == "DYNAMICBYTE")
                {
                    UseVariable useVar = new UseVariable(conv, element.Name, element.Type, element.Size, element.ExcludeFromChecksum);
                    message.Elements.Add(useVar);
                    if (element.SizeName != null)
                    {
                        try
                        {
                            ByteVar sizeVar = (ByteVar)conv.GetVariable(element.SizeName);
                            sizeVar.AddResizeVariable((ByteVar)useVar.Variable);
                        }
                        catch (Exception) // exceptions are caused by incorrect element types - both must be ByteVar
                        {
                        }
                    }
                }
                else if (element.Type == "STRING")
                {
                    UseVariable useVar = new UseVariable(conv, element.Name, element.Type, element.Size, element.ExcludeFromChecksum);
                    message.Elements.Add(useVar);
                }
            }

            return message;
        }

        private Conversation LoadConversation(ConversationSettings settings)
        {
            Conversation conv = new Conversation(this, settings.Name, UtilityLog);
            Conversations.Add(conv);

            foreach (MessageSettings msg in settings.MessageList)
            {
                Message message = LoadMessage(conv, msg);
                if (message == null)
                    return conv;
                conv.Messages.Add(message);
            }

            return conv;
        }
    }
}

