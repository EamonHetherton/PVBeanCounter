using System;
using System.Security.Cryptography.X509Certificates;

namespace ServiceModelEx.ServiceBus
{
    public abstract class ClientBufferResponseBase<T> : BufferedServiceBusClient<T> where T : class
    {
        public readonly Uri ResponseAddress;

        public ClientBufferResponseBase(string secret,Uri responseAddress) : base(secret)
        {
           ResponseAddress = responseAddress;
        }
        public ClientBufferResponseBase(string endpointName,string secret,Uri responseAddress) : base(endpointName,secret)
        {
           ResponseAddress = responseAddress;
        }
        public ClientBufferResponseBase(string endpointName,string issuer,string secret,Uri responseAddress) : base(endpointName,issuer,secret)
        {
           ResponseAddress = responseAddress;
        }
        public ClientBufferResponseBase(Uri serviceAddress,string secret,Uri responseAddress) : base(serviceAddress,secret)
        {
           ResponseAddress = responseAddress;
        }        
        public ClientBufferResponseBase(Uri serviceAddress,string issuer,string secret,Uri responseAddress) : base(serviceAddress,issuer,secret)
        {
           ResponseAddress = responseAddress;
        }

        protected override string Enqueue(Action action)
        {
            string methodId = GenerateMethodId();
            Header = new ResponseContext(ResponseAddress.AbsoluteUri,methodId);
            base.Enqueue(action);
            return Header.MethodId;
        }
        protected virtual string GenerateMethodId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}

