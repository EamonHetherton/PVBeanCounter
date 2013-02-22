/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.Text;
using System.Xml;

namespace PVSettings
{
    public class AlgorithmSettings : PVSettings.SettingsBase, INotifyPropertyChanged
    {
        private DeviceManagementSettings DeviceManagementSettings;
        private ObservableCollection<ActionSettings> _ActionList;

        public ObservableCollection<ActionSettings> ActionList { get { return _ActionList; } }

        public AlgorithmSettings(DeviceManagementSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            DeviceManagementSettings = root;
            LoadActions();
        }

        private void LoadActions()
        {
            _ActionList = new ObservableCollection<ActionSettings>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "action")
                {
                    ActionSettings action = new ActionSettings(DeviceManagementSettings, (XmlElement)e);

                    _ActionList.Add(action);
                }
            }
        }

        public String Type
        {
            get
            {
                return GetValue("type");
            }
        }

        public String Name
        {
            get
            {
                return GetValue("name");
            }
        }

        public String BlockName
        {
            get
            {
                return GetValue("blockname");
            }
        }

        public bool OnDbWriteOnly
        {
            get
            {
                String val = GetValue("ondbwriteonly");
                return val == "true";
            }
        }
    }
}
