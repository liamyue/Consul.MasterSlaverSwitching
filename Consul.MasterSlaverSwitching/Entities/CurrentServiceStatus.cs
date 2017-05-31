using System;
using System.Collections.Generic;
using System.Text;

namespace Consul.MasterSlaverSwitching.Entities
{
    public enum CurrentServiceStatus
    {
        Running,
        Critical,
        Unsetting
    }
}
