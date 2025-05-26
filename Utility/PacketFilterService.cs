using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Utility
{
    public class PacketFilterService
    {
        private List<PhysicalAddress> restrictedPhysicalAddresses = new();
        private List<IPAddress> restrictedIpAddresses = new();

        public PacketFilterService() { }
        public bool IsRestricted(PhysicalAddress address) { return restrictedPhysicalAddresses.Contains(address); }
        public bool IsRestricted(IPAddress address) { return restrictedIpAddresses.Contains(address); }
        public void AddRestriction(IPAddress address) { restrictedIpAddresses.Add(address); }
        public void AddRestriction(PhysicalAddress address) {  restrictedPhysicalAddresses.Add(address); }
        public void RemoveRestriction(PhysicalAddress address) { restrictedPhysicalAddresses.Remove(address); }
        public void RemoveRestriction(IPAddress address) { restrictedIpAddresses.Remove(address); }
    }
}
