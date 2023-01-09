using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Orleans.Clustering.Kubernetes.Models;
using Orleans.Configuration;
using Orleans.Messaging;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Clustering.Kubernetes;

internal class KubeGatewayListProvider : IGatewayListProvider
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _clusterId;
    private readonly k8s.IKubernetes _kube;
    private readonly KubeGatewayOptions _kubeGatewayOptions;
    private string _namespace;

    public TimeSpan MaxStaleness { get; private set; }
    public bool IsUpdatable => true;

    public KubeGatewayListProvider(
        ILoggerFactory loggerFactory,
        IOptions<ClusterOptions> clusterOptions,
        IOptions<GatewayOptions> gatewayOptions,
        IOptions<KubeGatewayOptions> kubeGatewayOptions,
        k8s.IKubernetes kubernetesClient
    )
    {
        this._loggerFactory = loggerFactory;
        this.MaxStaleness = gatewayOptions.Value.GatewayListRefreshPeriod;
        this._logger = loggerFactory?.CreateLogger<KubeGatewayListProvider>();
        this._kube = kubernetesClient;
        this._clusterId = clusterOptions.Value.ClusterId;
        this._kubeGatewayOptions = kubeGatewayOptions.Value;
    }

    public async Task<IList<Uri>> GetGateways()
    {
        try
        {
            var silos = ((JObject)await this._kube.ListNamespacedCustomObjectAsync(Constants.ORLEANS_GROUP, Constants.PROVIDER_MODEL_VERSION, this._namespace, SiloEntity.PLURAL))?["items"]?.ToObject<SiloEntity[]>();
            if (silos == null || silos.Length == 0) return Array.Empty<Uri>();

            var gateways = silos.Where(s => s.Status == SiloStatus.Active && s.ProxyPort != 0 && s.ClusterId == this._clusterId)
                .Select(ConvertToGatewayUri).ToList();
            return gateways;
        }
        catch (Exception exc)
        {
            this._logger?.LogError(exc, "Unable to get gateways from Kube objects for cluster {ClusterId}", this._clusterId);
            throw;
        }
    }

    public Task InitializeGatewayListProvider()
    {
        this._namespace = this.GetNamespace();
        return Task.CompletedTask;
    }

    private static Uri ConvertToGatewayUri(SiloEntity gateway)
    {
        SiloAddress address = SiloAddress.New(new IPEndPoint(IPAddress.Parse(gateway.Address), gateway.ProxyPort.Value), gateway.Generation);
        return address.ToGatewayUri();
    }

    private string GetNamespace()
    {
        if (!string.IsNullOrWhiteSpace(this._kubeGatewayOptions.Namespace)) return this._kubeGatewayOptions.Namespace;

        var namespaceFilePath = Path.Combine(Constants.SERVICE_ACCOUNT_PATH, Constants.SERVICE_ACCOUNT_NAMESPACE_FILENAME);
        if (!File.Exists(namespaceFilePath)) return Constants.ORLEANS_NAMESPACE;

        var ns = File.ReadAllText(namespaceFilePath);

        if (!string.IsNullOrWhiteSpace(ns)) return ns;

        this._logger?.LogWarning(
            "Namespace file {NamespaceFilePath} wasn't found. Are we running in a pod? If you are running unit tests outside a pod, please create the test namespace '{Namespace}'",
            namespaceFilePath, Constants.ORLEANS_NAMESPACE);

        return Constants.ORLEANS_NAMESPACE;
    }
}