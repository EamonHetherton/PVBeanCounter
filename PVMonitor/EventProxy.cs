﻿//2006 IDesign Inc. 
//Questions? Comments? go to 
//http://www.idesign.net

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

[ServiceContract]
public interface ISubscriptionService
{
    [OperationContract]
    void Subscribe(string eventOperation);

    [OperationContract]
    void Unsubscribe(string eventOperation);
}

[ServiceContract(CallbackContract = typeof(IEnergyEvents))]
public interface IMySubscriptionService : ISubscriptionService
{
}

partial class MySubscriptionServiceProxy : DuplexClientBase<IMySubscriptionService>, IMySubscriptionService
{
    public MySubscriptionServiceProxy(InstanceContext inputInstance)
        : base(inputInstance)
    { }

    public MySubscriptionServiceProxy(InstanceContext inputInstance, string endpointConfigurationName)
        : base(inputInstance, endpointConfigurationName)
    { }

    public MySubscriptionServiceProxy(InstanceContext inputInstance, string endpointConfigurationName, string remoteAddress)
        : base(inputInstance, endpointConfigurationName, remoteAddress)
    { }

    public MySubscriptionServiceProxy(InstanceContext inputInstance, string endpointConfigurationName, EndpointAddress remoteAddress)
        : base(inputInstance, endpointConfigurationName, remoteAddress)
    { }

    public MySubscriptionServiceProxy(InstanceContext inputInstance, Binding binding, EndpointAddress remoteAddress)
        : base(inputInstance, binding, remoteAddress)
    { }

    public void Subscribe(string eventOperation)
    {
        Channel.Subscribe(eventOperation);
    }
    public void Unsubscribe(string eventOperation)
    {
        Channel.Unsubscribe(eventOperation);
    }
}
