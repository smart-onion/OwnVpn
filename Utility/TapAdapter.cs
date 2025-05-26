using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
        private readonly IConfigurationRoot _configuration;
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        public TapAdapter(ILogger<TapAdapter> logger, IConfigurationRoot configuration) 
        { 
            if(instance == null)
            {
                _logger = logger;
                _configuration = configuration;
                Init();
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
            }
        }

        public async Task<bool> WriteAsync(byte[] data, int len)
        {
            await _fileLock.WaitAsync();
            try
            {
                var safeHandle = new SafeFileHandle(tap, ownsHandle: false);
                using (var fs = new FileStream(safeHandle, FileAccess.Write))
                {
                    await fs.WriteAsync(data, 0, len);
#if DEBUG
                    _logger.LogInformation($"{len} write to TAP adapter");
#endif
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Exception on TapAdapter.WriteAsync {ex.Message}");
                return false;
            }
            finally { _fileLock.Release(); }
        }

        public async Task<byte[]> ReadAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var safeHandle = new SafeFileHandle(tap, ownsHandle: false);
                byte[] buffer = new byte[TapAdapter.BufferSize];
                using (var fs = new FileStream(safeHandle, FileAccess.Read))
                {
                    int bytes = await fs.ReadAsync(buffer, 0, buffer.Length);
#if DEBUG
                    _logger.LogInformation($"{bytes} read from TAP adapter");
#endif
                    var result = new byte[bytes];
                    
                    for (int i = 0;i < result.Length; i++)
                    {
                        result[i] = buffer[i];
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Exception on TapAdapter.ReadAsync {ex.Message}");
                return new byte[0];
            }
            finally { _fileLock.Release(); }
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
    }

}
