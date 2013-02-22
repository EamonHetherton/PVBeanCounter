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
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Xml;
using MackayFisher.Utilities;

namespace PVSettings
{
    
    public class BlockSettings : PVSettings.SettingsBase, INotifyPropertyChanged
    {
        public enum Message
        {
            Send = 0,
            Receive,
            Both
        }

        private ObservableCollection<RegisterSettings> _RegisterList;

        private ObservableCollection<BlockMessageSettings> _MessageList;

        public BlockSettings(DeviceManagementSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            RootSettings = root;
            LoadRegisters("register",(DeviceManagementSettings)RootSettings, settings, ref _RegisterList);
            LoadMessages(settings, ref _RegisterList);
        }

        internal static void LoadRegisters(string name, DeviceManagementSettings rootSettings, XmlElement parent, ref ObservableCollection<RegisterSettings> registerList)
        {
            registerList = new ObservableCollection<RegisterSettings>();
            foreach (XmlNode e in parent.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == name)
                {
                    RegisterSettings register = new RegisterSettings((DeviceManagementSettings)rootSettings, (XmlElement)e);
                    registerList.Add(register);
                }
            }
        }

        private void LoadMessages(XmlElement parent, ref ObservableCollection<RegisterSettings> registerList)
        {
            _MessageList = new ObservableCollection<BlockMessageSettings>();
            foreach (XmlNode e in parent.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "message")
                {
                    BlockMessageSettings message = new BlockMessageSettings((DeviceManagementSettings)RootSettings, (XmlElement)e);
                    _MessageList.Add(message);
                }
            }
        }

        public ObservableCollection<RegisterSettings> RegisterList { get { return _RegisterList; } }
        public ObservableCollection<BlockMessageSettings> MessageList { get { return _MessageList; } }

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

        public String Type
        {
            get
            {
                String val = GetValue("type");
                return val;
            }
        }

        public bool OnDbWriteOnly
        {
            get
            {
                String val = GetValue("ondbwriteonly");
                return val == "true";
            }
        }

        public String Conversation
        {
            get
            {
                String val = GetValue("conversation");
                return val;
            }
        }

        public byte? CommandId
        {
            get
            {
                String val = GetValue("commandid");
                if (val == "")
                    return null;
                return byte.Parse(val);
            }
        }

        public Int32? Base  // can be set to -1 - allow negatives
        {
            get
            {
                String val = GetValue("base");                
                if (val == "")
                    return null;
                if (val.Length > 2)
                {
                    if (val.StartsWith("0x"))
                    {
                        return SystemServices.HexToUInt16(val);
                    }
                }
                return Int32.Parse(val);
            }
        }

        public Int32? AltBase
        {
            get
            {
                String val = GetValue("altbase");
                if (val == "")
                    return null;
                if (val.Length > 2)
                {
                    if (val.StartsWith("0x"))
                    {
                        return SystemServices.HexToUInt16(val);
                    }
                }
                return Int32.Parse(val);
            }
        }

        public Int16 TimeoutRetries
        {
            get
            {
                String val = GetValue("timeoutretries");
                if (val == "")
                    return 0;
                
                return Int16.Parse(val);
            }
        }

        public bool Optional
        {
            get
            {
                return GetValue("optional") == "true";
            }
        }

        public bool RelativeBase
        {
            get
            {
                return GetValue("relativebase") == "true";
            }
        }

    }
}

