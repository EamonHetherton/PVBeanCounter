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
using System.Text;
using System.Xml;

namespace PVSettings
{
    public enum ConsolidationType
    {
        PVOutput,
        Generic,
        EnumCount
    }

    public enum EventType
    {
        Yield,
        Consumption,
        FeedInYield,
        FeedInConsumption,
        _TypeCount    // This marker must be last
    }

    public class DeviceEventSettings : PVSettings.SettingsBase
    {        
        private ApplicationSettings ApplicationSettings;
        private DeviceManagerDeviceSettings Device;

        private static List<EventType> _EventTypeList = null;

        public DeviceEventSettings(ApplicationSettings root, XmlElement element, DeviceManagerDeviceSettings device)
            : base(root, element)
        {
            ApplicationSettings = root;
            Device = device;
            if (_EventTypeList == null)
            {
                LoadEventTypeList();
                DoPropertyChanged("EventTypeList");
            }
        }

        public static void LoadEventTypeList()
        {
            _EventTypeList = new List<EventType>();

            for(PVSettings.EventType i = 0; i < PVSettings.EventType._TypeCount; i++ )
                _EventTypeList.Add(i);
            
        }

        public List<EventType> EventTypeList { get { return _EventTypeList; } }

        public ObservableCollection<FeatureSettings> EventFeatures
        {
            get
            {
                return Device.DeviceSettings.FeatureList;
            }
        }

        public FeatureSettings EventFeature
        {
            get
            {
                FeatureType? fromType = EventFeatureType;
                uint? fromId = EventFeatureId;
                if (fromType == null || fromId == null)
                    return null;
                return Device.DeviceSettings.FindFeature(fromType.Value, fromId.Value);
            }
            set
            {
                if (value == null)
                {
                    EventFeatureType = null;
                    EventFeatureId = null;
                }
                else
                {
                    EventFeatureType = value.Type;
                    EventFeatureId = value.Id;
                }
            }
        }

        public FeatureType? EventFeatureType
        {
            get
            {
                String val = GetValue("eventfeaturetype");
                if (val == "")
                    return null;
                return FeatureSettings.FeatureTypeFromString(val);
            }
            set
            {
                FeatureType? old = EventFeatureType;
                if (value.HasValue)
                    SetValue("eventfeaturetype", value.ToString(), "EventFeatureType");
                else
                    SetValue("eventfeaturetype", "", "EventFeatureType");
                if (old != value)
                {
                    EventFeatureId = null;
                    DoPropertyChanged("EventFeatureId");
                }
            }
        }

        public uint? EventFeatureId
        {
            get
            {
                String val = GetValue("eventfeatureid");
                if (val == "")
                    return null;
                return uint.Parse(val);
            }
            set
            {
                if (value.HasValue)
                    SetValue("eventfeatureid", value.ToString(), "EventFeatureId");
                else
                    SetValue("eventfeatureid", "", "EventFeatureId");
            }
        }

        public static EventType? EventTypeFromString(String eventType)
        {
            for (EventType et = (EventType)0; et < PVSettings.EventType._TypeCount; et++)
                if (et.ToString() == eventType)
                    return et;
            return null;
        }

        public EventType? EventType
        {
            get
            {
                String val = GetValue("eventtype");
                if (val == "")
                    return null;
                return EventTypeFromString(val);
            }
            set
            {
                if (value.HasValue)
                    SetValue("eventtype", value.ToString(), "EventType");
                else
                    SetValue("eventtype", "", "EventType");
            }
        }
    }

    public class ConsolidateDeviceSettings : PVSettings.SettingsBase
    {
        public enum OperationType
        {
            Add = 0,
            Subtract
        }

        private DeviceManagerDeviceSettings _ConsolidateToDevice = null;
        private DeviceManagerDeviceSettings _ConsolidateFromDevice = null;
        private ApplicationSettings ApplicationSettings;
        public ConsolidateDeviceSettings(ApplicationSettings root, XmlElement element, DeviceManagerDeviceSettings fromDevice)
            : base(root, element)
        {
            ApplicationSettings = root;
            ConsolidateFromDevice = fromDevice;
        }

