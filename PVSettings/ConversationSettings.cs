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
using Conversations;

namespace PVSettings
{
    public class ConversationSettings : SettingsBase, INotifyPropertyChanged
    {
        private DeviceManagementSettings DeviceManagementSettings;
        private ObservableCollection<MessageSettings> _MessageList;

        public ConversationSettings(DeviceManagementSettings root, XmlElement element, String typeName = null)
            : base(root, element)
        {
            DeviceManagementSettings = root;
            LoadMessages();
        }

        private void LoadMessages()
        {
            _MessageList = new ObservableCollection<MessageSettings>();
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == "message")
                {
                    MessageSettings message = new MessageSettings(DeviceManagementSettings, (XmlElement)e);
                    _MessageList.Add(message);
                }
            }

        }

        public ObservableCollection<MessageSettings> MessageList { get { return _MessageList; } }

        public String Name
        {
            get
            {
                String val = GetValue("name");
                return val;
            }
        }

    }
}
