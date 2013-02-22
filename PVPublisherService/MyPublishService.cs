//2006 IDesign Inc. 
//Questions? Comments? go to 
//http://www.idesign.net

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
using ServiceModelEx;

[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
class MyPublishService : PublishService<IEnergyEvents>,IEnergyEvents
{

    public void AvailableEventList(bool updatedEvents, EnergyEventsEventInfo[] eventTypes)
    {
        FireEvent(updatedEvents, eventTypes);
        
    }

   public void OnStatusChangeEvent(String statusType, DateTime time, string text)
   {
       FireEvent(statusType, time, text);
   }

   public void OnYieldEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnConsumptionEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnMeterEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnYieldEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnConsumptionEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnMeterEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnYieldEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnConsumptionEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }

   public void OnMeterEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalWattHrs, int powerWatts)
   {
       FireEvent(id, time, energyDayTotalWattHrs, powerWatts);
   }
}