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
using System.Linq;
using System.Text;
using System.Xml;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;

namespace Algorithms
{
    #region Register

    public abstract class Register
    {
        public delegate String ExtractStringDelegate(DeviceAlgorithm device, RegisterSettings.RegisterValueType valueType, ref byte[] input, int size, int start, bool cStringNull);
        public delegate Decimal ExtractDecimalDelegate(DeviceAlgorithm device, RegisterSettings.RegisterValueType valueType, ref byte[] input, int size, int start, bool cStringNull);
        public delegate byte[] ExtractBytesDelegate(DeviceAlgorithm device, ref byte[] input, int size, int start, bool cStringNull);

        public delegate void InsertStringDelegate(DeviceAlgorithm device, String input, ref byte[] blockBuffer, int size, int start, bool cStringNull);
        public delegate void InsertDecimalDelegate(DeviceAlgorithm device, Decimal input, RegisterSettings.RegisterValueType valueType, ref byte[] blockBuffer, int size, int start, bool cStringNull);
        public delegate void InsertBytesDelegate(DeviceAlgorithm device, byte[] input, int inputIndex, ref byte[] blockBuffer, int size, int start, bool cStringNull);

        public struct FroniusModel
        {
            public byte Id;
            public String Name;

            public FroniusModel(string id, string name)
            {
                Id = SystemServices.HexToBytes(id)[0];
                Name = name;
            }
        }

        public static List<FroniusModel> FroniusModels;

        protected struct StringAccessDelegates
        {
            public String ExtractorName;
            public ExtractStringDelegate Extractor;
            public String InserterName;
            public InsertStringDelegate Inserter;
        }
        protected static StringAccessDelegates[] StringAccessors;

        protected struct DecimalAccessDelegates
        {
            public String ExtractorName;
            public ExtractDecimalDelegate Extractor;
            public String InserterName;
            public InsertDecimalDelegate Inserter;
        }
        protected static DecimalAccessDelegates[] DecimalAccessors;

        protected struct BytesAccessDelegates
        {
            public String ExtractorName;
            public ExtractBytesDelegate Extractor;
            public String InserterName;
            public InsertBytesDelegate Inserter;
        }
        protected static BytesAccessDelegates[] BytesAccessors;

        public DeviceAlgorithm Device { get; private set; }

        public String Name;
        public String Content;
        public RegisterSettings Settings;

        protected UInt16 StartRegister;
        public UInt16? PayloadPosition;
        //public bool IsContent { get; private set; }
        public bool MappedToRegisterData { get; protected set; }
        public BlockSettings.Message Message { get; private set; }
        public String BindingName { get; private set; }
        public Variable Binding { get; private set; }
        protected UInt16 RegisterCount;
        protected UInt16 Size;
        protected DeviceBlock DeviceBlock;
        public bool IsCString { get; private set; }
        public bool IsAlarmFlag;
        public bool IsAlarmDetail;
        public bool IsErrorFlag;
        public bool IsErrorDetail;
        public bool IsHexadecimal;
        public bool HasFixedValue { get; protected set; }

        public bool BoundToSend;
        public bool BoundToRead;
        public bool BoundToFind;
        public bool BoundToExtract;

        protected List<RegisterListValue> ValueList;

        public abstract decimal ValueDecimal { get; set; }
        public abstract String ValueString { get; set; }
        public abstract byte[] ValueBytes { get; set; }

        // This is the data type received in the message from the device
        // It does not necessarily match the sub-type of DeviceMapItem
        // The sub-type of DeviceMapItem matches the data expectation of the Device handler class
        public RegisterSettings.RegisterValueType ValueType { get; private set; }

        public UInt16 RegisterIndex
        {
            get
            {
                if (DeviceBlock.ProtocolType == ProtocolSettings.ProtocolType.Modbus)
                    return (UInt16)((StartRegister - ((DeviceBlock_Modbus)DeviceBlock).FirstRegister) * 2);  // fixed position for modbus
                else
                    return PayloadPosition.Value;
            }
        }

        protected UInt16? FixedSize;

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage("ModbusMapItem", message, logEntryType);
        }

