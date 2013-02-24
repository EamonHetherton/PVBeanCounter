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
        FeatureTypeCount    // This marker must be last
    }

    public class FeatureSettings
    {
        public FeatureSettings(XmlElement element)
        {
            String val = element.GetAttribute("type");
            if (val == "")
                Type = FeatureType.Unknown;
            else
                Type = FeatureTypeFromString(val);
            
            _Id = 0;
            val = element.GetAttribute("id");
            if (val != "")
            {
                uint.TryParse(val, out _Id);
            }
            val = element.GetAttribute("description");
            if (val == "")
                Name = Type.ToString() + "_" + _Id.ToString();
            else
                Name = val.Trim();

        }

        private uint _Id;
        public uint Id { get { return _Id; } private set { _Id = value; } }
        public FeatureType Type { get; private set; }
        public String Name { get; private set; }

        public static FeatureType FeatureTypeFromString(String featureType)
        {
            for (FeatureType ft = (FeatureType)0; ft < FeatureType.FeatureTypeCount; ft++)
                if (ft.ToString() == featureType)
                    return ft;
            return FeatureType.Unknown;
        }

        public static MeasureType MeasureTypeFromFeatureType(FeatureType featureType)
        {
            if (featureType >= FeatureType.EnergyAC && featureType <= FeatureType.ConsumptionAC)
                return MeasureType.Energy;
            return MeasureType.Unknown;
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
                if (fs.Type == featureType && fs.Id == featureId)
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
                if (fs.Type == featureType && fs.Id == featureId)
                    return fs;
            return null;
        }

        public String Id { get { return DeviceTypeName + ": " + Specification + " - " + Version; } }

        public String Description 
        { 
            get 
            {
                String val = GetValue("description");
                string status = Status;
                if (val == "")
                    if (status == "")
                        return DeviceTypeName + ": " + Specification + " - " + Version; 
                    else
                        return DeviceTypeName + ": " + Specification + " - " + Version + " - " + status; 
                else
                    if (status == "")
                        return DeviceTypeName + ": " + val;
                    else
                        return DeviceTypeName + ": " + val + " - " + status; 
            } 
        }

        public List<String> Names
        {
            get
            {
                List<String> names = new List<String>();
                names.Add(Description);
                
                foreach (XmlNode e in settings.ChildNodes)
                {
                    if (e.NodeType == XmlNodeType.Element && e.Name == "synonym")
                    {
                        XmlAttribute attrib = (XmlAttribute)e.Attributes.GetNamedItem("value");
                        if (attrib != null)
                            names.Add(DeviceTypeName + ": " + attrib.Value);
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

        public String Manager
        {
            get
            {
                String val = GetValue("manager");
                return val;
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

        public String Specification
        {
            get
            {
                String val = GetValue("specification");
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

        public String Endian32Bit
        {
            get
            {
                String val = GetValue("endian32bit");
                if (val == "")
                    return "Big";
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



    }
}

