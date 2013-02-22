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
    public class EnergyNode
    {
        public HierarchyType Hierarchy { get; private set; }
        public String ManagerName { get; private set; }
        public String Component { get; private set; }
        public String DeviceName { get; private set; }
        public String Inverter { get; private set; }
        public int? Frequency { get; private set; }
        public bool EmitEvent { get; private set; }
        public String EmitEventType { get; private set; }

        public int EventCount;

        private Double EnergyTotal;
        private int NodePower;
        public int LastPowerEmitted { get; private set; }
        public Double LastEnergyEmitted { get; private set; }

        private float Interval;
        
        private DateTime? IntervalEnd;
        IUtilityLog Logger;

        private DateTime? PowerExpires
        {
            get
            {
                if (IntervalEnd.HasValue)
                {
                    int limit = Frequency.Value * 4 + 10;
                    return IntervalEnd.Value.AddSeconds(limit);
                }
                else
                    return null;
            }
        }

        private List<EnergyNode> AddsTo;
        private List<EnergyNode> ComposedOf;

        public void InitialiseEnergyNode(HierarchyType hierarchy, String managerName,
            String component, String deviceName, String emitEventType, String inverter, int? frequency, bool emitEvent, List<EnergyNode> allNodes, IUtilityLog logger)
        {
            Hierarchy = hierarchy;
            ManagerName = managerName;            
            Component = component;
            DeviceName = deviceName;
            EmitEventType = emitEventType;
            Inverter = inverter;
            Frequency = frequency;
            EmitEvent = emitEvent;
            AddsTo = new List<EnergyNode>();
            ComposedOf = new List<EnergyNode>();

            EnergyTotal = 0.0;
            IntervalEnd = null;
            NodePower = 0;

            LastPowerEmitted = 0;
            LastEnergyEmitted = 0.0;
            
            Interval = 0.0F;
            EventCount = 0;
            Logger = logger;
            ConnectToNodes(allNodes);
            allNodes.Add(this);       
        }

        public EnergyNode(HierarchyType hierarchy, String managerName,
            String component, String deviceName, String emitEventType, String inverter, int? frequency,bool emitEvent, List<EnergyNode> allNodes, IUtilityLog logger)
        {
            InitialiseEnergyNode(hierarchy, managerName, component, deviceName, emitEventType, inverter, frequency, emitEvent, allNodes, logger);
        }

        public EnergyNode(EnergyEventSettings settings, List<EnergyNode> allNodes, IUtilityLog logger)
        {
            InitialiseEnergyNode(settings.Hierarchy, settings.ManagerName, settings.Component, settings.DeviceName, settings.EventType,
                settings.Inverter, settings.Interval, settings.EmitEvent, allNodes, logger);
        }

        private void LinkNodes(HierarchyType hierarchy, List<EnergyNode> allNodes)
        {
            Logger.LogMessage("LinkNodes", "Linking New Node - Hierarchy: " + Hierarchy + " - Manager: " + ManagerName +
                    " - Component: " + Component + " - Device: " + DeviceName, LogEntryType.Event);

            foreach (EnergyNode node in allNodes)
            {
                if (hierarchy != node.Hierarchy)
                    continue;

                Logger.LogMessage("LinkNodes", "Comparing to node - Hierarchy: " + node.Hierarchy + " - Manager: " + node.ManagerName +
                    " - Component: " + node.Component + " - Device: " + node.DeviceName, LogEntryType.Event);
                
                // node is a root node
                if (node.ManagerName == "" && node.Component == "") 
                {
                    Logger.LogMessage("LinkNodes", "Node is root: " + node.Hierarchy, LogEntryType.Event);
                    // this is a manager summary
                    if (ManagerName != "" && DeviceName == "" && Component == "")
                    {
                        AddsTo.Add(node);
                        node.ComposedOf.Add(this);
                        Logger.LogMessage("LinkNodes", "Nodes Linked", LogEntryType.Event);
                    }
                    continue;
                }

                // node is a manager node
                if (node.ManagerName != "" && node.Component == "")
                {
                    Logger.LogMessage("LinkNodes", "Node is Manager: " + node.ManagerName, LogEntryType.Event);
                    if (ManagerName != node.ManagerName)
                        continue;
                    // this is a component summary
                    if (Component != "" && DeviceName == "")
                    {
                        AddsTo.Add(node);
                        node.ComposedOf.Add(this);
                        Logger.LogMessage("LinkNodes", "Nodes Linked", LogEntryType.Event);
                    }
                    continue;
                }

                // node is an component summary
                if (node.Component != "" && node.DeviceName == "")
                {
                    Logger.LogMessage("LinkNodes", "Node is Component: " + node.Component, LogEntryType.Event);
                    if (ManagerName != node.ManagerName || Component != node.Component)
                        continue;
                    // this is a device detail
                    if (DeviceName != "")
                    {
                        AddsTo.Add(node);
                        node.ComposedOf.Add(this);
                        Logger.LogMessage("LinkNodes", "Nodes Linked", LogEntryType.Event);
                    }
                }
            }
        }

        private void LinkMeterNodes(HierarchyType hierarchy, List<EnergyNode> allNodes)
        {
            Logger.LogMessage("LinkMeterNodes", "Linking New Node - from Hierarchy: " + Hierarchy + " - to Hierarchy: " + hierarchy + " - Manager: " + ManagerName +
                    " - Component: " + Component + " - Device: " + DeviceName, LogEntryType.Event);
            foreach (EnergyNode node in allNodes)
            {
                if (hierarchy != node.Hierarchy)
                    continue;

                Logger.LogMessage("LinkMeterNodes", "Comparing to Node - Hierarchy: " + node.Hierarchy + " - Manager: " + node.ManagerName +
                    " - Component: " + node.Component + " - Device: " + node.DeviceName, LogEntryType.Event);
  
                // node is non-meter detail - inverter or non-inverter
                if (node.DeviceName != "")
                {
                    Logger.LogMessage("LinkMeterNodes", "Node is Device: " + node.DeviceName, LogEntryType.Event);
                    // this is a meter with matching inverter / device details
                    if ((hierarchy == HierarchyType.Consumption && ManagerName == node.ManagerName && Component == node.Component && DeviceName == node.DeviceName)
                    || (hierarchy == HierarchyType.Yield && Inverter == node.Component && DeviceName == node.DeviceName))
                    {
                        AddsTo.Add(node);
                        node.ComposedOf.Add(this);
                        Logger.LogMessage("LinkMeterNodes", "Nodes Linked", LogEntryType.Event);
                    }
                }
            }
        }

        private void ConnectToNodes(List<EnergyNode> allNodes)
        {
            LinkNodes(Hierarchy, allNodes);
            if (Hierarchy == HierarchyType.Meter && ManagerName != "" && Component != "" && DeviceName != "")
            {
                // Meter leaf nodes can add to TotalYield and TotalConsumption as well as the Meter root nodes
                LinkMeterNodes(HierarchyType.Yield, allNodes);
                LinkMeterNodes(HierarchyType.Consumption, allNodes);
            }
        }

        private int GetNodePower(DateTime asAt)
        {
            DateTime? expires = PowerExpires;
            if (expires.HasValue)
                if (expires.Value >= asAt)
                    return NodePower;
                else
                {
                    if (Logger.LogMeterTrace)
                        Logger.LogMessage("EnergyNode.GetNodePower", "Power has expired - Hierarchy: " + Hierarchy +
                            " - Manager: " + ManagerName + " - Component: " + Component + " - Device: " + DeviceName +
                            " - Expired: " + expires.Value, LogEntryType.MeterTrace);
                    return 0;
                }
            else
                return 0;
        }

        public void GetCurrentReading(DateTime asAt, out Double energyToday, out int currentPower)
        {
            Double energy = 0.0;
            if (IntervalEnd.HasValue)
                energy = ((asAt.Date == IntervalEnd.Value.Date) ? EnergyTotal : 0.0);

            int power = GetNodePower(asAt);

            foreach (EnergyNode node in ComposedOf)
                if (node.Frequency.HasValue)
                {
                    Double subEnergy;
                    int subPower;

                    node.GetCurrentReading(asAt, out subEnergy, out subPower);
                    energy += subEnergy;
                    power += subPower;
                }
            energyToday = energy;
            currentPower = power;
            LastPowerEmitted = power;
            LastEnergyEmitted = energy;
        }

        public void IncrementEventCount(IUtilityLog logger, int depth)
        {
            if (depth > 10)
            {
                logger.LogMessage("IncrementEventCount", "Recursion too deep: " + depth + " - increment aborted", LogEntryType.ErrorMessage);
                return;
            }

            if (logger.LogEvent)
                logger.LogMessage("IncrementEventCount", 
                    "Increment - Type: " + Hierarchy + 
                    " - Manager: " + ManagerName + 
                    " - Component: " + Component + 
                    " - Device: " + DeviceName, LogEntryType.Event);
            EventCount++;
            depth++;
            foreach (EnergyNode node in AddsTo)
                node.IncrementEventCount(logger, depth);
        }

        public void SetNodeReading(IUtilityLog logger, DateTime time, Double energy, int power, float interval, bool energyIsDayTotal)
        {
            DateTime eventTime = DateTime.Now;
            if (energyIsDayTotal)
                EnergyTotal = energy;
            else
            {
                // Don't use the time reported on the event as this can be from a device with bad time sync (eg CC Meter)
                // if (IntervalEnd.Date != time.Date)
                if (!IntervalEnd.HasValue || IntervalEnd.Value.Date != eventTime.Date)
                    EnergyTotal = 0.0;
                double sinceDayStart = (eventTime - eventTime.Date).TotalSeconds;
                if (interval > sinceDayStart && interval > 0)
                    EnergyTotal += energy * sinceDayStart / interval;
                else
                    EnergyTotal += energy;
            }            
            IntervalEnd = eventTime;
            Interval = interval;
            NodePower = power;
            IncrementEventCount(logger, 0);
        }
    }

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
        
        private List<EnergyNode> EnergyNodes;
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
            BuildEventNetwork();
            PendingEvents = new List<EventPending>();
            LastEmitErrorReported = DateTime.MinValue;
        }

        public void BuildEventNetwork()
        {
            try
            {
                NodeReaderWriterLock.AcquireWriterLock(3000);
                EnergyNodes = new List<EnergyNode>();
                foreach (EnergyEventSettings evnt in ApplicationSettings.EnergyEventList)
                {
                    // note this auto adds itself to EnergyNodes
                    // it also scans EnergyNodes to build rollup links
                    new EnergyNode(evnt, EnergyNodes, SystemServices);
                }
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("EnergyEvents", "BuildEventNetwork - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (NodeReaderWriterLock.IsWriterLockHeld)
                    NodeReaderWriterLock.ReleaseWriterLock();
            }
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

                foreach (EnergyEventSettings eEvent in ApplicationSettings.EnergyEventList)
                    if (eEvent.EmitEvent) count++;
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
