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
    public class DeviceIdentity
    {
        // require InvertertID or Make, Model and SerialNo
        public int? InverterId { get; internal set; }
        public String Make { get; internal set; }       // inverter manufacturer
        public String Model { get; internal set; }      // inverter model
        public String SerialNo { get; internal set; } // inverter unique id - could be serial number if available
        public String SiteId;

        public DeviceIdentity()
        {
            InverterId = null;
            Make = null;
            Model = null;
            SerialNo = null;
            SiteId = null;
        }
    }
}
