using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EgoUdpClient.Protocol
{
    public enum StatusCode
    {
        OK = 0,
        Error = 1
    }

    public enum Events
    {
        None = 0,
        Connected = 1,
        Disconnected = 2
    }

    public enum Methods
    {
        None = 0,
        Get = 1,
        Set = 2
    }
}
