using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using SharpPcap;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Utility
{
    public class EthPacket
    {
        public IPAddress Source { get; set; }
        public IPAddress Destination { get; set; }
        public IPVersion Version { get; set; }
        public PhysicalAddress PhysicalAddress { get; set; }

        public readonly byte[] Data;

        public EthPacket(byte[] rawData)
        {
            this.Data = rawData;
            ParseRawPacket(Data);
        }

        public static PhysicalAddress GetSourceMAC(byte[] data)
        {
            var packet = PacketDotNet.Packet.ParsePacket(LinkLayers.Ethernet, data);

            var ethPacket = packet.Extract<PacketDotNet.EthernetPacket>();
            return ethPacket.SourceHardwareAddress;
        }

        private void ParseRawPacket(byte[] rawData)
        {
            var packet = PacketDotNet.Packet.ParsePacket(LinkLayers.Ethernet, Data);

            var ethPacket = packet.Extract<PacketDotNet.EthernetPacket>();
            PhysicalAddress = ethPacket.SourceHardwareAddress;

            var ipPacket = packet.Extract<PacketDotNet.IPv4Packet>();
            if(ipPacket != null)
            {
                Source = ipPacket.SourceAddress;
                Destination = ipPacket.DestinationAddress;
                Version = ipPacket.Version;
            }
            
            var ipV6Packet = packet.Extract<IPv6Packet>();
            if (ipV6Packet != null)
            {
                Source = ipV6Packet.SourceAddress;
                Destination = ipV6Packet.DestinationAddress;
                Version = ipV6Packet.Version;
            }
        }
    }
}
