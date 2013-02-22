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
using System.Linq;
using System.Text;
using MackayFisher.Utilities;

namespace PVSettings
{
    public enum RegisterValueType
    {
        rv_bytes = 0,
        rv_string,
        rv_byte,
        rv_uint16,
        rv_uint32,
        rv_sint16,
        rv_sint32,
        rv_bcd
    }

    public abstract class RegisterValue
    {
        public enum EqualityResult
        {
            Incompatible = -1,
            NotEqual,
            Equal
        }

        public enum RangeResult
        {
            NotAvailable = -1,
            Less,
            Equal,
            Greater
        }

        public struct RegisterCompareResult
        {
            public RangeResult RangeResult;
            public EqualityResult EqualityResult;

            public RegisterCompareResult(int dummy = 0)
            {
                RangeResult = RangeResult.NotAvailable;
                EqualityResult = EqualityResult.Incompatible;
            }
        }

        public static String BytesToHex(byte[] bytes)
        {
            String str = "0x";
            foreach (byte b in bytes)
                str += b.ToString("X2");
            return str;
        }

        public static RegisterValue BuildRegisterValue(RegisterSettings settings)
        {
            RegisterValueType type = settings.Type;
            if (type == RegisterValueType.rv_bytes)
                return new RegisterValueBytes(settings);
            if (type == RegisterValueType.rv_string)
                return new RegisterValueString(settings);
            else
                return new RegisterValueDecimal(settings);
        }

        protected RegisterSettings RegisterSettings = null;

        public virtual RegisterValueType RegisterType { get { return RegisterSettings.Type; } }

        public RegisterValue(RegisterSettings settings)
        {
            RegisterSettings = settings;
        }

        public abstract void SetValue(String value);
        public abstract void SetValue(byte[] value);
        public abstract void SetValue(decimal value);

        public abstract override string ToString();
        public abstract decimal ToDecimal();
        public abstract byte[] ToBytes();

        public static RegisterCompareResult Compare(RegisterValue RegisterA, RegisterValue RegisterB)
        {
            if (RegisterA.RegisterType == RegisterValueType.rv_bytes)
            {
                if (RegisterB.RegisterType == RegisterValueType.rv_bytes)
                    return RegisterValueBytes.CompareBytes((RegisterValueBytes)RegisterA, (RegisterValueBytes)RegisterB);
                else
                    return new RegisterCompareResult();
            }
            else if (RegisterA.RegisterType == RegisterValueType.rv_string)
            {
                if (RegisterB.RegisterType == RegisterValueType.rv_string)
                    return RegisterValueString.CompareString((RegisterValueString)RegisterA, (RegisterValueString)RegisterB);
                else
                    return new RegisterCompareResult();
            }
            else if (RegisterB.RegisterType >= RegisterValueType.rv_byte)
            {
                return RegisterValueDecimal.CompareDecimal((RegisterValueDecimal)RegisterA, (RegisterValueDecimal)RegisterB);
            }
            else
                return new RegisterCompareResult();
        }
    }

    public class RegisterValueString : RegisterValue
    {
        private String _Value = "";

        public override RegisterValueType RegisterType { get { return RegisterValueType.rv_string; } }

        public RegisterValueString(RegisterSettings settings)
            : base(settings)
        {
        }

        public override void SetValue(String value)
        {
            _Value = value;
        }

        public override void SetValue(decimal value)
        {
            _Value = value.ToString();
        }

        public override void SetValue(byte[] value)
        {
            throw new NotImplementedException();
        }

        public override String ToString() { return _Value; }
        public override decimal ToDecimal()
        {
            try
            {
                return System.Decimal.Parse(_Value);
            }
            catch
            {
                return 0;
            }
        }

        public override byte[] ToBytes()
        {
            if (RegisterSettings.IsHexadecimal)
                return SystemServices.HexToBytes(_Value);
            else
                return SystemServices.StringToBytes(_Value);
        }

        public String Value { get { return _Value; } }

