// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using Microsoft.ServiceBus;
using System.Collections.ObjectModel;
using System.Diagnostics;
using ServiceModelEx;
using System.ServiceModel.Configuration;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;

namespace ServiceModelEx.ServiceBus
{
   public static partial class ServiceBusHelper
   {
      internal const string DefaultIssuer = "owner";

      static void SetServiceBusCredentials(Collection<ServiceEndpoint> endpoints,string issuer,string secret)
      {
         TransportClientEndpointBehavior behavior = new TransportClientEndpointBehavior();
         behavior.CredentialType = TransportClientCredentialType.SharedSecret;
         behavior.Credentials.SharedSecret.IssuerName = issuer;
         behavior.Credentials.SharedSecret.IssuerSecret = secret;

         SetBehavior(endpoints,behavior);
      }
      
      public static void SetServiceBusCredentials<T>(this ClientBase<T> proxy,string secret) where T : class
      {
         if(proxy.State == CommunicationState.Opened)
         {
            throw new InvalidOperationException("Proxy is already opened");
         }
         proxy.SetServiceBusCredentials(DefaultIssuer,secret);
      }
      public static void SetServiceBusCredentials<T>(this ClientBase<T> proxy,string issuer,string secret) where T : class
      {
         if(proxy.State == CommunicationState.Opened)
         {
            throw new InvalidOperationException("Proxy is already opened");
         }
         proxy.ChannelFactory.SetServiceBusCredentials(issuer,secret);
      }

      public static void SetServiceBusCredentials<T>(this ChannelFactory<T> factory,string issuer,string secret) where T : class
      {
         if(factory.State == CommunicationState.Opened)
         {
            throw new InvalidOperationException("Factory is already opened");
         }
         Collection<ServiceEndpoint> endpoints = new Collection<ServiceEndpoint>();
         endpoints.Add(factory.Endpoint);
         SetServiceBusCredentials(endpoints,issuer,secret);
      }
      public static void SetServiceBusCredentials<T>(this ChannelFactory<T> factory,string secret) where T : class
      {
         factory.SetServiceBusCredentials(DefaultIssuer,secret);
      }
      public static void SetServiceBusCredentials(this ServiceHost host,string secret)
      {
         if(host.State == CommunicationState.Opened)
         {
            throw new InvalidOperationException("Host is already opened");
         }
         SetServiceBusCredentials(host.Description.Endpoints,DefaultIssuer,secret);
      }
      public static void SetServiceBusCredentials(this ServiceHost host,string issuer,string secret)
      {
         if(host.State == CommunicationState.Opened)
         {
            throw new InvalidOperationException("Host is already opened");
         }
         SetServiceBusCredentials(host.Description.Endpoints,issuer,secret);
      }
   }
}





