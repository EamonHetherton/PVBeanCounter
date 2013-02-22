/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using MackayFisher.Utilities;

namespace PVSettings
{

    public class ApplicationSettings : ApplicationSettingsBase
    {
        //  0 - Standard Name
        //  1 - Database Type
        //  2 - Provider Type
        //  3 - Provider Name
        //  4 - OleDB Name
        //  5 - Database Name
        //  6 - Database Username
        //  7 - Host Name

        public static String[,] StandardDBMatrix = 
            { { "SQLite", "SQLite","Proprietary", "System.Data.SQLite", "", "pvhistory.s3db", "", "" }
            , { "Jet (2007)", "Jet", "OleDb", "System.Data.OleDb", "Microsoft.ACE.OLEDB.12.0", "PVRecords_jet.accdb", "", "" }
            , { "Jet (2003)", "Jet", "OleDb", "System.Data.OleDb", "Microsoft.Jet.OLEDB.4.0", "PVRecords_jet.mdb", "", "" }
            , { "SQL Server", "SQL Server", "Proprietary", "System.Data.SqlClient", "", "PVHistory", "", @"localhost\SQLEXPRESS" }
            , { "MySQL", "MySql", "Proprietary", "MySql.Data.MySQLClient", "", "pvhistory", "PVRecords", "localhost" } };

        //private ObservableCollection<InverterManagerSettings> _InverterManagerList;
        private ObservableCollection<DeviceManagerSettings> _DeviceManagerList;
        //private ObservableCollection<MeterManagerSettings> _MeterManagerList;
        private ObservableCollection<PvOutputSiteSettings> _PvOutputSystemList;
        private ObservableCollection<EnergyEventSettings> _EnergyEventList;
        private ObservableCollection<DeviceManagerDeviceSettings> _AllDevicesList;
        private ObservableCollection<DeviceManagerDeviceSettings> _AllConsolidationDevicesList;
        
        protected SystemServices SystemServices;

        public DeviceManagementSettings DeviceManagementSettings;

        public bool LoadingEnergyEvents { get; private set; }

        public InverterManagerSettings DeviceInverterManagerSettings { get; private set; }

        public ApplicationSettings(String SettingsFileName)
            : base(SettingsFileName, "configuration", @"\settings_template_SE_SQLite.xml")
        {
            _EnergyEventList = new ObservableCollection<EnergyEventSettings>();
            DeviceManagementSettings = new DeviceManagementSettings();
            
            SystemServices = null;
            DeviceInverterManagerSettings = null;
            LoadSettingsSub();
            //KnownServiceAccountName = "";
            ServiceAccountPassword = "";
            ServiceDetailsChanged = false;
            LoadingEnergyEvents = false;
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
        }

        public PvOutputSiteSettings FindPVOutputBySystemId(String systemId)
        {
            foreach (PvOutputSiteSettings settings in _PvOutputSystemList)
                if (settings.SystemId == systemId)
                    return settings;
            return null;
        }

        public void SetSystemServices(SystemServices systemServices, bool loadEnergyEvents = true)
        {
            bool oldChangedState = ServiceDetailsChanged;
            SystemServices = systemServices;
            if (loadEnergyEvents)
                LoadEnergyEvents(true);
            ServiceDetailsChanged = oldChangedState;
        }

        private void RemoveOldElements()
        {
            DeleteElement("pvforceliveupload");
            DeleteElement("defaultmanagertype");
            DeleteElement("pvoutputbatch");
            DeleteElement("metertolerance");
            DeleteElement("resetfirstfullday");
            DeleteElement("usecctemperature");
            DeleteElement("appliancelist");
            DeleteElement("meterportname");
            DeleteElement("consumptionmeterhisthours");
            DeleteElement("meterbaudrate");
            DeleteElement("pvoutputsiteid");
            DeleteElement("pvoutputapikey");
            DeleteElement("pvoutputapiversion");
            DeleteElement("enablepvoutput");
            DeleteElement("pvoutputdatainterval");
            DeleteElement("pvoutputbackload");
            DeleteElement("pvlivedays");
            DeleteElement("pvoutputdaylist");
            DeleteElement("sunnyexplorerpath");
            DeleteElement("sunnyexplorerplantname");
            DeleteElement("energyeventlist");
            DeleteElement("logfile");
            DeleteElement("logmetermessage");
            DeleteElement("monitorinverters");
            DeleteElement("monitormeters");
            DeleteElement("metermanagerlist");
        }

        internal String GetApplianceListXml()
        {
            XmlElement elem = GetElement("appliancelist");
            if (elem == null)
                return "";

            return elem.InnerXml;
        }

