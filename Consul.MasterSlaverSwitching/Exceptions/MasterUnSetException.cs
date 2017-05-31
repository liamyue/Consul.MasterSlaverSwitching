using System;
using System.Collections.Generic;
using System.Text;

namespace Consul.MasterSlaverSwitching.Exceptions
{
    public class MasterUnSetException: Exception
    {
        public MasterUnSetException(string message) : base(message) { }
    }
}
