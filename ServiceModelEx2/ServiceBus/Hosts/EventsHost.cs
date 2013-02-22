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
using System.ServiceModel.Description;

namespace ServiceModelEx.ServiceBus
{
   public abstract class EventsHost : IServiceBusProperties
   {
      TransportClientEndpointBehavior m_Credential;

      //For service cert lookup
      StoreLocation m_ServiceCertLocation;
      StoreName m_ServiceCertStoreName;
      X509FindType m_ServiceCertFindType;
      object m_ServiceCertFindValue;

      //For message security
      string m_ApplicationName;
      bool m_UseProviders;
      bool m_Anonymous;

      //Managing the host-per-event
      protected Dictionary<Type,Dictionary<string,ServiceBusHost>> Hosts
      {get;set;}

      Type m_SericeType;
      protected string[] BaseAddresses
      {get;set;}

      object m_SingletonInstance;

      protected NetOnewayRelayBinding RelayBinding
      {get;set;}

      //Service bus credentials
      string m_ServiceBusPassword;
      string m_Issuer;

      public EventsHost(object singletonInstance,string baseAddress) : this(singletonInstance,new string[]{baseAddress})
      {}

      public EventsHost(object singletonInstance,string[] baseAddresses) 
      {
         Hosts = new Dictionary<Type,Dictionary<string,ServiceBusHost>>();

         Debug.Assert(baseAddresses != null);
         Debug.Assert(baseAddresses.Length > 0);

         m_SingletonInstance = singletonInstance;

         for(int index = 0;index < baseAddresses.Length;index++)
         {
            if(baseAddresses[index].EndsWith("/") == false)
            {
               baseAddresses[index] += "/";
            }
         }

         BaseAddresses = baseAddresses;

         //Try to guess a certificate 
         ConfigureAnonymousMessageSecurity(ServiceBusHelper.ExtractNamespace(new Uri(baseAddresses[0])));
      }
      public EventsHost(Type serviceType,string baseAddress) : this(serviceType,new string[]{baseAddress})
      {}
      public EventsHost(Type serviceType,string[] baseAddresses)
      {
         Hosts = new Dictionary<Type,Dictionary<string,ServiceBusHost>>();

         Debug.Assert(baseAddresses != null);
         Debug.Assert(baseAddresses.Length > 0);

         m_SericeType = serviceType;

         for(int index = 0;index < baseAddresses.Length;index++)
         {
            if(baseAddresses[index].EndsWith("/") == false)
            {
               baseAddresses[index] += "/";
            }
         }
         BaseAddresses = baseAddresses;
          
         //Try to guess a certificate 
         ConfigureAnonymousMessageSecurity(ServiceBusHelper.ExtractNamespace(new Uri(baseAddresses[0])));
      }
      public void SetServiceBusCredentials(string secret)
      {
         SetServiceBusCredentials(ServiceBusHelper.DefaultIssuer,secret);
      }
      public void SetServiceBusCredentials(string isssuer,string secret)
      {
         m_ServiceBusPassword = secret;
         m_Issuer = isssuer;

         m_Credential = new TransportClientEndpointBehavior();
         m_Credential.CredentialType = TransportClientCredentialType.SharedSecret;
         m_Credential.Credentials.SharedSecret.IssuerName = isssuer;
         m_Credential.Credentials.SharedSecret.IssuerSecret = secret;
      }

      public void ConfigureAnonymousMessageSecurity(string serviceCert)
      {
         ConfigureAnonymousMessageSecurity(serviceCert,StoreLocation.LocalMachine,StoreName.My);
      }
      public void ConfigureAnonymousMessageSecurity(string serviceCert,StoreLocation location,StoreName storeName)
      {
         ConfigureAnonymousMessageSecurity(location,storeName,X509FindType.FindBySubjectName,serviceCert);
      }
            
      [MethodImpl(MethodImplOptions.Synchronized)]
      public void ConfigureAnonymousMessageSecurity(StoreLocation location,StoreName storeName,X509FindType findType,object findValue)
      {
         m_ServiceCertLocation = location;
         m_ServiceCertStoreName = storeName;
         m_ServiceCertFindType = findType;
         m_ServiceCertFindValue = findValue;
         m_Anonymous = true;
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
         ConfigureMessageSecurity(location,storeName,X509FindType.FindBySubjectName,serviceCert,useProviders,applicationName);
      }    
      [MethodImpl(MethodImplOptions.Synchronized)]
      void ConfigureMessageSecurity(StoreLocation location,StoreName storeName,X509FindType findType,object findValue,bool useProviders,string applicationName)
      {
         m_ServiceCertLocation = location;
         m_ServiceCertStoreName = storeName;
         m_ServiceCertFindType = findType;
         m_ServiceCertFindValue = findValue;
         m_UseProviders = useProviders;
         m_ApplicationName = applicationName;
         m_Anonymous = false;
      }

      [MethodImpl(MethodImplOptions.Synchronized)]
      public abstract void SetBinding(NetOnewayRelayBinding binding);

      [MethodImpl(MethodImplOptions.Synchronized)]
      public abstract void SetBinding(string bindingConfigName);

