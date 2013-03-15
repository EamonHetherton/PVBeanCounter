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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml;

namespace PVSettings
{
    public class InverterManagerSettings : SettingsBase
    {
        public enum InverterManagerType
        {
            Unknown = -1,
            SMASunnyExplorer = 0,
            SMABluetooth = 1,
            CMS = 2,
            Meter = 3,
            SMAWebBox = 4,
            KLNE = 5,
            Growatt = 6,
            SAMIL = 7,
            JFY = 8,
            Devices = 9
        }

        public ObservableCollection<DeviceManagerDeviceSettings> inverterList;

        private static InverterManagerType GetInverterManagerType(String typeName)
        {
            for (int i = 0; i <= InverterManagerSettings.InverterManagerTypes.GetUpperBound(0); i++)
                if (InverterManagerSettings.InverterManagerTypes[i].Name == typeName)
                    return (InverterManagerSettings.InverterManagerType)i;

            throw new Exception("GetInverterManagerType - Unknown Inverter Manager Type: " + typeName);
        }

        private ApplicationSettings ApplicationSettings;
        private InverterManagerInfo ManagerInfo;

        public InverterManagerSettings(ApplicationSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            ApplicationSettings = (ApplicationSettings)root;
            SerialPort = null;
            if (typeName != null) // Cannot be null when creating a new instance of an Inverter Manager using the Add Manager button
                ManagerTypeName = typeName;
            ManagerInfo = GetManagerInfo();
            ManagerInfo.IsLoaded = true;

            inverterList = new ObservableCollection<DeviceManagerDeviceSettings>();

            LoadInvertersSub();
        }

        public ObservableCollection<DeviceManagerDeviceSettings> InverterList { get { return inverterList; } }

        private void LoadInvertersSub()
        {
            XmlElement serial = GetElement("serialport");
            if (serial != null)
                SerialPort = new SerialPortSettings(ApplicationSettings, serial);
            else if (ManagerInfo.UsesSerialPort)
            {
                serial = AddElement(settings, "serialport");
                SerialPort = new SerialPortSettings(ApplicationSettings, serial);
            }
                
            if (ManagerType == InverterManagerType.Growatt)
            {
                XmlElement inverters = GetElement("inverterlist");
                if (inverters == null)
                    inverters = AddElement(settings, "inverterlist");

                foreach (XmlElement e in inverters.ChildNodes)
                {
                    if (e.Name == "inverter")
                    {
                        DeviceManagerDeviceSettings inverter = new DeviceManagerDeviceSettings(ApplicationSettings, e, null);

                        inverterList.Add(inverter);
                    }
                }
            }

            RemoveOldElements();
        }

        private void RemoveOldElements()
        {
            DeleteElement("forceuseenergytotal");
        }

        public DeviceManagerDeviceSettings AddInverter()
        {
            XmlElement inverters = GetElement("inverterlist");
            if (inverters == null)
                return null;
            XmlElement e = AddElement(inverters, "inverter");
            DeviceManagerDeviceSettings inv = new DeviceManagerDeviceSettings(ApplicationSettings, e, null);
            inv.Address = 0;
            inv.Enabled = true;

            inverterList.Add(inv);
            return inv;
        }

        public void DeleteInverter(DeviceManagerDeviceSettings delInv)
        {
            inverterList.Remove(delInv);

            XmlElement inverters = GetElement("inverterlist");
            if (inverters == null)
                return;

            foreach (XmlNode child in inverters.ChildNodes)
            {
                if (child.Name == "inverter")
                {
                    if (ElementHasChild(child, "address", delInv.Address.ToString()))
                    {
                        inverters.RemoveChild(child);
                        SettingChangedEventHandler("");
                        return;
                    }
                }
            }
        }

        public int SampleFrequency 
        {
            get
            {
                InverterManagerType type = ManagerType;
                if (type == InverterManagerType.CMS)
                    return QueryPeriod.HasValue ? QueryPeriod.Value : 6;
                else if (type == InverterManagerType.KLNE)
                    return QueryPeriod.HasValue ? QueryPeriod.Value : 6;
                else if (type == InverterManagerType.Meter)
                    return 6;
                else if (type == InverterManagerType.SMASunnyExplorer)
                    return 300;
                else if (type == InverterManagerType.SMAWebBox)
                    return 900;
                else if (type == InverterManagerType.Growatt)
                    return QueryPeriod.HasValue ? QueryPeriod.Value : 6;
                else if (type == InverterManagerType.SAMIL)
                    return QueryPeriod.HasValue ? QueryPeriod.Value : 6;
                else if (type == InverterManagerType.JFY)
                    return QueryPeriod.HasValue ? QueryPeriod.Value : 6;
                else if (type == InverterManagerType.Devices)
                    return QueryPeriod.HasValue ? QueryPeriod.Value : 6;
                else
                    return 300;
            }
        }

