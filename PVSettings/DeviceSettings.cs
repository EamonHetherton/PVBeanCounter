﻿/*
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
    public enum DeviceType
    {
        Unknown, // this marker must be first
        Inverter,
        EnergyMeter,
        WeatherStation,
        Consolidation,
        _TypeCount  // this marker must be last
    }

    public enum MeasureType
    {
        Unknown, // this marker must be first
        Energy,
        Temperature,
        _TypeCount   // This marker must be last
    }

    public enum FeatureType
    {
        Unknown = 0,    // this marker must be first
        EnergyAC,        
        EnergyDC,        
        YieldAC,        
        YieldDC,        
        ConsumptionAC,
        GridFeedInAC,
        _TypeCount    // This marker must be last
    }

    public class FeatureSettings
    {
        public FeatureSettings(XmlElement element)
        {
            String val = element.GetAttribute("type");
            if (val == "")
                FeatureType = FeatureType.Unknown;
            else
                FeatureType = FeatureTypeFromString(val);
            
            _FeatureId = 0;
            val = element.GetAttribute("id");
            if (val != "")
            {
                uint.TryParse(val, out _FeatureId);
            }
            val = element.GetAttribute("description");
            if (val == "")
                Name = FeatureType.ToString() + "_" + _FeatureId.ToString();
            else
                Name = val.Trim();

        }

        //public int Id = -1;

        private uint _FeatureId;
        public uint FeatureId { get { return _FeatureId; } private set { _FeatureId = value; } }
        public FeatureType FeatureType { get; private set; }
        public String Name { get; private set; }

        public static FeatureType FeatureTypeFromString(String featureType)
        {
            for (FeatureType ft = (FeatureType)0; ft < FeatureType._TypeCount; ft++)
                if (ft.ToString() == featureType)
                    return ft;
            return FeatureType.Unknown;
        }

        public static MeasureType MeasureTypeFromFeatureType(FeatureType featureType)
        {
            if (featureType >= FeatureType.EnergyAC && featureType <= FeatureType.GridFeedInAC)
                return MeasureType.Energy;
            return MeasureType.Unknown;
        }

        public bool? IsAC
        {
            get
            {
                if (FeatureType == PVSettings.FeatureType.YieldAC || FeatureType == PVSettings.FeatureType.ConsumptionAC 
                    || FeatureType == PVSettings.FeatureType.EnergyAC || FeatureType == PVSettings.FeatureType.GridFeedInAC)
                    return true;
                if (FeatureType == PVSettings.FeatureType.YieldDC || FeatureType == PVSettings.FeatureType.EnergyDC)
                    return false;
                return null;
            }
        }
    }

    public class DeviceSettings : SettingsBase, INotifyPropertyChanged
    {
        private DeviceManagementSettings DeviceManagementSettings;
        private ObservableCollection<FeatureSettings> _FeatureList;
        private ObservableCollection<BlockSettings> _BlockList;
        private ObservableCollection<ActionSettings> _AlgorithmList;
        private ProtocolSettings _ProtocolSettings = null;

        public DeviceSettings(DeviceManagementSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            DeviceManagementSettings = root;
            LoadFeatures();
            LoadBlocks();
            LoadAlgorithms();
            _ProtocolSettings = root.GetProtocol(Protocol);
        }

        public FeatureSettings FindFeature(FeatureType featureType, uint featureId)
        {
            foreach (FeatureSettings fs in FeatureList)
                if (fs.FeatureType == featureType && fs.FeatureId == featureId)
                    return fs;
            return null;
        }

        public ObservableCollection<FeatureSettings> FeatureList { get { return _FeatureList; } }
        public ObservableCollection<BlockSettings> BlockList { get { return _BlockList; } }
        public ObservableCollection<ActionSettings> AlgorithmList { get { return _AlgorithmList; } }
        public ProtocolSettings ProtocolSettings { get { return _ProtocolSettings; } }

        public static DeviceType StringToDeviceType(string deviceType)
        {
            for (int i = 1; i < (int)DeviceType._TypeCount; i++ )
                if (((DeviceType)i).ToString() == deviceType)
                    return (DeviceType)i;

            return DeviceType.Unknown;
        }

        public static MeasureType StringToMeasureType(string measureType)
        {
            for (int i = 1; i < (int)MeasureType._TypeCount; i++)
                if (((MeasureType)i).ToString() == measureType)
                    return (MeasureType)i;

            return MeasureType.Unknown;
        }

        private void LoadFeatures()
        {
            _FeatureList = new ObservableCollection<FeatureSettings>();            
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "features")
                {
                    foreach (XmlNode eFeature in e.ChildNodes)
                    {
                        if (eFeature.NodeType == XmlNodeType.Element && eFeature.Name == "feature")
                        {
                            FeatureSettings feature = new FeatureSettings((XmlElement)eFeature);
                            _FeatureList.Add(feature);
                        }
                    }
                    break;
                }
            }
        }

        private void LoadBlocks()
        {
            _BlockList = new ObservableCollection<BlockSettings>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "block")
                {
                    BlockSettings command = new BlockSettings(DeviceManagementSettings, (XmlElement)e);
                    _BlockList.Add(command);
                }
            }
        }

        private void LoadAlgorithms()
        {
            _AlgorithmList = new ObservableCollection<ActionSettings>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "algorithm")
                {
                    ActionSettings algorithm = new ActionSettings(DeviceManagementSettings, (XmlElement)e);
                    _AlgorithmList.Add(algorithm);
                }
            }
        }

        public override string ToString()
        {
            return Id;
        }

        public FeatureSettings GetFeatureSettings(FeatureType featureType, uint featureId)
        { 
            foreach (FeatureSettings fs in _FeatureList)
                if (fs.FeatureType == featureType && fs.FeatureId == featureId)
                    return fs;
            return null;
        }

        public String Name
        {
            get
            {
                String val = settings.GetAttribute("name");
                if (val == null || val == "") // legacy style
                    val = GetValue("specification");
                return val;
            }
        }

        public String Id { get { return DeviceTypeName + ": " + Name + " - " + Version; } }

        public String Description 
        { 
            get 
            {
                String val = GetValue("description");
                string status = Status;
                if (val == "")
                    if (status == "")
                        return DeviceTypeName + ": " + Name + " - " + Version; 
                    else
                        return DeviceTypeName + ": " + Name + " - " + Version + " - " + status; 
                else
                    if (status == "")
                        return DeviceTypeName + ": " + val;
                    else
                        return DeviceTypeName + ": " + val + " - " + status; 
            } 
        }

        public struct DeviceName
        {
            public string Name;
            public string Id;
        }

        public List<DeviceName> Names
        {
            get
            {
                List<DeviceName> names = new List<DeviceName>();
                DeviceName baseName;
                baseName.Name = Description;
                baseName.Id = Id;
                names.Add(baseName);
                
                foreach (XmlNode e in settings.ChildNodes)
                {
                    if (e.NodeType == XmlNodeType.Element && e.Name == "synonym")
                    {
                        XmlAttribute valueAttrib = (XmlAttribute)e.Attributes.GetNamedItem("value");
                        XmlAttribute idAttrib = (XmlAttribute)e.Attributes.GetNamedItem("id");
                        if (valueAttrib != null && idAttrib != null)
                        {
                            DeviceName name;
                            
                            if (Status == "")
                                name.Name = DeviceTypeName + ": " + valueAttrib.Value;
                            else
                                name.Name = DeviceTypeName + ": " + valueAttrib.Value +" - " + Status; 
                            
                            name.Id = idAttrib.Value;
                            names.Add(name);
                        }
                    }
                }
                return names;
            }
        }

        public DeviceType DeviceType
        {
            get
            {
                return StringToDeviceType(GetValue("devicetype"));
            }
        }

        public MeasureType MeasureType
        {
            get
            {
                return StringToMeasureType(GetValue("measuretype"));
            }
        }

        public String Protocol
        {
            get
            {
                return GetValue("protocol");
            }
        }

        /*
        public String DeviceGroup 
        { 
            get 
            {
                String val = GetValue("groupname");
                if (val == "")
                    return Protocol;
                else
                    return val; 
            } 
        }
        */

        public struct GroupName
        {
            public string Name;
            public string Id;
        }

        public List<GroupName> DeviceGroups
        {
            get
            {
                List<GroupName> names = new List<GroupName>();                
                foreach (XmlNode e in settings.ChildNodes)
                {
                    if (e.NodeType == XmlNodeType.Element && e.Name == "groupname")
                    {
                        XmlAttribute valueAttrib = (XmlAttribute)e.Attributes.GetNamedItem("value");
                        XmlAttribute idAttrib = (XmlAttribute)e.Attributes.GetNamedItem("id");
                        
                        if (valueAttrib != null)
                        {
                            GroupName name;
                            name.Name = valueAttrib.Value;
                            if (idAttrib != null)
                                name.Id = idAttrib.Value;
                            else
                                name.Id = name.Name;
                            names.Add(name);
                        }
                    }
                }
                List<GroupName> protocolGroups = ProtocolSettings.DeviceGroups;
                foreach (GroupName g in protocolGroups)
                {
                    names.Add(g);
                }
                if (names.Count == 0)
                {
                    GroupName name;
                    name.Name = Protocol;
                    name.Id = name.Name;
                    names.Add(name);
                }
                return names;
            }
        }

        public bool? IsThreePhase
        {
            get
            {
                String val = GetValue("threephase");
                if (val == "true")
                    return true;
                if (val == "false")
                    return false;
                return null;
            }
        }

        public String DeviceTypeName
        {
            get
            {
                String val = GetValue("devicetype");
                return val;
            }
        }

        public String Status
        {
            get
            {
                String val = GetValue("status");
                return val;
            }
        }

        public String Version
        {
            get
            {
                String val = GetValue("version");
                return val;
            }
        }

        public bool HasStartOfDayEnergyDefect
        {
            get
            {
                String val = GetValue("hasstartofdayenergydefect");
                return val == "true";
            }
        }

        public int CrazyDayStartMinutes
        {
            get
            {
                String val = GetValue("crazydaystartminutes");
                if (val == "")
                    return 90;
                return Int32.Parse(val);
            }
        }

        public bool UseHistory
        {
            get
            {
                return GetValue("usehistory") == "true";
            }
        }

    }
}

