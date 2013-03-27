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
using System.Text;
using System.Xml;

namespace PVSettings
{
    public enum DeviceManagerType
    {
        Modbus = 0,
        SMA_SunnyExplorer,
        SMA_WebBox,
        Owl_Meter,
        CC128,
        Consolidation
    }

    public class DeviceManagerSettings : SettingsBase, MackayFisher.Utilities.INamedItem
    {
        public ApplicationSettings ApplicationSettings;
        public PVSettings.SerialPortSettings SerialPort;
        private ProtocolSettings ProtocolSettings;

        ObservableCollection<DeviceManagerDeviceSettings> deviceList = null;

        public bool IsRealDeviceManager { get { return ManagerType != DeviceManagerType.Consolidation; } }

        private bool? _UsesSerialPort = null;
        public bool UsesSerialPort 
        { 
            get 
            {
                if (_UsesSerialPort.HasValue)
                    return _UsesSerialPort.Value;
                ProtocolSettings protocol = ApplicationSettings.DeviceManagementSettings.GetProtocol(Protocol);
                _UsesSerialPort = protocol.UsesSerialPort;
                return _UsesSerialPort.Value;
            } 
        }

        public String PortName
        {
            get { return !UsesSerialPort ? null : SerialPort.PortName; }
            set
            {
                if (SerialPort != null)
                {
                    SerialPort.PortName = value;
                    DoPropertyChanged("Description");
                }
            }
        }

        public String BaudRate
        {
            get { return !UsesSerialPort ? null : SerialPort.BaudRate.HasValue ? SerialPort.BaudRate.ToString() : ProtocolSettings.BaudRate; }
            set
            {
                if (SerialPort != null)
                    SerialPort.BaudRate = value == "" ? (int?)null : Convert.ToInt32(value);
            }
        }

        public String DataBits
        {
            get { return !UsesSerialPort ? null : SerialPort.DataBits.HasValue ? SerialPort.DataBits.ToString() : ProtocolSettings.DataBits; }
            set
            {
                if (SerialPort != null)
                    SerialPort.DataBits = value == "" ? (int?)null : Convert.ToInt32(value);
            }
        }

        public String StopBits
        {
            get { return !UsesSerialPort ? null : SerialPort.StopBits.HasValue ? PVSettings.SerialPortSettings.ToString(SerialPort.StopBits) : ProtocolSettings.StopBits; }
            set
            {
                if (SerialPort != null)
                    SerialPort.StopBits = PVSettings.SerialPortSettings.ToStopBits(value);
            }
        }

        public String Parity
        {
            get { return !UsesSerialPort ? null : SerialPort.Parity.HasValue ? PVSettings.SerialPortSettings.ToString(SerialPort.Parity) : ProtocolSettings.Parity; }
            set
            {
                if (SerialPort != null)
                    SerialPort.Parity = PVSettings.SerialPortSettings.ToParity(value);
            }
        }

        public String Handshake
        {
            get { return !UsesSerialPort ? null : SerialPort.Handshake.HasValue ? PVSettings.SerialPortSettings.ToString(SerialPort.Handshake) : ProtocolSettings.Handshake; }
            set
            {
                if (SerialPort != null)
                    SerialPort.Handshake = PVSettings.SerialPortSettings.ToHandshake(value);
            }
        }

        public DeviceManagerSettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
            ApplicationSettings = (ApplicationSettings)root;
            SerialPort = null;
            ProtocolSettings = ApplicationSettings.DeviceManagementSettings.GetProtocol(Protocol);

            LoadDetails();
        }

        private void LoadSerialPort()
        {
            ProtocolSettings = ApplicationSettings.DeviceManagementSettings.GetProtocol(Protocol);
            _UsesSerialPort = null; // forces refer back tp protocol for this info
            if (UsesSerialPort)
            {
                XmlElement serial = GetElement("serialport");
                if (serial != null)
                    SerialPort = new PVSettings.SerialPortSettings(ApplicationSettings, serial);
                else
                {
                    serial = AddElement(settings, "serialport");
                    SerialPort = new PVSettings.SerialPortSettings(ApplicationSettings, serial);
                }
            }
            else
                SerialPort = null;
        }

