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
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Linq;
using System.ServiceModel;
using System.Windows.Threading;
using System.Windows.Data;
using System.ComponentModel;
using MackayFisher.Utilities;
using System.Collections.Generic;


namespace Subscriber
{
    public class MTObservableCollection<T> : ObservableCollection<T>
    {
        public override event NotifyCollectionChangedEventHandler CollectionChanged;
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var eh = CollectionChanged;
            if (eh != null)
            {
                Dispatcher dispatcher = (from NotifyCollectionChangedEventHandler nh in eh.GetInvocationList()
                                         let dpo = nh.Target as DispatcherObject
                                         where dpo != null
                                         select dpo.Dispatcher).FirstOrDefault();

                if (dispatcher != null && dispatcher.CheckAccess() == false)
                {
                    dispatcher.Invoke(DispatcherPriority.DataBind, (Action)(() => OnCollectionChanged(e)));
                }
                else
                {
                    foreach (NotifyCollectionChangedEventHandler nh in eh.GetInvocationList())
                        nh.Invoke(this, e);
                }
            }
        }
    } 

    public class ActiveEvent : INotifyPropertyChanged
    {
        public String Name { get; private set; }
        public EnergyEventsEventId Id { get; private set; }

        public String Type { get; private set; }

        public bool IsCurrent { get; set; }
        private IUpdateDials UpdateDials;

        public bool FeedInYield { get; private set; }
        public bool FeedInConsumption { get; private set; }

        public ActiveEvent(EnergyEventsEventInfo info, IUpdateDials updateDials)
        {
            Name = info.Id.Name;
            Id = info.Id;

            Type = info.Type;
            
            UpdateDials = updateDials;

            if (info.Description != "")
                ReadingDescription = info.Description;
            else
                ReadingDescription = info.Id.Name;

            FeedInYield = info.FeedInYield;
            FeedInConsumption = info.FeedInConsumption;

            updateDials.UpdateDial(this);
            CurrentPower = 0;           

            IsCurrent = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        private String _Description;

        public String ReadingDescription 
        {
            get { return _Description; }
            set
            {
                _Description = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ReadingDescription"));
            }
        }

        private Double _CurrentPower;

        public Double CurrentPower 
        {
            get { return _CurrentPower; }
            set
            {
                _CurrentPower = value;
                OnPropertyChanged(new PropertyChangedEventArgs("CurrentPower"));
            }
        }
    }

    public partial class MyTransientSubscriber : IEnergyEvents, INotifyPropertyChanged
    {
        private MySubscriptionServiceProxy m_Proxy;
        String _LastStatusEvent = "";
        int _CurrentYieldPower = 0;
        int _CurrentConsumptionPower = 0;
        String _PublisherMachine = "LocalHost";
        String _AlternatePublisherMachine = "";
        bool _ManualCredentials = false;
        String _Domain = "WORKGROUP";
        String _Username = "";
        String _Password = "";
        bool PublisherChanged = false;
        private List<String> _LocalMachines;
        SystemServices SystemServices;
        InstanceContext Context;

        Double LastYield = 0.0;
        Double LastConsumption = 0.0;
        //DateTime DateYield = DateTime.MinValue;
        //DateTime DateConsumption = DateTime.MinValue;
        ActiveEvent FeedInEvent = null;

        MTObservableCollection<ActiveEvent> _ActiveEvents;
        
        List<String> Subscriptions;
        public bool Subscribed { get{ return (Subscriptions.Count > 0); }}

        DateTime _LastEventTime;
        public DateTime LastEventTime { get { return _LastEventTime; } private set { _LastEventTime = value;} }

        public event PropertyChangedEventHandler PropertyChanged;

        IUpdateDials UpdateDials;

        public bool UseAlternate { get; private set; }

        public virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        private void CreateDefaultEvents()
        {
            EnergyEventsEventInfo info;
            info.Type = "Feed-In";
            info.Description = "Feed-In";
            info.Id.Name = "";
            info.FeedInYield = false;
            info.FeedInConsumption = false;
            FeedInEvent = GetActiveEvent(info, true);
        }

        public MyTransientSubscriber(SystemServices systemServices, IUpdateDials updateDials)
        {
            UseAlternate = false;
            SystemServices = systemServices;
            _PublisherMachine = PVMonitor.Properties.Settings.Default.PublisherMachine;
            _AlternatePublisherMachine = PVMonitor.Properties.Settings.Default.AlternatePublisherMachine;
            _ManualCredentials = PVMonitor.Properties.Settings.Default.ManualCredentials;
            _Username = PVMonitor.Properties.Settings.Default.Username;
            _Password = PVMonitor.Properties.Settings.Default.Password;
            _MaxYield = PVMonitor.Properties.Settings.Default.MaxYield;
            _MaxConsumption = PVMonitor.Properties.Settings.Default.MaxConsumption;
            LoadNetworkList();
            Context = new InstanceContext(this);
            Subscriptions = new List<String>();
            _ActiveEvents = new MTObservableCollection<ActiveEvent>();
            UpdateDials = updateDials;

            CreateDefaultEvents();

            m_Proxy = null;
            //PropertyChanged = null;
            
            LastEventTime = DateTime.MinValue;
        }

        public List<String> LocalMachines
        {
            get
            {
                return _LocalMachines;
            }
        }

        private void LoadNetworkList()
        {
            ListNetworkComputers.NetworkBrowser networkBrowser;
            ArrayList rawNetworkList;
            networkBrowser = new ListNetworkComputers.NetworkBrowser();
            rawNetworkList = networkBrowser.getNetworkComputers();
            _LocalMachines = new List<String>();
            _LocalMachines.Add("LocalHost");
            foreach (Object obj in rawNetworkList)
                _LocalMachines.Add((string)obj);
        }

        public ActiveEvent GetActiveEvent(EnergyEventsEventInfo info, bool create = false)
        {
            foreach (ActiveEvent evnt in _ActiveEvents)
            {
                if (evnt.Id == info.Id)
                    return evnt;
            }

            if (create)
            {
                ActiveEvent evnt = new ActiveEvent(info, UpdateDials);
                _ActiveEvents.Add(evnt);
                return evnt;
            }
            else
                return null;
        }

        private void UpdateFeedInEvent(bool isYield, Double power)
        {
            //DateTime testTime = DateTime.Now.AddSeconds(-14.0);
            if (isYield)
            {
                LastYield = power;
                //DateYield = DateTime.Now;
                //if (DateConsumption >= testTime)
                    FeedInEvent.CurrentPower = LastYield - LastConsumption;
            }
            else
            {
                LastConsumption = power;
                //DateConsumption = DateTime.Now;
                //if (DateYield >= testTime)
                    FeedInEvent.CurrentPower = LastYield - LastConsumption;
            }
        }


        public void AvailableEventList(bool updatedEvents, EnergyEventsEventInfo[] eventTypes)
        {
            // mark all current events as not current
            foreach (ActiveEvent evnt in _ActiveEvents)
                evnt.IsCurrent = false;

            for (int i = 0; i < eventTypes.GetLength(0); i++)
            {
                // locate or create an ActiveEvent for each available event
                ActiveEvent evnt = GetActiveEvent(eventTypes[i], true);
                
                // Mark event as current
                evnt.IsCurrent = true;
            }
        }

        public void OnStatusChangeEvent(String statusType, DateTime time, String text)
        {
            LastStatusEvent = statusType + " - " + text + " (" + time.ToString() + ")";
            LastEventTime = DateTime.Now;
        }

        public void OnYieldEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            CurrentYieldPower = powerWatts;
            LastEventTime = DateTime.Now;

            EnergyEventsEventInfo info;
            info.Type = "Yield";
            info.Id = id;
            info.Description = "Yield Found";
            info.FeedInConsumption = false;
            info.FeedInYield = false;

            if (powerWatts > _MaxYield)
            {
                MaxYield = (powerWatts / 2000) * 2000 + 2000;
            }

            ActiveEvent evnt = GetActiveEvent(info, false);
            if (evnt != null)
            {
                evnt.CurrentPower = powerWatts;
                if (evnt.FeedInYield)
                    UpdateFeedInEvent(true, powerWatts);
            }
        }

        public void OnConsumptionEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            CurrentConsumptionPower = powerWatts;
            LastEventTime = DateTime.Now;

            EnergyEventsEventInfo info;
            info.Type = "Consumption";
            info.Id = id;
            info.Description = "Consumption Found";
            info.FeedInConsumption = false;
            info.FeedInYield = false;

            if (powerWatts > _MaxConsumption)
            {
                MaxConsumption = (powerWatts / 2000) * 2000 + 2000;
            }

            ActiveEvent evnt = GetActiveEvent(info, false);
            if (evnt != null)
            {
                evnt.CurrentPower = powerWatts;
                if (evnt.FeedInConsumption)
                    UpdateFeedInEvent(false, powerWatts);
            }
        }

        public void OnEnergyEvent(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            LastEventTime = DateTime.Now;

            EnergyEventsEventInfo info;
            info.Type = "Energy";
            info.Id = id;
            info.Description = "Energy Found";
            info.FeedInConsumption = false;
            info.FeedInYield = false;

            ActiveEvent evnt = GetActiveEvent(info, false);
            if (evnt != null)
            {
                evnt.CurrentPower = powerWatts;

                if (evnt.FeedInConsumption)
                    UpdateFeedInEvent(false, powerWatts);
                else if (evnt.FeedInYield)
                    UpdateFeedInEvent(true, powerWatts);
            }
        }

        public void OnYieldEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            LastEventTime = DateTime.Now;
        }

        public void OnConsumptionEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            LastEventTime = DateTime.Now;
        }

        public void OnMeterEvent60second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            LastEventTime = DateTime.Now;
        }

        public void OnYieldEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            LastEventTime = DateTime.Now;
        }

        public void OnConsumptionEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            LastEventTime = DateTime.Now;
        }

        public void OnMeterEvent300second(EnergyEventsEventId id, DateTime time, Double energyDayTotalKWattHrs, int powerWatts)
        {
            LastEventTime = DateTime.Now;
        }

        public void Close()
        {
            if (m_Proxy != null)
            {
                m_Proxy.Close();
                m_Proxy = null;
            }
        }

        public String ApplicationVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public String PublisherMachine
        {
            get
            {
                return _PublisherMachine;
            }

            set
            {
                bool changed = (value != _PublisherMachine);
                PublisherChanged |= changed;
                if (changed)
                {
                    _PublisherMachine = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("PublisherMachine"));
                }
            }
        }

        public String AlternatePublisherMachine
        {
            get
            {
                return _AlternatePublisherMachine;
            }

            set
            {
                bool changed = (value != _AlternatePublisherMachine);
                PublisherChanged |= changed;
                if (changed)
                {
                    _AlternatePublisherMachine = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("AlternatePublisherMachine"));
                }
            }
        }

        private int _MaxYield = 6000;
        public int MaxYield
        {
            get
            {
                return _MaxYield;
            }
            set
            {
                _MaxYield = value;
                PVMonitor.Properties.Settings.Default.MaxYield = _MaxYield;
                PVMonitor.Properties.Settings.Default.Save();
                OnPropertyChanged(new PropertyChangedEventArgs("MaxYield"));
            }
        }

        private int _MaxConsumption = 6000;
        public int MaxConsumption
        {
            get
            {
                return _MaxConsumption;
            }
            set
            {
                _MaxConsumption = value;
                PVMonitor.Properties.Settings.Default.MaxConsumption = _MaxConsumption;
                PVMonitor.Properties.Settings.Default.Save();
                OnPropertyChanged(new PropertyChangedEventArgs("MaxConsumption"));
                OnPropertyChanged(new PropertyChangedEventArgs("MaxConsumptionNegative"));
            }
        }

        public int MaxConsumptionNegative
        {
            get
            {
                return -_MaxConsumption;
            }
            set
            {
                _MaxConsumption = -value;
                OnPropertyChanged(new PropertyChangedEventArgs("MaxConsumption"));
                OnPropertyChanged(new PropertyChangedEventArgs("MaxConsumptionNegative"));
            }
        }

        public bool ManualCredentials
        {
            get
            {
                return _ManualCredentials;
            }
            set
            {
                bool changed = (value != _ManualCredentials);
                PublisherChanged |= changed;
                if (changed)
                {
                    _ManualCredentials = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ManualCredentials"));
                }
            }
        }

        public String Domain
        {
            get
            {
                return _Domain;
            }

            set
            {
                bool changed = (value != _Domain);
                PublisherChanged |= changed;
                if (changed)
                {
                    _Domain = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Domain"));
                }
            }
        }

        public String Username
        {
            get
            {
                return _Username;
            }

            set
            {
                bool changed = (value != _Username);
                PublisherChanged |= changed;
                if (changed)
                {
                    _Username = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Username"));
                }
            }
        }

        public String Password
        {
            get
            {
                return _Password;
            }

            set
            {
                bool changed = (value != _Password);
                PublisherChanged |= changed;
                if (changed)
                {
                    _Password = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Password"));
                }
            }
        }

        public void ReconnectPublisher(bool useAlternate)
        {
            if (useAlternate)
            {
                if (_AlternatePublisherMachine == "")
                    return;
            }
            else if (_PublisherMachine == "")
                return;

            PVMonitor.Properties.Settings.Default.PublisherMachine = _PublisherMachine;
            PVMonitor.Properties.Settings.Default.AlternatePublisherMachine = _AlternatePublisherMachine;
            PVMonitor.Properties.Settings.Default.ManualCredentials = _ManualCredentials;
            PVMonitor.Properties.Settings.Default.Domain = _Domain;
            PVMonitor.Properties.Settings.Default.Username = _Username;
            PVMonitor.Properties.Settings.Default.Password = _Password;
            PVMonitor.Properties.Settings.Default.Save();
            Disconnect();
            Reconnect(useAlternate);
            PublisherChanged = false;
        }

        private MySubscriptionServiceProxy GetProxy(bool useAlternate)
        {
            MySubscriptionServiceProxy proxy;
            if (useAlternate)
                proxy = new MySubscriptionServiceProxy(Context, "PVMonitorSubscribe_TCP", "net.tcp://" + AlternatePublisherMachine + ":8013/MySubscriptionManager");
            else
                proxy = new MySubscriptionServiceProxy(Context, "PVMonitorSubscribe_TCP", "net.tcp://" + PublisherMachine + ":8013/MySubscriptionManager");

            if (ManualCredentials)
            {
                proxy.ClientCredentials.Windows.ClientCredential.Domain = Domain;
                proxy.ClientCredentials.Windows.ClientCredential.UserName = Username;
                proxy.ClientCredentials.Windows.ClientCredential.Password = Password;
            }
            return proxy;
        }

        public void Subscribe(String eventName, bool useAlternate)
        {
            try
            {
                if (m_Proxy == null)
                    m_Proxy = GetProxy(useAlternate);
                m_Proxy.Subscribe(eventName);
                LastEventTime = DateTime.Now;
                Subscriptions.Add(eventName);
                UseAlternate = useAlternate;
            }
            catch(Exception e)
            {
                SystemServices.LogMessage("EventSubscriber", "Subscribe - Exception: " + e.Message, LogEntryType.Trace);
                m_Proxy = null; // in a faulted state - cannot be reused
            }
        }

        public void Unsubscribe(String eventName)
        {
            try
            {
                m_Proxy.Unsubscribe(eventName);
                LastEventTime = DateTime.MinValue;
                Subscriptions.Remove(eventName);
                if (!Subscribed)
                {
                    m_Proxy.Close();
                    m_Proxy = null;
                }
            }
            catch(Exception e)
            {
                SystemServices.LogMessage("EventSubscriber", "Unsubscribe - Exception: " + e.Message, LogEntryType.Trace);
            }
        }

        public void Disconnect(bool discardSubscriptions = false)
        {
            foreach (String sub in Subscriptions)
            {
                try
                {
                    m_Proxy.Unsubscribe(sub);
                    LastEventTime = DateTime.MinValue;
                }
                catch (Exception e)
                {
                    SystemServices.LogMessage("EventSubscriber", "Disconnect - Exception: " + e.Message, LogEntryType.Trace);
                }
            }

            try
            {
                m_Proxy.Close();
                LastEventTime = DateTime.MinValue;
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("EventSubscriber", "Disconnect - Close - Exception: " + e.Message, LogEntryType.Trace);
            }
            m_Proxy = null;
            if (discardSubscriptions)
                Subscriptions.Clear();
        }

        public void Reconnect(bool useAlternate)
        {
            if (Subscribed && m_Proxy == null)
            {
                try
                {
                    m_Proxy = GetProxy(useAlternate);
                    foreach (String sub in Subscriptions)
                    {
                        m_Proxy.Subscribe(sub);
                    }
                }
                catch (Exception e)
                {
                    SystemServices.LogMessage("EventSubscriber", "Reconnect - Exception: " + e.Message, LogEntryType.Trace);
                }
            }
        }

        public void SetLocalEvent(String text)
        {
            LastStatusEvent = text;
            LastEventTime = DateTime.Now;
        }

        public String LastStatusEvent 
        {
            get
            {
                return _LastStatusEvent;
            }
            set
            {
                _LastStatusEvent = value;
                OnPropertyChanged(new PropertyChangedEventArgs("LastStatusEvent"));
            }
        }

        public int CurrentYieldPower
        {
            get
            {
                return _CurrentYieldPower;
            }
            set
            {
                _CurrentYieldPower = value;
                OnPropertyChanged(new PropertyChangedEventArgs("CurrentYieldPower"));
            }
        }

        public int CurrentConsumptionPower
        {
            get
            {
                return _CurrentConsumptionPower;
            }
            set
            {
                _CurrentConsumptionPower = value;
                OnPropertyChanged(new PropertyChangedEventArgs("CurrentConsumptionPower"));
            }
        }

        public MTObservableCollection<ActiveEvent> ActiveEvents
        {
            get { return _ActiveEvents;  }
        }
    }
}