// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;

namespace ServiceModelEx
{
   /// <summary>
	/// Provides thread-safe access to some methods and properties
	/// </summary>
   [ToolboxBitmap(typeof(SafeTrackBar),"SafeProgressBar.bmp")]
   public class SafeTrackBar : TrackBar
   {
      SynchronizationContext m_SynchronizationContext = SynchronizationContext.Current;

      public int GetValue()
      {
         int value = 0;
         SendOrPostCallback getValue = delegate
                                       {
                                          value = base.Value;
                                       };
         Debug.Assert(m_SynchronizationContext == SynchronizationContext.Current && InvokeRequired == false || InvokeRequired == true);

         try
         {
            m_SynchronizationContext.Send(getValue,null);
         }
         catch
         {}
         return value;   
      }
      public void SetValue(int progress)
      {
         SendOrPostCallback setValue = delegate(object value)
                                       {
                                          base.Value = (int)value;
                                       };
         Debug.Assert(m_SynchronizationContext == SynchronizationContext.Current && InvokeRequired == false || InvokeRequired == true);

         try
         {
            m_SynchronizationContext.Send(setValue,progress);
         }
         catch
         {}
      }
   }
}
