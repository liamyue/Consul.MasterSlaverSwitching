using System;
using System.Collections.Generic;
using System.Text;

namespace Consul.MasterSlaverSwitching.Exceptions
{
    public class CurrentRunningException : Exception
    {
        public CurrentRunningException(string message) : base(message) { }
    }
}
