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
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace PVSettings
{
    public class InverterSettings : SettingsBase
    {
        public InverterSettings(ApplicationSettings root, XmlElement element)
            : base(root, element)
        {
        }

        public UInt64 Address
        {
            get
            {
                return Convert.ToUInt64(GetValue("address"));
            }

            set
            {
                SetValue("address", value.ToString(), "Address");
            }
        }

        public bool Enable
        {
            get
            {
                return GetValue("enable") != "false";
            }

            set
            {
                SetValue("enable", value ? "true" : "false", "Enable");
            }
        }
    }
}
