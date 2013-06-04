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
using System.Text;
using System.Xml;
using System.ComponentModel;
using System.Reflection;
using System.IO;

namespace PVSettings
{
    public class DeviceListItem
    {
        public String Id { get; set; }
        public String Description { get; set; }
        public DeviceSettings DeviceSettings;
    }

    public class DeviceGroup
    {
        public String Name { get; set; }
        public String Description { get; set; }
        public String Protocol { get; set; }
        public ObservableCollection<DeviceListItem> DeviceList { get; set; }
    }

    public class DeviceManagementSettings : SettingsBase, INotifyPropertyChanged
    {
        private String SettingsDirectory;
        private ObservableCollection<DeviceListItem> _DeviceList = null;
        private ObservableCollection<ProtocolSettings> _ProtocolList = null;
        private ObservableCollection<DeviceGroup> _DeviceGroupList = null;
        private ObservableCollection<String> _IntervalList = null;

        public DeviceManagementSettings()
        {
            SettingsDirectory = AppDomain.CurrentDomain.BaseDirectory;

            BuildIntervalList();
            LoadSettings();
        }

        private void BuildIntervalList()
        {
            _IntervalList = new ObservableCollection<String>();
            _IntervalList.Add("");
            _IntervalList.Add("6");
            _IntervalList.Add("10");
            _IntervalList.Add("12");
            _IntervalList.Add("15");
            _IntervalList.Add("20");
            _IntervalList.Add("30");
            _IntervalList.Add("60");
            _IntervalList.Add("120");
            _IntervalList.Add("180");
            _IntervalList.Add("240");
            _IntervalList.Add("300");
            _IntervalList.Add("600");
            _IntervalList.Add("900");
            _IntervalList.Add("1800");
            _IntervalList.Add("3600");
        }

        private void LoadSettings()
        {
            _DeviceList = new ObservableCollection<DeviceListItem>();
            _ProtocolList = new ObservableCollection<ProtocolSettings>();
            _DeviceGroupList = new ObservableCollection<DeviceGroup>();
            
            String mainName = Path.Combine(SettingsDirectory, "DeviceManagement_v02.xml");

            foreach (String fileName in System.IO.Directory.EnumerateFiles(SettingsDirectory, "*Device*.xml"))
            {
                XmlReader reader;

                // Create the validating reader and specify DTD validation.
                XmlReaderSettings readerSettings = new XmlReaderSettings();
                readerSettings.DtdProcessing = DtdProcessing.Parse;
                readerSettings.ValidationType = ValidationType.None;

                reader = XmlReader.Create(fileName, readerSettings);

                // Pass the validating reader to the XML document.
                // Validation fails due to an undefined attribute, but the 
                // data is still loaded into the document.

                XmlDocument newDocument = new XmlDocument();
                //settings = new XmlDocument();
                newDocument.Load(reader);
                reader.Close();

                bool isMainSettings = false;
                String targetNode;
                if (fileName == mainName)
                {
                    document = newDocument;
                    isMainSettings = true;
                    targetNode = "devicemanagement";
                }
                else
                    targetNode = "devices";

                bool found = false;

                PVSettings.SettingsBase fileSettings = null;

                foreach (XmlNode n in newDocument.ChildNodes)
                    if (n.NodeType == XmlNodeType.Element && n.Name == targetNode)
                    {
                        if (isMainSettings)
                        {
                            settings = (XmlElement)n;
                        }
                        else
                        {
                            fileSettings = new PVSettings.SettingsBase();
                            fileSettings.SetDocument(newDocument);
                            fileSettings.settings = (XmlElement)n;
                            fileSettings.SetupBaseAfterDocument();
                        }
                        found = true;
                        break;
                    }

                if (found)
                {
                    LoadProtocolsAndDevices(isMainSettings ? this : fileSettings);
                }
                else
                    throw new Exception("LoadSettings - Cannot find element '" + targetNode + "'");
            }
            LoadProtocolDevices();
        }

