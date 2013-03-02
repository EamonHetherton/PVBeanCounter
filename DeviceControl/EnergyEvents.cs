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
            public String Hierarchy;
            public String EmitEventType;
            public String ManagerName;
            public String Component;
            public String DeviceName;
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

        public EnergyEvents(ApplicationSettings settings)
        {
            ApplicationSettings = settings;
            SystemServices = GlobalSettings.SystemServices;
            PVEventReadyEvent = new ManualResetEvent(false);
            NodeReaderWriterLock = new ReaderWriterLock();
            NodeUpdateMutex = new Mutex();
            PendingListMutex = new Mutex();
            
            PendingEvents = new List<EventPending>();
            LastEmitErrorReported = DateTime.MinValue;
        }

        private EnergyNode FindNode(HierarchyType type, String managerName, String component, String deviceName)
        {
            foreach (EnergyNode node in EnergyNodes)
                if (node.Hierarchy == type && node.ManagerName == managerName 
                && node.DeviceName == deviceName 
                && node.Component == component)
                    return node;
            return null;
        }

        public bool NewEnergyReading(HierarchyType type, String managerName, String component, 
            String deviceName, DateTime time, Double? energy, int? powerWatts, float interval, 
            bool energyIsDayTotal = false, bool isRetry = false)
        {
            bool nodeFound = false;
            try
            {
                NodeReaderWriterLock.AcquireReaderLock(3000);
                int useWatts = 0;
                Double useEnergy = 0.0;
                if (interval > 0)
                {
                    if (powerWatts.HasValue)
                        useWatts = powerWatts.Value;
                    else if (energy.HasValue)
                        useWatts = (int)(energy.Value * 3600000.0 / interval); //kwH to Watts
                    if (energy.HasValue)
                        useEnergy = energy.Value;
                    else if (powerWatts.HasValue)
                        useEnergy = powerWatts.Value * interval / 3600000.0;
                }

                if (SystemServices.LogEvent)
                    SystemServices.LogMessage("EnergyEvents", "NewEnergyReading - time: " + time.ToString() + " - Type: " + type + " - Manager: " + managerName +
                        " - Component: " + component + " - Device: " + deviceName +
                        " - Interval: " + interval + " - Power: " + useWatts + " - Energy: " + useEnergy, LogEntryType.Event);

                EnergyNode node = FindNode(type, managerName, component, deviceName);
                nodeFound = (node != null);
                if (nodeFound)
                {
                    NodeUpdateMutex.WaitOne();
                    try
                    {
                        node.SetNodeReading(SystemServices, time, useEnergy, useWatts, interval, energyIsDayTotal);
                    }
                    catch (Exception e)
                    {
                        SystemServices.LogMessage("NewEnergyReading", "Exception: " + e.Message, LogEntryType.ErrorMessage);
                    }
                    NodeUpdateMutex.ReleaseMutex();
                    PVEventReadyEvent.Set();
                }
                else
                {
                    SystemServices.LogMessage("EnergyEvents", "NewEnergyReading - Event Type Not Found - time: " + time.ToString() + " - Type: " + type + " - Manager: " + managerName +
                        " - Component: " + component + " - Device: " + deviceName +
                        " - Interval: " + interval + " - Power: " + useWatts + " - Energy: " + useEnergy, LogEntryType.ErrorMessage);
                    if (!isRetry)
                    {
                        SystemServices.LogMessage("EnergyEvents", "NewEnergyReading - Reloading Events", LogEntryType.Trace);
                        NodeReaderWriterLock.ReleaseReaderLock();
                        ApplicationSettings.LoadEnergyEvents(true);
                        BuildEventNetwork();
                        nodeFound = NewEnergyReading(type, managerName, component, deviceName, time, 
                            energy, powerWatts, interval, energyIsDayTotal, true);
                    }
                }
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("EnergyEvents", "NewEnergyReading - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (NodeReaderWriterLock.IsReaderLockHeld)
                    NodeReaderWriterLock.ReleaseReaderLock();
            }
            return nodeFound;
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
                foreach (EnergyNode node in EnergyNodes)
                {
                    if (SystemServices.LogEvent)
                        SystemServices.LogMessage("ScanForEvents", "Hierarchy: " + node.Hierarchy + " - Manager: " + node.ManagerName +
                            " - Component: " + node.Component + " - Device: " + node.DeviceName +
                            " - Frequency: " + node.Frequency + " - Count: " + node.EventCount + " - Emit: " + node.EmitEvent.ToString() , LogEntryType.Event);
                    if (node.Frequency.HasValue && node.EmitEvent)
                    {
                        int lastPower = node.LastPowerEmitted;
                        Double lastEnergy = node.LastEnergyEmitted;

                        Double energyToday;
                        int currentPower;
                        
                        node.GetCurrentReading(eventTime, out energyToday, out currentPower);

                        if (lastPower != currentPower || lastEnergy != energyToday)
                        {
                            EventPending pend;
                            pend.PendingType = PendingType.Energy;
                            pend.Hierarchy = node.Hierarchy.ToString();
                            pend.EmitEventType = node.EmitEventType;
                            pend.ManagerName = node.ManagerName;
                            pend.Component = node.Component;
                            pend.DeviceName = node.DeviceName;
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
                    node.EventCount = 0;
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
                pend.Hierarchy = "";
                pend.EmitEventType = "";
                pend.EventTime = DateTime.Now;
                pend.CurrentPower = 0;
                pend.EnergyToday = 0.0;
                pend.DeviceName = "";
                pend.Component = "";
                pend.ManagerName = "";
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
                    id.ManagerName = node.ManagerName;
                    id.Component = node.Component;
                    id.Device = node.DeviceName;

                    if (node.PendingType == PendingType.Energy)
                    {
                        if (node.Hierarchy == "Yield")
                            proxy.OnYieldEvent(id, node.EventTime, node.EnergyToday, node.CurrentPower);
                        else if (node.Hierarchy == "Consumption")
                            proxy.OnConsumptionEvent(id, node.EventTime, node.EnergyToday, node.CurrentPower);
                        else
                            proxy.OnMeterEvent(id, node.EventTime, node.EnergyToday, node.CurrentPower);

                        if (SystemServices.LogEvent)
                            SystemServices.LogMessage("EmitPendingEvents", "Hierarchy: " + node.Hierarchy + " - Manager: " + node.ManagerName +
                                " - Component: " + node.Component + " - Device: " + node.DeviceName + " - Type: " + node.EmitEventType + " - Time: " + node.EventTime +
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

                //foreach (EnergyEventSettings eEvent in ApplicationSettings.EnergyEventList)
                //    if (eEvent.EmitEvent) count++;
                eventTypes = new EnergyEventsEventInfo[count];
                if (count > 0)
                {
                    count = 0;
                    foreach (EnergyEventSettings eEvent in ApplicationSettings.EnergyEventList)
                        if (eEvent.EmitEvent)
                        {
                            eventTypes[count].Hierarchy = eEvent.Hierarchy.ToString();
                            eventTypes[count].Type = eEvent.EventType;
                            eventTypes[count].Id.ManagerName = eEvent.ManagerName;
                            eventTypes[count].Id.Component = eEvent.Component;
                            eventTypes[count].Id.Device = eEvent.DeviceName;
                            eventTypes[count].Description = eEvent.Description;
                            eventTypes[count].FeedInYield = eEvent.FeedInYield;
                            eventTypes[count].FeedInConsumption = eEvent.FeedInConsumption;
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
