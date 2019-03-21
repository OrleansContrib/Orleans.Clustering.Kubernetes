using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Clustering.Kubernetes.API;
using Orleans.Clustering.Kubernetes.Models;
using Orleans.Configuration;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Orleans.Clustering.Kubernetes
{
    internal class KubeMembershipTable : IMembershipTable
    {
        private const string PROVIDER_MODEL_VERSION = "v1";
        private const string KUBE_API_VERSION = "apiextensions.k8s.io/v1beta1";
        private const string NAMESPACED = "Namespaced";

        private readonly ClusterOptions _clusterOptions;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly KubeClusteringOptions _options;
        private readonly string _group;

        private KubeClient _kubeClient;


        public KubeMembershipTable(ILoggerFactory loggerFactory, IOptions<ClusterOptions> clusterOptions, IOptions<KubeClusteringOptions> clusteringOptions)
        {
            this._clusterOptions = clusterOptions.Value;
            this._loggerFactory = loggerFactory;
            this._logger = loggerFactory?.CreateLogger<KubeMembershipTable>();
            this._options = clusteringOptions.Value;
            this._group = string.IsNullOrWhiteSpace(this._options.Group) ? KubeClient.ORLEANS_GROUP : this._options.Group;
        }

        public async Task InitializeMembershipTable(bool tryInitTableVersion)
        {
            this._kubeClient = new KubeClient(this._loggerFactory, null, this._options.APIEndpoint,
                this._group, this._options.APIToken, this._options.CertificateData);

            if (this._options.CanCreateResources)
            {
                if (this._options.DropResourcesOnInit)
                {
                    await TryDeleteResources();
                }

                await TryCreateResources();
            }

            if (tryInitTableVersion)
            {
                await TryInitClusterVersion();
            }
        }

        public async Task DeleteMembershipTableEntries(string clusterId)
        {
            var clusterVersion = await GetClusterVersion();

            if (clusterVersion != null)
            {
                await this._kubeClient.DeleteCustomObject(clusterVersion.Metadata.Name,
                            PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL);
            }

            var silos = await GetSilos();

            foreach (var silo in silos)
            {
                await this._kubeClient.DeleteCustomObject(silo.Metadata.Name,
                              PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);
            }
        }

        public async Task<bool> InsertRow(MembershipEntry entry, TableVersion tableVersion)
        {
            try
            {
                var siloEntity = this.ConvertToEntity(entry);
                var versionEntity = this.BuildVersionEntity(tableVersion);

                var existentSiloEntry = await this._kubeClient.GetCustomObject<SiloEntity>(
                    siloEntity.Metadata.Name, PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);

                if (existentSiloEntry != null)
                {
                    return false;
                }

                var currentVersionEntity = await this.GetClusterVersion();

                if (currentVersionEntity == null ||
                    currentVersionEntity.ClusterVersion == versionEntity.ClusterVersion)
                {
                    return false;
                }

                var updatedVersionEntity = await this._kubeClient.UpdateCustomObject(
                    PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL, versionEntity);

                if (updatedVersionEntity == null)
                {
                    return false;
                }

                var createdSiloEntity = await this._kubeClient.CreateCustomObject(
                    PROVIDER_MODEL_VERSION, SiloEntity.PLURAL, siloEntity);

                return createdSiloEntity != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Exception exc)
            {
                this._logger?.LogError(exc, "Unable to insert Silo Entry.");

                throw;
            }
        }

        public async Task<MembershipTableData> ReadAll()
        {
            try
            {
                var versionEntity = await this.GetClusterVersion();
                var entryEntities = await this.GetSilos();

                var version = default(TableVersion);

                if (versionEntity != null)
                {
                    version = new TableVersion(versionEntity.ClusterVersion, versionEntity.Metadata.ResourceVersion);
                }
                else
                {
                    this._logger?.LogError("Initial ClusterVersionEntity entity doesn't exist.");
                }

                var memEntries = new List<Tuple<MembershipEntry, string>>();

                foreach (var entity in entryEntities)
                {
                    try
                    {
                        MembershipEntry membershipEntry = ParseEntity(entity);

                        memEntries.Add(new Tuple<MembershipEntry, string>(membershipEntry, entity.Metadata.ResourceVersion));
                    }
                    catch (Exception exc)
                    {
                        this._logger?.LogWarning(exc, "Failure reading all membership records from Kubernetes");

                        throw;
                    }
                }

                var data = new MembershipTableData(memEntries, version);

                return data;
            }
            catch (Exception exc)
            {
                this._logger?.LogWarning(exc, $"Failure reading all silo entries for cluster id {this._clusterOptions.ClusterId}");

                throw;
            }
        }

        public async Task<MembershipTableData> ReadRow(SiloAddress key)
        {
            var name = ConstructSiloEntityId(key);

            try
            {
                var versionEntity = await this.GetClusterVersion();

                var entity = await this._kubeClient.GetCustomObject<SiloEntity>(name,
                    PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);

                var version = default(TableVersion);

                if (versionEntity != null)
                {
                    version = new TableVersion(versionEntity.ClusterVersion, versionEntity.Metadata.ResourceVersion);
                }
                else
                {
                    this._logger?.LogError("Initial ClusterVersionEntity entity doesn't exist.");
                }

                var memEntries = new List<Tuple<MembershipEntry, string>>();

                if (entity != null)
                {
                    var membershipEntry = ParseEntity(entity);

                    memEntries.Add(new Tuple<MembershipEntry, string>(membershipEntry, entity.Metadata.ResourceVersion));
                }

                var data = new MembershipTableData(memEntries, version);

                return data;
            }
            catch (Exception exc)
            {
                this._logger?.LogError(exc, $"Failure reading silo entry {name} for cluster id {this._clusterOptions.ClusterId}.");

                throw;
            }
        }

        public async Task UpdateIAmAlive(MembershipEntry entry)
        {
            var name = ConstructSiloEntityId(entry.SiloAddress);

            try
            {
                var siloEntity = await this._kubeClient.GetCustomObject<SiloEntity>(
                    name, PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);

                if (siloEntity == null)
                {
                    throw new InvalidOperationException($"Unable to find silo entry {name}.");
                }

                siloEntity.IAmAliveTime = entry.IAmAliveTime;

                await this._kubeClient.UpdateCustomObject(PROVIDER_MODEL_VERSION, SiloEntity.PLURAL, siloEntity);
            }
            catch (Exception exc)
            {
                this._logger?.LogError(exc, $"Unable to update Silo Entry {name}.");

                throw;
            }
        }

        public async Task<bool> UpdateRow(MembershipEntry entry, string etag, TableVersion tableVersion)
        {
            try
            {
                var siloEntity = this.ConvertToEntity(entry);

                siloEntity.Metadata.ResourceVersion = etag;

                var versionEntity = this.BuildVersionEntity(tableVersion);

                var currentVersionEntity = await this.GetClusterVersion();

                if (currentVersionEntity == null ||
                    currentVersionEntity.ClusterVersion == versionEntity.ClusterVersion)
                {
                    return false;
                }

                var updatedVersionEntity = await this._kubeClient.UpdateCustomObject(
                    PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL, versionEntity);

                if (updatedVersionEntity == null)
                {
                    return false;
                }

                var updated = await this._kubeClient.UpdateCustomObject(PROVIDER_MODEL_VERSION, SiloEntity.PLURAL, siloEntity);

                return updated != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Exception exc)
            {
                this._logger?.LogError(exc, "Unable to update Silo Entry.");

                throw;
            }
        }

        private string GetSiloObjectDefinitionName() => $"{SiloEntity.PLURAL}.{this._group}";

        private string GetClusterVersionObjectDefinitionName() => $"{ClusterVersionEntity.PLURAL}.{this._group}";

        private async Task TryCreateResources()
        {
            try
            {
                var clusterVersionDefinition = new CustomResourceDefinition
                {
                    ApiVersion = KUBE_API_VERSION,
                    Kind = CustomResourceDefinition.KIND,
                    Metadata = new ObjectMetadata
                    {
                        Name = this.GetClusterVersionObjectDefinitionName()
                    },
                    Spec = new CustomResourceDefinitionSpec
                    {
                        Group = this._group,
                        Version = PROVIDER_MODEL_VERSION,
                        Scope = NAMESPACED,
                        Names = new CustomResourceDefinitionNames
                        {
                            Plural = ClusterVersionEntity.PLURAL,
                            Singular = ClusterVersionEntity.SINGULAR,
                            Kind = ClusterVersionEntity.KIND,
                            ShortNames = new List<string> { ClusterVersionEntity.SHORT_NAME }
                        }
                    }
                };

                await this._kubeClient.CreateCRD(clusterVersionDefinition);

                var siloDefinition = new CustomResourceDefinition
                {
                    ApiVersion = KUBE_API_VERSION,
                    Kind = CustomResourceDefinition.KIND,
                    Metadata = new ObjectMetadata
                    {
                        Name = this.GetSiloObjectDefinitionName()
                    },
                    Spec = new CustomResourceDefinitionSpec
                    {
                        Group = this._group,
                        Version = PROVIDER_MODEL_VERSION,
                        Scope = NAMESPACED,
                        Names = new CustomResourceDefinitionNames
                        {
                            Plural = SiloEntity.PLURAL,
                            Singular = SiloEntity.SINGULAR,
                            Kind = SiloEntity.KIND,
                            ShortNames = new List<string> { SiloEntity.SHORT_NAME }
                        }
                    }
                };

                await this._kubeClient.CreateCRD(siloDefinition);
            }
            catch (Exception exc)
            {
                // TODO: Handle conflicts better when the schema is already deployed
                this._logger?.LogWarning(exc, "We tried to create the resources but fail. Ignoring for now...");
            }
        }

        private async Task TryInitClusterVersion()
        {
            try
            {
                var version = await this._kubeClient.GetCustomObject<ClusterVersionEntity>(
                    this._clusterOptions.ClusterId, PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL);

                if (version == null)
                {
                    version = new ClusterVersionEntity
                    {
                        ClusterId = this._clusterOptions.ClusterId,
                        ClusterVersion = 0,
                        Kind = ClusterVersionEntity.KIND,
                        ApiVersion = $"{this._group}/{PROVIDER_MODEL_VERSION}",
                        Metadata = new ObjectMetadata { Name = this._clusterOptions.ClusterId }
                    };

                    var created = await this._kubeClient.CreateCustomObject(PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL, version);

                    if (created != null)
                    {
                        this._logger?.Info($"Created new Cluster Version entity for Cluster {this._clusterOptions.ClusterId}.");
                    }
                }
                else
                {
                    this._logger?.Info($"Cluster {this._clusterOptions.ClusterId} already exist. Trying to join it.");
                }
            }
            catch (Exception exc)
            {
                // TODO: Handle conflicts better when the schema is already deployed
                this._logger?.LogWarning(exc, "We tried to Initialize ClusterVersion but fail. Ignoring for now...");
            }
        }

        private async Task TryDeleteResources()
        {
            try
            {
                var versions = await this._kubeClient.ListCustomObjects<ClusterVersionEntity>(
                    PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL);

                if (versions != null)
                {
                    foreach (var ver in versions)
                    {
                        await this._kubeClient.DeleteCustomObject(ver.Metadata.Name,
                            PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL);
                    }
                }

                var silos = await this._kubeClient.ListCustomObjects<SiloEntity>(PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);

                if (silos != null)
                {
                    foreach (var silo in silos)
                    {
                        await this._kubeClient.DeleteCustomObject(silo.Metadata.Name, PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);
                    }
                }

                var definitions = await this._kubeClient.ListCRDs();

                var toRemove = definitions.Where(d =>
                    d.Metadata.Name == this.GetSiloObjectDefinitionName() ||
                    d.Metadata.Name == this.GetClusterVersionObjectDefinitionName()).ToList();

                foreach (var def in toRemove)
                {
                    await this._kubeClient.DeleteCRD(def);
                }
            }
            catch (Exception exc)
            {
                this._logger?.LogWarning(exc, "We tried to delete the resources but fail. Ignoring for now...");
            }
        }

        private async Task<ClusterVersionEntity> GetClusterVersion()
        {
            var versions = await this._kubeClient.ListCustomObjects<ClusterVersionEntity>(PROVIDER_MODEL_VERSION, ClusterVersionEntity.PLURAL);

            if (versions == null) return null;

            return versions.FirstOrDefault(v => v.ClusterId == this._clusterOptions.ClusterId &&
                v.Metadata.Name == this._clusterOptions.ClusterId);
        }

        private async Task<IReadOnlyList<SiloEntity>> GetSilos()
        {
            var silos = await this._kubeClient.ListCustomObjects<SiloEntity>(PROVIDER_MODEL_VERSION, SiloEntity.PLURAL);

            return silos.Where(s => s.ClusterId == this._clusterOptions.ClusterId).ToList();
        }

        private static MembershipEntry ParseEntity(SiloEntity entity)
        {
            var entry = new MembershipEntry
            {
                HostName = entity.Hostname,
                Status = entity.Status
            };

            if (entity.ProxyPort.HasValue)
                entry.ProxyPort = entity.ProxyPort.Value;

            entry.SiloAddress = SiloAddress.New(new IPEndPoint(IPAddress.Parse(entity.Address), entity.Port), entity.Generation);

            entry.SiloName = entity.SiloName;

            entry.StartTime = entity.StartTime.UtcDateTime;

            entry.IAmAliveTime = entity.IAmAliveTime.UtcDateTime;

            var suspectingSilos = new List<SiloAddress>();
            var suspectingTimes = new List<DateTime>();

            foreach (var silo in entity.SuspectingSilos)
            {
                suspectingSilos.Add(SiloAddress.FromParsableString(silo));
            }

            foreach (var time in entity.SuspectingTimes)
            {
                suspectingTimes.Add(LogFormatter.ParseDate(time));
            }

            if (suspectingSilos.Count != suspectingTimes.Count)
            {
                throw new OrleansException($"SuspectingSilos.Length of {suspectingSilos.Count} as read from Kubernetes is not equal to SuspectingTimes.Length of {suspectingTimes.Count}");
            }

            for (int i = 0; i < suspectingSilos.Count; i++)
            {
                entry.AddSuspector(suspectingSilos[i], suspectingTimes[i]);
            }

            return entry;
        }

        private SiloEntity ConvertToEntity(MembershipEntry membershipEntry)
        {
            var tableEntry = new SiloEntity
            {
                Metadata = new ObjectMetadata { Name = ConstructSiloEntityId(membershipEntry.SiloAddress) },
                ClusterId = this._clusterOptions.ClusterId,
                Address = membershipEntry.SiloAddress.Endpoint.Address.ToString(),
                Port = membershipEntry.SiloAddress.Endpoint.Port,
                Generation = membershipEntry.SiloAddress.Generation,
                Hostname = membershipEntry.HostName,
                Status = membershipEntry.Status,
                ProxyPort = membershipEntry.ProxyPort,
                SiloName = membershipEntry.SiloName,
                StartTime = membershipEntry.StartTime,
                IAmAliveTime = membershipEntry.IAmAliveTime,
                Kind = SiloEntity.KIND,
                ApiVersion = $"{this._group}/{PROVIDER_MODEL_VERSION}",
            };

            if (membershipEntry.SuspectTimes != null)
            {
                foreach (var tuple in membershipEntry.SuspectTimes)
                {
                    tableEntry.SuspectingSilos.Add(tuple.Item1.ToParsableString());
                    tableEntry.SuspectingTimes.Add(LogFormatter.PrintDate(tuple.Item2));
                }
            }

            return tableEntry;
        }

        private ClusterVersionEntity BuildVersionEntity(TableVersion tableVersion)
        {
            return new ClusterVersionEntity
            {
                ClusterId = this._clusterOptions.ClusterId,
                ClusterVersion = tableVersion.Version,
                Metadata = new ObjectMetadata
                {
                    Name = this._clusterOptions.ClusterId,
                    ResourceVersion = tableVersion.VersionEtag
                },
                Kind = ClusterVersionEntity.KIND,
                ApiVersion = $"{this._group}/{PROVIDER_MODEL_VERSION}",
            };
        }

        private static string ConstructSiloEntityId(SiloAddress silo) => $"{silo.Endpoint.Address}-{silo.Endpoint.Port}-{silo.Generation}";

        public async Task CleanupDefunctSiloEntries(DateTimeOffset beforeDate)
        {
            var allSilos = await this.GetSilos();
            if (allSilos.Count == 0) return;

            var toDelete = allSilos.Where(s => s.Status == SiloStatus.Dead && s.IAmAliveTime < beforeDate);
            var tasks = new List<Task>();


            foreach (var deadSilo in toDelete)
            {
                tasks.Add(
                    this._kubeClient.DeleteCustomObject(deadSilo.Metadata.Name,
                              PROVIDER_MODEL_VERSION, SiloEntity.PLURAL)
                );
            }

            await Task.WhenAll(tasks);
        }
    }
}
