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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MackayFisher.Utilities;

namespace Conversations
{
    public abstract class Element
    {
        // byte order represented with 0 being the byte array position of the low order byte
        // common storage formats
        public static readonly byte[] BigEndian64Bit = { 7, 6, 5, 4, 3, 2, 1, 0 };
        public static readonly byte[] LittleEndian64Bit = { 0, 1, 2, 3, 4, 5, 6, 7 };
        public static readonly byte[] BigEndian32Bit = { 3, 2, 1, 0 };
        public static readonly byte[] LittleEndian32Bit = { 0, 1, 2, 3 };
        public static readonly byte[] BigEndian16Bit = { 1, 0 };
        public static readonly byte[] LittleEndian16Bit = { 0, 1 };

        // unusual formats
        // BigLittleEndian32Bit is an "on the wire" format found in Modbus transfers of 32 bit data
        public static readonly byte[] BigLittleEndian32Bit = { 1, 0, 3, 2 }; // 16 bit values are internal BigEndian. but the 16 bit "words" are LittleEndian
        public static readonly byte[] LittleBigEndian32Bit = { 2, 3, 0, 1 }; // 16 bit values are internal LittleEndian. but the 16 bit "words" are BigEndian

        public Converse Converse { get; private set; }
        public Conversation InitialConversation { get; private set; }

        public bool ExcludeFromChecksum { get; private set; }

        public Element(Conversation conversation, bool excludeFromChecksum = false)
        {
            InitialConversation = conversation;
            Converse = conversation.Converse;
            ExcludeFromChecksum = excludeFromChecksum;
        }

        public abstract int Length { get; }

        public virtual String GetHexString()
        {
            byte[] b = GetBytes();
            return SystemServices.BytesToHex(ref b, 1, "", "");
        }

        public virtual String GetDisplayString()
        {
            byte[] b = GetBytes();
            return SystemServices.BytesToHex(ref b, 1, " ", "");
        }

        public abstract byte[] GetBytes();


        public static String StringToHex(String input, String separator = " ")
        {
            char[] values = input.ToCharArray();
            String output = "";
            String hexOutput;
            foreach (char letter in values)
            {
                int value = Convert.ToInt32(letter);
                // Convert the decimal value to a hexadecimal value in string form.
                hexOutput = String.Format("{0:X2}", value);
                if (output != "")
                    output = output + separator + hexOutput;
                else
                    output = hexOutput;
            }
            return output;
        }

        public abstract String ElementText { get; }
    }

    public abstract class Variable : Element
    {
        //public bool ValueSet { get; internal set; }

        public String Name { get; private set; }
        public int MessageId { get; private set; }

        public bool IsUnknown { get; set; }

        public Variable(String varName, Conversation conversation, int messageId)
            : base(conversation, false)
        {
            Name = varName.ToUpper();
            MessageId = messageId;
            IsUnknown = false;
        }

        public override String ElementText
        {
            get
            {
                return "$" + Name;
            }
        }
    }

    public class StringVar : Variable
    {
        private String strValue;
        public String Value
        {
            get
            {
                return strValue;
            }
            set
            {
                strValue = value;
                //ValueSet = true;
            }
        }

        public StringVar(String varName, Conversation conversation, int messageId = 0)
            : base(varName, conversation, messageId)
        {
            strValue = "";
        }

        public override int Length
        {
            get
            {
                return strValue.Length;
            }
        }

        public override byte[] GetBytes()
        {
            return SystemServices.StringToBytes(strValue);
        }

        public override String ToString()
        {
            return strValue;
        }
    }

    public class CRC : Variable
    {
        private byte[] addressBytes;
        public CRC(String varName, Conversation conversation, int messageId = 0)
            : base(varName, conversation, messageId)
        {
            addressBytes = null;
        }

        public override byte[] GetBytes()
        {
            if (addressBytes == null)
                return null;

            byte[] copy = new byte[addressBytes.Length];
            addressBytes.CopyTo(copy, 0);
            return copy;
        }

        public override int Length
        {
            get
            {
                if (addressBytes == null)
                    return 0;
                return addressBytes.Length;
            }
        }

