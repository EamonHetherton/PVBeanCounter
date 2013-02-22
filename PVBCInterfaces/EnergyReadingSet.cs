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

// Was PVReadingSet

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVBCInterfaces;

namespace PVBCInterfaces
{
    public class EnergyReadingSet
    {
        public IDevice Device;
        public List<EnergyReading> Readings;

        public EnergyReadingSet(IDevice device, Int32 initialReadingsCapacity)
        {
            Device = device;
            Readings = new List<EnergyReading>(initialReadingsCapacity);
        }
    }
}


