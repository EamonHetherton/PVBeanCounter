/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using GenericConnector;
using MackayFisher.Utilities;
using PVSettings;

namespace PVBeanCounter
{
    public class OwlDatabaseInfo
    {
        public class ApplianceInfo
        {
            public int ApplianceNo { get; set; }
            public String Description { get; set; }
            public override string ToString()
            {
                return Description;
            }
        }

        private DeviceManagerSettings Settings;
        SystemServices SystemServices;
        private GenDatabase OwlDb;
        private ObservableCollection<ApplianceInfo> ApplianceListInternal;

        public OwlDatabaseInfo(DeviceManagerSettings owlSettings, SystemServices systemServices)
        {
            Settings = owlSettings;
            SystemServices = systemServices;
            OwlDb = null;
            ApplianceListInternal = null;
        }

        private GenDatabase GetDatabase()
        {
            if (OwlDb == null)
            {
                if (Settings.OwlDatabase != "")
                {
                    try
                    {
                        OwlDb = new GenDatabase("", Settings.OwlDatabase, "", "", "SQLite", "Proprietary",
                        "System.Data.SQLite", "", "", Settings.ApplicationSettings.DefaultDirectory, SystemServices);
                    }
                    catch(Exception)
                    {
                    }
                }
            }
            return OwlDb;
        }

        private ObservableCollection<ApplianceInfo> GetApplianceList()
        {
            ObservableCollection<ApplianceInfo> list = new ObservableCollection<ApplianceInfo>();

            GenDatabase olwDb = GetDatabase();
            if (OwlDb != null)
            {
                GenConnection con = null;
                GenCommand cmd = null;
                GenDataReader reader = null;
                String selCmd =
                    "select addr, name, model " +
                    "from energy_sensor " +
                    "where addr is not null " +
                    "order by name ";

                try
                {
                    con = OwlDb.NewConnection();
                    cmd = new GenCommand(selCmd, con);
                    reader = (GenDataReader)cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        ApplianceInfo info = new ApplianceInfo();
                        
                        info.ApplianceNo = reader.GetInt32(0);
                        string name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        string model = reader.IsDBNull(2) ? "" : reader.GetInt32(2).ToString();
                        if (name == "")
                            if (model == "")
                                info.Description = info.ApplianceNo.ToString();
                            else
                                info.Description = model + ": " + info.ApplianceNo.ToString();
                        else if (model == "")
                            info.Description = name + ": " + info.ApplianceNo.ToString();
                        else
                            info.Description = name + " / " + model + ": " + info.ApplianceNo.ToString();
                        
                        list.Add(info);
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    if (con != null)
                    {
                        con.Close();
                        con.Dispose();
                    }
                    if (cmd != null)
                        cmd.Dispose();
                    if (reader != null)
                    {
                        reader.Close();
                        reader.Dispose();
                    }
                }
            }

            return list;
        }

        public void LoadApplianceList()
        {
            OwlDb = null; // Force new database settings in use (if changed)
            OwlAppliances = GetApplianceList();
        }

        public ObservableCollection<ApplianceInfo> OwlAppliances
        {
            get
            {
                if (ApplianceListInternal == null)
                    ApplianceListInternal = GetApplianceList();
                return ApplianceListInternal; 
            }

            private set
            {
                ApplianceListInternal = value;
            }
        }
    }

}
