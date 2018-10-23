using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Hosting;
using Orleans.Messaging;
using System;

namespace Orleans.Clustering.Kubernetes
{
    public static class ClusteringExtensions
    {
        public static ISiloHostBuilder UseKubeMembership(this ISiloHostBuilder builder,
            Action<KubeClusteringOptions> configureOptions)
        {
            return builder.ConfigureServices(services => services.UseKubeMembership(configureOptions));
        }

        public static ISiloHostBuilder UseKubeMembership(this ISiloHostBuilder builder,
            Action<OptionsBuilder<KubeClusteringOptions>> configureOptions)
        {
            return builder.ConfigureServices(services => services.UseKubeMembership(configureOptions));
        }

        public static ISiloHostBuilder UseKubeMembership(this ISiloHostBuilder builder)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddOptions<KubeClusteringOptions>();
                services.AddSingleton<IMembershipTable, KubeMembershipTable>();
            });
        }

        public static IClientBuilder UseKubeGatewayListProvider(this IClientBuilder builder, Action<KubeGatewayOptions> configureOptions)
        {
            return builder.ConfigureServices(services => services.UseKubeGatewayListProvider(configureOptions));
        }

        public static IClientBuilder UseKubeGatewayListProvider(this IClientBuilder builder)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddOptions<KubeGatewayOptions>();
                services.AddSingleton<IGatewayListProvider, KubeGatewayListProvider>();
            });
        }

        public static IClientBuilder UseKubeGatewayListProvider(this IClientBuilder builder, Action<OptionsBuilder<KubeGatewayOptions>> configureOptions)
        {
            return builder.ConfigureServices(services => services.UseKubeGatewayListProvider(configureOptions));
        }

        public static IServiceCollection UseKubeMembership(this IServiceCollection services,
            Action<KubeClusteringOptions> configureOptions)
        {
            return services.UseKubeMembership(ob => ob.Configure(configureOptions));
        }

        public static IServiceCollection UseKubeMembership(this IServiceCollection services,
            Action<OptionsBuilder<KubeClusteringOptions>> configureOptions)
        {
            configureOptions?.Invoke(services.AddOptions<KubeClusteringOptions>());

            return services.AddSingleton<IMembershipTable, KubeMembershipTable>();
        }

        public static IServiceCollection UseKubeGatewayListProvider(this IServiceCollection services,
            Action<KubeGatewayOptions> configureOptions)
        {
            return services.UseKubeGatewayListProvider(ob => ob.Configure(configureOptions));
        }

        public static IServiceCollection UseKubeGatewayListProvider(this IServiceCollection services,
            Action<OptionsBuilder<KubeGatewayOptions>> configureOptions)
        {
            configureOptions?.Invoke(services.AddOptions<KubeGatewayOptions>());

            return services.AddSingleton<IGatewayListProvider, KubeGatewayListProvider>();
        }
    }
}
