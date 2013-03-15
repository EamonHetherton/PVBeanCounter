/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeviceStream;
using Buffers;
using MackayFisher.Utilities;

namespace Conversations
{
    public class Message
    {
        public MessageType MessageType { get; private set; }
        public String Name { get; private set; }
        public List<Element> Elements;
        public bool LogActivity = false;
        public Conversation Conversation;
        public Converse Converse;

        public Message(Conversation conversation, MessageType messageType, String name)
        {
            Conversation = conversation;
            Converse = conversation.Converse;
            MessageType = messageType;
            Name = name;
            Elements = new List<Element>();
            if (MessageType == Conversations.MessageType.Extract && Elements.Count > 1 && Elements[0].GetType() == typeof(DynamicByteVar))
                MessageType = Conversations.MessageType.ExtractDynamic;
        }

        public Variable GetVariable(String varPrefix)
        {
            string value = varPrefix.ToUpper();
            foreach (Element elem in Elements)
            {
                if (elem.GetType() == typeof(UseVariable))
                {
                    if (((UseVariable)elem).Variable.Name.StartsWith(value))
                        return ((UseVariable)elem).Variable;
                }
                else if (elem.GetType() == typeof(Variable))
                {
                    if (((Variable)elem).Name.StartsWith(value))
                        return ((UseVariable)elem).Variable;
                }
            }

            return null;
        }

        public byte[] GetBytes()
        {
            List<byte[]> elementBytes = new List<byte[]>();
            List<byte[]> checksumBytes = new List<byte[]>();
            int size = 0;
            foreach (Element element in Elements)
            {
                if (element.GetType() == typeof(UseVariable) && ((UseVariable)element).Variable.GetType() == typeof(DynamicByteVar))
                    continue;
                else
                {
                    byte[] bytes;

                    if (element.GetType() == typeof(UseVariable) && ((UseVariable)element).IsChecksum)
                    {
                        if (((UseVariable)element).IsChecksum16)
                            bytes = ((UseVariable)element).GetChecksum16Bytes(checksumBytes);
                        else
                            bytes = ((UseVariable)element).GetChecksum8Bytes(checksumBytes);
                    }
                    else
                    {
                        bytes = element.GetBytes();
                        if (!element.ExcludeFromChecksum)
                            checksumBytes.Add(bytes);
                    }

                    elementBytes.Add(bytes);
                    size += bytes.Length;
                }
            }

            byte[] output = new byte[size];
            int pos = 0;

            foreach (byte[] bytes in elementBytes)
            {
                Array.Copy(bytes, 0, output, pos, bytes.Length);
                pos += bytes.Length;
            }

            return output;
        }

        public virtual byte[] GetEscapedBytes()
        {
            return BytesToEscapedBytes(GetBytes());
        }

        public static byte[] BytesToEscapedBytes(byte[] bytes)
        {
            int i;
            int escapeCount = 0;
            for (i = 0; i < bytes.Length; i++)
            {
                switch (bytes[i])
                {
                    case 0xFd:
                    case 0xFe:
                    case 0x11:
                    case 0x12:
                    case 0x13:
                        escapeCount++;
                        break;
                    default:
                        break;
                }
            }

            if (escapeCount > 0)
            {
                int newSize = bytes.Length + escapeCount;
                byte[] newBytes = new byte[newSize];
                int newPos = 0;

                for (i = 0; i < bytes.Length; i++)
                {
                    switch (bytes[i])
                    {
                        case 0xFd:
                        case 0xFe:
                        case 0x11:
                        case 0x12:
                        case 0x13:
                            newBytes[newPos++] = 0x7d;
                            newBytes[newPos] = (byte)(bytes[i] ^ 0x20);
                            break;
                        default:
                            newBytes[newPos] = bytes[i];
                            break;
                    }
                    newPos++;
                }

                return newBytes;
            }
            else
                return bytes;
        }

        public static byte[] EscapedBytesToBytes(byte[] bytes)
        {
            int i;
            int escapeCount = 0;
            for (i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0x7d)
                    escapeCount++;
            }

