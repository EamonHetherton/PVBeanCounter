/*
* Copyright (c) 2010-13 Dennis Mackay-Fisher
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
using System.Text;
using GenericConnector;
using MackayFisher.Utilities;
using PVSettings;

namespace PVBeanCounter
{
    public class DeviceDisplayInfo
    {
        private ApplicationSettings ApplicationSettings;

        public long Id { get; set; }
        public String SerialNumber { get; set; }
        public String DeviceType { get; set; }
        
        public String Manufacturer { get; set; }
        public String Model { get; set; }
        
        public bool Updated { get; set; }
        public bool InitialLoad { get; private set; }

        public String FeatureType { get; set; }
        public int FeatureId { get; set; }

        public DeviceDisplayInfo(ApplicationSettings settings)
        {
            ApplicationSettings = settings;
            InitialLoad = true;
        }
    }

    public class DeviceDisplay
    {
        ApplicationSettings Settings;
        SystemServices SystemServices;

        public ObservableCollection<DeviceDisplayInfo> DeviceList;

        public DeviceDisplay(ApplicationSettings settings, SystemServices services)
        {
            Settings = settings;
            //SystemServices = new SystemServices(settings.BuildFileName("PVBC.log"));
            SystemServices = services;
            DeviceList = new ObservableCollection<DeviceDisplayInfo>();
        }

        private GenDatabase GetDatabase()
        {
            GenDatabase db = new GenDatabase(Settings.Host, Settings.Database,
                Settings.UserName, Settings.Password,
                Settings.DatabaseType, Settings.ProviderType,
                Settings.ProviderName, Settings.OleDbName,
                Settings.ConnectionString, Settings.DefaultDirectory, SystemServices);

            return db;
        }

        public void LoadDeviceList()
        {
            DeviceList.Clear();

            GenDatabase db = null;
            GenConnection con = null;
            GenCommand cmd = null;
            GenDataReader dataReader = null;

            try
            {
                db = GetDatabase();
                con = db.NewConnection();
                String getDevices =
                    "select i.Id, i.SerialNumber, it.DeviceType, it.Manufacturer, it.Model, if.FeatureType, if.FeatureId, if.MeasureType " +
                    "from device i, devicetype it, devicefeature if " +
                    "where i.DeviceType_Id = it.Id and i.Id = if.Device_Id " +
                    "order by it.DeviceType, i.SerialNumber ";

                cmd = new GenCommand(getDevices, con);

                dataReader = (GenDataReader)cmd.ExecuteReader();

                while (dataReader.Read())
                {
                    DeviceDisplayInfo info = new DeviceDisplayInfo(Settings);
                    info.Id = dataReader.GetInt32(0);
                    info.SerialNumber = dataReader.GetString(1);
                    info.DeviceType = dataReader.GetString(2);
                    info.Manufacturer = dataReader.GetString(3);
                    info.Model = dataReader.GetString(4);
                    info.FeatureType = ((FeatureType)dataReader.GetInt16(5)).ToString();
                    info.FeatureId = dataReader.GetInt16(6);
                    
                    info.Updated = false;

                    DeviceList.Add(info);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (dataReader != null)
                {
                    dataReader.Close();
                    dataReader.Dispose();
                }
                if (cmd != null)
                    cmd.Dispose();
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }
    }    
}
