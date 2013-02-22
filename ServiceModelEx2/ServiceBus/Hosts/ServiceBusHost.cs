// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net


using System;
using System.ServiceModel;
using Microsoft.ServiceBus;
using System.Diagnostics;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;
using System.Web.Security;
using System.Reflection;
using System.Collections.Generic;


namespace ServiceModelEx.ServiceBus
{
   public class ServiceBusHost : ServiceHost,IServiceBusProperties
   {
      public ServiceBusHost(object singletonInstance,params Uri[] baseAddresses) : base(singletonInstance,baseAddresses)
      {
         IEndpointBehavior registeryBehavior = new ServiceRegistrySettings(DiscoveryType.Public);
         foreach(ServiceEndpoint endpoint in Description.Endpoints)
         {
            endpoint.Behaviors.Add(registeryBehavior);
         }
      }
      public ServiceBusHost(Type serviceType,params Uri[] baseAddresses) : base(serviceType,baseAddresses)
      {
         IEndpointBehavior registeryBehavior = new ServiceRegistrySettings(DiscoveryType.Public);
         foreach(ServiceEndpoint endpoint in Description.Endpoints)
         {
            endpoint.Behaviors.Add(registeryBehavior);
         }
      }
      protected override void OnOpening()
      {
         if(Credentials.ServiceCertificate.Certificate == null)
         {
            ConfigureAnonymousMessageSecurity();
         }
         base.OnOpening();
      }
      protected void ConfigureAnonymousMessageSecurity()
      {
         ConfigureAnonymousMessageSecurity("",StoreLocation.LocalMachine,StoreName.My);
      }
      public void ConfigureAnonymousMessageSecurity(string serviceCert)
      {
         ConfigureAnonymousMessageSecurity(serviceCert,StoreLocation.LocalMachine,StoreName.My);
      }
      public void ConfigureAnonymousMessageSecurity(string serviceCert,StoreLocation location,StoreName storeName)
      {
         if(serviceCert == String.Empty)
         {
            serviceCert = ServiceBusHelper.ExtractNamespace(Description.Endpoints[0].Address.Uri);
         }
         ConfigureAnonymousMessageSecurity(location,storeName,X509FindType.FindBySubjectName,serviceCert);
      }
      public void ConfigureAnonymousMessageSecurity(StoreLocation location,StoreName storeName,X509FindType findType,object findValue)
      {
         Credentials.ServiceCertificate.SetCertificate(location,storeName,findType,findValue);
         Authorization.PrincipalPermissionMode = PrincipalPermissionMode.None;

         foreach(ServiceEndpoint endpoint in Description.Endpoints)
         {    
            ServiceBusHelper.ConfigureBinding(endpoint.Binding);
         } 
      }
      public void ConfigureMessageSecurity()
      {
         ConfigureMessageSecurity("",StoreLocation.LocalMachine,StoreName.My,true,null);
      }      
      public void ConfigureMessageSecurity(string serviceCert)
      {
         ConfigureMessageSecurity(serviceCert,StoreLocation.LocalMachine,StoreName.My,true,null);
      }
      public void ConfigureMessageSecurity(string serviceCert,string applicationName)
      {
         ConfigureMessageSecurity(serviceCert,StoreLocation.LocalMachine,StoreName.My,true,applicationName);
      }
      public void ConfigureMessageSecurity(string serviceCert,bool useProviders,string applicationName)
      {
         ConfigureMessageSecurity(serviceCert,StoreLocation.LocalMachine,StoreName.My,useProviders,applicationName);
      }
      public void ConfigureMessageSecurity(string serviceCert,StoreLocation location,StoreName storeName,bool useProviders,string applicationName)
      {
         if(serviceCert == String.Empty)
         {
            serviceCert = ServiceBusHelper.ExtractNamespace(Description.Endpoints[0].Address.Uri);
         }
         ConfigureMessageSecurity(location,storeName,X509FindType.FindBySubjectName,serviceCert,useProviders,applicationName);
      }
      public void ConfigureMessageSecurity(StoreLocation location,StoreName storeName,X509FindType findType,object findValue,bool useProviders,string applicationName)
      {
         Credentials.ServiceCertificate.SetCertificate(location,storeName,findType,findValue);

         foreach(ServiceEndpoint endpoint in Description.Endpoints)
         {    
            ServiceBusHelper.ConfigureBinding(endpoint.Binding,false);
         }
         if(useProviders)
         {
            Authorization.PrincipalPermissionMode = PrincipalPermissionMode.UseAspNetRoles;
            Credentials.UserNameAuthentication.UserNamePasswordValidationMode = UserNamePasswordValidationMode.MembershipProvider;

            SecurityBehavior.EnableRoleManagerInConfig();

            string application;

            if(String.IsNullOrEmpty(applicationName))
            {
               applicationName = Membership.ApplicationName;
            }
            if(String.IsNullOrEmpty(applicationName) || applicationName == "/")
            {
               if(String.IsNullOrEmpty(Assembly.GetEntryAssembly().GetName().Name))
               {
                  application = AppDomain.CurrentDomain.FriendlyName;
               }
               else
               {
                  application = Assembly.GetEntryAssembly().GetName().Name;
               }
            }
            else
            {
               application = applicationName;
            }
            Membership.ApplicationName = application;
            Roles.ApplicationName = application;
         }
      }
      TransportClientEndpointBehavior IServiceBusProperties.Credential
      {
         get
         {
            TransportClientEndpointBehavior credentials = Description.Endpoints[0].Behaviors.Find<TransportClientEndpointBehavior>();
            //Sanity check
            foreach(ServiceEndpoint endpoint in Description.Endpoints)
            {
               Debug.Assert(endpoint.Behaviors.Find<TransportClientEndpointBehavior>() == credentials);
            }
            return credentials;
         }
         set
         {
            foreach(ServiceEndpoint endpoint in Description.Endpoints)
            {
               Debug.Assert(endpoint.Behaviors.Contains(typeof(TransportClientEndpointBehavior)) == false,"Do not add credentials mutiple times");
               endpoint.Behaviors.Add(value);
            }
         }
      }
      Uri[] IServiceBusProperties.Addresses
      {
         get
         {
            List<Uri> addresses = new List<Uri>();

            foreach(ServiceEndpoint endpoint in Description.Endpoints)
            {
               addresses.Add(endpoint.Address.Uri);
            }
            return addresses.ToArray();
         }
      }
   }
}
