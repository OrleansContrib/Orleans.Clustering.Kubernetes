using HelloWorld.Interfaces;
using Orleans;
using Orleans.Clustering.Kubernetes;
using Orleans.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using HelloWorld.Grains;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;

namespace KubeClient;

/// <summary>
/// Orleans test silo client
/// </summary>
public static class Program
{
    private static readonly AutoResetEvent Closing = new(false);

    public static async Task<int> Main()
    {
        try
        {
            var builder = new HostBuilder();
            builder.ConfigureServices(collection =>
            {
                collection.AddScoped<IHello, HelloGrain>();
            });

            builder.UseOrleansClient(ConfigureDelegate);
            var host = builder.Build();
            await host.StartAsync();

            var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
            await DoClientWork(grainFactory);

            Console.CancelKeyPress += OnExit;
            Closing.WaitOne();

            Console.WriteLine("Shutting down...");

            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 1;
        }
    }

    private static void ConfigureDelegate(IClientBuilder builder)
    {
        builder
            .Configure<ClusterOptions>(options =>
            {
                options.ClusterId = "testcluster";
                options.ServiceId = "testservice";
            })
            .UseKubeGatewayListProvider();
    }

    private static async Task DoClientWork(IGrainFactory client)
    {
        var friend = client.GetGrain<IHello>(0);
        for (var i = 0; i < 10; i++)
        {
            var response = await friend.SayHello("Good morning, my friend!");
            Console.WriteLine("\n\n{0}\n\n", response);
        }
    }

    private static void OnExit(object sender, ConsoleCancelEventArgs args)
    {
        Closing.Set();
    }
}