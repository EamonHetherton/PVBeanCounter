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
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Xml;
using Conversations;
using MackayFisher.Utilities;

namespace PVSettings
{
    public class MessageSettings : SettingsBase, INotifyPropertyChanged
    {
        private static int NextMessageId = 1;

        public int MessageId { get; private set; }

        public class ElementListValue
        {
            public String Name = "";
            public String Type = "";
            public UInt16? Size = null;
            public String ValueString = null;
            public byte[] ValueBytes = null;
            public String SizeName = null;
            public bool ExcludeFromChecksum = false;

            public bool ValueIsHex
            {
                get
                {
                    if (ValueString == null)
                        return false;
                    if (ValueString.Length > 2)
                    {
                        String sub = ValueString.Substring(0, 2);
                        if (sub == "0x" || sub == "0X")
                            return true;
                    }
                    return false;
                }
            }
        }

        private DeviceManagementSettings DeviceManagementSettings;
        private ObservableCollection<ElementListValue> _ElementList;

        public MessageSettings(DeviceManagementSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            MessageId = NextMessageId++;
            DeviceManagementSettings = root;
            LoadElements();
        }

        public ObservableCollection<ElementListValue> ElementList { get { return _ElementList; } }

        private void LoadElements()
        {
            _ElementList = new ObservableCollection<ElementListValue>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "element")
                {
                    XmlAttribute name = (XmlAttribute)e.Attributes.GetNamedItem("name");
                    XmlAttribute type = (XmlAttribute)e.Attributes.GetNamedItem("type");
                    XmlAttribute value = (XmlAttribute)e.Attributes.GetNamedItem("value");
                    XmlAttribute excludeFromChecksum = (XmlAttribute)e.Attributes.GetNamedItem("excludefromchecksum");
                    if (name != null && type != null)
                    {
                        ElementListValue element = ParseElementType(name.Value, type.Value);
                        if (element != null)
                        {
                            if (value != null)
                            {
                                element.ValueString = value.Value;
                                if (element.ValueIsHex)
                                {
                                    element.ValueBytes = SystemServices.HexToBytes(element.ValueString);
                                    element.Size = (UInt16)element.ValueBytes.Length;
                                }
                                else
                                    element.Size = (UInt16)element.ValueString.Length;
                            }
                            if (excludeFromChecksum != null)
                                element.ExcludeFromChecksum = excludeFromChecksum.Value == "true";
                            _ElementList.Add(element);
                        }
                    }
                    else if (value != null)
                    {
                        ElementListValue element = new ElementListValue();
                        element.Type = "BYTE";
                        element.ValueString = value.Value;
                        if (element.ValueIsHex)
                        {
                            element.ValueBytes = SystemServices.HexToBytes(element.ValueString);
                            element.Size = (UInt16)element.ValueBytes.Length;
                        }
                        else
                            element.Size = (UInt16)element.ValueString.Length;
                        if (excludeFromChecksum != null)
                            element.ExcludeFromChecksum = excludeFromChecksum.Value == "true";
                        _ElementList.Add(element);
                    }
                }
            }
        }

        private ElementListValue ParseElementType(String name, String typeSpec)
        {
            String type = "";
            UInt16? size = null;
            String sizeText = null;
            try
            {
                int sizeBracketPos = typeSpec.IndexOf('[');
                if (sizeBracketPos > 0)
                {
                    int sizeStartPos = sizeBracketPos + 1;
                    type = typeSpec.Substring(0, sizeBracketPos).Trim().ToUpper();
                    int sizeEndBracketPos = typeSpec.IndexOf(']', sizeStartPos);
                    if (sizeEndBracketPos > sizeBracketPos)
                    {
                        try
                        {
                            sizeText = typeSpec.Substring(sizeStartPos, sizeEndBracketPos - sizeStartPos).Trim();
                            size = System.Convert.ToUInt16(sizeText);
                            sizeText = null;
                        }
                        catch (System.FormatException)  // sizeText will be contain the size variable name
                        {
                        }
                    }
                }
                else if (sizeBracketPos == -1)
                {
                    type = typeSpec.Trim().ToUpper();
                }
            }
            catch (Exception)
            {
                return null;
            }
            ElementListValue val = new ElementListValue();
            val.Name = name;
            val.Type = type;
            val.Size = size;
            val.SizeName = sizeText;
            return val;
        }

        public Conversations.MessageType Type
        {
            get
            {
                String val = GetValue("type");
                for (MessageType i = 0; i < MessageType.ValueCount; i++)
                    if (i.ToString() == val)
                        return i;
                return MessageType.ValueCount;  // return invalid message type if no match
            }
        }

        public String Name
        {
            get
            {
                String val = GetValue("name");
                if (val == "")
                    return GetValue("type");
                else
                    return val;
            }
        }
    }

}