        public class InverterManagerInfo
        {
            public InverterManagerType Type { get; private set; }
            public String Name { get; private set; }
            public bool CanInsert { get; private set; }
            public bool IsLoaded;
            public bool UsesSerialPort;

            public InverterManagerInfo(InverterManagerType type, String name, bool canInsert, bool usesSerialPort)
            {
                Type = type;
                Name = name;
                CanInsert = canInsert;
                IsLoaded = false;
                UsesSerialPort = usesSerialPort;
            }
        }

        public static InverterManagerInfo[] InverterManagerTypes = 
        { 
            new InverterManagerInfo( InverterManagerType.SMASunnyExplorer, "Sunny Explorer", false, false), 
            new InverterManagerInfo( InverterManagerType.SMABluetooth, "SMA Bluetooth", false, false),
            new InverterManagerInfo( InverterManagerType.CMS, "CMS", true, true),
            new InverterManagerInfo( InverterManagerType.Meter, "Meter", false, false),
            new InverterManagerInfo( InverterManagerType.SMAWebBox, "SMA WebBox", true, false),
            new InverterManagerInfo( InverterManagerType.KLNE, "KLNE", true, true),
            new InverterManagerInfo( InverterManagerType.Growatt, "Growatt", true, true),
            new InverterManagerInfo( InverterManagerType.SAMIL, "SAMIL", true, true),
            new InverterManagerInfo( InverterManagerType.JFY, "JFY", true, true),
            new InverterManagerInfo( InverterManagerType.Devices, "Devices", false, false)
        };

        internal SerialPortSettings SerialPort;

        public String PortName { get { return SerialPort == null ? "" : SerialPort.PortName; } set { SerialPort.PortName = value; } }
        public String BaudRate { get { return SerialPort == null ? "" : SerialPort.BaudRate.ToString(); } set { SerialPort.BaudRate = value == "" ? (int?)null : Convert.ToInt32(value); } }
        public String DataBits { get { return SerialPort == null ? "" : SerialPort.DataBits.ToString(); } set { SerialPort.DataBits = value == "" ? (int?)null : Convert.ToInt32(value); } }
        public String StopBits { get { return SerialPort == null ? "" : SerialPortSettings.ToString(SerialPort.StopBits); } set { SerialPort.StopBits = SerialPortSettings.ToStopBits(value); } }
        public String Parity { get { return SerialPort == null ? "" : SerialPortSettings.ToString(SerialPort.Parity); } set { SerialPort.Parity = SerialPortSettings.ToParity(value); } }
        public String Handshake { get { return SerialPort == null ? "" : SerialPortSettings.ToString(SerialPort.Handshake); } set { SerialPort.Handshake = SerialPortSettings.ToHandshake(value); } }

        public InverterManagerInfo GetManagerInfo()
        {
            foreach (InverterManagerInfo info in InverterManagerTypes)
            {
                if (info.Name == ManagerTypeName)
                    return info;
            }
            throw new Exception("GetManagerInfo - Unknown Inverter Manager Type: " + ManagerTypeName);
        }

        public bool CanInsert
        {
            get
            {
                foreach(InverterManagerInfo info in InverterManagerTypes)
                {
                    if (info.Name == ManagerTypeName)
                        return info.CanInsert;
                }
                return false;
            }
        }

        public InverterManagerType ManagerType
        {
            get
            {
                return GetInverterManagerType(ManagerTypeName);
            }
        }


        public String ManagerTypeName
        {
            get
            {
                String val = GetValue("managertype");
                if (val == "SMABluetooth")
                {
                    val = "SMA Bluetooth";
                    SetValue("managertype", val, "ManagerType");
                }
                else if (val == "Current Cost")
                {
                    val = "Meter";
                    SetValue("managertype", val, "ManagerType");
                }
                else if (val == "Modbus")
                {
                    val = "Devices";
                    SetValue("managertype", val, "ManagerType");
                }
                return val;
            }

            set
            {
                SetValue("managertype", value, "ManagerType");
            }
        }

