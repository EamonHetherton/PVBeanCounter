   // © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net


using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.ServiceModel;
using Microsoft.ServiceBus;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;

namespace ServiceModelEx.ServiceBus
{
   public class EventsRelayHost : EventsHost
   {
      public EventsRelayHost(object singletonInstance,string baseAddress) : this(singletonInstance,new string[]{baseAddress})
      {}

      public EventsRelayHost(object singletonInstance,string[] baseAddresses) : base(singletonInstance,baseAddresses)
      {}

      public EventsRelayHost(Type serviceType,string baseAddress) : this(serviceType,new string[]{baseAddress})
      {}
      public EventsRelayHost(Type serviceType,string[] baseAddresses) : base(serviceType,baseAddresses)
      {} 


      [MethodImpl(MethodImplOptions.Synchronized)]
      public override void SetBinding(NetOnewayRelayBinding binding)
      {
         Debug.Assert(binding is NetEventRelayBinding);

         RelayBinding = binding;
      }

      [MethodImpl(MethodImplOptions.Synchronized)]
      public override void SetBinding(string bindingConfigName)
      {
         SetBinding(new NetEventRelayBinding(bindingConfigName));
      }
      protected override NetOnewayRelayBinding GetBinding()
      {
         return RelayBinding ?? new NetEventRelayBinding();
      }
   }
}
