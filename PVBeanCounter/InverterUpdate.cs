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
    public class DeviceInfo
    {
        private ApplicationSettings ApplicationSettings;

        public long Id { get; set; }
        public String SerialNumber { get; set; }
        public String DeviceType { get; set; }
        
        public String Manufacturer { get; set; }
        public String Model { get; set; }
        
        public bool Updated { get; set; }
        public bool InitialLoad { get; private set; }

        public DeviceInfo(ApplicationSettings settings)
        {
            ApplicationSettings = settings;
            InitialLoad = true;
        }
    }

    public class DeviceUpdate
    {
        ApplicationSettings Settings;
        SystemServices SystemServices;

        public ObservableCollection<DeviceInfo> DeviceList;

        public DeviceUpdate(ApplicationSettings settings, SystemServices services)
        {
            Settings = settings;
            //SystemServices = new SystemServices(settings.BuildFileName("PVBC.log"));
            SystemServices = services;
            DeviceList = new ObservableCollection<DeviceInfo>();
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
                    "select i.Id, i.SerialNumber, it.DeviceType, it.Manufacturer, it.Model " +
                    "from device i, devicetype it " +
                    "where i.DeviceType_Id = it.Id " +
                    "order by it.DeviceType, i.SerialNumber ";

                cmd = new GenCommand(getDevices, con);

                dataReader = (GenDataReader)cmd.ExecuteReader();

                while (dataReader.Read())
                {
                    DeviceInfo info = new DeviceInfo(Settings);
                    info.Id = dataReader.GetInt32(0);
                    info.SerialNumber = dataReader.GetString(1);
                    info.DeviceType = dataReader.GetString(2);
                    info.Manufacturer = dataReader.GetString(3);
                    info.Model = dataReader.GetString(4);
                    
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
