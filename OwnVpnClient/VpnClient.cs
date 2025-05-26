using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using Utility;
using System.Text;
using System.Net.NetworkInformation;

public class VpnClient
{
    private static readonly TapAdapter _tapAdapter;
    private static readonly ConcurrentQueue<(byte[], int)> _tapQueue = new ConcurrentQueue<(byte[], int)>();
    private static readonly SemaphoreSlim _udpLock = new SemaphoreSlim(1, 1);
    static VpnClient()
    {
        try
        {
            // Замените на GUID вашего TAP-адаптера на клиенте
            _tapAdapter = TapAdapter.Init();
            Console.WriteLine("TAP-адаптер клиента инициализирован");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка инициализации TAP-адаптера: {ex.Message}");
            throw;
        }
    }

    public static async Task Run(string[] args)
    {
        Console.Write("Введите IP сервера: ");
        string ip = Console.ReadLine();
        Console.Write("Введите порт сервера: ");
        if (!int.TryParse(Console.ReadLine(), out int port))
        {
            Console.WriteLine("Неверный порт");
            return;
        }
        await ConnectToServer(ip, port);
    }

    static async Task ConnectToServer(string serverIp, int serverPort)
    {
        try
        {
            using (var udpClient = new UdpClient())
            {
                var serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
                udpClient.Connect(serverEndPoint);
                Console.WriteLine($"UDP-клиент подключён к {serverIp}:{serverPort}");
                await Task.WhenAll(UdpHandler(udpClient));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка клиента: {ex.Message}");
        }
    }

    private static async Task UdpHandler(UdpClient udpClient)
    {
        byte[] buffer = new byte[TapAdapter.BufferSize];
        //await Task.WhenAll(FromClientToTap(udpClient), FromTapToClient(udpClient));
        await udpClient.SendAsync(Encoding.UTF8.GetBytes("8889"));

        Task.Run(async () => ReceiveUdpServer());

        await FromTapToClient(udpClient);
    }

    private static async Task ReceiveUdpServer()
    {
        using (var udpServer = new UdpClient(8889))
        {
            Console.WriteLine($"UDP-сервер запущен на порту {8889}");

            await FromClientToTap(udpServer);
        }
    }

    private static async Task FromClientToTap(UdpClient udp)
    {
        while (true)
        {
            Console.WriteLine("trying to receive from server");
            var receivedResult = await udp.ReceiveAsync();
            Console.WriteLine($"received from server {receivedResult.Buffer.Length} bytes");

            byte[] buffer = receivedResult.Buffer;
            int bytesRead = buffer.Length;

            //_tapAdapter.Write(buffer, (uint)bytesRead);
            await _tapAdapter.WriteAsync(buffer, bytesRead);
        }
    }

    private static async Task FromTapToClient(UdpClient udp)
    {
        while (true)
        {
            byte[] buffer = await _tapAdapter.ReadAsync();
            //var readBytes = _tapAdapter.Read(ref buffer);
            //await _udpLock.WaitAsync();
            PhysicalAddress.TryParse("00-FF-52-DD-F2-37", out PhysicalAddress? clientAddr);
            if (clientAddr == null) throw new Exception("Failed to parce mac address");
            var receivedAddr = EthPacket.GetSourceMAC(buffer);
            if (!receivedAddr.Equals(clientAddr))
            {
                try
                {

                    await udp.SendAsync(buffer, buffer.Length);
                }
                finally
                {
                    //_udpLock.Release();
                }
            }
        }
    }
    private static async Task TapReader()
    {
        byte[] buffer = new byte[TapAdapter.BufferSize];
        while (true)
        {
            try
            {
                int bufferSize = _tapAdapter.Read(ref buffer);
                if (bufferSize > 0)
                {
                    Console.WriteLine($"Прочитано {bufferSize} байт из TAP");
                    if (bufferSize >= 14)
                    {
                        string packetType = BitConverter.ToString(buffer, 12, 2);
                        Console.WriteLine($"Тип пакета: {packetType}");
                        if (packetType == "08-06" && bufferSize >= 42)
                        {
                            string srcIp = $"{buffer[28]}.{buffer[29]}.{buffer[30]}.{buffer[31]}";
                            string dstIp = $"{buffer[38]}.{buffer[39]}.{buffer[40]}.{buffer[41]}";
                            Console.WriteLine($"ARP: {srcIp} запрашивает MAC-адрес {dstIp}");
                        }
                        else if (packetType == "08-00" && bufferSize >= 34)
                        {
                            byte ipProtocol = buffer[23];
                            if (ipProtocol == 1)
                            {
                                Console.WriteLine($"ICMP-пакет: Тип {buffer[34]}, Код {buffer[35]}");
                            }
                        }
                    }
                    byte[] packetCopy = new byte[bufferSize];
                    Buffer.BlockCopy(buffer, 0, packetCopy, 0, bufferSize);
                    _tapQueue.Enqueue((packetCopy, bufferSize));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка чтения из TAP: {ex.Message}");
                await Task.Delay(100);
            }
        }
    }
}