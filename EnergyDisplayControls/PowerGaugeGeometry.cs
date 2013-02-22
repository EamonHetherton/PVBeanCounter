using System;
using System.Collections.Generic;
using System.Linq;
//using System.ComponentModel;
//using System.Globalization;
//using System.Text;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Media.Effects;
//using System.Windows.Media.Animation;
//using System.Windows.Navigation;
//using System.Windows.Shapes;

namespace EnergyDisplayControls
{
    public struct StandardGaugeConfig
    {
        public Double MaxValue;
        public int MajorMarks_FullScale;
        public int MinorMarks_FullScale;
        public int MajorMarks_HalfScale;
        public int MinorMarks_HalfScale;

        public StandardGaugeConfig(Double maxValue, int majorMarksFullScale, int minorMarksFullScale, int majorMarksHalfSCale, int minorMarksHalfScale)
        {
            MaxValue = maxValue;
            MajorMarks_FullScale = majorMarksFullScale;
            MinorMarks_FullScale = minorMarksFullScale;
            MajorMarks_HalfScale = majorMarksHalfSCale;
            MinorMarks_HalfScale = minorMarksHalfScale;
        }
    }

    public class PowerGaugeGeometry
    {
        public static readonly StandardGaugeConfig[] StandardSizes;

        static PowerGaugeGeometry()
        {
            StandardSizes = new StandardGaugeConfig[] 
            { 
                new StandardGaugeConfig(1000.0, 4, 9, 4, 4 ), 
                new StandardGaugeConfig(1500.0, 3, 9, 3, 4 ),
		        new StandardGaugeConfig(2000.0, 4, 9, 4, 4 ),
		        new StandardGaugeConfig(2500.0, 5, 4, 5, 3 ),
                new StandardGaugeConfig(3000.0, 6, 4, 3, 4 ),
                new StandardGaugeConfig(3500.0, 7, 4, 7, 1 ),
                new StandardGaugeConfig(4000.0, 8, 4, 4, 4 ),
                new StandardGaugeConfig(5000.0, 5, 4, 5, 3 ),
		        new StandardGaugeConfig(6000.0, 6, 4, 3, 4 ),
		        new StandardGaugeConfig(7000.0, 7, 4, 7, 1 ),
		        new StandardGaugeConfig(8000.0, 8, 4, 4, 4 ),
		        new StandardGaugeConfig(9000.0, 9, 4, 5, 2 ),
		        new StandardGaugeConfig(10000.0, 10, 3, 5, 3 ),
		        new StandardGaugeConfig(12000.0, 6, 4, 4, 4 ),
		        new StandardGaugeConfig(14000.0, 7, 4, 7, 1 ),
		        new StandardGaugeConfig(16000.0, 8, 4, 4, 4 ),
		        new StandardGaugeConfig(18000.0, 9, 4, 6, 3 ),
		        new StandardGaugeConfig(20000.0, 10, 3, 5, 3 ),
		        new StandardGaugeConfig(25000.0, 5, 4, 5, 3 ),
		        new StandardGaugeConfig(30000.0, 6, 4, 3, 4 ),
		        new StandardGaugeConfig(35000.0, 5, 4, 5, 3 ),
		        new StandardGaugeConfig(40000.0, 8, 4, 4, 4 ),
		        new StandardGaugeConfig(45000.0, 9, 4, 5, 4 ),
		        new StandardGaugeConfig(50000.0, 10, 3, 5, 3 ),
		        new StandardGaugeConfig(60000.0, 10, 4, 3, 4 ),
		        new StandardGaugeConfig(70000.0, 10, 4, 7, 1 ),
		        new StandardGaugeConfig(80000.0, 10, 4, 4, 4 ),
		        new StandardGaugeConfig(90000.0, 10, 4, 5, 3 ),
		        new StandardGaugeConfig(100000.0, 10, 4, 5, 3 )
            };
        }

