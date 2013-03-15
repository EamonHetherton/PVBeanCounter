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
using System.Net;
using System.IO;
using DeviceStream;
//using InTheHand.Net.Sockets;
//using InTheHand.Net.Bluetooth;
//using InTheHand.Net;
using Buffers;
using MackayFisher.Utilities;

namespace Conversations
{
    public class ConvException : Exception
    {
        public ConvException(String message)
            : base(message)
        {
        }
    }

    public enum ByteStatus
    {
        Match,
        Mismatch,
        Extract,
        Ignored
    }

    public class MessageNotReceivedException : Exception
    {
    }

    public class MessageRecord
    {
        public Message Message = null;
        public byte[] Expected = null;
        public byte[] Actual = null;
        public ByteStatus[] ByteResults = null;
        public bool? Conformant = null;
        public MatchInfo MatchInfo = null;
    }

    public enum MessageType
    {
        // first value must start at 0
        Read = 0,
        Find,
        Extract,
        ExtractDynamic,
        Send,
        Unknown,   // used when config error condition exists - cannot locate message definition
        ValueCount // leave ValueCount as last value - it supplies the number of values in the enum
    }

    public class Conversation
    {
        public String Name { get; private set; }

        public List<Message> Messages;
        public Converse Converse { get; private set; }

        IUtilityLog UtilityLog;

        public Conversation(Converse converse, String conversationName, IUtilityLog log)
        {
            Converse = converse;
            UtilityLog = log;
            Name = conversationName;
            Messages = new List<Message>();
        }

        internal void LogMessage(String message, LogEntryType logEntryType)
        {
            UtilityLog.LogMessage("Conversation", message, logEntryType);
        }

        public bool GetVariableUsage(string variableName, out bool inSend, out bool inRead, out bool inFind, out bool inExtract)
        {
            inSend = false;
            inRead = false;
            inFind = false;
            inExtract = false;

            Variable var = GetVariable(variableName);
            if (var == null)
                return false;

            foreach (Message msg in Messages)
            {
                bool isDynamic = false;
                foreach (Element elem in msg.Elements)
                {
                    if (elem.GetType() != typeof(UseVariable))
                        continue;

                    UseVariable useVar = (UseVariable)elem;
                    if (useVar.Variable == var)
                    {
                        if (msg.MessageType == MessageType.Read)
                            inRead = true;
                        else if (msg.MessageType == MessageType.Send)
                            inSend = true;
                        else if (msg.MessageType == MessageType.Extract)
                        {
                            if (isDynamic)
                                inFind = true;
                            else
                                inExtract = true;
                        }
                        else if (msg.MessageType == MessageType.Find)
                            inFind = true;
                    }
                    if (useVar.Variable.GetType() == typeof(DynamicByteVar))
                        isDynamic = true;
                }
            }

            return true;
        }

        public Message GetMessage(string messageName)
        {
            foreach (Message msg in Messages)
                if (msg.Name == messageName)
                    return msg;
            return null;
        }

        public Variable GetVariable(String varName)
        {
            return Converse.GetSessionVariable(varName, this);
        }

        public bool Execute(out bool timeout, bool continueOnFailure = false)
        {
            Message msg = null;
            try
            {
                int i = 0;
                while (i < Messages.Count)
                {
                    msg = Messages[i];

                    if (UtilityLog.LogMeterTrace)
                        Converse.LogMessage("Conversation.Execute", "Message: " + i + " - Text: " + msg.MessageText);

                    MessageRecord msgRecord;

                    if (msg.MessageType == MessageType.Send)
                    {
                        if (!msg.Send(out msgRecord))
                        {
                            timeout = false;
                            Converse.DeviceStream.PurgeStreamBuffers();
                            return false;
                        }
                    }
                    else
                    {
                        if (msg.MessageType == MessageType.Find)
                            msgRecord = msg.Find(null, continueOnFailure);
                        else
                            msgRecord = msg.Receive(null, continueOnFailure);

                        if (!msgRecord.Conformant.Value)
                        {
                            timeout = msgRecord.MatchInfo.Timeout;
                            if (continueOnFailure)
                                return false;
                            Converse.DeviceStream.PurgeStreamBuffers();
                            return false;
                        }
                    }

                    i++;
                }
                timeout = false;
            }
            catch (Exception e)
            {
                Converse.LogMessage("Conversation.Execute", "Scan Messages - Exception: " + e.Message, LogEntryType.ErrorMessage);
                msg.LogActivity = false;
                throw e;
            }
            if (UtilityLog.LogMeterTrace)
                Converse.LogMessage("Conversation.Execute", "Name: " + Name + " - complete");
            return true;
        }
    }

