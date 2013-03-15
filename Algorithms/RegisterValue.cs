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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MackayFisher.Utilities;
using PVSettings;

namespace Algorithms
{
    public class RegisterListValue
    {
        public String Name = "";
        public RegisterValue Value = null;
        public String Tag = "";
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

        protected RegisterSettings RegisterSettings = null;
        public Register Register { get; protected set; }

        public virtual RegisterSettings.RegisterValueType RegisterType { get { return RegisterSettings.Type; } }

        public RegisterValue(Register register)
        {
            Register = register;
            RegisterSettings = register.Settings;
        }

        public abstract decimal ValueDecimal { get; }
        public abstract String ValueString { get; }
        public abstract byte[] ValueBytes { get; }

        public abstract RegisterCompareResult Compare(RegisterValue RegisterB);
    }

    public class RegisterValueString : RegisterValue
    {
        private String _Value = "";

        public override RegisterSettings.RegisterValueType RegisterType { get { return RegisterSettings.RegisterValueType.rv_string; } }

        public RegisterValueString(RegisterString register)
            : base(register)
        {
        }

        public RegisterValueString(RegisterString register, String value)
            : base(register)
        {
            _Value = value;
        }

        public void SetValue(String value)
        {
            _Value = value;
        }

        public override String ValueString { get { return _Value; } }
        public override decimal ValueDecimal
        {
            get
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
        }

        public override byte[] ValueBytes
        {
            get
            {
                if (RegisterSettings.IsHexadecimal)
                    return SystemServices.HexToBytes(_Value);
                else
                    return SystemServices.StringToBytes(_Value);
            }
        }

        public override RegisterCompareResult Compare(RegisterValue RegisterB)
        {
            RegisterCompareResult res;
            try
            {
                String valueB = RegisterB.ValueString;
                if (_Value == valueB)
                {
                    res.EqualityResult = EqualityResult.Equal;
                    res.RangeResult = RangeResult.Equal;
                }
                else
                {
                    res.EqualityResult = EqualityResult.NotEqual;
                    if (String.Compare(_Value, valueB) < 1)
                        res.RangeResult = RangeResult.Less;
                    else
                        res.RangeResult = RangeResult.Greater;
                }
            }
            catch (Exception)
            {
                res.EqualityResult = EqualityResult.Incompatible;
                res.RangeResult = RangeResult.NotAvailable;
            }
            return res;
        }
    }

    public class RegisterValueBytes : RegisterValue
    {
        private byte[] _Value = null;

        public override RegisterSettings.RegisterValueType RegisterType { get { return RegisterSettings.RegisterValueType.rv_byte; } }

        public RegisterValueBytes(Register register)
            : base(register)
        {
        }

        public RegisterValueBytes(Register register, string value)
            : base(register)
        {
            if (value.StartsWith("0x"))
                _Value = SystemServices.HexToBytes(value);
            else
                _Value = SystemServices.StringToBytes(value);
        }

        public void SetValue(byte[] value)
        {
            _Value = value;
        }

        public override String ValueString { get { return BytesToHex(_Value); } }
        public override decimal ValueDecimal
        {
            get
            {
                GlobalSettings.LogMessage("RegisterVlaueBytes.ValueDecimal", "Cannot convert Bytes to Decimal", LogEntryType.ErrorMessage);
                throw new NotImplementedException("RegisterVlaueBytes.ValueDecimal" + " - " + "Cannot convert Bytes to Decimal");
            }
        }
        public override byte[] ValueBytes { get { return _Value; } }

        public override RegisterCompareResult Compare(RegisterValue RegisterB)
        {
            RegisterCompareResult res;
            try
            {
                byte[] valueB = RegisterB.ValueBytes;

                res.EqualityResult = EqualityResult.NotEqual;
                res.RangeResult = RangeResult.NotAvailable;
                int i;
                for (i = 0; i < _Value.Length; i++)
                    if (i < valueB.Length)
                    {
                        if (_Value[i] < valueB[i])
                        {
                            res.RangeResult = RangeResult.Less;
                            break;
                        }
                        else if (_Value[i] > valueB[i])
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

                if (i == _Value.Length)
                    if (i < valueB.Length)
                        res.RangeResult = RangeResult.Less;
                    else
                    {
                        res.RangeResult = RangeResult.Equal;
                        res.EqualityResult = EqualityResult.Equal;
                    }
            }
            catch (Exception)
            {
                res.EqualityResult = EqualityResult.Incompatible;
                res.RangeResult = RangeResult.NotAvailable;
            }
            return res;
        }
    }

    public class RegisterValueNumber : RegisterValue
    {
        private decimal _Value = 0;

        public override RegisterSettings.RegisterValueType RegisterType { get { return RegisterSettings.Type; } }

        public RegisterValueNumber(Register register)
            : base(register)
        {
        }

        public RegisterValueNumber(Register register, String value)
            : base(register)
        {
            if (value.StartsWith("0x"))
                _Value = SystemServices.HexToDecimal(value);
            else
                _Value = Decimal.Parse(value);
        }

        public void SetValue(Decimal value)
        {
            _Value = value;
        }

        public override String ValueString { get { return _Value.ToString(); } }
        public override decimal ValueDecimal { get { return _Value; } }
        public override byte[] ValueBytes { get { return ((RegisterNumber)Register).NumberToBytes(_Value); } }

        public override RegisterCompareResult Compare(RegisterValue RegisterB)
        {
            RegisterCompareResult res;
            try
            {
                Decimal valueB = RegisterB.ValueDecimal;
                if (_Value == valueB)
                {
                    res.EqualityResult = EqualityResult.Equal;
                    res.RangeResult = RangeResult.Equal;
                }
                else
                {
                    res.EqualityResult = EqualityResult.NotEqual;
                    if (_Value < valueB)
                        res.RangeResult = RangeResult.Less;
                    else
                        res.RangeResult = RangeResult.Greater;
                }
            }
            catch (Exception)
            {
                res.EqualityResult = EqualityResult.Incompatible;
                res.RangeResult = RangeResult.NotAvailable;
            }
            return res;
        }
    }
}

