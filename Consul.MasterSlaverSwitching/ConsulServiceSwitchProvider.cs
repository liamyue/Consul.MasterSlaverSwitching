using Consul.MasterSlaverSwitching.Entities;
using Consul.MasterSlaverSwitching.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Consul;
using System.Net;
using System.Text;

namespace Consul.MasterSlaverSwitching
{
    public class ConsulServiceSwitchProvider
    {
        private readonly ConsulClient _consul;
        public ConsulServiceSwitchProvider(ConsulClient consul)
        {
            _consul = consul;
        } 
        /// <summary>
        /// 获取指定服务状态
        /// </summary>
        /// <param name="serviceName">服务名</param>
        /// <param name="currentZone">分片键</param>
        /// <param name="masterOrSlaver">主从身份</param>
        /// <returns></returns>
        public async Task<CurrentServiceStatus> GetCurrentServiceStatus(string serviceName, string currentZone, MasterOrSlaver masterOrSlaver)
        {
            var queryResult = await _consul.Health.Service(serviceName, currentZone, false);
            var tarService = queryResult.Response.Where(serviceEntry => serviceEntry.Service != null && serviceEntry.Service.Tags.Contains(masterOrSlaver.ToString())).FirstOrDefault();
            CurrentServiceStatus status = CurrentServiceStatus.Unsetting;
            if (tarService != null && tarService.Checks.Any(serviceCheck => !string.IsNullOrWhiteSpace(serviceCheck.ServiceID)))
            {
                var serviceStatus = tarService.Checks.Where(serviceCheck => !string.IsNullOrWhiteSpace(serviceCheck.ServiceID)).FirstOrDefault();
                if (serviceStatus != null)
                    status = serviceStatus.Status == HealthStatus.Passing ? CurrentServiceStatus.Running : CurrentServiceStatus.Critical;
            }
            return status;
        } 
        public async Task<bool> CheckTargetInstanceRunningStatus(string serviceName, string currentZone, string serviceId,MasterOrSlaver masterOrSlaver)
        {
            var queryResult = await _consul.Health.Service(serviceName, currentZone, false);
            var tarService = queryResult.Response.Where(serviceEntry => serviceEntry.Service != null && serviceEntry.Service.ID == serviceId && serviceEntry.Service.Tags.Contains(masterOrSlaver.ToString())).FirstOrDefault(); 
            return tarService?.Checks.Where(serviceCheck => !string.IsNullOrWhiteSpace(serviceCheck.ServiceID)).FirstOrDefault()?.Status == HealthStatus.Passing ? true : false;
        } 
        public async Task<string> GetCurrentRunningServiceId(string serviceName, string currentZone)
        {
            string key = serviceName + "/" + currentZone;
            var queryResult = await _consul.KV.Get(key);

            var res = queryResult.Response?.Value;
            if(res!=null)
                return Encoding.UTF8.GetString(queryResult.Response.Value); 
            return string.Empty;
        }
        public async Task SetCurrentRunningServiceId(string serviceName, string currentZone, string serviceId)
        {
            string key = serviceName + "/" + currentZone;
            KVPair kv = new KVPair(key);
            kv.Value = Encoding.UTF8.GetBytes(serviceId);
            var queryResult = await _consul.KV.Put(kv);
            if (queryResult.StatusCode != HttpStatusCode.OK)
                throw new CurrentRunningException("update current running info failed");
        }
    }  
}
