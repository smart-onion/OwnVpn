using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class MyEndPoint : IPEndPoint
    {
        public readonly PhysicalAddress PhysicalAddress;
        public MyEndPoint(string mac, long address, int port) : base(address, port)
        {
            PhysicalAddress = PhysicalAddress.Parse(mac);
        }
        public MyEndPoint(string mac, IPAddress address, int port) : base(address, port)
        {
            PhysicalAddress = PhysicalAddress.Parse(mac);
        }
        
    }
}
