using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using Utility;
using System.Text;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebSocketSharp;

public class VpnClient
{
    private int _localPort;
    private int _serverPort;
    private readonly ILogger<VpnClient> _logger;
    private readonly IConfigurationRoot _settings;
    private readonly PacketFilterService _packetFilterService;
    private readonly NetAdapter _tapAdapter;
    public VpnClient(
        ILogger<VpnClient> logger,
        IConfigurationRoot settings,
        PacketFilterService packetFilterService,
        NetAdapter tapAdapter
        )
    {
        _logger = logger;
        _settings = settings;
        _packetFilterService = packetFilterService;
        _tapAdapter = tapAdapter;
        
        _serverPort = _settings.GetValue<int>("ServerPort");
        _localPort = _settings.GetValue<int>("LocalPort");

        if (_localPort == 0 || _serverPort == 0)
        {
            _logger.LogCritical("LocalPort or ServerPort value is missing in appsettings.json");
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

    public async Task Run(string[] args)
    {
        int localPort = 0;
        string ip = "";
        // Parse command-line arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int port))
                {
                    _serverPort = port;
                }
                i++;
            }
            else if (args[i] == "--ip" && i + 1 < args.Length)
            {
                ip = args[i + 1];
                i++;
            }
        }
#if DEBUG
        ip = "9.21.20.216";
#endif
        if (ip.IsNullOrEmpty())
        {
            Console.WriteLine("Usage: --ip [ip] --port [port]");
            return;
        }

        await ConnectToServer(ip);
    }

    async Task ConnectToServer(string serverIp)
    {
        try
        {
            using (var udpClient = new UdpClient())
            {
                var serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), _serverPort);
                udpClient.Connect(serverEndPoint);
                _logger.LogInformation($"UDP-client connected to {serverIp}:{_serverPort}");
                await Task.WhenAll(UdpHandler(udpClient));
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical($"Error on client: {ex.Message}");
        }
    }

    private async Task UdpHandler(UdpClient udpClient)
    {
        byte[] buffer = new byte[TapAdapter.BufferSize];
        //await Task.WhenAll(FromClientToTap(udpClient), FromTapToClient(udpClient));
        await udpClient.SendAsync(Encoding.UTF8.GetBytes($"{_localPort}"));
        await udpClient.SendAsync(Encoding.UTF8.GetBytes(_tapAdapter.PhysicalAddress.ToString()));
        Task.Run(async () => ReceiveUdpServer());

        await FromTapToClient(udpClient);
    }

    private async Task ReceiveUdpServer()
    {
        using (var udpServer = new UdpClient(_localPort))
        {
            Console.WriteLine($"UDP-receive server started on port {_localPort}");
            var bytes = await udpServer.ReceiveAsync();

            var mac = Encoding.UTF8.GetString(bytes.Buffer);

            var pa = PhysicalAddress.Parse(mac);

            _packetFilterService.AddRestriction(pa);

            await FromClientToTap(udpServer);
        }
    }

    private async Task FromClientToTap(UdpClient udp)
    {
        while (true)
        {

            var receivedResult = await udp.ReceiveAsync();
#if DEBUG
            _logger.LogInformation($"received from server {receivedResult.Buffer.Length} bytes");
#endif
            byte[] buffer = receivedResult.Buffer;
            int bytesRead = buffer.Length;

            //await _tapAdapter.WriteAsync(buffer, bytesRead);
            var packet = new EthPacket(buffer);
            //if (!packet.PhysicalAddress.Equals(_tapAdapter.PhysicalAddress))
            //{
                _tapAdapter.EnqueuePacket(buffer);
            //}
        }
    }

    private async Task FromTapToClient(UdpClient udp)
    {
        while (true)
        {
            //byte[] buffer = await _tapAdapter.ReadAsync();
            //await Task.Delay(10);

            var packet = _tapAdapter.DequeuePacket();
            if (packet == null) continue;
            // add mac address filtering to prevent loop
            var ethPacket = new EthPacket(packet);
            if (!_packetFilterService.IsRestricted(ethPacket.PhysicalAddress))
            {
                try
                {
                    await udp.SendAsync(packet, packet.Length);
#if DEBUG
                    _logger.LogInformation($"Packet sent to remote | {packet.Length} bytes");
#endif
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error on sending packet to client: {ex.Message}");
                }
            }
        }
    }
    //private static async Task TapReader()
    //{
    //    byte[] buffer = new byte[TapAdapter.BufferSize];
    //    while (true)
    //    {
    //        try
    //        {
    //            int bufferSize = _tapAdapter.Read(ref buffer);
    //            if (bufferSize > 0)
    //            {
    //                Console.WriteLine($"Прочитано {bufferSize} байт из TAP");
    //                if (bufferSize >= 14)
    //                {
    //                    string packetType = BitConverter.ToString(buffer, 12, 2);
    //                    Console.WriteLine($"Тип пакета: {packetType}");
    //                    if (packetType == "08-06" && bufferSize >= 42)
    //                    {
    //                        string srcIp = $"{buffer[28]}.{buffer[29]}.{buffer[30]}.{buffer[31]}";
    //                        string dstIp = $"{buffer[38]}.{buffer[39]}.{buffer[40]}.{buffer[41]}";
    //                        Console.WriteLine($"ARP: {srcIp} запрашивает MAC-адрес {dstIp}");
    //                    }
    //                    else if (packetType == "08-00" && bufferSize >= 34)
    //                    {
    //                        byte ipProtocol = buffer[23];
    //                        if (ipProtocol == 1)
    //                        {
    //                            Console.WriteLine($"ICMP-пакет: Тип {buffer[34]}, Код {buffer[35]}");
    //                        }
    //                    }
    //                }
    //                byte[] packetCopy = new byte[bufferSize];
    //                Buffer.BlockCopy(buffer, 0, packetCopy, 0, bufferSize);
    //                _tapQueue.Enqueue((packetCopy, bufferSize));
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Console.WriteLine($"Ошибка чтения из TAP: {ex.Message}");
    //            await Task.Delay(100);
    //        }
    //    }
    //}
}