        public int Scale1LeftMarks;
        public int Scale1RightMarks;
        public int Scale1MajorMarks;
        public int Scale1LeftMinorMarks;
        public int Scale1RightMinorMarks;
        public Double? Scale1Centre;
        public int Scale1Factor;
        public Double Scale1Max;
        public Double Scale1Min;
        public int Scale1Multiplier { get { return (int)Math.Pow(10.0, Scale1Factor); } }
        public Double LeftMultiple;
        public Double RightMultiple;
        public bool UseStandardSize;

        public String ErrorMessage { get; private set; }
        public String NotifyMessage { get; private set; }

        public bool IsValid { get; private set; }

        public PowerGaugeGeometry(PowerGauge g)
        {
            UseStandardSize = g.UseStandardSize;

            Scale1LeftMarks = g.Scale1LeftMarks;
            Scale1RightMarks = g.Scale1RightMarks;
            Scale1MajorMarks = g.Scale1MajorMarks;
            Scale1RightMinorMarks = g.Scale1RightMinorMarks;
            Scale1LeftMinorMarks = g.Scale1LeftMinorMarks;
            Scale1Centre = g.Scale1Centre;
            Scale1Factor = g.Scale1Factor;
            Scale1Max = g.Scale1Max;
            Scale1Min = g.Scale1Min;

            if (UseStandardSize)
                LoadStandardSize();
            else if (g.geometry != null)
            {
                LeftMultiple = g.geometry.LeftMultiple;
                RightMultiple = g.geometry.RightMultiple;
            }
            else
            {
                LeftMultiple = Scale1Multiplier;
                RightMultiple = LeftMultiple;
            }

            Validate();
        }

        private void LoadStandardSize()
        {
            foreach(StandardGaugeConfig std in StandardSizes)
                if (std.MaxValue >= Scale1Max)
                {
                    if (Scale1Centre.HasValue)
                    {
                        Scale1RightMarks = std.MajorMarks_HalfScale;
                        Scale1RightMinorMarks = std.MinorMarks_HalfScale;
                    }
                    else
                    {
                        Scale1RightMarks = std.MajorMarks_FullScale;
                        Scale1RightMinorMarks = std.MinorMarks_FullScale;
                    }
                    Scale1Max = std.MaxValue;
                    break;
                }

            if (Scale1Centre.HasValue)
            {
                foreach (StandardGaugeConfig std in StandardSizes)
                    if (std.MaxValue >= 0-Scale1Min)
                    {
                        if (Scale1Centre.HasValue)
                        {
                            Scale1LeftMarks = std.MajorMarks_HalfScale;
                            Scale1LeftMinorMarks = std.MinorMarks_HalfScale;
                        }
                        Scale1Min = 0-std.MaxValue;
                        break;
                    }

                LeftMultiple = (Scale1Centre.Value - Scale1Min) / Scale1LeftMarks;
                RightMultiple = (Scale1Max - Scale1Centre.Value) / Scale1RightMarks;
            }
            else
            {
                RightMultiple = (Scale1Max - Scale1Min) / Scale1RightMarks;
                LeftMultiple = RightMultiple;
            }
        }

        private static bool IsMultiple(Double value, Double multiplier)
        {
            int range = (int) (value / multiplier);
            return ((range * multiplier) == value) ;
        }

        private static bool IsMultiple(int value, int multiplier)
        {
            int range = (int)(value / multiplier);
            return ((range * multiplier) == value);
        }

