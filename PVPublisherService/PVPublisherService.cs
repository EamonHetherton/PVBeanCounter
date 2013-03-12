using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using MackayFisher.Utilities;
using System.ServiceModel;
using ServiceModelEx;
using PVSettings;

namespace PVPublisherService
{
    public class EnergyEventsInfo
    {
        public static EnergyEventsEventInfo[] eventTypes = new EnergyEventsEventInfo[0];
    }

    public partial class PVPublisherService : ServiceBase, IEventLoggerLocal
    {
        public SystemServices SystemServices;
        public ApplicationSettings ApplicationSettings;

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
                if (force)
                    SystemServices.LogMessage("PVPublisher", eventText, LogEntryType.Information);
                else
                    SystemServices.LogMessage("PVPublisher", eventText, LogEntryType.Trace);
            }
        }

        // The following loads SystemServices with appropriate parameter settings
        private void LoadLogSettings()
        {
            SystemServices.LogError = ApplicationSettings.LogError;
            SystemServices.LogFormat = ApplicationSettings.LogFormat;
            SystemServices.LogInformation = ApplicationSettings.LogInformation;
            SystemServices.LogStatus = ApplicationSettings.LogStatus;
            SystemServices.LogTrace = ApplicationSettings.LogTrace;
            SystemServices.LogMeterTrace = ApplicationSettings.LogMeterTrace;
            SystemServices.LogMessageContent = ApplicationSettings.LogMessageContent;
            SystemServices.LogDatabase = ApplicationSettings.LogDatabase;
            SystemServices.LogEvent = ApplicationSettings.LogEvent;
        }

        public PVPublisherService()
        {
            InitializeComponent();

            ApplicationSettings = new ApplicationSettings();
            SystemServices = new MackayFisher.Utilities.SystemServices(ApplicationSettings.BuildFileName("PVPublisher.log"));
            ApplicationSettings.SetSystemServices(SystemServices);

            LoadLogSettings();
            
            SystemServices.LogMessage("PVPublisher", "Service starting", LogEntryType.Information);
        }

        protected override void OnStart(string[] args)
        {
            PublisherLogLock = new Object();

            _PublisherLog = new ObservableCollection<String>();
            //gridMainWindow.DataContext = this;

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
            RecordEvent("Service startup complete");
        }

        protected override void OnStop()
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
                RecordEvent("OnStop - Exception: " + e.Message);
                if (e.InnerException != null)
                    RecordEvent("OnStop - InnerException: " + e.InnerException.Message);
            }
            RecordEvent("OnStop - stopped");
        }

        private enum CustomCommands { ReloadSettings = 128 };

        protected override void OnCustomCommand(int command)
        {
            try
            {
                if (command == (int)CustomCommands.ReloadSettings)
                {
                    SystemServices.LogMessage("PVPublisher", "Reloading settings", LogEntryType.StatusChange);
                    ApplicationSettings.ReloadSettings();
                    LoadLogSettings();
                }
                else
                    base.OnCustomCommand(command);
            }
            catch (Exception e)
            {
                SystemServices.LogMessage("PVPublisher", "OnCustomCommand - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw e;
            }
        }
    }
}
