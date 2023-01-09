using Microsoft.Extensions.Logging;
using Orleans.Clustering.Kubernetes;
using Orleans.Configuration;
using Orleans.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace KubeSiloHost;

public static class Program
{
    private static readonly AutoResetEvent Closing = new AutoResetEvent(false);

    public static async Task<int> Main()
    {
        try
        {
            var builder = new HostBuilder();
            builder.UseOrleans(ConfigureDelegate);
            var host = builder.Build();

            await host.StartAsync();
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

    private static void ConfigureDelegate(HostBuilderContext context, ISiloBuilder builder)
    {
        builder.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = "testcluster";
                options.ServiceId = "testservice";
            })
            .ConfigureEndpoints(new Random(1).Next(10001, 10100), new Random(1).Next(20001, 20100))
            .UseKubeMembership()
            .AddMemoryGrainStorageAsDefault()
            .ConfigureLogging(logging => logging.AddConsole());
    }

    private static void OnExit(object sender, ConsoleCancelEventArgs args)
    {
        Closing.Set();
    }
}