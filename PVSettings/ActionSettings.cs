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
    public class ActionSettings : SettingsBase, INotifyPropertyChanged
    {
        private DeviceManagementSettings DeviceManagementSettings;
        private ObservableCollection<ActionSettings> Actions;
        private ObservableCollection<ParameterSettings> Parameters;

        public ObservableCollection<ActionSettings> ActionList { get { return Actions; } }
        public ObservableCollection<ParameterSettings> ParameterList { get { return Parameters; } }

        public ActionSettings(DeviceManagementSettings root, XmlElement element)
            : base(root, element)
        {
            DeviceManagementSettings = root;
            LoadActions();
            LoadParameters();
        }

        private void LoadActions()
        {
            Actions = new ObservableCollection<ActionSettings>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "action")
                {
                    ActionSettings action = new ActionSettings(DeviceManagementSettings, (XmlElement)e);

                    Actions.Add(action);
                }
            }
        }

        private void LoadParameters()
        {
            Parameters = new ObservableCollection<ParameterSettings>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "parameter")
                {
                    ParameterSettings par = new ParameterSettings(DeviceManagementSettings, (XmlElement)e);

                    Parameters.Add(par);
                }
            }
        }

        public String Name
        {
            get
            {
                return GetValue("name");
            }
        }

        public String Type
        {
            get
            {
                return GetValue("type");
            }
        }

        public String BlockName
        {
            get
            {
                return GetValue("blockname");
            }
        }

        public String Count
        {
            get
            {
                return GetValue("count");
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

        public bool ContinueOnFailure
        {
            get
            {
                bool isSuccessExit = ExitOnSuccess;
                if (isSuccessExit)  // cannot abort on message failure
                    return true;

                String val = GetValue("continueonfailure");
                return val == "true";
            }
        }

        public bool ExitOnSuccess
        {
            get
            {
                String val = GetValue("exitonsuccess");
                return val == "true";
            }
        }

        public bool ExitOnFailure
        {
            get
            {
                String val = GetValue("exitonfailure");
                return val == "true";
            }
        }
    }
}
