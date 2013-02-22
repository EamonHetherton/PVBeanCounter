/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using GenericConnector;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using DeviceDataRecorders;
using PVBCInterfaces;
using Device;

namespace PVInverterManagement
{
    public abstract class InverterManager : GenThread, PVBCInterfaces.IDeviceManager
    {
        // the following are loaded from the 
        //invertermanager table by the 
        //base class constructor
        protected string ConfigFileName = null;       // used if extract utility requires a configuration file
        protected DateTime? NextFileDate = null;      // specifies the next DateTime to be used for extract
        protected string OutputDirectory;               // directory where extract files will be written
        protected string ArchiveDirectory;              // directory where extract files will be moved when processed
        protected string FileNamePattern = null;      // pattern used to identify extract files in outputDirectory
        protected string Password = null;             // used for devices requiring a password for data extraction

        public virtual int IntervalSeconds { get { return 300; } } // Interval size in OutputHistory (not the thread run interval)

        public ErrorLogger ErrorLogger { get; private set; }

        public IDataRecorder DataRecorder { get { return InverterDataRecorder; } }
        public DataRecorder InverterDataRecorder = null;

        private List<IDevice>  DeviceListInternal;
        public List<IDevice> DeviceList { get { return DeviceListInternal; } }

        public InverterManagerSettings InverterManagerSettings { get; private set; }
        public int InverterManagerID{ get; private set;}
        public IManagerManager ManagerManager { get; private set; }
        public IManagerManager InverterManagerManager { get { return ManagerManager; } }

        public abstract String InverterManagerType{ get; }

        protected bool Continuous;
        protected bool EmitEvents;

        protected void LogMessage(String component, String message, LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage(component, message, logEntryType);
        }

        public String Description { get { return InverterManagerSettings.Description; } }

        public override String ThreadName { get { return Description; } }

        public String ManagerTypeName { get { return InverterManagerSettings.ManagerTypeName; } }
        public int InstanceNo { get { return InverterManagerSettings.InstanceNo; } }
        public String ManagerId { get { return InverterManagerSettings.Description; } }

        public InverterManager(GenThreadManager genThreadManager, int inverterManagerId,
            InverterManagerSettings imSettings, IManagerManager imm, bool useDefaultHistoryUpdater = true) : base(genThreadManager, GlobalSettings.SystemServices)
        {
            //InverterStatusList = new List<DeviceStatus>();
            DeviceListInternal = new List<IDevice>();

            InverterManagerSettings = imSettings;
            EmitEvents = GlobalSettings.ApplicationSettings.EmitEvents;
            InverterManagerID = inverterManagerId;
            ManagerManager = imm;
            Continuous = false;

            String directory = GlobalSettings.ApplicationSettings.InverterLogs;
            if (directory == "" || directory == null)
                directory = System.IO.Path.Combine(GlobalSettings.ApplicationSettings.DefaultDirectory, "ErrorLogs");
            else if (System.IO.Path.IsPathRooted(directory))
                directory = System.IO.Path.Combine(directory);
            else
                directory = System.IO.Path.Combine(GlobalSettings.ApplicationSettings.DefaultDirectory, directory);

            ErrorLogger = new ErrorLogger(GlobalSettings.SystemServices, InverterManagerType, imSettings.Description, directory);

            LogMessage("InverterManager", "Id = " + imSettings.Description + " loading", LogEntryType.Trace);
           
            OutputDirectory = GlobalSettings.ApplicationSettings.DefaultDirectory;
            ArchiveDirectory = "Archive";
          
            try
            {
                GenConnection con = GlobalSettings.TheDB.NewConnection();
                GenDataReader dataReader;
                String cmdStr =
                    "select NextFileDate " +
                    "from invertermanager " +
                    "where Id = @ManagerId;";

                GenCommand cmd = new GenCommand(cmdStr, con);
                cmd.AddParameterWithValue("@ManagerId", inverterManagerId);
                dataReader = (GenDataReader)cmd.ExecuteReader(System.Data.CommandBehavior.SingleRow);
               
                if (dataReader.Read())
                {
                    try
                    {
                        int ord;

                        // Note - the current r.GetXXXX functions do not handle nulls - even on nullable data types like String
                        // workaround is to use IsDBNull

                        if (dataReader.IsDBNull(ord = dataReader.GetOrdinal("NextFileDate")))
                            NextFileDate = null;
                        else
                            NextFileDate = dataReader.GetDateTime(ord);
                    }
                    catch (Exception e)
                    {
                        throw new PVException(PVExceptionType.UnexpectedDBError, "InverterManager: " + e.Message, e);
                    }

                    ConfigureInverterManager();
                }
                else
                    throw new PVException(PVExceptionType.UnexpectedDBError, "InverterManager: inverter manager not found");

                dataReader.Close();
                con.Close();

                LoadLocalInverters(imSettings); // Modify Inverters here if required
                
                InverterDataRecorder = new DataRecorder(this, InverterManagerSettings.FirstFullDay, useDefaultHistoryUpdater);

                LogMessage("InverterManager", "Id = " + inverterManagerId + " loaded", LogEntryType.Information);
            }
            catch(PVException e)
            {
                throw e;
            }
            catch(Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "InverterManager: " + e.Message, e);
            }
        }