        public DeviceManagerDeviceSettings ConsolidateFromDevice 
        { 
            get { return _ConsolidateFromDevice; }
            set
            {
                _ConsolidateFromDevice = value;
                if (value != null && ConsolidateFromFeatureType == FeatureType.Unknown)
                {
                    if (ConsolidateFromFeatures.Count == 1)
                    {
                        ConsolidateFromFeatureType = ConsolidateFromFeatures[0].Type;
                        ConsolidateFromFeatureId = ConsolidateFromFeatures[0].Id;
                    }
                    else
                    {
                        ConsolidateFromFeatureType = FeatureType.Unknown;
                        ConsolidateFromFeatureId = null;
                        foreach (FeatureSettings fs in ConsolidateFromFeatures)
                            if (fs.Type == ConsolidateToFeatureType)
                            {
                                ConsolidateFromFeatureType = ConsolidateToFeatureType;
                                break;
                            }
                    }
                    DoPropertyChanged("ConsolidateFromFeatureType");
                    DoPropertyChanged("ConsolidateFromFeatureId");
                    DoPropertyChanged("ConsolidateFromFeature");
                    DoPropertyChanged("ConsolidateFromFeatures");
                }
            }
        }

        public ObservableCollection<FeatureSettings> ConsolidateFromFeatures
        {
            get
            {
                return ConsolidateFromDevice.DeviceSettings.FeatureList;
            }
        }

        public ObservableCollection<FeatureSettings> ConsolidateToFeatures
        {
            get
            {
                if (_ConsolidateToDevice == null)
                    return new ObservableCollection<FeatureSettings>();
                return _ConsolidateToDevice.DeviceSettings.FeatureList;
            }
        }

        public FeatureSettings ConsolidateFromFeature
        {
            get
            {
                FeatureType? fromType = ConsolidateFromFeatureType;
                uint? fromId = ConsolidateFromFeatureId;
                if (fromType == null || fromId == null)
                    return null;
                return ConsolidateFromDevice.DeviceSettings.FindFeature(fromType.Value, fromId.Value);
            }
            set
            {
                if (value == null)
                {
                    ConsolidateFromFeatureType = null;
                    ConsolidateFromFeatureId = null;
                }
                else
                {
                    ConsolidateFromFeatureType = value.Type;
                    ConsolidateFromFeatureId = value.Id;
                }
            }
        }

        public FeatureType? ConsolidateFromFeatureType 
        { 
            get
            {
                String val = GetValue("consolidatefromfeaturetype");
                if (val == "")
                    return null;
                return FeatureSettings.FeatureTypeFromString(val);
            }
            set
            {
                FeatureType? old = ConsolidateFromFeatureType;            
                if (value.HasValue)
                    SetValue("consolidatefromfeaturetype", value.ToString(), "ConsolidateFromFeatureType");
                else
                    SetValue("consolidatefromfeaturetype", "", "ConsolidateFromFeatureType");
                if (old != value)
                {
                    ConsolidateFromFeatureId = null;
                    DoPropertyChanged("ConsolidateFromFeatureId");
                }
            }
        }

        public uint? ConsolidateFromFeatureId
        {
            get
            {
                String val = GetValue("consolidatefromfeatureid");
                if (val == "")
                    return null;
                return uint.Parse(val);
            }
            set
            {
                if (value.HasValue)
                    SetValue("consolidatefromfeatureid", value.ToString(), "ConsolidateFromFeatureId");
                else
                    SetValue("consolidatefromfeatureid", "", "ConsolidateFromFeatureId");
            }
        }

