using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EgoUdpClient.Protocol
{
    internal class Packet
    {
        public Header Header { get; set; }
        public Request Request { get; set; }
    }
}
