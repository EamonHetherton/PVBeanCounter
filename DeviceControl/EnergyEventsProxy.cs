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
using System.ServiceModel;
using System.ServiceModel.Channels;


partial class EnergyEventsProxy : ClientBase<IEnergyEvents>, IEnergyEvents
{
    public EnergyEventsProxy()
    { 
    }

    public EnergyEventsProxy(string endpointConfigurationName) : base(endpointConfigurationName)
    { 
    }

    public EnergyEventsProxy(string endpointConfigurationName, string remoteAddress) : base(endpointConfigurationName, remoteAddress)
    { 
    }

    public EnergyEventsProxy(string endpointConfigurationName, EndpointAddress remoteAddress) : base(endpointConfigurationName, remoteAddress)
    { 
    }

    public EnergyEventsProxy(Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress)
    {
    }

    public void AvailableEventList(bool updatedEvents, EnergyEventsEventInfo[] eventTypes)
    {
        Channel.AvailableEventList(updatedEvents, eventTypes);
    }

    public void OnStatusChangeEvent(String statusType, DateTime time, String text)
    {
        Channel.OnStatusChangeEvent(statusType, time, text);
    }

    public void OnYieldEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnYieldEvent(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnConsumptionEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnConsumptionEvent(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnEnergyEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnEnergyEvent(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnYieldEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnYieldEvent60second(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnConsumptionEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnConsumptionEvent60second(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnMeterEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnMeterEvent60second(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnYieldEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnYieldEvent300second(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnConsumptionEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnConsumptionEvent300second(id, time, energyDayTotalKWattHrs, powerWatts);
    }

    public void OnMeterEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
    {
        Channel.OnMeterEvent300second(id, time, energyDayTotalKWattHrs, powerWatts);
    }
}

