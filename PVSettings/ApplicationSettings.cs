/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
            //, { "Jet (2007)", "Jet", "OleDb", "System.Data.OleDb", "Microsoft.ACE.OLEDB.12.0", "PVRecords_jet.accdb", "", "" }
            //, { "Jet (2003)", "Jet", "OleDb", "System.Data.OleDb", "Microsoft.Jet.OLEDB.4.0", "PVRecords_jet.mdb", "", "" }
            , { "SQL Server", "SQL Server", "Proprietary", "System.Data.SqlClient", "", "PVHistory", "", @"localhost\SQLEXPRESS" }
            , { "MySQL", "MySql", "Proprietary", "MySql.Data.MySQLClient", "", "pvhistory", "PVRecords", "localhost" } };

        private ObservableCollection<DeviceManagerSettings> _DeviceManagerList;
        private ObservableCollection<PvOutputSiteSettings> _PvOutputSystemList;
        private ObservableCollection<DeviceManagerDeviceSettings> _AllDevicesList;
        private ObservableCollection<DeviceManagerDeviceSettings> _AllConsolidationDevicesList;
        
        protected SystemServices SystemServices;

        public DeviceManagementSettings DeviceManagementSettings;

        public bool LoadingEnergyEvents { get; private set; }

        private static Mutex SaveSettingsMutex = new Mutex();

        //public InverterManagerSettings DeviceInverterManagerSettings { get; private set; }

        public ApplicationSettings()
            : base("settings_v2.xml", "configuration", @"\settings_template_SE_SQLite.xml")
        {            
            LegacySettingsNames.Add("settings.xml");

            LoadSettings(true);

            DeviceManagementSettings = new DeviceManagementSettings();
            
            SystemServices = null;

            _InitialSave = new GenericSetting<bool>(true, this, "InitialSave");
            _InitialCheck = new GenericSetting<bool>(true, this, "InitialCheck");
            _ServiceAccountName = new GenericSetting<string>("Local Service", this, "ServiceAccountName");
            _ServiceAccountPassword = new GenericSetting<string>("", this, "ServiceAccountPassword");
            _ServiceAccountRequiresPassword = new GenericSetting<bool>(false, this, "ServiceAccountRequiresPassword");
            _AutoStartPVBCService = new GenericSetting<bool>(true, this, "AutoStartPVBCService");
            _Host = new GenericSetting<string>("", this, "Host");
            _Database = new GenericSetting<string>("", this, "Database");
            _DatabaseType = new GenericSetting<string>("", this, "DatabaseType");
            _ProviderType = new GenericSetting<string>("", this, "ProviderType");
            _ProviderName = new GenericSetting<string>("", this, "ProviderName");
            _OleDbName = new GenericSetting<string>("", this, "OleDbName");
            _ConnectionString = new GenericSetting<string>("", this, "ConnectionString");
            _UserName = new GenericSetting<string>("", this, "UserName");
            _Password = new GenericSetting<string>("", this, "Password");
            _DefaultDirectory = new GenericSetting<string>("", this, "DefaultDirectory");
            _InverterLogs = new GenericSetting<string>("", this, "InverterLogs");
            _ServiceSuspendType = new GenericSetting<string>("", this, "ServiceSuspendType");
            _EveningSuspendType = new GenericSetting<string>("", this, "EveningSuspendType");
            _ServiceStartTime = new GenericSetting<TimeSpan?>(this, "ServiceStartTime");
            _ServiceStopTime = new GenericSetting<TimeSpan?>(this, "ServiceStopTime");
            _WakeDelay = new GenericSetting<Int32>(0, this, "WakeDelay");
            _IntervalStartTime = new GenericSetting<TimeSpan?>(this, "IntervalStartTime");
            _IntervalStopTime = new GenericSetting<TimeSpan?>(this, "IntervalStopTime");
            _InverterStartTime = new GenericSetting<TimeSpan?>(this, "InverterStartTime");
            _InverterStopTime = new GenericSetting<TimeSpan?>(this, "InverterStopTime");
            _ServiceWakeInterval = new GenericSetting<TimeSpan?>(this, "ServiceWakeInterval");
            _ServiceSuspendInterval = new GenericSetting<TimeSpan?>(this, "ServiceSuspendInterval");
            _SunnyExplorerPlantName = new GenericSetting<string>("", this, "SunnyExplorerPlantName");
            _FirstFullDay = new GenericSetting<DateTime?>(this, "FirstFullDay", DateStrings);
            _NewLogEachDay = new GenericSetting<bool>(true, this, "NewLogEachDay");
            _LogRetainDays = new GenericSetting<int?>(this, "LogRetainDays");
            _EnableIntervalSuspend = new GenericSetting<bool>(false, this, "EnableIntervalSuspend");
            _EnableEveningSuspend = new GenericSetting<bool>(false, this, "EnableEveningSuspend");
            _ManualSuspendAutoResume = new GenericSetting<bool>(false, this, "ManualSuspendAutoResume");
            _EmitEvents = new GenericSetting<bool>(false, this, "EmitEvents");
            _LogTrace = new GenericSetting<bool>(false, this, "LogTrace");
            _LogDatabase = new GenericSetting<bool>(false, this, "LogDatabase");
            _LogDetailTrace = new GenericSetting<bool>(false, this, "LogMeterTrace");
            _LogMessageContent = new GenericSetting<bool>(false, this, "LogMessageContent");
            _LogInformation = new GenericSetting<bool>(true, this, "LogInformation");
            _LogStatus = new GenericSetting<bool>(true, this, "LogStatus");
            _LogError = new GenericSetting<bool>(true, this, "LogError");
            _LogFormat = new GenericSetting<bool>(true, this, "LogFormat");
            _LogEvent = new GenericSetting<bool>(false, this, "LogEvent");

            LoadSettingsSub();
            ServiceAccountPassword = "";
            ServiceDetailsChanged = false;
            LoadingEnergyEvents = false;

            CheckProtocolDeviceGroups();
        }

        private void CheckProtocolDeviceGroups()
        {
            bool needed = false;
            foreach (DeviceManagerSettings dm in DeviceManagerList)
                needed |= (dm.DeviceGroup.IsProtocol);
            if (!needed)
                DeviceManagementSettings.RemoveProtocolDeviceGroups();
        }

        public override void SaveSettings()
        {
            SaveSettingsMutex.WaitOne();
            try
            {
                base.SaveSettings();
            }
            finally
            {
                SaveSettingsMutex.ReleaseMutex();
            }
        }

        public PvOutputSiteSettings FindPVOutputBySystemId(String systemId)
        {
            foreach (PvOutputSiteSettings settings in _PvOutputSystemList)
                if (settings.SystemId == systemId)
                    return settings;
            return null;
        }

        public void SetSystemServices(SystemServices systemServices)
        {
            bool oldChangedState = ServiceDetailsChanged;
            SystemServices = systemServices;
            ServiceDetailsChanged = oldChangedState;
        }

        private void RemoveOldElements()
        {
        }

        protected override void RemoveLegacyElements()
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
            DeleteElement("usedefaultevents");
            DeleteElement("invertermanagerlist");
            DeleteElement("energyevents");
        }

        internal String GetApplianceListXml()
        {
            XmlElement elem = GetElement("appliancelist");
            if (elem == null)
                return "";

            return elem.InnerXml;
        }

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

        public DeviceManagerSettings AddDeviceManager()
        {
            XmlElement managers = GetElement("devicemanagerlist");
            if (managers == null)
                managers = AddElement(settings, "devicemanagerlist");

            XmlElement e = AddElement(managers, "devicemanager");
            DeviceManagerSettings manager = new DeviceManagerSettings(this, e);
            // This was producing extra serial port element
            //XmlElement e2 = AddElement(e, "serialport");
            //manager.SerialPort = new SerialPortSettings(this, e2);
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
                        RefreshAllDevices();
                        return;
                    }
                }
            }            
        }

        public void PVOutputSystemIdChanged(String oldSystemId, String newSystemId)
        {
            foreach (DeviceManagerSettings dms in _DeviceManagerList)
            {
                foreach (DeviceManagerDeviceSettings ds in dms.DeviceList)
                {
                    if (ds.PVOutputSystem == oldSystemId)
                        ds.PVOutputSystem = newSystemId;
                }
            }
            
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
                site.APIKey = "YourAPIKeyHere";
                site.APIVersion = "";
                site.SystemId = "YourSiteID";
                site.LiveDays = 2;
                site.AutoBackload = true;
                site.DataInterval = "10";
                site.Enable = EnablePVOutput;

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
            LoadPVOutputSites();

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

        private GenericSetting<bool> _InitialSave;
        public bool InitialSave
        {
            get { return _InitialSave.Value; }
            set { _InitialSave.Value = value; }
        }

        private GenericSetting<bool> _InitialCheck;
        public bool InitialCheck
        {
            get { return _InitialCheck.Value; }
            set { _InitialCheck.Value = value; }
        }

        public bool ServiceDetailsChanged { get; set; }

        private GenericSetting<string> _ServiceAccountName;
        public String ServiceAccountName
        {
            get { return _ServiceAccountName.Value; }
            set
            {
                _ServiceAccountName.Value = value;
                ServiceDetailsChanged = true;
            }
        }

        private GenericSetting<string> _ServiceAccountPassword;
        public String ServiceAccountPassword
        {
            get
            {
                if (ServiceAccountRequiresPassword)
                    return _ServiceAccountPassword.Value;
                else
                    return "";
            }

            set
            {
                _ServiceAccountPassword.Value = value;
                ServiceDetailsChanged = true;
            }
        }

        private GenericSetting<bool> _ServiceAccountRequiresPassword;
        public bool ServiceAccountRequiresPassword
        {
            get
            {
                return _ServiceAccountRequiresPassword.Value;
            }

            set
            {
                _ServiceAccountRequiresPassword.Value = value;
                ServiceDetailsChanged = true;
            }
        }

        private GenericSetting<bool> _AutoStartPVBCService;
        public bool AutoStartPVBCService
        {
            get
            {
                return _AutoStartPVBCService.Value;
            }

            set
            {
                _AutoStartPVBCService.Value = value;
                ServiceDetailsChanged = true;
            }
        }

        private GenericSetting<string> _Host;
        public String Host
        {
            get { return _Host.Value; }
            set { _Host.Value = value; }
        }

        private GenericSetting<string> _Database;
        public String Database
        {
            get { return _Database.Value; }
            set { _Database.Value = value; }
        }

        private GenericSetting<string> _DatabaseType;
        public String DatabaseType
        {
            get { return _DatabaseType.Value; }
            set { _DatabaseType.Value = value; }
        }

        private GenericSetting<string> _ProviderType;
        public String ProviderType
        {
            get { return _ProviderType.Value; }
            set { _ProviderType.Value = value; }
        }

        private GenericSetting<string> _ProviderName;
        public String ProviderName
        {
            get { return _ProviderName.Value; }
            set { _ProviderName.Value = value; }
        }

        private GenericSetting<string> _OleDbName;
        public String OleDbName
        {
            get { return _OleDbName.Value; }
            set { _OleDbName.Value = value; }
        }

        private GenericSetting<string> _ConnectionString;
        public String ConnectionString
        {
            get { return _ConnectionString.Value; }
            set { _ConnectionString.Value = value; }
        }

        private GenericSetting<string> _UserName;
        public String UserName
        {
            get { return _UserName.Value; }
            set { _UserName.Value = value; }
        }

        private GenericSetting<string> _Password;
        public String Password
        {
            get { return _Password.Value; }
            set { _Password.Value = value; }
        }

        private GenericSetting<string> _DefaultDirectory;
        public String DefaultDirectory
        {
            get { return _DefaultDirectory.Value; }
            set 
            {                 
                SettingsDirectory = value;
                WriteWorkingDirectory = true;
                _DefaultDirectory.Value = value;
            }
        }

        private GenericSetting<string> _InverterLogs;
        public String InverterLogs
        {
            get { return _InverterLogs.Value; }
            set 
            {
                try
                {
                    if (value == "")
                    {
                        _InverterLogs.Value = value;
                        return;
                    }
                    DirectoryInfo info = new DirectoryInfo(value);
                    if (info.Exists)
                        _InverterLogs.Value = value;
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

        private GenericSetting<string> _ServiceSuspendType;
        public String ServiceSuspendType
        {
            get { return _ServiceSuspendType.Value; }
            set 
            {
                if (value != "sleep" && value != "hibernate" && value != "idle" && value != "")
                    _ServiceSuspendType.Value = "sleep";
                else
                    _ServiceSuspendType.Value = value; 
            }
        }

        private GenericSetting<string> _EveningSuspendType;
        public String EveningSuspendType
        {
            get 
            {
                String tmp = _EveningSuspendType.Value;
                if (tmp == "")
                    tmp = _ServiceSuspendType.Value;
                return tmp;
            }
            set
            {
                if (value != "sleep" && value != "hibernate" && value != "idle" && value != "")
                    _EveningSuspendType.Value = "sleep";
                else
                    _EveningSuspendType.Value = value;
            }
        }

        private GenericSetting<TimeSpan?> _ServiceStartTime;
        public TimeSpan? ServiceStartTime
        {
            get { return _ServiceStartTime.Value; }
            set { _ServiceStartTime.Value = value; }
        }

        private GenericSetting<TimeSpan?> _ServiceStopTime;
        public TimeSpan? ServiceStopTime
        {
            get { return _ServiceStopTime.Value; }
            set { _ServiceStopTime.Value = value; }
        }

        private GenericSetting<Int32> _WakeDelay;
        public int WakeDelay
        {
            get { return _WakeDelay.Value; }
            set { _WakeDelay.Value = value; }
        }

        private GenericSetting<TimeSpan?> _IntervalStartTime;
        public TimeSpan? IntervalStartTime
        {
            get { return _IntervalStartTime.Value; }
            set { _IntervalStartTime.Value = value; }
        }

        private GenericSetting<TimeSpan?> _IntervalStopTime;
        public TimeSpan? IntervalStopTime
        {
            get { return _IntervalStopTime.Value; }
            set { _IntervalStopTime.Value = value; }
        }

        private GenericSetting<TimeSpan?> _InverterStartTime;
        public TimeSpan? InverterStartTime
        {
            get { return _InverterStartTime.Value; }
            set { _InverterStartTime.Value = value; }
        }

        private GenericSetting<TimeSpan?> _InverterStopTime;
        public TimeSpan? InverterStopTime
        {
            get { return _InverterStopTime.Value; }
            set { _InverterStopTime.Value = value; }
        }

        private GenericSetting<TimeSpan?> _ServiceWakeInterval;
        public TimeSpan? ServiceWakeInterval
        {
            get { return _ServiceWakeInterval.Value; }
            set { _ServiceWakeInterval.Value = value; }
        }

        private GenericSetting<TimeSpan?> _ServiceSuspendInterval;
        public TimeSpan? ServiceSuspendInterval
        {
            get { return _ServiceSuspendInterval.Value; }
            set { _ServiceSuspendInterval.Value = value; }
        }

        private GenericSetting<string> _SunnyExplorerPlantName;
        public String SunnyExplorerPlantName
        {
            get { return _SunnyExplorerPlantName.Value; }
        }

        private GenericSetting<DateTime?> _FirstFullDay;
        public DateTime? FirstFullDay
        {
            get { return _FirstFullDay.Value; }
            set { _FirstFullDay.Value = value; }
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

        private GenericSetting<bool> _NewLogEachDay;
        public bool NewLogEachDay
        {
            get { return _NewLogEachDay.Value; }
            set 
            { 
                _NewLogEachDay.Value = value;
                OnPropertyChanged(new PropertyChangedEventArgs("LogFile"));
            }
        }

        private GenericSetting<int?> _LogRetainDays;
        public int? LogRetainDays
        {
            get { return _LogRetainDays.Value; }
            set { _LogRetainDays.Value = value; }
        }

        private GenericSetting<bool> _EnableIntervalSuspend;
        public bool EnableIntervalSuspend
        {
            get { return _EnableIntervalSuspend.Value; }
            set { _EnableIntervalSuspend.Value = value; }
        }

        private GenericSetting<bool> _EnableEveningSuspend;
        public bool EnableEveningSuspend
        {
            get { return _EnableEveningSuspend.Value; }
            set { _EnableEveningSuspend.Value = value; }
        }

        private GenericSetting<bool> _ManualSuspendAutoResume;
        public bool ManualSuspendAutoResume
        {
            get { return _ManualSuspendAutoResume.Value; }
            set { _ManualSuspendAutoResume.Value = value; }
        }

        private GenericSetting<bool> _EmitEvents;
        public bool EmitEvents
        {
            get { return _EmitEvents.Value; }
            set { _EmitEvents.Value = value; }
        }

        private GenericSetting<bool> _LogTrace;
        public bool LogTrace
        {
            get { return _LogTrace.Value; }
            set { _LogTrace.Value = value; }
        }

        private GenericSetting<bool> _LogDatabase;
        public bool LogDatabase
        {
            get { return _LogDatabase.Value; }
            set { _LogDatabase.Value = value; }
        }

        private GenericSetting<bool> _LogDetailTrace;
        public bool LogDetailTrace
        {
            get { return _LogDetailTrace.Value; }
            set { _LogDetailTrace.Value = value; }
        }

        private GenericSetting<bool> _LogMessageContent;
        public bool LogMessageContent
        {
            get { return _LogMessageContent.Value; }
            set { _LogMessageContent.Value = value; }
        }

        private GenericSetting<bool> _LogInformation;
        public bool LogInformation
        {
            get { return _LogInformation.Value; }
            set { _LogInformation.Value = value; }
        }

        private GenericSetting<bool> _LogStatus;
        public bool LogStatus
        {
            get { return _LogStatus.Value; }
            set { _LogStatus.Value = value; }
        }

        private GenericSetting<bool> _LogError;
        public bool LogError
        {
            get { return _LogError.Value; }
            set { _LogError.Value = value; }
        }

        private GenericSetting<bool> _LogFormat;
        public bool LogFormat
        {
            get { return _LogFormat.Value; }
            set { _LogFormat.Value = value; }
        }

        private GenericSetting<bool> _LogEvent;
        public bool LogEvent
        {
            get { return _LogEvent.Value; }
            set { _LogEvent.Value = value; }
        }

        public List<String> BaudRateList
        {
            get { return SerialPortSettings.BaudRateList; }
        }

        public List<String> SerialPortsList
        {
            get { return SerialPortSettings.SerialPortsList; }
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