        /*
        private void LoadMeterManagers()
        {
            _MeterManagerList = new ObservableCollection<MeterManagerSettings>();
            CCMeterManagerSettings ccMeterManagerSettings = null;
            OwlMeterManagerSettings owlMeterManagerSettings = null;
            EW4009MeterManagerSettings ew4009MeterManagerSettings = null;
            int count = 0;

            XmlElement managers = GetElement("metermanagerlist");
            if (managers == null)
                managers = AddElement(settings, "metermanagerlist");

            foreach (XmlNode e in managers.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "metermanager")
                {
                    foreach (XmlElement e2 in e.ChildNodes)
                    {
                        if (e2.Name == "managertype")
                        {
                            if (e2.Attributes["value"].Value == MeterManagerSettings.MeterManagerTypes[(int)MeterManagerSettings.MeterManagerType.CurrentCost].Name)
                            {
                                ccMeterManagerSettings = new CCMeterManagerSettings(this, (XmlElement)e);
                                _MeterManagerList.Add(ccMeterManagerSettings);
                                count++;
                            }
                            else if (e2.Attributes["value"].Value == MeterManagerSettings.MeterManagerTypes[(int)MeterManagerSettings.MeterManagerType.Owl].Name)
                            {
                                owlMeterManagerSettings = new OwlMeterManagerSettings(this, (XmlElement)e);
                                _MeterManagerList.Add(owlMeterManagerSettings);
                                count++;
                            }
                            else if (e2.Attributes["value"].Value == MeterManagerSettings.MeterManagerTypes[(int)MeterManagerSettings.MeterManagerType.EW4009].Name)
                            {
                                ew4009MeterManagerSettings = new EW4009MeterManagerSettings(this, (XmlElement)e);
                                _MeterManagerList.Add(ew4009MeterManagerSettings);
                                count++;
                            }

                            break;
                        }
                    }
                }
            }

            if (ccMeterManagerSettings == null)
            {
                XmlElement e = AddElement(managers, "metermanager");
                CCMeterManagerSettings manager = new CCMeterManagerSettings(this, e);
                manager.InstanceNo = 1;

                manager.ManagerType = MeterManagerSettings.MeterManagerTypes[(int)manager.ManagerTypeInternal].Name;
                manager.Enabled = (count == 0);
                manager.PortName = GetValue("meterportname");
                manager.BaudRate = GetValue("meterbaudrate");
                String histHours = GetValue("consumptionmeterhisthours").Trim();
                if (histHours != "")
                    manager.ConsumptionMeterHistHours = Convert.ToInt32(histHours);

                _MeterManagerList.Add(manager);
                count++;
            }

            if (owlMeterManagerSettings == null)
            {
                XmlElement e = AddElement(managers, "metermanager");
                OwlMeterManagerSettings manager = new OwlMeterManagerSettings(this, e);
                manager.InstanceNo = 1;

                manager.ManagerType = MeterManagerSettings.MeterManagerTypes[(int)manager.ManagerTypeInternal].Name;
                manager.Enabled = false;

                _MeterManagerList.Add(manager);
                count++;
            }

            if (ew4009MeterManagerSettings == null)
            {
                XmlElement e = AddElement(managers, "metermanager");
                EW4009MeterManagerSettings manager = new EW4009MeterManagerSettings(this, e);
                manager.InstanceNo = 1;

                manager.ManagerType = MeterManagerSettings.MeterManagerTypes[(int)manager.ManagerTypeInternal].Name;
                manager.Enabled = false;

                _MeterManagerList.Add(manager);
                count++;
            }       
        }
        */

        public static void  RemoveExtraElements(XmlElement element, string name, int limit)
        {
             int count = 0;

             for (int i = 0; i < element.ChildNodes.Count; i++)
             {
                 XmlNode elem = element.ChildNodes[i];
                 if (elem.GetType() == typeof(XmlElement))
                     if (elem.Name == name)
                     {
                         count++;
                         if (count > limit)
                         {
                             element.RemoveChild(elem);
                             i--;
                         }
                     }
             }
        }

        public DeviceManagerDeviceSettings GetDeviceByName(string deviceName)
        {
            foreach (DeviceManagerSettings devMgr in _DeviceManagerList)
            {
                foreach (DeviceManagerDeviceSettings device in devMgr.DeviceList)
                    if (deviceName == device.Name)
                        return device;
            }
            return null;
        }

        private void LoadDeviceManagers()
        {
            ObservableCollection<DeviceManagerSettings> deviceManagerList = new ObservableCollection<DeviceManagerSettings>();

            XmlElement managers = GetElement("devicemanagerlist");
            if (managers == null)
            {
                managers = GetElement("modbusmanagerlist");  // locate node to be replaced
                if (managers != null)
                {
                    XmlElement managersNew = AddElement(settings, "devicemanagerlist"); // create replacement
                    while(managers.HasChildNodes)  // locate old device manager nodes
                    {
                        XmlElement managerNew = AddElement(managersNew, "devicemanager"); // create new device manager node
                        XmlElement manager = (XmlElement)managers.FirstChild; // select old device manager
                        while (manager.HasChildNodes)
                            managerNew.AppendChild(manager.FirstChild);  // reparent old manager children to new manager
                        managers.RemoveChild(manager); // remove old device manager from old managers list
                    }
                    settings.RemoveChild(managers); // remove old device managers list
                    managers = managersNew; // install new device managers list
                }
            }
            if (managers == null)
                managers = AddElement(settings, "devicemanagerlist");
            else
                foreach(XmlElement manager in managers.ChildNodes)
                    RemoveExtraElements(manager, "serialport", 1);

            foreach (XmlNode e in managers.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "devicemanager")
                {
                    DeviceManagerSettings manager = new DeviceManagerSettings(this, (XmlElement)e);
                    deviceManagerList.Add(manager);
                }
            }

            IEnumerable<DeviceManagerSettings> sorted = deviceManagerList.OrderBy(mms => ((mms.Enabled ? "0" : "1") + mms.Description));

            _DeviceManagerList = new ObservableCollection<DeviceManagerSettings>();
            foreach (DeviceManagerSettings im in sorted)
                _DeviceManagerList.Add(im);

            _AllDevicesList = new ObservableCollection<DeviceManagerDeviceSettings>();
            _AllConsolidationDevicesList = new ObservableCollection<DeviceManagerDeviceSettings>();
            RefreshAllDevices();
            // consolidations must be registered after all devices from all managers are in the list
            foreach (DeviceManagerDeviceSettings dev in _AllDevicesList)
                dev.RegisterConsolidations();
        }

        public void RefreshAllDevices()
        {            
            DeviceEnumerator deviceEnumerator = new DeviceEnumerator(_DeviceManagerList);
            deviceEnumerator.Reset();
            _AllDevicesList.Clear();
            _AllConsolidationDevicesList.Clear();
            while (deviceEnumerator.MoveNext())
            {
                _AllDevicesList.Add(deviceEnumerator.Current);
                DeviceSettings ds = deviceEnumerator.Current.DeviceSettings;
                if (ds != null && ds.DeviceType == DeviceType.Consolidation)
                    _AllConsolidationDevicesList.Add(deviceEnumerator.Current);
            }
        }

