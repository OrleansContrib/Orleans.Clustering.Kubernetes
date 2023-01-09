using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Messaging;
using System;

namespace Orleans.Clustering.Kubernetes;

public static class ClusteringExtensions
{
    public static ISiloBuilder UseKubeMembership(this ISiloBuilder builder)
    {
        return builder.ConfigureServices(services =>
        {
            KubernetesClientConfiguration config = default;

            if (KubernetesClientConfiguration.IsInCluster())
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            else
            {
                config = KubernetesClientConfiguration.BuildDefaultConfig();
            }

            services.AddSingleton<IMembershipTable>(sp => new KubeMembershipTable(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IOptions<ClusterOptions>>(),
                new k8s.Kubernetes(config)
            ));
        });
    }

    public static IClientBuilder UseKubeGatewayListProvider(this IClientBuilder builder,
        Action<KubeGatewayOptions> configureOptions = null)
    {
        return builder.ConfigureServices(services =>
        {
            services.AddOptions<KubeGatewayOptions>();
            if (configureOptions != null)
            {
                services.Configure<KubeGatewayOptions>(configureOptions);
            }

            KubernetesClientConfiguration config = default;

            if (KubernetesClientConfiguration.IsInCluster())
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            else
            {
                config = KubernetesClientConfiguration.BuildDefaultConfig();
            }

            services.AddSingleton<IGatewayListProvider>(sp => new KubeGatewayListProvider(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IOptions<ClusterOptions>>(),
                sp.GetRequiredService<IOptions<GatewayOptions>>(),
                sp.GetRequiredService<IOptions<KubeGatewayOptions>>(),
                new k8s.Kubernetes(config)
            ));
        });
    }
}