/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using System.ServiceProcess;
using System.Threading;
using MackayFisher.Utilities;

namespace PVBeanCounter
{
    public delegate void ServiceStatusChangeNotification(ServiceControllerStatus status);

    internal class ManageService
    {
        const String PVServiceName = "PVService";
        const String PVPublisherServiceName = "PVPublisherService";
        private ServiceStatusChangeNotification ServiceStatusChangeNotification;
        private System.Threading.Thread MonitorThread;
        private SynchronizationContext SynchronizationContext;
        private bool RunMonitor = false;

        public bool ForceRefresh = false;

        private ServiceControllerStatus serviceStatus = ServiceControllerStatus.Stopped;
        private MackayFisher.Utilities.ServiceManager PVServiceManager;
        private MackayFisher.Utilities.ServiceManager PVPublisherManager;

        public ManageService()
        {
            PVServiceManager = new MackayFisher.Utilities.ServiceManager(PVServiceName);
            PVPublisherManager = new MackayFisher.Utilities.ServiceManager(PVPublisherServiceName);
        }

        internal void StartService(bool useEvents)
        {
            serviceStatus = ServiceControllerStatus.StartPending;

            if (useEvents && PVPublisherManager.ServiceExists)
            {
                try
                {
                    if (PVPublisherManager.GetServiceStatus() == ServiceControllerStatus.Stopped)
                        PVPublisherManager.StartService();
                }
                catch (Exception)
                {
                }
            }

            try
            {
                PVServiceManager.StartService();
                PVServiceManager.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(30000));
            }
            catch (Exception)
            {
            }
        }

        internal void StopService(bool useEvents)
        {
            serviceStatus = ServiceControllerStatus.StopPending;

            if (!useEvents && PVPublisherManager.ServiceExists)
            {
                // leave publisher running unless emit events has been turned off
                // this prevents clients to publisher from failing when the service is offline for maint
                try
                {
                    if (PVPublisherManager.GetServiceStatus() == ServiceControllerStatus.Running)
                        PVPublisherManager.StopService();
                }
                catch (Exception)
                {
                }
            }

            try
            {
                PVServiceManager.StopService();
                PVServiceManager.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(120000));
            }
            catch (Exception)
            {
            }
        }

        private enum CustomCommands
        {
            ReloadSettings = 128,
            ReleaseErrorLoggers = 129
        };


        internal void ReloadSettings()
        {
            try
            {
                PVServiceManager.CustomCommand((int)CustomCommands.ReloadSettings);
                if (PVPublisherManager.ServiceExists && PVPublisherManager.GetServiceStatus() == ServiceControllerStatus.Running)
                    PVPublisherManager.CustomCommand((int)CustomCommands.ReloadSettings);
            }
            catch (Exception)
            {
            }
        }

        internal void ReleaseErrorLoggers()
        {
            try
            {
                PVServiceManager.CustomCommand((int)CustomCommands.ReleaseErrorLoggers);
            }
            catch (Exception)
            {
            }
        }

        internal void MonitorService()
        {
            serviceStatus = ServiceControllerStatus.Stopped;
            System.Threading.Thread.Sleep(3000);

            bool first = true;

            while (RunMonitor)
            {
                try
                {
                    ServiceControllerStatus status = PVServiceManager.GetServiceStatus();
                    
                    if (status != serviceStatus || first || ForceRefresh)
                    {
                        SynchronizationContext.Post(new SendOrPostCallback(delegate
                                                                            {
                                                                                ServiceStatusChangeNotification(status);
                                                                            }), null);

                        //ServiceStatusChangeNotification(svc.Status);
                        first = ForceRefresh; // avoid race - do it twice
                        ForceRefresh = false;
                    }
                    serviceStatus = status;
                }
                catch (Exception)
                {
                    SynchronizationContext.Post(new SendOrPostCallback(delegate
                        {
                            ServiceStatusChangeNotification(ServiceControllerStatus.Stopped);
                        }), null);
                }
                System.Threading.Thread.Sleep(3000);
            }
        }

        internal void StartMonitorService(ServiceStatusChangeNotification serviceStatusChangeNotification)
        {
            SynchronizationContext = System.Threading.SynchronizationContext.Current;


            //if (MonitorThread != null)
            //    MonitorThread.Abort();

            RunMonitor = true;
            ServiceStatusChangeNotification = serviceStatusChangeNotification;
            MonitorThread = new Thread(new ThreadStart(MonitorService));
            MonitorThread.Start();
        }

        internal void StopMonitorService()
        {
            RunMonitor = false;
        }
    }
}
