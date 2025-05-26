using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class EthPacketManager
    {
        List<IPAddress> ipAddresses = new List<IPAddress>();

        public void AddSource(string ip)
        {
            IPAddress.TryParse(ip, out IPAddress? iPAddress);
            if (iPAddress != null) { ipAddresses.Add(iPAddress); }
            else Console.WriteLine("Failed to parse IP address");
        }

        public void RemoveSource(string ip)
        {
            IPAddress.TryParse(ip, out IPAddress? iPAddress);
            if (iPAddress != null) { ipAddresses.Remove(iPAddress); }
            else Console.WriteLine("Failed to parse IP address");
        }

        public bool IsInList(byte[] packet)
        {
            var ip = new EthPacket(packet);
            var source = ipAddresses.FirstOrDefault(i => i == ip.Source);
            return source != null;
        }
    }
}
