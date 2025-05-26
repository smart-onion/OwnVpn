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

        private static TapAdapter? instance = null;
        public static TapAdapter? Adapter { get { return instance; } }
        public const int BufferSize = 1500;

        static object obj = new object();

        private TapAdapter()
        {
          
        }

        public static TapAdapter Init()
        {
            if (instance != null) return instance;
            else instance = new TapAdapter();

            var guid = GetTAPGuid();

            if (guid == null) throw new ArgumentNullException("TAP adapter not found!");

            tap = OpenTapDevice(guid);
            if (tap != IntPtr.Zero)
            {
                var result = SetTapMediaStatus(tap, true);

                if (!result) throw new Exception("Failed connection to TAP adapter");
            }
            return instance;
        }

        public async Task<bool> WriteAsync(byte[] data, int len)
        {
            try
            {
                var safeHandle = new SafeFileHandle(tap, ownsHandle: false);
                using (var fs = new FileStream(safeHandle, FileAccess.Write))
                {
                    await fs.WriteAsync(data, 0, len);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception on TapAdapter.WriteAsync {ex.Message}");
                return false;
            }
        }

        public async Task<byte[]> ReadAsync()
        {
            try
            {
                var safeHandle = new SafeFileHandle(tap, ownsHandle: false);
                byte[] buffer = new byte[TapAdapter.BufferSize];
                using (var fs = new FileStream(safeHandle, FileAccess.Read))
                {
                    int bytes = await fs.ReadAsync(buffer, 0, buffer.Length);
                    Console.WriteLine($"{bytes} readed");
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
                Console.WriteLine($"Exception on TapAdapter.ReadAsync {ex.Message}");
                return new byte[0];
            }
}

        public bool Write(byte[] data, uint len)
        {
            var result = false;
            lock (obj)
            {
                result = WriteToTap(tap, data, len);
            }
            return result;
        } 

        public int Read(ref byte[] buffer)
        {
            var result = 0;
            lock (obj)
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
