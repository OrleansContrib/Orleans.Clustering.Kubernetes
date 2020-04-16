using HelloWorld.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Clustering.Kubernetes;
using Orleans.Configuration;
using Orleans.Runtime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KubeClient
{
    /// <summary>
    /// Orleans test silo client
    /// </summary>
    public class Program
    {
        private static readonly AutoResetEvent Closing = new AutoResetEvent(false);

        static async Task<int> Main(string[] args)
        {
            try
            {
                using (var client = await StartClientWithRetries())
                {
                    await DoClientWork(client);

                    Console.CancelKeyPress += OnExit;
                    Closing.WaitOne();

                    Console.WriteLine("Shutting down...");
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }
        }

        private static async Task<IClusterClient> StartClientWithRetries(int initializeAttemptsBeforeFailing = 5)
        {
            int attempt = 0;
            IClusterClient client;
            while (true)
            {
                try
                {
                    client = new ClientBuilder()
                        .Configure<ClusterOptions>(options =>  { options.ClusterId = "testcluster"; options.ServiceId = "testservice"; })
                        .UseKubeGatewayListProvider()
                        .ConfigureLogging(logging => logging.AddConsole())
                        .Build();

                    await client.Connect();
                    Console.WriteLine("Client successfully connect to silo host");
                    break;
                }
                catch (SiloUnavailableException)
                {
                    attempt++;
                    Console.WriteLine($"Attempt {attempt} of {initializeAttemptsBeforeFailing} failed to initialize the Orleans client.");
                    if (attempt > initializeAttemptsBeforeFailing)
                    {
                        throw;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }
            }

            return client;
        }

        private static async Task DoClientWork(IClusterClient client)
        {
            var friend = client.GetGrain<IHello>(0);
            Stopwatch watch = new Stopwatch();
            int iterations = 1000;
            for (int i = 0; i < iterations; i++)
            {
                if (i > 0) watch.Start();
                var response = await friend.SayHello("Good morning, my friend!");
                if (i > 0) watch.Stop();
                Console.WriteLine("{1}: {0}", i, response);
            }
            
            Console.WriteLine("KubeClient test is completed, avg time = {0:G4} milliseconds.", (double)watch.ElapsedTicks / (1000000.0 * iterations));
        }

        private static void OnExit(object sender, ConsoleCancelEventArgs args)
        {
            Closing.Set();
        }
    }
}
