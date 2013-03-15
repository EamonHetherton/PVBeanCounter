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
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Xml;
using MackayFisher.Utilities;

namespace PVSettings
{
    public enum RegisterSettingValueType
    {
        FixedValue = 0,
        ReferenceDependency = 1
    }

    public class RegisterListValueSetting
    {
        public String Name = "";
        public String Value = null;
        public String Tag = "";
    }

    public class RegisterSettings : SettingsBase, INotifyPropertyChanged
    {
        public enum RegisterValueType
        {
            rv_bytes = 0,
            rv_string,
            rv_byte,
            rv_uint16,
            rv_uint16_exp,
            rv_uint32,
            rv_sint16,
            rv_sint16_exp,
            rv_sint32,
            rv_bcd
        }

        private DeviceManagementSettings DeviceManagementSettings;

        public RegisterSettings(DeviceManagementSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            DeviceManagementSettings = root;
        }

        public UInt16? Id
        {
            get
            {
                String val = GetValue("id");
                if (val == "")
                    return null;
                if (val.Length > 2)
                {
                    if (val.StartsWith("0x"))
                    {
                        return SystemServices.HexToUInt16(val);
                    }
                }
                return UInt16.Parse(val);
            }
        }

        public UInt16? Position
        {
            get
            {
                String val = GetValue("position");
                if (val == "")
                    return null;
                if (val.Length > 2)
                {
                    if (val.StartsWith("0x"))
                    {
                        return SystemServices.HexToUInt16(val);
                    }
                }
                return UInt16.Parse(val);
            }
        }

        public UInt16? Id1
        {
            get
            {
                String val = GetValue("id1");
                if (val == "")
                    return null;
                if (val.Length > 2)
                {
                    if (val.StartsWith("0x"))
                    {
                        return SystemServices.HexToUInt16(val);
                    }
                }
                return UInt16.Parse(val);
            }
        }

        public UInt16? Id3
        {
            get
            {
                String val = GetValue("id3");
                if (val == "")
                    return null;
                if (val.Length > 2)
                {
                    if (val.StartsWith("0x"))
                    {
                        return SystemServices.HexToUInt16(val);
                    }
                }
                return UInt16.Parse(val);
            }
        }

        public String Name
        {
            get
            {
                String val = GetValue("name");
                if (val == "")
                    val = GetValue("content");

                return val;
            }
        }

        public String Content
        {
            get
            {
                String val = GetValue("content");
                return val;
            }
        }

        public String Tag
        {
            get
            {
                String val = GetValue("tag");
                return val;
            }
        }

        public String Extractor
        {
            get
            {
                String val = GetValue("extractor");
                return val;
            }
        }

        public String Inserter
        {
            get
            {
                String val = GetValue("inserter");
                return val;
            }
        }

        public String Binding
        {
            get
            {
                String val = GetValue("binding");
                return val;
            }
        }

        public bool IsHexadecimal
        {
            get
            {
                String val = GetValue("hexadecimal");
                return val == "true";
            }
        }

        public bool IsAlarmFlag
        {
            get
            {
                String val = GetValue("isalarmflag");
                return val == "true";
            }
        }

        public bool IsAlarmDetail
        {
            get
            {
                String val = GetValue("isalarmdetail");
                return val == "true";
            }
        }

        public bool IsErrorFlag
        {
            get
            {
                String val = GetValue("iserrorflag");
                return val == "true";
            }
        }

        public bool IsErrorDetail
        {
            get
            {
                String val = GetValue("iserrordetail");
                return val == "true";
            }
        }

        public bool HasValue
        {
            get
            {
                String val = GetValue("registervalue");
                return val != "";
            }
        }

        public List<RegisterListValueSetting> ValueList
        {
            get
            {
                XmlElement elem = GetElement("valuelist");
                if (elem == null)
                    return null;

                List<RegisterListValueSetting> list = new List<RegisterListValueSetting>();
                foreach (XmlNode e in elem.ChildNodes)
                {
                    if (e.NodeType == XmlNodeType.Element && e.Name == "option")
                    {
                        RegisterListValueSetting rec = new RegisterListValueSetting();

                        XmlAttribute name = (XmlAttribute)e.Attributes.GetNamedItem("name");
                        XmlAttribute value = (XmlAttribute)e.Attributes.GetNamedItem("value");
                        XmlAttribute tag = (XmlAttribute)e.Attributes.GetNamedItem("tag");

                        if (name == null)
                            rec.Name = "";
                        else
                            rec.Name = name.Value;

                        if (tag == null)
                            rec.Tag = "";
                        else
                            rec.Tag = tag.Value;

                        if (value == null)
                            rec.Value = null;
                        else
                            rec.Value = value.Value;

                        list.Add(rec);
                    }
                }

                return list;
            }
        }