        static void BuildFroniusModels()
        {
            FroniusModels = new List<FroniusModel>();
            FroniusModels.Add(new FroniusModel("0xFE", "FRONIUS IG 15"));
            FroniusModels.Add(new FroniusModel("0xFD", "FRONIUS IG 20"));
            FroniusModels.Add(new FroniusModel("0xfc", "FRONIUS IG 30"));
            FroniusModels.Add(new FroniusModel("0xfb", "FRONIUS IG 30"));
            FroniusModels.Add(new FroniusModel("0xfa", "FRONIUS IG 40"));
            FroniusModels.Add(new FroniusModel("0xf9", "FRONIUS IG 60 / IG 60 HV"));
            FroniusModels.Add(new FroniusModel("0xf6", "FRONIUS IG 300"));
            FroniusModels.Add(new FroniusModel("0xf5", "FRONIUS IG 400"));
            FroniusModels.Add(new FroniusModel("0xf4", "FRONIUS IG 500"));
            FroniusModels.Add(new FroniusModel("0xf3", "FRONIUS IG 60 / IG 60 HV"));
            FroniusModels.Add(new FroniusModel("0xee", "FRONIUS IG 2000"));
            FroniusModels.Add(new FroniusModel("0xed", "FRONIUS IG 3000"));
            FroniusModels.Add(new FroniusModel("0xeb", "FRONIUS IG 4000"));
            FroniusModels.Add(new FroniusModel("0xea", "FRONIUS IG 5100"));
            FroniusModels.Add(new FroniusModel("0xe5", "FRONIUS IG 2500-LV"));
            FroniusModels.Add(new FroniusModel("0xe3", "FRONIUS IG 4500-LV"));
            FroniusModels.Add(new FroniusModel("0xDF", "Fronius IG Plus 11.4-3 Delta"));
            FroniusModels.Add(new FroniusModel("0xDE", "Fronius IG Plus 11.4-1 UNI"));
            FroniusModels.Add(new FroniusModel("0xDD", "Fronius IG Plus 10.0-1 UNI"));
            FroniusModels.Add(new FroniusModel("0xDC", "Fronius IG Plus 7.5-1 UNI"));
            FroniusModels.Add(new FroniusModel("0xDB", "Fronius IG Plus 6.0-1 UNI"));
            FroniusModels.Add(new FroniusModel("0xDA", "Fronius IG Plus 5.0-1 UNI"));
            FroniusModels.Add(new FroniusModel("0xD9", "Fronius IG Plus 3.8-1 UNI"));
            FroniusModels.Add(new FroniusModel("0xD8", "Fronius IG Plus 3.0-1 UNI"));
            FroniusModels.Add(new FroniusModel("0xD7", "Fronius IG Plus 120-3"));
            FroniusModels.Add(new FroniusModel("0xD6", "Fronius IG Plus 70-2"));
            FroniusModels.Add(new FroniusModel("0xD5", "Fronius IG Plus 70-1"));
            FroniusModels.Add(new FroniusModel("0xD4", "Fronius IG Plus 35-1"));
            FroniusModels.Add(new FroniusModel("0xD3", "Fronius IG Plus 150-3"));
            FroniusModels.Add(new FroniusModel("0xD2", "Fronius IG Plus 100-2"));
            FroniusModels.Add(new FroniusModel("0xD1", "Fronius IG Plus 100-1"));
            FroniusModels.Add(new FroniusModel("0xD0", "Fronius IG Plus 50-1"));
            FroniusModels.Add(new FroniusModel("0xCF", "Fronius IG Plus 12.0-3 WYE277"));
        }

        static Register()
        {
            StringAccessors = new StringAccessDelegates[3];
            StringAccessors[0].ExtractorName = "Standard";
            StringAccessors[0].Extractor = RegisterString.BytesToString;
            StringAccessors[0].InserterName = "Standard";
            StringAccessors[0].Inserter = RegisterString.StringToBytes;
            StringAccessors[1].ExtractorName = "GrowattModel";
            StringAccessors[1].Extractor = RegisterString.GrowattModelFromBytes;
            StringAccessors[1].InserterName = "";
            StringAccessors[1].Inserter = null;   // not supported

            StringAccessors[2].ExtractorName = "FroniusModel";
            StringAccessors[2].Extractor = RegisterString.FroniusModelFromBytes;
            StringAccessors[2].InserterName = "";
            StringAccessors[2].Inserter = null;   // not supported

            DecimalAccessors = new DecimalAccessDelegates[1];
            DecimalAccessors[0].ExtractorName = "Standard";
            DecimalAccessors[0].Extractor = RegisterNumber.BytesToDecimal;
            DecimalAccessors[0].InserterName = "Standard";
            DecimalAccessors[0].Inserter = RegisterNumber.DecimalToBytes;

            BytesAccessors = new BytesAccessDelegates[1];
            BytesAccessors[0].ExtractorName = "Standard";
            BytesAccessors[0].Extractor = RegisterBytes.RegisterBytesToBytes;
            BytesAccessors[0].InserterName = "Standard";
            BytesAccessors[0].Inserter = RegisterBytes.BytesToRegisterBytes;

            BuildFroniusModels();
        }

