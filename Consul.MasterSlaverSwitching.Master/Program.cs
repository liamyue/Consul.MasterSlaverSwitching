using Consul.MasterSlaverSwitching.Entities;
using System;
using System.Threading;

namespace Consul.MasterSlaverSwitching.Master
{
    class Program
    {
        static void Main(string[] args)
        {
            string serviceName = "Consul.MasterSlaverSwitching.Demo";
            var consul = new ConsulClient();
            string serviceZone = "yu";
            ConsulServiceRegisterProvider pro = new ConsulServiceRegisterProvider(consul, Pro_SelfRegisterServiceExceptionEvent);
            var serviceId = pro.SelfRegisterService(serviceName, new TimeSpan(0, 0, 0, 10), new string[] { MasterOrSlaver.Master.ToString(), serviceZone }).ConfigureAwait(false).GetAwaiter().GetResult();
            MainEntry demo = new MainEntry();
            SwitchingProvider sp = new SwitchingProvider(consul, demo.Run, demo.Stop, serviceName, serviceId, serviceZone, MasterOrSlaver.Master, LogMsg, new TimeSpan(0, 0, 10), 3);
            sp.Begin();


            Console.ReadKey();
        }

        private static void LogMsg(string msg, LogLevel level)
        {
            Console.WriteLine(msg);
        }

        private static void Pro_SelfRegisterServiceExceptionEvent(Exception obj)
        {
            throw new NotImplementedException();
        }
    }
    public class MainEntry
    {
        volatile bool isRun = true;//控制实例的标识 如RabbimtMQ _reciever.Start();  _reciever.Stop();
        /// <summary>
        /// 应用程序启动时把该做的做了
        /// </summary>
        public void Run()
        {
            Console.WriteLine("服务启动Run");
            while (isRun)
            {
                Console.WriteLine("Runing now!");
                Thread.Sleep(3000);
            }
        }
        /// <summary>
        /// 该关的全部关闭
        /// </summary>
        public void Stop()
        {
            isRun = false;
            Console.WriteLine("Stop");
        }
    }
}