        private void LoadDetails()
        {
            LoadSerialPort();
            XmlElement devices = GetElement("devices");
            if (devices == null)
                devices = AddElement(settings, "devices");

            deviceList = new ObservableCollection<DeviceManagerDeviceSettings>();

            foreach (XmlElement e in devices.ChildNodes)
            {
                if (e.Name == "device")
                {
                    DeviceManagerDeviceSettings device = new DeviceManagerDeviceSettings(ApplicationSettings, e, this);
                    deviceList.Add(device);
                }
            }
        }

        public DeviceManagerDeviceSettings AddDevice()
        {
            XmlElement inverters = GetElement("devices");
            if (inverters == null)
                return null;
            XmlElement e = AddElement(inverters, "device");
            DeviceManagerDeviceSettings dev = new DeviceManagerDeviceSettings(ApplicationSettings, e, this);
            dev.Address = 0;
            dev.Name = "";
            dev.Id = DeviceListItems[0].Id;
            deviceList.Add(dev);
            ApplicationSettings.RefreshAllDevices();
            
            return dev;
        }

        public void DeleteDevice(DeviceManagerDeviceSettings delDev)
        {
            deviceList.Remove(delDev);

            XmlElement devices = GetElement("devices");
            if (devices == null || delDev == null)
                return;

            foreach (XmlNode child in devices.ChildNodes)
            {
                if (child.Name == "device")
                {
                    if (ElementHasChild(child, "address", delDev.Address.ToString())
                        && ElementHasChild(child, "id", delDev.Id.ToString()))
                    {
                        devices.RemoveChild(child);
                        ApplicationSettings.RefreshAllDevices();
                        SettingChangedEventHandler("");
                        return;
                    }
                }
            }
        }

        public ObservableCollection<DeviceManagementSettings.DeviceListItem> DeviceListItems
        {
            get
            {
                return ApplicationSettings.DeviceManagementSettings.GetDeviceList(Protocol);
            }
        }

        public ObservableCollection<DeviceManagerDeviceSettings> DeviceList { get { return deviceList; } }

        public DeviceManagerDeviceSettings GetDevice(uint address)
        {
            foreach (DeviceManagerDeviceSettings dev in deviceList)
            {
                if (dev.Address == address)
                    return dev;
            }
            return null;
        }

        public DeviceSettings ListenerDeviceSettings
        {
            get
            {
                String listenerDevice = GetValue("listenerdeviceid");
                if (listenerDevice == "")
                    return null;
                
                return ApplicationSettings.DeviceManagementSettings.GetDevice(listenerDevice);               
            }
            
        }

        public String ListenerDeviceId
        {
            get
            {
                return GetValue("listenerdeviceid");
            }
            set
            {
                SetValue("listenerdeviceid", value, "ListenerDeviceId");
            }
        }

        public void CheckListenerDeviceId()
        {
            if (ApplicationSettings.DeviceManagementSettings.GetProtocol(Protocol).Type != ProtocolSettings.ProtocolType.Listener)
            {
                ListenerDeviceId = "";
                DoPropertyChanged("ListenerDeviceId");
                return;
            }

            foreach (DeviceManagementSettings.DeviceListItem item in DeviceListItems)
            {
                if (item.Id == ListenerDeviceId)
                    return;
            }
            if (DeviceListItems.Count > 0)
            {
                ListenerDeviceId = DeviceListItems[0].Id;
                DoPropertyChanged("ListenerDeviceId");
            }
        }

        public String Protocol
        {
            get
            {
                string val = GetValue("protocol");
                return val == "" ? "Modbus" : val;
            }
            set
            {
                SetValue("protocol", value, "Protocol");
                DoPropertyChanged("DeviceListItems");
                foreach (DeviceManagerDeviceSettings device in DeviceList)
                    device.NotifySelectionChange();
                CheckListenerDeviceId();
                LoadSerialPort();
                DoPropertyChanged("ExecutablePath");
                DoPropertyChanged("ManagerType");
                DoPropertyChanged("ManagerTypeName");
                DoPropertyChanged("SunnyExplorerPlantName");
                DoPropertyChanged("SunnyExplorerPassword");
            }
        }

        public String Name
        {
            get
            {
                String val = GetValue("name");
                if (val == "")
                {
                    String newName = MackayFisher.Utilities.UniqueNameResolver<DeviceManagerSettings>.ResolveUniqueName(
                        "My Device Manager", ApplicationSettings.DeviceManagerList.GetEnumerator(), this);
                    SetValue("name", newName, "Name", true);
                    return newName;
                }
                else
                    return val;
            }

            set
            {
                SetValue("name", 
                    MackayFisher.Utilities.UniqueNameResolver<DeviceManagerSettings>.ResolveUniqueName( value, ApplicationSettings.DeviceManagerList.GetEnumerator(), this), 
                    "Name");
                DoPropertyChanged("Description");
            }
        }