        protected Register(DeviceBlock deviceBlock, RegisterSettings settings)
        {
            //SendBufferStartPos = 0;
            //ReceiveBufferStartPos = 0;
            PayloadPosition = settings.Position;

            Name = settings.Name;
            Content = settings.Content;

            Settings = settings;
            DeviceBlock = deviceBlock;
            Device = deviceBlock.Device;
            ValueType = settings.Type;
            Message = settings.Message;
            BindingName = settings.Binding;
            Binding = DeviceBlock.Conversation.GetVariable(BindingName);
            if (Binding == null)
            {
                BoundToSend = false;
                BoundToRead = false;
                BoundToFind = false;
                BoundToExtract = false;
            }
            else
            {
                bool res = DeviceBlock.Conversation.GetVariableUsage(BindingName, out BoundToSend, out BoundToRead, out BoundToFind, out BoundToExtract);
                if (!res)
                    LogMessage("Register Constructor - Variable: " + BindingName + " - not used in conversation: " + DeviceBlock.Conversation, LogEntryType.ErrorMessage);
            }
            MappedToRegisterData = (DeviceBlock.GetType() == typeof(DeviceBlock_Modbus) && settings.Id.HasValue)
                || (DeviceBlock.GetType() != typeof(DeviceBlock_Modbus) && BindingName == "");
            //IsContent = Content != "";
            StartRegister = settings.Id.HasValue ? settings.Id.Value : (UInt16)0;
            RegisterCount = settings.RegisterCount;
            UInt16? size = settings.Size;
            Size = size.HasValue ? size.Value : (UInt16)(RegisterCount * 2);
            FixedSize = settings.Size;
            IsAlarmFlag = settings.IsAlarmFlag;
            IsAlarmDetail = settings.IsAlarmDetail;
            IsErrorFlag = settings.IsErrorFlag;
            IsErrorDetail = settings.IsErrorDetail;
            IsHexadecimal = settings.IsHexadecimal;
            IsCString = settings.IsCString;

            LoadValueList();
        }

        protected abstract RegisterValue BuildRegisterValue(String value);

        private void LoadValueList()
        {
            ValueList = new List<RegisterListValue>();
            List<PVSettings.RegisterListValueSetting> settingsValueList = Settings.ValueList;
            if (settingsValueList == null)
                return;
            foreach (PVSettings.RegisterListValueSetting settingValue in settingsValueList)
            {
                RegisterListValue value = new RegisterListValue();
                value.Name = settingValue.Name;
                value.Tag = settingValue.Tag;
                value.Value = BuildRegisterValue(settingValue.Value);
                ValueList.Add(value);
            }
        }

        public RegisterListValue LocateInValueList()
        {
            if (ValueList == null)
                return null;

            foreach (RegisterListValue val in ValueList)
            {
                RegisterValue.RegisterCompareResult res = RegisterValue.Compare(val.Value);
                if (res.EqualityResult == RegisterValue.EqualityResult.Equal)
                    return val;
            }
            return null;
        }

        public byte[] GetRegisterDataBytes()
        {
            int length = RegisterCount * 2;
            int pos = RegisterIndex;
            byte[] val = new byte[length];
            for (int i = 0; i < length; i++)
                val[i] = DeviceBlock.RegisterData[pos++];
            return val;
        }

        // Extract value from block buffer (or literal if not mapped to block
        // and push this value to any associated device valiable
        public abstract void GetItemValue(ref byte[] buffer);

        // Copy current value into block buffer
        public abstract void StoreItemValue(ref byte[] buffer);

        public abstract void ClearSetValueDelegate();

        public abstract UInt16 CurrentSize { get; }

        // Get / Set current value formatted as a RegisterValue
        public abstract RegisterValue RegisterValue { get; set; }

        public String ErrorStatus
        {
            get
            {
                if (!IsErrorFlag)
                    return "";
                RegisterListValue testValue = LocateInValueList();
                if (testValue != null && testValue.Tag == "OK")
                    return "OK";
                else
                    return "Error";
            }
        }

        public String AlarmStatus
        {
            get
            {
                if (!IsAlarmFlag)
                    return "";
                RegisterListValue testValue = LocateInValueList();
                if (testValue != null && testValue.Tag == "OK")
                    return "OK";
                else
                    return "Alarm";
            }
        }

