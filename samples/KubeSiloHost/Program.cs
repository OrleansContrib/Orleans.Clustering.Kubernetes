using HelloWorld.Grains;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Clustering.Kubernetes;
using Orleans.Hosting;
using Orleans.Runtime.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KubeSiloHost
{
    public class Program
    {
        private static readonly AutoResetEvent Closing = new AutoResetEvent(false);

        public static int Main(string[] args)
        {
            return RunMainAsync().Result;
        }

        private static async Task<int> RunMainAsync()
        {
            try
            {
                var host = await StartSilo();
                Console.WriteLine("Silo is ready!");

                Console.CancelKeyPress += OnExit;
                Closing.WaitOne();

                Console.WriteLine("Shutting down...");

                await host.StopAsync();

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static async Task<ISiloHost> StartSilo()
        {
            // define the cluster configuration
            var config = new ClusterConfiguration();
            config.Defaults.Port = new Random(1).Next(10001, 10100);
            config.Globals.ClusterId = "testcluster";
            config.AddMemoryStorageProvider();
            config.Globals.ReminderServiceType = GlobalConfiguration.ReminderServiceProviderType.Disabled;

            var builder = new SiloHostBuilder()
                .UseConfiguration(config)
                .UseKubeMembership(opt =>
                {
                    //opt.APIEndpoint = "http://localhost:8001";
                    opt.CanCreateResources = true;
                    //opt.DropResourcesOnInit = true;
                })
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(HelloGrain).Assembly).WithReferences())
                .ConfigureLogging(logging => logging.AddConsole());

            var host = builder.Build();
            await host.StartAsync();
            return host;
        }

        private static void OnExit(object sender, ConsoleCancelEventArgs args)
        {
            Closing.Set();
        }
    }
}
