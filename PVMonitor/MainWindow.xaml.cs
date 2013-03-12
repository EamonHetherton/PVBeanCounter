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
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using GenThreadManagement;
using PVSettings;
using MackayFisher.Utilities;
using Subscriber;

public interface IUpdateDials
{
    void UpdateDial(ActiveEvent activeEvent);
}

namespace PVMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IUpdateDials
    {
        Subscriber.MyTransientSubscriber Subscriber;
        GenThreadManager GenThreadManager;
        SystemServices SystemServices;
        ApplicationSettings ApplicationSettings;

        bool GaugeYieldBound = false;
        bool GaugeCCYieldBound = false;
        bool GaugeConsumptionBound = false;
        bool GaugeFeedInBound = false;

        DispatcherTimer visibleDispatcherTimer = null;

        private Brush BrushSelected;
        private Brush BrushAvailable;

        public MainWindow()
        {
            InitializeComponent();
            Subscriber = null;
            BrushSelected = comboBoxLocalMachines.Background;
            BrushAvailable = comboBoxLocalMachines2.Background;

            visibleDispatcherTimer = new DispatcherTimer();
            visibleDispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            visibleDispatcherTimer.Interval = new TimeSpan(0, 0, 5);

            try
            {
                ApplicationSettings = new ApplicationSettings();
                LocateOrCreateDirectory(ApplicationSettings.DefaultDirectory);
                SystemServices = new MackayFisher.Utilities.SystemServices(ApplicationSettings.BuildFileName("PVMonitor.log"));
                ApplicationSettings.SetSystemServices(SystemServices);
                LoadLogSettings();
                
                Subscriber = new Subscriber.MyTransientSubscriber(SystemServices, this);
                expanderSettings.DataContext = Subscriber;
                stackPanelMain.DataContext = Subscriber;
                stackPanelGauges.DataContext = Subscriber;
                //listViewActiveEvents.ItemsSource = Subscriber.ActiveEvents;
                
                GenThreadManager = new GenThreadManager(SystemServices);
                ManageSubscription manageSubscription = new ManageSubscription(GenThreadManager, SystemServices, Subscriber, this);
                int threadId = GenThreadManager.AddThread(manageSubscription);
                GenThreadManager.StartThread(threadId);
                
                Rect bounds = Properties.Settings.Default.WindowPosition;
                this.Top = bounds.Top;
                this.Left = bounds.Left;
            }
            catch (Exception)
            {
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

        public String LocateOrCreateDirectory(String directoryName)
        {
            DirectoryInfo info = new DirectoryInfo(directoryName);

            if (!info.Exists)
            {
                info.Create();

                return "Directory Created: " + directoryName;
            }
            else
                return "Directory Located: " + directoryName;
        }


        private EnergyDisplayControls.PowerGauge GetDial(String description)
        {
            foreach (UIElement child in stackDials.Children)
            {
                EnergyDisplayControls.PowerGauge gauge = child as EnergyDisplayControls.PowerGauge;
                if (gauge != null && gauge.GaugeDescription == description)                
                    return gauge;
            }

            return null;
        }

        private enum GaugeType
        {
            Yield,
            Consumption,
            FeedIn,
            Unknown
        }

        private void BindGauge(EnergyDisplayControls.PowerGauge gauge, ActiveEvent activeEvent, GaugeType type, bool bindDescription)
        {
            gauge.DataContext = Subscriber;
            Binding binding = new Binding();
            binding.Source = activeEvent;
            binding.Path = new PropertyPath("CurrentPower");
            binding.Mode = BindingMode.TwoWay;
            gauge.SetBinding(EnergyDisplayControls.PowerGauge.Scale1ValueProperty, binding);

            binding = new Binding();
            binding.Source = Subscriber;
            if (type == GaugeType.Yield || type == GaugeType.FeedIn)
                binding.Path = new PropertyPath("MaxYield");
            else if (type == GaugeType.Consumption)
                binding.Path = new PropertyPath("MaxConsumption");
            else if (type == GaugeType.Unknown)
                binding.Path = new PropertyPath("MaxYield");
            binding.Mode = BindingMode.TwoWay;
            gauge.SetBinding(EnergyDisplayControls.PowerGauge.Scale1MaxProperty, binding);

            if (type == GaugeType.FeedIn)
            {
                binding = new Binding();
                binding.Source = Subscriber;
                binding.Path = new PropertyPath("MaxConsumptionNegative");                
                binding.Mode = BindingMode.TwoWay;
                gauge.SetBinding(EnergyDisplayControls.PowerGauge.Scale1MinProperty, binding);
            }
            
            if (bindDescription)
            {
                binding = new Binding();
                binding.Source = activeEvent;
                binding.Path = new PropertyPath("ReadingDescription");
                binding.Mode = BindingMode.TwoWay;
                gauge.SetBinding(EnergyDisplayControls.PowerGauge.GaugeDescriptionProperty, binding);
            }
        }

        private void DispatcherUpdateDial(ActiveEvent activeEvent)
        {
            if (activeEvent.Type == "Yield" && (activeEvent.ReadingDescription == "Meter Yield" || activeEvent.ReadingDescription == "CC Yield"))
            {
                if (!GaugeCCYieldBound)
                {
                    BindGauge(gaugeCCYield, activeEvent, GaugeType.Yield, false);
                    GaugeCCYieldBound = true;
                }
                gaugeCCYield.DataContext = activeEvent;
            }
            else if (activeEvent.Type == "Consumption" && activeEvent.ReadingDescription == "Consumption")
            {
                if (!GaugeConsumptionBound)
                {
                    BindGauge(gaugeConsume, activeEvent, GaugeType.Consumption, false);
                    GaugeConsumptionBound = true;
                }
                gaugeConsume.DataContext = activeEvent;
            }
            else if (activeEvent.Type == "Yield" && (activeEvent.ReadingDescription == "Inverter Yield" 
                || activeEvent.ReadingDescription == "Yield" || activeEvent.ReadingDescription == "Inverter_Yield"))
            {
                if (!GaugeYieldBound)
                {
                    BindGauge(gaugeYield, activeEvent, GaugeType.Yield, false);
                    GaugeYieldBound = true;
                }
                gaugeYield.DataContext = activeEvent;
            }
            else if (activeEvent.Type == "FeedIn")
            {
                if (!GaugeFeedInBound)
                {
                    BindGauge(gaugeFeedIn, activeEvent, GaugeType.FeedIn, false);
                    GaugeFeedInBound = true;
                }
                gaugeFeedIn.DataContext = activeEvent;
            }
            else
            {
                EnergyDisplayControls.PowerGauge gauge = GetDial(activeEvent.ReadingDescription);
                if (gauge == null)
                {
                    gauge = new EnergyDisplayControls.PowerGauge();
                    gauge.DialArc = 210.0;
                    gauge.DialWidth = 180.0;
                    gauge.DialRadiusY = 80;
                    gauge.Margin = new Thickness(4.0);
                    gauge.GaugeDescription = activeEvent.ReadingDescription;

                    if (activeEvent.Type == "Yield")
                    {
                        gauge.DialStyle = EnergyDisplayControls.DialVisualStyle.Generation;
                        BindGauge(gauge, activeEvent, GaugeType.Yield, true);
                    }
                    else if (activeEvent.Type == "Consumption")
                    {
                        gauge.DialStyle = EnergyDisplayControls.DialVisualStyle.Consumption;
                        BindGauge(gauge, activeEvent, GaugeType.Consumption, true);
                    }
                    else
                    {
                        gauge.DialStyle = EnergyDisplayControls.DialVisualStyle.Generic;
                        BindGauge(gauge, activeEvent, GaugeType.Unknown, true);
                    }

                    stackDials.Children.Add(gauge);
                }
                gauge.DataContext = activeEvent;
            }          
        }

        public void UpdateDial(ActiveEvent activeEvent)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate() { DispatcherUpdateDial(activeEvent); } );
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            GenThreadManager.StopThreads();
            Subscriber.SetLocalEvent("Disconnecting");
        }

        private void dial_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            visibleDispatcherTimer.Stop();
            button_Exit.Visibility = System.Windows.Visibility.Visible;
            expanderSettings.Visibility = System.Windows.Visibility.Visible;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {     
            visibleDispatcherTimer.Start();
        }

        private void button_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            button_Exit.Visibility = System.Windows.Visibility.Hidden;
            expanderSettings.Visibility = System.Windows.Visibility.Hidden;
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            button_Exit.Visibility = System.Windows.Visibility.Hidden;
            expanderSettings.Visibility = System.Windows.Visibility.Hidden;
            Rect rect = this.RestoreBounds;
            if (rect != Properties.Settings.Default.WindowPosition)
            {
                Properties.Settings.Default.WindowPosition = rect;
                Properties.Settings.Default.Save();
            }
            visibleDispatcherTimer.Stop();
        }

        private void comboBoxLocalMachines_LostFocus(object sender, RoutedEventArgs e)
        {
            //Subscriber.ReconnectPublisher(false);
        }

        private void comboBoxLocalMachines2_LostFocus(object sender, RoutedEventArgs e)
        {
            //Subscriber.ReconnectPublisher(true);
        }

        private void checkBox_ManualCredentials_Checked(object sender, RoutedEventArgs e)
        {
            comboBox_Domain.IsEnabled = true;
            textBox_Username.IsEnabled = true;
            passwordBox.IsEnabled = true;
        }

        private void checkBox_ManualCredentials_Unchecked(object sender, RoutedEventArgs e)
        {
            comboBox_Domain.IsEnabled = false;
            textBox_Username.IsEnabled = false;
            passwordBox.IsEnabled = false;
        }

        private void passwordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Subscriber.Password = passwordBox.Password;
        }

        private void buttonReconnect_Click(object sender, RoutedEventArgs e)
        {
            Subscriber.ReconnectPublisher(false);
            comboBoxLocalMachines.Background = BrushSelected;
            comboBoxLocalMachines2.Background = BrushAvailable;
        }

        private void buttonAlternateReconnect_Click(object sender, RoutedEventArgs e)
        {
            Subscriber.ReconnectPublisher(true);
            comboBoxLocalMachines.Background = BrushAvailable;
            comboBoxLocalMachines2.Background = BrushSelected;
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
            if (string.IsNullOrEmpty(value.ToString()))
                return null;
            return value;
        }
        #endregion
    }

}
