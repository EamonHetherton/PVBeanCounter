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
    public class PVReading
    {
        // Comparer allows readings to be sorted
        public static int Compare(PVReading x, PVReading y)
        {
            if (x == null)
                if (y == null)
                    return 0; // both null
                else
                    return -1; // x is null y is not null
            else if (y == null)
                return 1;  // y is null x is not null

            if (x.OutputTimeInternal > y.OutputTimeInternal)
                return 1;
            else if (x.OutputTimeInternal < y.OutputTimeInternal)
                return -1;

            return 0; // equal
        }

        public virtual DateTime OutputTime
        {
            get
            {
                return OutputTimeInternal;
            }
            set
            {
                OutputTimeInternal = value;
            }
        }

        public virtual Int32 Duration
        {
            get
            {
                return ReadingDurationInternal;
            }
            set
            {
                ReadingDurationInternal = value;
                if (CalcPower)
                    KWOutputInternal = Math.Round(((Double)KWHOutputInternal) * 3600 / ReadingDurationInternal, 3);
                else
                    KWHOutputInternal = Math.Round(((Double)KWOutputInternal) * ReadingDurationInternal / 3600, 3);
            }
        }

        public virtual Double KWHOutput
        {
            get
            {
                return KWHOutputInternal;
            }
            set
            {
                CalcPower = true;
                KWHOutputInternal = value;
                KWOutputInternal = Math.Round(((Double)KWHOutputInternal) * 3600 / ReadingDurationInternal, 3);
            }
        }

        // kwOutput is average output rate (kw) for readingDuration
        public virtual Double KWOutput
        {
            get
            {
                return KWOutputInternal;
            }
            set
            {
                CalcPower = false;
                KWOutputInternal = value;
                KWHOutputInternal = Math.Round(((Double)KWOutputInternal) * ReadingDurationInternal / 3600, 3);
            }
        }

        public virtual Double? MinPower
        {
            get
            {
                return MinPowerInternal;
            }

            set
            {
                MinPowerInternal = value;
            }
        }

        public virtual Double? MaxPower
        {
            get
            {
                return MaxPowerInternal;
            }

            set
            {
                MaxPowerInternal = value;
            }
        }

        public virtual Double? Temperature
        {
            get
            {
                return TemperatureInternal;
            }

            set
            {
                TemperatureInternal = value;
            }
        }

        //public Double KWHTotalFirst = 0.0;
        public Double KWHTotal = 0.0;

        private Double KWOutputInternal = 0.0;
        private Double KWHOutputInternal = 0.0;
        private Int32 ReadingDurationInternal = 0;
        private Double? MinPowerInternal = null;
        private Double? MaxPowerInternal = null;
        private Double? TemperatureInternal = null;
        private DateTime OutputTimeInternal = DateTime.Now;
        private bool CalcPower = true;
    }
}