        public override String ToString()
        {
            return "Invalid";
        }
    }

    public class TimeVar : Variable
    {
        private byte[] addressBytes;
        public TimeVar(String varName, Conversation conversation, int messageId = 0)
            : base(varName, conversation, messageId)
        {
            addressBytes = null;
        }

        public override byte[] GetBytes()
        {
            if (addressBytes == null)
                return null;

            byte[] copy = new byte[addressBytes.Length];
            addressBytes.CopyTo(copy, 0);
            return copy;
        }

        public override String ToString()
        {
            return "Invalid";
        }

        public override int Length
        {
            get
            {
                if (addressBytes == null)
                    return 0;
                return addressBytes.Length;
            }
        }
    }

    public class ByteVar : Variable
    {
        private byte[] Bytes = null;
        private int? FixedSize;
        private List<ByteVar> ResizeVariables;

        public ByteVar(String varName, int? size, Conversation conversation, int messageId = 0)
            : base(varName, conversation, messageId)
        {
            FixedSize = size;
            if (FixedSize.HasValue)
                Bytes = new byte[FixedSize.Value];
            else
                Bytes = new byte[0];
            ResizeVariables = new List<ByteVar>();
        }

        // The generic function calls GetBytes which can cause recursion with Trace on
        // This gives the same result without calling GetBytes
        public override int Length { get { return Bytes.Length; } }

        public override byte[] GetBytes()
        {
            /*
            if (Converse.UtilityLog.LogTrace && Name.StartsWith("%PAYLOAD"))
            {
                Converse.UtilityLog.LogMessage("ByteVar.GetBytes - " + Name,
                    "Bytes: '" + BytesToHex(ref Bytes, "-") + " - Length: " + Bytes.Length +
                    " - ToString: '" + ToString() + "'",
                    LogEntryType.Trace);
            }
            */
            byte[] copy = new byte[Bytes.Length];
            Bytes.CopyTo(copy, 0);
            return copy;
        }

        public void AddResizeVariable(ByteVar variable)
        {
            ResizeVariables.Add(variable);
        }

        public void Initialise(byte val = 0)
        {
            for (int i = 0; i < Bytes.Length; i++)
                Bytes[i] = val;
        }

        public void Resize(UInt16 newSize)
        {
            /*
            if (Converse.UtilityLog.LogTrace && Name.StartsWith("%PAYLOAD"))
            {
                Converse.UtilityLog.LogMessage("ByteVar.Resize - " + Name,
                    "Resize called - value cleared", LogEntryType.Trace);
            }
            */
            if (newSize != Bytes.Length)
                Bytes = new byte[newSize];
        }

        public void SetBytes(ref byte[] bytes, int start, int length, int outStart = 0)
        {
            if (!FixedSize.HasValue && length != Bytes.Length)
                Bytes = new byte[length];

            int pos = outStart;
            int inPos = start;
            int cnt = 0;
            while (inPos < bytes.Length && pos < Bytes.Length && cnt < length)
            {
                Bytes[pos++] = bytes[inPos++];
                cnt++;
            }
            if (pos < Bytes.Length)
                throw new Exception("Variable: " + Name + " - assigned size too small - expected " + Bytes.Length + " - assigned " + bytes.Length);

            foreach (ByteVar var in ResizeVariables)
            {
                UInt16 i;

                if (Bytes.Length == 1)
                    i = GetByte();
                else if (Bytes.Length == 2)
                    i = GetUInt16();
                else if (Bytes.Length == 4)
                    i = (UInt16)GetUInt32();
                else
                    continue;

                var.Resize(i);
            }

            /*
            if (Converse.UtilityLog.LogTrace && Name.StartsWith("%PAYLOAD"))
            {
                Converse.UtilityLog.LogMessage("ByteVar.SetBytes - " + Name,
                    "Bytes: '" + BytesToHex(ref bytes, "-", start, length) + "' - Start: " + start + " - Length: " + length +
                    " - Bytes.Length: " + Bytes.Length +
                    " - ToString: '" + ToString() + "'",
                    LogEntryType.Trace);
            }
            */
        }

