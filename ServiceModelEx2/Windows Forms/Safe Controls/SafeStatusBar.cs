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
   /// Provides thread-safe access way to set the Text property
   /// </summary>
   [ToolboxBitmap(typeof(SafeStatusBar),"SafeStatusBar.bmp")]
   public class SafeStatusBar : StatusBar
   {
      SynchronizationContext m_SynchronizationContext = SynchronizationContext.Current;

      override public string Text
      {
         set
         {
            SendOrPostCallback setText = delegate(object text)
                                         {
                                            base.Text = text as string;
                                         };

            Debug.Assert(m_SynchronizationContext == SynchronizationContext.Current && InvokeRequired == false || InvokeRequired == true);

            try
            {
               m_SynchronizationContext.Send(setText,value);
            }
            catch
            {}
         }
         get
         {
            string text = String.Empty;
            SendOrPostCallback getText = delegate
                                         {
                                            text = base.Text;
                                         };
            Debug.Assert(m_SynchronizationContext == SynchronizationContext.Current && InvokeRequired == false || InvokeRequired == true);

            try
            {
               m_SynchronizationContext.Send(getText,null);
               return text;
            }
            catch
            {}
            return text;
         }
      }
   }
}
