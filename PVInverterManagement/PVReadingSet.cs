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
using System.Linq;
using System.Text;

namespace PVInverterManagement
{
    public class PVReadingSet
    {
        public Device.DeviceIdentity Inverter{ get; set; }
        public List<PVReading> Readings{ get; set; }

        public PVReadingSet(Device.DeviceIdentity newInverter, Int32 initialReadingsCapacity)
        {
            Inverter = newInverter;
            Readings = new List<PVReading>(initialReadingsCapacity);
        }
    }
}
