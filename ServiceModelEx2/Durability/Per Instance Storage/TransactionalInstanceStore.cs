// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net


using System;
using System.IO;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using ServiceModelEx.Transactional;

namespace ServiceModelEx
{
   public class TransactionalInstanceStore<ID,T> : IInstanceStore<ID,T> where ID : IEquatable<ID> where T : class
   {
      Transactional<T> m_Instance = new Transactional<T>();

      static TransactionalInstanceStore()
      {
         //Verify [Serializable] on T
         Debug.Assert(typeof(T).IsSerializable);
      }

      public void RemoveInstance(ID instanceId)
      {
         this[instanceId] = null;
      }
      public bool ContainsInstance(ID instanceId)
      {
         return this[instanceId] != null;

      }
      public T this[ID instanceId]
      {
         get
         {
            lock(m_Instance)
            {
               return m_Instance.Value;
            }
         }
         set
         {
            lock(m_Instance)
            {
               m_Instance.Value = value;
            }
         }
      }
   }
}