using Consul.MasterSlaverSwitching.Entities;
using Consul.MasterSlaverSwitching.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Consul.MasterSlaverSwitching
{
    public class SwitchingProvider
    {
        private event Action BeginEvent;
        private event Action EndEvent;
        private event Action<string, LogLevel> _logEvent;
        private readonly string _serviceName;
        private readonly int _checkInsteval;
        private readonly int _changeTimes;
        private readonly string _serviceId;
        private readonly string _currentZone;
        private readonly MasterOrSlaver _masterOrSlaver;
        private readonly ConsulServiceSwitchProvider _consulServiceSwitchProvider;
        public SwitchingProvider(ConsulClient consul, Action beginEvent, Action endEvent, string serviceName, string serviceId, string currentZone, MasterOrSlaver masterOrSlaver, Action<string, LogLevel> logEvent, TimeSpan checkInsteval, int changeTimes = 3)
        {
            BeginEvent += beginEvent;
            EndEvent += endEvent;
            _logEvent = logEvent;
            _serviceName = serviceName;
            _checkInsteval = Convert.ToInt32(checkInsteval.TotalMilliseconds);
            _changeTimes = changeTimes;
            _serviceId = serviceId;
            _currentZone = currentZone;
            _masterOrSlaver = masterOrSlaver;
            _consulServiceSwitchProvider = new ConsulServiceSwitchProvider(consul);
        }



        public void Begin()
        {
            Task.Run(() =>
           {
               if (_masterOrSlaver == MasterOrSlaver.Slaver)
                   SlaverProcessOn();
               else
                   MasterProcessOn();
           });
        }

        private void MasterProcessOn()
        {
            bool masterRun = false;
            while (!masterRun)
            {
                try
                {
              
                    string currentRunning = _consulServiceSwitchProvider.GetCurrentRunningServiceId(_serviceName, _currentZone).ConfigureAwait(false).GetAwaiter().GetResult();
                    bool isMasterRunning = _consulServiceSwitchProvider.CheckTargetInstanceRunningStatus(_serviceName, _currentZone, currentRunning, MasterOrSlaver.Master ).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (isMasterRunning && currentRunning != _serviceId && !string.IsNullOrWhiteSpace(currentRunning))
                    {
                        LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] service \"{_serviceName} \" is exists and running now ,no more master instance could be set up . retry will begin later ", LogLevel.Warn);
                    }
                    else
                    {
                        TryNow:
                        if (string.IsNullOrWhiteSpace(currentRunning) || currentRunning == _serviceId)
                        {
                            if (string.IsNullOrWhiteSpace(currentRunning))
                                _consulServiceSwitchProvider.SetCurrentRunningServiceId(_serviceName, _currentZone, _serviceId).ConfigureAwait(false).GetAwaiter().GetResult();
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] master instance begin to run the start event", LogLevel.Info);
                            Run();
                            masterRun = true;
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] the start event has done ", LogLevel.Info);
                            break;
                        }
                        else
                        {
                            bool isSlaverRunning = _consulServiceSwitchProvider.CheckTargetInstanceRunningStatus(_serviceName, _currentZone, currentRunning,MasterOrSlaver.Slaver).ConfigureAwait(false).GetAwaiter().GetResult();
                            if (isSlaverRunning)
                            {
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] slaver instance is running now,retry will begin later  ", LogLevel.Info);
                            }
                            else
                            {
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] service id {currentRunning} is staling ,begin to update current running status ", LogLevel.Info);
                                _consulServiceSwitchProvider.SetCurrentRunningServiceId( _serviceName, _currentZone, _serviceId).ConfigureAwait(false).GetAwaiter().GetResult();
                                currentRunning = _serviceId;
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] current running status updated complete, retry now ", LogLevel.Debug);
                                goto TryNow;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMsg(ex.ToString(), LogLevel.Err);
                }
                Thread.Sleep(_checkInsteval);
            }
        }
        private bool _currentMasterisMasterDown = false;//当前主节点状态是否挂掉 默认false 没挂
        private int _currentFailedTimes = 0;//检查到失败次数 
        private void SlaverProcessOn()
        {
            while (true)
            {
                try
                {
                    CurrentServiceStatus masterServiceStatus = _consulServiceSwitchProvider.GetCurrentServiceStatus(_serviceName, _currentZone, MasterOrSlaver.Master).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (masterServiceStatus != CurrentServiceStatus.Running)
                    {
                        if (!_currentMasterisMasterDown)
                        {
                            if (masterServiceStatus == CurrentServiceStatus.Unsetting)
                            {
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] a master instance has not been set up firstly, current slaver instance begins to run now", LogLevel.Warn);
                                _currentFailedTimes = _changeTimes;
                            }
                            _currentFailedTimes = _currentFailedTimes + 1;
                            if (_currentFailedTimes <= _changeTimes)
                            {
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] the master instance is in critical status,slaver tries {_currentFailedTimes} times ,after {_changeTimes} times ,slaver instance will start", LogLevel.Info);
                                Thread.Sleep(_checkInsteval);
                                continue;
                            }
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] begin to update current running status", LogLevel.Debug);
                            _consulServiceSwitchProvider.SetCurrentRunningServiceId(_serviceName, _currentZone, _serviceId).ConfigureAwait(false).GetAwaiter().GetResult();
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] current running status updated complete", LogLevel.Debug);
                            _currentFailedTimes = 0;
                            _currentMasterisMasterDown = true;//master down
                            Task.Run(() =>
                            {
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] slaver instance begin to run the start event", LogLevel.Info);
                                Run();
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] the start event has done", LogLevel.Info);
                            });
                        }
                        else
                        {
                            if (masterServiceStatus == CurrentServiceStatus.Unsetting)
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] master instance is still unseting now", LogLevel.Warn);
                            if (masterServiceStatus == CurrentServiceStatus.Critical)
                                LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] master instance is still in critical status", LogLevel.Trace);
                        }
                    }
                    else
                    {
                        if (_currentMasterisMasterDown)
                        {
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] master instance alives again ,begin to update current running status", LogLevel.Info);
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] begin to update current running status", LogLevel.Debug);
                            string currentRunningId = _consulServiceSwitchProvider.GetCurrentRunningServiceId(_serviceName, _serviceId).ConfigureAwait(false).GetAwaiter().GetResult();
                            if (currentRunningId == _serviceId)
                                _consulServiceSwitchProvider.SetCurrentRunningServiceId(_serviceName, _currentZone, "").ConfigureAwait(false).GetAwaiter().GetResult();
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] current running status updated complete", LogLevel.Debug);
                            _currentMasterisMasterDown = false;
                            _currentFailedTimes = 0;
                            End();
                        }
                        else
                        {
                            LogMsg($"[ {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] master instance is still in passing status", LogLevel.Trace);
                            _currentFailedTimes = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMsg(ex.ToString(), LogLevel.Warn);
                    _currentFailedTimes = 0;
                    //_currentMasterisMasterDown = false;
                }
                Thread.Sleep(_checkInsteval);
            }
        }
        private void LogMsg(string msg, LogLevel level) => _logEvent?.Invoke(msg, level);
        private void Run() => BeginEvent?.Invoke();
        private void End() => EndEvent?.Invoke();
    }
}
