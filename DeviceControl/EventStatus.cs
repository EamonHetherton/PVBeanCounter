/*
* Copyright (c) 2013 Dennis Mackay-Fisher
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
using System.Threading.Tasks;
using MackayFisher.Utilities;
using PVSettings;

namespace Device
{
    public struct DeviceEventConfig
    {
        public String EventName;
        public EventType EventType;
        public bool UseForFeedIn;

        public DeviceEventConfig(DeviceEventSettings settings)
        {
            EventName = settings.EventName;
            EventType = settings.EventType.Value;
            UseForFeedIn = settings.UseForFeedIn;
        }
    }

    public abstract class EventStatus
    {
        protected DeviceBase Device;
        public FeatureType FeatureType;
        public uint FeatureId;

        public int Frequency;
        public List<DeviceEventConfig> EmitEvents;
        public int EventCount = 0;
        protected float Interval = 0.0F;
        protected DateTime? IntervalEnd = null;

        public List<DeviceLink> ToDeviceLinks;
        public List<DeviceLink> FromDeviceLinks;

        public DateTime LastEventTime = DateTime.MinValue;
        public DateTime LastEmitTime = DateTime.MinValue;

        public EventStatus(DeviceBase device, FeatureType featureType, uint featureId, int frequency, ObservableCollection<DeviceEventSettings> emitEvents)
        {
            Device = device;
            FeatureType = featureType;
            FeatureId = featureId;
            Frequency = frequency;
            ToDeviceLinks = new List<DeviceLink>();
            FromDeviceLinks = new List<DeviceLink>();

            EmitEvents = new List<DeviceEventConfig>();
            foreach(DeviceEventSettings es in device.DeviceManagerDeviceSettings.DeviceEvents)
                if (es.EventFeatureType == FeatureType && es.EventFeatureId == FeatureId)
                    EmitEvents.Add(new DeviceEventConfig(es));
        }

        protected void IncrementEventCount(int depth)
        {
            if (depth > 10)
            {
                GlobalSettings.LogMessage("IncrementEventCount", "Recursion too deep: " + depth + " - increment aborted", LogEntryType.ErrorMessage);
                return;
            }

            if (GlobalSettings.SystemServices.LogEvent)
                GlobalSettings.LogMessage("IncrementEventCount",
                    "Increment - Type: " + FeatureType +
                    " - Id: " + FeatureId +
                    " - Device: " + Device.DeviceManagerDeviceSettings.Name, LogEntryType.Event);
            EventCount++;
            depth++;
            foreach (DeviceLink link in FromDeviceLinks)                
                    link.ToEventStatus.IncrementEventCount(depth);
        }
    }

    public class EnergyEventStatus : EventStatus
    {
        private Double EnergyTotal;
        private int EventPower;
        public int LastPowerEmitted;
        public Double LastEnergyEmitted;

        public EnergyEventStatus(DeviceBase device, FeatureType featureType, uint featureId, int frequency, ObservableCollection<DeviceEventSettings> emitEvents)
            : base(device, featureType, featureId, frequency, emitEvents)
        {
            EnergyTotal = 0.0;            
            EventPower = 0;
            LastPowerEmitted = 0;
            LastEnergyEmitted = 0.0;        
        }

        private DateTime? PowerExpires
        {
            get
            {
                if (IntervalEnd.HasValue)
                {
                    int limit = Frequency * 4 + 10;
                    return IntervalEnd.Value.AddSeconds(limit);
                }
                else
                    return null;
            }
        }

        public void SetEventReading( DateTime time, Double energy, int power, float interval, bool energyIsDayTotal)
        {
            DateTime eventTime = DateTime.Now;
            if (energyIsDayTotal)
                EnergyTotal = energy;
            else
            {
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
            EventPower = power;
            IncrementEventCount( 0);
            LastEventTime = DateTime.Now;
        }

        public void GetCurrentReading(DateTime asAt, out Double energyToday, out int currentPower)
        {
            Double energy = 0.0;
            if (IntervalEnd.HasValue)
                energy = ((asAt.Date == IntervalEnd.Value.Date) ? EnergyTotal : 0.0);

            int power = GetNodePower(asAt);

            foreach (DeviceLink link in FromDeviceLinks)
            {
                Double subEnergy;
                int subPower;

                link.FromEventStatus.GetCurrentReading(asAt, out subEnergy, out subPower);
                energy += subEnergy;
                power += subPower;
            }
            energyToday = energy;
            currentPower = power;
            LastPowerEmitted = power;
            LastEnergyEmitted = energy;
        }

        private int GetNodePower(DateTime asAt)
        {
            DateTime? expires = PowerExpires;
            if (expires.HasValue)
                if (expires.Value >= asAt)
                    return EventPower;
                else
                {
                    if (GlobalSettings.ApplicationSettings.LogTrace)
                        GlobalSettings.LogMessage("EnergyNode.GetNodePower", "Power has expired - Name: " + Device.DeviceManagerDeviceSettings.Name +
                            " - Feature Type: " + FeatureType + " - Id: " + FeatureId +
                            " - Expired: " + expires.Value, LogEntryType.Trace);
                    return 0;
                }
            else
                return 0;
        }


        /*
        public void InitialiseEnergyNode( int? frequency, bool emitEvent)
        {
            //Hierarchy = hierarchy;
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

        
        IUtilityLog Logger;



        private List<EnergyNode> AddsTo;
        private List<EnergyNode> ComposedOf;

        public void InitialiseEnergyNode(HierarchyType hierarchy,  String managerName,
            String component, String deviceName, String emitEventType, String inverter, int? frequency, bool emitEvent, List<EnergyNode> allNodes, IUtilityLog logger)
        {
            //Hierarchy = hierarchy;
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



        public EnergyNode(EnergyEventSettings settings, List<EnergyNode> allNodes, IUtilityLog logger)
        {
            //InitialiseEnergyNode(settings.Hierarchy, settings.ManagerName, settings.Component, settings.DeviceName, settings.EventType,
           //     settings.Inverter, settings.Interval, settings.EmitEvent, allNodes, logger);
        }

        private void LinkNodes(HierarchyType hierarchy, List<EnergyNode> allNodes)
        {
            Logger.LogMessage("LinkNodes", "Linking New Node - Hierarchy: "  + " - Manager: " + ManagerName +
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

    */
    }
}