        public void SetBytes(String str, byte pad = (byte)' ', int outStart = 0)
        {
            int pos = outStart;
            int inPos = 0;
            while (inPos < str.Length && pos < Bytes.Length)
            {
                Bytes[pos++] = (byte)str[inPos++];
            }
            while (pos < Bytes.Length)
            {
                Bytes[pos++] = pad;
            }
        }

        public void SetBytes(UInt16 uint16, int pos = 0)
        {
            byte[] varBytes = Converse.EndianConverter16Bit.GetExternalBytes(uint16);
            SetBytes(ref varBytes, 0, 2, pos);
        }

        public void SetBytes(UInt32 uint32, int pos = 0)
        {
            byte[] varBytes = Converse.EndianConverter32Bit.GetExternalBytes(uint32);
            SetBytes(ref varBytes, 0, 4, pos);
        }

        public void SetByte(byte _byte, int pos = 0)
        {
            byte[] varBytes = System.BitConverter.GetBytes(_byte);
            SetBytes(ref varBytes, 0, 1, pos);
        }

        public override String ToString()
        {
            /*
            if (Converse.UtilityLog.LogTrace && Name.StartsWith("%PAYLOAD"))
            {
                Converse.UtilityLog.LogMessage("ByteVar.ToString - " + Name,
                    "Bytes: '" + BytesToHex(ref Bytes, "-") + "' - Bytes.Length: " + Bytes.Length + "' - Length: " + Length,
                    LogEntryType.Trace);
            }
            */
            String val = "";
            try
            {
                val = SystemServices.BytesToString(ref Bytes, Bytes.Length);
            }
            catch (ConvException)
            {
                Converse.UtilityLog.LogMessage("ByteVar.ToString", "ConvException thrown *************", LogEntryType.Trace);
            }
            return val;
        }

        public UInt16 GetUInt16(int pos = 0)
        {
            return Converse.EndianConverter16Bit.GetUInt16FromBytes(ref Bytes, pos);
        }

        public UInt32 GetUInt32(int pos = 0)
        {
            return Converse.EndianConverter32Bit.GetUInt32FromBytes(ref Bytes, pos);
        }

        public Byte GetByte(int pos = 0)
        {
            if (Bytes.Length <= pos)
                return 0;

            return Bytes[pos];
        }
    }

    public class DynamicByteVar : Variable
    {
        private byte[] Bytes;
        public int MaxBytes { get; private set; }
        private int CurrentBytes;

        public DynamicByteVar(String varName, int maxSize, Conversation conversation, int messageId = 0)
            : base(varName, conversation, messageId)
        {
            MaxBytes = maxSize;
            Bytes = null;
            CurrentBytes = 0;
        }

        public override byte[] GetBytes()
        {
            if (Bytes == null)
                return null;

            byte[] copy = new byte[CurrentBytes];
            for (int i = 0; i < CurrentBytes; i++)
                copy[i] = Bytes[i];
            return copy;
        }

        public override int Length
        {
            get
            {
                return CurrentBytes;
            }
        }

        public void SetBytes(byte[] bytes, int start, int length)
        {
            if (length > MaxBytes)
                throw new Exception("Variable: " + Name + " - assigned size too large - max " + MaxBytes + " - assigned " + length);
            Bytes = new byte[length];

            int pos = 0;
            int inPos = start;
            while (inPos < bytes.Length && pos < Bytes.Length)
            {
                Bytes[pos++] = bytes[inPos++];
            }
            if (pos < Bytes.Length)
                throw new Exception("Variable: " + Name + " - assigned size too small - expected " + Bytes.Length + " - assigned " + bytes.Length);
            CurrentBytes = length;
        }

        public override String ToString()
        {
            if (Bytes == null)
                return "";
            String val = "";
            try
            {
                val = SystemServices.BytesToString(ref Bytes, CurrentBytes);
            }
            catch (ConvException)
            {
                Converse.UtilityLog.LogMessage("ByteVar.ToString", "ConvException thrown *************", LogEntryType.Trace);
            }
            return val;
        }
    }