        public bool Validate()
        {
            ErrorMessage = "";
            NotifyMessage = "";

            int scale1Multiplier = Scale1Multiplier;

            if (Scale1Centre.HasValue)
            {
                if (Scale1LeftMarks < 1) ErrorMessage += "Scale1LeftMarks too small; ";
                if (Scale1RightMarks < 1) ErrorMessage += "Scale1RightMarks too small; ";
            }
            else
            {
                Scale1LeftMarks = 0;
                if (!UseStandardSize)
                    Scale1RightMarks = Scale1MajorMarks - 1;
                if (Scale1MajorMarks < 2) ErrorMessage += "Scale1MajorMarks too small; ";
            }

            if (Scale1Min >= Scale1Max) ErrorMessage += "Scale1Min too high; ";

            /*
            if (!IsMultiple( Scale1Min, scale1Multiplier))
                ErrorMessage += "Scale1Min not a valid factor multiple; ";
            if (!IsMultiple(Scale1Max, scale1Multiplier))
                ErrorMessage += "Scale1Max not a valid factor multiple; ";
            */
            if (Scale1Centre.HasValue)
            {
                if (!IsMultiple(Scale1Centre.Value, scale1Multiplier))
                    ErrorMessage += "Scale1Centre not a valid factor multiple; ";
                if (Scale1Min >= Scale1Centre || Scale1Max <= Scale1Centre)
                    ErrorMessage += "Scale1Centre out of bounds; ";
            }

            IsValid = ErrorMessage == "";
            if (!IsValid)
                return IsValid;

            if (Scale1Centre.HasValue)
            {
                Double gap = (Scale1Centre.Value - Scale1Min);
                int range = (int)(gap / (Scale1LeftMarks * scale1Multiplier));

                /*
                if (range * Scale1LeftMarks * scale1Multiplier != gap)
                {
                    if (IsMultiple(gap, LeftMultiple))
                        Scale1LeftMarks = (int)(gap / LeftMultiple);
                    else
                        Scale1LeftMarks = (int)(gap / scale1Multiplier);
                    NotifyMessage += "Scale1LeftMarks invalid - corrected; ";
                }

                gap = Scale1Max - Scale1Centre.Value;
                range = (int)(gap / (Scale1RightMarks * scale1Multiplier));

                if (range * Scale1RightMarks * scale1Multiplier != gap)
                {
                    if ((gap / RightMultiple) * RightMultiple == gap)
                        Scale1RightMarks = (int)(gap / RightMultiple);
                    else
                        Scale1RightMarks = (int)(gap / scale1Multiplier);
                    NotifyMessage += "Scale1RightMarks invalid - corrected; ";
                }
                */
                Scale1MajorMarks = Scale1LeftMarks + Scale1RightMarks + 1;
            }
            else
            {
                Double gap = Scale1Max - Scale1Min;
                int range = (int)(gap / scale1Multiplier);
                int maxMarks = range + 1;
                /*
                if (Scale1MajorMarks > maxMarks || Scale1MajorMarks < 2)
                {
                    NotifyMessage += "Scale1MajorMarks out of bounds - corrected; ";
                    Scale1MajorMarks = maxMarks;
                }
                else if ((Scale1MajorMarks - 1) * (range / (Scale1MajorMarks - 1)) != range)
                {
                    if ((gap / RightMultiple) * RightMultiple == gap)
                        Scale1MajorMarks = (int)((gap / RightMultiple) + 1.0);
                    else
                        Scale1MajorMarks = maxMarks;
                    NotifyMessage += "Scale1MajorMarks / Min Max misaligned - corrected; ";
                }
                */
                Scale1LeftMarks = 0;

                if (UseStandardSize)
                    Scale1MajorMarks = Scale1RightMarks + 1;
                else
                    Scale1RightMarks = Scale1MajorMarks - 1;
            }

            IsValid = ErrorMessage == "";

            if (IsValid)
                if (Scale1Centre.HasValue)
                {
                    LeftMultiple = (Scale1Centre.Value - Scale1Min) / Scale1LeftMarks;
                    RightMultiple = (Scale1Max - Scale1Centre.Value) / Scale1RightMarks;
                }
                else
                {
                    RightMultiple = (Scale1Max - Scale1Min) / Scale1RightMarks;
                    LeftMultiple = RightMultiple;
                }

            return IsValid;
        }
    }

}