      protected abstract NetOnewayRelayBinding GetBinding();

      
      [MethodImpl(MethodImplOptions.Synchronized)]
      public void Subscribe()
      {
         Type serviceType;
         if(m_SericeType != null)
         {
            serviceType = m_SericeType;
         }
         else
         {
            serviceType = m_SingletonInstance.GetType();
         }
         Type[] interfaces = serviceType.GetInterfaces();
         foreach(Type interfaceType in interfaces)
         {
            if(interfaceType.GetCustomAttributes(typeof(ServiceContractAttribute),false).Length == 1)
            {
               Subscribe(interfaceType);
            }
         }
      }

      [MethodImpl(MethodImplOptions.Synchronized)]
      public void Subscribe(Type contractType)
      {
         string[] operations = GetOperations(contractType);

         foreach(string operationName in operations)
         {
            Subscribe(contractType,operationName);
         }
      }
       
      [MethodImpl(MethodImplOptions.Synchronized)]
      public virtual void Subscribe(Type contractType,string operation)
      {
         Debug.Assert(String.IsNullOrEmpty(operation) == false);

         if(Hosts.ContainsKey(contractType) == false)
         {
            Hosts[contractType] = new Dictionary<string,ServiceBusHost>();

            string[] operations = GetOperations(contractType);
            foreach(string operationName in operations)
            {
               Hosts[contractType][operationName] = null;
            }
         }
         if(Hosts[contractType][operation] == null)
         {
            if(m_SericeType != null)
            {
               Hosts[contractType][operation] = new ServiceBusHost(m_SericeType);
            }
            else
            {
               Hosts[contractType][operation] = new ServiceBusHost(m_SingletonInstance);
            }

            AddHostEndpoints(contractType,operation);
            
            //Configure service bus credentials for the host
            if(m_ServiceBusPassword != null)
            {
               Hosts[contractType][operation].SetServiceBusCredentials(m_ServiceBusPassword);
            }

            //Configure message security
            if(m_Anonymous)
            {
               Hosts[contractType][operation].ConfigureAnonymousMessageSecurity(m_ServiceCertLocation,m_ServiceCertStoreName,m_ServiceCertFindType,m_ServiceCertFindValue);
            }
            else
            {
               Hosts[contractType][operation].ConfigureMessageSecurity(m_ServiceCertLocation,m_ServiceCertStoreName,m_ServiceCertFindType,m_ServiceCertFindValue,m_UseProviders,m_ApplicationName);
            }

            Hosts[contractType][operation].Open();
         }
      }

      protected virtual void AddHostEndpoints(Type contractType,string operation)
      {
         Binding binding = GetBinding();

         foreach(string baseAddress in BaseAddresses)
         {
            string address = baseAddress + contractType + "/" + operation + "/";
            Hosts[contractType][operation].AddServiceEndpoint(contractType,binding,address);
         }  
      }

      [MethodImpl(MethodImplOptions.Synchronized)]
      public void Unsubscribe()
      {
         foreach(Type contractType in Hosts.Keys)
         {
            Unsubscribe(contractType);
         }
      }
            
      [MethodImpl(MethodImplOptions.Synchronized)]
      public void Unsubscribe(Type contractType)
      {
         string[] operations = GetOperations(contractType);

         foreach(string operationName in operations)
         {
            Unsubscribe(contractType,operationName);
         }
      }      
      
      [MethodImpl(MethodImplOptions.Synchronized)]
      public virtual void Unsubscribe(Type contractType,string operation)
      {
         Debug.Assert(String.IsNullOrEmpty(operation) == false);

         if(Hosts.ContainsKey(contractType) == false)
         {
            return;
         }
         if(Hosts[contractType][operation] != null)
         {
            Hosts[contractType][operation].Close();
            Hosts[contractType].Remove(operation);
            Hosts[contractType][operation] = null;
         }
      }

      static internal string[] GetOperations(Type contract)
      {
         MethodInfo[] methods = contract.GetMethods(BindingFlags.Public|BindingFlags.FlattenHierarchy|BindingFlags.Instance);
         List<string> operations = new List<string>(methods.Length);

         Action<MethodInfo> add = (method)=>
                                  {
                                     Debug.Assert(! operations.Contains(method.Name));
                                     operations.Add(method.Name);
                                  };
         methods.ForEach(add);
         return operations.ToArray();
      }
      
      [MethodImpl(MethodImplOptions.Synchronized)]
      public void Abort()
      {
         foreach(Type contractType in Hosts.Keys)
         {
            foreach(ServiceHost host in Hosts[contractType].Values)
            {
               if(host != null)
               {
                  host.Abort();
               }
            }
         }
      }

      public TransportClientEndpointBehavior Credential
      {
         get
         {
            if(m_Credential == null)
            {
               m_Credential = new TransportClientEndpointBehavior();
            }
            return m_Credential;
         }
         set
         {
            m_Credential = value;
         }
      }

      public Uri[] Addresses
      {
         get
         {
            List<Uri> addresses = new List<Uri>();

            foreach(Dictionary<string,ServiceBusHost> dictionary in Hosts.Values)
            {
               foreach(ServiceBusHost host in dictionary.Values)
               {
                  foreach(ServiceEndpoint endpoint in host.Description.Endpoints)
                  {
                     addresses.Add(endpoint.Address.Uri);
                  }
               }
            }
            return addresses.ToArray();
         }
      }
   }
}