        private static Decimal ApplyExponent(Int32 value, byte exp)
        {
            Double factor;
            int exponent;
            if (exp < 11)
                exponent = exp;
            else if (exp == 255)
                exponent = -1;
            else if (exp == 254)
                exponent = -2;
            else if (exp == 253)
                exponent = -3;
            else // 0x0B is overflow or invalid;  0xFC = underflow; other values over 10 are undefined
                return 0;

            factor = Math.Pow(10, exponent);
            Decimal val;
            try
            {
                val = (decimal)(value * factor);
            }
            catch (OverflowException)
            {
                val = 0;
            }

            return val;
        }

        public static decimal BytesToDecimal(DeviceAlgorithm device, RegisterSettings.RegisterValueType valueType, ref byte[] registerData, int size, int registerIndex, bool cStringNull)
        {
            Decimal val = 0;

            // ordered to minimise tests
            if (valueType == RegisterSettings.RegisterValueType.rv_uint16)
                val = device.Params.EndianConverter16Bit.GetUInt16FromBytes(ref registerData, registerIndex);
            else if (valueType == RegisterSettings.RegisterValueType.rv_uint16_exp)
            {
                UInt16 raw = device.Params.EndianConverter16Bit.GetUInt16FromBytes(ref registerData, registerIndex);
                byte exp = registerData[registerIndex + 2];
                val = ApplyExponent(raw, exp);
            }
            else if (valueType == RegisterSettings.RegisterValueType.rv_uint32)
                val = device.Params.EndianConverter32Bit.GetUInt32FromBytes(ref registerData, registerIndex);
            else if (valueType == RegisterSettings.RegisterValueType.rv_byte)
                val = registerData[registerIndex];
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint16)
                val = device.Params.EndianConverter16Bit.GetInt16FromBytes(ref registerData, registerIndex);
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint16_exp)
            {
                Int16 raw = device.Params.EndianConverter16Bit.GetInt16FromBytes(ref registerData, registerIndex);
                byte exp = registerData[registerIndex + 2];
                val = ApplyExponent(raw, exp);
            }
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint32)
                val = device.Params.EndianConverter32Bit.GetInt32FromBytes(ref registerData, registerIndex);
            else if (valueType == RegisterSettings.RegisterValueType.rv_bcd)
                val = EndianConverter32Bit.GetDecimalFromBCD(ref registerData, size, registerIndex);
            else if (valueType == RegisterSettings.RegisterValueType.rv_string)
                val = Decimal.Parse(RegisterString.BytesToString(device, valueType, ref registerData, size, registerIndex, cStringNull));

            return val;
        }

        public static UInt16 SizeInBytes(RegisterSettings.RegisterValueType valueType, int size, bool cStringNull)
        {
            if (valueType == RegisterSettings.RegisterValueType.rv_uint16)
                return 2;
            else if (valueType == RegisterSettings.RegisterValueType.rv_uint16_exp)
                return 3;
            else if (valueType == RegisterSettings.RegisterValueType.rv_uint32)
                return 4;
            else if (valueType == RegisterSettings.RegisterValueType.rv_byte)
                return 1;
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint16)
                return 2;
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint16_exp)
                return 3;
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint32)
                return 4;
            else if (valueType == RegisterSettings.RegisterValueType.rv_bcd)
            {
                return (UInt16)((size + 1) / 2);
            }
            else if (valueType == RegisterSettings.RegisterValueType.rv_string)
                return (UInt16)size;
            else
                return 0;
        }

        public static void DecimalToBytes(DeviceAlgorithm device, decimal value, RegisterSettings.RegisterValueType valueType, ref byte[] registerData, int size, int registerIndex, bool cStringNull)
        {
            byte[] bytes = null;

            // rv_uint16_exp and rv_sint16_exp not supported for data writes
            // ordered to minimise tests
            if (valueType == RegisterSettings.RegisterValueType.rv_uint16)
                bytes = device.Params.EndianConverter16Bit.GetExternalBytes((UInt16)value);
            else if (valueType == RegisterSettings.RegisterValueType.rv_uint32)
                bytes = device.Params.EndianConverter32Bit.GetExternalBytes((UInt32)value);
            else if (valueType == RegisterSettings.RegisterValueType.rv_byte)
            {
                bytes = new byte[1];
                bytes[0] = (byte)value;
            }
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint16)
                bytes = device.Params.EndianConverter16Bit.GetExternalBytes((Int16)value);
            else if (valueType == RegisterSettings.RegisterValueType.rv_sint32)
                bytes = device.Params.EndianConverter32Bit.GetExternalBytes((Int32)value);
            else if (valueType == RegisterSettings.RegisterValueType.rv_bcd)
                bytes = EndianConverter32Bit.GetBCDFromDecimal(value, size, 0, false);
            else if (valueType == RegisterSettings.RegisterValueType.rv_string)
                bytes = StringToBytes(value.ToString(), size, cStringNull ? (byte)0 : (byte)' ');

            int j = registerIndex;
            for (int i = 0; i < bytes.Length; )
                registerData[j++] = bytes[i++];
        }

        public static void StringToBytes(DeviceAlgorithm device, String value, ref byte[] registerData, int size, int registerIndex, bool cStringNull)
        {
            byte[] bytes = StringToBytes(value, size, cStringNull ? (byte)0 : (byte)' ');

            int j = registerIndex;
            for (int i = 0; i < bytes.Length; )
                registerData[j++] = bytes[i++];
        }

        public static byte[] StringToBytes(String value, int length, byte pad)
        {
            int size = value.Length;
            if (size > length)
                throw new ConvException("StringToBytes size error - Requested: "
                    + length + " - Required: " + size + " - Value: " + value);

            byte[] bytes = new byte[length];
            int pos = 0;
            while (pos < length)
            {
                if (pos < size)
                    bytes[pos] = (byte)value[pos];
                else
                    bytes[pos] = pad;
                pos++;
            }

            return bytes;
        }

        public static void BytesToRegisterBytes(DeviceAlgorithm device, byte[] value, int valueIndex, ref byte[] registerData, int size, int registerIndex, bool cStringNull)
        {
            int j = registerIndex;
            int i;
            for (i = 0; i < value.Length && i < size; i++)
                registerData[j++] = value[valueIndex++];

            byte pad = cStringNull ? (byte)0 : (byte)' ';
            while (i < size)
                registerData[j++] = pad;
        }

        public static byte[] RegisterBytesToBytes(DeviceAlgorithm device, ref byte[] registerData, int size, int registerIndex, bool cStringNull)
        {
            byte[] bytes = new byte[size];

            int j = registerIndex;
            int i;
            for (i = 0; i < size; i++)
                bytes[i] = registerData[j++];

            return bytes;
        }

        public static String BytesToString(DeviceAlgorithm device, RegisterSettings.RegisterValueType valueType, ref byte[] input, int size, int start, bool cStringNull)
        {
            if (valueType == RegisterSettings.RegisterValueType.rv_string)
            {
                int outSize = size;
                if (start + outSize > input.Length)
                    outSize = input.Length - start;
                if (outSize < 1)
                    // return "";
                    // Need to know if this is occuring
                    throw new ConvException("BytesToString size error - Size: " + size + " - Start: " + start);

                char[] output = new char[outSize];
                int inPos = start;
                int strSize = outSize;

                for (int i = 0; i < outSize; i++)
                {
                    byte b = input[inPos];
                    if (cStringNull && b == 0)
                    {
                        strSize = inPos - start;
                        break;
                    }
                    output[i] = (char)b;
                    inPos++;
                }

                StringBuilder sb = new StringBuilder(output.Length);
                sb.Append(output, 0, strSize);

                return sb.ToString();
            }

            if (valueType == RegisterSettings.RegisterValueType.rv_bytes)
                return "";

            return RegisterNumber.BytesToDecimal(device, valueType, ref input, size, start, cStringNull).ToString();
        }

        public static string GrowattModelFromBytes(DeviceAlgorithm device, RegisterSettings.RegisterValueType valueType, ref byte[] input, int size, int start, bool cStringNull)
        {
            if (size < 2)
                return "Unknown";

            String hex = SystemServices.BytesToHex(ref input, 1, "", "", start, size);

            string result = String.Format("P{0} U{1} M{2} S{3}", hex[0], hex[1], hex[2], hex[3]);

            return result;
        }

        public static string FroniusModelFromBytes(DeviceAlgorithm device, RegisterSettings.RegisterValueType valueType, ref byte[] input, int size, int start, bool cStringNull)
        {
            if (size != 1)
                return "Unknown";

            byte id = input[start];

            foreach (FroniusModel model in FroniusModels)
                if (id == model.Id)
                    return model.Name;

            return "Fronius - Unknown";
        }
    }

    // Used when the device class expects to receive Decimal / Numeric data for a data item
    public class RegisterNumber : Register
    {
        //public Decimal Scale { get; private set; }

        private decimal _Value = 0;
        private ExtractDecimalDelegate Extractor = null;
        public InsertDecimalDelegate Inserter { get; private set; }

        private SetNumberValueDelegate SetNumberValueInternal = null;
        private GetNumberValueDelegate GetNumberValueInternal = null;

        private RegisterValueNumber RegisterValueNumber = null;

        public RegisterNumber(DeviceBlock deviceBlock, RegisterSettings settings,
            SetNumberValueDelegate setValue, GetNumberValueDelegate getValue = null)
            : base(deviceBlock, settings)
        {
            UseScale = false;
            _ScaleFactor = 1;
            Inserter = null;
            String defaultValue = Settings.RegisterValue;
            if (defaultValue != null)
                RegisterValueNumber = new RegisterValueNumber(this, defaultValue);
            if (RegisterValueNumber != null)
            {
                HasFixedValue = true;
                MappedToRegisterData &= (Device.Params.Protocol.Type == ProtocolSettings.ProtocolType.Modbus);
                _Value = RegisterValueNumber.ValueDecimal;
            }
            else
                HasFixedValue = false;
            SetNumberValueInternal = setValue;
            GetNumberValueInternal = getValue;
            UseScale = settings.UseScale;
            ScaleValueType = settings.ScaleValueType;
            if (UseScale)
                _ScaleFactor = 1 / settings.DefaultScale;

            LoadExtractor(settings.Extractor);
            LoadInserter(settings.Inserter);
        }

        public bool UseScale { get; private set; }

        private RegisterSettingValueType ScaleValueType;
        //private decimal defaultScaleFactor;

        private decimal _ScaleFactor;
        public decimal ScaleFactor
        {
            get
            {
                if (ScaleValueType == RegisterSettingValueType.FixedValue)
                    return _ScaleFactor;
                // TBI - implement scale value lookup here
                return _ScaleFactor;    // the default value
            }

        }

        public override UInt16 CurrentSize
        {
            get
            {
                if (FixedSize.HasValue)
                    return FixedSize.Value;
                else
                    return 0;
            }
        }

        protected override RegisterValue BuildRegisterValue(String value)
        {
            return new RegisterValueNumber(this, value);
        }

        public void SetSetValueDelegate(SetNumberValueDelegate setValue)
        {
            SetNumberValueInternal = setValue;
        }

        public override void ClearSetValueDelegate()
        {
            SetNumberValueInternal = null;
        }

        private void LoadExtractor(string extractorName)
        {
            if (extractorName == "")
            {
                Extractor = DecimalAccessors[0].Extractor;
                return;
            }

            foreach (DecimalAccessDelegates del in DecimalAccessors)
                if (del.ExtractorName == extractorName)
                {
                    Extractor = del.Extractor;
                    return;
                }
        }

        private void LoadInserter(string inserterName)
        {
            if (inserterName == "")
            {
                Inserter = DecimalAccessors[0].Inserter;
                return;
            }

            foreach (DecimalAccessDelegates del in DecimalAccessors)
                if (del.InserterName == inserterName)
                {
                    Inserter = del.Inserter;
                    return;
                }
        }

        public override RegisterValue RegisterValue
        {
            get
            {
                RegisterValueNumber val;
                val = new RegisterValueNumber(this);
                val.SetValue(_Value);
                return val;
            }

            set
            {
                _Value = value.ValueDecimal;
            }
        }

        public override decimal ValueDecimal { get { return _Value; } set { _Value = value; } }

        public override String ValueString { get { return _Value.ToString(); } set { _Value = Decimal.Parse(value); } }

        public override byte[] ValueBytes
        {
            get
            {
                return NumberToBytes(_Value);
            }

            set
            {
                _Value = Extractor(Device, ValueType, ref value, value.Length, 0, IsCString);
            }
        }

        public byte[] NumberToBytes(Decimal value)
        {
            UInt16 size = CurrentSize;
            if (size == 0)
            {
                LogMessage("DecimalToBytes - Cannot convert to Bytes when numeric format not specified", LogEntryType.ErrorMessage);
                throw new NotImplementedException("DecimalToBytes - Cannot convert to Bytes when numeric format not specified");
            }
            Decimal val;

            if (UseScale)
                val = value / ScaleFactor;
            else
                val = value;

            byte[] bytes = new byte[SizeInBytes(ValueType, size, IsCString)];
            Inserter(Device, val, ValueType, ref bytes, size, 0, IsCString);
            return bytes;
        }

        private void ExtractValue(ref byte[] buffer)
        {
            if (MappedToRegisterData)
            {
                Decimal val = Extractor(Device, ValueType, ref buffer, CurrentSize, RegisterIndex, IsCString);

                if (UseScale) val *= ScaleFactor;
                _Value = val;
            }
        }

        public override void StoreItemValue(ref byte[] buffer)
        {
            if (GetNumberValueInternal != null)
                _Value = GetNumberValueInternal();
            if (MappedToRegisterData)
            {
                Decimal val;

                if (UseScale)
                    val = _Value / ScaleFactor;
                else
                    val = _Value;
                Inserter(Device, val, ValueType, ref buffer, CurrentSize, RegisterIndex, IsCString);
            }
        }

        public override void GetItemValue(ref byte[] buffer)
        {
            ExtractValue(ref buffer);
            if (SetNumberValueInternal != null)
                SetNumberValueInternal(_Value);
        }
    }

    // Used when the device class expects to receive String data for a data item
    public class RegisterString : Register
    {
        private String _Value = "";
        private RegisterValueString RegisterValueString = null;
        private ExtractStringDelegate Extractor = null;
        private InsertStringDelegate Inserter = null;

        private SetStringValueDelegate SetStringValueInternal = null;
        private GetStringValueDelegate GetStringValueInternal = null;

        public RegisterString(DeviceBlock deviceBlock, RegisterSettings settings,
            SetStringValueDelegate setValue, GetStringValueDelegate getValue = null)
            : base(deviceBlock, settings)
        {
            String defaultValue = Settings.RegisterValue;
            if (defaultValue != null)
                RegisterValueString = new RegisterValueString(this, defaultValue);
            if (RegisterValueString != null)
            {
                HasFixedValue = true;
                MappedToRegisterData &= (Device.Params.Protocol.Type == ProtocolSettings.ProtocolType.Modbus);
                _Value = RegisterValueString.ValueString;
            }
            else
                HasFixedValue = false;
            SetStringValueInternal = setValue;
            GetStringValueInternal = getValue;
            LoadExtractor(settings.Extractor);
            LoadInserter(settings.Inserter);
        }

        public void SetSetValueDelegate(SetStringValueDelegate setValue)
        {
            SetStringValueInternal = setValue;
        }

        protected override RegisterValue BuildRegisterValue(String value)
        {
            return new RegisterValueString(this, value);
        }

        public override UInt16 CurrentSize
        {
            get
            {
                if (FixedSize.HasValue)
                    return FixedSize.Value;
                else
                    return (UInt16)_Value.Length;
            }
        }

        public override void ClearSetValueDelegate()
        {
            SetStringValueInternal = null;
        }

        private void LoadExtractor(string extractorName)
        {
            if (extractorName == "")
            {
                Extractor = StringAccessors[0].Extractor;
                return;
            }

            foreach (StringAccessDelegates del in StringAccessors)
                if (del.ExtractorName == extractorName)
                {
                    Extractor = del.Extractor;
                    return;
                }
        }

        public override RegisterValue RegisterValue
        {
            get
            {
                RegisterValueString val;
                val = new RegisterValueString(this);
                val.SetValue(_Value);
                return val;
            }

            set
            {
                _Value = value.ToString();
            }
        }

        public override decimal ValueDecimal { get { return Decimal.Parse(_Value); } set { _Value = value.ToString(); } }

        public override String ValueString { get { return _Value; } set { _Value = value; } }

        public override byte[] ValueBytes
        {
            get
            {
                UInt16 size = CurrentSize;
                byte[] bytes = new byte[SizeInBytes(ValueType, size, IsCString)];
                Inserter(Device, _Value, ref bytes, size, 0, IsCString);
                return bytes;
            }

            set
            {
                _Value = Extractor(Device, ValueType, ref value, value.Length, 0, IsCString);
            }
        }

        private void ExtractValue(ref byte[] buffer)
        {
            if (MappedToRegisterData)
                _Value = Extractor(Device, ValueType, ref buffer, Size, RegisterIndex, IsCString);
        }

        private void LoadInserter(string inserterName)
        {
            if (inserterName == "")
            {
                Inserter = StringAccessors[0].Inserter;
                return;
            }

            foreach (StringAccessDelegates del in StringAccessors)
                if (del.InserterName == inserterName)
                {
                    Inserter = del.Inserter;
                    return;
                }
        }

        public override void StoreItemValue(ref byte[] buffer)
        {
            if (GetStringValueInternal != null)
                _Value = GetStringValueInternal();
            if (MappedToRegisterData)
                Inserter(Device, _Value, ref buffer, CurrentSize, RegisterIndex, IsCString);
        }

        public override void GetItemValue(ref byte[] buffer)
        {
            ExtractValue(ref buffer);
            if (SetStringValueInternal != null)
                SetStringValueInternal(_Value);
        }
    }

    // Used when the device class expects to receive byte array data for a data item
    public class RegisterBytes : Register
    {
        private RegisterValueBytes RegisterValueBytes = null;
        private ExtractBytesDelegate Extractor = null;
        private InsertBytesDelegate Inserter = null;

        private SetBytesValueDelegate SetBytesValueInternal = null;
        private GetBytesValueDelegate GetBytesValueInternal = null;

        private byte[] _Value;

        public RegisterBytes(DeviceBlock deviceBlock, RegisterSettings settings,
            SetBytesValueDelegate setValue, GetBytesValueDelegate getValue = null)
            : base(deviceBlock, settings)
        {
            String defaultValue = Settings.RegisterValue;
            if (defaultValue != null)
                RegisterValueBytes = new RegisterValueBytes(this, defaultValue);
            if (RegisterValueBytes != null)
            {
                HasFixedValue = true;
                MappedToRegisterData &= (Device.Params.Protocol.Type == ProtocolSettings.ProtocolType.Modbus);
                _Value = RegisterValueBytes.ValueBytes;
            }
            else
            {
                HasFixedValue = false;
                _Value = null;
            }
            SetBytesValueInternal = setValue;
            GetBytesValueInternal = getValue;
            LoadExtractor(settings.Extractor);
            LoadInserter(settings.Inserter);
        }

        public override UInt16 CurrentSize
        {
            get
            {
                if (FixedSize.HasValue)
                    return FixedSize.Value;
                else if (_Value == null)
                    return 0;
                else
                    return (UInt16)_Value.Length;
            }
        }

        protected override RegisterValue BuildRegisterValue(String value)
        {
            return new RegisterValueBytes(this, value);
        }

        public void SetSetValueDelegate(SetBytesValueDelegate setValue)
        {
            SetBytesValueInternal = setValue;
        }

        public override void ClearSetValueDelegate()
        {
            SetBytesValueInternal = null;
        }

        private void LoadExtractor(string extractorName)
        {
            if (extractorName == "")
            {
                Extractor = BytesAccessors[0].Extractor;
                return;
            }

            foreach (BytesAccessDelegates del in BytesAccessors)
                if (del.ExtractorName == extractorName)
                {
                    Extractor = del.Extractor;
                    return;
                }
        }

        public override RegisterValue RegisterValue
        {
            get
            {
                RegisterValueBytes val;
                val = new RegisterValueBytes(this);
                val.SetValue(_Value);
                return val;
            }

            set
            {
                _Value = value.ValueBytes;
            }
        }

        public override decimal ValueDecimal { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public override String ValueString
        {
            get
            {
                return RegisterString.BytesToString(Device, ValueType, ref _Value, _Value.Length, 0, IsCString);
            }
            set
            {
                int size = value.Length;
                if (FixedSize.HasValue)
                {
                    if (size != FixedSize)
                    {
                        LogMessage("DeviceMapItem_Bytes.ValueString - Fixed Size - Expected: " + FixedSize.Value + " - Found: " + value.Length, LogEntryType.ErrorMessage);
                        return;
                    }
                }

                if (_Value == null || size != _Value.Length)
                    _Value = new byte[size];
                RegisterString.StringToBytes(Device, value, ref _Value, size, 0, IsCString);
            }
        }

        public override byte[] ValueBytes
        {
            get
            {
                return _Value;
            }
            set
            {
                _Value = value;
            }
        }

        private void ExtractValue(ref byte[] buffer)
        {
            if (MappedToRegisterData)
                if (Content == "%DataMap")
                {
                    int size = buffer.Length - RegisterIndex;
                    if (size > 0)
                        _Value = Extractor(Device, ref buffer, size, RegisterIndex, false);
                }
                else
                    _Value = Extractor(Device, ref buffer, CurrentSize, RegisterIndex, IsCString);
        }

        private void LoadInserter(string inserterName)
        {
            if (inserterName == "")
            {
                Inserter = BytesAccessors[0].Inserter;
                return;
            }

            foreach (BytesAccessDelegates del in BytesAccessors)
                if (del.InserterName == inserterName)
                {
                    Inserter = del.Inserter;
                    return;
                }
        }

        public override void StoreItemValue(ref byte[] buffer)
        {
            if (GetBytesValueInternal != null)
                _Value = GetBytesValueInternal();
            if (MappedToRegisterData)
                Inserter(Device, _Value, 0, ref buffer, CurrentSize, RegisterIndex, IsCString);
        }

        public override void GetItemValue(ref byte[] buffer)
        {
            ExtractValue(ref buffer);
            if (SetBytesValueInternal != null)
                SetBytesValueInternal(_Value);
        }
    }

    #endregion
}