        /*
        private void LoadInverterManagers()
        {
            ObservableCollection<InverterManagerSettings> inverterManagerList = new ObservableCollection<InverterManagerSettings>();
            
            XmlElement managers = GetElement("invertermanagerlist");
            if (managers == null)
                managers = AddElement(settings, "invertermanagerlist");

            foreach (XmlNode e in managers.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "invertermanager")
                {
                    InverterManagerSettings manager = new InverterManagerSettings(this, (XmlElement)e);

                    if (manager.ManagerType == InverterManagerSettings.InverterManagerType.SMABluetooth)
                        continue;
                    if (manager.ManagerType == InverterManagerSettings.InverterManagerType.Devices)
                        DeviceInverterManagerSettings = manager;

                    inverterManagerList.Add(manager);
                }
            }

            foreach (InverterManagerSettings.InverterManagerInfo info in InverterManagerSettings.InverterManagerTypes)
            {
                if (!info.IsLoaded && info.Type != InverterManagerSettings.InverterManagerType.SMABluetooth)
                {
                    XmlElement e = AddElement(managers, "invertermanager");
                    InverterManagerSettings manager = new InverterManagerSettings(this, e, info.Name);
                    manager.InstanceNo = 1;
                    manager.Enabled = false;
                    inverterManagerList.Add(manager);
                    if (manager.ManagerType == InverterManagerSettings.InverterManagerType.Devices)
                        DeviceInverterManagerSettings = manager;
                }
            }

            IEnumerable<InverterManagerSettings> sorted = inverterManagerList.OrderBy(ims => ((ims.Enabled ? "0" : "1") + ims.Description));

            _InverterManagerList = new ObservableCollection<InverterManagerSettings>();
            foreach (InverterManagerSettings im in sorted)
                _InverterManagerList.Add(im);
        }
        */

        /*
        private int GetNextInverterManagerInstanceNo(String managerType)
        {
            int maxId = 1;
            foreach (InverterManagerSettings manager in InverterManagerList)
            {
                if (manager.ManagerTypeName == managerType && manager.InstanceNo > maxId)
                    maxId = manager.InstanceNo;
            }

            return maxId + 1;
        }
        */

        /*
        public InverterManagerSettings AddInverterManager(String managerType)
        {
            XmlElement managers = GetElement("invertermanagerlist");
            if (managers == null)
                managers = AddElement(settings, "invertermanagerlist");

            XmlElement e = AddElement(managers, "invertermanager");
            InverterManagerSettings manager = new InverterManagerSettings(this, e, managerType);
            XmlElement e2 = AddElement(e, "serialport");
            manager.SerialPort = new SerialPortSettings(this, e2);

            manager.Enabled = false;
            manager.InstanceNo = GetNextInverterManagerInstanceNo(managerType);

            InverterManagerList.Add(manager);

            return manager;
        }
        */

        public DeviceManagerSettings AddDeviceManager()
        {
            XmlElement managers = GetElement("devicemanagerlist");
            if (managers == null)
                managers = AddElement(settings, "devicemanagerlist");

            XmlElement e = AddElement(managers, "devicemanager");
            DeviceManagerSettings manager = new DeviceManagerSettings(this, e);
            XmlElement e2 = AddElement(e, "serialport");
            manager.SerialPort = new SerialPortSettings(this, e2);
            manager.Enabled = false;

            DeviceManagerList.Add(manager);

            return manager;
        }