            if (escapeCount > 0)
            {
                int newSize = bytes.Length - escapeCount;
                byte[] newBytes = new byte[newSize];
                int newPos = 0;

                for (i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0x7d)
                    {
                        newBytes[newPos] = (byte)(bytes[++i] ^ 0x20);
                    }
                    else
                        newBytes[newPos] = bytes[i];

                    newPos++;
                }

                return newBytes;
            }
            else
                return bytes;
        }

        public String MessageText
        {
            get
            {
                String msg;
                if (MessageType == MessageType.Extract || MessageType == MessageType.ExtractDynamic)
                    msg = "E";
                else if (MessageType == MessageType.Read)
                    msg = "R";
                else if (MessageType == MessageType.Find)
                    msg = "F";
                else
                    msg = "S";

                foreach (Element elem in Elements)
                    msg += " " + elem.ElementText;

                return msg;
            }
        }

        private void AddUseVariable(String useVariable, bool isExtract)
        {
            String varName;
            String type = null;
            int? size = null;


            int bracketPos = useVariable.IndexOf('(');

            if (bracketPos == -1)
                varName = useVariable;
            else if (bracketPos == 0)
            {
                Converse.LogMessage("AddUseVariable", "Invalid variable reference - '(' position: " + useVariable, LogEntryType.ErrorMessage);
                return;
            }
            else try
                {
                    varName = useVariable.Substring(0, bracketPos).Trim();
                    int typeStartPos = bracketPos + 1;
                    int endBracketPos = useVariable.IndexOf(')', typeStartPos);

                    if (endBracketPos > bracketPos)
                    {
                        int sizeBracketPos = useVariable.IndexOf('[', typeStartPos);
                        if (sizeBracketPos > typeStartPos)
                        {
                            int sizeStartPos = sizeBracketPos + 1;
                            type = useVariable.Substring(typeStartPos, sizeBracketPos - typeStartPos).Trim();
                            int sizeEndBracketPos = useVariable.IndexOf(']', sizeStartPos);
                            if (sizeEndBracketPos > sizeBracketPos)
                                size = System.Convert.ToInt32(useVariable.Substring(sizeStartPos, sizeEndBracketPos - sizeStartPos).Trim());
                        }
                        else if (sizeBracketPos == -1)
                            type = useVariable.Substring(typeStartPos, endBracketPos - typeStartPos).Trim();
                    }
                    else
                    {
                        Conversation.Converse.LogMessage("AddUseVariable", "Invalid variable reference - ')' position: " + useVariable, LogEntryType.ErrorMessage);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Conversation.Converse.LogMessage("AddUseVariable", "Parsing: " + useVariable + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    return;
                }

            Elements.Add(new UseVariable(Conversation, varName, type, size));
        }

        internal void ParseMessage(String msgString)
        {
            int pos = 0;
            bool isExtract = (MessageType == MessageType.Extract);

            while (pos < msgString.Length)
            {
                int varPos = msgString.IndexOf('$', pos);
                if (varPos >= 0)
                {
                    int varEndPos = msgString.IndexOf(' ', varPos);
                    if (varEndPos == -1)
                        varEndPos = msgString.Length;

                    String literal = msgString.Substring(pos, varPos - pos).Trim();
                    String varString = msgString.Substring(varPos + 1, varEndPos - (varPos + 1)).Trim();

                    if (literal.Length > 0)
                    {
                        if (isExtract)
                            throw new Exception("Literal not allowed in Extract command");

                        Elements.Add(new Literal(literal, Conversation));
                    }
                    if (varString.Length > 0)
                        AddUseVariable(varString, isExtract);

                    pos = varEndPos;
                }
                else // only literal text remains
                {
                    String literal = msgString.Substring(pos).Trim();
                    if (literal.Length > 0)
                    {
                        //if (isExtract)
                        //    throw new Exception("Literal not allowed in Extract command");

                        Elements.Add(new Literal(literal, Conversation));
                    }
                    pos = msgString.Length;
                }
            }
        }

        private static bool CheckActual(MessageRecord msgRecord)
        {
            int pos = 0;
            bool result = true;

            foreach (Element element in msgRecord.Message.Elements)
            {
                int elemPos = 0;
                int length = element.Length;
                bool known = true;
                Variable var = null;

                if (element.GetType() == typeof(UseVariable))
                {
                    var = ((UseVariable)element).Variable;
                    known = !var.IsUnknown;
                }

                int bytesRead = msgRecord.MatchInfo.BytesRead;

                if (msgRecord.Message.MessageType == MessageType.Read || element.GetType() == typeof(Literal))
                    while (elemPos < length)
                    {
                        int testPos = pos + elemPos;
                        if (testPos >= bytesRead || msgRecord.Actual[testPos] != msgRecord.Expected[testPos])
                        {
                            msgRecord.ByteResults[testPos] = ByteStatus.Mismatch;
                            result = false;
                        }
                        else
                            msgRecord.ByteResults[testPos] = ByteStatus.Match;
                        elemPos++;
                    }
                else if (msgRecord.Message.MessageType == MessageType.Extract)
                {
                    if (var.GetType() == typeof(ByteVar))
                    {
                        ByteVar byteVar = (ByteVar)var;
                        byteVar.SetBytes(ref msgRecord.Actual, pos, length);
                        //byteVar.ValueSet = true;
                        while (elemPos < length)
                        {
                            int testPos = pos + elemPos;
                            if (testPos >= bytesRead)
                            {
                                msgRecord.ByteResults[testPos] = ByteStatus.Mismatch;
                                result = false;
                            }
                            msgRecord.ByteResults[testPos] = ByteStatus.Extract;
                            elemPos++;
                        }
                    }
                }
                pos += length;
            }
            return result;
        }

        private static void LoadDynamic(MessageRecord msgRecord, DataChunk chunk)
        {
            Element element = msgRecord.Message.Elements[0];  // Dynamic must be first element

            Variable var = null;

            if (element.GetType() == typeof(UseVariable))
                var = ((UseVariable)element).Variable;
            else
                return;

            if (var.GetType() != typeof(DynamicByteVar))
                return;

            DynamicByteVar byteVar = (DynamicByteVar)var;

            byteVar.SetBytes(chunk.ChunkBytes, 0, chunk.ChunkSize);
            //byteVar.ValueSet = true;
        }

        public bool Send(out MessageRecord sentMessage)
        {
            MessageRecord msgRecord = new MessageRecord();
            msgRecord.Message = this;

            msgRecord.Actual = GetBytes();
            msgRecord.Expected = null;
            msgRecord.ByteResults = null;
            sentMessage = msgRecord;

            DateTime now = DateTime.Now;
            Double gap = (now - Converse.LastSendTime).TotalMilliseconds;
            if (gap < Converse.SendGap)
                System.Threading.Thread.Sleep((int)(Converse.SendGap - gap));

            Converse.LastSendTime = DateTime.Now;

            if (Converse.DeviceStream.Write(msgRecord.Actual, 0, msgRecord.Actual.Length))
                return true;
            else
            {
                Converse.UtilityLog.LogMessage("Message.Send - FAILED - Message: ", MessageText, LogEntryType.ErrorMessage);
                return false;
            }
        }

        public MessageRecord Receive(TimeSpan? timeOut, bool continueOnFailure)
        {
            MessageRecord msgRecord = new MessageRecord();
            msgRecord.Message = this;

            byte[] expected = GetBytes();

            if (timeOut == null && !Converse.NoTimeout)
                timeOut = TimeSpan.FromSeconds(Converse.DefaultTimeOut);

            msgRecord.Expected = expected;

            if (MessageType == Conversations.MessageType.ExtractDynamic)
            {
                DataChunk skipped = null;

                msgRecord.MatchInfo = Converse.DeviceStream.FindInBuffer(expected, 20000, true, out skipped, timeOut);
                LoadDynamic(msgRecord, skipped);
                msgRecord.Conformant = msgRecord.MatchInfo.Matched;
            }
            else
            {
                if (continueOnFailure)  // cannot consume if no match so check for a match before removing from buffer
                {
                    DataChunk skipped = null;
                    msgRecord.MatchInfo = Converse.DeviceStream.FindInBuffer(expected, 0, true, out skipped, timeOut, false);
                    msgRecord.Conformant = msgRecord.MatchInfo.Matched;
                    if (!msgRecord.MatchInfo.Matched)
                    {
                        if (Converse.UtilityLog.LogMessageContent)
                        {
                            Converse.UtilityLog.LogMessage("Message.Receive", "Message: " + MessageText + " - Mismatch - Expected: ", LogEntryType.MeterMessage);
                            ByteBuffer.FormatDump(expected, 0, expected.Length, Converse.DeviceStream.SystemServices);
                            Converse.UtilityLog.LogMessage("Message.Receive", "Mismatch - Found: ", LogEntryType.MeterMessage);
                            ByteBuffer.FormatDump(skipped.ChunkBytes, 0, skipped.ChunkBytes.Length, Converse.DeviceStream.SystemServices);
                        }
                        return msgRecord;
                    }
                }
                msgRecord.MatchInfo = Converse.DeviceStream.ReadFromBuffer(expected.Length, expected.Length, out msgRecord.Actual, timeOut);
                msgRecord.ByteResults = new ByteStatus[expected.Length];
                msgRecord.Conformant = CheckActual(msgRecord);
                if (!msgRecord.Conformant.Value && Converse.UtilityLog.LogMessageContent)
                {
                    Converse.UtilityLog.LogMessage("Message.Receive", "Message: " + MessageText + " - Mismatch - Expected: ", LogEntryType.MeterMessage);
                    ByteBuffer.FormatDump(expected, 0, expected.Length, Converse.DeviceStream.SystemServices);
                    Converse.UtilityLog.LogMessage("Message.Receive", "Mismatch - Found: ", LogEntryType.MeterMessage);
                    ByteBuffer.FormatDump(msgRecord.Actual, 0, msgRecord.Actual.Length, Converse.DeviceStream.SystemServices);
                }

            }

            return msgRecord;
        }

        public MessageRecord Find(TimeSpan? timeOut, bool continueOnFailure)
        {
            MessageRecord msgRecord = new MessageRecord();
            msgRecord.Message = this;

            if (timeOut == null && !Converse.NoTimeout)
                timeOut = TimeSpan.FromSeconds(Converse.DefaultTimeOut);

            byte[] expected = GetBytes();

            msgRecord.Expected = expected;

            DataChunk skipped = null;

            msgRecord.MatchInfo = Converse.DeviceStream.FindInBuffer(expected, 5000, true, out skipped, timeOut);
            if (msgRecord.MatchInfo.BytesSkipped > 0 && Converse.UtilityLog.LogMessageContent)
            {
                Converse.LogMessage("Message.Find", "Skipped " + msgRecord.MatchInfo.BytesSkipped + " bytes: " + ByteBuffer.BytesToHex(skipped.ChunkBytes, " ", (int)skipped.ChunkSize)
                    + " - Total Read: " + msgRecord.MatchInfo.TotalBytesRead, LogEntryType.MeterMessage);
            }

            msgRecord.Conformant = msgRecord.MatchInfo.Matched;

            if (Converse.UtilityLog.LogMeterTrace)
                if (msgRecord.Conformant.HasValue && msgRecord.Conformant.Value)
                    Converse.LogMessage("Message.Find", "Found: " + ByteBuffer.BytesToHex(expected, " ", expected.GetUpperBound(0) + 1));
                else
                    Converse.LogMessage("Message.Find", "Not Found: " + ByteBuffer.BytesToHex(expected, " ", expected.GetUpperBound(0) + 1));

            return msgRecord;
        }
    }
}
