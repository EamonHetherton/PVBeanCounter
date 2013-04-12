using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ServiceProcess;
using PVSettings;
using TimeControl;
using MackayFisher.Utilities;

namespace PVBeanCounter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        ManageService ManageService; 
        ApplicationSettings ApplicationSettings;
        SystemServices SystemServices;
        CheckEnvironment CheckEnvironment;
        DeviceDisplay DeviceUpdate;
        //OwlDatabaseInfo OwlDatabaseInfo;

        bool saveRequired = false;
        bool checkRequired = false;

        PvOutputSiteSettings PVSettings = null;
        DeviceManagerSettings DeviceManagerSettings = null;
        DeviceManagerDeviceSettings DeviceManagerDeviceSettings = null;

        void SettingsChangedCallback()
        {
            butSavePVSettings.IsEnabled = true;
            comboBoxStandardDB.Text = "Custom";
        }

        void SettingsSavedCallback()
        {
            butSavePVSettings.IsEnabled = false;
            ManageService.ForceRefresh = saveRequired;
            saveRequired = false;
        }

        void SettingsSaved()
        {
            butSavePVSettings.IsEnabled = saveRequired;
        }   

        private void SyncServiceStartup()
        {
            try
            {
                ServiceManager PVBCService;
                ServiceManager PublisherService;
                PVBCService = new ServiceManager("PVService");
                PublisherService = new ServiceManager("PVPublisherService");

                PVBCService.SyncServiceStartup(ApplicationSettings.AutoStartPVBCService);
                PublisherService.SyncServiceStartup(ApplicationSettings.AutoStartPVBCService && ApplicationSettings.EmitEvents);
            }
            catch (Exception)
            {
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Thread.Sleep(30000);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ManageService = new ManageService();                
                ApplicationSettings = new PVSettings.ApplicationSettings();
                SystemServices = new MackayFisher.Utilities.SystemServices("Configuration.log");
                //SystemServices = new SystemServices();
            
                CheckEnvironment = new CheckEnvironment(ApplicationSettings, SystemServices);

                ApplicationSettings.SetSystemServices(SystemServices);

                //OwlDatabaseInfo = null;

                gridPVSettings.DataContext = ApplicationSettings;

                comboBoxMMPortName.ItemsSource = SerialPortSettings.SerialPortsList;
                comboBoxMMBaudRate.ItemsSource = SerialPortSettings.BaudRateList;
                comboBoxMMParity.ItemsSource = SerialPortSettings.ParityList;
                comboBoxMMDataBits.ItemsSource = SerialPortSettings.DataBitsList;
                comboBoxMMStopBits.ItemsSource = SerialPortSettings.StopBitsList;
                comboBoxMMHandshake.ItemsSource = SerialPortSettings.HandshakeList;

                DeviceUpdate = new DeviceDisplay(ApplicationSettings, SystemServices);

                dataGridInverterList.ItemsSource = DeviceUpdate.DeviceList;
                //applianceConsumeSiteIdColumn.ItemsSource = ApplicationSettings.PvOutputSiteList;
                //comboBoxMAConsumeSystem.ItemsSource = ApplicationSettings.PvOutputSiteList;

                ManageService.StartMonitorService(ServiceStatusCallback);
                Closing += new System.ComponentModel.CancelEventHandler(OnClosing);

                ApplicationSettings.SetNotifications(SettingsChangedCallback, SettingsSavedCallback);

                buttonDeleteSite.IsEnabled = ApplicationSettings.PvOutputSystemList.Count > 1;

                SyncServiceStartup();

                LoadModbusSettings();
                saveRequired = ApplicationSettings.InitialSave;
                checkRequired = ApplicationSettings.InitialCheck;
                if (saveRequired || checkRequired)
                    butStartService.IsEnabled = false;
                butSavePVSettings.IsEnabled = saveRequired;

                if (dataGridDeviceList != null && dataGridDeviceList.SelectedItem != null)
                {
                    var selected = dataGridDeviceList.SelectedItem;
                    dataGridDeviceList.SelectedItem = null;
                    dataGridDeviceList.SelectedItem = selected;
                }
                consolidateToDeviceColumn.ItemsSource = ApplicationSettings.AllConsolidationDevicesList;
                consolidateFromDeviceColumn.ItemsSource = ApplicationSettings.AllDevicesList;

                SetDeviceVisibility();
            }
            catch (Exception ex)
            {
                SystemServices.LogMessage("Window_Loaded", "Exception: " + ex.Message, LogEntryType.ErrorMessage);
                System.Windows.MessageBox.Show(ex.Message, "Startup Exception Occurred", MessageBoxButton.OK);
            }
        }

        private void LoadModbusSettings()
        {
            comboBoxProtocol.ItemsSource = ApplicationSettings.DeviceManagementSettings.ProtocolList;
            comboBoxMessageInterval.ItemsSource = ApplicationSettings.DeviceManagementSettings.IntervalList;
            comboBoxDBInterval.ItemsSource = ApplicationSettings.DeviceManagementSettings.IntervalList;
            comboBoxDeviceQueryInterval.ItemsSource = ApplicationSettings.DeviceManagementSettings.IntervalList;
            comboBoxDeviceDBInterval.ItemsSource = ApplicationSettings.DeviceManagementSettings.IntervalList;

            buttonDeleteDeviceMgr.IsEnabled = ApplicationSettings.DeviceManagerList.Count > 0;
        }

        private void butSavePVSettings_Click(object sender, RoutedEventArgs e)
        {
            String envLog;

            if (!CheckEnvironment.SetupEnvironment(out envLog))
            {
                SystemServices.LogMessage("SaveSettings", "Fail Environment Check: " + envLog, LogEntryType.ErrorMessage);
                System.Windows.MessageBox.Show(envLog, "Environment Setup Problem", MessageBoxButton.OK);
            }

            try
            {
                ApplicationSettings.InitialSave = false;
                ApplicationSettings.SaveSettings();
                SystemServices.LogMessage("SaveSettings", "Save Location: " + ApplicationSettings.DefaultDirectory, LogEntryType.ErrorMessage);
                
                ManageService.ReloadSettings();
            }
            catch (Exception ex)
            {
                SystemServices.LogMessage("SaveSettings", "Exception: " + ex.Message, LogEntryType.ErrorMessage);
                System.Windows.MessageBox.Show("Exception saving settings: " + ex.Message, "Save Settings Exception", MessageBoxButton.OK);
            }          
        }

        private void butDefaultLogSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!ApplicationSettings.LogInformation)
                ApplicationSettings.LogInformation = true;
            if (ApplicationSettings.LogTrace)
                ApplicationSettings.LogTrace = false;
            if (ApplicationSettings.LogDatabase)
                ApplicationSettings.LogDatabase = false;
            if (ApplicationSettings.LogEvent)
                ApplicationSettings.LogEvent = false;
            if (ApplicationSettings.LogMeterTrace)
                ApplicationSettings.LogMeterTrace = false;
            if (ApplicationSettings.LogMessageContent)
                ApplicationSettings.LogMessageContent = false;
            if (!ApplicationSettings.LogStatus)
                ApplicationSettings.LogStatus = true;
            if (!ApplicationSettings.LogError)
                ApplicationSettings.LogError = true;

            ApplicationSettings.LogRetainDays = 62;
            ApplicationSettings.NewLogEachDay = true;
            ApplicationSettings.InverterLogs = "";
        }

        private void butReleaseErrorLoggers_Click(object sender, RoutedEventArgs e)
        {
            ManageService.ReleaseErrorLoggers();
        }

        private void butCheckEnv_Click(object sender, RoutedEventArgs e)
        {
            String envLog;
            bool res = CheckEnvironment.SetupEnvironment(out envLog);
            if (res)
            {
                SystemServices.LogMessage("CheckEnvironment", envLog, LogEntryType.ErrorMessage);
                System.Windows.MessageBox.Show(envLog, "Environment Configured", MessageBoxButton.OK);
                ManageService.ForceRefresh = checkRequired;
                checkRequired = false;
                ApplicationSettings.InitialCheck = false;
            }
            else
            {
                SystemServices.LogMessage("CheckEnvironment", envLog, LogEntryType.ErrorMessage);
                System.Windows.MessageBox.Show(envLog, "Environment Problem Detected", MessageBoxButton.OK);
            }
        }

        private void butStartService_Click(object sender, RoutedEventArgs e)
        {
            butStopService.IsEnabled = false;
            butStartService.IsEnabled = false;
            ManageService.StartService(ApplicationSettings.EmitEvents);
        }

        private void butStopService_Click(object sender, RoutedEventArgs e)
        {
            butStopService.IsEnabled = false;
            butStartService.IsEnabled = false;
            ManageService.StopService(ApplicationSettings.EmitEvents);
        }

        public void OnClosing(Object sender, System.ComponentModel.CancelEventArgs e)
        {
            ManageService.StopMonitorService();
        }

        internal void ServiceStatusCallback(ServiceControllerStatus status)
        {

            if (status == ServiceControllerStatus.Running)
            {
                butStopService.IsEnabled = true;
                butStartService.IsEnabled = false;
                txtServiceStatus.Text = "Running";
            }
            else if (status == ServiceControllerStatus.Stopped)
            {
                butStopService.IsEnabled = false;
                butStartService.IsEnabled = !saveRequired && !checkRequired;
                if (checkRequired)
                    txtServiceStatus.Text = "Stopped - Check Required";
                else
                    txtServiceStatus.Text = "Stopped";
            }
            else
            {
                butStopService.IsEnabled = false;
                butStartService.IsEnabled = false;
                if (status == ServiceControllerStatus.ContinuePending)
                    txtServiceStatus.Text = "Continue Pending";
                else if (status == ServiceControllerStatus.Paused)
                    txtServiceStatus.Text = "Paused";
                else if (status == ServiceControllerStatus.PausePending)
                    txtServiceStatus.Text = "Pause Pending";
                else if (status == ServiceControllerStatus.StartPending)
                    txtServiceStatus.Text = "Start Pending";
                else if (status == ServiceControllerStatus.StopPending)
                    txtServiceStatus.Text = "Stop Pending";
            }
        }

        private void butDefaultDirectory_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog myDialog = new FolderBrowserDialog();
            myDialog.ShowNewFolderButton = true;
            myDialog.SelectedPath = ApplicationSettings.DefaultDirectory;
            String permResult = "";

            if (myDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ApplicationSettings.DefaultDirectory = myDialog.SelectedPath;
            }

            if (permResult != "")
            {
                System.Windows.MessageBox.Show(permResult, "Default Directory - Permissions Problem", MessageBoxButton.OK);
            }
        }

        private void butExecutable_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog myDialog = new OpenFileDialog();
            myDialog.CheckFileExists = true;
            myDialog.Multiselect = false;
            myDialog.FileName = ((DeviceManagerSettings)gridDeviceManagers.DataContext).ExecutablePath;
            myDialog.InitialDirectory = System.IO.Path.GetDirectoryName(myDialog.FileName);
            
            if (myDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ((DeviceManagerSettings)gridDeviceManagers.DataContext).ExecutablePath = myDialog.FileName;
            }
        }

        private void butPushDirectory_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog myDialog = new FolderBrowserDialog();
            myDialog.ShowNewFolderButton = true;
            //myDialog.SelectedPath =  ((InverterManagerSettings)tabControlInverterManagers.DataContext).WebBoxPushDirectory;
            String permResult = "";

            if (myDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //((InverterManagerSettings)tabControlInverterManagers.DataContext).WebBoxPushDirectory = myDialog.SelectedPath;
            }

            if (permResult != "")
            {
                System.Windows.MessageBox.Show(permResult, "WebBox Push Directory - Permissions Problem", MessageBoxButton.OK);
            }
        }

        private void dataGridPvOutputSiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PVSettings = (PvOutputSiteSettings) dataGridPvOutputSiteIds.SelectedItem;

            gridPvOutputSite.DataContext = PVSettings;
            if (PVSettings.HaveSubscription)
                SetLiveDaysComboBox(90);
            else
                SetLiveDaysComboBox(14);
        }

        private void dataGridDeviceManagers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeviceManagerSettings = (DeviceManagerSettings)dataGridDeviceManagers.SelectedItem;
            if (DeviceManagerSettings != gridDeviceManagers.DataContext)
                SetDeviceContext(null);
            gridDeviceManagers.DataContext = null;
            //comboBoxListenerDevice.ItemsSource = mmSettings == null ? null : mmSettings.DeviceListItems;
            gridDeviceManagers.DataContext = DeviceManagerSettings;
            comboBoxProtocol_SelectionChanged();
            //gridDeviceManagerDeviceList.DataContext = mmSettings;
            buttonDeleteDeviceMgr.IsEnabled = DeviceManagerSettings != null;
            if (e.RemovedItems.Count > 0)
                ((DeviceManagerSettings)(e.RemovedItems[0])).IsSelected = false;
            if (DeviceManagerSettings != null)
            {
                DeviceManagerSettings.IsSelected = true;
                if (DeviceManagerSettings.ManagerType == DeviceManagerType.Consolidation)
                    checkBoxDevMgrEnabled.Visibility = System.Windows.Visibility.Collapsed;
                else
                    checkBoxDevMgrEnabled.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void buttonAddSite_Click(object sender, RoutedEventArgs e)
        {
            ApplicationSettings.AddPvOutputSite();
            if (ApplicationSettings.PvOutputSystemList.Count > 0)
                dataGridPvOutputSiteIds.SelectedItem = ApplicationSettings.PvOutputSystemList[ApplicationSettings.PvOutputSystemList.Count - 1];
            buttonDeleteSite.IsEnabled = ApplicationSettings.PvOutputSystemList.Count > 1;
        }

        private void buttonDeleteSite_Click(object sender, RoutedEventArgs e)
        {
            int cur = dataGridPvOutputSiteIds.SelectedIndex;

            if (cur == -1)
                return;

            ApplicationSettings.DeletePvOutputSite((PvOutputSiteSettings)dataGridPvOutputSiteIds.SelectedItem);

            if (ApplicationSettings.PvOutputSystemList.Count > 0)
                if (cur < ApplicationSettings.PvOutputSystemList.Count)
                    dataGridPvOutputSiteIds.SelectedItem = ApplicationSettings.PvOutputSystemList[cur];
                else
                    dataGridPvOutputSiteIds.SelectedItem = ApplicationSettings.PvOutputSystemList[ApplicationSettings.PvOutputSystemList.Count - 1];

            buttonDeleteSite.IsEnabled = ApplicationSettings.PvOutputSystemList.Count > 1;
        }

        private void buttonInverterRefresh_Click(object sender, RoutedEventArgs e)
        {
            DeviceDisplay iu = new DeviceDisplay(ApplicationSettings, SystemServices);
            DeviceUpdate.LoadDeviceList();
        }

        private void butBrowseOwlDb_Click(object sender, RoutedEventArgs e)
        {
            //OwlMeterManagerSettings ommSettings = (OwlMeterManagerSettings)canvasMeterDetail.DataContext;
            OpenFileDialog myDialog = new OpenFileDialog();
            myDialog.CheckFileExists = true;
            myDialog.Multiselect = false;
            //myDialog.FileName = ommSettings.OwlDatabase;
            if (myDialog.FileName != "")
            {
                try
                {
                    myDialog.InitialDirectory = System.IO.Path.GetDirectoryName(myDialog.FileName);
                }
                catch (Exception)
                {
                    myDialog.FileName = "";
                }
            }
            if (myDialog.FileName == "")
            {
                myDialog.InitialDirectory = @"C:\ProgramData\2SE";
                myDialog.FileName = "be.db";
            }

            if (myDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //ommSettings.OwlDatabase = myDialog.FileName;
            }
        }

        private void checkBoxOwlReload_Click(object sender, RoutedEventArgs e)
        {
            if (checkBoxOwlReload.IsChecked == null || !checkBoxOwlReload.IsChecked.Value)
            {
                datePickerOwlReload.IsEnabled = false;
            }
            else
            {
                datePickerOwlReload.IsEnabled = true;
            }
        }

        private void passwordBoxServiceAccount_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ApplicationSettings.ServiceAccountPassword = passwordBoxServiceAccount.Password;
            ApplicationSettings.ServiceAccountName = ApplicationSettings.ServiceAccountName;
            ApplicationSettings.ServiceAccountRequiresPassword = true;
            ApplicationSettings.ServiceDetailsChanged = true;
        }

        private void comboBoxServiceAccount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBoxServiceAccount.SelectedIndex == -1)
            {
                ApplicationSettings.ServiceAccountRequiresPassword = true;
                passwordBoxServiceAccount.IsEnabled = true;
                checkBoxServiceRequiresPassword.IsEnabled = true;
            }
            else
            {
                if (ApplicationSettings.ServiceAccountRequiresPassword)
                    ApplicationSettings.ServiceAccountRequiresPassword = false;
                    
                passwordBoxServiceAccount.IsEnabled = false;
                checkBoxServiceRequiresPassword.IsEnabled = false;
            }
        }

        private void checkBoxServiceRequiresPassword_Click(object sender, RoutedEventArgs e)
        {
            if (!checkBoxServiceRequiresPassword.IsChecked.Value)
            {
                ApplicationSettings.ServiceAccountPassword = "";
                passwordBoxServiceAccount.Password = "";
            }
        }

        private void textBoxOwlDb_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            try
            {
                //MeterManagerSettings mmSettings = (MeterManagerSettings)dataGridMeterManagers.SelectedItem;
                //OwlDatabaseInfo = new OwlDatabaseInfo((OwlMeterManagerSettings)mmSettings, SystemServices);
                //OwlDatabaseInfo.LoadApplianceList();

                //if (OwlDatabaseInfo.OwlAppliances.Count < 1)
                {
                    int cnt = 0;
                    //foreach (MeterApplianceSettings appl in mmSettings.ApplianceList)
                    {
                        //appl.ApplianceNo = cnt.ToString();
                        cnt++;
                    }
                }

                //comboBoxOwlApplianceNo.ItemsSource = OwlDatabaseInfo.OwlAppliances;
            }
            catch
            {
            }

        }

        private void butInverterLogs_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog myDialog = new FolderBrowserDialog();
            myDialog.ShowNewFolderButton = true;
            if (ApplicationSettings.InverterLogs != "")
                myDialog.SelectedPath = ApplicationSettings.InverterLogs;
            else
                myDialog.SelectedPath = ApplicationSettings.DefaultDirectory;
            String permResult = "";

            if (myDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ApplicationSettings.InverterLogs = myDialog.SelectedPath;
            }

            if (permResult != "")
            {
                System.Windows.MessageBox.Show(permResult, "Inverter Logs - Permissions Problem", MessageBoxButton.OK);
            }

        }

        private void buttonAddModbusMgr_Click(object sender, RoutedEventArgs e)
        {
            dataGridDeviceManagers.SelectedItem = ApplicationSettings.AddDeviceManager();
            buttonDeleteDeviceMgr.IsEnabled = true;
        }

        private void buttonDeleteModbusMgr_Click(object sender, RoutedEventArgs e)
        {
            DeviceManagerSettings mmSettings = (DeviceManagerSettings)dataGridDeviceManagers.SelectedItem;
            ApplicationSettings.DeleteDeviceManager(mmSettings);
            if (ApplicationSettings.DeviceManagerList.Count < 1)
                buttonDeleteDeviceMgr.IsEnabled = false;
        }

        private void buttonAddDevice_Click(object sender, RoutedEventArgs e)
        {
            DeviceManagerSettings mmSettings = (DeviceManagerSettings)dataGridDeviceManagers.SelectedItem;
            if (mmSettings == null)
                return;
            mmSettings.AddDevice();

            dataGridDeviceList.SelectedItem = mmSettings.DeviceList[mmSettings.DeviceList.Count - 1];
            buttonDeleteDevice.IsEnabled = true;
        }

        private void buttonDeleteDevice_Click(object sender, RoutedEventArgs e)
        {
            DeviceManagerSettings mmSettings = (DeviceManagerSettings)dataGridDeviceManagers.SelectedItem;
            if (mmSettings == null)
                return;
            mmSettings.DeleteDevice((DeviceManagerDeviceSettings)dataGridDeviceList.SelectedItem);
            buttonDeleteDevice.IsEnabled = mmSettings.DeviceList.Count > 0;
        }

        private void SetDeviceContext(DeviceManagerDeviceSettings device)
        {
            gridDevice.DataContext = device;
            gridConsolidations.DataContext = device;
            comboBoxDeviceType.ItemsSource = device == null ? null : device.DeviceManagerSettings.DeviceListItems;
            //comboBoxListenerDevice.ItemsSource = device == null ? null : device.DeviceManagerSettings.DeviceListItems;
        }

        private void SelectDevice()
        {
            DeviceManagerDeviceSettings dSettings = (DeviceManagerDeviceSettings)dataGridDeviceList.SelectedItem;
            if (dSettings != null)
            {
                buttonDeleteDevice.IsEnabled = true;
                if (!dSettings.DeviceManagerSettings.IsSelected)
                    dataGridDeviceManagers.SelectedItem = dSettings.DeviceManagerSettings;                                  
            }
            SetDeviceContext(null);
            if (dSettings != null)
            {
                SetDeviceContext(dSettings);
                dSettings.NotifySelectionChange();
            }

            SetDeviceVisibility();
        }

        private void SetDeviceVisibility()
        {
            DeviceManagerDeviceSettings = (DeviceManagerDeviceSettings)dataGridDeviceList.SelectedItem;
            if (DeviceManagerDeviceSettings == null)
                return;

            if (DeviceManagerDeviceSettings.ConsolidationType.HasValue)
            {
                gridConsolidatedFrom.Visibility = System.Windows.Visibility.Visible;
                gridConsolidationType.Visibility = System.Windows.Visibility.Visible;
                
                if (DeviceManagerDeviceSettings.ConsolidationType == ConsolidationType.PVOutput)
                {
                    labelPVOutputSystem.Visibility = System.Windows.Visibility.Visible;
                    comboBoxPVOutputSystem.Visibility = System.Windows.Visibility.Visible;
                    rowPVOutput.Height = GridLength.Auto;
                }
                else
                {
                    labelPVOutputSystem.Visibility = System.Windows.Visibility.Hidden;
                    comboBoxPVOutputSystem.Visibility = System.Windows.Visibility.Hidden;
                    rowPVOutput.Height = new GridLength(0.0);
                }
                labelDeviceAddress.Visibility = System.Windows.Visibility.Visible;
                textBoxDeviceAddress.Visibility = System.Windows.Visibility.Visible;
                labelSerialNo.Visibility = System.Windows.Visibility.Collapsed;
                textBoxSerialNo.Visibility = System.Windows.Visibility.Collapsed;
                checkBoxHistoryAdjust.Visibility = System.Windows.Visibility.Collapsed;
                rowAddressSerialNo.Height = new GridLength(0.0);
                rowDeviceAdvanced_0.Height = new GridLength(0.0);
                rowDeviceAdvanced_1.Height = new GridLength(0.0);
                rowDeviceAdvanced_2.Height = new GridLength(0.0);
            }
            else
            {
                gridConsolidatedFrom.Visibility = System.Windows.Visibility.Collapsed;
                gridConsolidationType.Visibility = System.Windows.Visibility.Collapsed;
                rowPVOutput.Height = new GridLength(0.0);
                
                labelPVOutputSystem.Visibility = System.Windows.Visibility.Hidden;
                comboBoxPVOutputSystem.Visibility = System.Windows.Visibility.Collapsed;

                labelSerialNo.Visibility = System.Windows.Visibility.Visible;
                textBoxSerialNo.Visibility = System.Windows.Visibility.Visible;

                rowDeviceAdvanced_0.Height = GridLength.Auto;

                //if ((PVSettings.DeviceManagementSettings.DeviceListItem)comboBoxDeviceType.SelectedItem != null)
                if (DeviceManagerDeviceSettings.DeviceSettings.DeviceTypeName == "SMA_SunnyExplorer")
                {
                    labelDeviceAddress.Visibility = System.Windows.Visibility.Hidden;
                    textBoxDeviceAddress.Visibility = System.Windows.Visibility.Hidden;
                    rowAddressSerialNo.Height = GridLength.Auto;
                    checkBoxHistoryAdjust.Visibility = System.Windows.Visibility.Collapsed;
                    rowDeviceAdvanced_1.Height = new GridLength(0.0);
                    rowDeviceAdvanced_2.Height = new GridLength(0.0);
                }
                else
                {
                    labelDeviceAddress.Visibility = System.Windows.Visibility.Visible;
                    textBoxDeviceAddress.Visibility = System.Windows.Visibility.Visible;
                    rowAddressSerialNo.Height = GridLength.Auto;
                    checkBoxHistoryAdjust.Visibility = System.Windows.Visibility.Visible;
                    rowDeviceAdvanced_1.Height = GridLength.Auto;
                    rowDeviceAdvanced_2.Height = GridLength.Auto;

                    labelDeviceQueryInterval.Visibility =
                        ((DeviceManagerSettings)gridDeviceManagers.DataContext).ManagerType == DeviceManagerType.CC128 ? Visibility.Hidden : Visibility.Visible;
                    comboBoxDeviceQueryInterval.Visibility =
                        ((DeviceManagerSettings)gridDeviceManagers.DataContext).ManagerType == DeviceManagerType.CC128 ? Visibility.Hidden : Visibility.Visible;
                }
            }
        }

        private void dataGridDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectDevice();
        }

        private void dataGridDeviceList_GotFocus(object sender, RoutedEventArgs e)
        {
            SelectDevice();
        }

        private void comboBoxProtocol_SelectionChanged()
        {
            try
            {
                ProtocolSettings p = ApplicationSettings.DeviceManagementSettings.GetProtocol(((ProtocolSettings)comboBoxProtocol.SelectedItem).Name);
                gridSerialBasic.Visibility = p.UsesSerialPort ? Visibility.Visible : Visibility.Collapsed;
                gridSerialDetail.Visibility = gridSerialBasic.Visibility;
                gridListenerDevice.Visibility = p.Type == ProtocolSettings.ProtocolType.Listener ? Visibility.Visible : Visibility.Collapsed;
                gridDeviceManagerTimings.Visibility = gridListenerDevice.Visibility;
                gridExecutablePath.Visibility = p.Type == ProtocolSettings.ProtocolType.Executable ? Visibility.Visible : Visibility.Collapsed;

                if (((ProtocolSettings)comboBoxProtocol.SelectedItem).Name == "Owl Database")
                {
                    checkBoxOwlReload.Visibility = System.Windows.Visibility.Visible;
                    datePickerOwlReload.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    checkBoxOwlReload.Visibility = System.Windows.Visibility.Collapsed;
                    datePickerOwlReload.Visibility = System.Windows.Visibility.Collapsed;
                }

                if (((ProtocolSettings)comboBoxProtocol.SelectedItem).Name == "CC128")
                {
                    columnDeviceAdvancedTiming_Qry.Width = new GridLength(0.0);
                }
                else
                {
                    columnDeviceAdvancedTiming_Qry.Width = GridLength.Auto;
                }

                AdjustAfterProtocolChange();
                
                if (p.Type == ProtocolSettings.ProtocolType.Listener)
                {
                    comboBoxListenerDevice.IsEnabled = true;
                    comboBoxMessageInterval.IsEnabled = true;
                    comboBoxDBInterval.IsEnabled = true;
                }
                else
                {
                    comboBoxListenerDevice.IsEnabled = false;
                    comboBoxMessageInterval.IsEnabled = false;
                    comboBoxDBInterval.IsEnabled = false;
                }
            }
            catch
            {
            }
        }

        private void comboBoxProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            comboBoxProtocol_SelectionChanged();
        }

        private void AdjustAfterProtocolChange()
        {
            if (gridDeviceManagers.DataContext != null)
            {
                comboBoxDeviceType.ItemsSource = ((DeviceManagerSettings)gridDeviceManagers.DataContext).DeviceListItems;
                //comboBoxListenerDevice.ItemsSource = ((DeviceManagerSettings)gridDeviceManagers.DataContext).DeviceListItems;
                if (((DeviceManagerSettings)gridDeviceManagers.DataContext).ManagerType == DeviceManagerType.SMA_SunnyExplorer)
                {
                    gridSunnyExplorerAdvanced.Visibility = Visibility.Visible;
                    labelDeviceDB.Visibility = Visibility.Collapsed;
                    textBoxDeviceDB.Visibility = Visibility.Collapsed;
                    butBrowseOwlDb.Visibility = Visibility.Collapsed;
                }
                else
                {
                    gridSunnyExplorerAdvanced.Visibility = Visibility.Collapsed;
                    labelDeviceDB.Visibility = Visibility.Collapsed;
                    textBoxDeviceDB.Visibility = Visibility.Collapsed;
                    butBrowseOwlDb.Visibility = Visibility.Collapsed;
                }
                gridHistoryHours.Visibility =
                        ((DeviceManagerSettings)gridDeviceManagers.DataContext).ManagerType == DeviceManagerType.CC128 ? Visibility.Visible : Visibility.Collapsed;
                gridHistoryDays.Visibility =
                        ((DeviceManagerSettings)gridDeviceManagers.DataContext).ManagerType == DeviceManagerType.SMA_SunnyExplorer ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void comboBoxProtocol_LostFocus(object sender, RoutedEventArgs e)
        {
            AdjustAfterProtocolChange();
        }

        private void expanderDeviceAdvanced_Expanded(object sender, RoutedEventArgs e)
        {
            expanderDeviceMgrAdvanced.IsExpanded = false;
            expanderConsolidatedFrom.IsExpanded = false;
            expanderConsolidatesTo.IsExpanded = false;
            expanderDeviceEvents.IsExpanded = false;
        }

        private void expanderDeviceMgrAdvanced_Expanded(object sender, RoutedEventArgs e)
        {
            expanderDeviceAdvanced.IsExpanded = false;
            expanderConsolidatedFrom.IsExpanded = false;
            expanderConsolidatesTo.IsExpanded = false;
            expanderDeviceEvents.IsExpanded = false;
        }

        private void buttonAddConsolidateTo_Click(object sender, RoutedEventArgs e)
        {
            DeviceManagerDeviceSettings device = ((DeviceManagerDeviceSettings)gridDevice.DataContext);
            if (device == null)
                return;
            device.AddDevice(ConsolidateDeviceSettings.OperationType.Add);
        }

        private void buttonDeleteConsolidateTo_Click(object sender, RoutedEventArgs e)
        {
            DeviceManagerDeviceSettings device = ((DeviceManagerDeviceSettings)gridDevice.DataContext);
            if (device == null)
                return;
            device.DeleteDevice((ConsolidateDeviceSettings)datGridConsolidateToDevices.SelectedItem);
            buttonDeleteConsolidateTo.IsEnabled &= device.ConsolidateToDevices.Count > 0;
        }

        private void buttonAddDeviceEvent_Click(object sender, RoutedEventArgs e)
        {
            DeviceManagerDeviceSettings device = ((DeviceManagerDeviceSettings)gridDevice.DataContext);
            if (device == null)
                return;
            device.AddEvent();
            buttonDeleteDeviceEvent.IsEnabled = device.DeviceEvents.Count > 0;
        }

        private void buttonDeleteDeviceEvent_Click(object sender, RoutedEventArgs e)
        {
            DeviceManagerDeviceSettings device = ((DeviceManagerDeviceSettings)gridDevice.DataContext);
            if (device == null)
                return;
            device.DeleteEvent((DeviceEventSettings)dataGridDeviceEvents.SelectedItem);
            buttonDeleteDeviceEvent.IsEnabled = device.DeviceEvents.Count > 0;
        }

        private void datGridConsolidateToDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConsolidateDeviceSettings consol = (ConsolidateDeviceSettings)datGridConsolidateToDevices.SelectedItem;
            buttonDeleteConsolidateTo.IsEnabled = consol != null;
        }

        private void comboBoxDeviceType_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            DeviceManagerDeviceSettings curDev = (DeviceManagerDeviceSettings)dataGridDeviceList.SelectedItem;
            ApplicationSettings.RefreshAllDevices();
            dataGridDeviceList.SelectedItem = curDev;
        }

        private void comboBoxConsolidationType_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            SetDeviceVisibility();
        }

        private void expanderConsolidatedFrom_Expanded(object sender, RoutedEventArgs e)
        {
            expanderDeviceMgrAdvanced.IsExpanded = false;
            expanderDeviceAdvanced.IsExpanded = false;
            expanderConsolidatesTo.IsExpanded = false;
            expanderDeviceEvents.IsExpanded = false;
        }

        private void expanderConsolidatesTo_Expanded(object sender, RoutedEventArgs e)
        {
            expanderDeviceMgrAdvanced.IsExpanded = false;
            expanderDeviceAdvanced.IsExpanded = false;
            expanderConsolidatedFrom.IsExpanded = false;
            expanderDeviceEvents.IsExpanded = false;
        }

        private void expanderDeviceEvents_Expanded(object sender, RoutedEventArgs e)
        {
            expanderDeviceMgrAdvanced.IsExpanded = false;
            expanderDeviceAdvanced.IsExpanded = false;
            expanderConsolidatedFrom.IsExpanded = false;
            expanderConsolidatesTo.IsExpanded = false;
            checkBoxAutoEvents_Auto.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void expanderDeviceEvents_Collapsed(object sender, RoutedEventArgs e)
        {
            checkBoxAutoEvents_Auto.Visibility = System.Windows.Visibility.Visible;
        }

        private void dataGridDeviceEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeviceEventSettings de = (DeviceEventSettings)dataGridDeviceEvents.SelectedItem;
            buttonDeleteDeviceEvent.IsEnabled = de != null && de.Device.ManualEvents;
        }

        private void checkBoxHaveSubscription_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            if (PVSettings.HaveSubscription)
                SetLiveDaysComboBox(90);
            else               
                SetLiveDaysComboBox(14);            
        }

        private void SetLiveDaysComboBox(int days)
        {
            int? val = PVSettings.LiveDaysInternal;
            comboBoxLiveDays.Items.Clear();
            comboBoxLiveDays.Items.Add("");
            for (int i = 1; i <= days; i++)
            {
                comboBoxLiveDays.Items.Add(i);
            }
            if (val > days)
                PVSettings.LiveDaysInternal = days;
            else
                PVSettings.LiveDaysInternal = val;
            
        }

    }

    public class NullableValueConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;            
            if (string.IsNullOrEmpty(value.ToString()))
                return null;
            return value;
        }

        #endregion
    }

}
