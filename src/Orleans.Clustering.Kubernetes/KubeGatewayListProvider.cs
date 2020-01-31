using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Clustering.Kubernetes.API;
using Orleans.Clustering.Kubernetes.Models;
using Orleans.Configuration;
using Orleans.Messaging;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Orleans.Clustering.Kubernetes
{
    internal class KubeGatewayListProvider : IGatewayListProvider
    {
        private const string PROVIDER_MODEL_VERSION = "v1";

        private readonly KubeGatewayOptions _options;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _clusterId;

        private KubeClient _kube;

        public TimeSpan MaxStaleness { get; private set; }
        public bool IsUpdatable => true;

        public KubeGatewayListProvider(ILoggerFactory loggerFactory, IOptions<KubeGatewayOptions> options, IOptions<ClusterOptions> clusterOptions, IOptions<GatewayOptions> gatewayOptions)
        {
            this._loggerFactory = loggerFactory;
            this.MaxStaleness = gatewayOptions.Value.GatewayListRefreshPeriod;
            this._logger = loggerFactory?.CreateLogger<KubeGatewayListProvider>();
            this._options = options.Value;
            this._clusterId = clusterOptions.Value.ClusterId;
        }

        public async Task<IList<Uri>> GetGateways()
        {
            try
            {
                var silos = await this._kube.ListCustomObjects<SiloEntity>(PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);

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

        public Task InitializeGatewayListProvider()
        {
            this._kube = new KubeClient(
                this._loggerFactory, null, this._options.APIEndpoint, this._options.Group,
                this._options.APIToken, this._options.CertificateData);

            return Task.CompletedTask;
        }

        private static Uri ConvertToGatewayUri(SiloEntity gateway)
        {
            SiloAddress address = SiloAddress.New(new IPEndPoint(IPAddress.Parse(gateway.Address), gateway.ProxyPort.Value), gateway.Generation);
            return address.ToGatewayUri();
        }
    }
}
