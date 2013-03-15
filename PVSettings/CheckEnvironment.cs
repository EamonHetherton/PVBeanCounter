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
using System.Linq;
using System.Text;
using System.Security.Permissions;
using System.Security;
using System.IO;
using System.Security.AccessControl;
using PVSettings;
using System.ServiceProcess;
using MackayFisher.Utilities;

namespace PVSettings
{
    public class CheckEnvironment
    {
        ApplicationSettings Settings;
        SystemServices SystemServices;

        ServiceManager PVBCService;
        ServiceManager PublisherService;

        const String NewLine = " \r\n";

        public CheckEnvironment(ApplicationSettings settings, SystemServices services)
        {
            Settings = settings;
            SystemServices = services;
        }

        private String SetDirectoryAccess(String directoryName)
        {
            String name = "not set";
            try
            {
                name = PVBCService.ServiceAccountSecurityName;
                DirectorySecurity dirSecurity = Directory.GetAccessControl(directoryName);
                FileSystemAccessRule allowRule = new FileSystemAccessRule(name,
                    FileSystemRights.FullControl, AccessControlType.Allow);
                dirSecurity.AddAccessRule(allowRule);
                Directory.SetAccessControl(directoryName, dirSecurity);
            }
            catch (Exception e)
            {
                return "Cannot set Full Control permission on: '" + directoryName + "' for " + name + " - Error: " + e.Message;
            }

            return "";
        }

        private String SetFileAccess(String fileName)
        {
            String name = "not set";
            try
            {
                name = PVBCService.ServiceAccountSecurityName;
                FileSecurity fileSecurity = File.GetAccessControl(fileName);
                FileSystemAccessRule allowRule = new FileSystemAccessRule(name,
                    FileSystemRights.FullControl, AccessControlType.Allow);
                fileSecurity.AddAccessRule(allowRule);
                File.SetAccessControl(fileName, fileSecurity);
            }
            catch (Exception e)
            {
                return "Cannot set Full Control permission on: '" + fileName + "' for " + name + " - Error: " + e.Message;
            }

            return "";
        }

        public String LocateOrCreateDirectory(String directoryName, out DirectoryInfo dirInfo)
        {
            DirectoryInfo info = new DirectoryInfo(directoryName);
            dirInfo = info;

            if (!info.Exists)
            {
                info.Create();

                return "Directory Created: " + directoryName;
            }
            else
                return "Directory Located: " + directoryName;
        }

        private bool CheckSunnyExplorerConfig(out String log)
        {
            log = "";

            String fileName = Path.Combine(Settings.DefaultDirectory, "SunnyExplorer.sx2");

            try
            {
                FileInfo info = new FileInfo(fileName);
                if (info.Exists)
                {
                    log += "File '" + fileName + "' located";
                }
                else
                {
                    log += "File '" + fileName + "' NOT located - this file must be created for SMA inverter access";
                    return false;
                }
            }
            catch (FileNotFoundException)
            {
                log += "File '" + fileName + "' NOT located - this file must be created for SMA inverter access";
                return false;
            }
            catch (Exception e)
            {
                log += "Error locating: '" + fileName + "' - Exception: " + e.Message;
                return false;
            }

            String result = SetFileAccess(fileName);

            if (result == "")
                result = "Assigned full control to " + PVBCService.ServiceAccountName + " on: " + fileName;
            else
            {
                log = result + NewLine + result;
                return false;
            }

            log += NewLine + result;
            return true;
        }

