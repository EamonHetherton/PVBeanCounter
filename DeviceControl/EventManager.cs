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
using GenThreadManagement;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace DeviceControl
{
    public class EventManager : GenThread
    {
        private IEvents EnergyEvents;
        private bool InitialEventRun;

        private DateTime LastEventTypeList;

        // Double testPower = 0.0;

        public override String ThreadName { get { return "EventManager"; } }
        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(0.0); } }
        public override TimeSpan InitialPause { get { return TimeSpan.FromSeconds(12.0); } }

        public EventManager(GenThreadManager genThreadManager, IEvents energyEvents)
            : base(genThreadManager, GlobalSettings.SystemServices)
        {
            EnergyEvents = energyEvents;
            LastEventTypeList = DateTime.MinValue;
            InitialEventRun = true;
        }

        protected void LogMessage(String message, LogEntryType logEntryType)
        {
            GlobalSettings.SystemServices.LogMessage("EventManager", message, logEntryType);
        }

        public override void Initialise()
        {
            base.Initialise();
            
            LogMessage("Initialise - Energy Manager running", LogEntryType.StatusChange);
        }

        public override bool DoWork()
        {
            try
            {
                /*
                // Debug event for testing - MUST BE REMOVED
                {
                    testPower += 1.0;
                    if (testPower > 5.0) testPower = 0.0;
                    EnergyEvents.NewEnergyReading(SystemServices, EnergyEventType.TotalYield, "Sunny Explorer/1", "Inverters", "2001380621", "", DateTime.Now, testPower, null, 3600);
                    EnergyEvents.PVEventReadyEvent.Reset(); // ensure it waits below
                }
                 * */

                if (LastEventTypeList <= DateTime.Now.AddMinutes(-2.0))
                {
                    EnergyEvents.EmitEventTypes(InitialEventRun);
                    LastEventTypeList = DateTime.Now;
                    InitialEventRun = false;
                }

                if (EnergyEvents.PVEventReadyEvent.WaitOne(TimeSpan.FromSeconds(10.0)))
                {
                    EnergyEvents.PVEventReadyEvent.Reset();
                    EnergyEvents.ScanForEvents();
                }

                return true;
            }
            catch (System.Threading.ThreadInterruptedException)
            {
            }
            catch (Exception e)
            {
                LogMessage("DoWork - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            return true;
        }

        public override void Finalise()
        {
            base.Finalise();
            
            LogMessage("Finalise - Event Manager stopped", LogEntryType.StatusChange);
        }
    }
}