        public String Description
        {
            get
            {
                String val = GetValue("managertype");
                if (val == "SMABluetooth")
                    val = "SMA Bluetooth";
                else if (val == "Current Cost")
                    val = "Meter";
                else if (val == "Modbus")
                    val = "Devices";
                
                return val + "/" + InstanceNo.ToString(); 
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
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMABluetooth].Name)
                    return false;
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.Devices].Name)
                    return false;
                String rffd = GetValue("enabled");
                return (rffd == "true");
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMABluetooth].Name)
                    SetValue("enabled", "", "Enabled");
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.Devices].Name)
                    SetValue("enabled", "", "Enabled");
                else if (value)
                    SetValue("enabled", "true", "Enabled");
                else
                    SetValue("enabled", "", "Enabled");
            }
        }

        public bool UseMeterTemperature
        {
            get
            {
                String rffd = GetValue("usemetertemperature");
                return (rffd == "true");
            }
            set
            {
                SetValue("usemetertemperature", value ? "true" : "false" , "UseMeterTemperature");
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

        // YieldThreshold - used by Current Cost meter inverter manager - 
        // sets the noise level in watts. Readings below this are assumed to be zero
        public int? YieldThreshold
        {
            get
            {
                String rffd = GetValue("yieldthreshold");

                if (rffd == "")
                    return null;

                return Convert.ToInt32(rffd);
            }

            set
            {
                if (value == null)
                    SetValue("yieldthreshold", "", "YieldThreshold");
                else
                    SetValue("yieldthreshold", value.ToString(), "YieldThreshold");
            }
        }

        private int NearestMultipleAofB(int valA, int valB)
        {
            if (valB == 0)
                return valA;

            int rem = valA % valB;
            if (rem == 0)
                return valA;
            else
                return valB * ((valA / valB) + 1);
        }

        public int? QueryPeriod
        {
            get
            {
                String rffd = GetValue("queryperiod");

                if (rffd == "")
                    return null;

                return Convert.ToInt32(rffd);
            }

            set
            {
                if (value.HasValue)
                {
                    // Range check
                    int val = value.Value;
                    if (val > 60)
                        val = 60;
                    else if (val < 6)
                        val = 6;

                    SetValue("queryperiod", val.ToString(), "QueryPeriod");
                    // Must not be greater than dbPeriod period
                    int? dbPeriod = DatabasePeriod;
                    if (dbPeriod.HasValue)
                        if (dbPeriod.Value < val)
                            DatabasePeriod = val;
                        else
                        {
                            int dbNearMultiple = NearestMultipleAofB( dbPeriod.Value, val);
                            if (dbNearMultiple != dbPeriod.Value)
                                if (dbNearMultiple > 60 || (60 % dbNearMultiple) != 0 )
                                {
                                    DatabasePeriod = 60;
                                    SetValue("queryperiod", "", "QueryPeriod");
                                }
                                else
                                    DatabasePeriod = dbNearMultiple;
                        }
                }
                else
                    SetValue("queryperiod", "", "QueryPeriod");
            }
        }

        public int? DatabasePeriod
        {
            get
            {
                String rffd = GetValue("databaseperiod");

                if (rffd == "")
                    return null;

                return Convert.ToInt32(rffd);
            }

            set
            {
                if (value.HasValue)
                {
                    // Range check
                    int val = value.Value;
                    if (val > 60)
                        val = 60;
                    else if (val < 6)
                        val = 6;

                    SetValue("databaseperiod", val.ToString(), "DatabasePeriod");
                    // Must not be lower than query period
                    int? period = QueryPeriod;
                    if (period.HasValue)
                        if (period.Value > val)
                            QueryPeriod = val;
                        else
                        {
                            int dbNearMultiple = NearestMultipleAofB(val, period.Value);
                            if (dbNearMultiple != val)
                                QueryPeriod = val;                                
                        }
                }
                else
                    SetValue("databaseperiod", "", "DatabasePeriod");                
            }
        }

        public int? CrazyDayStartMinutes
        {
            get
            {
                if (ManagerInfo.Type != InverterManagerType.CMS)
                    return null;

                String rffd = GetValue("crazydaystartminutes");

                if (rffd == "")
                    return null;
                
                int value = Convert.ToInt32(rffd);
                if (value < 0)
                    value = 0;
                else if (value > 120)
                    value = 120;

                return value;
            }

            set
            {
                if (value == null)
                    SetValue("crazydaystartminutes", "", "CrazyDayStartMinutes");
                else
                    SetValue("crazydaystartminutes", value.ToString(), "CrazyDayStartMinutes");
            }
        }

        public String SunnyExplorerPath
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMASunnyExplorer].Name)
                    return "";

                String val = GetValue("sunnyexplorerpath").Trim();

                if (val == "")
                {
                    // global value moved to inverter manager specific settings
                    // retrieve old global value and delete the old node
                    val = ApplicationSettings.OldSunnyExplorerPath.Trim();
                    if (val != "")
                        SetValue("sunnyexplorerpath", val, "SunnyExplorerPath");
                    ApplicationSettings.DeleteValue("sunnyexplorerpath");
                }
                return val;
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMASunnyExplorer].Name)
                    SetValue("sunnyexplorerpath", value.Trim(), "SunnyExplorerPath");
            }
        }

        public String SunnyExplorerPlantName
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMASunnyExplorer].Name)
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
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMASunnyExplorer].Name)
                    SetValue("sunnyexplorerplantname", value.Trim(), "SunnyExplorerPlantName");
            }
        }

        public String SunnyExplorerPassword
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMASunnyExplorer].Name)
                    return "";

                String val = GetValue("sunnyexplorerpassword").Trim();

                return val;
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMASunnyExplorer].Name)
                    SetValue("sunnyexplorerpassword", value.Trim(), "SunnyExplorerPassword");
            }
        }

        public String WebBoxFtpUrl
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return "";

                String val = GetValue("webboxftpurl").Trim();

                return val;
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    SetValue("webboxftpurl", value.Trim(), "WebBoxFtpUrl");
            }
        }

        public String WebBoxFtpBasePath
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return "";
                
                if (WebBoxVersion == 1)
                    return "DATA";
                else
                    return "XML";
            }
        }

        public int IntervalOffset
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return 0;

                String val = GetValue("intervaloffset").Trim();

                if (val == "")
                    return 2;

                return Convert.ToInt32(val);
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    SetValue("intervaloffset", value.ToString(), "IntervalOffset");
            }
        }

        public int WebBoxVersion
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return 0;

                String val = GetValue("webboxversion").Trim();

                if (val == "")
                    return 1;

                return Convert.ToInt32(val);
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    SetValue("webboxversion", value.ToString(), "WebBoxVersion");
            }
        }


        public String WebBoxUserName
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return "";

                String val = GetValue("webboxusername").Trim();

                if (val == "")
                    return "user";

                return val;
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    SetValue("webboxusername", value.Trim(), "WebBoxUserName");
            }
        }

        public String WebBoxPassword
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return "";

                String val = GetValue("webboxpassword").Trim();

                return val;
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    SetValue("webboxpassword", value.Trim(), "WebBoxPassword");
            }
        }

        public int? WebBoxFtpLimit
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return null;

                String val = GetValue("webboxftplimit").Trim();

                return val == "" ? (int?) null : int.Parse(val);
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    if (value == null)
                        SetValue("webboxftplimit", "", "WebBoxFtpLimit");
                    else
                        SetValue("webboxftplimit", value.Value.ToString(), "WebBoxFtpLimit");
            }
        }

        public bool WebBoxUsePush
        {
            get
            {
                String rffd = GetValue("webboxusepush");
                return (rffd == "true");
            }
            set
            {
                SetValue("webboxusepush", value ? "true" : "false", "WebBoxUsePush");
            }
        }

        public String WebBoxPushDirectory
        {
            get
            {
                if (ManagerTypeName != InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    return "";

                return GetValue("webboxpushdirectory").Trim();
            }

            set
            {
                if (ManagerTypeName == InverterManagerSettings.InverterManagerTypes[(int)InverterManagerSettings.InverterManagerType.SMAWebBox].Name)
                    SetValue("webboxpushdirectory", value.Trim(), "WebBoxPushDirectory");
            }
        }
    }
}
