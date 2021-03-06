﻿/*
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
using System.ServiceModel;
using System.ServiceModel.Channels;

public struct EnergyEventsEventInfo
{
    public String Type;
    public EnergyEventsEventId Id;
    public String Description;
    public bool FeedInYield;
    public bool FeedInConsumption;
}

public struct EnergyEventsEventId
{
    public String Name;

    public override bool Equals(Object obj)
    {
        return obj is EnergyEventsEventId && this == (EnergyEventsEventId)obj;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public static bool operator ==(EnergyEventsEventId x, EnergyEventsEventId y)
    {
        return (x.Name == y.Name);
    }

    public static bool operator !=(EnergyEventsEventId x, EnergyEventsEventId y)
    {
        return !(x == y);
    }
}

[ServiceContract]
public interface IEnergyEvents
{
    [OperationContract(IsOneWay = true)]
    void AvailableEventList(bool updatedEvents, EnergyEventsEventInfo[] eventTypes);

    [OperationContract(IsOneWay = true)]
    void OnStatusChangeEvent(String statusType, DateTime time, String text);

    [OperationContract(IsOneWay = true)]
    void OnYieldEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnConsumptionEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnMeterEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnYieldEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnConsumptionEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnMeterEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnYieldEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnConsumptionEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);

    [OperationContract(IsOneWay = true)]
    void OnMeterEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts);
}
