using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Utility
{
    public class TapAdapter
    {
        [DllImport("Tap.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OpenTapDevice(string guid);

        [DllImport("Tap.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetTapMediaStatus(IntPtr tap, bool connected);

        [DllImport("Tap.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool WriteToTap(IntPtr tap, byte[] data, uint len);

        [DllImport("Tap.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ReadFromTap(IntPtr tap, byte[] buffer, uint bufSize);

        private static IntPtr tap;
        public const int BufferSize = 1500;
        static object locker = new object();
        static TapAdapter? instance = null;
        private readonly ILogger<TapAdapter> _logger;
        private readonly IConfigurationRoot _settings;
        private static readonly SemaphoreSlim _readLock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly Channel<(byte[] data, int len)> _readQueue = Channel.CreateUnbounded<(byte[], int)>();
        private readonly Channel<(byte[] data, int len)> _writeQueue = Channel.CreateUnbounded<(byte[], int)>();
        private FileStream _tapStream;
        public PhysicalAddress PhysicalAddress { get; private set; }

        public TapAdapter(ILogger<TapAdapter> logger, IConfigurationRoot settings) 
        { 
            if(instance == null)
            {
                _logger = logger;
                _settings = settings;
                Init();
                SetTAPMacAddress();
                instance = this;
            }
            else
            {
                throw new InvalidOperationException("One instance of TAP-adapter is allowed");
            }
        }
        public static TapAdapter? GetAdapter() { return instance; }
        private void Init()
        {
            var guid = GetTAPGuid();

            if (guid == null) throw new ArgumentNullException("TAP adapter not found!");

            tap = OpenTapDevice(guid);
            if (tap != IntPtr.Zero)
            {
                var result = SetTapMediaStatus(tap, true);

                if (!result) throw new Exception("Failed connection to TAP adapter");
                _logger.LogInformation("TAP adapter connected");
                var safeHandle = new SafeFileHandle(tap, ownsHandle: false);

                _tapStream = new FileStream(safeHandle, FileAccess.ReadWrite, bufferSize: 4096, isAsync: true);
                Task.Run(async () => EnqueueReadAsync());
                Task.Run(async () => DequeueWriteAsync());
            }
        }
        private async Task DequeueWriteAsync()
        {

            while (true)
            {
                await Task.Delay(100);
#if DEBUG
                _logger.LogWarning($"BEFORE SEM LOCK WRITE");
#endif
                await _writeLock.WaitAsync().ConfigureAwait(false);
#if DEBUG
                _logger.LogWarning($"AFTER SEM LOCK WRITE");
#endif
                try
                {
                    //if (_writeQueue.TryDequeue(out var packet))
                    //{
                    //var packet = await _writeQueue.Reader.ReadAsync();
                    //await _tapStream.WriteAsync(packet.data, 0, packet.len).ConfigureAwait(false);

                    _writeQueue.Reader.TryRead(out (byte[] data, int len) packet);
                    _tapStream.Write(packet.data, 0, packet.len);//.ConfigureAwait(false);
#if DEBUG
                    _logger.LogInformation($"{packet.len} Dequeue to TAP adapter");
#endif
                    //}
                    //else
                    //{
                        _logger.LogError("Failed to dequeue on WriteAsync");
                    //}
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Exception on TapAdapter.WriteAsync {ex.Message}");
                }
                finally
                {
                    _writeLock.Release();
                }
            }
        }
        public async Task<bool> EnqueueWrite(byte[] data, int len)
        {
            _writeQueue.Writer.TryWrite((data, len));
            return true;
        }
        private async Task EnqueueReadAsync()
        {
            while (true)
            {
                await Task.Delay(100);
                // Enqueue packet instead of writing directly
                await _readLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    byte[] buffer = new byte[TapAdapter.BufferSize];

                    //int bytes = await _tapStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    int bytes = _tapStream.Read(buffer, 0, buffer.Length);//.ConfigureAwait(false);
                    var result = new byte[bytes];
                    Array.Copy(buffer, result, bytes);
                    //await _readQueue.Writer.WriteAsync((result, bytes));
                    _readQueue.Writer.TryWrite((result, bytes));
#if DEBUG
                    _logger.LogInformation($"{bytes} Enqueue from TAP adapter");
#endif
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Exception on TapAdapter.ReadAsync {ex.Message}");
                }
                finally
                {
                    _readLock.Release();
                }
            }
        }
        public async Task<(byte[] data, int len)?> DequeueRead()
        {
            //if(_readQueue.TryDequeue(out var packet))
            //{
            _readQueue.Reader.TryRead(out (byte[] data, int len) packet) ;
            return packet;
            //}
            //else
            //{
#if DEBUG
                //_logger.LogWarning($"Failed to dequeue on read {_readQueue.Count}");
#endif
            //}
            return null;
        }
        public async Task<bool> WriteAsync(byte[] data, int len)
        {
#if DEBUG
            _logger.LogWarning($"BEFORE SEM LOCK WRITE");
#endif
            await _writeLock.WaitAsync();
#if DEBUG
            _logger.LogWarning($"AFTER SEM LOCK WRITE");
#endif
            try
            {
                //if (_packetQueue.TryDequeue(out var packet))
                //{
                    await _tapStream.WriteAsync(data, 0, len).ConfigureAwait(false);
#if DEBUG
                    _logger.LogInformation($"{len} write to TAP adapter");
#endif
                    return true;
                //}
                //else
                //{
                    //_logger.LogError("Failed to dequeue on WriteAsync");
                    //return false;
                //}
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Exception on TapAdapter.WriteAsync {ex.Message}");
                return false;
            }
            finally {
                _writeLock.Release();
                      }
        }

        public async Task<byte[]> ReadAsync()
        {
#if DEBUG
            _logger.LogWarning($"BEFORE SEM LOCK READ");
#endif
            await _readLock.WaitAsync();
#if DEBUG
            _logger.LogWarning($"AFTER SEM LOCK READ");
#endif
            try
            {
                byte[] buffer = new byte[TapAdapter.BufferSize];

                int bytes = await _tapStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
#if DEBUG
                _logger.LogInformation($"{bytes} read from TAP adapter");
#endif
                var result = new byte[bytes];
                Array.Copy(buffer, result, bytes);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Exception on TapAdapter.ReadAsync {ex.Message}");
                return new byte[0];
            }
            finally {
                _readLock.Release();
                      }
}
        
        [Obsolete("Use WriteAsync instead.")]
        public bool Write(byte[] data, uint len)
        {
            var result = false;
            lock (locker)
            {
                result = WriteToTap(tap, data, len);
            }
            return result;
        }
        
        [Obsolete("Use ReadAsync instead.")]
        public int Read(ref byte[] buffer)
        {
            var result = 0;
            lock (locker)
            {
                result = ReadFromTap(tap, buffer, (uint)buffer.Length);
            }
            return result;
        }

        private static string? GetTAPGuid()
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                string name = adapter.Name;
                string description = adapter.Description;

                if (description.Contains("TAP") || name.Contains("TAP"))
                {
                    string id = adapter.Id; // This is the TAP GUID
                    return id;
                }
            }
            return null;
        }

        private void SetTAPMacAddress()
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                string name = adapter.Name;
                string description = adapter.Description;

                if (description.Contains("TAP", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("TAP", StringComparison.OrdinalIgnoreCase))
                {
                    PhysicalAddress = adapter.GetPhysicalAddress();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _tapStream?.Dispose();
                if (tap != IntPtr.Zero)
                {
                    SetTapMediaStatus(tap, false); // Disconnect TAP adapter
                                                   // Note: Tap.dll may require a CloseTapDevice function; add if available
                }
                _readLock.Dispose();
                _writeLock.Dispose();
                instance = null;
                _logger.LogInformation("TapAdapter disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Exception on TapAdapter.Dispose: {ex.Message}");
            }
        }
    }

}
