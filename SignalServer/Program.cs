using System;
using System.Net;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SignalServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
           SignalingServer.Run(args);
        }
    }


    class SignalingServer
    {
        public class SignalService : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs e)
            {
                // Пересылаем сообщение всем подключённым клиентам
                Sessions.Broadcast(e.Data);
                Console.WriteLine($"Переслано: {e.Data}");
            }
        }

        public static void Run(string[] args)
        {
            WebSocketServer wss = new WebSocketServer("ws://0.0.0.0:8080");
            wss.AddWebSocketService<SignalService>("/signal");
            wss.Start();
            Console.WriteLine("Сигнализационный сервер запущен на ws://localhost:8080/signal");
            Console.ReadLine();
            wss.Stop();
        }
    }
}
