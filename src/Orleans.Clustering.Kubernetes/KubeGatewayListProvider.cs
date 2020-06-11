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

namespace Orleans.Clustering.Kubernetes
{
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
                this._logger?.LogError(exc, "Unable to get gateways from Kube objects for cluster {clusterId}", this._clusterId);
                throw;
            }
        }

        public async Task InitializeGatewayListProvider()
        {
            this._namespace = await this.GetNamespace();
        }

        private static Uri ConvertToGatewayUri(SiloEntity gateway)
        {
            SiloAddress address = SiloAddress.New(new IPEndPoint(IPAddress.Parse(gateway.Address), gateway.ProxyPort.Value), gateway.Generation);
            return address.ToGatewayUri();
        }

        private async ValueTask<string> GetNamespace()
        {
            if (!string.IsNullOrWhiteSpace(this._kubeGatewayOptions.Namespace)) return this._kubeGatewayOptions.Namespace;

            var namespaceFilePath = Path.Combine(Constants.SERVICE_ACCOUNT_PATH, Constants.SERVICE_ACCOUNT_NAMESPACE_FILENAME);
            if (File.Exists(namespaceFilePath))
            {
                using var sourceStream = new FileStream(namespaceFilePath,
                    FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 4096, useAsync: true);

                var sb = new StringBuilder();

                byte[] buffer = new byte[0x1000];
                int numRead;
                while ((numRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string text = Encoding.Unicode.GetString(buffer, 0, numRead);
                    sb.Append(text);
                }

                return sb.ToString();
            }

            this._logger?.LogWarning(
                "Namespace file {namespaceFilePath} wasn't found. Are we running in a pod? If you are running unit tests outside a pod, please create the test namespace '{namespace}'.",
                namespaceFilePath, Constants.ORLEANS_NAMESPACE);

            return Constants.ORLEANS_NAMESPACE;
        }
    }
}
