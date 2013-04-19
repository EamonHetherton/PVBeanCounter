using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GenericConnector;
using MackayFisher.Utilities;
using PVSettings;

namespace PVSettings
{
    public class InverterInfo
    {
        public long Id;
        public String SerialNumber;
        public String Manufacturer;
        public String Model;
    }

    public class TestDatabase
    {
        private ApplicationSettings Settings;
        private SystemServices SystemServices;

        public TestDatabase(ApplicationSettings settings, SystemServices services)
        {
            Settings = settings;
            SystemServices = services;
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

        public String RunDatabaseTest(ref String outStage, ref Exception outException)
        {
            String stage = "Initialise";
            GenDatabase db = null;
            GenConnection con = null;
            GenCommand cmd = null;
            GenDataReader dataReader = null;
            try
            {
                stage = "Get Database from settings";
                db = GetDatabase();
                stage = "Get Database connection";
                con = db.NewConnection();
                VersionManager vm = new VersionManager();                
                vm.PopulateDatabaseIfEmpty(con);
                //con.GetSchemaTable("Fred");
                String cmd1 = "select count(*) from pvoutputlog ";
                stage = "Creating select command";
                cmd = new GenCommand(cmd1, con);
                stage = "Executing data reader";
                dataReader = (GenDataReader)cmd.ExecuteReader();
                stage = "Calling DataReader.Read()";
                bool res = dataReader.Read();
            }
            catch (Exception e)
            {
                outStage = stage;
                outException = e;
                return "Database Test - Stage: " + stage + " - Exception: " + e.Message;
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

            outStage = "Complete";
            outException = null;
            return "Success";
        }

        public List<InverterInfo> GetInverterList(String managerType, int instanceNo)
        {
            List<InverterInfo> InverterList = new List<InverterInfo>();

            GenDatabase db = null;
            GenConnection con = null;
            GenCommand cmd = null;
            GenDataReader dataReader = null;

            try
            {
                db = GetDatabase();
                con = db.NewConnection();
                String getInverters =
                    "select i.Id, i.SerialNumber, i.SiteId, it.Manufacturer, it.Model, im.ManagerType " +
                    "from inverter i, invertertype it, invertermanager im " +
                    "where im.ManagerType = @ManagerType " +
                    "and im.InstanceNo = @InstanceNo " +
                    "and i.InverterManager_Id = im.Id " +
                    "and i.InverterType_Id = it.Id " + 
                    "order by SerialNumber ";

                cmd = new GenCommand(getInverters, con);
                cmd.AddParameterWithValue("@ManagerType", managerType);
                cmd.AddParameterWithValue("@InstanceNo", instanceNo);

                dataReader = (GenDataReader)cmd.ExecuteReader();

                while (dataReader.Read())
                {
                    InverterInfo info = new InverterInfo();
                    info.Id = dataReader.GetInt32(0);
                    info.SerialNumber = dataReader.GetString(1);
                    info.Manufacturer = dataReader.GetString(3);
                    info.Model = dataReader.GetString(4);

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
            return InverterList;
        }

    }
}
