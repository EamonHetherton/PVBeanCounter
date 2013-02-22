using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;

namespace CGS
{
    /// <summary>
    /// Interaction logic for TimeControl.xaml
    /// </summary>
    public partial class TimeControl : UserControl
    {
        const string amText = "am";
        const string pmText = "pm";

        static SolidColorBrush brBlue = new SolidColorBrush(Colors.LightBlue);
        static SolidColorBrush brWhite = new SolidColorBrush(Colors.White);

        DateTime _lastKeyDown;

        public TimeControl()
        {
            InitializeComponent();

            _lastKeyDown = DateTime.Now;
        }

        public TimeSpan? Value
        {
            get { return (TimeSpan?)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(TimeSpan?), typeof(TimeControl),
        new PropertyMetadata(null, new PropertyChangedCallback(OnValueChanged)));

        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeControl control = obj as TimeControl;

            TimeSpan? newTime = ((TimeSpan?)e.NewValue);

            if (newTime.HasValue)
            {
                int timehours = newTime.Value.Hours;
                int hours = timehours % 12;
                hours = (hours > 0) ? hours : 12;

                control._hours = newTime.Value.Hours;
                control.Hours = hours;
                control.Minutes = ((TimeSpan?)e.NewValue).Value.Minutes;
                control.DayHalf = ((timehours - 12) >= 0) ? pmText : amText;
            }
            else
            {
                control._hours = null;
                control.Hours = null;
                control.Minutes = null;
                control.DayHalf = "";
            }
        }

        private int? _hours;  // time hours value (0 = midnight)
        public int? Hours     // display hours (12 = both midnight and midday
        {
            get { return (int?)GetValue(HoursProperty); }
            set { SetValue(HoursProperty, value); }
        }
        public static readonly DependencyProperty HoursProperty =
        DependencyProperty.Register("Hours", typeof(int?), typeof(TimeControl),
        new UIPropertyMetadata(null));

        public int? Minutes
        {
            get { return (int?)GetValue(MinutesProperty); }
            set { SetValue(MinutesProperty, value); }
        }
        public static readonly DependencyProperty MinutesProperty =
        DependencyProperty.Register("Minutes", typeof(int?), typeof(TimeControl),
        new UIPropertyMetadata(null));


        public string DayHalf
        {
            get { return (string)GetValue(DayHalfProperty); }
            set { SetValue(DayHalfProperty, value); }
        }
        public static readonly DependencyProperty DayHalfProperty =
        DependencyProperty.Register("DayHalf", typeof(string), typeof(TimeControl),
        new UIPropertyMetadata(" "));

        private void Down(object sender, KeyEventArgs args)
        {
            bool updateValue = false;

            if (args.Key == Key.Up || args.Key == Key.Down)
            {
                switch (((Grid)sender).Name)
                {
                    case "min":
                        if (!Hours.HasValue)
                        {
                            _hours = 0;
                            Minutes = 0;
                            break;
                        }
                        if (args.Key == Key.Up)
                            if (this.Minutes >= 59)
                            {
                                this.Minutes = 0;
                                goto case "hour";
                            }
                            else
                            {
                                this.Minutes++;
                            }
                        if (args.Key == Key.Down)
                            if (this.Minutes <= 0)
                            {
                                this.Minutes = 59;
                                goto case "hour";
                            }
                            else
                            {
                                this.Minutes--;
                            }
                        break;

                    case "hour":
                        if (!_hours.HasValue)
                        {
                            _hours = 0;
                            Minutes = 0;
                            break;
                        }
                        if (args.Key == Key.Up)
                            this._hours = (_hours >= 23) ? 0 : _hours + 1;
                        if (args.Key == Key.Down)
                            this._hours = (_hours <= 0) ? 23 : _hours - 1;
                        break;

                    case "half":
                        
                        if (!Hours.HasValue)
                        {
                            _hours = 0;
                            Minutes = 0;
                            break;
                        }
                                              
                        int tempHours = _hours.Value + 12;
                        if (tempHours >= 24)
                            _hours = tempHours - 24;
                        else
                            _hours = tempHours;

                        // this.DayHalf = (_hours.Value >= 12) ? pmText : amText;

                        break;
                }

                updateValue = true;

                args.Handled = true;
            }
            else if ((args.Key >= Key.D0 && args.Key <= Key.D9) || (args.Key >= Key.NumPad0 && args.Key <= Key.NumPad9))
            {
                int keyValue = (int)args.Key;
                int number = 0;

                number = keyValue - ((args.Key >= Key.D0 && args.Key <= Key.D9) ?
                                        (int)Key.D0 :
                                        (int)Key.NumPad0
                                    );

                bool attemptAdd = (DateTime.Now - _lastKeyDown).TotalSeconds < 1.5;

                if (!Hours.HasValue)
                {
                    _hours = 0;
                    Minutes = 0;
                    this.DayHalf = amText;
                }
                
                switch (((Grid)sender).Name)
                {
                    case "min":
                        if (attemptAdd)
                        {
                            number += this.Minutes.Value * 10;

                            if (number < 0 || number >= 60)
                            {
                                number -= this.Minutes.Value * 10;
                            }
                        }

                        this.Minutes = number;
                        break;

                    case "hour":
                        if (attemptAdd)
                        {
                            number += this.Hours.Value * 10;

                            if (number < 0 || number >= 13)
                            {
                                number -= this.Hours.Value * 10;
                            }
                        }

                        number = (number == 12) ? 0 : number;
                        number += (this.DayHalf == amText) ? 0 : 12;

                        _hours = number;
                        break;

                    default:
                        break;
                }
             
                updateValue = true;

                args.Handled = true;
            }
            else if (args.Key == Key.A || args.Key == Key.P)
            {
                if (((Grid)sender).Name == "half")
                {
                    if (!Hours.HasValue)
                    {
                        _hours = 0;
                        Minutes = 0;
                    }

                    if (args.Key == Key.A)
                    {
                        if (_hours >= 12)
                            _hours -= 12;
                    }
                    else if (_hours < 12)
                        _hours += 12;

                    // this.DayHalf = (_hours.Value < 12) ? amText : pmText;                   
                }
                updateValue = true;
            }
            else if ((args.Key == Key.Delete || args.Key ==Key.Back))
            {
                _hours = null;
                Minutes = null;

                updateValue = true;
            }

            if (updateValue)
            {
                if (Minutes.HasValue)
                    this.Value = new TimeSpan(_hours.Value, this.Minutes.Value, 0);
                else
                    this.Value = null;
            }

            _lastKeyDown = DateTime.Now;
        }

        private void Grid_GotFocus(object sender, RoutedEventArgs e)
        {
            var grd = sender as Grid;

            grd.Background = brBlue;
        }

        private void Grid_LostFocus(object sender, RoutedEventArgs e)
        {
            var grd = sender as Grid;

            grd.Background = brWhite;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var grd = sender as Grid;

            grd.Focus();
        }
    }


    public class MinuteSecondToStringConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value != null)
            {
                if (value is int)
                {
                    return ((int)value).ToString("00");
                }
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value != null)
            {
                if (value is string)
                {
                    int number;
                    if (int.TryParse(value as string, out number))
                    {
                        return number;
                    }
                }
            }

            return value;
        }

        #endregion
    }
}
