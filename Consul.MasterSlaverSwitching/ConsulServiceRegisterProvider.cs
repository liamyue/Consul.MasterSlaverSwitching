using Consul.MasterSlaverSwitching.Exceptions;
using Consul.MasterSlaverSwitching.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Consul.MasterSlaverSwitching
{
    public class ConsulServiceRegisterProvider
    {
        private readonly ConsulClient _consul;
        public ConsulServiceRegisterProvider(ConsulClient consul, Action<Exception> selfRegisterServiceExceptionEvent)
        {
            _consul = consul;
            SelfRegisterServiceExceptionEvent += selfRegisterServiceExceptionEvent;
        }
        private event Action<Exception> SelfRegisterServiceExceptionEvent;
        public async Task<string> SelfRegisterService(string serviceName, TimeSpan ttl, string[] tags, string address = "", int port = 0)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                var nodeSelfQuery = await _consul.Agent.Self();
                address = nodeSelfQuery.Response["Member"]["Addr"];
            }
            string serviceId = (address + AppContext.BaseDirectory).CreateMd5();
            return await RegisterService(serviceName, address, port, serviceId, ttl, tags);
        }

        private async Task<string> RegisterService(string serviceName, string address, int port, string serviceId, TimeSpan ttl, string[] tags)
        {
            var deregisterResult = await _consul.Agent.ServiceDeregister(serviceId);
            if (deregisterResult.StatusCode != HttpStatusCode.OK)
                throw new ConsulRegisterException("service deregistered failed before register");
            var registerServiceResult = await _consul.Agent.ServiceRegister(new AgentServiceRegistration
            {
                Address = address,
                ID = serviceId,
                Port = port,
                Name = serviceName,
                Tags = tags,
                Check = new AgentServiceCheck
                {
                    TTL = ttl,
                    Status = HealthStatus.Critical
                }
            });
            RunHealthCheck("service:" + serviceId, ttl);
            return serviceId;
        }
        private void RunHealthCheck(string checkId, TimeSpan ttl)
        {
            int checkInterval = Convert.ToInt32(ttl.TotalMilliseconds);
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        _consul.Agent.PassTTL(checkId, "service is ok" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        SelfRegisterServiceExceptionEvent?.Invoke(ex);
                    }
                    Thread.Sleep(checkInterval);
                }
            });
        }
    }
}
