// © 2010 IDesign Inc. All rights reserved 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
using System.Diagnostics;
using ServiceModelEx;


public abstract class WcfWrapper<S,I> : IDisposable where I : class 
                                                    where S : class,I
{
   protected I Proxy
   {get;private set;}

   protected WcfWrapper()
   {
      Proxy = InProcFactory.CreateInstance<S,I>();
   }
   
   protected WcfWrapper(S singleton)
   {
      InProcFactory.SetSingleton(singleton);
      Proxy = InProcFactory.CreateInstance<S,I>();
   }
   public void Dispose()
   {
      Close();
   }

   public void Close()
   {
      InProcFactory.CloseProxy(Proxy);
   }
}