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
    public class InverterInfo
    {
        private String siteId;
        private ApplicationSettings ApplicationSettings;

        public long Id { get; set; }
        public String SerialNumber { get; set; }
        public String SiteId
        {
            get
            {
                return siteId;
            }

            set
            {
                if (value == "" || value == null)
                    siteId = "none";
                else
                    siteId = value;
                Updated = true;
                if (InitialLoad)
                    InitialLoad = false;
                else
                    ApplicationSettings.SettingChangedEventHandler("");
            }
        }
        public String Manufacturer { get; set; }
        public String Model { get; set; }
        
        public bool Updated { get; set; }
        public bool InitialLoad { get; private set; }

        public InverterInfo(ApplicationSettings settings)
        {
            ApplicationSettings = settings;
            InitialLoad = true;
        }
    }

    public class InverterUpdate
    {
        ApplicationSettings Settings;
        SystemServices SystemServices;

        public ObservableCollection<InverterInfo> InverterList;

        public InverterUpdate(ApplicationSettings settings, SystemServices services)
        {
            Settings = settings;
            //SystemServices = new SystemServices(settings.BuildFileName("PVBC.log"));
            SystemServices = services;
            InverterList = new ObservableCollection<InverterInfo>();
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

        public void LoadInverterList()
        {
            InverterList.Clear();

            GenDatabase db = null;
            GenConnection con = null;
            GenCommand cmd = null;
            GenDataReader dataReader = null;

            try
            {
                db = GetDatabase();
                con = db.NewConnection();
                String getInverters =
                    "select i.Id, i.SerialNumber, i.SiteId, it.Manufacturer, it.Model " +
                    "from inverter i, invertertype it " +
                    "where i.InverterType_Id = it.Id " +
                    "order by SerialNumber ";

                cmd = new GenCommand(getInverters, con);

                dataReader = (GenDataReader)cmd.ExecuteReader();

                while (dataReader.Read())
                {
                    InverterInfo info = new InverterInfo(Settings);
                    info.Id = dataReader.GetInt32(0);
                    info.SerialNumber = dataReader.GetString(1);
                    info.SiteId = dataReader.GetString(2);
                    info.Manufacturer = dataReader.GetString(3);
                    info.Model = dataReader.GetString(4);
                    
                    info.Updated = false;

                    InverterList.Add(info);
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

        public void UpdateInverters()
        {
            GenDatabase db = GetDatabase();

            GenConnection con = db.NewConnection();

            String updInverters =
                "update inverter set SiteId = @SiteId " +
                "where Id = @Id " ;

            foreach(InverterInfo info in InverterList)
            {
                if (info.Updated)
                {
                    GenCommand cmd = new GenCommand(updInverters, con);
                    cmd.AddParameterWithValue( "@SiteId", info.SiteId);
                    cmd.AddParameterWithValue("@Id", info.Id);
                    cmd.ExecuteNonQuery();
                }

                info.Updated = false;
            }
        }
    }    
}
