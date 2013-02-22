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
using System.Data.Common;
using System.Data.OleDb;
using System.Collections.Generic;
using GenericConnector;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MackayFisher.Utilities;
using PVBCInterfaces;
using PVSettings;

namespace PVInverterManagement
{
    public abstract class ExtractCSV
    {
        public abstract string ComponentType { get; }

        private InverterManager InverterManager;

        private bool ExtractHasRun = false;

        public ExtractCSV(InverterManager inverterManager)
        {
            InverterManager = inverterManager;
        }

        protected void LogMessage(String message, MackayFisher.Utilities.LogEntryType logEntryType)
        {
            GlobalSettings.LogMessage(ComponentType, message, logEntryType);
        }

        public void RunExtracts(String configFile, String password, 
            String exportDirectory, GenConnection connection/*, IUtilityLog inverterManagerManagerLog*/)
        {
            List<DateTime> dateList;

            try
            {
                bool resetFirstFullDay = InverterManager.InverterManagerSettings.ResetFirstFullDay && !ExtractHasRun;
                dateList = InverterManager.InverterDataRecorder.FindIncompleteDays(InverterManager.InverterManagerID, resetFirstFullDay);
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtracts: " + e.Message, e);
            }

            int dateCount = dateList.Count;

            // Add today's date to the end of the list if it is not there already
            if (dateCount > 0)
            {
                if (!dateList.Contains( DateTime.Today))
                    dateList.Add( DateTime.Today);
            }
            else
                dateList.Add( DateTime.Today);

            bool res = false;
            DateTime? fromDate = null;
            DateTime toDate = DateTime.Today;

            try
            {
                // call RunExtract for groups of consecutive days - reduce execution time
                foreach (DateTime dt in dateList)
                {
                    if (fromDate == null)
                        fromDate = dt;
                    else if (dt > (toDate.AddDays(1)))
                    {
                        res = RunExtract(configFile, password, exportDirectory, (DateTime)fromDate, toDate);
                        fromDate = dt;
                        ExtractHasRun |= res;
                    }

                    toDate = dt.Date;
                }

                // one call outstanding at end of loop
                if (fromDate != null)
                {
                    res = RunExtract(configFile, password, exportDirectory, (DateTime)fromDate, toDate);
                    ExtractHasRun |= res;
                }
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("ExtractCSV.RunExtracts", "exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtracts: error calling RunExtract: " + e.Message, e);
            }
        }

        public abstract bool RunExtract(String configFile, String password, String exportDir, DateTime fromDate, DateTime toDate);

    }
}
