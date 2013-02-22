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
using System.Linq;
using System.Text;
using System.IO;
using DeviceStream;
using Buffers;
using MackayFisher.Utilities;

namespace Conversations
{
    public class Converse
    {
        public EndianConverter16Bit CheckSumEndianConverter16Bit { get; protected set; }
        public EndianConverter16Bit EndianConverter16Bit { get; protected set; }
        public EndianConverter32Bit EndianConverter32Bit { get; protected set; }

        public List<Conversation> Conversations;
        public List<Variable> Variables;
        public DeviceStream.DeviceStream DeviceStream { get; private set; }

        public IConverseCheckSum16 Calculations;

        public bool NoTimeout { set; get; }

        public int DefaultTimeOut = 5;

        public IUtilityLog UtilityLog;

        public DateTime LastSendTime;
        public Double SendGap;

        public Converse(IUtilityLog log, IConverseCheckSum16 calculations, int sendGap = 1200)
        {
            LastSendTime = DateTime.Today;
            SendGap = sendGap;
            UtilityLog = log;
            Conversations = new List<Conversation>();
            Variables = new List<Variable>();
            DeviceStream = null;
            NoTimeout = false;
            if (calculations == null)
            {
                Calculations = (IConverseCheckSum16)new ConverseCalculations();
                LogMessage("Converse", "Using standard Calculations: " + Calculations.GetType().ToString(), LogEntryType.MeterTrace);
            }
            else
            {
                Calculations = calculations;
                LogMessage("Converse", "Using custom Calculations: " + Calculations.GetType().ToString(), LogEntryType.MeterTrace);
            }
            
            EndianConverter16Bit = new EndianConverter16Bit(EndianConverter.BigEndian16Bit);          
            EndianConverter32Bit = new EndianConverter32Bit(EndianConverter.BigEndian32Bit);
            CheckSumEndianConverter16Bit = EndianConverter16Bit;
        }

        public void SetCheckSum16Endian(bool isLittleEndian = false)
        {
            if (isLittleEndian)
                CheckSumEndianConverter16Bit = new EndianConverter16Bit(EndianConverter.LittleEndian16Bit);    
            else
                CheckSumEndianConverter16Bit = new EndianConverter16Bit(EndianConverter.BigEndian16Bit);
        }

        public void LogMessage(String routine, String message, LogEntryType logEntryType = LogEntryType.MeterTrace)
        {
            UtilityLog.LogMessage("Converse: " + routine, message, logEntryType);
        }

        public void SetDeviceStream(DeviceStream.DeviceStream stream)
        {
            DeviceStream = stream;
        }

        public Variable GetSessionVariable(String varName, Conversation conversation = null)
        {
            try
            {
                String var = varName.ToUpper();
                bool conversationScope = var.StartsWith("%PAYLOAD");
                foreach (Variable v in Variables)
                {
                    if (v.Name == var)
                        if (!conversationScope || conversation == v.InitialConversation)
                            return v;
                }
                return null;
            }
            catch (Exception e)
            {
                LogMessage("GetVariable", "varName: " + varName + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }

        public Conversation GetConversation(String conversationName)
        {
            foreach (Conversation conv in Conversations)
            {
                if (conv.Name == conversationName)
                    return conv;
            }
            return null;
        }

        public Message GetMessage(string conversation, string messageName)
        {
            Conversation conv = GetConversation(conversation);
            if (conv == null)
                return null;

            return conv.GetMessage(messageName);
        }

        public bool GetVariableUsage(string conversation, string variableName, out bool inSend, out bool inRead, out bool inFind, out bool inExtract)
        {
            Conversation conv = GetConversation(conversation);
            if (conv == null)
            {
                inSend = false;
                inRead = false;
                inFind = false;
                inExtract = false;
                return false;
            }

            return conv.GetVariableUsage(variableName, out inSend, out inRead, out inFind, out inExtract);
        }

        public bool DoConversation(String conversationName, out bool timeout, bool continueOnFailure = false)
        {
            Conversation conv = null;

            if (UtilityLog.LogMeterTrace)
                LogMessage("DoConversation", "Name: " + conversationName + " - starting");
            try
            {
                conv = GetConversation(conversationName);
            }
            catch (Exception e)
            {
                LogMessage("DoConversation", "Initialise - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }

            if (conv == null)
                throw new Exception("DoConversation: Conversation not found - " + conversationName);
            if (DeviceStream == null)
                throw new Exception("DoConversation: No stream set for conversation - " + conversationName);

            return conv.Execute(out timeout, continueOnFailure);
        }

        public bool DoConversation(String conversationName, bool continueOnFailure = false)
        {
            bool timeout;

            return DoConversation(conversationName, out timeout, continueOnFailure);
        }
    }
}