        public DeviceManagerDeviceSettings ConsolidateToDevice 
        {
            get
            {
                if (_ConsolidateToDevice != null)
                    return _ConsolidateToDevice;
                string val = GetValue("devicename");
                _ConsolidateToDevice = ApplicationSettings.GetDeviceByName(val);
                return _ConsolidateToDevice;
            }
            set
            {
                DeviceManagerDeviceSettings old = ConsolidateToDevice;
                
                if (value == null)
                    SetValue("devicename", "", "DeviceName");
                else if (ConsolidateFromDevice.CheckDeviceRecursion(value)) // reject change if recursion detected
                    return;
                else
                    SetValue("devicename", value.Name, "DeviceName");
                _ConsolidateToDevice = value;
                if (old != null)
                    old.ConsolidateFromDevices.Remove(this);
                if (value != null)
                    value.ConsolidateFromDevices.Add(this);

                if (ConsolidateToFeatures.Count == 1)
                {
                    ConsolidateToFeatureType = ConsolidateToFeatures[0].Type;
                    ConsolidateToFeatureId = ConsolidateToFeatures[0].Id;
                }
                else
                {
                    ConsolidateToFeatureType = FeatureType.Unknown;
                    ConsolidateToFeatureId = 0;
                    foreach (FeatureSettings fs in ConsolidateToFeatures)
                        if (fs.Type == ConsolidateFromFeatureType)
                        {
                            ConsolidateToFeatureType = ConsolidateFromFeatureType.Value;
                            break;
                        }
                }
                //DoPropertyChanged("ConsolidateFromFeatures");
                DoPropertyChanged("ConsolidateToFeatureType");
                DoPropertyChanged("ConsolidateToFeatureId");
                DoPropertyChanged("ConsolidateToFeature");
                DoPropertyChanged("ConsolidateToFeatures");
            }
        }

        public FeatureSettings ConsolidateToFeature
        {
            get
            {
                FeatureType fromType = ConsolidateToFeatureType;
                uint? fromId = ConsolidateToFeatureId;
                if (fromType == FeatureType.Unknown || fromId == null)
                    return null;
                return ConsolidateToDevice.DeviceSettings.FindFeature(fromType, fromId.Value);
            }
            set
            {
                if (value == null)
                {
                    ConsolidateToFeatureType = FeatureType.Unknown;
                    ConsolidateToFeatureId = 0;
                }
                else
                {
                    ConsolidateToFeatureType = value.Type;
                    ConsolidateToFeatureId = value.Id;
                }
            }
        }

        public FeatureType ConsolidateToFeatureType
        {
            get
            {
                String val = GetValue("consolidatetofeaturetype");
                if (val == "")
                    return FeatureType.Unknown;
                return FeatureSettings.FeatureTypeFromString(val);
            }
            set
            {
                FeatureType old = ConsolidateToFeatureType;            
                SetValue("consolidatetofeaturetype", value.ToString(), "ConsolidateToFeatureType");
                if (old != value)
                {
                    ConsolidateToFeatureId = 0;
                    DoPropertyChanged("ConsolidateToFeatureId");
                }
            }
        }

        public uint ConsolidateToFeatureId
        {
            get
            {
                String val = GetValue("consolidatetofeatureid");
                if (val == "")
                    return 0;
                return uint.Parse(val);
            }
            set
            {               
                SetValue("consolidatetofeatureid", value.ToString(), "ConsolidateToFeatureId");
            }
        }

        public void RefreshDeviceReference()
        {
            DeviceManagerDeviceSettings dev = ConsolidateToDevice;
            if (dev == null)
                SetValue("devicename", "", "DeviceName");
            else
                SetValue("devicename", dev.Name, "DeviceName");
        }

        public String DeviceName
        {
            get
            {
                return GetValue("devicename");
            }
        }

        public OperationType Operation
        {
            get
            {
                String op = GetValue("operation");
                if (op == "Add")
                    return OperationType.Add;
                else 
                    return OperationType.Subtract;
            }

            set
            {
                SetValue("operation", value.ToString(), "Operation");
            }
        }        
    }


    public class DeviceManagerDeviceSettings : PVSettings.SettingsBase, MackayFisher.Utilities.INamedItem
    {
        private ApplicationSettings ApplicationSettings;
        private DeviceSettings _DeviceSettings = null;

        private DeviceManagerSettings _DeviceManagerSettings;

        public ObservableCollection<ConsolidateDeviceSettings> consolidateToDevices = null;
        public ObservableCollection<ConsolidateDeviceSettings> consolidateFromDevices = null;

        public ObservableCollection<DeviceEventSettings> deviceEvents = null;

        public DeviceManagerDeviceSettings(ApplicationSettings root, XmlElement element, DeviceManagerSettings deviceManagerSettings)
            : base(root, element)
        {
            ApplicationSettings = root;
            _DeviceManagerSettings = deviceManagerSettings;
            RemoveOldElements();
            LoadDetails();
        }

