// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Collections.ObjectModel;

namespace ServiceModelEx
{
   [AttributeUsage(AttributeTargets.Class)]
   public class ThreadAffinityBehaviorAttribute : ThreadPoolBehaviorAttribute
   {
      public ThreadAffinityBehaviorAttribute(Type serviceType) : this(serviceType,"Affinity Worker Thread")
      {}
      public ThreadAffinityBehaviorAttribute(Type serviceType,string threadName) : base(1,serviceType,threadName)
      {}
   }
}