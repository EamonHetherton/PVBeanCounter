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
	/// Provides thread-safe enabling of the button
	/// </summary>
   [ToolboxBitmap(typeof(SafeButton),"SafeButton.bmp")]
   public class SafeButton : Button
   {
      SynchronizationContext m_SynchronizationContext = SynchronizationContext.Current;
      
      public bool SafeEnabled
      {
         set
         {
            SendOrPostCallback enable = delegate(object enabled)
                                        {
                                           base.Enabled = (bool)enabled;
                                        };
            Debug.Assert(m_SynchronizationContext == SynchronizationContext.Current && InvokeRequired == false || InvokeRequired == true);

            try
            {
               m_SynchronizationContext.Send(enable,value);
            }
            catch
            {}
         }
         get
         {
            bool status = false;
            SendOrPostCallback enabled = delegate
                                         {
                                            status = base.Enabled;
                                         };
            Debug.Assert(m_SynchronizationContext == SynchronizationContext.Current && InvokeRequired == false || InvokeRequired == true);

            try
            {
               m_SynchronizationContext.Send(enabled,null);
            }
            catch
            {}
            
            return status;
         }
      }
   }
}
