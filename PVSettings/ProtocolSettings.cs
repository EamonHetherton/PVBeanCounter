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

namespace PVSettings
{
    public class ProtocolSettings : PVSettings.SettingsBase, INotifyPropertyChanged
    {
        public enum ProtocolType
        {
            Generic = 0,
            Modbus,
            Phoenixtec,
            QueryResponse,
            Listener,
            ManagerQueryResponse,
            Executable
        }

        private DeviceManagementSettings DeviceManagementSettings;
        private ObservableCollection<ConversationSettings> _ConversationList;
        private SerialPortSettings SerialPort;

        public bool UsesSerialPort { get { return SerialPort != null; } }

        public String Name
        {
            get
            {
                String n = settings.GetAttribute("name");
                //String val = GetValue("name");
                return n;
            }
        }

        
        public List<DeviceSettings.GroupName> DeviceGroups
        {
            get
            {
                List<DeviceSettings.GroupName> names = new List<DeviceSettings.GroupName>();
                foreach (XmlNode e in settings.ChildNodes)
                {
                    if (e.NodeType == XmlNodeType.Element && e.Name == "groupname")
                    {
                        XmlAttribute valueAttrib = (XmlAttribute)e.Attributes.GetNamedItem("value");
                        XmlAttribute idAttrib = (XmlAttribute)e.Attributes.GetNamedItem("id");

                        if (valueAttrib != null)
                        {
                            DeviceSettings.GroupName name;
                            name.Name = valueAttrib.Value;
                            if (idAttrib != null)
                                name.Id = idAttrib.Value;
                            else
                                name.Id = name.Name;
                            names.Add(name);
                        }
                    }
                }
                /*
                if (names.Count == 0)
                {
                    DeviceSettings.GroupName name;
                    name.Name = Name;
                    name.Id = name.Name;
                    names.Add(name);
                }
                */
                return names;
            }
        }

        public String BaudRate 
        { 
            get { return (SerialPort == null) ? null : SerialPort.BaudRate.ToString(); } 
            set 
            {
                if (SerialPort != null)
                    SerialPort.BaudRate = value == "" ? (int?)null : Convert.ToInt32(value);
            } 
        }

        public String DataBits 
        { 
            get { return (SerialPort == null) ? null : SerialPort.DataBits.ToString(); } 
            set 
            { 
                 if (SerialPort != null)
                     SerialPort.DataBits = value == "" ? (int?)null : Convert.ToInt32(value); 
            } 
        }

        public String StopBits 
        {
            get { return (SerialPort == null) ? null : PVSettings.SerialPortSettings.ToString(SerialPort.StopBits); } 
            set 
            {
                if (SerialPort != null)
                    SerialPort.StopBits = PVSettings.SerialPortSettings.ToStopBits(value); 
            } 
        }

        public String Parity 
        {
            get { return (SerialPort == null) ? null : PVSettings.SerialPortSettings.ToString(SerialPort.Parity); }
            set 
            {
                if (SerialPort != null)
                    SerialPort.Parity = PVSettings.SerialPortSettings.ToParity(value); 
            }
        }

        public String Handshake 
        {
            get { return (SerialPort == null) ? null : PVSettings.SerialPortSettings.ToString(SerialPort.Handshake); } 
            set 
            {
                if (SerialPort != null)
                    SerialPort.Handshake = PVSettings.SerialPortSettings.ToHandshake(value); 
            } 
        }


        public ProtocolSettings(DeviceManagementSettings root, XmlElement element)
            : base(root, element)
        {
            DeviceManagementSettings = root;
            LoadConversations();
            XmlElement serialport = GetElement("serialportsettings");
            if (serialport == null)
                SerialPort = null;
            else
                SerialPort = new SerialPortSettings(root, serialport);
        }

        public ObservableCollection<ConversationSettings> Conversations { get { return _ConversationList; } }

        private void LoadConversations()
        {
            _ConversationList = new ObservableCollection<ConversationSettings>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "conversation")
                {
                    ConversationSettings conv = new ConversationSettings(DeviceManagementSettings, (XmlElement)e);

                    _ConversationList.Add(conv);
                }
            }
        }

        public ProtocolType Type
        {
            get
            {
                String val = GetValue("type");
                if (val == "Modbus")
                    return ProtocolType.Modbus;
                else if (val == "Phoenixtec")
                    return ProtocolType.Phoenixtec;
                else if (val == "QueryResponse")
                    return ProtocolType.QueryResponse;
                else if (val == "Listener")
                    return ProtocolType.Listener;
                else if (val == "ManagerQueryResponse")
                    return ProtocolType.ManagerQueryResponse;
                else if (val == "Executable")
                    return ProtocolType.Executable;
                else
                    return ProtocolType.Generic;
            }
        }

        public override string ToString()
        {
            return Name;
        }
       
        public String AutoAddress
        {
            get
            {
                String val = GetValue("autoaddress");
                return val;
            }
        }

        public UInt16 AddressLow
        {
            get
            {
                String val = GetValue("addresslow");
                if (val == "")
                    return 1;
                return UInt16.Parse(val);
            }
        }

        public UInt16 AddressHigh
        {
            get
            {
                String val = GetValue("addresshigh");
                if (val == "")
                    return 254;
                return UInt16.Parse(val);
            }
        }

        public String Endian32Bit
        {
            get
            {
                String val = GetValue("endian32bit");
                if (val == "")
                    return "Big";
                else
                    return val;
            }
        }

        public String Endian16Bit
        {
            get
            {
                String val = GetValue("endian16bit");
                if (val == "")
                    return "Big";
                else
                    return val;
            }
        }

        public String CheckSumEndian16Bit
        {
            get
            {
                String val = GetValue("checksumendian16bit");
                if (val == "")
                    return "Big";
                else
                    return val;
            }
        }

        public String CheckSum
        {
            get
            {
                String val = GetValue("checksum");
                if (val == "")
                    return "CheckSum16";
                else
                    return val;
            }
        }

    }
}