        public String UniqueName { get { return Name; } }

        public String Description
        {
            get
            {
                if (SerialPort == null)
                    return Name;
                else
                    return Name + ": " + SerialPort.PortName;
            }
        }

        public String ManagerTypeName
        {
            get
            {
                return ProtocolSettings.Name;
            }
        }

        /*
        public String ManagerId
        {
            get
            {
                String val = ManagerTypeName;

                return val + "/" + InstanceNo.ToString();
            }
        }
        */

        public static DeviceManagerType GetManagerType(String managerTypeName)
        {
            if (managerTypeName == "SMA_SunnyExplorer")
                return DeviceManagerType.SMA_SunnyExplorer;
            else if (managerTypeName == "SMA_WebBox")
                return DeviceManagerType.SMA_WebBox;
            else if (managerTypeName == "Owl_Meter")
                return DeviceManagerType.Owl_Meter;
            else if (managerTypeName == "CC128")
                return DeviceManagerType.CC128;
            else if (managerTypeName == "Consolidation")
                return DeviceManagerType.Consolidation;
            else
                return DeviceManagerType.Modbus;    
        }

        public static String GetManagerTypeName(DeviceManagerType managerType)
        {
            return managerType.ToString();            
        }

        public DeviceManagerType ManagerType
        {
            get
            {
                return GetManagerType(ManagerTypeName);          
            }
        }

        public int InstanceNo
        {
            get
            {
                String rffd = GetValue("instanceno");
                return Convert.ToInt32(rffd);
            }

            set
            {
                SetValue("instanceno", value.ToString(), "InstanceNo");
            }
        }

        public bool Enabled
        {
            get
            {
                return GetValue("enabled") == "true";
            }

            set
            {
                SetValue("enabled", value ? "true" : "false", "Enabled");
            }
        }

        // Used with Listener device managers

        public String MessageInterval
        {
            get
            {
                String val = GetValue("messageinterval");
                return val;
            }

            set
            {
                SetValue("messageinterval", value, "MessageInterval");
                if (DBIntervalInt < MessageIntervalInt)
                {
                    DBInterval = MessageInterval;
                    DoPropertyChanged("DBInterval");
                }
                else if ((DBIntervalInt / MessageIntervalInt) * MessageIntervalInt != DBIntervalInt)
                {
                    DBInterval = MessageInterval;
                    DoPropertyChanged("DBInterval");
                }
            }
        }

        public UInt16 MessageIntervalInt
        {
            get
            {
                String val = GetValue("messageinterval");
                if (val == "")
                    if (ManagerType == DeviceManagerType.SMA_SunnyExplorer)
                        return 300;
                    else
                        return 6;
                else
                    return UInt16.Parse(val); ;
            }
        }

        public String DBInterval
        {
            get
            {
                String val = GetValue("dbinterval");
                return val;
            }

            set
            {
                SetValue("dbinterval", value, "DBInterval");
                if (DBIntervalInt < MessageIntervalInt)
                {
                    MessageInterval = DBInterval;
                    DoPropertyChanged("MessageInterval");
                }
                else if ((DBIntervalInt / MessageIntervalInt) * MessageIntervalInt != DBIntervalInt)
                {
                    MessageInterval = DBInterval;
                    DoPropertyChanged("MessageInterval");
                }
            }
        }

        public UInt16 DBIntervalInt
        {
            get
            {
                String val = GetValue("dbinterval");
                if (val == "")
                    if (ManagerType == DeviceManagerType.SMA_SunnyExplorer)
                        return 300;
                    else
                        return 60;
                else
                    return UInt16.Parse(val); ;
            }
        }

        public String ExecutablePath
        {
            get
            {
                if (ProtocolSettings.Type != PVSettings.ProtocolSettings.ProtocolType.Executable)
                    return "";

                String val = GetValue("executablepath").Trim();
                if (val == "")
                {
                    val = "C:\\Program Files (x86)\\SMA\\Sunny Explorer\\SunnyExplorer.exe";
                    try
                    {
                        val = System.IO.Path.GetFullPath(val);
                    }
                    catch (ArgumentException)
                    {
                        val = "C:\\Program Files\\SMA\\Sunny Explorer\\SunnyExplorer.exe";
                        try
                        {
                            val = System.IO.Path.GetFullPath(val);
                        }
                        catch (ArgumentException)
                        {
                            val = "Cannot find SunnyExplorer.exe - Is it installed?";
                        }
                    }
                }
                return val;
            }

            set
            {
                if (value != null) value = value.Trim();
                if (ProtocolSettings.Type == PVSettings.ProtocolSettings.ProtocolType.Executable)
                    SetValue("executablepath", value, "ExecutablePath");
            }
        }

