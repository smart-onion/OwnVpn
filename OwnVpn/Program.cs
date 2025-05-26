using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Utility;
using Microsoft.Extensions.Logging;
internal class Program
{
    private static async Task Main(string[] args)
    {
        // Set up configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        // Set up DI
        var serviceProvider = new ServiceCollection()
            .AddSingleton(configuration)
            .AddLogging(builder => builder.AddConsole()) // Add logging
            .AddSingleton<TapAdapter>()
            .AddSingleton<PacketFilterService>()
            .AddSingleton<VpnServer>()
            .BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var myService = scope.ServiceProvider.GetService<VpnServer>();
            await myService.Run(); // Triggers warning: 'DoWork' is obsolete
            // myService.DoWorkAsync(); // Use this instead
        }
    }

    private static byte[] GetDemiPacket()
    {
        byte[] demiPacket = new byte[60]; // Minimum Ethernet frame size

        // Destination MAC (broadcast)
        demiPacket[0] = 0xFF;
        demiPacket[1] = 0xFF;
        demiPacket[2] = 0xFF;
        demiPacket[3] = 0xFF;
        demiPacket[4] = 0xFF;
        demiPacket[5] = 0xFF;

        // Source MAC (random)
        demiPacket[6] = 0x00;
        demiPacket[7] = 0x11;
        demiPacket[8] = 0x22;
        demiPacket[9] = 0x33;
        demiPacket[10] = 0x44;
        demiPacket[11] = 0x55;

        // EtherType (IPv4)
        demiPacket[12] = 0x08;
        demiPacket[13] = 0x00;

        // Dummy payload (zeros)
        for (int i = 14; i < 60; i++)
        {
            demiPacket[i] = 0x00;
        }
        return demiPacket;
    }
}