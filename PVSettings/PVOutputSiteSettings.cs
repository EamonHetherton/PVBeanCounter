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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml;

namespace PVSettings
{
    public class PvOutputSiteSettings : SettingsBase
    {
        public ApplicationSettings ApplicationSettings;
        private ObservableCollection<PVOutputDaySettings> pvOutputDayList;

        public PvOutputSiteSettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
            ApplicationSettings = (ApplicationSettings)root;
            LoadPVOutputDays();
        }

        private void AdjustPVOutputDayList(bool initialLoadx = false)
        {
            int dayCount = LiveDays == null ? 2 : LiveDays.Value;
            if (dayCount < 1)
                dayCount = 1;
            //else if (dayCount > 14)
            //    dayCount = 14;

            XmlElement days = GetElement("pvoutputdaylist");

            int i = 0;

            DateTime date = DateTime.Today;

            while (i < dayCount)
            {
                date = DateTime.Today - TimeSpan.FromDays(i);

                PVOutputDaySettings day;

                XmlElement e;

                if (pvOutputDayList.Count > i)
                {
                    day = pvOutputDayList[i];
                    if (day.Day < date)
                    {
                        e = AddElement(days, "pvoutputday", day.settings);
                        day = new PVOutputDaySettings(ApplicationSettings, e);
                        day.Day = date;
                        day.ForceLoad = false;
                        LoadPVOutputDaysSub();
                    }
                }
                else
                {
                    e = AddElement(days, "pvoutputday");
                    day = new PVOutputDaySettings(ApplicationSettings, e);
                    day.Day = date;
                    day.ForceLoad = false;
                    LoadPVOutputDaysSub();
                }

                i++;
            }

            while (pvOutputDayList.Count > dayCount)
            {
                PVOutputDaySettings day = pvOutputDayList[pvOutputDayList.Count - 1];
                pvOutputDayList.Remove(day);
                days.RemoveChild(day.settings);
            }

            LoadPVOutputDaysSub();
        }

        private void LoadPVOutputDays()
        {
            LoadPVOutputDaysSub();
            AdjustPVOutputDayList();
        }

        private void LoadPVOutputDaysSub()
        {
            pvOutputDayList = new ObservableCollection<PVOutputDaySettings>();

            XmlElement days = GetElement("pvoutputdaylist");
            if (days == null)
                days = AddElement(settings, "pvoutputdaylist");

            foreach (XmlElement e in days.ChildNodes)
            {
                if (e.Name == "pvoutputday")
                {
                    PVOutputDaySettings day = new PVOutputDaySettings(ApplicationSettings, e);

                    pvOutputDayList.Add(day);
                }
            }
        }

        public bool HaveSubscription
        {
            get { return GetValue("havesubscription") == "true"; }
            set 
            { 
                SetValue("havesubscription", value ? "true" : "false", "HaveSubscription");
            }
        }

        public ObservableCollection<PVOutputDaySettings> PvOutputDayList
        {
            get { return pvOutputDayList; }
        }

        public String Description
        {
            get
            {
                if (SystemId == Name)
                    return "System Id: " + SystemId;
                else
                    return Name + " - Id: " + SystemId;
            }
        }

        public String SystemId
        {
            get
            {
                String val = GetValue("systemid");
                if (val == "")
                    val = GetValue("siteid"); // look for legacy value
                return val;
            }

            set
            {
                string val = value == null ? "" : value.Trim();
                bool updateName = (Name == SystemId);
                    
                SetValue("systemid", val, "SystemId");
                DeleteElement("siteid"); // legacy value no longer required

                if (updateName)
                    Name = "";
            }
        }

        public String Name
        {
            get
            {
                String val = GetValue("name");
                if (val == null || val == "")
                    return SystemId;
                else
                    return val;
            }

            set
            {
                string val = value == null ? "" : value.Trim();
                if (value == SystemId)
                    SetValue("name", "", "Name");
                else
                    SetValue("name", value, "Name");
            }
        }

        public String DataInterval
        {
            get
            {
                return GetValue("datainterval");
            }

            set
            {
                SetValue("datainterval", value, "DataInterval");
            }
        }

        public int DataIntervalSeconds
        {
            get
            {
                try
                {
                    String intervalStr = GetValue("datainterval");
                    if (intervalStr == "")
                        return 600;
                    return int.Parse(intervalStr) * 60;
                }
                catch (Exception)
                {
                    return 600;
                }
            }
        }

        public String APIKey
        {
            get
            {
                return GetValue("apikey");
            }

            set
            {
                SetValue("apikey", value, "APIKey");
            }
        }

        public String APIVersion
        {
            get
            {
                return GetValue("apiversion");
            }

            set
            {
                SetValue("apiversion", value, "APIVersion");
            }
        }

        public bool Enable
        {
            get
            {
                String rffd = GetValue("enable");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                    SetValue("enable", "true", "Enable");
                else
                    SetValue("enable", "", "Enable");
            }
        }

        public bool AutoBackload
        {
            get
            {
                String rffd = GetValue("autobackload");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("autobackload", "", "AutoBackload");
                else
                    SetValue("autobackload", "false", "AutoBackload");
            }
        }

        public int? LiveDaysInternal
        {
            get
            {
                String rffd = GetValue("livedays");
                if (rffd == "")
                    return null;
                else
                    return Convert.ToInt32(rffd);
            }

            set
            {
                if (value == null)
                    SetValue("livedays", "", "LiveDays", true);
                else
                    SetValue("livedays", value.ToString(), "LiveDays", true);
                AdjustPVOutputDayList();
                OnPropertyChanged(new PropertyChangedEventArgs("PvOutputDayList"));
            }
        }

        public int? LiveDays
        {
            get
            {
                String rffd = GetValue("livedays");
                if (rffd == "")
                    return null;
                else
                    return Convert.ToInt32(rffd);
            }

            set
            {
                if (value == null)
                    SetValue("livedays", "", "LiveDays");
                else
                    SetValue("livedays", value.ToString(), "LiveDays");
                AdjustPVOutputDayList();
                OnPropertyChanged(new PropertyChangedEventArgs("PvOutputDayList"));
            }
        }

        public bool UploadYield
        {
            get
            {
                String rffd = GetValue("uploadyield");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("uploadyield", "true", "UploadYield");
                else
                    SetValue("uploadyield", "false", "UploadYield");
            }
        }

        public bool UploadConsumption
        {
            get
            {
                String rffd = GetValue("uploadconsumption");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("uploadconsumption", "true", "UploadConsumption");
                else
                    SetValue("uploadconsumption", "false", "UploadConsumption");
            }
        }

        public bool PowerMinMax
        {
            get
            {
                String rffd = GetValue("powerminmax");
                if (rffd == "")
                    rffd = GetValue("consumptionpowerminmax");
                if (rffd == "false")
                    return false;
                else
                    return true;
            }

            set
            {
                if (value)
                    SetValue("powerminmax", "true", "PowerMinMax");
                else
                    SetValue("powerminmax", "false", "PowerMinMax");

                DeleteElement("consumptionpowerminmax");
            }
        }
    }
}
