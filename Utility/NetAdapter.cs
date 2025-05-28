using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Utility
{
    public abstract class NetAdapter
    {
        public PhysicalAddress PhysicalAddress { get; protected set; }
        protected readonly ConcurrentQueue<byte[]> _readQueue = new();
        protected readonly ConcurrentQueue<byte[]> _writeQueue = new();

        public abstract Task<bool> EnqueuePacketAsync(byte[] data);
        public abstract Task<byte[]?> DequeuePacketAsync();
        public abstract bool EnqueuePacket(byte[] data);
        public abstract byte[]? DequeuePacket();

    }
}
