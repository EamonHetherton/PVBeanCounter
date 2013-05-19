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
* along with PV Bean Counter.
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
            if (SystemServices != null)
                SystemServices.LogMessage(component, message, logEntryType);
        }

        public static void LogError()
        {
            //if (SystemServices != null)
                
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

    public interface IGenericStrings
    {
        string ToString(object genericValue);
        object FromString(string stringValue);
    }

    public class DateStrings : IGenericStrings
    {
        String DateFormat;
        String LegacyDateFormat;

        public DateStrings(String dateFormat, String legacyDateFormat)
        {
            DateFormat = dateFormat;
            LegacyDateFormat = legacyDateFormat;
        }

        String IGenericStrings.ToString(object inDate)
        {
            if (inDate == null)
                return "";
            else
                return ((DateTime?)inDate).Value.Date.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        object IGenericStrings.FromString(String inDateStr)
        {
            if (inDateStr == "")
                return null;

            DateTime? date = null;
            try
            {
                date = DateTime.ParseExact(inDateStr, DateFormat, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            if (LegacyDateFormat != null)
            {
                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateFormat, System.Globalization.CultureInfo.InvariantCulture);
                    return date;
                }
                catch (FormatException)
                {
                }

                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateFormat, System.Globalization.CultureInfo.CurrentCulture);
                    return date;
                }
                catch (FormatException)
                {
                }
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.CurrentCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            return date;
        }
    }

    public class DateTimeStrings : IGenericStrings
    {
        String DateTimeFormat;
        String LegacyDateTimeFormat;

        public DateTimeStrings(String dateTimeFormat, String legacyDateTimeFormat)
        {
            DateTimeFormat = dateTimeFormat;
            LegacyDateTimeFormat = legacyDateTimeFormat;
        }

        String IGenericStrings.ToString(object inDateTime)
        {
            if (inDateTime == null)
                return "";
            else
                return ((DateTime?)inDateTime).Value.ToString(DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        object IGenericStrings.FromString(String inDateStr)
        {
            if (inDateStr == "")
                return null;

            DateTime? date = null;
            try
            {
                date = DateTime.ParseExact(inDateStr, DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            if (LegacyDateTimeFormat != null)
            {
                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
                    return date;
                }
                catch (FormatException)
                {
                }

                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateTimeFormat, System.Globalization.CultureInfo.CurrentCulture);
                    return date;
                }
                catch (FormatException)
                {
                }
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.CurrentCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            return date;
        }
    }
    

    public struct GenericSetting<T> 
    {
        private SettingsBase _settings;
        private bool haveValue;
        private T _Value;
        private T _defaultValue;
        private Type coreType;
        private Type outerType;
        private string _elementName;
        private string _propertyName;
        private bool isDefault;
        private IGenericStrings GenericStrings;

        private bool GetBoolean(string val)
        {
            val = val.ToLower();
            if (val == "true")
            {
                IsDefault = false;
                return true;
            }
            else if (val == "false")
            {
                IsDefault = false;
                return false;
            }
            else
            {
                IsDefault = true;
                return (bool)Convert.ChangeType(_defaultValue, coreType);
            }
        }

        public T Value
        {
            get
            {
                if (haveValue)
                    return _Value;
                else
                {
                    string val = _settings.GetValue(_elementName);
                    if (val == "" || val == null)
                    {
                        isDefault = true;
                        return _defaultValue;
                    }
                    else
                        isDefault = false;
             
                    if (coreType == typeof(string))
                        _Value = (T)Convert.ChangeType(val, outerType);
                    else if (coreType == typeof(bool))
                        _Value = (T)Convert.ChangeType(GetBoolean(val), outerType);
                    else if (coreType == typeof(Int32) || coreType == typeof(int))
                    {
                        Int32 temp = Int32.Parse(val);
                        TypeConverter conv = TypeDescriptor.GetConverter(outerType);
                        _Value = (T)conv.ConvertFrom(temp);
                    }
                    else if (coreType == typeof(TimeSpan))
                    {
                        TimeSpan temp = TimeSpan.Parse(val);
                        TypeConverter conv = TypeDescriptor.GetConverter(outerType);
                        _Value = (T)conv.ConvertFrom(temp);
                    }
                    else if (coreType == typeof(DateTime))
                    {
                        DateTime? temp = (DateTime?)GenericStrings.FromString(val);
                        if (!temp.HasValue)
                            _Value = _defaultValue;
                        else
                        {
                            TypeConverter conv = TypeDescriptor.GetConverter(outerType);
                            _Value = (T)conv.ConvertFrom(temp);
                        }
                    }
                    else
                        throw new NotImplementedException("GenericSetting - Type: " + coreType.ToString() + " - Not available");

                    haveValue = true;
                    return _Value;
                }
            }
            set
            {
                _Value = value;
                haveValue = true;

                if (value == null)
                    _settings.SetValue(_elementName, "", _propertyName);
                else if (coreType == typeof(bool))
                    _settings.SetValue(_elementName, value.ToString().ToLower(), _propertyName);
                else if (coreType == typeof(DateTime))
                {
                    string temp;
                    TypeConverter conv = TypeDescriptor.GetConverter(typeof(DateTime?));
                    
                    DateTime? date = (DateTime?)conv.ConvertFrom(value);  // conv needed if T is DateTime rather than DateTime?
                    temp = GenericStrings.ToString(date);
                    
                    _settings.SetValue(_elementName, temp, _propertyName);
                }
                else
                    _settings.SetValue(_elementName, value.ToString(), _propertyName);
            }
        }

        public bool IsDefault { get { return isDefault; } private set { isDefault = value; } }

        public GenericSetting(T defaultValue, SettingsBase settings, string propertyName, IGenericStrings genericStrings = null)
        {
            outerType = typeof(T);
            if (outerType.IsGenericType && outerType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // drill down to natural type
                coreType = outerType.GenericTypeArguments[0];
                _defaultValue = default(T); // parameter defaultValue is ignored on nullables
            }
            else
            {
                coreType = outerType;
                _defaultValue = defaultValue;
            }

            if (genericStrings == null)
            {
                if (coreType == typeof(DateTime))
                    GenericStrings = settings.DateTimeStrings;
                else
                    GenericStrings = null;
            }
            else
                GenericStrings = genericStrings;
            
            haveValue = false;            
            _settings = settings;
            _propertyName = propertyName;
            _elementName = propertyName.ToLower();
            _Value = defaultValue;
            isDefault = true;
        }

        public GenericSetting(SettingsBase settings, string propertyName, IGenericStrings genericStrings = null) 
        {
            outerType = typeof(T);
            if (outerType.IsGenericType && outerType.GetGenericTypeDefinition() == typeof(Nullable<>))
                coreType = outerType.GenericTypeArguments[0];                
            else
                coreType = outerType;

            if (genericStrings == null)
            {
                if (coreType == typeof(DateTime))
                    GenericStrings = settings.DateTimeStrings;
                else
                    GenericStrings = null;
            }
            else
                GenericStrings = genericStrings;

            _defaultValue = default(T);
            haveValue = false;
           
            _settings = settings;
            _propertyName = propertyName;
            _elementName = propertyName.ToLower();
            _Value = _defaultValue;
            isDefault = true;
        }
    }

    public class SettingsBase : INotifyPropertyChanged
    {
        public static System.Threading.Mutex ApplicationSettingsMutex = new Mutex();

        internal SettingsBase RootSettings;

        public event PropertyChangedEventHandler PropertyChanged;

        protected XmlDocument document;
        internal XmlElement settings;

        protected const String NotDefined = "Not Defined";

        public DateStrings DateStrings;
        public DateTimeStrings DateTimeStrings;

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

            SetupBaseAfterDocument();
        }

        public void SetupBaseAfterDocument()
        {
            DateStrings = new DateStrings(DateFormat, LegacyDateFormat);
            DateTimeStrings = new DateTimeStrings(DateTimeFormat, LegacyDateTimeFormat);
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

        internal String GetValue(String nodeName)
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

        internal void SetValue(String nodeName, String value, String propertyName, bool suppressChange = false)
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

        public String DateFormat
        {
            get
            {
                return GetValue("dateformat");
            }

            set
            {
                SetValueInternal("dateformat", value);
            }
        }

        public String DateTimeFormat
        {
            get
            {
                return GetValue("datetimeformat");
            }

            set
            {
                SetValueInternal("datetimeformat", value);
            }
        }

        public String LegacyDateFormat
        {
            get
            {
                return GetValue("legacydateformat");
            }

            set
            {
                SetValueInternal("legacydateformat", value);
            }
        }

        public String LegacyDateTimeFormat
        {
            get
            {
                return GetValue("legacydatetimeformat");
            }

            set
            {
                SetValueInternal("legacydatetimeformat", value);
            }
        }

        public String DateToString(DateTime? inDate, String dateFormat = null)
        {
            if (inDate == null)
                return "";
            else if (dateFormat == null)
                return inDate.Value.Date.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);
            else
                return inDate.Value.Date.ToString(dateFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        public DateTime? StringToDate(String inDateStr)
        {
            if (inDateStr == "")
                return null;

            DateTime? date = null;
            try
            {
                date = DateTime.ParseExact(inDateStr, DateFormat, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            if (LegacyDateFormat != null)
            {
                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateFormat, System.Globalization.CultureInfo.InvariantCulture);
                    return date;
                }
                catch (FormatException)
                {
                }

                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateFormat, System.Globalization.CultureInfo.CurrentCulture);
                    return date;
                }
                catch (FormatException)
                {
                }
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.CurrentCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            return date;
        }

        public String DateTimeToString(DateTime? inDateTime, String dateTimeFormat = null)
        {
            if (inDateTime == null)
                return "";
            else if (dateTimeFormat == null)
                return inDateTime.Value.ToString(DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
            else
                return inDateTime.Value.ToString(dateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        public DateTime? StringToDateTime(String inDateStr)
        {
            if (inDateStr == "")
                return null;

            DateTime? date = null;
            try
            {
                date = DateTime.ParseExact(inDateStr, DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            if (LegacyDateTimeFormat != null)
            {
                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
                    return date;
                }
                catch (FormatException)
                {
                }

                try
                {
                    date = DateTime.ParseExact(inDateStr, LegacyDateTimeFormat, System.Globalization.CultureInfo.CurrentCulture);
                    return date;
                }
                catch (FormatException)
                {
                }
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            try
            {
                date = DateTime.Parse(inDateStr, System.Globalization.CultureInfo.CurrentCulture);
                return date;
            }
            catch (FormatException)
            {
            }

            return date;
        }
    }
}

