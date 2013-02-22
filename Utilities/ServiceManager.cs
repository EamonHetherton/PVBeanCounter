using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Management;
using System.Windows;

namespace MackayFisher.Utilities
{
    public class ServiceManager
    {
        public enum ServiceAccountNameType
        {
            Security,
            ServiceControl
        }

        private String[] ServiceAccountSecurityNames = { @"NT AUTHORITY\LocalService", @"NT AUTHORITY\NetworkService", @"NT AUTHORITY\SYSTEM" };

        public String ServiceName { get; private set; }
        private String ServiceAccountNameInternal;
        public ServiceAccount ServiceAccountType { get; private set; }

        public String ServiceAccountName
        {
            get
            {
                return ServiceAccountNameInternal;
            }
            set
            {
                ServiceAccountNameInternal = value.Trim();
                if (ServiceAccountNameInternal == "Local Service")
                    ServiceAccountType = ServiceAccount.LocalService;
                else if (ServiceAccountNameInternal == "Network Service")
                    ServiceAccountType = ServiceAccount.NetworkService;
                else if (ServiceAccountNameInternal == "Local System")
                    ServiceAccountType = ServiceAccount.LocalSystem;
                else
                    ServiceAccountType = ServiceAccount.User;
            }
        }

        public String ServiceAccountSecurityName
        {
            get
            {
                if (ServiceAccountType >= System.ServiceProcess.ServiceAccount.User)
                    return ServiceAccountNameInternal;
                else
                    return ServiceAccountSecurityNames[(int)ServiceAccountType];
            }
        }

        public String ServiceAccountServiceName
        {
            get
            {
                if (ServiceAccountType >= System.ServiceProcess.ServiceAccount.User)
                    return ServiceAccountNameInternal;
                else if (ServiceAccountType == System.ServiceProcess.ServiceAccount.LocalSystem)
                    return "LocalSystem";
                else
                    return ServiceAccountSecurityNames[(int)ServiceAccountType];
            }
        }

        public String ServiceAccountServiceNameCompare
        {
            get
            {
                if (ServiceAccountType >= System.ServiceProcess.ServiceAccount.User)
                {
                    String name = ServiceAccountNameInternal;
                    int pos = name.IndexOf('\\');
                    if (pos >= 0)
                    {
                        return @".\" + name.Substring(pos + 1);
                    }
                    else
                        return @".\" + name;
                }
                else
                    return ServiceAccountSecurityNames[(int)ServiceAccountType];
            }
        }

        public bool ServiceExists 
        {
            get
            {
                return (Service != null);
            }   
        }

        private ServiceController Service
        {
            get
            {
                ServiceController[] scServices;
                scServices = ServiceController.GetServices();

                foreach (ServiceController svc in scServices)
                {
                    if (svc.ServiceName == ServiceName)
                        return svc;
                }
                return null;
            }
        }

        private String CurrentServiceAccount
        {
            get
            {
                SelectQuery query = new System.Management.SelectQuery(string.Format(
                    "select name, startname from Win32_Service where name = '{0}'", ServiceName));
                using (ManagementObjectSearcher searcher =
                    new System.Management.ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject service in searcher.Get())
                    {
                        return service["startname"].ToString();
                    }
                }
                
                return "";
            }
        }

        public bool ServiceAccountInSync
        {
            get
            {
                String name = CurrentServiceAccount.ToUpper();
                return name == ServiceAccountServiceNameCompare.ToUpper();
            }
        }

        public bool SyncServiceCredentials(String password)
        {
            return SetServiceCredentials(password);
        }

        public bool SyncServiceStartup(bool  autoStart)
        {
            bool isAutoStart = IsServiceAutoStart();
            if (isAutoStart != autoStart)
                return SetServiceAutoStart(autoStart);
            else
                return true;
        }

        public ServiceManager(String serviceName)
        {
            ServiceName = serviceName;
            ServiceAccountName = CurrentServiceAccount;
        }

        public ServiceControllerStatus GetServiceStatus()
        {
            ServiceController service = Service;
            if (service == null)
                throw new Exception("ServiceManager.GetServiceStatus - Service: " + ServiceName + " does not exist");
            return service.Status;
        }

