using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using Utility;
using System.Text;
using System.Net.NetworkInformation;
using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;

public class VpnServer
{
    private List<IPEndPoint> clients = new List<IPEndPoint>();
    // dependencies 
    private readonly int _localPort;
    private readonly ILogger<VpnServer> _logger;
    private readonly IConfigurationRoot _settings;
    private readonly PacketFilterService _packetFilterService;
    private readonly NetAdapter _tapAdapter;

    public VpnServer(
        ILogger<VpnServer> logger,
        IConfigurationRoot settings, 
        PacketFilterService packetFilterService,
        NetAdapter tapAdapter
        )
    {
        _logger = logger;
        _settings = settings;
        _packetFilterService = packetFilterService;

        _localPort = settings.GetValue<int>("LocalPort");
        if (_localPort == 0)
        {
            _logger.LogCritical("LocalPort value missing in appsettings.json");
            throw new Exception();
        }

        try
        {

            _tapAdapter = tapAdapter;
            _logger.LogInformation("Tap-adapter initialized");
        }
        catch (Exception ex)
        {

            _logger.LogCritical($"Failed to Initialize Tap_adapter: {ex.Message}");
            throw;
        }

    }

    public  async Task Run()
    {
        await Task.WhenAll(StartUdpServer());
    }

    private async Task StartUdpServer()
    {
        try
        {
            using (var udpServer = new UdpClient(_localPort))
            {
                _logger.LogInformation($"UPD-server started at port {_localPort}");
                await FromClientToTap(udpServer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"UDP-server error: {ex.Message}");
            throw;
        }
    }
    private async Task FromClientToTap(UdpClient udp)
    {
        while (true)
        {
            //await Task.Delay(10);
            var receivedResult = await ReadFromClientsAsync(udp);
            if (receivedResult != null)
            {
                var buffer = receivedResult?.Buffer;
                int bytesRead = buffer!.Length;
#if DEBUG
                _logger.LogInformation($"Received from remote {bytesRead} bytes");
#endif
                //await _tapAdapter.WriteAsync(buffer, bytesRead);
                var packet = new EthPacket(buffer);
                //if (!packet.PhysicalAddress.Equals(_tapAdapter.PhysicalAddress))
                //{
                   _tapAdapter.EnqueuePacket(buffer);
                //}
            }
        }
    }
    private async Task<UdpReceiveResult?> ReadFromClientsAsync(UdpClient udp)
    {
        var client = await udp.ReceiveAsync();
#if DEBUG
        _logger.LogWarning($"Receive from clinet {client.Buffer.Length} | int size {sizeof(int)}");
#endif
        if (client.Buffer.Length == sizeof(int))
        {
            if (!clients.Contains(client.RemoteEndPoint))
            {
                clients.Add(client.RemoteEndPoint);
                _logger.LogInformation($"New client connected {client.RemoteEndPoint.Address}:{client.RemoteEndPoint.Port}");

                // receive port from client receive server
                var port = Encoding.UTF8.GetString(client.Buffer);
                // receive physical address from client
                client = await udp.ReceiveAsync();
                var endPoint = new MyEndPoint(Encoding.UTF8.GetString(client.Buffer), client.RemoteEndPoint.Address, int.Parse(port));
                _packetFilterService.AddRestriction(endPoint.PhysicalAddress);

                _logger.LogInformation($"Client start receive data server on {endPoint.Address}:{port}" +
                    $"\nClient MAC: {endPoint.PhysicalAddress}");
                Task.Run(async () => ConnectToClient(endPoint));
            }
            return null;
        }
        else
        {
            return client;
        }
    }
    private async Task ConnectToClient(IPEndPoint endPoint)
    {
        using(var udpServer = new UdpClient())
        {
            try
            {
                _logger.LogInformation($"Trying to connect to client server {endPoint.Address}:{endPoint.Port}...");
                udpServer.Connect(endPoint);
                _logger.LogInformation("Connected to the client server.");
                await udpServer.SendAsync(Encoding.UTF8.GetBytes(_tapAdapter.PhysicalAddress.ToString()));
                await FromTapToClient(udpServer, endPoint);
            }
            catch(Exception ex)
            {
                _logger.LogWarning($"Disconnected from client {endPoint.Address}:{endPoint.Port} | {ex.Message}");
            }
            finally
            {
                clients.Remove(endPoint);
            }
        }
    }
    
    
    private async Task FromTapToClient(UdpClient udp, IPEndPoint endPoint)
    {
        while (true)
        {
#if DEBUG
            _logger.LogInformation($"START READING FROM TAP");
#endif
            var packet = _tapAdapter.DequeuePacket();
            if (packet == null) continue;
#if DEBUG
            _logger.LogInformation($"END READING FROM TAP");
#endif
            // add mac address filtering to prevent loop
            var ethPacket = new EthPacket(packet);

            if (!_packetFilterService.IsRestricted(ethPacket.PhysicalAddress))
            {
                try
                {
                    await udp.SendAsync(packet, packet.Length);
#if DEBUG
                    _logger.LogInformation($"Packet sent to remote {endPoint.Address}:{endPoint.Port} | {packet.Length} bytes");
#endif
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error on sending packet to client: {ex.Message}");
                }
            }
        }
    }
    private async Task FromTapToFile()
    {
        using (var fs = new FileStream("C:\\Temp\\pcap.pcap", FileMode.Create))
        {
            while (true)
            {
                //var buffer = await _tapAdapter.ReadAsync();
                //await fs.WriteAsync(buffer, 0, buffer.Length);
            }

        }
    }
}