        private void LoadDetails()
        {
            XmlElement consolSettings = GetElement("consolidatetodevices");
            if (consolSettings == null)
                consolSettings = AddElement(settings, "consolidatetodevices");

            consolidateToDevices = new ObservableCollection<ConsolidateDeviceSettings>();
            consolidateFromDevices = new ObservableCollection<ConsolidateDeviceSettings>();

            RegisterEvents();
        }

        public void RegisterEvents()
        {
            XmlElement events = GetElement("deviceevents");
            if (events == null)
                events = AddElement(settings, "deviceevents");

            deviceEvents = new ObservableCollection<DeviceEventSettings>();
            
            foreach (XmlElement e in events.ChildNodes)
            {
                if (e.Name == "event")
                {
                    DeviceEventSettings eventSettings = new DeviceEventSettings(ApplicationSettings, e, this);
                    deviceEvents.Add(eventSettings);
                }
            }
        }

        public void RegisterConsolidations()
        {
            XmlElement consolSettings = GetElement("consolidatetodevices");
            foreach (XmlElement e in consolSettings.ChildNodes)
            {
                if (e.Name == "device")
                {
                    ConsolidateDeviceSettings consolidation = new ConsolidateDeviceSettings(ApplicationSettings, e, this);
                    consolidateToDevices.Add(consolidation);
                    if (consolidation.ConsolidateToDevice != null)
                        consolidation.ConsolidateToDevice.ConsolidateFromDevices.Add(consolidation);
                }
            }
        }

        private void RemoveOldElements()
        {
            DeleteElement("description");
        }

        public DeviceSettings DeviceSettings
        {
            get
            {
                if (_DeviceSettings == null)
                    _DeviceSettings = ApplicationSettings.DeviceManagementSettings.GetDevice(Id);
                return _DeviceSettings;
            }
        }

        public ObservableCollection<ConsolidateDeviceSettings> ConsolidateToDevices { get { return consolidateToDevices; } }

        public ObservableCollection<ConsolidateDeviceSettings> ConsolidateFromDevices { get { return consolidateFromDevices; } }

        public ObservableCollection<DeviceEventSettings> DeviceEvents { get { return deviceEvents; } }

        internal bool CheckDeviceRecursion(DeviceManagerDeviceSettings device)
        {
            if (device == null)
                return false;
            if (device.Name == Name)    
                return true;

            foreach (ConsolidateDeviceSettings consolSettings in device.ConsolidateToDevices)
            {
                DeviceManagerDeviceSettings listDevice = consolSettings.ConsolidateFromDevice;
                if (listDevice != null && listDevice.CheckDeviceRecursion(listDevice))
                    return true;
            }

            return false;
        }

        public void AddEvent()
        {
            XmlElement events = GetElement("deviceevents");
            XmlElement e = AddElement(events, "event");

            DeviceEventSettings newEvent = new DeviceEventSettings(ApplicationSettings, e, this);
            deviceEvents.Add(newEvent);

            DoPropertyChanged("DeviceEvents");
        }

        public void DeleteEvent(DeviceEventSettings e)
        {
            DeviceEventSettings forDeletion = null;

            foreach (DeviceEventSettings settings in deviceEvents)
            {
                if (settings == e)
                {
                    forDeletion = settings;
                    break;
                }
            }

            if (forDeletion != null)
                deviceEvents.Remove(forDeletion);


            XmlElement devices = GetElement("deviceevents");
            if (devices == null || e == null)
                return;
            
            devices.RemoveChild(e.settings);
            SettingChangedEventHandler("");
                    
            DoPropertyChanged("DeviceEvents");
        }

        public void AddDevice(ConsolidateDeviceSettings.OperationType operation)
        {
            
            XmlElement devices = GetElement("consolidatetodevices");
            XmlElement e = AddElement(devices, "device");

            ConsolidateDeviceSettings newConsol = new ConsolidateDeviceSettings(ApplicationSettings, e, this);
            newConsol.ConsolidateToDevice = null;
            newConsol.Operation = operation;
            consolidateToDevices.Add(newConsol);
            
            DoPropertyChanged("ConsolidateDevices");
            DoPropertyChanged("ConsolidateFromDevices");
        }
      