        private bool CheckSunnyExplorer(DeviceManagerSettings dmSettings, out String log)
        {
            log = "";

            try
            {
                FileInfo info = new FileInfo(dmSettings.ExecutablePath);
                if (info.Exists)
                {
                    log += "Sunny Explorer located";
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (Exception e)
            {
                log += "Error checking Sunny Explorer at: " + dmSettings.ExecutablePath + " - Exception: " + e.Message;
            }

            String newPath = @"C:\Program Files (x86)\SMA\Sunny Explorer\SunnyExplorer.exe";

            try
            {
                FileInfo info = new FileInfo(newPath);
                if (info.Exists)
                {
                    dmSettings.ExecutablePath = newPath;
                    log += "Sunny Explorer located at '" + newPath + "'";
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (Exception e)
            {
                log += "Error checking Sunny Explorer at: " + newPath + " - Exception: " + e.Message;
            }

            newPath = @"C:\Program Files\SMA\Sunny Explorer\SunnyExplorer.exe";

            try
            {
                FileInfo info = new FileInfo(newPath);
                if (info.Exists)
                {
                    dmSettings.ExecutablePath = newPath;
                    log += "Sunny Explorer located at '" + newPath + "'";
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (Exception e)
            {
                log += "Error checking Sunny Explorer at: " + newPath + " - Exception: " + e.Message;
            }

            log += NewLine + "Sunny Explorer not located - Please install Sunny Explorer";

            return false;
        }

        public bool SetupEnvironment(out String log)
        {
            bool retVal = true;
            string stage = "Start";
            String result = "";

            PVBCService = new ServiceManager("PVService");
            PublisherService = new ServiceManager("PVPublisherService");

            PVBCService.ServiceAccountName = Settings.ServiceAccountName.Trim();

            Settings.ServiceDetailsChanged |= (!PVBCService.ServiceAccountInSync);

            try
            {
                String defaultDirectoryName = Settings.DefaultDirectory;
                DirectoryInfo defaultDirectoryInfo;

                stage = "Locate or Create Default Directory";
                try
                {
                    result = LocateOrCreateDirectory(defaultDirectoryName, out defaultDirectoryInfo);
                }
                catch (SecurityException e)
                {
                    log = "Security Exception locating or creating directory: " + defaultDirectoryName + " - message: " + e.Message;
                    return false;
                }
                catch (Exception e)
                {
                    log = "Exception locating or creating directory: " + defaultDirectoryName + " - message: " + e.Message;
                    return false;
                }

                stage = "Locate or Create Archive Directory";
                String archiveName = Path.Combine(defaultDirectoryName, "Archive");
                try
                {
                    DirectoryInfo archiveDirectoryInfo;
                    result += (NewLine + LocateOrCreateDirectory(archiveName, out archiveDirectoryInfo));

                }
                catch (SecurityException e)
                {
                    log = "Security Exception locating or creating directory: " + archiveName + " - message: " + e.Message;
                    return false;
                }
                catch (Exception e)
                {
                    log = "Exception locating or creating directory: " + archiveName + " - message: " + e.Message;
                    return false;
                }

                stage = "Set Access to Default Directory";
                String accessResult = SetDirectoryAccess(defaultDirectoryName);

                if (accessResult == "")
                    result += NewLine + "Assigned full control to " + PVBCService.ServiceAccountName + " on: " + defaultDirectoryName;
                else
                {
                    result += NewLine + accessResult;
                    retVal = false;
                }

                stage = "Set Access to Archive Directory";
                accessResult = SetDirectoryAccess(archiveName);

                if (accessResult == "")
                    result += NewLine + "Assigned full control to " + PVBCService.ServiceAccountName + " on: " + archiveName;
                else
                {
                    retVal = false;
                    result += NewLine + accessResult;
                }

                stage = "Locate Empty Database Directory";
                String patternDirName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Empty Databases");
                DirectoryInfo patternDir;
                try
                {
                    patternDir = new DirectoryInfo(patternDirName);
                    if (!patternDir.Exists)
                    {
                        log = result + NewLine + "Cannot locate directory '" + patternDirName + "'";
                        return false;
                    }

                }
                catch (SecurityException e)
                {
                    log = result + NewLine + "Security Exception locating directory: " + patternDirName + " - message: " + e.Message;
                    return false;
                }
                catch (Exception e)
                {
                    log = result + NewLine + "Exception locating directory: " + patternDirName + " - message: " + e.Message;
                    return false;
                }

                // No action on non-standard settings - only create empty database for Jet or SQLite installs where DB does not exist
                if (Settings.StandardDBType != "Custom")
                    if (Settings.DatabaseType == "Jet" || Settings.DatabaseType == "SQLite")
                    {
                        stage = "Create Empty Database";
                        String srcName = Path.Combine(patternDirName, Settings.Database);
                        String destName = Path.Combine(defaultDirectoryName, Settings.Database);
                        try
                        {
                            File.Copy(srcName, destName, false);
                            result += NewLine + "Created empty database: " + destName;
                        }
                        catch (IOException) // file already exists
                        {
                            result += NewLine + "Database not changed - already exists: " + destName;
                        }

                        stage = "Set Access to Database";
                        accessResult = SetFileAccess(destName);

                        if (accessResult == "")
                            result += NewLine + "Assigned full control to " + PVBCService.ServiceAccountName + " on: " + destName;
                        else
                        {
                            log = result + NewLine + accessResult;
                            return false;
                        }

                        if (Settings.StandardDBType == "Jet (2003)")
                            result += NewLine + "Ensure PV Bean Counter 32bit and Microsoft Access 2003 are installed";
                        else if (Settings.StandardDBType == "Jet (2007)")
                            result += NewLine + "Ensure Microsoft Access 2007 or later is installed";
                    }
                    else
                        result += NewLine + "Your selected database type requires manual configuration: " + Settings.StandardDBType;
                else
                    result += NewLine + "You have a 'Custom' database configuration - Manual setup required";

                stage = "Set Access to settings.xml";
                String settingsName = Path.Combine(defaultDirectoryName, "settings.xml");
                if (File.Exists(settingsName))
                {
                    accessResult = SetFileAccess(settingsName);
                    if (accessResult == "")
                        result += NewLine + "Assigned full control to " + PVBCService.ServiceAccountName + " on: " + settingsName;
                    else
                    {
                        result += NewLine + "Cannot set permissions on: " + settingsName;
                        retVal = false;
                    }
                }
                
                stage = "Check Meter Warnings";
                /*
                foreach (MeterManagerSettings meter in Settings.MeterManagerList)
                {
                    if (meter.Enabled && meter.ManagerTypeInternal == MeterManagerSettings.MeterManagerType.CurrentCost)
                    {
                        if (((CCMeterManagerSettings)meter).PortName == "")
                        {
                            result += NewLine + "You cannot monitor Current Cost Meters without selecting a Meter Port";
                                
                            retVal = false;
                        }
                        else if (!SerialPortSettings.SerialPortsList.Contains(((CCMeterManagerSettings)meter).PortName))
                        {
                            result += NewLine + "Meter Port not found - Connect meter hardware";
                            retVal = false;
                        }
                    }
                    else if (meter.Enabled && meter.ManagerTypeInternal == MeterManagerSettings.MeterManagerType.Owl)
                    {
                        string dbName = ((OwlMeterManagerSettings)meter).OwlDatabase;
                        if (dbName == "")
                        {
                            result += NewLine + "You cannot monitor Owl Meters without locating the Owl database";
                                
                            retVal = false;
                        }
                        else if (File.Exists(dbName))
                        {
                            accessResult = SetFileAccess(dbName);
                            if (accessResult == "")
                                result += NewLine + "Assigned full control to " + PVBCService.ServiceAccountName + " on: " + dbName;
                            else
                            {
                                result += NewLine + "Cannot set permissions on: " + dbName;
                                retVal = false;
                            }
                        }
                        else
                        {
                            result += NewLine + "Specified Owl database does not exist - check file location";
                                
                            retVal = false;
                        }
                    }
                }
                */

                stage = "Check Inverter Warnings";
                
                bool useSunnyExplorer = false;
                DeviceManagerSettings deviceManagerSettings = null;

                foreach (DeviceManagerSettings dmSettings in Settings.DeviceManagerList)
                {
                    if (dmSettings.ManagerType == DeviceManagerType.SMA_SunnyExplorer
                        && dmSettings.Enabled)
                    {
                        deviceManagerSettings = dmSettings;
                        useSunnyExplorer = true;
                    }
                }

                if (useSunnyExplorer)
                {
                    if (CheckSunnyExplorer(deviceManagerSettings, out accessResult))
                    {
                        if (accessResult != "")
                            result += NewLine + accessResult;
                    }
                    else
                    {
                        retVal = false;
                        result += NewLine + accessResult;
                    }

                    retVal &= CheckSunnyExplorerConfig(out accessResult);
                    result += NewLine + accessResult;

                    String plantFileName = deviceManagerSettings.SunnyExplorerPlantName + "-20110131.csv";
                    try
                    {
                        String fileName = Path.Combine(defaultDirectoryName, plantFileName);
                    }
                    catch (ArgumentException)
                    {
                        retVal = false;
                        result += NewLine + "Cannot form a legal filename based on the supplied Sunny Explorer Plant Name: '" + plantFileName + "'";
                    }
                }

                stage = "Check pvoutput Warnings";
                if (Settings.EnablePVOutput)
                {
                    bool siteIdMissing = false;
                    bool apiKeyMissing = false;

                    foreach (PvOutputSiteSettings setting in Settings.PvOutputSystemList)
                    {
                        if (setting.Enable)
                        {
                            siteIdMissing |= (setting.SystemId == "-1");
                            apiKeyMissing |= (setting.APIKey == "");
                        }
                    }

                    if (siteIdMissing)
                    {
                        retVal = false;
                        result += NewLine + "pvoutput.org System ID is required for pvoutput upload";
                    }

                    if (apiKeyMissing)
                    {
                        retVal = false;
                        result += NewLine + "pvoutput.org API Key is required for pvoutput upload";
                    }

                    result += NewLine + "Check that 'Status Interval' on the 'pvoutput.org Settings' tab matches 'Status Interval' at pvoutput.org";
                }

                stage = "Test Database";
                TestDatabase testDatabase = new TestDatabase(Settings, SystemServices);
                String dbStage = "";
                Exception dbException = null;
                String dbTestResult = testDatabase.RunDatabaseTest(ref dbStage, ref dbException);
                if (dbTestResult == "Success")
                    result += NewLine + "Database read test OK";
                else
                {
                    retVal = false;
                    result += NewLine + dbTestResult;
                }

                bool res;
                if (Settings.ServiceDetailsChanged)
                {
                    stage = "Setup Service";

                    if (!Settings.ServiceAccountRequiresPassword || Settings.ServiceAccountPassword.Trim() != "")
                    {
                        if (PVBCService.ServiceAccountType == ServiceAccount.User)
                        {
                            bool retry = false;
                            try
                            {
                                LsaWrapper.LsaWrapperCaller.AddPrivileges(PVBCService.ServiceAccountSecurityName, "SeServiceLogonRight");
                            }
                            catch
                            {
                                retry = true;
                            }
                            if (retry)
                                LsaWrapper.LsaWrapperCaller.AddPrivileges(PVBCService.ServiceAccountSecurityName, "SeServiceLogonRight");
                        }

                        res = PVBCService.SyncServiceCredentials( Settings.ServiceAccountPassword.Trim());
                        if (!res)
                        {
                            result += NewLine + "Cannot set PVService credentials - check that the user name and password are correct";
                            retVal = false;
                        }
                        else
                        {
                            result += NewLine + "PVService credentials set to " + PVBCService.ServiceAccountName;
                            //Settings.KnownServiceAccountName = Settings.ServiceAccountName;
                            Settings.ServiceDetailsChanged = false;
                        }
                    }
                    else
                    {
                        result += NewLine + "Cannot set PVService credentials - password not provided";
                        retVal = false;
                    }
                }

                res = PVBCService.SyncServiceStartup(Settings.AutoStartPVBCService);
                if (!res)
                {
                    result += NewLine + "Cannot set PVService startup type";
                    retVal = false;
                }

                res = PublisherService.SyncServiceStartup(Settings.AutoStartPVBCService);
                if (!res)
                {
                    result += NewLine + "Cannot set PVPublisherService startup type";
                    retVal = false;
                }

                if (retVal)
                    result += NewLine + NewLine + "Configuration Passed Checks";
                else
                    result += NewLine + NewLine + "Configuration Failed Checks - Review messages above";

                stage = "Output Log Result";

                log = result;
            }
            catch (Exception e)
            {
                log = "Unexpected Exception: " + e.Message + " - Stage: " + stage + " - Log: " + result;
                retVal = false;
            }
            return retVal;
        }

        public bool CheckDatabaseExists(out String message)
        {
            message = "";
            if (Settings.DatabaseType == "Jet" || Settings.DatabaseType == "SQLite")
            {
                try
                {
                    String database = Path.Combine(Settings.DefaultDirectory, Settings.Database);
                    if (System.IO.File.Exists(database))
                    {
                        FileInfo info = new FileInfo(database);
                        if (info.IsReadOnly)
                        {
                            message = "Database file: '" + database + "' - is read only";
                            return false;
                        }
                        else
                            return true;
                    }
                    else
                    {
                        message = "Database file: '" + database + "' - does not exist or is not visible";
                        return false;
                    }

                }
                catch (Exception e)
                {
                    message = "Exception checking database file: " + e.Message;
                    return false;
                }
            }

            return true;
        }
    }
}
