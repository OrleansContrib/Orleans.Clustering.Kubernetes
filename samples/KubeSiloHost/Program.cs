using HelloWorld.Grains;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Clustering.Kubernetes;
using Orleans.Configuration;
using Orleans.Hosting;
using System;
using System.Threading.Tasks;

namespace KubeSiloHost
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var host = BuildHost();

                await host.RunAsync();
                Console.WriteLine("Shutting down...");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static IHost BuildHost()
        {
            var hostBuilder = new HostBuilder()
                .UseOrleans(siloBuilder => siloBuilder
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "testcluster"; // Must be lowercase
                        options.ServiceId = "hello";
                    })
                    .ConfigureEndpoints(new Random(1).Next(10001, 10100), new Random(1).Next(20001, 20100))
                    .UseKubeMembership(opt =>
                    {
                        //opt.APIEndpoint = "http://localhost:8001";
                        //opt.CertificateData = "test";
                        //opt.APIToken = "test";
                        opt.CanCreateResources = true;
                        opt.DropResourcesOnInit = true;
                    })
                    .AddMemoryGrainStorageAsDefault()
                    .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(HelloGrain).Assembly).WithReferences())
                    .ConfigureLogging(logging => logging.AddConsole())
                );

            return hostBuilder.Build();
        }
    }
}
