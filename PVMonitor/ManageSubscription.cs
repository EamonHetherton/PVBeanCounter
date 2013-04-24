/*
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GenThreadManagement;
using MackayFisher.Utilities;

namespace PVMonitor
{
    public class ManageSubscription : GenThread
    {
        public Subscriber.MyTransientSubscriber Subscriber;
        DateTime LastReconnect = DateTime.MinValue;
        public IUpdateDials UpdateDials;

        public ManageSubscription(GenThreadManager genThreadManager, SystemServices systemServices, 
            Subscriber.MyTransientSubscriber subscriber, IUpdateDials updateDials) : base(genThreadManager, systemServices)
        {
            Subscriber = subscriber;
            UpdateDials = updateDials;
        }

        public override string ThreadName
        {
            get { return "ManageSubscription"; }
        }

        public override TimeSpan Interval
        {
            get { return TimeSpan.FromSeconds(5.0); }
        }

        public override void Initialise()
        {
            base.Initialise();
        }

        public override void Finalise()
        {
            base.Finalise();
            Subscriber.Disconnect(true);
        }

        public override bool DoWork()
        {
            if (!Subscriber.Subscribed)
            {
                Subscriber.SetLocalEvent("Connecting");
                Subscriber.Subscribe("", Subscriber.UseAlternate);
                if (!Subscriber.Subscribed)
                    Subscriber.SetLocalEvent("Failed - Retry pending");
                else
                    Subscriber.SetLocalEvent("Waiting for first event");
            }

            DateTime triggerTime = DateTime.Now.AddSeconds(-20.0);
            if (Subscriber.LastEventTime <= triggerTime && LastReconnect <= triggerTime)
            {
                Subscriber.SetLocalEvent("Reconnecting");
                Subscriber.Disconnect();
                Subscriber.Reconnect(Subscriber.UseAlternate);
                LastReconnect = DateTime.Now;
                Subscriber.SetLocalEvent("Waiting for further events");
            }
            
            return true;
        }
    }
}
