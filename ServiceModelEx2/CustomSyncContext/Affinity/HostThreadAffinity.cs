// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.Messaging;
using System.ServiceModel;
using System.Diagnostics;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ServiceModelEx
{
   public static class HostThreadAffinity 
   {
      /// <summary>
      /// Can only call before openning the host
      /// </summary>
      public static void SetThreadAffinity(this ServiceHost host,string threadName)
      {
         if(host.State == CommunicationState.Opened)
         {
            throw new InvalidOperationException("Host is already opened");
         }

         Debug.Assert(SynchronizationContext.Current == null);//Can call only once

         AffinitySynchronizer affinitySynchronizer = new AffinitySynchronizer(threadName);
         SynchronizationContext.SetSynchronizationContext(affinitySynchronizer);

         host.Closing += delegate
                         {
                            using(affinitySynchronizer)
                            {}
                         };
      }
      /// <summary>
      ///  Can only call before openning the host
      /// </summary>
      public static void SetThreadAffinity(this ServiceHost host)
      {
         SetThreadAffinity(host,"Executing all endpoints of " + host.Description.ServiceType);
      }           
   }
}





