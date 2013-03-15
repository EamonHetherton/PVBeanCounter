/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading;
using Microsoft.Win32;
using MackayFisher.Utilities;

namespace PVSettings
{
    public class GlobalSettings
    {
        public static SystemServices SystemServices;
        public static ApplicationSettings ApplicationSettings;
        public static GenericConnector.GenDatabase TheDB;

        public static void LogMessage(String component, String message, LogEntryType logEntryType = LogEntryType.Trace)
        {
            SystemServices.LogMessage(component, message, logEntryType);
        }
    }

    public delegate void SettingsNotification();

    public enum AutoDBTypes
    {
        Jet_2003,
        Jet_2007,
        SQLite,
        MySQL,
        SQLServer
    }

    public class SettingsBase : INotifyPropertyChanged
    {
        public static System.Threading.Mutex ApplicationSettingsMutex = new Mutex();

        internal SettingsBase RootSettings;

        public event PropertyChangedEventHandler PropertyChanged;

        protected XmlDocument document;
        internal XmlElement settings;

        protected const String NotDefined = "Not Defined";

        // Used to construct ApplicationsSettingsBase only 
        // All others must use - 
        // public SettingsBase(ApplicationSettingsBase rootSettings, XmlElement element)
        public SettingsBase()
        {
            document = null;
            settings = null;
        }

        public SettingsBase(SettingsBase rootSettings, XmlElement element)
        {
            RootSettings = rootSettings;
            document = element.OwnerDocument;
            settings = element;
        }

        public void SetDocument(XmlDocument newDocument)
        {
            document = newDocument;
        }

        public void DeleteSettings()
        {
            if (settings != null)
                settings.ParentNode.RemoveChild(settings);
        }

        public void OnPropertyChanged(SettingsBase obj, PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(obj, e);
        }

        public virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        protected XmlElement GetElement(String nodeName)
        {
            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == nodeName)
                    return (XmlElement)e;
            }

            return null;
        }

        protected bool DeleteElement(String nodeName)
        {

            foreach (XmlNode e in settings.ChildNodes)
            {
                if (e.NodeType == XmlNodeType.Element && e.Name == nodeName)
                {
                    settings.RemoveChild(e);
                    return true;
                }
            }

            return false;
        }

        protected String GetValue(String nodeName)
        {
            ApplicationSettingsMutex.WaitOne();
            try
            {
                XmlElement elem = GetElement(nodeName);
                String tmp;
                if (elem == null)
                    tmp = "";
                else
                    tmp = elem.Attributes["value"].Value;
                ApplicationSettingsMutex.ReleaseMutex();
                return tmp;
            }
            catch
            {
            }
            ApplicationSettingsMutex.ReleaseMutex();
            return "";
        }

        protected String GetNullableValue(String nodeName)
        {
            ApplicationSettingsMutex.WaitOne();
            try
            {
                XmlElement elem = GetElement(nodeName);
                String tmp;
                if (elem == null)
                    tmp = null;
                else
                    tmp = elem.Attributes["value"].Value;
                ApplicationSettingsMutex.ReleaseMutex();
                return tmp;
            }
            catch
            {
            }
            ApplicationSettingsMutex.ReleaseMutex();
            return "";
        }

        public virtual void SettingChangedEventHandler(String name)
        {
            RootSettings.SettingChangedEventHandler(name);
        }

        protected void SetValueInternal(String nodeName, String value)
        {
            ApplicationSettingsMutex.WaitOne();
            try
            {
                GetElement(nodeName).Attributes["value"].Value = value;
            }
            catch
            {
                AddElement(nodeName, value);
            }
            ApplicationSettingsMutex.ReleaseMutex();
        }

        protected void SetPropertyChanged(String propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            SettingChangedEventHandler("");
        }

        protected void DoPropertyChanged(String propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        protected void SetValue(String nodeName, String value, String propertyName, bool suppressChange = false)
        {
            ApplicationSettingsMutex.WaitOne();
            try
            {
                XmlElement elem = GetElement(nodeName);
                if (elem == null)
                    AddElement(nodeName, value);
                else
                    elem.Attributes["value"].Value = value;
                // OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            }
            catch
            {
            }
            // moved here to run after Add
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            ApplicationSettingsMutex.ReleaseMutex();
            if (!suppressChange)
                SettingChangedEventHandler(nodeName);
        }

        internal void DeleteValue(String nodeName)
        {
            ApplicationSettingsMutex.WaitOne();
            try
            {
                DeleteElement(nodeName);
            }
            catch
            {
            }
            ApplicationSettingsMutex.ReleaseMutex();
        }

        protected XmlElement AddElement(String elementName, String value)
        {
            XmlElement e;
            XmlAttribute a;

            e = document.CreateElement(elementName);
            a = document.CreateAttribute("value");

            a.Value = value;
            e.Attributes.Append(a);

            settings.AppendChild(e);
            return e;
        }

        protected XmlElement AddElement(XmlElement parent, String elementName, String value = null)
        {
            XmlElement e;

            e = document.CreateElement(elementName);
            if (value != null)
            {
                XmlAttribute a = document.CreateAttribute("value");
                a.Value = value;
                e.Attributes.Append(a);
            }

            parent.AppendChild(e);
            return e;
        }

        protected XmlElement AddElement(XmlElement parent, String elementName, XmlElement beforeElement)
        {
            XmlElement e;

            e = document.CreateElement(elementName);

            parent.InsertBefore(e, beforeElement);
            return e;
        }

        internal static bool ElementHasChild(XmlNode target, String childName, String childValue)
        {
            foreach (XmlNode child in target.ChildNodes)
            {
                if (child.Name == childName)
                {
                    foreach (XmlNode childChild in child.Attributes)
                    {
                        if (childChild.Name == "value")
                            return childChild.Value == childValue;
                    }
                }
            }
            return false;
        }

        protected static void SetMachineRegistryValue(String author, String application, String name, String value)
        {
            Microsoft.Win32.RegistryKey macFishKey;
            Microsoft.Win32.RegistryKey software = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE", true);
            macFishKey = software.CreateSubKey(author);
            Microsoft.Win32.RegistryKey pvbcKey;
            pvbcKey = macFishKey.CreateSubKey(application);
            pvbcKey.SetValue(name, value);
            pvbcKey.Close();
            macFishKey.Close();
            software.Close();
        }

        protected static String GetMachineRegistryValue(String author, String application, String name)
        {
            Microsoft.Win32.RegistryKey macFishKey;
            Microsoft.Win32.RegistryKey software = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE");
            macFishKey = software.OpenSubKey(author);
            if (macFishKey == null)
                return NotDefined;
            Microsoft.Win32.RegistryKey pvbcKey;
            pvbcKey = macFishKey.OpenSubKey(application);
            if (pvbcKey == null)
                return NotDefined;
            String value = (String)pvbcKey.GetValue(name, NotDefined);
            pvbcKey.Close();
            macFishKey.Close();
            software.Close();
            return value;
        }
    }
}