    /*

    public class BluetoothAddressVar : Variable
    {
        private byte[] addressBytes;
        private BluetoothAddress address;
        public BluetoothAddress Value
        {
            get
            {
                return address;
            }
            set
            {
                address = value;
                if (address == null)
                {
                    ValueSet = false;
                    addressBytes = null;
                }
                else
                {
                    // SMA only use the first 6 bytes of the bluetooth address
                    byte[] addr = address.ToByteArray();
                    addressBytes = new byte[6];
                    for (int i = 0; i < 6; i++)
                        addressBytes[i] = addr[i];

                    ValueSet = true;
                }
            }
        }

        public BluetoothAddressVar(String varName)
            : base(varName)
        {
            addressBytes = null;
            address = null;
        }


        public override byte[] GetBytes()
        {
            return addressBytes;
        }

        public override String ToString()
        {
            return "Invalid";
        }
    }
    */

    public class UseVariable : Element
    {
        public Variable Variable { get; set; }
        public String VariableType;

        public bool IsChecksum;
        public bool IsChecksum8;
        public bool IsChecksum16;
        public IConverseCheckSum16 Calculations;

        public UseVariable(Conversation conversation, String varName, String type = null, int? size = null, bool excludeFromChecksum = false, int messageId = 0)
            : base(conversation, excludeFromChecksum)
        {
            Converse session = conversation.Converse;
            int? variableSize;
            Variable = conversation.GetVariable(varName);
            VariableType = type;
            variableSize = size;
            Calculations = session.Calculations;

            IsChecksum16 = (varName.ToUpper() == "CHECKSUM16");
            IsChecksum8 = (varName.ToUpper() == "CHECKSUM8");
            IsChecksum = IsChecksum16 || IsChecksum8;

            if (Variable == null) // variable not defined
                if (type == null)
                {
                    Variable = new StringVar(varName, conversation);
                    session.Variables.Add(Variable);
                }
                else if (type == "BYTE")
                {
                    Variable = new ByteVar(varName, variableSize, conversation);
                    session.Variables.Add(Variable);
                }
                else if (type == "DYNAMICBYTE")
                {
                    if (variableSize == null || variableSize < 1)
                        variableSize = 10000;
                    Variable = new DynamicByteVar(varName, variableSize.Value, conversation);
                    session.Variables.Add(Variable);
                }
                else if (type == "STRING")
                {
                    Variable = new StringVar(varName, conversation);
                    session.Variables.Add(Variable);
                }
                else
                    throw new Exception("Variable " + varName + " type not supported: " + type);
        }

        public override byte[] GetBytes()
        {
            return Variable.GetBytes();
        }

        public byte[] GetChecksum16Bytes(List<byte[]> message)
        {
            UInt16 checkSum = Calculations.GetCheckSum16(message);

            byte[] varBytes = Converse.CheckSumEndianConverter16Bit.GetExternalBytes(checkSum);

            ((ByteVar)Variable).SetBytes(ref varBytes, 0, 2);

            return Variable.GetBytes();
        }

        public byte[] GetChecksum8Bytes(List<byte[]> message)
        {
            byte checkSum = (byte)Calculations.GetCheckSum16(message);

            byte[] varBytes = new byte[1];
            varBytes[0] = checkSum;

            ((ByteVar)Variable).SetBytes(ref varBytes, 0, 1);

            return Variable.GetBytes();
        }

        public override int Length
        {
            get
            {
                return Variable.Length;
            }
        }

        public override String ElementText
        {
            get
            {
                return Variable.ElementText;
            }
        }

        public override String ToString()
        {
            return Variable.ToString();
        }
    }

    public class Literal : Element
    {
        private byte[] bytes;

        public String Value { get; set; }

        public Literal(String value, Conversation conversation, bool excludeFromChecksum = false)
            : base(conversation, excludeFromChecksum)
        {
            Value = value;
            try
            {
                bytes = SystemServices.HexToBytes(Value);
            }
            catch (Exception)
            {
                bytes = SystemServices.StringToBytes(Value);
            }
        }

        public override int Length { get { return bytes.Length; } }

        public override byte[] GetBytes()
        {
            return bytes;
        }

        public override String ElementText
        {
            get
            {
                return Value;
            }
        }

        public override String ToString()
        {
            return Value;
        }
    }
}
