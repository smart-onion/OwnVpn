namespace OwnVpnClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await Task.Delay(1000);
            await VpnClient.Run(args);
        }
    }
}
