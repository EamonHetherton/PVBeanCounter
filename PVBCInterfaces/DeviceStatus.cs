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

namespace PVBCInterfaces
{
    public class DeviceStatus
    {
        public DeviceStatus(int id, String siteId, int interval)
        {
            Id = id;
            // SerialNo = serialNo;
            SiteId = siteId;
            Interval = interval;
            OutputRecorded = DateTime.Now;
            LastOutput = OutputRecorded;

        }

        public int Id { get; private set; }
        // public String SerialNo { get; private set; }
        public String SiteId { get; set; }

        public int Interval { get; private set; }

        public DateTime LastOutput;
        public DateTime NextOutput
        {
            get { return LastOutput + TimeSpan.FromSeconds(Interval); }
        }

        public DateTime OutputRecorded;
    }
}


