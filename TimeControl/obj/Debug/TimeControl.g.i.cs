﻿#pragma checksum "..\..\TimeControl.xaml" "{406ea660-64cf-4c82-b6f0-42d48172a799}" "15E30EB6C1663B9A3AAEB915209A9117"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.17929
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using CGS;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace CGS {
    
    
    /// <summary>
    /// TimeControl
    /// </summary>
    public partial class TimeControl : System.Windows.Controls.UserControl, System.Windows.Markup.IComponentConnector {
        
        
        #line 7 "..\..\TimeControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal CGS.TimeControl UserControl;
        
        #line default
        #line hidden
        
        
        #line 13 "..\..\TimeControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid LayoutRoot;
        
        #line default
        #line hidden
        
        
        #line 22 "..\..\TimeControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid hour;
        
        #line default
        #line hidden
        
        
        #line 28 "..\..\TimeControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBlock sep1;
        
        #line default
        #line hidden
        
        
        #line 32 "..\..\TimeControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid min;
        
        #line default
        #line hidden
        
        
        #line 37 "..\..\TimeControl.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid half;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/TimeControl;component/timecontrol.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\TimeControl.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.UserControl = ((CGS.TimeControl)(target));
            return;
            case 2:
            this.LayoutRoot = ((System.Windows.Controls.Grid)(target));
            return;
            case 3:
            this.hour = ((System.Windows.Controls.Grid)(target));
            
            #line 22 "..\..\TimeControl.xaml"
            this.hour.KeyDown += new System.Windows.Input.KeyEventHandler(this.Down);
            
            #line default
            #line hidden
            
            #line 22 "..\..\TimeControl.xaml"
            this.hour.GotFocus += new System.Windows.RoutedEventHandler(this.Grid_GotFocus);
            
            #line default
            #line hidden
            
            #line 22 "..\..\TimeControl.xaml"
            this.hour.LostFocus += new System.Windows.RoutedEventHandler(this.Grid_LostFocus);
            
            #line default
            #line hidden
            
            #line 22 "..\..\TimeControl.xaml"
            this.hour.MouseDown += new System.Windows.Input.MouseButtonEventHandler(this.Grid_MouseDown);
            
            #line default
            #line hidden
            return;
            case 4:
            this.sep1 = ((System.Windows.Controls.TextBlock)(target));
            return;
            case 5:
            this.min = ((System.Windows.Controls.Grid)(target));
            
            #line 32 "..\..\TimeControl.xaml"
            this.min.KeyDown += new System.Windows.Input.KeyEventHandler(this.Down);
            
            #line default
            #line hidden
            
            #line 32 "..\..\TimeControl.xaml"
            this.min.GotFocus += new System.Windows.RoutedEventHandler(this.Grid_GotFocus);
            
            #line default
            #line hidden
            
            #line 32 "..\..\TimeControl.xaml"
            this.min.LostFocus += new System.Windows.RoutedEventHandler(this.Grid_LostFocus);
            
            #line default
            #line hidden
            
            #line 32 "..\..\TimeControl.xaml"
            this.min.MouseDown += new System.Windows.Input.MouseButtonEventHandler(this.Grid_MouseDown);
            
            #line default
            #line hidden
            return;
            case 6:
            this.half = ((System.Windows.Controls.Grid)(target));
            
            #line 37 "..\..\TimeControl.xaml"
            this.half.KeyDown += new System.Windows.Input.KeyEventHandler(this.Down);
            
            #line default
            #line hidden
            
            #line 37 "..\..\TimeControl.xaml"
            this.half.GotFocus += new System.Windows.RoutedEventHandler(this.Grid_GotFocus);
            
            #line default
            #line hidden
            
            #line 37 "..\..\TimeControl.xaml"
            this.half.LostFocus += new System.Windows.RoutedEventHandler(this.Grid_LostFocus);
            
            #line default
            #line hidden
            
            #line 37 "..\..\TimeControl.xaml"
            this.half.MouseDown += new System.Windows.Input.MouseButtonEventHandler(this.Grid_MouseDown);
            
            #line default
            #line hidden
            return;
            }
            this._contentLoaded = true;
        }
    }
}

