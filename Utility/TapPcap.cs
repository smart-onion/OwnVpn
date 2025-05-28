using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utility
{
    public class TapPcap : NetAdapter
    {
        private readonly ILogger<TapAdapter> _logger;
        private readonly IConfigurationRoot _settings;
        private readonly LibPcapLiveDevice _tapDevice;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        public TapPcap(ILogger<TapAdapter> logger, IConfigurationRoot settings)
        {
            _logger = logger;
            _settings = settings;
            _tapDevice = FindTapDevice();
            if (_tapDevice != null)
            {
                // Open for capture and injection
                _tapDevice.Open(DeviceModes.Promiscuous, 1000);
                _logger.LogInformation($"Opened TAP adapter: {_tapDevice.Description}");
            }
            Task.Run(async () => await StartAsync());
        }
        public async Task StartAsync()
        {
            if (_tapDevice == null)
            {
                _logger.LogError("No TAP adapter available");
                return;
            }

            try
            {
                // Start capture and injection tasks
                await Task.WhenAll(
                    StartCaptureAsync(),
                    ProcessWriteQueueAsync()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Processing error: {ex.Message}");
            }
        }
        public override byte[]? DequeuePacket()
        {
            if(_readQueue.TryDequeue(out byte[]? packet) && packet != null)
            {
                return packet;
            }
            return null;
        }
        public override bool EnqueuePacket(byte[] data)
        {
            _writeQueue.Enqueue(data);
            return true;
        }
        private async Task StartCaptureAsync()
        {
            try
            {
                _tapDevice.OnPacketArrival += (sender, e) =>
                {
                    var rawPacket = e.Data.ToArray();
                    var packet = new EthPacket(rawPacket);
                    if (packet != null)
                    {

                        // Check for loop (injected packet read back)
//                        if (packet.PhysicalAddress.Equals(PhysicalAddress))
//                        {
//#if DEBUG
//                            _logger.LogDebug($"Dropped loop packet: Src MAC={packet.PhysicalAddress}, Dst MAC={PhysicalAddress}, Length={rawPacket.Length}");
//#endif
//                            return;
//                        }
#if DEBUG
                        _logger.LogDebug($"Captured: Src MAC={packet.PhysicalAddress}, Dst MAC={PhysicalAddress}, Type={packet.Version}, Length={rawPacket.Length}");
#endif
                        _readQueue.Enqueue(rawPacket);
                    }
                };

                _tapDevice.StartCapture();
                _logger.LogInformation("Capture started");

                // Wait for cancellation
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error on StartCaptureAsync {ex.Message}");
            }
        }
        private async Task ProcessWriteQueueAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    
                    try
                    {
                        if(_writeQueue.TryDequeue(out byte[]? packet) && packet != null)
                        {
                                _tapDevice.SendPacket(packet);
#if DEBUG
                                _logger.LogDebug($"Injected packet of {packet} bytes");
#endif
                        }
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        _logger.LogDebug($"Injection error: {ex.Message}");
#endif
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Injection stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Queue processing error: {ex.Message}");
            }
        }
        private LibPcapLiveDevice FindTapDevice()
        {
            var devices = LibPcapLiveDeviceList.Instance;
            foreach (var device in devices)
            {
                if (device.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Found TAP adapter: {device.Description}");
                    PhysicalAddress = device.MacAddress;
                    return device;
                }
            }
            _logger.LogError("TAP adapter not found");
            return null;
        }

        public override Task<bool> EnqueuePacketAsync(byte[] data)
        {
            throw new NotImplementedException();
        }

        public override Task<byte[]?> DequeuePacketAsync()
        {
            throw new NotImplementedException();
        }
    }
}