        public String SunnyExplorerPlantName
        {
            get
            {
                if (ManagerType != DeviceManagerType.SMA_SunnyExplorer)
                    return "";

                String name = GetValue("sunnyexplorerplantname").Trim();

                if (name == "")
                {
                    // global value moved to inverter manager specific settings
                    // retrieve old global value and delete the old node
                    name = ApplicationSettings.OldSunnyExplorerPlantName.Trim();
                    if (name != "")
                        SetValue("sunnyexplorerplantname", name, "SunnyExplorerPlantName");
                    ApplicationSettings.DeleteValue("sunnyexplorerplantname");
                }

                if (name == "")
                    return "SunnyExplorer";
                else
                    return name;
            }

            set
            {
                if (ManagerType == DeviceManagerType.SMA_SunnyExplorer)
                    SetValue("sunnyexplorerplantname", value.Trim(), "SunnyExplorerPlantName");
            }
        }

        public String SunnyExplorerPassword
        {
            get
            {
                if (ManagerType != DeviceManagerType.SMA_SunnyExplorer)
                    return "";
                string val = GetValue("sunnyexplorerpassword").Trim();
                if (val == "")
                    val = "0000";
                return val;
            }

            set
            {
                if (ManagerType == DeviceManagerType.SMA_SunnyExplorer)
                    SetValue("sunnyexplorerpassword", value.Trim(), "SunnyExplorerPassword");
            }
        }

        public int MaxSMAHistoryDays
        {
            get
            {
                String rffd = GetValue("maxsmahistorydays");
                if (rffd == "")
                    return 64;
                else
                    return Convert.ToInt32(rffd);
            }

            set
            {
                SetValue("maxsmahistorydays", value.ToString(), "MaxSMAHistoryDays");
            }
        }

        public int? HistoryHours
        {
            get
            {
                String rffd = GetValue("historyhours");
                if (rffd == "")
                    return 24;
                else
                    return Convert.ToInt32(rffd);
            }

            set
            {
                if (value == null)
                    SetValue("historyhours", "", "HistoryHours");
                else
                    SetValue("historyhours", value.Value.ToString(), "HistoryHours");
            }
        }

        public DateTime? FirstFullDay
        {
            get
            {
                String ffd = GetValue("firstfullday");
                if (ffd == "")
                    return ApplicationSettings.FirstFullDay;
                else
                    return ApplicationSettings.StringToDate(ffd);
            }

            set
            {
                SetValue("firstfullday", ApplicationSettings.DateToString(value), "FirstFullDay");
            }
        }


        public bool ResetFirstFullDay
        {
            get
            {
                String rffd = GetValue("resetfirstfullday");
                DateTime? resetFirstFullDatSet = ResetFirstFullDaySet;

                // ResetFirstFullDay is only logically set for 10 minutes
                if (rffd == "true")
                    if (resetFirstFullDatSet >= (DateTime.Now - TimeSpan.FromMinutes(10.0)))
                        return true;
                return false;
            }

            set
            {
                if (value)
                {
                    SetValue("resetfirstfullday", "true", "ResetFirstFullDay");
                    ResetFirstFullDaySet = DateTime.Now;
                }
                else
                {
                    SetValue("resetfirstfullday", "", "ResetFirstFullDay");
                    ResetFirstFullDaySet = null;
                }
            }
        }

        public DateTime? ResetFirstFullDaySet
        {
            get
            {
                return ApplicationSettings.StringToDateTime(GetValue("resetfirstfulldayset"));
            }

            set
            {
                SetValue("resetfirstfulldayset", ApplicationSettings.DateTimeToString(value), "ResetFirstFullDaySet");
            }
        }

        private bool _IsSelected = false;
        public bool IsSelected 
        {
            get { return _IsSelected; }
            
            set
            {
                _IsSelected = value;
                DoPropertyChanged("IsSelected");
                foreach (DeviceManagerDeviceSettings device in DeviceList)
                    device.NotifySelectionChange();
            }
        }
        
    }

}

