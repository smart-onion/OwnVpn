using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.DependencyInjection;
using Utility;
using Microsoft.Extensions.Logging;

namespace OwnVpnClient
{
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
                .AddSingleton<VpnClient>()
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var myService = scope.ServiceProvider.GetService<VpnClient>();
                await myService.Run(args); // Triggers warning: 'DoWork' is obsolete
                                       // myService.DoWorkAsync(); // Use this instead
            }
        }
    }
}