        public String RegisterValue
        {
            get
            {
                String val = GetNullableValue("registervalue");
                return val;
            }
        }

        public bool HasValueList
        {
            get
            {
                XmlElement elem = GetElement("valuelist");
                return (elem != null);
            }
        }

        public bool IsEndBlockOffset
        {
            get
            {
                return GetValue("isendblockoffset") == "true";
            }
        }

        public bool IsMarkerIdentifier
        {
            get
            {
                return GetValue("ismarkeridentifier") == "true";
            }
        }

        public bool VerifyValue
        {
            get
            {
                return GetValue("verifyvalue") == "true";
            }
        }

        public bool IsCString
        {
            get
            {
                return GetValue("type") == "cstring";
            }
        }

        public RegisterValueType Type
        {
            get
            {
                String val = GetValue("type");
                if (val == "string" || val == "cstring")
                    return RegisterValueType.rv_string;
                else if (val == "byte")
                    return RegisterValueType.rv_byte;
                else if (val == "bytes")
                    return RegisterValueType.rv_bytes;
                else if (val == "uint16")
                    return RegisterValueType.rv_uint16;
                else if (val == "uint16_exp")
                    return RegisterValueType.rv_uint16_exp;
                else if (val == "uint32")
                    return RegisterValueType.rv_uint32;
                else if (val == "sint16" || val == "int16")
                    return RegisterValueType.rv_sint16;
                else if (val == "sint16_exp" || val == "int16_exp")
                    return RegisterValueType.rv_sint16_exp;
                else if (val == "sint32" || val == "int32")
                    return RegisterValueType.rv_sint32;
                else if (val == "bcd")
                    return RegisterValueType.rv_bcd;
                else
                    return RegisterValueType.rv_string;
            }
        }

        public BlockSettings.Message Message
        {
            get
            {
                String val = GetValue("message");
                if (val == "send")
                    return BlockSettings.Message.Send;
                else if (val == "receive")
                    return BlockSettings.Message.Receive;
                else
                    return BlockSettings.Message.Both;
            }
        }

        public UInt16? Size
        {
            get
            {
                RegisterValueType type = Type;
                if (type == RegisterValueType.rv_byte)
                    return 1;
                if (type == RegisterValueType.rv_sint16)
                    return 2;
                if (type == RegisterValueType.rv_sint16_exp)
                    return 3;
                if (type == RegisterValueType.rv_sint32)
                    return 4;
                if (type == RegisterValueType.rv_uint16)
                    return 2;
                if (type == RegisterValueType.rv_uint16_exp)
                    return 3;
                if (type == RegisterValueType.rv_uint32)
                    return 4;

                String val = GetValue("size");
                if (val == "")
                    return null;
                return UInt16.Parse(val);
            }
        }

        public bool UseScale
        {
            get
            {
                XmlElement elem = GetElement("scale");
                if (elem == null)
                    return false;

                XmlNodeList elems = elem.GetElementsByTagName("reference");
                if (elems.Count > 0)
                    return true;

                XmlAttribute value = elem.GetAttributeNode("value");
                return value.Value != "1" && value.Value != "";
            }
        }

        public RegisterSettingValueType ScaleValueType
        {
            get
            {
                XmlElement elem = GetElement("scale");
                if (elem == null)
                    return RegisterSettingValueType.FixedValue;

                XmlNodeList elems = elem.GetElementsByTagName("reference");
                if (elems.Count > 0)
                    return RegisterSettingValueType.ReferenceDependency;

                return RegisterSettingValueType.FixedValue;
            }
        }

        public Decimal DefaultScale
        {
            get
            {
                XmlElement elem = GetElement("scale");
                if (elem == null)
                    return 1;

                XmlAttribute defaultValue = elem.GetAttributeNode("default");
                if (defaultValue == null)
                {
                    XmlAttribute value = elem.GetAttributeNode("value");
                    if (value == null)
                        return 1;
                    else
                        return Decimal.Parse(value.Value);
                }
                else
                    return Decimal.Parse(defaultValue.Value);
            }
        }

        public UInt16 RegisterCount
        {
            get
            {
                RegisterValueType type = Type;
                if (type == RegisterValueType.rv_byte)
                    return 1;
                if (type == RegisterValueType.rv_sint16)
                    return 1;
                if (type == RegisterValueType.rv_sint16_exp)
                    return 2;
                if (type == RegisterValueType.rv_sint32)
                    return 2;
                if (type == RegisterValueType.rv_uint16)
                    return 1;
                if (type == RegisterValueType.rv_uint16_exp)
                    return 2;
                if (type == RegisterValueType.rv_uint32)
                    return 2;

                UInt16? size = Size;

                if (!size.HasValue)
                    return 1;

                return (UInt16)((size.Value + 1) / 2);
            }
        }
    }

}

