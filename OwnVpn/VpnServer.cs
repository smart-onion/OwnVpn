using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using Utility;
using System.Text;
using System.Net.NetworkInformation;
using System.Data;

public class VpnServer
{
    private const int LOCAL_PORT = 8888;
    private static readonly TapAdapter _tapAdapter;
    private static readonly ConcurrentQueue<(byte[], int)> _tapQueue = new ConcurrentQueue<(byte[], int)>();
    static IPEndPoint? endPoint = null;
    static VpnServer()
    {
        try
        {
            // Замените на GUID вашего TAP-адаптера на сервере
            _tapAdapter = TapAdapter.Init();
            Console.WriteLine("TAP-адаптер сервера инициализирован");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка инициализации TAP-адаптера: {ex.Message}");
            throw;
        }
    }

    public static async Task Run()
    {
        await Task.WhenAll(StartUdpServer());
    }

    static async Task StartUdpServer()
    {
        try
        {
            using (var udpServer = new UdpClient(LOCAL_PORT))
            {
                Console.WriteLine($"UDP-сервер запущен на порту {LOCAL_PORT}");
                
                var clientPort = await udpServer.ReceiveAsync();

                var port = Encoding.UTF8.GetString(clientPort.Buffer);

                endPoint = new IPEndPoint(clientPort.RemoteEndPoint.Address, int.Parse(port));

                //Task.Run(async () => ConnectToClient(endPoint));

                await FromClientToTap(udpServer);

            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка UDP-сервера: {ex.Message}");
        }
    }

    private static async Task FromTapToFile()
    {
        using(var fs = new FileStream("C:\\Temp\\pcap.pcap",FileMode.Create))
        {
            while (true)
            {
                var buffer =await _tapAdapter.ReadAsync();
                //await fs.WriteAsync(buffer, 0, buffer.Length);
            }
            
        }
    }

    private static async Task ConnectToClient(IPEndPoint endPoint)
    {
        using(var udpServer = new UdpClient())
        {

            Console.WriteLine($"trying to connect to client server {endPoint.Address}:{endPoint.Port}");
            udpServer.Connect(endPoint);
            Console.WriteLine("connected to the client server");
            await FromTapToClient(udpServer, endPoint);
        }
    }
    private static async Task FromClientToTap(UdpClient udp)
    {
        while (true)
        {

            var receivedResult = await udp.ReceiveAsync();
            if(endPoint == null)
            {
                endPoint = receivedResult.RemoteEndPoint;
                var packet = new EthPacket(receivedResult.Buffer);
            }

            byte[] buffer = receivedResult.Buffer;
            int bytesRead = buffer.Length;
            Console.WriteLine($"Received from remote {bytesRead} bytes");
            //_tapAdapter.Write(buffer, (uint)bytesRead);


            await _tapAdapter.WriteAsync(buffer, bytesRead);
        }
    }

    private static async Task FromTapToClient(UdpClient udp, IPEndPoint endPoint)
    {
        while (true)
        {
            byte[] buffer = await _tapAdapter.ReadAsync();


            //PhysicalAddress.TryParse("00-FF-F2-C3-69-B0", out PhysicalAddress? clientAddr);
            //if (clientAddr == null) throw new Exception("Failed to parce mac address");
            //var receivedAddr = EthPacket.GetSourceMAC(buffer);
            //if (!receivedAddr.Equals(clientAddr))
            {
                try
                {
                    Console.WriteLine($"sending packet to remote {endPoint.Address}:{endPoint.Port} | {buffer.Length} bytes");
                    await udp.SendAsync(buffer, buffer.Length);
                    Console.WriteLine("packet sent");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}