        public void DeleteDevice(ConsolidateDeviceSettings consol)
        {
            ConsolidateDeviceSettings forDeletion = null;

            foreach (ConsolidateDeviceSettings consolSettings in ConsolidateToDevices)
            {
                if (consolSettings == consol)
                {
                    forDeletion = consol;
                    break;
                }
            }

            if (forDeletion != null)
            {
                consolidateToDevices.Remove(forDeletion);
                if (forDeletion.ConsolidateToDevice != null)
                    forDeletion.ConsolidateToDevice.ConsolidateFromDevices.Remove(forDeletion);
            }

            XmlElement devices = GetElement("consolidatetodevices");
            if (devices == null || consol == null)
                return;
                       
            devices.RemoveChild(consol.settings);
            SettingChangedEventHandler("");
                   
            DoPropertyChanged("ConsolidateDevices");
            DoPropertyChanged("ConsolidateFromDevices");
        }

        public DeviceManagerSettings DeviceManagerSettings
        {
            get { return _DeviceManagerSettings; }
        }

        public String Name
        {
            get
            {
                String val = GetValue("name");
                if (val == "")
                {
                    DeviceEnumerator devices = new DeviceEnumerator(ApplicationSettings.DeviceManagerList);
                    String newName = MackayFisher.Utilities.UniqueNameResolver<DeviceManagerDeviceSettings>.ResolveUniqueName(
                        "My Device", devices, this);
                    SetValue("name", newName, "Name", true);
                    return newName;
                }
                else
                    return val;
            }
            
            set
            {
                DeviceEnumerator devices = new DeviceEnumerator(ApplicationSettings.DeviceManagerList);
                SetValue("name",
                    MackayFisher.Utilities.UniqueNameResolver<DeviceManagerDeviceSettings>.ResolveUniqueName(value, devices, this),
                    "Name");
                // the following dorces the name change into the ConsolidateDeviceSettings entries by refreshing the device reference
                foreach (ConsolidateDeviceSettings consol in ConsolidateFromDevices)
                    consol.RefreshDeviceReference();
            }
        }

        public String UniqueName { get { return Name; } }

        public static ConsolidationType? StringToConsolidationType(string value)
        {
            for (int i = 0; i < (int)PVSettings.ConsolidationType.EnumCount; i++)
                if (value == ((ConsolidationType)i).ToString())
                    return (ConsolidationType)i;
            return null;
        }

        public ObservableCollection<PVSettings.ConsolidationType> AllConsolidationTypes
        {
            get
            {
                ObservableCollection<PVSettings.ConsolidationType> col = new ObservableCollection<ConsolidationType>();
                for (int i = 0; i < (int)PVSettings.ConsolidationType.EnumCount; i++)
                    col.Add((ConsolidationType)i);
                return col;
            }
        }

        public DeviceType DeviceType
        {
            get
            {
                return DeviceSettings.DeviceType;
            }
        }

        public ConsolidationType? ConsolidationType
        {
            get
            {
                if (DeviceType != PVSettings.DeviceType.Consolidation)
                    return null;
                string val = GetValue("consolidationtype");
                if (val == "")
                    return PVSettings.ConsolidationType.PVOutput;
                else
                    return StringToConsolidationType(val);
            }
            set
            {
                if (value.HasValue && value < PVSettings.ConsolidationType.EnumCount)
                    SetValue("consolidationtype", value.Value.ToString(), "ConsolidationType");
                else
                    SetValue("consolidationtype", "", "ConsolidationType");
            }
        }

        public ObservableCollection<PvOutputSiteSettings> PVOutputSystemList { get { return ApplicationSettings.PvOutputSystemList; } }

        public String PVOutputSystem
        {
            get
            {
                return GetValue("pvoutputsystem");
            }

            set
            {
                SetValue("pvoutputsystem", value, "PVOutputSystem");
            }
        }
        
        public String Id
        {
            get
            {
                return GetValue("id");
            }
            
            set
            {
                _DeviceSettings = null;  // clear cached reference to device type details
                SetValue("id", value, "Id");
            }             
        }
 
        public String SerialNo
        {
            get
            {
                return GetValue("serialno"); ;
            }

            set
            {
                SetValue("serialno", value, "SerialNo");
            }
        }

