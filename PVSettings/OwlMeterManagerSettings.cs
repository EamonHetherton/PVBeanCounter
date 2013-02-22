/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using System.Linq;
using System.Text;
using System.Xml;

namespace PVSettings
{
    public class OwlMeterManagerSettings : MeterManagerSettings
    {
        public OwlMeterManagerSettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
            
        }

        public override int SampleFrequency
        {
            get { return 300; }
            set { }
        }

        public override MeterManagerType ManagerTypeInternal { get { return MeterManagerType.Owl; } }

        public String OwlDatabase
        {
            get
            {
                String val = GetValue("owldatabase").Trim();
                if (val == "")
                    return @"C:\ProgramData\2SE\be.db";
                else
                    return val;
            }

            set
            {
                SetValue("owldatabase", value, "OwlDatabase");
            }
        }

        public bool ReloadDays
        {
            get
            {
                String rffd = GetValue("reloaddays");
                DateTime? reloadDaysSet = ReloadDaysSet;

                // ResetFirstFullDay is only logically set for 10 minutes
                if (rffd == "true")
                    if (reloadDaysSet >= (DateTime.Now - TimeSpan.FromMinutes(10.0)))
                        return true;
                return false;
            }

            set
            {
                if (value)
                {
                    SetValue("reloaddays", "true", "ReloadDays");
                    ReloadDaysSet = DateTime.Now;
                }
                else
                {
                    SetValue("reloaddays", "", "ReloadDays");
                    ReloadDaysSet = null;
                }
            }
        }

        public DateTime? ReloadDaysSet
        {
            get
            {
                String val = GetValue("reloaddaysset");
                if (val == "")
                    return ApplicationSettings.FirstFullDay;
                else
                    return ApplicationSettings.StringToDateTime(val);
            }

            private set
            {
                SetValue("reloaddaysset", ApplicationSettings.DateTimeToString(value), "ReloadDaysSet");
            }
        }

        public DateTime? ReloadDaysFromDate
        {
            get
            {
                String ffd = GetValue("reloaddaysfromdate");
                if (ffd == "")
                    return ApplicationSettings.FirstFullDay;
                else
                    return ApplicationSettings.StringToDate(ffd);
            }

            set
            {
                SetValue("reloaddaysfromdate", ApplicationSettings.DateToString(value), "ReloadDaysFromDate");
            }
        }
    }
}
