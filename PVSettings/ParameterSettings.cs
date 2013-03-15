/*
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
    public class ParameterSettings : SettingsBase, INotifyPropertyChanged
    {
        private DeviceManagementSettings DeviceManagementSettings;

        public ParameterSettings(DeviceManagementSettings root, XmlElement element)
            : base(root, element)
        {
            DeviceManagementSettings = root;
        }

        public String Name
        {
            get
            {
                return GetValue("name");
            }
        }

        public String Content
        {
            get
            {
                return GetValue("content");
            }
        }

        public String ParameterValue
        {
            get
            {
                String val = GetValue("parametervalue");
                if (val == "")
                    return null;
                else
                    return val;
            }
        }
    }
}