        protected virtual IDevice GetNewDevice(int? deviceId, String make, String model, String serialNo, ulong? address)
        {
            PassiveDevice dev = new PseudoDevice(this);
            if (address.HasValue)
                dev.Address = address.Value;

            dev.Make = make;
            dev.Model = model;
            dev.SerialNo = serialNo;
            dev.DeviceId = deviceId;

            return dev;
        }

        // use the following when device identity is known
        public IDevice GetKnownDevice(GenConnection con, ulong address, String make, String model, String serialNo)
        {
            foreach (IDevice knownDevice in DeviceList)
            {
                if (knownDevice.Manufacturer == make && knownDevice.Model == model && knownDevice.SerialNo == serialNo)
                    return knownDevice;
            }

            String siteId;
            int deviceId = InverterDataRecorder.GetDeviceId(make, model, serialNo, con, true, out siteId);

            IDevice newDev = GetNewDevice(deviceId, make, model, serialNo, address);

            DeviceList.Add(newDev);
            return newDev;
        }

        // use the following when deviceidentity is not known
        protected IDevice NewUnknownDevice(UInt64 address, bool autoAdd = true)
        {
            IDevice newDev = GetNewDevice(null, "", "", "", address);

            if (autoAdd)
                DeviceList.Add(newDev);
            return newDev;
        }

        protected virtual bool HasPhoenixtecStartOfDayEnergyDefect { get { return false; } }

        protected virtual void LoadLocalInverters(InverterManagerSettings imSettings)
        {
            bool sodd = HasPhoenixtecStartOfDayEnergyDefect;
            Double crazyDayStartMinutes = 0.0;
            if (sodd)
                crazyDayStartMinutes = imSettings.CrazyDayStartMinutes.HasValue ? imSettings.CrazyDayStartMinutes.Value : 90.0;
            foreach (DeviceManagerDeviceSettings iSetting in InverterManagerSettings.InverterList)
                if (iSetting.Enabled)
                {
                    IDevice gi = NewUnknownDevice(iSetting.Address);
                    gi.HasStartOfDayEnergyDefect = sodd;
                    gi.CrazyDayStartMinutes = crazyDayStartMinutes;
                }
        }

        protected virtual void ConfigureInverterManager()
        {
            try
            {
                // default configuration - if no override
            }
            catch (PVException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "InverterManager.ConfigureInverterManager: " + e.Message, e);
            }
        }

        public override void Initialise()
        {
            base.Initialise();
            //HistoryUpdater = new PVHistoryUpdate(InverterDataRecorder, SystemServices);
            LogMessage("Initialise", "Inverter Manager - Id = " + ManagerId + " - manager running", LogEntryType.StatusChange);
        }

        protected virtual int ExtractYield()
        {
            throw new NotImplementedException("ExtractYield in base class not useable");
        }

        public virtual void DoInverterWork()
        {
            String state = "start";
            try
            {
                int res = 0;

                state = "before GetMutex";
                GlobalSettings.SystemServices.GetDatabaseMutex();
                try
                {
                    res = ExtractYield();

                    /*
                    state = "before SetPVOutputReady";
                    if (res > 0)
                        SetPVOutputReady();
                    */
                }
                catch (Exception e)
                {
                    LogMessage("DoInverterWork", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
                    //throw (e);
                }
                finally
                {
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                }
            }
            catch (System.Threading.ThreadInterruptedException)
            {
                // LogMessage("DoInverterWork - Inverter Manager - Id = " + InverterManagerID + " - interrupted", LogEntryType.ErrorMessage);
                //HistoryUpdater = null;
            }
        }

        public override bool DoWork()
        {
            try
            {
                // Check that inverter managers should be running now
                if (!ManagerManager.RunInverterManagers)
                {
                    return true;
                }

                DoInverterWork();
            }
            catch (System.Threading.ThreadInterruptedException)
            {
                // LogMessage("RunExtract - Inverter Manager - Id = " + InverterManagerID + " - interrupted", LogEntryType.ErrorMessage);
                //HistoryUpdater = null;
            }
            return true;
        }

        public override void Finalise()
        {
            base.Finalise();
            ErrorLogger.Close();
            //HistoryUpdater = null;
            LogMessage("Finalise", "Inverter Manager - Id = " + ManagerId + " - manager stopping", LogEntryType.StatusChange);
        }

        public void CloseErrorLogger()
        {
            ErrorLogger.Close();
        }
    }
}
