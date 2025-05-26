namespace Utility
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    public class StunClient
    {
        private const string STUN_SERVER = "stun.l.google.com";
        private const int STUN_PORT = 19302;

        public static (string PublicIp, int PublicPort) GetPublicEndpoint()
        {
            try
            {
                UdpClient udpClient = new UdpClient();

                udpClient.Connect(STUN_SERVER, STUN_PORT);

                // Формируем STUN Binding Request
                byte[] request = new byte[20];
                request[0] = 0x00; // Тип сообщения: Binding Request
                request[1] = 0x01;
                request[2] = 0x00; // Длина сообщения
                request[3] = 0x00;
                // Magic Cookie
                request[4] = 0x21;
                request[5] = 0x12;
                request[6] = 0xA4;
                request[7] = 0x42;
                // Transaction ID (случайный)
                Random rand = new Random();
                for (int i = 8; i < 20; i++)
                    request[i] = (byte)rand.Next(0, 256);

                udpClient.Send(request, request.Length);

                // Получаем ответ
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] response = udpClient.Receive(ref remoteEndPoint);

                // Парсим ответ для получения публичного IP и порта
                if (response.Length >= 20 && response[0] == 0x01 && response[1] == 0x01) // Binding Response
                {
                    int length = (response[2] << 8) + response[3];
                    int offset = 20;
                    while (offset < response.Length && offset < 20 + length)
                    {
                        int attrType = (response[offset] << 8) + response[offset + 1];
                        int attrLength = (response[offset + 2] << 8) + response[offset + 3];
                        if (attrType == 0x0001 || attrType == 0x0020) // MAPPED-ADDRESS или XOR-MAPPED-ADDRESS
                        {
                            offset += 4;
                            int port = (response[offset + 2] << 8) + response[offset + 3];
                            if (attrType == 0x0020) // XOR-MAPPED-ADDRESS
                            {
                                port ^= 0x2112;
                                for (int i = 0; i < 4; i++)
                                    response[offset + 4 + i] ^= request[4 + i];
                            }
                            string ip = $"{response[offset + 4]}.{response[offset + 5]}.{response[offset + 6]}.{response[offset + 7]}";
                            udpClient.Close();
                            return (ip, port);
                        }
                        offset += 4 + attrLength;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка STUN: {ex.Message}");
            }
            return (null, 0);
        }
    }
}