        public void DeleteDeviceManager(DeviceManagerSettings manager)
        {
            XmlElement managers = GetElement("devicemanagerlist");
            if (managers == null)
                return;

            DeviceManagerList.Remove(manager);

            foreach (XmlNode child in managers.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element && child.Name == "devicemanager")
                {
                    if (ElementHasChild(child, "name", manager.Name))
                    {
                        managers.RemoveChild(child);
                        SettingChangedEventHandler("");
                        return;
                    }
                }
            }
        }


        public void DeleteInverterManager(InverterManagerSettings manager)
        {
            // Delete default instances is not allowed
            if (manager.InstanceNo == 1)
                return;

            XmlElement managers = GetElement("invertermanagerlist");
            if (managers == null)
                return;

            //InverterManagerList.Remove(manager);

            foreach (XmlNode child in managers.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element && child.Name == "invertermanager")
                {
                    if (ElementHasChild(child, "managertype", manager.ManagerTypeName)
                        && ElementHasChild(child, "instanceno", manager.InstanceNo.ToString()))                    
                    {
                        managers.RemoveChild(child);
                        SettingChangedEventHandler("");
                        return;                                        
                    }          
                }
            }
        }

        public EnergyEventSettings FindEnergyEventSettings(HierarchyType hierarchy, 
            String managerName, String componentId, String deviceName, bool autoCreate = false)
        {
            foreach (EnergyEventSettings evnt in _EnergyEventList)
            {
                if (evnt.Hierarchy == hierarchy
                && evnt.ManagerName == managerName
                && evnt.Component == componentId
                && evnt.DeviceName == deviceName)
                {
                    evnt.IsCurrentEvent = true;
                    return evnt;
                }
            }

            if (!autoCreate)
                return null;

            XmlElement events = GetElement("energyevents");
            XmlElement e = AddElement(events, "event");
            EnergyEventSettings newEvnt = new EnergyEventSettings(this, e);
           
            newEvnt.Hierarchy = hierarchy;
            newEvnt.ManagerName = managerName;
            newEvnt.Component = componentId;
            newEvnt.DeviceName = deviceName;
            newEvnt.Interval = null;
            newEvnt.IsCurrentEvent = true;

            _EnergyEventList.Add(newEvnt);

            return newEvnt;
        }

        private void RollUpInterval(HierarchyType type, EnergyEventSettings evnt, int interval)
        {
            if (interval <= 0)
                return;

            if (evnt.Interval == null || evnt.Interval > interval)
            {
                evnt.Interval = interval;
                EnergyEventSettings parentEvnt = null;
                if (evnt.DeviceName != "")
                    parentEvnt = FindEnergyEventSettings(type, evnt.ManagerName, evnt.Component, "");
                else if (evnt.Component != "")
                    parentEvnt = FindEnergyEventSettings(type, evnt.ManagerName, "", "");
                else if (evnt.ManagerName != "")
                    parentEvnt = FindEnergyEventSettings(type, "", "", "");
                else
                {
                    // is a root node
                    if (UseDefaultEvents && (type == HierarchyType.Yield || type == HierarchyType.Consumption))
                        evnt.EmitEvent = true;
                }
                    
                if (parentEvnt != null)
                    RollUpInterval(type, parentEvnt, interval);
            }
        }

        private void BuildStandardEvents()
        {
            bool useDefaultEvents = UseDefaultEvents;

            EnergyEventSettings rootYield;
            rootYield = FindEnergyEventSettings(HierarchyType.Yield, "", "", "", true); // System wide total yield event
            if (useDefaultEvents)
                rootYield.FeedInYield = true;
            /*
            foreach(InverterManagerSettings imSettings in InverterManagerList)
                if (imSettings.Enabled || !useDefaultEvents)
                {
                    EnergyEventSettings evnt;
                    evnt = FindEnergyEventSettings(HierarchyType.Yield, imSettings.Description, "", "", true); // Inverter Manager specific yield events
                    if (imSettings.ManagerTypeName == "Meter")
                    {
                        foreach (MeterManagerSettings mmSettings in MeterManagerList)
                        {
                            foreach (MeterApplianceSettings appl in mmSettings.ApplianceList)
                            {
                                if (appl.IsInverterYield)
                                {
                                    evnt = FindEnergyEventSettings(HierarchyType.Yield,
                                        imSettings.Description, appl.Inverter, "", true); // inverter summary node
                                    evnt = FindEnergyEventSettings(HierarchyType.Yield,
                                        imSettings.Description, appl.Inverter, appl.ApplianceNo.ToString(), true); // Meter appliance specific yield events
                                    RollUpInterval(HierarchyType.Yield, evnt, imSettings.SampleFrequency);
                                }
                            }
                        }
                    }
                    else
                    {
                        TestDatabase Database = new TestDatabase(this, SystemServices);
                        List<InverterInfo> inverterList = Database.GetInverterList(imSettings.ManagerTypeName, imSettings.InstanceNo);
                        foreach (InverterInfo info in inverterList)
                        {
                            evnt = FindEnergyEventSettings(HierarchyType.Yield, imSettings.Description, info.SerialNumber, "", true); // Inverter yield events
                            RollUpInterval(HierarchyType.Yield, evnt, imSettings.SampleFrequency);
                        }
                    }
                }
            */
            EnergyEventSettings rootConsumption;
            rootConsumption = FindEnergyEventSettings(HierarchyType.Consumption, "", "", "", true); // System wide total consumption event
            if (useDefaultEvents)
                rootConsumption.FeedInConsumption = true;
            /*
            foreach (MeterManagerSettings mmSettings in MeterManagerList)
                if (mmSettings.Enabled || !useDefaultEvents)
                {
                    EnergyEventSettings evnt;
                    evnt = FindEnergyEventSettings(HierarchyType.Consumption, "Meter", "", "", true); // Meter Manager specific consumption events
                    evnt = FindEnergyEventSettings(HierarchyType.Consumption, "Meter", 
                        mmSettings.StandardName, "", true); // Meter Manager specific consumption events
                    foreach (MeterApplianceSettings appl in mmSettings.ApplianceList)
                    {
                        if (appl.IsConsumption)
                        {
                            evnt = FindEnergyEventSettings(HierarchyType.Consumption, "Meter", 
                                mmSettings.StandardName, appl.ApplianceNo.ToString(), true); // Meter appliance specific consumption events
                            RollUpInterval(HierarchyType.Consumption, evnt, mmSettings.SampleFrequency);
                        }
                    }
                }
            */
            EnergyEventSettings rootUntyped;
            rootUntyped = FindEnergyEventSettings(HierarchyType.Meter, "", "", "", true); // System wide total untyped meter event

            EnergyEventSettings mEvent = FindEnergyEventSettings(HierarchyType.Meter, "Meter", "", "", true); // System wide total untyped meter event
            /*
            foreach (MeterManagerSettings mmSettings in MeterManagerList)
                if (mmSettings.Enabled || !useDefaultEvents)
                {
                    mEvent = FindEnergyEventSettings(HierarchyType.Meter, "Meter", 
                        mmSettings.StandardName, "", true); // Meter Manager specific untyped events
                    foreach (MeterApplianceSettings appl in mmSettings.ApplianceList)
                    {
                        if (appl.StoreReading)
                        {
                            mEvent = FindEnergyEventSettings(HierarchyType.Meter, "Meter", mmSettings.StandardName, 
                                appl.ApplianceNo.ToString(), true); // Meter appliance specific untyped events
                            mEvent.Inverter = appl.Inverter;
                            mEvent.ConsumeSystem = appl.ConsumptionSiteId;
                            RollUpInterval(HierarchyType.Meter, mEvent, mmSettings.SampleFrequency);
                            // emit standard event if meter is not Yield and not Consumption
                            if (useDefaultEvents)
                            {
                                mEvent.EmitEvent = (!appl.IsInverterYield && !appl.IsConsumption);
                                if (mEvent.EmitEvent)
                                    mEvent.EventType = "Generic";
                            }
                        }
                    }
                }
             * */
        }

        private struct EnergyEventUserInfo
        {
            public EnergyEventKey Key;
            public bool EmitEvent;
            public String Description;
            public bool FeedInYield;
            public bool FeedInConsumption;
            public String Type;
        }

        public void ClearYieldFeedin()
        {
            foreach (EnergyEventSettings evnt in _EnergyEventList)
            {
                evnt.FeedInYield = false;
            }
        }

        public void ClearConsumptionFeedin()
        {
            foreach (EnergyEventSettings evnt in _EnergyEventList)
            {
                evnt.FeedInConsumption = false;
            }
        }

        public void LoadEnergyEvents(bool initialLoad = false)
        {
            LoadingEnergyEvents = initialLoad;
            XmlElement events = GetElement("energyevents");
            if (events == null)
                events = AddElement(settings, "energyevents");

            foreach (XmlNode e in events.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "event")
                {
                    EnergyEventSettings eventx = new EnergyEventSettings(this, (XmlElement)e);
                    _EnergyEventList.Add(eventx);
                    eventx.RemoveOldNodes();
                }
            }

            // Store user supplied event details
            List<EnergyEventUserInfo> userInfoList = new List<EnergyEventUserInfo>();
            foreach (EnergyEventSettings evnt in _EnergyEventList)
            {
                EnergyEventUserInfo info;
                
                info.Key.Hierarchy = evnt.Hierarchy;
                info.Key.ManagerName = evnt.ManagerName;
                info.Key.Component = evnt.Component;
                info.Key.DeviceName = evnt.DeviceName;
                info.Description = evnt.Description;
                info.EmitEvent = evnt.EmitEvent;
                info.FeedInConsumption = evnt.FeedInConsumption;
                info.FeedInYield = evnt.FeedInYield;
                info.Type = evnt.EventType;
                userInfoList.Add(info);
            }

            DeleteElement("energyevents");
            _EnergyEventList.Clear();

            events = AddElement(settings, "energyevents");

            BuildStandardEvents();

            if (!UseDefaultEvents)
            {
                // restore user supplied info
                foreach (EnergyEventUserInfo info in userInfoList)
                {
                    EnergyEventSettings evnt = FindEnergyEventSettings(info.Key.Hierarchy, info.Key.ManagerName, info.Key.Component, info.Key.DeviceName);
                    if (evnt != null)
                    {
                        evnt.Description = info.Description;
                        evnt.EventType = info.Type;                       
                        evnt.EmitEvent = info.EmitEvent;
                        evnt.FeedInYield = info.FeedInYield;
                        evnt.FeedInConsumption = info.FeedInConsumption;            
                    }
                }
            }
            LoadingEnergyEvents = false;
        }

        private void LoadPVOutputSites()
        {
            _PvOutputSystemList = new ObservableCollection<PvOutputSiteSettings>();

            XmlElement sites = GetElement("pvoutputsitelist");
            if (sites == null)
                sites = AddElement(settings, "pvoutputsitelist");

            foreach (XmlNode e in sites.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "site")
                {
                    PvOutputSiteSettings site = new PvOutputSiteSettings(this, (XmlElement)e);
                    _PvOutputSystemList.Add(site);
                }
            }

            if (_PvOutputSystemList.Count == 0)
            {
                XmlElement e = AddElement(sites, "site");
                PvOutputSiteSettings site = new PvOutputSiteSettings(this, e);
                site.APIKey = PVOutputAPIKey;
                site.APIVersion = "";
                site.SystemId = PVOutputSiteId;
                site.LiveDays = 2;
                site.AutoBackload = true;
                site.DataInterval = PVOutputDataInterval;
                site.Enable = EnablePVOutput;
                site.UseCCTemperature = UseCCTemperatureOld;

                _PvOutputSystemList.Add(site);
            }
        }

        public void AddPvOutputSite()
        {
            XmlElement sites = GetElement("pvoutputsitelist");

            XmlElement e = AddElement(sites, "site");
            PvOutputSiteSettings site = new PvOutputSiteSettings(this, e);
            site.Name = "New System";
            site.SystemId = "0";

            _PvOutputSystemList.Add(site);
        }

        public void DeletePvOutputSite(PvOutputSiteSettings site)
        {
            XmlElement sites = GetElement("pvoutputsitelist");
            if (sites == null)
                return;

            int index = _PvOutputSystemList.IndexOf(site);
            _PvOutputSystemList.Remove(site);

            int pos = 0;
            foreach (XmlNode child in sites.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element && child.Name == "site")
                {
                    if (ElementHasChild(child, "siteid", site.SystemId) && (pos == index))
                    {
                        sites.RemoveChild(child);
                        SettingChangedEventHandler("");
                        return;
                    }
                    pos++;
                }
            }           
        }

        protected override void LoadSettingsSub()
        {
            base.LoadSettingsSub();

            SettingsDirectory = DefaultDirectory;

            LoadDeviceManagers();
            //LoadMeterManagers();
            //LoadInverterManagers();
            LoadPVOutputSites();
            //LoadEnergyEvents(); // requires DB access only works after services set

            RemoveOldElements();
        }

        public String StandardDBType
        {
            get
            {
                for (int i = 0; i <= StandardDBMatrix.GetUpperBound(0); i++)
                {
                    if ((DatabaseType == StandardDBMatrix[i, 1])
                    && (ProviderType == StandardDBMatrix[i, 2])
                    && (ProviderName == StandardDBMatrix[i, 3])
                    && (OleDbName == StandardDBMatrix[i, 4])
                    && (Database == StandardDBMatrix[i, 5]))
                        return StandardDBMatrix[i, 0];
                }

                return "Custom";
            }

            set
            {
                if (value == "Custom")
                    return;

                String prevDatabaseType = DatabaseType;

                for (int i = 0; i <= StandardDBMatrix.GetUpperBound(0); i++)
                    if (StandardDBMatrix[i, 0] == value)
                    {
                        DatabaseType = StandardDBMatrix[i, 1];
                        ProviderType = StandardDBMatrix[i, 2];
                        ProviderName = StandardDBMatrix[i, 3];
                        OleDbName = StandardDBMatrix[i, 4];
                        Database = StandardDBMatrix[i, 5];
                        if (UserName == "" || StandardDBMatrix[i, 6] == "" || DatabaseType != prevDatabaseType)
                            UserName = StandardDBMatrix[i, 6];
                        if (Host == "" || StandardDBMatrix[i, 7] == "" || DatabaseType != prevDatabaseType)
                            Host = StandardDBMatrix[i, 7];
                        return;
                    }
            }
        }

        public bool InitialSave
        {
            get
            {
                String val = GetValue("initialsave");
                return val != "false";
            }

            set
            {
                SetValue("initialsave", value ? "true" : "false", "InitialSave");
            }
        }

        public bool InitialCheck
        {
            get
            {
                String val = GetValue("initialcheck");
                return val != "false";
            }

            set
            {
                SetValue("initialcheck", value ? "true" : "false", "InitialCheck");
            }
        }

        //public String KnownServiceAccountName { get; set; }

        public bool ServiceDetailsChanged { get; set; }

        public String ServiceAccountName
        {
            get
            {
                string val = GetValue("serviceaccountname");
                if (val == "")
                    return "Local Service";
                else
                    return val;
            }

            set
            {
                SetValue("serviceaccountname", value, "ServiceAccountName");
                ServiceDetailsChanged = true;
            }
        }

        public String ServiceAccountPassword
        {
            get
            {
                if (ServiceAccountRequiresPassword)
                    return GetValue("serviceaccountpassword");
                else
                    return "";
            }

            set
            {
                SetValue("serviceaccountpassword", value, "ServiceAccountPassword");
                ServiceDetailsChanged = true;
            }
        }

        public bool ServiceAccountRequiresPassword
        {
            get
            {
                String rffd = GetValue("serviceaccountrequirespassword");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                {
                    SetValue("serviceaccountrequirespassword", "true", "ServiceAccountRequiresPassword");
                }
                else
                {
                    ServiceAccountPassword = "";
                    SetValue("serviceaccountrequirespassword", "false", "ServiceAccountRequiresPassword");
                }

                ServiceDetailsChanged = true;
            }
        }

        public bool AutoStartPVBCService
        {
            get
            {
                String rffd = GetValue("autostartpvbcservice");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("autostartpvbcservice", "true", "AutoStartPVBCService");
                else
                    SetValue("autostartpvbcservice", "false", "AutoStartPVBCService");
                ServiceDetailsChanged = true;
            }
        }

        public String Host
        {
            get
            {
                return GetValue("host");
            }

            set
            {
                SetValue("host", value, "Host");
            }
        }

        public String Database
        {
            get
            {
                return GetValue("database");
            }

            set
            {
                SetValue("database", value, "Database");
            }
        }

        public String DatabaseType
        {
            get
            {
                return GetValue("databasetype");
            }

            set
            {
                SetValue("databasetype", value, "DatabaseType");
            }
        }

        public String ProviderType
        {
            get
            {
                return GetValue("providertype");
            }

            set
            {
                SetValue("providertype", value, "ProviderType");
            }
        }

        public String ProviderName
        {
            get
            {
                return GetValue("providername");
            }

            set
            {
                SetValue("providername", value, "ProviderName");
            }
        }

        public String OleDbName
        {
            get
            {
                return GetValue("oledbname");
            }

            set
            {
                SetValue("oledbname", value, "OleDbName");
            }
        }

        public String ConnectionString
        {
            get
            {
                return GetValue("connectionstring");
            }

            set
            {
                SetValue("connectionstring", value, "ConnectionString");
            }
        }

        public String UserName
        {
            get
            {
                return GetValue("username");
            }

            set
            {
                SetValue("username", value, "UserName");
            }
        }

        public String Password
        {
            get
            {
                return GetValue("password");
            }

            set
            {
                SetValue("password", value, "Password");
            }
        }

        public String DefaultDirectory
        {
            get
            {
                return GetValue("defaultdirectory");
            }

            set
            {
                SettingsDirectory = value;
                WriteWorkingDirectory = true;
                SetValue("defaultdirectory", value, "DefaultDirectory");
            }
        }

        public String InverterLogs
        {
            get
            {
                return GetValue("inverterlogs");
            }

            set
            {
                try
                {
                    if (value == "")
                    {
                        SetValue("inverterlogs", value, "InverterLogs");
                        return;
                    }
                    DirectoryInfo info = new DirectoryInfo(value);
                    if (info.Exists)
                        SetValue("inverterlogs", value, "InverterLogs");
                }
                catch (Exception)
                {
                }               
            }
        }

        public String LogFile
        {
            get
            {
                if (NewLogEachDay)
                    return "PVService_" + DateTime.Today.ToString("yyyyMMdd") + ".log";
                else
                    return "PVService.log";
            }
        }

        // deprecated
        private String PVOutputSiteId
        {
            get
            {
                return GetValue("pvoutputsiteid");
            }
        }

        // deprecated
        private String PVOutputDataInterval
        {
            get
            {
                return GetValue("pvoutputdatainterval");
            }
        }

        // deprecated
        private String PVOutputAPIKey
        {
            get
            {
                return GetValue("pvoutputapikey");
            }
        }

        public String ServiceSuspendType
        {
            get
            {
                return GetValue("servicesuspendtype");
            }

            set
            {
                if (value != "sleep" && value != "hibernate" && value != "idle" && value != "")
                    SetValue("servicesuspendtype", "sleep", "ServiceSuspendType");
                else
                    SetValue("servicesuspendtype", value, "ServiceSuspendType");
            }
        }


        public String EveningSuspendType
        {
            get
            {
                String tmp = GetValue("eveningsuspendtype");
                if (tmp == "")
                    tmp = GetValue("servicesuspendtype");
                return tmp;
            }

            set
            {
                if (value != "sleep" && value != "hibernate" && value != "idle" && value != "")
                    SetValue("eveningsuspendtype", "sleep", "EveningSuspendType");
                else
                    SetValue("eveningsuspendtype", value, "EveningSuspendType");
            }
        }

        public TimeSpan? ServiceStartTime
        {
            get
            {
                String ts = GetValue("servicestarttime");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("servicestarttime", "", "ServiceStartTime");
                else
                    SetValue("servicestarttime", value.ToString(), "ServiceStartTime");
            }
        }

        public TimeSpan? ServiceStopTime
        {
            get
            {
                String ts = GetValue("servicestoptime");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("servicestoptime", "", "ServiceStopTime");
                else
                    SetValue("servicestoptime", value.ToString(), "ServiceStopTime");
            }
        }

        public int WakeDelay
        {
            get
            {
                String ts = GetValue("wakedelay");
                if (ts == "" || ts == null)
                    return 0;
                else
                    return Convert.ToInt32(ts);
            }

            set
            {
                SetValue("wakedelay", value.ToString(), "WakeDelay");
            }
        }

        public TimeSpan? IntervalStartTime
        {
            get
            {
                String ts = GetValue("intervalstarttime");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("intervalstarttime", "", "IntervalStartTime");
                else
                    SetValue("intervalstarttime", value.ToString(), "IntervalStartTime");
            }
        }

        public TimeSpan? IntervalStopTime
        {
            get
            {
                String ts = GetValue("intervalstoptime");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("intervalstoptime", "", "IntervalStopTime");
                else
                    SetValue("intervalstoptime", value.ToString(), "IntervalStopTime");
            }
        }

        public TimeSpan? InverterStartTime
        {
            get
            {
                String ts = GetValue("inverterstarttime");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("inverterstarttime", "", "InverterStartTime");
                else
                    SetValue("inverterstarttime", value.ToString(), "InverterStartTime");
            }
        }

        public TimeSpan? InverterStopTime
        {
            get
            {
                String ts = GetValue("inverterstoptime");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("inverterstoptime", "", "InverterStopTime");
                else
                    SetValue("inverterstoptime", value.ToString(), "InverterStopTime");
            }
        }

        public TimeSpan? ServiceWakeInterval
        {
            get
            {
                String ts = GetValue("servicewakeinterval");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("servicewakeinterval", "", "ServiceWakeInterval");
                else
                    SetValue("servicewakeinterval", value.ToString(), "ServiceWakeInterval");
            }
        }

        public TimeSpan? ServiceSuspendInterval
        {
            get
            {
                String ts = GetValue("servicesuspendinterval");
                if (ts == "")
                    return null;
                else
                    return TimeSpan.Parse(ts);
            }

            set
            {
                if (value == null)
                    SetValue("servicesuspendinterval", "", "ServiceSuspendInterval");
                else
                    SetValue("servicesuspendinterval", value.ToString(), "ServiceSuspendInterval");
            }
        }

        public String OldSunnyExplorerPath
        {
            get
            {
                return GetValue("sunnyexplorerpath");
            }
        }

        public String OldSunnyExplorerPlantName
        {
            get
            {
                String name = GetValue("sunnyexplorerplantname").Trim();
                return name;
            }
        }

        public DateTime? FirstFullDay
        {
            get
            {
                return StringToDate(GetValue("firstfullday"));
            }

            set
            {
                SetValue("firstfullday", DateToString(value), "FirstFullDay");
            }
        }

        public bool EnablePVOutput
        {
            get
            {
                bool enable = false;
                foreach (PvOutputSiteSettings settings in PvOutputSystemList)
                    enable |= settings.Enable;
                return enable;
            }
        }

        public bool NewLogEachDay
        {
            get
            {
                String rffd = GetValue("newlogeachday");

                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("newlogeachday", "true", "NewLogEachDay");
                else
                    SetValue("newlogeachday", "false", "NewLogEachDay");
                OnPropertyChanged(new PropertyChangedEventArgs("LogFile"));
            }
        }

        public int? LogRetainDays
        {
            get
            {
                String rffd = GetValue("logretaindays");
                if (rffd == "")
                    return null;
                return Convert.ToInt32(rffd);
            }

            set
            {
                if (value.HasValue)
                    SetValue("logretaindays", value.Value.ToString(), "LogRetainDays");
                else
                    SetValue("logretaindays", "", "LogRetainDays");
            }
        }

        public bool EnableIntervalSuspend
        {
            get
            {
                String rffd = GetValue("enableintervalsuspend");
                
                // Setting changed from general suspend to interval suspend 
                // - retrieve old setting if new setting not defined
                if (rffd == "")
                    rffd = GetValue("enablesuspend");

                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("enableintervalsuspend", "true", "EnableIntervalSuspend");
                else
                    // use explicit false value to differentiate new setting from old enablesuspend setting
                    SetValue("enableintervalsuspend", "false", "EnableIntervalSuspend");
            }
        }

        public bool EnableEveningSuspend
        {
            get
            {
                //default value when missing from settings is true - opposite to other bool settings
                String rffd = GetValue("enableeveningsuspend");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (!value)
                    SetValue("enableeveningsuspend", "false", "EnableEveningSuspend");
                else
                    SetValue("enableeveningsuspend", "true", "EnableEveningSuspend");
            }
        }

        public bool ManualSuspendAutoResume
        {
            get
            {
                String rffd = GetValue("manualsuspendautoresume");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("manualsuspendautoresume", "true", "ManualSuspendAutoResume");
                else
                    SetValue("manualsuspendautoresume", "false", "ManualSuspendAutoResume");
            }
        }

        public int? ConsumptionMeterHistHoursOld
        {
            get
            {
                String rffd = GetValue("consumptionmeterhisthours");
                if (rffd == "")
                    return null;
                else
                    return Convert.ToInt32(rffd);
            }

            set
            {
                if (value == null)
                    SetValue("consumptionmeterhisthours", "", "ConsumptionMeterHistHours");
                else
                    SetValue("consumptionmeterhisthours", value.ToString(), "ConsumptionMeterHistHours");
            }
        }

        public bool UseCCTemperatureOld
        {
            get
            {
                String rffd = GetValue("usecctemperature");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }
        }


        public bool MeterHistoryTimeLineAdjust
        {
            get
            {
                String rffd = GetValue("meterhistorytimelineadjust");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("meterhistorytimelineadjust", "true", "MeterHistoryTimeLineAdjust");
                else
                    SetValue("meterhistorytimelineadjust", "false", "MeterHistoryTimeLineAdjust");
            }
        }

        public int? MeterHistoryStartMinute
        {
            get
            {
                String rffd = GetValue("meterhistorystartminute");
                if (rffd == "")
                    return null;
                else
                    return Convert.ToInt32(rffd);
            }

            set
            {
                SetValue("meterhistorystartminute", value.ToString(), "MeterHistoryStartMinute");
            }
        }

        public int? MeterHistoryEndMinute
        {
            get
            {
                String rffd = GetValue("meterhistoryendminute");
                if (rffd == "")
                    return null;
                else
                    return Convert.ToInt32(rffd);
            }

            set
            {
                SetValue("meterhistoryendminute", value.ToString(), "MeterHistoryEndMinute");
            }
        }

        public bool EmitEvents
        {
            get
            {
                String rffd = GetValue("emitevents");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("emitevents", "true", "EmitEvents");
                else
                    SetValue("emitevents", "false", "EmitEvents");
            }
        }

        public bool UseDefaultEvents
        {
            get
            {
                String rffd = GetValue("usedefaultevents");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("usedefaultevents", "true", " UseDefaultEvents");
                else
                    SetValue("usedefaultevents", "false", " UseDefaultEvents");
            }
        }

        public bool LogTrace
        {
            get
            {
                String rffd = GetValue("logtrace");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("logtrace", "true", "LogTrace");
                else
                    SetValue("logtrace", "false", "LogTrace");
            }
        }

        public bool LogDatabase
        {
            get
            {
                String rffd = GetValue("logdatabase");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("logdatabase", "true", "LogDatabase");
                else
                    SetValue("logdatabase", "false", "LogDatabase");
            }
        }

        public bool LogMeterTrace
        {
            get
            {
                String rffd = GetValue("logmetertrace");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("logmetertrace", "true", "LogMeterTrace");
                else
                    SetValue("logmetertrace", "false", "LogMeterTrace");
            }
        }

        public bool LogMessageContent
        {
            get
            {
                String rffd = GetValue("logmessagecontent");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("logmessagecontent", "true", "LogMessageContent");
                else
                    SetValue("logmessagecontent", "false", "LogMessageContent");
            }
        }

        public bool LogInformation
        {
            get
            {
                String rffd = GetValue("loginformation");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (!value)
                    SetValue("loginformation", "false", "LogInformation");
                else
                    SetValue("loginformation", "true", "LogInformation");
            }
        }

        public bool LogStatus
        {
            get
            {
                String rffd = GetValue("logstatus");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (!value)
                    SetValue("logstatus", "false", "LogStatus");
                else
                    SetValue("logstatus", "true", "LogStatus");
            }
        }

        public bool LogError
        {
            get
            {
                String rffd = GetValue("logerror");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (!value)
                    SetValue("logerror", "false", "LogError");
                else 
                    SetValue("logerror", "true", "LogError");
            }
        }

        public bool LogFormat
        {
            get
            {
                String rffd = GetValue("logformat");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (!value)
                    SetValue("logformat", "false", "LogFormat");
                else
                    SetValue("logformat", "true", "LogFormat");
            }
        }

        public bool LogEvent
        {
            get
            {
                String rffd = GetValue("logevent");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (!value)
                    SetValue("logevent", "false", "LogEvent");
                else
                    SetValue("logevent", "true", "LogEvent");
            }
        }

        public List<String> BaudRateList
        {
            get { return SerialPortSettings.BaudRateList; }
        }

        public List<String> SerialPortsList
        {
            get { return SerialPortSettings.SerialPortsList; }
        }

        public ObservableCollection<EnergyEventSettings> EnergyEventList
        {
            get { return _EnergyEventList; }
        }

        public ObservableCollection<DeviceManagerSettings> DeviceManagerList
        {
            get { return _DeviceManagerList; }
        }

        public ObservableCollection<PvOutputSiteSettings> PvOutputSystemList
        {
            get { return _PvOutputSystemList; }
        }

        public String BuildFileName(String fileName)
        {
            return BuildFileName(fileName, DefaultDirectory);
        }

        public ObservableCollection<DeviceManagerDeviceSettings> AllDevicesList
        {
            get { return _AllDevicesList; }
        }

        public ObservableCollection<DeviceManagerDeviceSettings> AllConsolidationDevicesList
        {
            get { return _AllConsolidationDevicesList; }
        }

    }
}
