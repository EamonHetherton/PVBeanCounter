// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
using System.Security;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using System.Web.Security;


namespace ServiceModelEx
{
   class ServiceHostActivator : MarshalByRefObject
   {
      ServiceHost m_Host;

      //Go with infinite lease
      public override object InitializeLifetimeService()
      {
         return null;
      }

      public string MembershipApplicationName
      {
         set
         {
            Membership.ApplicationName = value;
         }
      }
      public string RolesApplicationName
      {
         set
         {
            Roles.ApplicationName = value;
         }
      }

      public void CreateHost(Type serviceType,Uri[] baseAddresses)
      {
         //CodeAccessSecurityHelper.PermissionSetFromStandardSet(StandardPermissionSet.FullTrust).Assert();
         m_Host = new ServiceHost(serviceType,baseAddresses);
         //PermissionSet.RevertAssert();
         //m_Host.DemandHostPermissions();
      }
      [PermissionSet(SecurityAction.Assert,Unrestricted = true)]
      public void Open()
      {
         m_Host.Open();
      }   
      public void Close()
      {
         m_Host.Close();
      }
      public void Abort()
      {
         m_Host.Abort();
      }
   }
}