        public DeviceSettings GetDevice(String id)
        {
            foreach (DeviceListItem device in DeviceList)
            {
                if (device.Id == id)
                    return device.DeviceSettings;
            }
            return null;
        }

        public DeviceSettings GetDeviceByDescription(String description)
        {
            foreach (DeviceListItem device in DeviceList)
            {
                foreach(DeviceSettings.DeviceName name in device.DeviceSettings.Names)
                    if (name.Name == description)
                        return device.DeviceSettings;
            }
            return GetDevice(description);
        }

        private void LoadProtocolsAndDevices(PVSettings.SettingsBase fileSettings)
        {
            foreach (XmlNode e in fileSettings.settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "protocol")
                {
                    ProtocolSettings protocol = new ProtocolSettings(this, (XmlElement)e);
                    _ProtocolList.Add(protocol);
                }
                else if (e.NodeType == XmlNodeType.Element && e.Name == "device")
                {
                    DeviceSettings device = new DeviceSettings(this, (XmlElement)e);
                    foreach (DeviceSettings.DeviceName name in device.Names)
                    {
                        DeviceListItem item = new DeviceListItem();
                        item.Id = name.Id;
                        item.Description = name.Name;
                        item.DeviceSettings = device;
                        _DeviceList.Add(item);

                        DeviceGroup deviceGroup = FindOrCreateDeviceGroup(item.DeviceSettings.DeviceGroup, item.DeviceSettings.Protocol);
                        deviceGroup.DeviceList.Add(item);
                    }
                }
            }
        }

        private void LoadProtocolDevices()
        {
            int top = DeviceGroupList.Count;
            for (int i = 0; i < top; i++)
            {
                DeviceGroup g = DeviceGroupList[i];
                foreach (DeviceListItem item in g.DeviceList)
                {
                    if (g.Protocol != g.Name) // detect groups that are not just inherited protocol names
                    {
                        DeviceGroup deviceGroup = FindOrCreateDeviceGroup(g.Protocol, g.Protocol);
                        deviceGroup.Description = "Protocol: " + g.Protocol;
                        deviceGroup.DeviceList.Add(item);
                    }
                }
            }
        }

        private DeviceGroup FindOrCreateDeviceGroup(String groupName, String protocolName)
        {
            foreach (DeviceGroup g in _DeviceGroupList)
                if (g.Name == groupName)
                    return g;
 
            DeviceGroup group = new DeviceGroup();
            group.Name = groupName;
            group.Protocol = protocolName;
            group.Description = groupName;
            group.DeviceList = new ObservableCollection<DeviceListItem>();
            _DeviceGroupList.Add(group);
            return group;
        }

        public DeviceGroup GetDeviceGroup(String groupName)
        {
            foreach (DeviceGroup g in _DeviceGroupList)
                if (g.Name == groupName)
                    return g;
            
            return null;
        }

        public ProtocolSettings GetProtocol(String protocolName)
        {
            foreach (ProtocolSettings p in _ProtocolList)
                if (p.Name == protocolName)
                    return p;
            return null;
        }

        public ObservableCollection<DeviceListItem> GetProtocolDeviceList(string protocol)
        {
            ObservableCollection<DeviceListItem> list = new ObservableCollection<DeviceListItem>();
           
            foreach (DeviceListItem i in _DeviceList)
                if (i.DeviceSettings.ProtocolSettings.Name == protocol || protocol == "")
                    list.Add(i);

            return list;
        }

        public ObservableCollection<DeviceListItem> GetGroupNameDeviceList(string groupName)
        {
            foreach (DeviceGroup g in _DeviceGroupList)
                if (g.Name == groupName)
                    return g.DeviceList;
            
            return null;
        }

        public ObservableCollection<DeviceListItem> DeviceList { get { return _DeviceList; } }

        public ObservableCollection<DeviceGroup> DeviceGroupList { get { return _DeviceGroupList; } }

        public ObservableCollection<ProtocolSettings> ProtocolList { get { return _ProtocolList; } }

        public ObservableCollection<String> IntervalList { get { return _IntervalList; } }

    }
}

