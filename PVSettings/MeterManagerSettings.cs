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
    public abstract class MeterManagerSettings : SettingsBase
    {
        public enum MeterManagerType
        {
            CurrentCost = 0,
            Owl = 1,
            EW4009 = 2
        }

        public class MeterManagerInfo
        {
            public MeterManagerType Type { get; private set; }
            public String Name { get; private set; }
            public bool CanInsert { get; private set; }
            public String LongName { get; private set; }

            public MeterManagerInfo(MeterManagerType type, String name, bool canInsert, String longName)
            {
                Type = type;
                Name = name;
                CanInsert = canInsert;
                LongName = longName;
            }
        }

        public abstract int SampleFrequency { get; set; }

        public static MeterManagerInfo[] MeterManagerTypes = 
        { 
            new MeterManagerInfo( MeterManagerType.CurrentCost, "CC128", false, "Current Cost"), 
            new MeterManagerInfo( MeterManagerType.Owl, "Owl", false, "Owl"), 
            new MeterManagerInfo( MeterManagerType.EW4009, "EW4009", false, "EW4009"), 
        };

        public ApplicationSettings ApplicationSettings;
        internal ObservableCollection<MeterApplianceSettings> applianceList;

        public MeterManagerSettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
            ApplicationSettings = root;
            LoadAppliances();
        }

        private void LoadAppliances()
        {
            applianceList = new ObservableCollection<MeterApplianceSettings>();

            bool[] loaded = new bool[16];
            int index;

            for (index = 0; index < 10; index++)
                loaded[index] = false;

            XmlElement appliances = GetElement("appliancelist");
            if (appliances == null)
            {
                appliances = AddElement(settings, "appliancelist");
                // retrieve old appliance list (if it exists as an application setting)
                if (ManagerTypeInternal == MeterManagerType.CurrentCost)
                {
                    String xml = ApplicationSettings.GetApplianceListXml();
                    if (xml != "")
                        appliances.InnerXml = xml;
                }                
            }

            index = 0;
            foreach (XmlElement e in appliances.ChildNodes)
            {
                if (e.Name == "appliance")
                {
                    MeterApplianceSettings appliance = new MeterApplianceSettings(RootSettings, e, ManagerTypeInternal);
                    applianceList.Add(appliance);                    
                    loaded[index] = true;
                    index++;
                }
            }

            int maxIndex = ManagerTypeInternal == MeterManagerType.CurrentCost ? 10 : (ManagerTypeInternal == MeterManagerType.EW4009 ? 16 : 1);

            // create default appliances
            for (index = 0; index < maxIndex; index++)
                if (!loaded[index])
                {
                    XmlElement e = AddElement(appliances, "appliance");
                    MeterApplianceSettings appliance = new MeterApplianceSettings(RootSettings, e, ManagerTypeInternal);

                    appliance.ApplianceNo = index.ToString();
                    appliance.StoreReading = (index < 2) && (ManagerTypeInternal == MeterManagerType.CurrentCost) || (index < 1);
                    if (ManagerTypeInternal == MeterManagerType.CurrentCost)
                    {
                        appliance.AdjustHistory = (index < 2);
                        appliance.StoreHistory = false;
                        appliance.Calibrate = 1.0;
                    }

                    applianceList.Add(appliance);
                }
        }

        public ObservableCollection<MeterApplianceSettings> ApplianceList
        {
            get { return applianceList; }
        }

        public MeterApplianceSettings AddAppliance()
        {
            XmlElement appliances = GetElement("appliancelist");
            if (appliances == null)
                return null;
            XmlElement e = AddElement(appliances, "appliance");
            MeterApplianceSettings appl = new MeterApplianceSettings(RootSettings, e, ManagerTypeInternal);

            int applNo = 999;
            
            bool found;

            do
            {
                found = false;
                foreach (MeterApplianceSettings oneAppl in ApplianceList)
                {
                    if (oneAppl.ApplianceNo == applNo.ToString())
                    {
                        found = true;
                        applNo++;
                    }
                }
            }
            while (found);

            appl.ApplianceNo = applNo.ToString();

            appl.Calibrate = 1.0;
            appl.StoreReading = true;

            ApplianceList.Add(appl);
            return appl;
        }

        public void DeleteAppliance(MeterApplianceSettings delAppl)
        {
            ApplianceList.Remove(delAppl);

            XmlElement appliances = GetElement("appliancelist");
            if (appliances == null)
                return;

            foreach (XmlNode child in appliances.ChildNodes)
            {
                if (child.Name == "appliance")
                {
                    if (ElementHasChild(child, "applianceno", delAppl.ApplianceNo.ToString()))
                    {
                        appliances.RemoveChild(child);
                        SettingChangedEventHandler("");
                        return;
                    }
                }
            }
        }

        public MeterApplianceSettings GetAppliance(String applianceNo)
        {
            foreach (MeterApplianceSettings app in ApplianceList)
            {
                if (app.ApplianceNo == applianceNo)
                    return app;
            }

            return null;
        }

        public abstract MeterManagerType ManagerTypeInternal { get; }
        
        public String ManagerType
        {
            get
            {
                return GetValue("managertype");
            }

            set
            {
                // Note the set value is ignored, use the official identification name
                SetValue("managertype", MeterManagerTypes[(int)ManagerTypeInternal].Name, "ManagerType");
            }
        }

        public String LongName { get { return MeterManagerTypes[(int)ManagerTypeInternal].LongName; }}

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

        public String StandardName
        {
            get
            {
                return ManagerType + "/" + InstanceNo.ToString();
            }
        }

        public String Description
        {
            get
            {
                String rffd = GetValue("description");
                if (rffd == "")
                    return StandardName;
                else
                    return rffd;
            }

            set
            {
                SetValue("description", value, "Description");
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

        public bool Enabled
        {
            get
            {
                String rffd = GetValue("enabled");
                return (rffd == "true");
            }

            set
            {
                if (value)
                    SetValue("enabled", "true", "Enabled");
                else
                    SetValue("enabled", "", "Enabled");
            }
        }
    }
}
