// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using Microsoft.ServiceBus;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ServiceModel.Configuration;
using System.Configuration;
using System.ServiceModel.Channels;

namespace ServiceModelEx.ServiceBus
{
   public static partial class ServiceBusHelper
   {
      static void SetBehavior(Collection<ServiceEndpoint> endpoints,TransportClientEndpointBehavior credential)
      {
         foreach(ServiceEndpoint endpoint in endpoints)
         {
            if(endpoint.Binding is NetTcpRelayBinding   ||
               endpoint.Binding is WSHttpRelayBinding   ||
               endpoint.Binding is NetOnewayRelayBinding)
            {
               endpoint.Behaviors.Add(credential);
            }
         }
      }
      internal static void ConfigureBinding(Binding binding)
      {
         ConfigureBinding(binding,true);
      }
      internal static void ConfigureBinding(Binding binding,bool anonymous)
      {
         if(binding is NetTcpRelayBinding)
         {
            NetTcpRelayBinding tcpBinding = (NetTcpRelayBinding)binding;
            tcpBinding.Security.Mode  = EndToEndSecurityMode.Message;
            if(anonymous)
            {
               tcpBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;
            }
            else
            {
               tcpBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            }

            tcpBinding.ConnectionMode = TcpRelayConnectionMode.Hybrid;
            tcpBinding.ReliableSession.Enabled = true; 

            return;
         }
         if(binding is WSHttpRelayBinding)
         {
            WSHttpRelayBinding wsBinding = (WSHttpRelayBinding)binding;
            wsBinding.Security.Mode = EndToEndSecurityMode.Message;
            if(anonymous)
            {
               wsBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;
            }
            else
            {
               wsBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            }
            wsBinding.ReliableSession.Enabled = true; 

            return;
         }
         if(binding is NetOnewayRelayBinding)
         {
            NetOnewayRelayBinding onewayBinding = (NetOnewayRelayBinding)binding;
            onewayBinding.Security.Mode = EndToEndSecurityMode.Message;
            if(anonymous)
            {
               onewayBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;
            }
            else
            {
               onewayBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            }
            return;
         }
         throw new InvalidOperationException(binding.GetType() + " is unsupported");
      }

      public static string ExtractNamespace(Uri address)
      {
         return address.Host.Split('.')[0];
      }

      public static string ExtractNamespace(string endpointName)
      {
         Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
         ServiceModelSectionGroup sectionGroup = ServiceModelSectionGroup.GetSectionGroup(config);

         foreach(ChannelEndpointElement endpointElement in sectionGroup.Client.Endpoints)
         {
            if(endpointElement.Name == endpointName)
            {
               return ExtractNamespace(endpointElement.Address);
            }
         }
         return null;
      }
      public static string ExtractNamespace(Type serviceType)
      {
         Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
         ServiceModelSectionGroup sectionGroup = ServiceModelSectionGroup.GetSectionGroup(config);

         foreach(ServiceElement serviceElement in sectionGroup.Services.Services)
         {
            if(serviceElement.Name == serviceType.ToString())
            {
               return ExtractNamespace(serviceElement.Endpoints[0].Address);
            }
         }
         return null;
      }
   }
}





