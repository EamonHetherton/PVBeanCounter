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
using System.Collections.Generic;
using GenericConnector;
using System.Linq;
using System.Text;
using System.Threading;
using PVSettings;
using MackayFisher.Utilities;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class SunnyExplorerInverterManager : InverterManager
    {
        //protected string ConfigFileName;       // used if extract utility requires a configuration file
        //protected DateTime? NextFileDate;      // specifies the next DateTime to be used for extract
        //protected string OutputDirectory;      // directory where extract files will be written
        //protected string ArchiveDirectory;     // directory where extract files will be moved when processed
        //protected string FileNamePattern;      // pattern used to identify extract files in outputDirectory
        //protected string Password;             // used for devices requiring a password for data extraction

        public override String InverterManagerType
        {
            get
            {
                return "Sunny Explorer";
            }
        }

        private SunnyExplorerExtractCSV SunnyExplorerExtractCSV = null;

        public ExtractCSV CSVExtractor
        {
            get
            {
                if (SunnyExplorerExtractCSV == null)
                    SunnyExplorerExtractCSV = new SunnyExplorerExtractCSV(this, InverterManagerSettings);

                return SunnyExplorerExtractCSV;
            }
        }

        public SunnyExplorerInverterManager(GenThreadManager genThreadManager, int imid, InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager, false)
        {
            // The base class retrieves the database row  and loads it into protected fields
            // set default file name pattern for Sunny Explorer inverter managers
            if (FileNamePattern == null || FileNamePattern == "")
                FileNamePattern = InverterManagerSettings.SunnyExplorerPlantName + "-????????.csv";
            InverterDataRecorder.HistoryUpdater = new SunnyExplorerHistoryUpdate(InverterDataRecorder, ManagerManager.EnergyEvents);
        }

        protected override int ExtractYield()
        {
            GenConnection connection = null;
            String state = "getting connection";
            int res = 0;

            try
            {
                connection = GlobalSettings.TheDB.NewConnection();
                state = "before RunExtracts";
                CSVExtractor.RunExtracts(ConfigFileName,
                    Password, OutputDirectory, connection);

                state = "before UpdateFromDirectory";
                res = InverterDataRecorder.HistoryUpdater.UpdateFromDirectory(OutputDirectory, FileNamePattern, GlobalSettings.ApplicationSettings.BuildFileName(ArchiveDirectory), connection);

                DateTime? newNextFileDate = InverterDataRecorder.FindNewStartDate(InverterManagerID);

                if ((NextFileDate != newNextFileDate) && (newNextFileDate != null))
                {
                    state = "before UpdateNextFileDate";
                    InverterDataRecorder.UpdateNextFileDate(InverterManagerID, newNextFileDate.Value);
                    NextFileDate = newNextFileDate;
                }
            }
            catch (Exception e)
            {
                LogMessage("ExtractYield", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                    connection = null;
                }
            }

            return res;
        }

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(300); } }

        public override TimeSpan? StartHourOffset { get { return TimeSpan.FromSeconds(120); } }

    }
}
