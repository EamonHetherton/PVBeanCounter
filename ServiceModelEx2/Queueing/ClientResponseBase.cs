// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
using System.Reflection;
using System.Diagnostics;

namespace ServiceModelEx
{
   public abstract class ClientResponseBase<T> : HeaderClientBase<T,ResponseContext> where T : class
   {
      public readonly string ResponseAddress;

      public ClientResponseBase(string responseAddress)
      {
         ResponseAddress = responseAddress;
         Endpoint.VerifyQueue();
         Debug.Assert(Endpoint.Binding is NetMsmqBinding);
      }
      public ClientResponseBase(string responseAddress,string endpointName) : base(endpointName)
      {
         ResponseAddress = responseAddress;
         Endpoint.VerifyQueue();
         Debug.Assert(Endpoint.Binding is NetMsmqBinding);
      }
      public ClientResponseBase(string responseAddress,string endpointName,string remoteAddress) : base(endpointName,remoteAddress)
      {
         ResponseAddress = responseAddress;
         Endpoint.VerifyQueue();
         Debug.Assert(Endpoint.Binding is NetMsmqBinding);
      }
      public ClientResponseBase(string responseAddress,string endpointName,EndpointAddress remoteAddress) : base(endpointName,remoteAddress)
      {
         ResponseAddress = responseAddress;
         Endpoint.VerifyQueue();
         Debug.Assert(Endpoint.Binding is NetMsmqBinding);
      }
      public ClientResponseBase(string responseAddress,NetMsmqBinding binding,EndpointAddress remoteAddress) : base(binding,remoteAddress)
      {
         ResponseAddress = responseAddress;
         Endpoint.VerifyQueue();
      }
      protected string Enqueue(Action action) 
      {
         string methodId = GenerateMethodId();
         Header = new ResponseContext(ResponseAddress,methodId);
         action();
         return Header.MethodId;
      }
      protected virtual string GenerateMethodId()
      {
         return Guid.NewGuid().ToString();
      }
   }
}
