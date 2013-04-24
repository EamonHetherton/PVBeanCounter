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
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace PVSettings
{
    public class PVOutputDaySettings : SettingsBase
    {
        ApplicationSettings ApplicationSettings;
        public PVOutputDaySettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
            ApplicationSettings = root;
        }

        public DateTime? Day
        {
            get
            {
                return ApplicationSettings.StringToDate(GetValue("day"));
            }

            set
            {
                SetValue("day", ApplicationSettings.DateToString(value), "Day");
            }
        }

        public Boolean ForceLoad
        {
            get
            {
                String val = GetValue("forceload");
                DateTime? forceLoadSet = ForceLoadSet;

                // ForceLoad is only logically set for 10 minutes
                if (val == "true")
                    if (forceLoadSet >= (DateTime.Now - TimeSpan.FromMinutes(10.0)))
                        return true;
                return false;
            }

            set
            {
                if (value)
                {
                    SetValue("forceload", "true", "ForceLoad");
                    // set start time for this Force Load - it expires after a number of minutes - refer get
                    ForceLoadSet = DateTime.Now;
                }
                else
                {
                    SetValue("forceload", "", "ForceLoad");
                    ForceLoadSet = null;
                }
            }
        }

        public DateTime? ForceLoadSet
        {
            get
            {
                return ApplicationSettings.StringToDateTime(GetValue("forceloadset"));
            }

            set
            {
                SetValue("forceloadset", ApplicationSettings.DateTimeToString(value), "ForceLoadSet");
            }
        }
    }
}
