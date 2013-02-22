// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.Messaging;
using System.ServiceModel;
using System.Diagnostics;
using System.Threading;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Xml;
using Microsoft.ServiceBus;

namespace ServiceModelEx.ServiceBus
{
   public abstract class ServiceBufferResponseBase<T> : BufferedServiceBusClient<T> where T : class 
   {
      public ServiceBufferResponseBase() : base(new Uri(ResponseContext.Current.ResponseAddress))
      {
         Header = ResponseContext.Current;
                  
         //Grab the creds the host was using 
         IServiceBusProperties properties = OperationContext.Current.Host as IServiceBusProperties;
         Debug.Assert(properties != null);

         Credential = properties.Credential;
      }
   }
}





