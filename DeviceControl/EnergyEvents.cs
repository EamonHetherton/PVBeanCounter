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
using System.Threading;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace DeviceControl
{
    public class EnergyEvents : IEvents
    {
        public enum PendingType
        {
            Status = 0,
            Energy = 1,
            EventType = 2
        }

        public struct EventPending
        {
            public PendingType PendingType;
            public String EmitEventType;
            public String Name;
            public DateTime EventTime;
            public Double EnergyToday;
            public int CurrentPower;
            public String StatusText;
            public String StatusType;
        }

        private ApplicationSettings ApplicationSettings;
        private SystemServices SystemServices;

        public ManualResetEvent PVEventReadyEvent { get; private set; }
        
        private List<EventPending> PendingEvents;

        private ReaderWriterLock NodeReaderWriterLock;
        private Mutex NodeUpdateMutex;
        private Mutex PendingListMutex;

        DateTime LastEmitErrorReported;

        IDeviceManagerManager ManagerManager;

        public EnergyEvents(ApplicationSettings settings, IDeviceManagerManager managerManager)
        {
            ApplicationSettings = settings;
            ManagerManager = managerManager;
            SystemServices = GlobalSettings.SystemServices;
            PVEventReadyEvent = new ManualResetEvent(false);
            NodeReaderWriterLock = new ReaderWriterLock();
            NodeUpdateMutex = new Mutex();
            PendingListMutex = new Mutex();
            
            PendingEvents = new List<EventPending>();
            LastEmitErrorReported = DateTime.MinValue;
        }

        public void ScanForEvents()
        {
            bool haveMutex = false;
            bool havePendingMutex = false;
            DateTime eventTime = DateTime.Now;
            
            try
            {
                NodeReaderWriterLock.AcquireReaderLock(3000);
                haveMutex = NodeUpdateMutex.WaitOne();
                foreach (DeviceManagerBase mgr in ManagerManager.RunningDeviceManagers)
                {
                    foreach (Device.DeviceBase dev in mgr.GenericDeviceList)
                    {
                        foreach(Device.EnergyEventStatus status in dev.EventStatusList)
                        {
                            if (status.EmitEvents.Count > 0)
                            {                                
                                int lastPower = status.LastPowerEmitted;
                                Double lastEnergy = status.LastEnergyEmitted;

                                Double energyToday;
                                int currentPower;

                                status.GetCurrentReading(eventTime, out energyToday, out currentPower);
                                
                                if (lastPower != currentPower || lastEnergy != energyToday)
                                    foreach(Device.DeviceEventConfig e in status.EmitEvents)
                                    {
                                        if (SystemServices.LogEvent)
                                            SystemServices.LogMessage("ScanForEvents", " - Name: " + e.EventName +
                                                " - Type: " + e.EventType, LogEntryType.Event);

                                        EventPending pend;
                                        pend.PendingType = PendingType.Energy;
                                        pend.Name = e.EventName;
                                        pend.EmitEventType = e.EventType.ToString();
                                        pend.EventTime = eventTime;
                                        pend.EnergyToday = energyToday;
                                        pend.CurrentPower = currentPower;
                                        pend.StatusText = "";
                                        pend.StatusType = "";
                                        // Queue pending events so that they are sent outside the Mutex lock
                                        // allow more events to be recorded without extended thread block
                                        havePendingMutex = PendingListMutex.WaitOne();
                                        PendingEvents.Add(pend);
                                        if (havePendingMutex)
                                        {
                                            PendingListMutex.ReleaseMutex();
                                            havePendingMutex = false;
                                        }
                                        SystemServices.LogMessage("ScanForEvents", "Event queued", LogEntryType.Event);
                                                                        
                                    }
                            }
                        
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("ScanForEvents", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (haveMutex)
                    NodeUpdateMutex.ReleaseMutex();
                if (havePendingMutex)
                    PendingListMutex.ReleaseMutex();
                if (NodeReaderWriterLock.IsReaderLockHeld)
                    NodeReaderWriterLock.ReleaseReaderLock();
            }

            EmitPendingEvents();
        }

        public void NewStatusEvent(String statusType, String statusText)
        {
            bool havePendingMutex = false;
            try
            {
                NodeReaderWriterLock.AcquireReaderLock(3000);
                havePendingMutex = PendingListMutex.WaitOne();

                EventPending pend;
                pend.PendingType = PendingType.Status;
                pend.Name = "";
                pend.EmitEventType = "";
                pend.EventTime = DateTime.Now;
                pend.CurrentPower = 0;
                pend.EnergyToday = 0.0;
                pend.StatusType = statusType;
                pend.StatusText = statusText;
                PendingEvents.Add(pend);
                SystemServices.LogMessage("ScanForEvents", "Status Event queued - text: " + statusText, LogEntryType.Event);
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("SendStatusEvent", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (havePendingMutex)
                    PendingListMutex.ReleaseMutex();
                if (NodeReaderWriterLock.IsReaderLockHeld)
                    NodeReaderWriterLock.ReleaseReaderLock();
            }

            EmitPendingEvents();
        }

        private void EmitPendingEvents()
        {
            bool havePendingMutex = false;
            EnergyEventsProxy proxy = null;            
            try
            {
                NodeReaderWriterLock.AcquireReaderLock(3000);
                proxy = new EnergyEventsProxy();
                EventPending node;
                while(PendingEvents.Count > 0)
                {
                    // lock out access briefly so that event add to list proceeds unhindered
                    havePendingMutex = PendingListMutex.WaitOne();
                    node = PendingEvents[0];
                    PendingEvents.RemoveAt(0);
                    if (havePendingMutex)
                    {
                        PendingListMutex.ReleaseMutex();
                        havePendingMutex = false;
                    }

                    EnergyEventsEventId id;
                    id.Name = node.Name;
                    

                    if (node.PendingType == PendingType.Energy)
                    {
                        if (node.EmitEventType == "Yield")
                            proxy.OnYieldEvent(id, node.EventTime, node.EnergyToday, node.CurrentPower);
                        else if (node.EmitEventType == "Consumption")
                            proxy.OnConsumptionEvent(id, node.EventTime, node.EnergyToday, node.CurrentPower);
                        else
                            proxy.OnEnergyEvent(id, node.EventTime, node.EnergyToday, node.CurrentPower);

                        if (SystemServices.LogEvent)
                            SystemServices.LogMessage("EmitPendingEvents", "Type: " + node.EmitEventType + " - Name: " + node.Name + " - Time: " + node.EventTime +
                                " - Power: " + node.CurrentPower + " - Energy: " + node.EnergyToday, LogEntryType.Event);
                    }
                    else if (node.PendingType == PendingType.Status)
                    {
                        proxy.OnStatusChangeEvent(node.StatusType, node.EventTime, node.StatusText);
                        if (SystemServices.LogEvent)
                            SystemServices.LogMessage("EmitPendingEvents", "StatusType: " + node.StatusType + " - Time: " + node.EventTime +
                                " - StatusText: " + node.StatusText, LogEntryType.Event);
                    }
                }
            }
            catch (Exception e)
            {
                // Limit logging to one per minute
                if (LastEmitErrorReported < DateTime.Now.AddMinutes(-1.0))
                {
                    SystemServices.LogMessage("EmitPendingEvents", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                    LastEmitErrorReported = DateTime.Now;
                }
                PendingEvents.Clear();
            }

            try
            {
                if (havePendingMutex)
                    PendingListMutex.ReleaseMutex();

                if (proxy != null)
                    proxy.Close();
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("EmitPendingEvents", "Closing proxy - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (NodeReaderWriterLock.IsReaderLockHeld)
                    NodeReaderWriterLock.ReleaseReaderLock();
            }
        }

        public void EmitEventTypes(bool updatedEvents)
        {
            EnergyEventsProxy proxy = null;
            EnergyEventsEventInfo[] eventTypes;

            try
            {
                NodeReaderWriterLock.AcquireReaderLock(3000);
                proxy = new EnergyEventsProxy();
                int count = 0;
                foreach (DeviceManagerBase mgr in ManagerManager.RunningDeviceManagers)                
                    foreach (Device.DeviceBase dev in mgr.GenericDeviceList)                    
                        foreach (Device.EnergyEventStatus status in dev.EventStatusList)                        
                            count += status.EmitEvents.Count;
                            
                        
                    
                

                   

                //foreach (EnergyEventSettings eEvent in ApplicationSettings.EnergyEventList)
                //    if (eEvent.EmitEvent) count++;
                eventTypes = new EnergyEventsEventInfo[count];
                if (count > 0)
                {
                    count = 0;
                    foreach (DeviceManagerBase mgr in ManagerManager.RunningDeviceManagers)                
                        foreach (Device.DeviceBase dev in mgr.GenericDeviceList)                    
                            foreach(Device.EnergyEventStatus status in dev.EventStatusList)                        
                                foreach(Device.DeviceEventConfig eEvent in status.EmitEvents)
                                {
                                    eventTypes[count].Id.Name = eEvent.EventName;
                                    eventTypes[count].Type = eEvent.EventType.ToString();                                    
                                    eventTypes[count].Description = "";
                                    eventTypes[count].FeedInYield = eEvent.EventType == EventType.Yield && eEvent.UseForFeedIn;
                                    eventTypes[count].FeedInConsumption = eEvent.EventType == EventType.Consumption && eEvent.UseForFeedIn;
                                    count++;
                                }

                    proxy.AvailableEventList(updatedEvents, eventTypes);
                }
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("EmitEventTypes", "Exception: " + e.Message, LogEntryType.ErrorMessage);
            }

            try
            {
                if (proxy != null)
                    proxy.Close();
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("EmitEventTypes", "Closing proxy - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (NodeReaderWriterLock.IsReaderLockHeld)
                    NodeReaderWriterLock.ReleaseReaderLock();
            }
        }

    }
}
