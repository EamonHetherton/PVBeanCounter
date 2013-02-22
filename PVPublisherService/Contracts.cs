//2006 IDesign Inc. 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
// using Service;
using ServiceModelEx;

[ServiceContract(CallbackContract = typeof(IEnergyEvents))]
interface IMySubscriptionService : ISubscriptionService
{}