        public static RegisterCompareResult CompareString(RegisterValueString RegisterA, RegisterValueString RegisterB)
        {
            RegisterCompareResult res;
            if (RegisterA.Value == RegisterB.Value)
            {
                res.EqualityResult = EqualityResult.Equal;
                res.RangeResult = RangeResult.Equal;
            }
            else
            {
                res.EqualityResult = EqualityResult.NotEqual;
                if (String.Compare(RegisterA.Value, RegisterB.Value) < 1)
                    res.RangeResult = RangeResult.Less;
                else
                    res.RangeResult = RangeResult.Greater;
            }
            return res;
        }
    }

    public class RegisterValueBytes : RegisterValue
    {
        private byte[] _Value;

        public override RegisterValueType RegisterType { get { return RegisterValueType.rv_byte; } }

        public RegisterValueBytes(RegisterSettings settings)
            : base(settings)
        {
            _Value = null;
        }

        public override void SetValue(byte[] value)
        {
            _Value = value;
        }

        public override void SetValue(String value)
        {
           _Value = SystemServices.HexToBytes(value);
        }

        public override void SetValue(decimal value)
        {
            throw new NotImplementedException();
        }

        public override String ToString() { return BytesToHex(_Value); }
        public override decimal ToDecimal() { throw new NotImplementedException(); }
        public override byte[] ToBytes() { return _Value; }

        public byte[] Value { get { return _Value; } }

        public static RegisterCompareResult CompareBytes(RegisterValueBytes RegisterA, RegisterValueBytes RegisterB)
        {
            RegisterCompareResult res = new RegisterCompareResult();
            if (RegisterA.Value == RegisterB.Value)
            {
                res.EqualityResult = EqualityResult.Equal;
                res.RangeResult = RangeResult.Equal;
            }
            else
            {
                res.EqualityResult = EqualityResult.NotEqual;
                int i;
                for (i = 0; i < RegisterA.Value.Length; i++)
                    if (i < RegisterB.Value.Length)
                    {
                        if (RegisterA.Value[i] < RegisterB.Value[i])
                        {
                            res.RangeResult = RangeResult.Less;
                            break;
                        }
                        else if (RegisterA.Value[i] > RegisterB.Value[i])
                        {
                            res.RangeResult = RangeResult.Greater;
                            break;
                        }
                    }
                    else
                    {
                        res.RangeResult = RangeResult.Greater;
                        break;
                    }

                if (i == RegisterA.Value.Length)
                    if (i < RegisterB.Value.Length)
                        res.RangeResult = RangeResult.Less;
                    else
                    {
                        res.RangeResult = RangeResult.Equal;  // this should not happen if the == above works
                        res.EqualityResult = EqualityResult.Equal;
                    }
            }
            return res;
        }
    }

    public class RegisterValueDecimal : RegisterValue
    {
        private decimal _Value = 0;

        public override RegisterValueType RegisterType { get { return RegisterSettings.Type; } }

        public RegisterValueDecimal(RegisterSettings settings)
            : base(settings)
        {
        }

        public override void SetValue(decimal value)
        {
            _Value = value;
        }

        public override void SetValue(String value)
        {
            _Value = decimal.Parse(value);
        }

        public override void SetValue(byte[] value)
        {
            throw new NotImplementedException();
        }

        public override String ToString() { return _Value.ToString(); }
        public override decimal ToDecimal() { return _Value; }
        public override byte[] ToBytes() { throw new NotImplementedException(); }

        public decimal Value { get { return _Value; } }

        public static RegisterCompareResult CompareDecimal(RegisterValueDecimal RegisterA, RegisterValueDecimal RegisterB)
        {
            RegisterCompareResult res;
            if (RegisterA.Value == RegisterB.Value)
            {
                res.EqualityResult = EqualityResult.Equal;
                res.RangeResult = RangeResult.Equal;
            }
            else
            {
                res.EqualityResult = EqualityResult.NotEqual;
                if (RegisterA.Value < RegisterB.Value)
                    res.RangeResult = RangeResult.Less;
                else
                    res.RangeResult = RangeResult.Greater;
            }
            return res;
        }
    }
}