    public class ConversationExporter
    {
        private System.IO.StreamWriter StreamWriter;

        public ConversationExporter(String fileName, SMABluetooth_Converse session)
        {
            StreamWriter = new StreamWriter(fileName);

            foreach (Conversation conv in session.Conversations)
            {
                StreamWriter.WriteLine(":" + conv.Name + " $END;");
                foreach (Message msg in conv.Messages)
                {
                    StreamWriter.WriteLine(msg.MessageText + " $END;");
                }
            }

            StreamWriter.Close();
        }
    }

    public class ConversationLoader
    {
        private System.IO.StreamReader StreamReader;
        public bool EOF { get; private set; }

        private IUtilityLog UtilityLog;

        private String EndCommand = "$END;";
        private String CommentStart = "//";

        private String Buffer;
        private int BufferPos;  // next unused position in Buffer

        public String CurrentCommand { get; private set; }

        public ConversationLoader(String fileName, IUtilityLog log)
        {
            UtilityLog = log;
            StreamReader = new StreamReader(fileName);
            EOF = false;
            Buffer = "";
            BufferPos = 0;
        }

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            UtilityLog.LogMessage("ConversationLoader", message, logEntryType);
        }

        private String GetNextLine()
        {
            String line;
            bool lineFound = false;

            do
            {
                bool isComment = false;
                if (StreamReader.EndOfStream)
                {
                    line = "";
                    lineFound = true;
                    EOF = true;
                }
                else
                {
                    line = StreamReader.ReadLine();
                    if (line.Trim() != "")
                    {
                        isComment = (line[0] == '#');
                        lineFound = !isComment;
                    }
                }
            }
            while (!lineFound);

            return line;
        }

        internal String GetNextCommand()
        {
            String command = "";
            bool endFound = false;

            do
            {

                if (BufferPos >= Buffer.Length)
                {
                    Buffer = GetNextLine();
                    BufferPos = 0;

                    // discard comment at end of line
                    int commentPos = Buffer.IndexOf(CommentStart, BufferPos);
                    if (commentPos >= 0)
                    {
                        Buffer = Buffer.Substring(0, commentPos);
                    }
                }

                endFound = EOF;

                if (Buffer.Length > 0)
                {
                    // detect command end marker
                    int pos = Buffer.IndexOf(EndCommand, BufferPos);

                    if (pos == -1)
                    {
                        // command does not end on this line
                        command += Buffer.Substring(BufferPos);
                        Buffer = "";
                        BufferPos = 0;
                    }
                    else
                    {
                        // command ends on this line
                        command += Buffer.Substring(BufferPos, pos - BufferPos);
                        BufferPos = pos + EndCommand.Length + 1;
                        if (command.Length > 0)
                        {
                            endFound = true;
                            // discard all after EndCommand marker
                            //BufferPos = 0;
                            //Buffer = "";
                        }
                    }
                }
            } while (!endFound);

            CurrentCommand = command.TrimStart();
            return CurrentCommand;
        }

        public void LoadConversation(Converse session, Conversation conv)
        {
            MessageType messageType = MessageType.Read;
            Message curMessage = null;

            String message = GetNextCommand();
            while (message.Length > 0)
            {
                // check for start of new conversation
                if (message[0] == ':')
                    break;
                message = message.ToUpper();
                char msgType = message[0];

                if (msgType == 'R')
                {
                    messageType = MessageType.Read;
                }
                else if (msgType == 'F')
                {
                    messageType = MessageType.Find;
                }
                else if (msgType == 'E')
                {
                    messageType = MessageType.Extract;
                }
                else if (msgType == 'S')
                {
                    messageType = MessageType.Send;
                }

                /*
                if (messageType == MessageType.Extract)
                {
                    if (curMessage == null || curMessage.MessageType == MessageType.Send)
                        throw new Exception("Extract must follow Receive message");
                }
                */

                curMessage = new Message(conv, messageType, "");
                conv.Messages.Add(curMessage);

                curMessage.ParseMessage(message.Substring(1).Trim());

                message = GetNextCommand();
            }
        }

        public void LoadConversations(Converse session)
        {
            GetNextCommand();
            while (!EOF)
            {
                String name = "";
                // should be conversation label
                if (CurrentCommand[0] == ':')
                {
                    if (CurrentCommand.Length < 2)
                        throw new Exception("Conversation Name has zero length");
                    name = CurrentCommand.Substring(1).Trim();
                    if (name.Length < 1)
                        throw new Exception("Conversation Name has zero length");
                }
                else
                    throw new Exception("Conversation Name missing");

                Conversation conv = new Conversation(session, name, UtilityLog);
                LoadConversation(session, conv);
                session.Conversations.Add(conv);
            }
        }
    }
}
