using System;
using System.Collections.Generic;
using System.Text;

 
namespace Consul.MasterSlaverSwitching.Exceptions
{
    public class ConsulRegisterException : Exception
    {
        public ConsulRegisterException(string message) : base(message) { }
    }
}