        public ServiceControllerStatus StartService()
        {
            ServiceController service = Service;
            if (service == null)
                throw new Exception("ServiceManager.StartService - Service: " + ServiceName + " does not exist");

            ServiceControllerStatus status = service.Status;
            if (status == ServiceControllerStatus.Stopped)
            {
                try
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(30000));
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                }
                catch (Exception e)
                {
                    throw new Exception("ServiceManager.StartService - Service: " + ServiceName + " - Exception: " + e.Message, e);
                }
            }
            else
                throw new Exception("ServiceManager.StartService - Service: " + ServiceName + " Status: " + status.ToString());

            return service.Status;
        }

        public ServiceControllerStatus StopService()
        {
            ServiceController service = Service;
            if (service == null)
                throw new Exception("ServiceManager.StopService - Service: " + ServiceName + " does not exist");

            ServiceControllerStatus status = service.Status;

            if  (status != ServiceControllerStatus.Stopped
                && status != ServiceControllerStatus.StopPending)
            {
                try
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(120000));
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                }
                catch (Exception e)
                {
                    throw new Exception("ServiceManager.StopService - Service: " + ServiceName + " - Exception: " + e.Message, e);
                }
            }
            else
                throw new Exception("ServiceManager.StopService - Service: " + ServiceName + " Status: " + status.ToString());

            return service.Status;
        }

        public ServiceControllerStatus WaitForStatus(ServiceControllerStatus targetStatus, TimeSpan? wait = null)
        {
            ServiceController service = Service;
            if (service == null)
                throw new Exception("ServiceManager.WaitForstatus - Service: " + ServiceName + " does not exist");
            if (wait == null)
                wait = TimeSpan.FromMilliseconds(60000);
            try
            {
                service.WaitForStatus(targetStatus, wait.Value);
            }
            catch (System.ServiceProcess.TimeoutException)
            {
            }
            catch (Exception e)
            {
                throw new Exception("ServiceManager.WaitForStatus - Service: " + ServiceName + " - Exception: " + e.Message, e);
            }

            return service.Status;
        }

        public void CustomCommand(int command)
        {
            ServiceController service = Service;
            if (service == null)
                throw new Exception("ServiceManager.CustomCommand - Service: " + ServiceName + " does not exist");

            ServiceControllerStatus status = service.Status;

            if (status == ServiceControllerStatus.Running)
            {
                try
                {
                    service.ExecuteCommand(command);
                }
                catch (Exception e)
                {
                    throw new Exception("ServiceManager.CustomCommand - Service: " + ServiceName + " - Exception: " + e.Message, e);
                }
            }
            else
                throw new Exception("ServiceManager.CustomCommand - Service: " + ServiceName + " Status: " + status.ToString());
        }

        #region DLLImport
        [DllImport("advapi32.dll")]
        public static extern IntPtr OpenSCManager(string lpMachineName, string lpSCDB, int scParameter);
        [DllImport("Advapi32.dll")]
        public static extern IntPtr CreateService(IntPtr SC_HANDLE, string lpSvcName, string lpDisplayName,
            int dwDesiredAccess, int dwServiceType, int dwStartType, int dwErrorControl, string lpPathName, string lpLoadOrderGroup, int lpdwTagId, 
            string lpDependencies, string lpServiceStartName, string lpPassword);
        [DllImport("Advapi32.dll")]
        public static extern bool ChangeServiceConfig(IntPtr SC_HANDLE, int dwServiceType, int dwStartType, int dwErrorControl, string lpPathName,
            string lpLoadOrderGroup, int lpdwTagId, string lpDependencies, string lpServiceStartName, string lpPassword, string lpDisplayName);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int QueryServiceConfig(IntPtr SC_HANDLE, IntPtr queryServiceConfig, int dwBufferSize, ref int dwBytesNeeded); 

        [DllImport("advapi32.dll")]
        public static extern void CloseServiceHandle(IntPtr SCHANDLE);
        [DllImport("advapi32.dll")]
        public static extern int StartService(IntPtr SVHANDLE, int dwNumServiceArgs, string lpServiceArgVectors);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern IntPtr OpenService(IntPtr SCHANDLE, string lpSvcName, int dwNumServiceArgs);
        [DllImport("advapi32.dll")]
        public static extern int DeleteService(IntPtr SVHANDLE);
        [DllImport("kernel32.dll")]
        public static extern int GetLastError();
        #endregion DLLImport

        [StructLayout(LayoutKind.Sequential)]
        private struct QueryServiceConfigStruct
        {
            public int serviceType;
            public int startType;
            public int errorControl;
            public IntPtr binaryPathName;
            public IntPtr loadOrderGroup;
            public int tagID;
            public IntPtr dependencies;
            public IntPtr startName;
            public IntPtr displayName;
        }

        private struct ServiceInfo
        {
            public int serviceType;
            public int startType;
            public int errorControl;
            public string binaryPathName;
            public string loadOrderGroup;
            public int tagID;
            public string dependencies;
            public string startName;
            public string displayName;
        }
 
        private static ServiceInfo GetServiceInfo(string ServiceName)
        {
            if (ServiceName.Equals(""))
                throw new NullReferenceException("ServiceName must contain a valid service name.");

            IntPtr sc_handle = OpenSCManager(null, null, STANDARD_RIGHTS_REQUIRED);
            if (sc_handle.ToInt32() == 0)
                throw new NullReferenceException();
            
            IntPtr service = OpenService(sc_handle, ServiceName, SERVICE_QUERY_CONFIG);
            if (service.ToInt32() <= 0)
                throw new NullReferenceException();

            int bytesNeeded = 5;
            QueryServiceConfigStruct qscs = new QueryServiceConfigStruct();
            IntPtr qscPtr = Marshal.AllocCoTaskMem(0);

            int retCode = QueryServiceConfig(service, qscPtr, 0, ref bytesNeeded);
            if (retCode == 0 && bytesNeeded == 0)
            {
                throw new Win32Exception();
            }
            else
            {
                qscPtr = Marshal.AllocCoTaskMem(bytesNeeded);
                retCode = QueryServiceConfig(service, qscPtr, bytesNeeded, ref bytesNeeded);
                if (retCode == 0)
                {
                    throw new Win32Exception();
                }
                qscs.binaryPathName = IntPtr.Zero;
                qscs.dependencies = IntPtr.Zero;
                qscs.displayName = IntPtr.Zero;
                qscs.loadOrderGroup = IntPtr.Zero;
                qscs.startName = IntPtr.Zero;

                qscs = (QueryServiceConfigStruct)
                Marshal.PtrToStructure(qscPtr, new QueryServiceConfigStruct().GetType());
            }

            ServiceInfo serviceInfo = new ServiceInfo();
            serviceInfo.binaryPathName = Marshal.PtrToStringAuto(qscs.binaryPathName);
            serviceInfo.dependencies = Marshal.PtrToStringAuto(qscs.dependencies);
            serviceInfo.displayName = Marshal.PtrToStringAuto(qscs.displayName);
            serviceInfo.loadOrderGroup = Marshal.PtrToStringAuto(qscs.loadOrderGroup);
            serviceInfo.startName = Marshal.PtrToStringAuto(qscs.startName);

            serviceInfo.errorControl = qscs.errorControl;
            serviceInfo.serviceType = qscs.serviceType;
            serviceInfo.startType = qscs.startType;
            serviceInfo.tagID = qscs.tagID;

            Marshal.FreeCoTaskMem(qscPtr);
            return serviceInfo;            
        }

        private bool SetServiceAutoStart(bool autoStart)
        {
            ServiceController service = Service;

            if (service != null)
            {
                String userName = ServiceAccountSecurityName;
                bool res;
                int startType = autoStart ? SERVICE_AUTO_START : SERVICE_DEMAND_START;

                res = ChangeServiceConfig(service.ServiceHandle.DangerousGetHandle(),
                    SERVICE_WIN32_OWN_PROCESS, startType, SERVICE_ERROR_NORMAL,
                    null, null, 0, null, null, null, null);

                return res;
            }
            else
                return false;
        }

        #region Constants declaration.
        const int GENERIC_WRITE = 0x40000000;
        const int SC_MANAGER_CREATE_SERVICE = 0x0002;
        const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        //int SERVICE_DEMAND_START = 0x00000003;
        const int SERVICE_ERROR_NORMAL = 0x00000001;
        const int STANDARD_RIGHTS_REQUIRED = 0xF0000;
        const int SERVICE_QUERY_CONFIG = 0x0001;
        const int SERVICE_CHANGE_CONFIG = 0x0002;
        const int SERVICE_QUERY_STATUS = 0x0004;
        const int SERVICE_ENUMERATE_DEPENDENTS = 0x0008;
        const int SERVICE_START = 0x0010;
        const int SERVICE_STOP = 0x0020;
        const int SERVICE_PAUSE_CONTINUE = 0x0040;
        const int SERVICE_INTERROGATE = 0x0080;
        const int SERVICE_USER_DEFINED_CONTROL = 0x0100;

        const int SERVICE_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED |
                                    SERVICE_QUERY_CONFIG |
                                    SERVICE_CHANGE_CONFIG |
                                    SERVICE_QUERY_STATUS |
                                    SERVICE_ENUMERATE_DEPENDENTS |
                                    SERVICE_START |
                                    SERVICE_STOP |
                                    SERVICE_PAUSE_CONTINUE |
                                    SERVICE_INTERROGATE |
                                    SERVICE_USER_DEFINED_CONTROL);

        const int SERVICE_AUTO_START = 0x00000002;
        const int SERVICE_DEMAND_START = 0x00000003;
        
        #endregion Constants declaration.

        private bool SetServiceCredentials(String password)
        {
            ServiceController service = Service;

            if (service != null)
            {
                String userName = ServiceAccountSecurityName;
                bool res;
                if (ServiceAccountType == System.ServiceProcess.ServiceAccount.User)
                    res =  ChangeServiceConfig(service.ServiceHandle.DangerousGetHandle(),
                        SERVICE_WIN32_OWN_PROCESS, SERVICE_AUTO_START, SERVICE_ERROR_NORMAL,
                        null, null, 0, null, userName, password, null);
                else
                {
                    res = ChangeServiceConfig(service.ServiceHandle.DangerousGetHandle(),
                        SERVICE_WIN32_OWN_PROCESS, SERVICE_AUTO_START, SERVICE_ERROR_NORMAL,
                        null, null, 0, null, userName, null, null); 
                }
                return res;
            }
            else
                return false;
        }

        public bool IsServiceAutoStart()
        {
            ServiceInfo info = GetServiceInfo(ServiceName);
            return info.startType == SERVICE_AUTO_START;
        }

        public bool InstallService(string svcPath, string svcName, string svcDispName)
        {
            try
            {
                IntPtr sc_handle = OpenSCManager(null, null, SC_MANAGER_CREATE_SERVICE);
                if (sc_handle.ToInt32() != 0)
                {
                    IntPtr sv_handle = CreateService(sc_handle, svcName, svcDispName,
                        SERVICE_ALL_ACCESS, SERVICE_WIN32_OWN_PROCESS, SERVICE_AUTO_START, SERVICE_ERROR_NORMAL,
                        svcPath, null, 0, null, null, null);
                    if (sv_handle.ToInt32() == 0)
                    {
                        CloseServiceHandle(sc_handle);
                        return false;
                    }
                    else
                    {
                        //now trying to start the service
                        int i = StartService(sv_handle, 0, null);
                        // If the value i is zero, then there was an error starting the service.
                        // note: error may arise if the service is already running or some other problem.
                        if (i == 0)
                        {
                            //Console.WriteLine("Couldnt start service");
                            return false;
                        }
                        //Console.WriteLine("Success");
                        CloseServiceHandle(sc_handle);
                        return true;
                    }
                }
                else
                    //Console.WriteLine("SCM not opened successfully");
                    return false;
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        /// <summary>
        /// This method uninstalls the service from the service conrol manager.
        /// </summary>
        /// <param name="svcName">Name of the service to uninstall.</param>
        public bool UnInstallService(string svcName)
        {
            
            IntPtr sc_hndl;
            try
            {
                sc_hndl = OpenSCManager(null, null, GENERIC_WRITE);
            }
            catch (Exception)
            {
                return false;
            }

            if (sc_hndl.ToInt32() != 0)
            {
                int DELETE = 0x10000;
                IntPtr svc_hndl;
                try
                {
                    svc_hndl = OpenService(sc_hndl, svcName, DELETE);
                }
                catch
                {
                    CloseServiceHandle(sc_hndl);
                    return false;
                }

                //Console.WriteLine(svc_hndl.ToInt32());
                if (svc_hndl.ToInt32() != 0)
                {
                    int i;
                    try
                    {
                        i = DeleteService(svc_hndl);

                        CloseServiceHandle(svc_hndl);
                        CloseServiceHandle(sc_hndl);
                        return (i != 0);
                    }
                    catch (Exception)
                    {
                    }
                    return false;
                }
                else
                {
                    CloseServiceHandle(sc_hndl);
                    return false;
                }
            }
            else
                return false;
        }
    }
}