        public UInt64 Address
        {
            get
            {
                String val = GetValue("address");
                if (val == "")
                    return 0;
                else
                    return UInt64.Parse(val);
            }

            set
            {
                SetValue("address", value.ToString(), "Address");
            }
        }

        public float CalibrationFactor
        {
            get
            {
                String val = GetValue("calibrationfactor");
                if (val == "")
                    return 1.0F;
                else
                    return float.Parse(val);
            }

            set
            {
                SetValue("calibrationfactor", value.ToString(), "CalibrationFactor");
            }
        }

        public uint Feature
        {
            get
            {
                String val = GetValue("feature");
                if (val == "")
                    return 0;
                else
                    return uint.Parse(val);
            }

            set
            {
                SetValue("feature", value.ToString(), "Feature");
            }
        }

        public uint ZeroThreshold
        {
            get
            {
                String val = GetValue("zerothreshold");
                if (val == "")
                    return 0;
                else
                    return uint.Parse(val);
            }

            set
            {
                SetValue("zerothreshold", value.ToString(), "ZeroThreshold");
            }
        }

        public String Manufacturer
        {
            get
            {
                return GetValue("manufacturer").Trim();
            }

            set
            {
                SetValue("manufacturer", value, "Manufacturer");
            }
        }

        public String Model
        {
            get
            {
                return GetValue("model").Trim();
            }

            set
            {
                SetValue("model", value, "Model");
            }
        }

        public String QueryInterval
        {
            get
            {
                String val = GetValue("queryinterval");
                return val;
            }

            set
            {
                SetValue("queryinterval", value, "QueryInterval");
                if (DBIntervalInt < QueryIntervalInt)
                    QueryInterval = DBInterval;
                else if ((DBIntervalInt / QueryIntervalInt) * QueryIntervalInt != DBIntervalInt)
                    QueryInterval = DBInterval;
            }
        }

        public UInt16 QueryIntervalInt
        {
            get
            {
                String val = GetValue("queryinterval");
                if (val == "")                   
                    return DeviceManagerSettings.MessageIntervalInt;
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
                if (DBIntervalInt < QueryIntervalInt)
                    QueryInterval = DBInterval;
                else if ((DBIntervalInt / QueryIntervalInt) * QueryIntervalInt != DBIntervalInt)
                    QueryInterval = DBInterval;
            }
        }

        public UInt16 DBIntervalInt
        {
            get
            {
                String val = GetValue("dbinterval");
                if (val == "")
                    return DeviceManagerSettings.DBIntervalInt;
                else
                    return UInt16.Parse(val); ;
            }
        }

        public bool Enabled
        {
            get
            {
                string val = GetValue("enabled");
                if (val == "")
                    val = GetValue("enable");  // check legacy value
                return val == "true";
            }

            set
            {
                if (Id == "")
                    value = false;

                SetValue("enabled", value ? "true" : "false", "Enabled");
                DeleteElement("enable");
            }
        }

        public bool AutoEvents
        {
            get
            {
                string val = GetValue("autoevents");
                if (val == "")
                    return false;
                return val == "true";
            }

            set
            {
                SetValue("autoevents", value ? "true" : "false", "AutoEvents");
            }
        }

        public bool UpdateHistory
        {
            get
            {
                string val = GetValue("updatehistory");
                if (val == "")
                    return true;
                return val == "true";
            }

            set
            {
                SetValue("updatehistory", value ? "true" : "false", "UpdateHistory");
            }
        }

        public bool StoreHistory
        {
            get
            {
                string val = GetValue("storehistory");
                if (val == "")
                    return true;
                return val == "true";
            }

            set
            {
                SetValue("storehistory", value ? "true" : "false", "StoreHistory");
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

        public ObservableCollection<DeviceManagementSettings.DeviceListItem> DeviceListItems
        {
            get
            {
                return DeviceManagerSettings.DeviceListItems;
            }
        }

        public bool DeviceManagerSelected
        {
            get { return DeviceManagerSettings.IsSelected; }
        }

        public void NotifySelectionChange()
        {
            DoPropertyChanged("DeviceManagerSelected");
            DoPropertyChanged("DeviceListItems");
        }
    }
}
