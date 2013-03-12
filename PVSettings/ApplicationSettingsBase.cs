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
using System.ComponentModel;
using System.Reflection;
using System.IO;

namespace PVSettings
{
    public class ApplicationSettingsBase : SettingsBase, INotifyPropertyChanged
    {
        protected String SettingsFileName;
        protected String SettingsInputFileName;
        protected String SettingsDirectory;
        protected String TemplateFileName;
        protected String TemplateName;
        protected bool UsingTemplate; // true if settings were loaded from a template
        protected bool WriteWorkingDirectory; // true if workinf dieectory must be written to registry
        protected String ElementName;
        private const String Author = "Mackay-Fisher";
        private const String Application = "PV Bean Counter";

        internal SettingsNotification PropertyChangedCallback;
        
        private SettingsNotification PropertiesSavedCallback;
        private bool CallbacksSet;

        protected List<String> LegacySettingsNames;

        public ApplicationSettingsBase(String settingsFileName, String elementName, String templateName, List<String> legacyNames = null)
        {
            LegacySettingsNames = new List<string>();
            SettingsDirectory = GetMachineRegistryValue(Author, Application, "WorkingDirectory");

            SettingsFileName = settingsFileName;
            SettingsInputFileName = settingsFileName; // this is changed if loading from a legacy file

            if (SettingsDirectory == NotDefined)
            {
                WriteWorkingDirectory = true;
                SettingsDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }
            else
                WriteWorkingDirectory = false;

            ElementName = elementName;
            TemplateName = templateName;
        }

        public void ReloadSettings()
        {
            LoadSettings();
        }

        public String ApplicationVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public String SettingsVersion
        {
            get
            {
                return GetValue("settingsversion");
            }

            private set
            {
                SetValueInternal("settingsversion", value);
            }
        }

        protected void LoadSettings(bool isConstructing = false)
        {
            bool useLegacySettings = false;

            if (!File.Exists(SettingsDirectory + @"\" + SettingsFileName))
            {
                UsingTemplate = true;
                foreach (String name in LegacySettingsNames)                    
                    if (File.Exists(SettingsDirectory + @"\" + name))
                    {
                        UsingTemplate = false;
                        SettingsInputFileName = name;
                        useLegacySettings = true;
                        break;
                    }
                
                if (UsingTemplate)
                    TemplateFileName = AppDomain.CurrentDomain.BaseDirectory + TemplateName;
            }
            else
                UsingTemplate = false;

            ApplicationSettingsMutex.WaitOne();

            XmlReader reader;

            // Create the validating reader and specify DTD validation.
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.DtdProcessing = DtdProcessing.Parse;
            readerSettings.ValidationType = ValidationType.None;
            //settings.ValidationEventHandler += eventHandler;

            if (UsingTemplate)
            {
                reader = XmlReader.Create(TemplateFileName, readerSettings);
                UsingTemplate = false;
            }
            else
                reader = XmlReader.Create(SettingsDirectory + @"\" + SettingsInputFileName, readerSettings);

            // Pass the validating reader to the XML document.
            // Validation fails due to an undefined attribute, but the 
            // data is still loaded into the document.

            document = new XmlDocument();
            //settings = new XmlDocument();
            document.Load(reader);
            reader.Close();

            bool found = false;

            foreach (XmlNode n in document.ChildNodes)
                if (n.Name == ElementName)
                {
                    settings = (XmlElement)n;
                    found = true;
                    break;
                }

            if (!found)
                throw new Exception("Cannot find element '" + ElementName + "'");

            if (!isConstructing)
                LoadSettingsSub();

            if (useLegacySettings)
                RemoveLegacyElements();

            ApplicationSettingsMutex.ReleaseMutex();

            CallbacksSet = false;

            if (DateFormat != "yyyy-MM-dd")
            {
                if (DateFormat != "")
                    LegacyDateFormat = DateFormat;
                DateFormat = "yyyy-MM-dd";
            }

            if (DateTimeFormat != "yyyy'-'MM'-'dd'T'HH':'mm':'ss")
            {
                if (DateTimeFormat != "")
                    LegacyDateTimeFormat = DateTimeFormat;
                DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss";
            }
        }

        protected virtual void RemoveLegacyElements()
        {
        }

        protected virtual void LoadSettingsSub()
        {
        }

        public virtual void SaveSettings()
        {
            if (WriteWorkingDirectory)
            {
                SetMachineRegistryValue(Author, Application, "WorkingDirectory", SettingsDirectory);
                WriteWorkingDirectory = false;
            }
            SettingsVersion = ApplicationVersion;
            document.Save(SettingsDirectory + @"\" + SettingsFileName);
            SettingsSavingEventHandler();
        }

        private void SettingsSavingEventHandler()
        {
            if (CallbacksSet)
                PropertiesSavedCallback();
        }

        protected void CancelSaveSettings()
        {
            if (CallbacksSet)
                PropertiesSavedCallback();
        }

        public override void SettingChangedEventHandler(String name)
        {
            if (PropertyChangedCallback != null)
                PropertyChangedCallback();
        }

        public void SetNotifications(SettingsNotification propertyChangedCallback, SettingsNotification propertiesSavedCallback)
        {
            PropertiesSavedCallback = propertiesSavedCallback;
            PropertyChangedCallback = propertyChangedCallback;
            CallbacksSet = true;
        }

        public static String BuildFileName(String fileName, String defaultDirectory)
        {
            if (Path.IsPathRooted(fileName))
                return Path.GetFullPath(fileName);
            else
                return Path.GetFullPath(defaultDirectory + @"\" + fileName);
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

        private String LegacyDateFormat
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

        private String LegacyDateTimeFormat
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

        public String DateTimeToString(DateTime? inDateTime, String dateTimeFormat = null)
        {
            if (inDateTime == null)
                return "";
            else if (dateTimeFormat == null)
                return inDateTime.Value.ToString(DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
            else
                return inDateTime.Value.ToString(dateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
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
