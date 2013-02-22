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
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;
using System.ServiceModel;
using ServiceModelEx;

namespace PVPublisherInteractive
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window, IEventLoggerLocal
    {
        ServiceHost publishServiceHost = null;
        ServiceHost subscriptionManagerHost = null;

        public ObservableCollection<String> _PublisherLog;
        private Object PublisherLogLock;


        public void RecordEvent(String eventText, bool force = false)
        {
            lock (PublisherLogLock)
            {
                _PublisherLog.Add(DateTime.Now.ToString() + " : " + eventText);
                if (_PublisherLog.Count > 200)
                    _PublisherLog.RemoveAt(0);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            PublisherLogLock = new Object();

            _PublisherLog = new ObservableCollection<String>();
            gridMainWindow.DataContext = this;

            RecordEvent("Startup");

            MyPublishService.SetEventLogger(this);
            try
            {
                RecordEvent("Creating Publisher");
                publishServiceHost = new ServiceHost(typeof(MyPublishService), new Uri("http://localhost:8008/"));
                RecordEvent("Opening Publisher");
                publishServiceHost.Open();
            }
            catch (Exception e)
            {
                publishServiceHost = null;
                RecordEvent("new ServiceHost(typeof(MyPublishService)... - Exception: " + e.Message);
                if (e.InnerException != null)
                    RecordEvent("new ServiceHost(typeof(MyPublishService)... - InnerException: " + e.InnerException.Message);
            }

            MySubscriptionService.SetEventLogger(this);
            try
            {
                RecordEvent("Creating Subscription Listener");
                subscriptionManagerHost = new ServiceHost(typeof(MySubscriptionService), new Uri("http://localhost:8009/"));
                RecordEvent("Opening Subscription Listener");
                subscriptionManagerHost.Open();
            }
            catch (Exception e)
            {
                subscriptionManagerHost = null;
                RecordEvent("new ServiceHost(typeof(MySubscriptionService)... - Exception: " + e.Message);
                if (e.InnerException != null)
                    RecordEvent("new ServiceHost(typeof(MySubscriptionService)... - InnerException: " + e.InnerException.Message);
            }
            RecordEvent("MainWindow constructor complete");
        }

        public ObservableCollection<String> PublisherLog { get { return _PublisherLog; } }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e1)
        {
            try
            {
                if (publishServiceHost != null)
                    publishServiceHost.Close();
                if (subscriptionManagerHost != null)
                    subscriptionManagerHost.Close();
            }
            catch (Exception e)
            {
                RecordEvent("Window_Closing - Exception: " + e.Message);
                if (e.InnerException != null)
                    RecordEvent("Window_Closing - InnerException: " + e.InnerException.Message);
            }
        }
    }
}
