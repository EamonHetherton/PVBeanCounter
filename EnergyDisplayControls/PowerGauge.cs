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
using System.Linq;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EnergyDisplayControls
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:EnergyDisplayControls"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:EnergyDisplayControls;assembly=EnergyDisplayControls"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:CustomControl1/>
    ///
    /// </summary>
    /// 
    public enum DialVisualStyle
    {
        Consumption,
        Generation,
        FeedIn,
        Generic
    };

    public class PowerGauge : Control
    {
        private static Double DialWidthDefault = 300.0;
        private static Double DialBorderWidthDefault = 5.0;
        //private static Brush DialBorderColorDefault = new LinearGradientBrush(Color(
        private static Double DialRadiusYDefault = 100.0;
        private static Double DialMarkerGapBase = 4.0;
        private static Double DialNumeralGapBase = 4.0;
        private static Double DialNumeralFontSizeBase = 20.0;
        private static Double DropShadowAngle = 315.0;
        private static Double DialArcOversize = 15.0;
        private static Double DialRadiusXDefault { get { return DialWidthDefault / 2.0; } }
        private static Double PointerAccelleration = 0.33;
        private static Double PointerDeceleration = PointerAccelleration;
        private static Double FullCircleDuration = 7.0;
        private static Double MinDuration = 1.0;
        private static Double IncrementDelta = 0.25;

        private Style StyleDialConsumption = null;
        private Style StyleDialGeneration = null;
        private Style StyleDialFeedIn = null;
        private Style StyleDialConsumptionReverse = null;
        private Style StyleDialGenerationReverse = null;
        private Style StyleDialFeedInReverse = null;
        private Style StyleDialGeneric = null;

        private Double DialBottomY = 0.0;

        private Double Scale1CurrentValue = 0.0;
        private Double Scale1ChangeValue;
        private Double Scale1ChangeDuration;
        private DateTime Scale1ChangeStart;
        private DateTime Scale1ChangeEnd;
        private DateTime Scale1ChangePrevious;
        private Double Scale1ChangeIncrement;
        private int Scale1ChangeCount;
        private Double AspectFactor;

        private bool GeometryLock = false;

        internal PowerGaugeGeometry geometry;

        private Double DialOversize
        {
            get
            {
                return (- PointOnEllipse(DialRadiusX, DialRadiusY, 360.0 - ((DialArcRender - 180.0) / 2.0)).Y);
            }
        }

        private Double MinAngle { get { return (180.0 - DialArc) / 2.0; } }
        private Double MaxAngle { get { return 180.0 + (DialArc - 180.0) / 2.0; } }

        private PointCollection _Pointer1Points;
        protected bool IsInitialised;

        static PowerGauge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PowerGauge), new FrameworkPropertyMetadata(typeof(PowerGauge)));
        }

        public PowerGauge()
        {
            IsInitialised = false;
            _Pointer1Points = new PointCollection();

            GaugeDescription = "";
            Scale1Units = "";
            DialBorderShadowDepth = 4.0;

            CalcAspectRatio();
            Scale1Min = 0;
            Scale1Max = 6000;
            Scale1MajorMarks = 7;
            Scale1LeftMarks = 3;
            Scale1RightMarks = 3;
            Scale1MinorMarks = 3;
            Scale1LeftMinorMarks = Scale1MinorMarks;
            Scale1Centre = null;
            geometry = new PowerGaugeGeometry(this);
            geometry.Validate();
               
            Pointer1Reposition();
            
            IsInitialised = true;
        }

        private void CalcAspectRatio()
        {
            if (DialRadiusY <= 0.0 || DialRadiusX <= 0.0)
                AspectFactor = 0.0;
            else if (DialRadiusX > DialRadiusY)
                AspectFactor = (DialRadiusX - DialRadiusY) / DialRadiusX;
            else
                AspectFactor = (DialRadiusY - DialRadiusX) / DialRadiusY;
        }

        public bool LoadGeometry(PowerGaugeGeometry geom)
        {
            GaugeMessage = geom.ErrorMessage + geom.NotifyMessage;
            if (!geom.Validate())
                return false;

            geometry = geom;

            GeometryLock = true;
            Scale1LeftMarks = geom.Scale1LeftMarks;
            Scale1RightMarks = geom.Scale1RightMarks;
            Scale1MajorMarks = geom.Scale1MajorMarks;
            Scale1Centre = geom.Scale1Centre;
            Scale1Factor = geom.Scale1Factor;
            Scale1Min = geom.Scale1Min;
            Scale1Max = geom.Scale1Max;
            Scale1RightMinorMarks = geom.Scale1RightMinorMarks;
            if (Scale1Centre.HasValue)
                Scale1LeftMinorMarks = geom.Scale1LeftMinorMarks;
            GeometryLock = false;
            RecalcGeometry();

            return true;
        }

        private void CreatePointer1Points(Double length)
        {
            Double endX = -length;
            _Pointer1Points.Clear();
            _Pointer1Points.Add(new Point(4,3));
            _Pointer1Points.Add(new Point(5.5,2.5));
            _Pointer1Points.Add(new Point(6.5,1.5));
            _Pointer1Points.Add(new Point(7,0));
            _Pointer1Points.Add(new Point(6.5,-1.5));
            _Pointer1Points.Add(new Point(5.5,-2.5));
            _Pointer1Points.Add(new Point(4,-3));
            // Rounded (triangular) tip
            _Pointer1Points.Add(new Point(endX + 3, -1));
            _Pointer1Points.Add(new Point(endX, 0));
            _Pointer1Points.Add(new Point(endX + 3, 1));
            Pointer1Points = _Pointer1Points;
        }

        public static readonly DependencyProperty Pointer1PointsProperty = 
            DependencyProperty.Register("Pointer1Points", typeof(PointCollection), typeof(PowerGauge), null);
        public PointCollection Pointer1Points
        { 
            get 
            {
                return (PointCollection)base.GetValue(Pointer1PointsProperty); 
            }
            set
            {
                base.SetValue(Pointer1PointsProperty, value);
            }
        }


        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

        }

        public static readonly DependencyProperty RotationProperty =
            DependencyProperty.Register("Rotation", typeof(Double), typeof(PowerGauge), new PropertyMetadata(0.0, new PropertyChangedCallback(OnDialGeometryChanged)));
        public Double Rotation
        {
            get
            {
                return (Double)base.GetValue(RotationProperty);
            }
            set
            {
                base.SetValue(RotationProperty, value);
            }
        }

        public static readonly DependencyProperty DialReverseProperty = DependencyProperty.Register("DialReverse", typeof(bool), typeof(PowerGauge), 
            new PropertyMetadata(false, new PropertyChangedCallback(OnDialReverseChanged)));

        public bool DialReverse
        {
            get
            {
                return (bool)base.GetValue(DialReverseProperty);
            }
            set
            {
                base.SetValue(DialReverseProperty, value);
            }
        }

        #region Scale1LeftMarks Property and Events
        public static readonly DependencyProperty Scale1LeftMarksProperty = DependencyProperty.Register("Scale1LeftMarks", typeof(int), typeof(PowerGauge),
            new PropertyMetadata(0, new PropertyChangedCallback(OnScale1LeftMarksChanged)));
        public int Scale1LeftMarks
        {
            get
            {
                return (int)base.GetValue(Scale1LeftMarksProperty);
            }
            set
            {
                base.SetValue(Scale1LeftMarksProperty, value);
            }
        }

        private static void OnScale1LeftMarksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;            
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion

        #region Scale1RightMarks Property and Events
        public static readonly DependencyProperty Scale1RightMarksProperty = DependencyProperty.Register("Scale1RightMarks", typeof(int), typeof(PowerGauge),
            new PropertyMetadata(6, new PropertyChangedCallback(OnScale1RightMarksChanged)));
        public int Scale1RightMarks
        {
            get
            {
                return (int)base.GetValue(Scale1RightMarksProperty);
            }
            set
            {
                base.SetValue(Scale1RightMarksProperty, value);
            }
        }

        private static void OnScale1RightMarksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion

        #region Scale1MajorMarks Property and Events
        public static readonly DependencyProperty Scale1MajorMarksProperty = DependencyProperty.Register("Scale1MajorMarks", typeof(int), typeof(PowerGauge),
            new PropertyMetadata(7, new PropertyChangedCallback(OnScale1MajorMarksChanged)));
        public int Scale1MajorMarks
        {
            get
            {
                return (int)base.GetValue(Scale1MajorMarksProperty);
            }
            set
            {
                base.SetValue(Scale1MajorMarksProperty, value);
            }
        }
        
        private static void OnScale1MajorMarksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion

        #region Scale1MinorMarks Property and Events
        public static readonly DependencyProperty Scale1MinorMarksProperty = DependencyProperty.Register("Scale1MinorMarks", typeof(int), typeof(PowerGauge), 
            new PropertyMetadata(4, new PropertyChangedCallback(OnScale1MinorMarksChanged)));
        public int Scale1MinorMarks
        {
            get
            {
                return (int)base.GetValue(Scale1MinorMarksProperty);
            }
            set
            {
                base.SetValue(Scale1MinorMarksProperty, value);
            }
        }

        private static void OnScale1MinorMarksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            ((PowerGauge)o).Scale1LeftMinorMarks = ((PowerGauge)o).Scale1MinorMarks;
            ((PowerGauge)o).Scale1RightMinorMarks = ((PowerGauge)o).Scale1MinorMarks;
            ((PowerGauge)o).RecalcGeometry();
        }
        #endregion

        #region Scale1RightMinorMarks Property and Events
        public static readonly DependencyProperty Scale1RightMinorMarksProperty = DependencyProperty.Register("Scale1RightMinorMarks", typeof(int), typeof(PowerGauge),
            new PropertyMetadata(4, new PropertyChangedCallback(OnScale1RightMinorMarksChanged)));
        public int Scale1RightMinorMarks
        {
            get
            {
                return (int)base.GetValue(Scale1RightMinorMarksProperty);
            }
            set
            {
                base.SetValue(Scale1RightMinorMarksProperty, value);
            }
        }

        private static void OnScale1RightMinorMarksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (!((PowerGauge)o).Scale1Centre.HasValue)
                ((PowerGauge)o).Scale1MinorMarks = ((PowerGauge)o).Scale1RightMinorMarks;
            ((PowerGauge)o).RecalcGeometry();
        }
        #endregion

        #region Scale1LeftMinorMarks Property and Events
        public static readonly DependencyProperty Scale1LeftMinorMarksProperty = DependencyProperty.Register("Scale1LeftMinorMarks", typeof(int), typeof(PowerGauge),
            new PropertyMetadata(4, new PropertyChangedCallback(OnScale1LeftMinorMarksChanged)));
        public int Scale1LeftMinorMarks
        {
            get
            {
                return (int)base.GetValue(Scale1LeftMinorMarksProperty);
            }
            set
            {
                base.SetValue(Scale1LeftMinorMarksProperty, value);
            }
        }

        private static void OnScale1LeftMinorMarksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (!((PowerGauge)o).Scale1Centre.HasValue)
                ((PowerGauge)o).Scale1MinorMarks = ((PowerGauge)o).Scale1LeftMinorMarks;
            ((PowerGauge)o).RecalcGeometry();
        }
        #endregion

        #region Scale1Centre Property and Events
        public static readonly DependencyProperty Scale1CentreProperty = DependencyProperty.Register("Scale1Centre", typeof(Double?), typeof(PowerGauge),
            new PropertyMetadata(null, new PropertyChangedCallback(OnScale1CentreChanged)));

        public Double? Scale1Centre
        {
            get
            {
                return (Double?)base.GetValue(Scale1CentreProperty);
            }
            set
            {
                base.SetValue(Scale1CentreProperty, value);
            }
        }

        private static void OnScale1CentreChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion

        #region Scale1Factor Property and Events
        public static readonly DependencyProperty Scale1FactorProperty = DependencyProperty.Register("Scale1Factor", typeof(int), typeof(PowerGauge),
            new PropertyMetadata(3, new PropertyChangedCallback(OnScale1FactorChanged)));

        public int Scale1Factor
        {
            get
            {
                return (int)base.GetValue(Scale1FactorProperty);
            }
            set
            {
                base.SetValue(Scale1FactorProperty, value);
            }
        }

        private static void OnScale1FactorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion

        #region Scale1Max Property and Events
        public static readonly DependencyProperty Scale1MaxProperty = DependencyProperty.Register("Scale1Max", typeof(Double), typeof(PowerGauge),
            new PropertyMetadata(10.0, new PropertyChangedCallback(OnScale1MaxChanged)));

        public Double Scale1Max
        {
            get
            {
                return (Double)base.GetValue(Scale1MaxProperty);
            }
            set
            {
                base.SetValue(Scale1MaxProperty, value);
            }
        }

        private static void OnScale1MaxChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion

        #region Scale1Min Property and Events
        public static readonly DependencyProperty Scale1MinProperty = DependencyProperty.Register("Scale1Min", typeof(Double), typeof(PowerGauge),
            new PropertyMetadata(0.0, new PropertyChangedCallback(OnScale1MinChanged)));

        public Double Scale1Min
        {
            get
            {
                return (Double)base.GetValue(Scale1MinProperty);
            }
            set
            {
                base.SetValue(Scale1MinProperty, value);
            }
        }

        private static void OnScale1MinChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion

        #region UseStandardSize Property and Events
        public static readonly DependencyProperty UseStandardSizeProperty = DependencyProperty.Register("UseStandardSize", typeof(bool), typeof(PowerGauge),
            new PropertyMetadata(false, new PropertyChangedCallback(OnUseStandardSizeChanged)));

        public bool UseStandardSize
        {
            get
            {
                return (bool)base.GetValue(UseStandardSizeProperty);
            }
            set
            {
                base.SetValue(UseStandardSizeProperty, value);
            }
        }

        private static void OnUseStandardSizeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PowerGauge g = o as PowerGauge;
            if (!g.GeometryLock)
            {
                PowerGaugeGeometry geom = new PowerGaugeGeometry(g);
                g.LoadGeometry(geom);
            }
        }
        #endregion


        public static readonly DependencyProperty Scale1UnitsProperty = DependencyProperty.Register("Scale1Units", typeof(String), typeof(PowerGauge), null);

        public String Scale1Units
        {
            get
            {
                return (String)base.GetValue(Scale1UnitsProperty);
            }
            set
            {
                base.SetValue(Scale1UnitsProperty, value);
            }
        }

        public static readonly DependencyProperty Scale1ValueProperty = DependencyProperty.Register("Scale1Value", typeof(Double), 
            typeof(PowerGauge), new PropertyMetadata(0.0, new PropertyChangedCallback(OnScale1ValueInvalidated)));

        public Double Scale1Value
        {
            get
            {
                return (Double)base.GetValue(Scale1ValueProperty);
            }
            set
            {
                base.SetValue(Scale1ValueProperty, value);
            }
        }

        public static readonly DependencyProperty Scale1AngleProperty =
            DependencyProperty.Register("Scale1Angle", typeof(Double), typeof(PowerGauge), null);

        public Double Scale1Angle
        {
            get
            {
                return (Double)base.GetValue(Scale1AngleProperty);
            }
            set
            {
                base.SetValue(Scale1AngleProperty, value);
            }
        }

        public static readonly DependencyProperty Scale1CurrentProperty = 
            DependencyProperty.Register("Scale1Current", typeof(bool), typeof(PowerGauge), null);

        public bool Scale1Current
        {
            get
            {
                return (bool)base.GetValue(Scale1CurrentProperty);
            }
            set
            {
                base.SetValue(Scale1CurrentProperty, value);
            }
        }

        public static readonly DependencyProperty GaugeDescriptionProperty =
            DependencyProperty.Register("GaugeDescription", typeof(String), typeof(PowerGauge), 
            new PropertyMetadata("Empty", new PropertyChangedCallback(OnGaugeDescriptionInvalidated)));
        public String GaugeDescription
        {
            get
            {
                return (String)base.GetValue(GaugeDescriptionProperty);
            }
            set
            {
                base.SetValue(GaugeDescriptionProperty, value);
            }
        }

        public static readonly DependencyProperty GaugeMessageProperty =
            DependencyProperty.Register("GaugeMessage", typeof(String), typeof(PowerGauge), null);
        public String GaugeMessage
        {
            get
            {
                return (String)base.GetValue(GaugeMessageProperty);
            }
            set
            {
                base.SetValue(GaugeMessageProperty, value);
            }
        }

        public static readonly RoutedEvent GaugeDescriptionChangedEvent =
            EventManager.RegisterRoutedEvent("GaugeDescriptionChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<String>), typeof(PowerGauge));

        public static readonly RoutedEvent Scale1ValueChangedEvent = 
            EventManager.RegisterRoutedEvent("Scale1ValueChanged", RoutingStrategy.Bubble, 
            typeof(RoutedPropertyChangedEventHandler<Double>), typeof(PowerGauge));

        public static readonly RoutedEvent Scale1AngleChangedEvent =
            EventManager.RegisterRoutedEvent("Scale1AngleChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<Double>), typeof(PowerGauge));

        private struct PointerPosition
        {
            public Double Length;
            public Double Angle;
        }

        // interimValue is provided during frame based animation
        private PointerPosition CalcPointer1Position(Double? interimValue = null)
        {
            Double thisValue = interimValue == null ? Scale1Value : interimValue.Value;
            if (thisValue > Scale1Max) thisValue = Scale1Max;
            else if (thisValue < Scale1Min) thisValue = Scale1Min;

            Double scale1BaseAngle;

            if (Scale1Centre == null)
                scale1BaseAngle = ((thisValue - Scale1Min) / (Scale1Max - Scale1Min)) * DialArc + MinAngle;
            else
            {
                Double centreVal = Scale1Centre.Value;
                Double halfArc = DialArc / 2.0;

                if (thisValue <= centreVal)
                {
                    scale1BaseAngle = ((thisValue - Scale1Min) / (centreVal - Scale1Min)) * halfArc + MinAngle;
                }
                else
                {
                    scale1BaseAngle = ((thisValue - centreVal) / (Scale1Max - centreVal)) * halfArc + MinAngle + halfArc;
                }
            }

            Double renderAngle = DialReverse ? MaxAngle - (scale1BaseAngle - MinAngle) : scale1BaseAngle;
            Point p = PointOnEllipse(DialRadiusX, DialRadiusY, renderAngle);
            Double len = Math.Pow(Math.Pow(p.X, 2.0) + Math.Pow(p.Y, 2.0), 0.5);

            p.X = -p.X;
            p.Y = -p.Y;

            if (len > DialMarkerGapBase)
                len -= DialMarkerGapBase * 2.0;

            Double eAngle = (Math.Atan2(p.Y, p.X) * 180.0 / Math.PI) + 180.0;

            PointerPosition position;
            position.Length = len;
            position.Angle = eAngle;

            return position;
        }

        private void AdjustPointer1(PointerPosition position)
        {
            CreatePointer1Points(position.Length);

            Polygon poly = GetTemplateChild("polygonPointer1") as Polygon;

            if ((poly != null) && (poly.Effect != null)
                && (poly.Effect.GetType() == typeof(DropShadowEffect)))
            {
                DropShadowEffect shadow = new DropShadowEffect();
                shadow.Color = ((DropShadowEffect)poly.Effect).Color;
                shadow.Opacity = ((DropShadowEffect)poly.Effect).Opacity;
                shadow.BlurRadius = ((DropShadowEffect)poly.Effect).BlurRadius;
                shadow.ShadowDepth = ((DropShadowEffect)poly.Effect).ShadowDepth;
                shadow.Direction = position.Angle + DropShadowAngle + Rotation;
                poly.Effect = shadow;
            }            
        }

        private void RenderFrame(object sender, EventArgs e)
        {
            if (Scale1CurrentValue == Scale1Value)
            {
                CompositionTarget.Rendering -= RenderFrame;
                return;
            }

            Double range = Scale1Max - Scale1Min;
            Double valueDelta = Scale1Value - Scale1CurrentValue;
            Double arc = Math.Abs(DialArc * valueDelta / range);
            Double complete = (Scale1ChangeValue - valueDelta) / Scale1ChangeValue;

            DateTime stamp = DateTime.Now;
            if (Scale1ChangeCount > 0)
            {
                TimeSpan last = stamp - Scale1ChangePrevious;
                Double iterations = valueDelta / (valueDelta * Scale1ChangeIncrement / arc);
                TimeSpan duration = TimeSpan.FromSeconds(iterations * last.TotalSeconds);
                DateTime end = stamp + duration;
                if (end > Scale1ChangeEnd || complete < 0.5)
                    Scale1ChangeIncrement += IncrementDelta;
                else if (end < Scale1ChangeEnd && Scale1ChangeIncrement > IncrementDelta)
                    Scale1ChangeIncrement -= IncrementDelta;
            }

            if (arc > Scale1ChangeIncrement)
                Scale1CurrentValue += valueDelta * Scale1ChangeIncrement / arc;
            else
                Scale1CurrentValue = Scale1Value;

            PointerPosition position = CalcPointer1Position(Scale1CurrentValue);
            AdjustPointer1(position);
            Scale1Angle = position.Angle;
            Scale1ChangePrevious = stamp;
            Scale1ChangeCount++;
        }

        private bool UseSimpleAnimation(Double aFrom, Double aTo)
        {
            Double from;
            Double to;
            if (aFrom < aTo)
            {
                from = aFrom;
                to = aTo;
            }
            else
            {
                from = aTo;
                to = aFrom;
            }

            double delta;
            int fromQuadrant = (int)(from / 90.0);
            int toQuadrant = (int)(to / 90.0);

            if (fromQuadrant == toQuadrant) // same quadrant - use difference
                delta = Math.Abs(to - from);
            if (toQuadrant < 3)
            {
                if ((toQuadrant - fromQuadrant) > 1)
                    delta = 90.0; // crossing two quadrants - max is 90
                else
                {
                    // use largest single quad arc
                    delta = to - (90.0 * toQuadrant);
                    Double d = from - (90.0 * fromQuadrant);
                    if (d > delta)
                        delta = d;
                }                
            }
            else if(toQuadrant == 0) // crossing full circle - use largest single quad arc
            {
                delta = (360 - to);
                if (delta < from)
                    delta = from;
            }
            else // crossing two quadrants - max is 90
                delta = 90.0;

            // Aspect factor reduces toward zero as the X and Y radii approach equality
            // Delta increases as the angle affecting pointer length change increases
            // Decision to use simple animation requires a minimal pointer length change
            return (delta * AspectFactor) <= 25.0;  
            // 25 is arbitary - larger values will increase use of smooth animation 
            // with increased chance of visible pointer length changes 
        }

        // oldValue is supplied when animation is required
        public void Pointer1Reposition(Double? oldValue = null)
        {
            if (oldValue == null)
            {
                {
                    // clear old animation - if present
                    Double temp = Scale1Angle;
                    this.BeginAnimation(PowerGauge.Scale1AngleProperty, null);
                    Scale1Angle = temp;
                }

                PointerPosition position = CalcPointer1Position();
                AdjustPointer1(position);
                Scale1Angle = position.Angle;
            }
            else
            {
                PointerPosition position = CalcPointer1Position();
                double delta = Math.Abs(position.Angle - Scale1Angle);

                Scale1ChangeDuration = FullCircleDuration * (delta) / 360.0;

                if (UseSimpleAnimation(Scale1Angle, position.Angle))
                {
                    AdjustPointer1(position);
                    if (delta > 0.0)
                    {
                        DoubleAnimation animate = new DoubleAnimation();
                        animate.To = position.Angle;

                        if (Scale1ChangeDuration < MinDuration) Scale1ChangeDuration = MinDuration;
                        animate.Duration = new Duration(TimeSpan.FromSeconds(Scale1ChangeDuration));
                        animate.AccelerationRatio = PointerAccelleration;
                        animate.DecelerationRatio = PointerDeceleration;
                        this.BeginAnimation(PowerGauge.Scale1AngleProperty, animate);
                    }
                }
                else
                {
                    {
                        // clear old animation - if present
                        Double temp = Scale1Angle;
                        this.BeginAnimation(PowerGauge.Scale1AngleProperty, null);
                        Scale1Angle = temp;
                    }

                    Scale1CurrentValue = oldValue.Value;
                    Scale1ChangeValue = Scale1Value - Scale1CurrentValue;
                    Scale1ChangeStart = DateTime.Now;
                    Scale1ChangeEnd = Scale1ChangeStart.AddSeconds(Scale1ChangeDuration);
                    Scale1ChangePrevious = Scale1ChangeStart;
                    Scale1ChangeIncrement = IncrementDelta;
                    Scale1ChangeCount = 0;
                    CompositionTarget.Rendering += RenderFrame;
                }
            }
        }

        public static readonly DependencyProperty DialWidthProperty = 
            DependencyProperty.Register("DialWidth", typeof(Double), typeof(PowerGauge), 
            new FrameworkPropertyMetadata(DialWidthDefault, new PropertyChangedCallback(OnDialWidthChanged)));
        public Double DialWidth
        {
            get { return (Double)base.GetValue(DialWidthProperty); }

            set
            {
                base.SetValue(DialWidthProperty, value);
            }
        }

        public static readonly DependencyProperty DialBorderWidthProperty =
            DependencyProperty.Register("DialBorderWidth", typeof(Double), typeof(PowerGauge),
            new FrameworkPropertyMetadata(DialBorderWidthDefault, null));
        public Double DialBorderWidth
        {
            get { return (Double)base.GetValue(DialBorderWidthProperty); }

            set
            {
                base.SetValue(DialBorderWidthProperty, value);
            }
        }

        public static readonly DependencyProperty DialBorderColorProperty =
            DependencyProperty.Register("DialBorderColor", typeof(Brush), typeof(PowerGauge),
            null);
        public Brush DialBorderColor
        {
            get { return (Brush)base.GetValue(DialBorderColorProperty); }

            set
            {
                base.SetValue(DialBorderColorProperty, value);
            }
        }

        public static readonly DependencyProperty DialStyleProperty =
                DependencyProperty.Register("DialStyle", typeof(DialVisualStyle), typeof(PowerGauge), 
                new PropertyMetadata(DialVisualStyle.Generation, new PropertyChangedCallback(OnDialStyleChanged)));
        public DialVisualStyle DialStyle
        {
            get { return (DialVisualStyle)base.GetValue(DialStyleProperty); }

            set
            {
                base.SetValue(DialStyleProperty, value);
            }
        }

        private static void OnDialStyleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            ((PowerGauge)o).RecalcGeometry();
        }
        

        public static readonly DependencyProperty DialSizeProperty = DependencyProperty.Register("DialSize", typeof(Size), typeof(PowerGauge), null);
        public Size DialSize
        {
            get
            {
                return (Size)base.GetValue(DialSizeProperty);
            }
            set
            {
                base.SetValue(DialSizeProperty, value);
            }
        }

        public static readonly DependencyProperty DialLowerLeftProperty = DependencyProperty.Register("DialLowerLeft", typeof(Point), typeof(PowerGauge), null);
        public Point DialLowerLeft
        {
            get
            {
                return (Point)base.GetValue(DialLowerLeftProperty);
            }
            set
            {
                base.SetValue(DialLowerLeftProperty, value);
            }
        }

        public static readonly DependencyProperty DialLowerRightProperty = DependencyProperty.Register("DialLowerRight", typeof(Point), typeof(PowerGauge), null);
        public Point DialLowerRight
        {
            get
            {
                return (Point)base.GetValue(DialLowerRightProperty);
            }
            set
            {
                base.SetValue(DialLowerRightProperty, value);
            }
        }

        public static readonly DependencyProperty DialBorderShadowDepthProperty = DependencyProperty.Register("DialBorderShadowDepth", typeof(Double), typeof(PowerGauge), null);
        public Double DialBorderShadowDepth
        {
            get
            {
                return (Double)base.GetValue(DialBorderShadowDepthProperty);
            }
            set
            {
                base.SetValue(DialBorderShadowDepthProperty, value);
            }
        }

        public static readonly DependencyProperty DialBorderSizeProperty = DependencyProperty.Register("DialBorderSize", typeof(Size), typeof(PowerGauge), null);
        public Size DialBorderSize
        {
            get
            {
                return (Size)base.GetValue(DialBorderSizeProperty);
            }
            set
            {
                base.SetValue(DialBorderSizeProperty, value);
            }
        }

        public static readonly DependencyProperty DialBorderLowerLeftProperty = DependencyProperty.Register("DialBorderLowerLeft", typeof(Point), typeof(PowerGauge), null);
        public Point DialBorderLowerLeft
        {
            get
            {
                return (Point)base.GetValue(DialBorderLowerLeftProperty);
            }
            set
            {
                base.SetValue(DialBorderLowerLeftProperty, value);
            }
        }

        public static readonly DependencyProperty DialBorderLowerRightProperty = 
            DependencyProperty.Register("DialBorderLowerRight", typeof(Point), typeof(PowerGauge), null);
        public Point DialBorderLowerRight
        {
            get
            {
                return (Point)base.GetValue(DialBorderLowerRightProperty);
            }
            set
            {
                base.SetValue(DialBorderLowerRightProperty, value);
            }
        }

        private static void OnDialWidthChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (((PowerGauge)o).IsInitialised)
            {
                o.SetValue(DialRadiusXProperty, ((Double)e.NewValue) / 2.0);
                o.SetValue(WidthProperty, ((PowerGauge)o).ActualWidth);
                ((PowerGauge)o).RecalcGeometry();
            }
        }


        public static readonly DependencyProperty DialRadiusXProperty =
            DependencyProperty.Register("DialRadiusX", typeof(Double), typeof(PowerGauge), 
            new FrameworkPropertyMetadata(DialRadiusXDefault, null));
        public Double DialRadiusX
        {
            get { return (Double)base.GetValue(DialRadiusXProperty); }
            
            private set // Only set via DialWidth
            {
                base.SetValue(DialRadiusXProperty, value);
            }           
        }

        public static readonly DependencyProperty DialRadiusYProperty = 
            DependencyProperty.Register("DialRadiusY", typeof(Double), typeof(PowerGauge), 
            new FrameworkPropertyMetadata(DialRadiusYDefault, new PropertyChangedCallback(OnDialGeometryChanged)));
        public Double DialRadiusY
        {
            get { return (Double)base.GetValue(DialRadiusYProperty); }
            set
            {
                base.SetValue(DialRadiusYProperty, value);
            }
        }

        public static readonly DependencyProperty DialTranslateXProperty =
            DependencyProperty.Register("DialTranslateX", typeof(Double), typeof(PowerGauge), null);
        public Double DialTranslateX
        {
            get { return (Double)base.GetValue(DialTranslateXProperty); }

            private set 
            {
                base.SetValue(DialTranslateXProperty, value);
            }
        }

        public static readonly DependencyProperty DialTranslateYProperty =
            DependencyProperty.Register("DialTranslateY", typeof(Double), typeof(PowerGauge), null);
        public Double DialTranslateY
        {
            get { return (Double)base.GetValue(DialTranslateYProperty); }

            private set 
            {
                base.SetValue(DialTranslateYProperty, value);
            }
        }

        private static void OnDialGeometryChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            ((PowerGauge)o).RecalcGeometry();
        }
        
        private static Point PointOnEllipse(Double radiusX, Double radiusY, Double degrees)
        {
            Double rad = ((Double)Math.PI / 180.0) * degrees;           
            Double x = (radiusX * (Double)Math.Cos(rad));
            Double y = (radiusY * (Double)Math.Sin(rad));
            return new Point(x, y);
        }

        private void PositionText(Size sizeLabel, Double radiusXInner, Double radiusYInner, Double dialNumeralGap)
        {
            int i;
            if (GaugeDescription == "Inverter_Yield")
            {
                i = 0;
                i++;
            }
            Style styleDialText = null;
            try
            {
                styleDialText = (Style)this.FindResource("DialText");
            }
            catch (Exception)
            {
            }

            Label labReading = GetTemplateChild("textScale1Value") as Label;
            Label labDesc = GetTemplateChild("textDescription") as Label;
            Label labUnits = GetTemplateChild("textUnits") as Label;

            {
                Size maxSize = new Size(Double.PositiveInfinity, Double.PositiveInfinity);
                if (labReading != null)
                {
                    labReading.Padding = new Thickness(0.0);
                    labReading.Margin = new Thickness(0.0);

                    labReading.Style = styleDialText;
                    labReading.RenderTransform = null;
                    if (DialRadiusX <= 100.0 || DialRadiusY <= 80.0)
                        labReading.FontSize = 18.0;
                    else if (DialRadiusX <= 120.0 || DialRadiusY <= 100.0)
                        labReading.FontSize = 24.0;
                    else if (DialRadiusX <= 150.0 || DialRadiusY <= 120.0)
                        labReading.FontSize = 30.0;
                    else
                        labReading.FontSize = 36.0;

                    labReading.Measure(maxSize);
                }
                if (labDesc != null)
                {
                    labDesc.Padding = new Thickness(0.0);
                    labDesc.Margin = new Thickness(0.0);

                    labDesc.Style = styleDialText;
                    labDesc.RenderTransform = null;
                    if (DialRadiusX <= 100.0 || DialRadiusY <= 80.0)
                        labDesc.FontSize = 10.0;
                    else if (DialRadiusX <= 120.0 || DialRadiusY <= 100.0)
                        labDesc.FontSize = 12.0;
                    else if (DialRadiusX <= 150.0 || DialRadiusY <= 120.0)
                        labDesc.FontSize = 15.0;
                    else
                        labDesc.FontSize = 18.0;
                    labDesc.Measure(maxSize);
                }
                if (labUnits != null)
                {
                    labUnits.Padding = new Thickness(0.0);
                    labUnits.Margin = new Thickness(0.0);

                    labUnits.Style = styleDialText;
                    labUnits.RenderTransform = null;
                    if (DialRadiusX <= 100.0 || DialRadiusY <= 80.0)
                        labUnits.FontSize = 8.0;
                    else if (DialRadiusX <= 120.0 || DialRadiusY <= 100.0)
                        labUnits.FontSize = 10.0;
                    else if (DialRadiusX <= 150.0 || DialRadiusY <= 120.0)
                        labUnits.FontSize = 13.0;
                    else
                        labUnits.FontSize = 16.0;
                    labUnits.Measure(maxSize);
                }
            }

            bool rotateText = (Rotation == 90.0 || Rotation == 270.0);

            if (rotateText)
            {
                if (labReading != null)
                {
                    labReading.Width = labReading.FontSize * 7.0;
                    labReading.Height = labReading.FontSize * 1.5;

                    labReading.SetValue(Canvas.LeftProperty, -labReading.DesiredSize.Height / 2.0 - 2.0);
                    labReading.SetValue(Canvas.TopProperty, -((radiusYInner - (dialNumeralGap + sizeLabel.Height)) - labReading.Width) / 2.0 - 4.0);

                    TransformGroup tfg = new TransformGroup();
                    if (Rotation == 270.0)
                    {
                        RotateTransform rtf2 = new RotateTransform(180.0, labReading.Width / 2.0, labReading.Height / 2.0);
                        tfg.Children.Add(rtf2);
                    }
                    RotateTransform rtf = new RotateTransform(-90.0);
                    tfg.Children.Add(rtf);
                    labReading.RenderTransform = tfg;
                }

                if (labDesc != null)
                {
                    labDesc.Width = labDesc.DesiredSize.Width;
                    labDesc.Height = labDesc.DesiredSize.Height;

                    labDesc.SetValue(Canvas.LeftProperty, 20.0);
                    labDesc.SetValue(Canvas.TopProperty, 0.0);

                    TransformGroup tfg = new TransformGroup();
                    if (Rotation == 270.0)
                    {
                        RotateTransform rtf2 = new RotateTransform(180.0, labDesc.Width / 2.0, labDesc.Height / 2.0);
                        tfg.Children.Add(rtf2);
                    }
                    RotateTransform rtf = new RotateTransform(-90.0);
                    tfg.Children.Add(rtf);
                    labDesc.RenderTransform = tfg;
                }

                if (labUnits != null)
                {
                    labUnits.Width = labUnits.DesiredSize.Width;
                    labUnits.Height = labUnits.DesiredSize.Height;

                    labUnits.RenderTransform = null;
                    labUnits.SetValue(Canvas.LeftProperty, -(radiusXInner - (dialNumeralGap + sizeLabel.Height)));
                    labUnits.SetValue(Canvas.TopProperty, -(labDesc.DesiredSize.Height / 2.0));
                }
            }
            else
            {
                if (labDesc != null)
                {
                    labDesc.Width = labDesc.DesiredSize.Width;
                    labDesc.Height = labDesc.DesiredSize.Height;

                    labDesc.RenderTransform = null;
                    labDesc.SetValue(Canvas.LeftProperty, -labDesc.Width / 2.0);

                    if (DialArc < 210.0)
                        labDesc.SetValue(Canvas.TopProperty, -(radiusYInner / 3.0));
                    else
                        labDesc.SetValue(Canvas.TopProperty, labDesc.Height / 6.0);

                    if (Rotation >= 180.0)
                    {
                        RotateTransform rtf = new RotateTransform(180.0, labDesc.Width / 2.0, labDesc.Height / 2.0);
                        labDesc.RenderTransform = rtf;
                    }
                }

                if (labReading != null)
                {
                    labReading.Width = labReading.FontSize * 7.0;
                    labReading.Height = labReading.FontSize * 1.5;

                    labReading.RenderTransform = null;
                    labReading.SetValue(Canvas.LeftProperty, -labReading.Width / 2.0);

                    if (DialArc < 210.0)
                        labReading.SetValue(Canvas.TopProperty, -(radiusYInner / 1.5));
                    else
                        labReading.SetValue(Canvas.TopProperty, DialBottomY - labReading.Height);

                    if (Rotation >= 180.0)
                    {
                        RotateTransform rtf = new RotateTransform(180.0, labReading.Width / 2.0, labReading.Height / 2.0);
                        labReading.RenderTransform = rtf;
                    }
                }

                if (labUnits != null)
                {
                    labUnits.Width = labUnits.DesiredSize.Width;
                    labUnits.Height = labUnits.DesiredSize.Height;

                    labUnits.RenderTransform = null;
                    labUnits.SetValue(Canvas.LeftProperty, -(radiusXInner - (dialNumeralGap + sizeLabel.Height)));
                    labUnits.SetValue(Canvas.TopProperty, -(labDesc.DesiredSize.Height / 2.0));

                    if (Rotation >= 180.0)
                    {
                        RotateTransform rtf = new RotateTransform(180.0, labUnits.Width / 2.0, labUnits.Height / 2.0);
                        labUnits.RenderTransform = rtf;
                    }
                }
            }
        }

        private void SetDialStyle()
        {
            Path path = GetTemplateChild("pathDial") as Path;
            if (path == null)
                return;

            if (DialReverse)
            {
                if (DialStyle == DialVisualStyle.Consumption)
                {
                    if (StyleDialConsumptionReverse != null)
                        path.Style = StyleDialConsumptionReverse;
                }
                else if (DialStyle == DialVisualStyle.Generation)
                {
                    if (StyleDialGenerationReverse != null)
                        path.Style = StyleDialGenerationReverse;
                }
                else if (DialStyle == DialVisualStyle.FeedIn)
                {
                    if (StyleDialFeedInReverse != null)
                        path.Style = StyleDialFeedInReverse;
                }
            }
            else
            {
                if (DialStyle == DialVisualStyle.Consumption)
                {
                    if (StyleDialConsumption != null)
                        path.Style = StyleDialConsumption;
                }
                else if (DialStyle == DialVisualStyle.Generation)
                {
                    if (StyleDialGeneration != null)
                        path.Style = StyleDialGeneration;
                }
                else if (DialStyle == DialVisualStyle.FeedIn)
                {
                    if (StyleDialFeedIn != null)
                        path.Style = StyleDialFeedIn;
                }
                else if (DialStyle == DialVisualStyle.Generic)
                {
                    if (StyleDialGeneric != null)
                        path.Style = StyleDialGeneric;
                }
            }
        }

        private void Adjust3DGradients()
        {
            Path pathBorder = (Path)GetTemplateChild("pathBorder");
            if (pathBorder != null)
            {
                LinearGradientBrush brush = pathBorder.Fill as LinearGradientBrush;
                if (brush != null)
                {
                    LinearGradientBrush newBrush = brush.Clone();
                    newBrush.Transform = new RotateTransform(-Rotation);
                    pathBorder.Fill = newBrush;
                }
                brush = pathBorder.Stroke as LinearGradientBrush;
                if (brush != null)
                {
                    LinearGradientBrush newBrush = brush.Clone();
                    newBrush.Transform = new RotateTransform(-Rotation);
                    pathBorder.Stroke = newBrush;
                }
                AdjustDropShadow(pathBorder, Rotation);
            }

            Path pathDial = (Path)GetTemplateChild("pathDial");
            if (pathDial != null)
            {
                LinearGradientBrush brush = pathDial.Stroke as LinearGradientBrush;
                if (brush != null)
                {
                    LinearGradientBrush newBrush = brush.Clone();
                    newBrush.Transform = new RotateTransform(-Rotation);
                    pathDial.Stroke = newBrush;
                }
            }
        }

        private void RecalcGeometry()
        {
            SetDialStyle();

            Size dialSize = new Size();
            dialSize.Width = DialRadiusX;
            dialSize.Height = DialRadiusY;
            DialSize = dialSize;
            CalcAspectRatio();

            Point endPoint = PointOnEllipse(DialRadiusX, DialRadiusY, ((DialArcRender - 180.0) / 2.0));
            Point startPoint = endPoint;
            startPoint.X = -startPoint.X;

            DialLowerLeft = startPoint;
            DialLowerRight = endPoint;

            dialSize = new Size();
            dialSize.Width = DialRadiusX + DialBorderWidth;
            dialSize.Height = DialRadiusY + DialBorderWidth;
            DialBorderSize = dialSize;

            Double y = startPoint.Y;
            Double x = Math.Sqrt(Math.Pow(DialRadiusY, 2.0) - Math.Pow(y, 2.0)); // x coordinate on Y coordinate dial circle
            Double y2 = y + DialBorderWidth; // y coordinate on ellipse
            Double x2 = Math.Sqrt(Math.Pow(dialSize.Height, 2.0) - Math.Pow(y2, 2.0)); // x coordinate on Y coordinate border circle
            Double x3 = x2 * dialSize.Width / dialSize.Height; // x coordinate on border ellipse

            DialBottomY = y2;

            Point borderEndPoint = new Point(x3, y2);
            Point borderStartPoint = new Point(-x3, y2);

            DialBorderLowerLeft = borderStartPoint;
            DialBorderLowerRight = borderEndPoint;

            SetValue(HeightProperty, (DialRadiusY + DialOversize + DialBorderWidth * 2.0) + DialBorderShadowDepth * 2.0);
            SetValue(WidthProperty, DialWidth + DialBorderWidth * 2.0 + DialBorderShadowDepth * 2.0);

            SetValue(DialTranslateXProperty, DialRadiusX + DialBorderWidth + DialBorderShadowDepth);
            SetValue(DialTranslateYProperty, DialRadiusY + DialBorderWidth + DialBorderShadowDepth);

            Pointer1Reposition();
            LayoutDialContent();
            Adjust3DGradients();
        }

        private static Double GetWidth(PointCollection points)
        {
            Double left = 0.0;
            Double right = 0.0;
            bool first = true;
            foreach (Point p in points)
            {
                if (first)
                {
                    left = p.X;
                    right = p.X;
                    first = false;
                }
                else if (p.X < left)
                        left = p.X;
                    else if (p.X > right)
                        right = p.X;    
            }
            return right - left;
        }

        private void AdjustDropShadow(Shape shape, Double rotation)
        {
            if ((shape.Effect != null) 
                && (shape.Effect.GetType() == typeof(DropShadowEffect)))
            {
                DropShadowEffect shadow = new DropShadowEffect();
                shadow.Color = ((DropShadowEffect)shape.Effect).Color;
                shadow.Opacity = ((DropShadowEffect)shape.Effect).Opacity;
                shadow.BlurRadius = ((DropShadowEffect)shape.Effect).BlurRadius;
                shadow.ShadowDepth = ((DropShadowEffect)shape.Effect).ShadowDepth;
                shadow.Direction = DropShadowAngle + rotation;
                shape.Effect = shadow;
            }
        }

        private struct DialMarkContext
        {
            public Canvas canvas;
            public bool isMajor;
            public Double angle;
            public Double labelValue;
            public Double dialNumeralFontSize;
            public Style styleLarge;
            public Style styleSmall;
            public Style styleDialNumeral;
            public Double radiusXInner;
            public Double radiusYInner;
            public Double dialMarkerScale;
            public Double dialNumeralGap;
            public Double gaugeWidth;
        }

        private void LayoutMark(ref Size sizeLabel, DialMarkContext context)
        {
            Shape shape;
            Label label = null;
            Style style = context.isMajor ? context.styleLarge : context.styleSmall;
            bool isPoly = true;

            if (style.TargetType == typeof(Polygon))
                shape = new Polygon();
            else
            {
                shape = new Ellipse();
                isPoly = false;
            }

            shape.Style = style;
            context.canvas.Children.Add(shape);

            if (context.isMajor)
            {               
                
                label = new Label();
                label.Style = context.styleDialNumeral;
                label.Content = context.labelValue.ToString();
                
                label.Background = new SolidColorBrush(); // transparent
                label.BorderThickness = new Thickness(0.0);
                label.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
                label.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;

                label.FontSize = context.dialNumeralFontSize;
            }

            Point outerPoint = PointOnEllipse(DialRadiusX, DialRadiusY, context.angle);
            Point innerPoint = PointOnEllipse(context.radiusXInner, context.radiusYInner, context.angle);

            Double eAngle = Math.Atan2(innerPoint.Y, innerPoint.X) * 180.0 / Math.PI;
            Double outerLen = Math.Pow(Math.Pow(outerPoint.X, 2.0) + Math.Pow(outerPoint.Y, 2.0), 0.5);
            Double innerLen = Math.Pow(Math.Pow(innerPoint.X, 2.0) + Math.Pow(innerPoint.Y, 2.0), 0.5);
            Double adjustedWidth = outerLen - innerLen;

            {
                TransformGroup tGroup = new TransformGroup();
                ScaleTransform stf = new ScaleTransform(context.dialMarkerScale * adjustedWidth / context.gaugeWidth, 1.0);
                TranslateTransform ttf;
                if (isPoly)
                    ttf = new TranslateTransform(-(outerLen - DialMarkerGapBase), 0.0);
                else // unsue why this is needed for ellipse / circle 
                    ttf = new TranslateTransform(-(outerLen - DialMarkerGapBase), - shape.Height / 2.0);
                RotateTransform rtf = new RotateTransform(eAngle);

                tGroup.Children.Add(stf);
                tGroup.Children.Add(ttf);
                tGroup.Children.Add(rtf);
                shape.RenderTransform = tGroup;

                AdjustDropShadow(shape, eAngle + Rotation);

                //context.canvas.Children.Add(shape);
            }

            if (label != null)
            {
                context.canvas.Children.Add(label);

                label.Measure(new Size(Double.MaxValue, Double.MaxValue));
                label.Margin = new Thickness(0.0);
                label.Padding = new Thickness(0.0);
                sizeLabel = label.DesiredSize;
                label.Width = sizeLabel.Width;
                label.Height = sizeLabel.Height;

                TransformGroup tGroup = new TransformGroup();
                ScaleTransform stf = new ScaleTransform(adjustedWidth / context.gaugeWidth, 1.0);
                RotateTransform rtf = new RotateTransform(-Rotation, sizeLabel.Width / 2.0 + 5.0, sizeLabel.Height / 2.0);

                tGroup.Children.Add(stf);
                tGroup.Children.Add(rtf);
                label.RenderTransform = tGroup;

                Point labelPoint =
                    PointOnEllipse(context.radiusXInner - (context.dialNumeralGap + sizeLabel.Width / 2.0),
                    context.radiusYInner - (context.dialNumeralGap + sizeLabel.Height / 2.5), context.angle);

                label.SetValue(Canvas.TopProperty, -(labelPoint.Y + sizeLabel.Height / 2.0));
                label.SetValue(Canvas.LeftProperty, -(labelPoint.X + 5.0 + sizeLabel.Width / 2.0));

                if ((label.Effect != null)
                && (label.Effect.GetType() == typeof(DropShadowEffect)))
                {
                    DropShadowEffect shadow = new DropShadowEffect();
                    shadow.Color = ((DropShadowEffect)shape.Effect).Color;
                    shadow.Opacity = ((DropShadowEffect)shape.Effect).Opacity;
                    shadow.BlurRadius = ((DropShadowEffect)shape.Effect).BlurRadius;
                    shadow.ShadowDepth = ((DropShadowEffect)shape.Effect).ShadowDepth;
                    shadow.Direction = DropShadowAngle;
                    label.Effect = shadow;
                }                
            }            
        }

        private void LayoutDialSection(bool isLeft, bool fullDial, ref Size sizeLabel, DialMarkContext context)
        {
            int marks;
            int minorMarks = isLeft ? Scale1LeftMinorMarks : Scale1RightMinorMarks;
            int scale1Multiplier = geometry.Scale1Multiplier;
            
            Double majorIncrement;
            Double minorIncrement;

            if (isLeft)
                marks = geometry.Scale1LeftMarks;
            else
                marks = geometry.Scale1RightMarks;

            majorIncrement = fullDial ? DialArc / (marks) : DialArc / (2.0 * marks);
            minorIncrement = majorIncrement / (minorMarks + 1);

            if (DialReverse)
            {
                majorIncrement = -majorIncrement;
                minorIncrement = -minorIncrement;
            }

            context.labelValue = (Double)(isLeft ? (geometry.Scale1Min / scale1Multiplier) : 
                (fullDial ? (geometry.Scale1Min / scale1Multiplier) : (geometry.Scale1Centre / scale1Multiplier)));
            Double labelIncrement = (Double)(isLeft ? ((geometry.Scale1Centre - geometry.Scale1Min) / (scale1Multiplier * geometry.Scale1LeftMarks)) :
                (fullDial ? ((geometry.Scale1Max - geometry.Scale1Min) / (scale1Multiplier * geometry.Scale1RightMarks)) :
                ((geometry.Scale1Max - geometry.Scale1Centre) / (scale1Multiplier * geometry.Scale1RightMarks))));

            Double majorAngle = isLeft ? MinAngle : (fullDial ? MinAngle : 90.0);
            if (DialReverse && (isLeft || fullDial))
                majorAngle += DialArc;

            // Start with a Major Mark
            context.isMajor = true;
            context.angle = majorAngle;
            
            if (isLeft || fullDial)
                LayoutMark(ref sizeLabel, context);
           
            for (int iMajor = 0; iMajor < marks; iMajor++)
            {
                context.isMajor = false;
                for (int iMinor = 1; iMinor <= minorMarks; iMinor++)
                {
                    context.angle = majorAngle + (iMinor * minorIncrement);
                    LayoutMark(ref sizeLabel, context);
                }

                context.labelValue += labelIncrement;
                majorAngle += majorIncrement;
                context.angle = majorAngle;
                context.isMajor = true;
                LayoutMark(ref sizeLabel, context);
            }
        }

        private void LayoutDialContent()
        {
            DialMarkContext context;
            context.labelValue = 0;
            context.angle = 0.0;
            context.isMajor = true;
           
            context.canvas = GetTemplateChild("canvasScale1Markers") as Canvas;
            if (context.canvas == null)
                return;

            context.canvas.Children.Clear();

            context.styleLarge = null;
            context.styleSmall = null;
            context.styleDialNumeral = null;
            context.gaugeWidth = 20.0;

            try
            {
                context.styleLarge = (Style)this.FindResource("ScaleMarkerLarge");
            }
            catch (Exception)
            {
            }

            try
            {
                context.styleSmall = (Style)this.FindResource("ScaleMarkerSmall");
            }
            catch (Exception)
            {
            }

            try
            {
                context.styleDialNumeral = (Style)this.FindResource("DialNumeral");
            }
            catch (Exception)
            {
            }

            if (context.styleLarge == null) context.styleLarge = context.styleSmall;
            if (context.styleSmall == null) context.styleSmall = context.styleLarge;

            if (context.styleLarge == null)
                return;

            Shape shape;

            if (context.styleLarge.TargetType == typeof(Polygon))
                shape = new Polygon();
            else
                shape = new Ellipse();

            shape.Style = context.styleLarge;
            context.gaugeWidth = GetWidth(((Polygon)shape).Points);

            Double dialMarkerGap;
            if (DialRadiusY < 150.0)
            {
                dialMarkerGap = DialMarkerGapBase * DialRadiusY / 150.0;
                if (dialMarkerGap < 1.5)
                    dialMarkerGap = 1.5;
            }
            else
                dialMarkerGap = DialMarkerGapBase;

            context.dialMarkerScale = DialRadiusY / 150.0;           
            context.dialNumeralGap = DialNumeralGapBase * DialRadiusY / 150.0;
            if (context.dialNumeralGap < 1.5)
                context.dialNumeralGap = 1.5;

            context.dialNumeralFontSize = DialNumeralFontSizeBase * DialRadiusY / 150.0;
            if (context.dialNumeralFontSize < 10.0)
                context.dialNumeralFontSize = 10.0;

            context.radiusXInner = DialRadiusX - (context.gaugeWidth + dialMarkerGap);
            context.radiusYInner = DialRadiusY - (context.gaugeWidth * DialRadiusY / DialRadiusX + dialMarkerGap);

            Double labelValue;
            if (DialReverse)
                labelValue = geometry.Scale1Max / geometry.Scale1Multiplier;
            else
                labelValue = geometry.Scale1Min / geometry.Scale1Multiplier;

            Size sizeLabel = new Size(0.0, 0.0);

            if (geometry.Scale1LeftMarks > 0)
                LayoutDialSection(true, false, ref sizeLabel, context);

            LayoutDialSection(false, geometry.Scale1LeftMarks == 0, ref sizeLabel, context);

            PositionText(sizeLabel, context.radiusXInner, context.radiusYInner, context.dialNumeralGap);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            LayoutDialContent();

            StyleDialConsumption = null;
            try
            {
                StyleDialConsumption = (Style)this.FindResource("DialConsumption");
            }
            catch (Exception)
            {
            }

            StyleDialGeneration = null;
            try
            {
                StyleDialGeneration = (Style)this.FindResource("DialGeneration");
            }
            catch (Exception)
            {
            }

            StyleDialFeedIn = null;
            try
            {
                StyleDialFeedIn = (Style)this.FindResource("DialFeedIn");
            }
            catch (Exception)
            {
            }

            StyleDialConsumptionReverse = null;
            try
            {
                StyleDialConsumptionReverse = (Style)this.FindResource("DialConsumptionReverse");
            }
            catch (Exception)
            {
            }

            StyleDialGenerationReverse = null;
            try
            {
                StyleDialGenerationReverse = (Style)this.FindResource("DialGenerationReverse");
            }
            catch (Exception)
            {
            }

            StyleDialFeedInReverse = null;
            try
            {
                StyleDialFeedInReverse = (Style)this.FindResource("DialFeedInReverse");
            }
            catch (Exception)
            {
            }

            StyleDialGeneric = null;
            try
            {
                StyleDialGeneric = (Style)this.FindResource("DialGeneric");
            }
            catch (Exception)
            {
            }

            RecalcGeometry();
        }

        public static readonly DependencyProperty DialArcProperty = 
            DependencyProperty.Register("DialArc", typeof(Double), typeof(PowerGauge), new PropertyMetadata(180.0, new PropertyChangedCallback(OnDialArcChanged)));
        public Double DialArc
        {
            get
            {
                return (Double)base.GetValue(DialArcProperty);
            }
            set
            {
                base.SetValue(DialArcProperty, value);
            }
        }

        public Double DialArcRender 
        { 
            get 
            {
                Double arc = DialArc + DialArcOversize;
                if (arc > 360.0)
                    return 360.0;
                else
                    return arc;
            } 
        }

        private static void OnDialArcChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            ((PowerGauge)o).RecalcGeometry();
        }

        private static void OnDialReverseChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            ((PowerGauge)o).RecalcGeometry();
        }

        protected virtual void OnScale1ValueChanged(Double oldValue, Double newValue)
        {
            //RecalcGeometry();
            if (newValue > Scale1Max)
            {
                Scale1Max = ((int)(newValue / geometry.Scale1Multiplier) + 1) * geometry.Scale1Multiplier;
            }
            Pointer1Reposition(oldValue);
            RoutedPropertyChangedEventArgs<Double> args = new RoutedPropertyChangedEventArgs<Double>(oldValue, newValue);
            args.RoutedEvent = PowerGauge.Scale1ValueChangedEvent;
            RaiseEvent(args);
        }

        private static void OnScale1ValueInvalidated(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            PowerGauge gauge = (PowerGauge)obj;
            Double oldValue = (Double)args.OldValue;
            Double newValue = (Double)args.NewValue;
            gauge.OnScale1ValueChanged(oldValue, newValue);
        }

        protected virtual void OnGaugeDescriptionChanged(String oldValue, String newValue)
        {
            GaugeDescription = newValue;
            RoutedPropertyChangedEventArgs<String> args = new RoutedPropertyChangedEventArgs<String>(oldValue, newValue);
            args.RoutedEvent = PowerGauge.GaugeDescriptionChangedEvent;
            RaiseEvent(args);
        }

        private static void OnGaugeDescriptionInvalidated(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            PowerGauge gauge = (PowerGauge)obj;
            String oldValue = (String)args.OldValue;
            String newValue = (String)args.NewValue;
            if (newValue != oldValue)
                gauge.RecalcGeometry();
            //gauge.OnGaugeDescriptionChanged(oldValue, newValue);
        }
    }
}
