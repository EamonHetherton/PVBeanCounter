// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.ServiceBus;
using System.Reflection;
using System.ServiceModel.Channels;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Description;

namespace ServiceModelEx.ServiceBus
{
   public abstract class EventRelayClientBase<T> : IServiceBusProperties,IDisposable where T : class
   {
      //State management 
      public event EventHandler Closed  = delegate{};
      public event EventHandler Closing = delegate{};
      public event EventHandler Opened  = delegate{};
      public event EventHandler Opening = delegate{};

      public CommunicationState State 
      {
         get;private set;
      }

      //Service bus items
      readonly string BaseAddress;

      public NetOnewayRelayBinding Binding
      {get;protected set;}

      Dictionary<string,T> m_Proxies;

      //For message security
      readonly string ServiceUsername;
      readonly string ServicePassword;
      readonly bool Anonymous;
   
      //For serivce cert lookup
      StoreLocation m_ServiceCertLocation;
      StoreName m_ServiceCertStoreName;
      X509FindType m_ServiceCertFindType;
      object m_ServiceCertFindValue;

      //Service bus credentials
      string m_Secret;
      string m_Issuer;

      public EventRelayClientBase(string namespaceBaseAddress,string username,string password) : this(namespaceBaseAddress,null,username,password)
      {}
      public EventRelayClientBase(string namespaceBaseAddress,NetOnewayRelayBinding binding,string username,string password) : this(namespaceBaseAddress,binding)
      {
         ServiceUsername = username;
         ServicePassword = password;
         Anonymous = false;
      }

      public EventRelayClientBase(string namespaceBaseAddress) : this(namespaceBaseAddress,new NetOnewayRelayBinding())
      {}      
      public EventRelayClientBase(string namespaceBaseAddress,NetOnewayRelayBinding binding)
      {
         State = CommunicationState.Faulted;

         Debug.Assert(namespaceBaseAddress.Contains("servicebus.windows.net"));
         if(namespaceBaseAddress.EndsWith("/") == false)
         {
            namespaceBaseAddress += "/";
         }
         BaseAddress = namespaceBaseAddress;

         Binding = binding;

         Anonymous = true;

         SetServiceCertificate("");

         State = CommunicationState.Created;
      }      
      
      [MethodImpl(MethodImplOptions.Synchronized)]
      public void Open()
      {
         if(State != CommunicationState.Created)
         {
            return;
         }
         try
         {
            Opening(this,EventArgs.Empty);
            ServiceBusHelper.ConfigureBinding(Binding,Anonymous);

            m_Proxies = new Dictionary<string,T>();

            IServiceBusProperties properties = this;

            foreach(Uri uri in properties.Addresses)
            {
               EndpointIdentity identity = new DnsEndpointIdentity(m_ServiceCertFindValue.ToString());
               EndpointAddress address = new EndpointAddress(uri,identity);
               ChannelFactory<T> factory = new ChannelFactory<T>(Binding,address);

               //Set credentials for message security (if needed)
               factory.Credentials.UserName.UserName = ServiceUsername;//could be null
               factory.Credentials.UserName.Password = ServicePassword;//could be null

               //Set service cert to secure message 
               ClientCredentials behavior = factory.Endpoint.Behaviors.Find<ClientCredentials>();
               behavior.ServiceCertificate.SetDefaultCertificate(m_ServiceCertLocation,m_ServiceCertStoreName,m_ServiceCertFindType,m_ServiceCertFindValue);

               //Set service bus creds
               if(properties.Credential == null)
               {
                  if(m_Secret != null)
                  {
                     factory.SetServiceBusCredentials(m_Issuer,m_Secret);
                  }
               }
               else
               {
                  Debug.Assert(m_Secret == null);
                  factory.Endpoint.Behaviors.Add(properties.Credential);
               }
               string methodName = uri.Segments[uri.Segments.Length-1];
               methodName = methodName.Replace("/","");
               m_Proxies[methodName] = factory.CreateChannel();
               ICommunicationObject proxy = m_Proxies[methodName] as ICommunicationObject;
               proxy.Open();
            }
            State = CommunicationState.Opened;

            Opened(this,EventArgs.Empty);
         }
         catch
         {
            State = CommunicationState.Faulted;
         }
      }
      public void Close()
      {
         if(State != CommunicationState.Opened)
         {
            return;
         }
         try
         {
            Closing(this,EventArgs.Empty);

            foreach(ICommunicationObject proxy in m_Proxies.Values)
            {
               proxy.Close();
            }
            State = CommunicationState.Closed;
            Closed(this,EventArgs.Empty);
         }
         catch
         {
            State = CommunicationState.Faulted;
         }
      }
      public void Abort()
      {
         try
         {
            foreach(ICommunicationObject proxy in m_Proxies.Values)
            {
               proxy.Abort();
            }
         }
         finally
         {
            State = CommunicationState.Faulted;
         }
      }

      protected T Channel
      {
         get
         {
            if(State != CommunicationState.Opened)
            {
               Open();
            }
            StackFrame frame = new StackFrame(1);
            return m_Proxies[frame.GetMethod().Name];
         }
      }
      public void SetServiceCertificate(string serviceCert)
      {
         SetServiceCertificate(serviceCert,StoreLocation.LocalMachine,StoreName.My);
      }
      public void SetServiceCertificate(string serviceCert,StoreLocation location,StoreName storeName)
      {
         if(serviceCert == String.Empty)
         {
            serviceCert = ServiceBusHelper.ExtractNamespace(new Uri(BaseAddress));
         }
         SetServiceCertificate(serviceCert,location,storeName,X509FindType.FindBySubjectName);
      }
      public void SetServiceCertificate(object findValue,StoreLocation location,StoreName storeName,X509FindType findType)
      {
         m_ServiceCertLocation = location;
         m_ServiceCertStoreName = storeName;
         m_ServiceCertFindType = findType;
         m_ServiceCertFindValue = findValue;
       }
   
      public void SetServiceBusCredentials(string secret) 
      {
         SetServiceBusCredentials(ServiceBusHelper.DefaultIssuer,secret);
      } 

      public void SetServiceBusCredentials(string issuer,string secret) 
      {
         m_Issuer = issuer;
         m_Secret = secret;
      } 

      void IDisposable.Dispose()
      {
         Close();
      }

      TransportClientEndpointBehavior IServiceBusProperties.Credential
      {
         get;set;
      }

      Uri[] IServiceBusProperties.Addresses
      {
         get
         {
            List<Uri> addresses = new List<Uri>();

            MethodInfo[] methods = typeof(T).GetMethods(BindingFlags.Public|BindingFlags.FlattenHierarchy|BindingFlags.Instance);

            foreach(MethodInfo method in methods)
            {
               addresses.Add(new Uri(BaseAddress + typeof(T) + "/" + method.Name + "/"));
            }
            return addresses.ToArray();
         }
      }
   }
}