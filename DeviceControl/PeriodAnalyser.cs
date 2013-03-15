/*
* Copyright (c) 2012 Dennis Mackay-Fisher
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
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeviceDataRecorders
{
    public class PeriodEnumerator : IEnumerator<PeriodBase>, IEnumerable<PeriodBase>
    {
        private int index = -1;

        private PeriodBase CurrentPeriod;
        public PeriodBase StartPeriod { get; private set; }
        public PeriodBase EndPeriod { get; private set; }
        //private TimeSpan IntervalDuration;
        private DateTime IntervalStart;
        private DateTime IntervalEnd;
        private PeriodType PeriodType;

        public PeriodEnumerator(PeriodType periodType, TimeSpan periodStartOffset, DateTime intervalStart, DateTime intervalEnd)
        {
            PeriodType = periodType;
            DateTime firstPeriodStart = PeriodBase.GetPeriodStart(periodType, periodStartOffset, intervalStart, false);
            DateTime lastPeriodStart = PeriodBase.GetPeriodStart(periodType, periodStartOffset, intervalEnd, true);

            StartPeriod = new PeriodBase(periodType, firstPeriodStart, 0);
            if (firstPeriodStart == lastPeriodStart)
                EndPeriod = StartPeriod;
            else
                EndPeriod = new PeriodBase(periodType, lastPeriodStart, 0);
            
            CurrentPeriod = null;
            IntervalEnd = intervalEnd;
            IntervalStart = intervalStart;
            //IntervalDuration = intervalEnd - intervalStart;
            CurrentPeriod = default(PeriodBase);
        }

        public IEnumerator<PeriodBase> GetEnumerator()
        {
            return this;
        }
        
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            ++index;

            // first reference to first Period
            if (index == 0)
            {
                CurrentPeriod = StartPeriod;
                return true;
            }

            if (CurrentPeriod.End < IntervalEnd)
            {
                CurrentPeriod = new PeriodBase(PeriodType, CurrentPeriod.End, 0);
                return true;
            }                        
            return false;
        }

        public PeriodBase Current
        {
            get
            {
                if (index >= 0)
                    return CurrentPeriod;
                else
                    return default(PeriodBase);
            }
        }

        object System.Collections.IEnumerator.Current
        {
            get
            {
                if (index >= 0)
                    return (object)CurrentPeriod;
                else
                    return null;
            }
        }

        void IDisposable.Dispose() { }

        public void Reset()
        {
            index = -1;
            CurrentPeriod = default(PeriodBase);
        }
    }

}
