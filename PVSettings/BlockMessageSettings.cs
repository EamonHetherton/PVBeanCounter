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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Xml;
using MackayFisher.Utilities;
namespace PVSettings
{
    public class BlockMessageSettings : PVSettings.SettingsBase, INotifyPropertyChanged
    {
        public ObservableCollection<RegisterSettings> RegisterList;
        public ObservableCollection<RegisterSettings> RegisterTemplateList;

        public BlockMessageSettings(DeviceManagementSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            BlockSettings.LoadRegisters("register",(DeviceManagementSettings)RootSettings, settings, ref RegisterList);
            BlockSettings.LoadRegisters("registertemplate", (DeviceManagementSettings)RootSettings, settings, ref RegisterTemplateList);
        }

        public String Name
        {
            get
            {
                String val = GetValue("name");
                return val;
            }
        }

        public DynamicDataMap DynamicDataMap
        {
            get
            {
                if (RegisterTemplateList.Count == 0)
                    return null;
                
                return new DynamicDataMap(RegisterTemplateList);               
            }
        }

    }
}
