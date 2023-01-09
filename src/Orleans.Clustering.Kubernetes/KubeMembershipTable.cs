using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Orleans.Clustering.Kubernetes.Models;
using Orleans.Configuration;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Rest;

namespace Orleans.Clustering.Kubernetes;

internal class KubeMembershipTable : IMembershipTable
{
    private readonly ClusterOptions _clusterOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly k8s.IKubernetes _kubeClient;
    private string _namespace;

    public KubeMembershipTable(ILoggerFactory loggerFactory, IOptions<ClusterOptions> clusterOptions, k8s.IKubernetes kubernetesClient)
    {
        this._clusterOptions = clusterOptions.Value;
        this._loggerFactory = loggerFactory;
        this._logger = loggerFactory?.CreateLogger<KubeMembershipTable>();
        this._kubeClient = kubernetesClient;
    }

    public async Task InitializeMembershipTable(bool tryInitTableVersion)
    {
        this._namespace = this.GetNamespace();
        this._logger.LogInformation("Using Kubernetes namespace: {NameSpace}", this._namespace);

        if (tryInitTableVersion)
        {
            await this.TryInitClusterVersion();
        }
    }

    public async Task DeleteMembershipTableEntries(string clusterId)
    {
        var clusterVersion = await this.GetClusterVersion();

        if (clusterVersion != null)
        {
            await this._kubeClient.DeleteNamespacedCustomObjectAsync(
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                ClusterVersionEntity.PLURAL,
                clusterVersion.Metadata.Name);
        }

        var silos = await this.GetSilos();

        foreach (var silo in silos)
        {
            await this._kubeClient.DeleteNamespacedCustomObjectAsync(
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                SiloEntity.PLURAL,
                silo.Metadata.Name);
        }
    }

    public async Task<bool> InsertRow(MembershipEntry entry, TableVersion tableVersion)
    {
        try
        {
            var siloEntity = this.ConvertToEntity(entry);
            var versionEntity = this.BuildVersionEntity(tableVersion);

            SiloEntity existentSiloEntry = default;

            try
            {
                existentSiloEntry = ((JObject)await this._kubeClient.GetNamespacedCustomObjectAsync(
                    Constants.ORLEANS_GROUP,
                    Constants.PROVIDER_MODEL_VERSION,
                    this._namespace,
                    SiloEntity.PLURAL,
                    siloEntity.Metadata.Name
                ))?.ToObject<SiloEntity>();
            }
            catch (HttpOperationException ex)
            {
                if (ex.Response.StatusCode != HttpStatusCode.NotFound)
                    throw;
            }

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

            var updatedVersionEntity = await this._kubeClient.ReplaceNamespacedCustomObjectAsync(
                versionEntity,
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                ClusterVersionEntity.PLURAL,
                versionEntity.Metadata.Name
            );

            if (updatedVersionEntity == null)
            {
                return false;
            }

            var createdSiloEntity = await this._kubeClient.CreateNamespacedCustomObjectAsync(
                siloEntity,
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                SiloEntity.PLURAL
            );

            return createdSiloEntity != null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception exc)
        {
            this._logger?.LogError(exc, "Unable to insert Silo Entry");

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
                this._logger?.LogError("Initial ClusterVersionEntity entity doesn't exist");
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
            this._logger?.LogWarning(exc, "Failure reading all silo entries for cluster id {ClusterId}", this._clusterOptions.ClusterId);

            throw;
        }
    }

    public async Task<MembershipTableData> ReadRow(SiloAddress key)
    {
        var name = ConstructSiloEntityId(key);

        try
        {
            var versionEntity = await this.GetClusterVersion();
            SiloEntity entity = default;

            try
            {
                entity = ((JObject)await this._kubeClient.GetNamespacedCustomObjectAsync(
                    Constants.ORLEANS_GROUP,
                    Constants.PROVIDER_MODEL_VERSION,
                    this._namespace,
                    SiloEntity.PLURAL,
                    name
                ))?.ToObject<SiloEntity>();
            }
            catch (HttpOperationException ex)
            {
                if (ex.Response.StatusCode != HttpStatusCode.NotFound)
                    throw;
            }

            var version = default(TableVersion);

            if (versionEntity != null)
            {
                version = new TableVersion(versionEntity.ClusterVersion, versionEntity.Metadata.ResourceVersion);
            }
            else
            {
                this._logger?.LogError("Initial ClusterVersionEntity entity doesn't exist");
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
            this._logger?.LogError(exc, "Failure reading silo entry {Name} for cluster id {ClusterId}", name, this._clusterOptions.ClusterId);

            throw;
        }
    }

    public async Task UpdateIAmAlive(MembershipEntry entry)
    {
        var name = ConstructSiloEntityId(entry.SiloAddress);

        try
        {
            SiloEntity siloEntity = default;

            try
            {
                siloEntity = ((JObject)await this._kubeClient.GetNamespacedCustomObjectAsync(
                    Constants.ORLEANS_GROUP,
                    Constants.PROVIDER_MODEL_VERSION,
                    this._namespace,
                    SiloEntity.PLURAL,
                    name
                ))?.ToObject<SiloEntity>();
            }
            catch (HttpOperationException ex)
            {
                if (ex.Response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException($"Unable to find silo entry {name}.");

                throw;
            }

            siloEntity.IAmAliveTime = entry.IAmAliveTime;

            await this._kubeClient.ReplaceNamespacedCustomObjectAsync(
                siloEntity,
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                SiloEntity.PLURAL,
                siloEntity.Metadata.Name
            );
        }
        catch (Exception exc)
        {
            this._logger?.LogError(exc, "Unable to update Silo Entry {Name}", name);

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

            var updatedVersionEntity = await this._kubeClient.ReplaceNamespacedCustomObjectAsync(
                versionEntity,
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                ClusterVersionEntity.PLURAL,
                versionEntity.Metadata.Name
            );

            if (updatedVersionEntity == null)
            {
                return false;
            }

            var updated = await this._kubeClient.ReplaceNamespacedCustomObjectAsync(
                siloEntity,
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                SiloEntity.PLURAL,
                siloEntity.Metadata.Name
            );

            return updated != null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception exc)
        {
            this._logger?.LogError(exc, "Unable to update Silo Entry");

            throw;
        }
    }

    private string GetSiloObjectDefinitionName() => $"{SiloEntity.PLURAL}.{Constants.ORLEANS_GROUP}";

    private string GetClusterVersionObjectDefinitionName() => $"{ClusterVersionEntity.PLURAL}.{Constants.ORLEANS_GROUP}";

    private async Task TryInitClusterVersion()
    {
        try
        {
            ClusterVersionEntity version = default;

            try
            {
                version = ((JObject)await this._kubeClient.GetNamespacedCustomObjectAsync(
                    Constants.ORLEANS_GROUP,
                    Constants.PROVIDER_MODEL_VERSION,
                    this._namespace,
                    ClusterVersionEntity.PLURAL,
                    this._clusterOptions.ClusterId
                ))?.ToObject<ClusterVersionEntity>();
            }
            catch (HttpOperationException ex)
            {
                if (ex.Response.StatusCode != HttpStatusCode.NotFound)
                {
                    this._logger.LogError(ex, "Unable to initialize cluster version: {ExMessage}", ex.Message);
                    throw;
                }
            }

            if (version == null)
            {
                version = new ClusterVersionEntity
                {
                    ClusterId = this._clusterOptions.ClusterId,
                    ClusterVersion = 0,
                    ApiVersion = $"{Constants.ORLEANS_GROUP}/{Constants.PROVIDER_MODEL_VERSION}",
                    Metadata = new V1ObjectMeta { Name = this._clusterOptions.ClusterId }
                };

                var created = ((JObject)await this._kubeClient.CreateNamespacedCustomObjectAsync(
                    version,
                    Constants.ORLEANS_GROUP,
                    Constants.PROVIDER_MODEL_VERSION,
                    this._namespace,
                    ClusterVersionEntity.PLURAL
                ))?.ToObject<ClusterVersionEntity>();

                if (created != null)
                {
                    this._logger?.LogInformation("Created new Cluster Version entity for Cluster {ClusterId}", this._clusterOptions.ClusterId);
                }
            }
            else
            {
                this._logger?.LogInformation("Cluster {ClusterId} already exists. Trying to join it", this._clusterOptions.ClusterId);
            }
        }
        catch (Exception exc)
        {
            // TODO: Handle conflicts better when the schema is already deployed
            this._logger?.LogWarning(exc, "We tried to Initialize ClusterVersion but fail. Ignoring for now...");
        }
    }

    private async Task<ClusterVersionEntity> GetClusterVersion()
    {
        var versions = ((JObject)await this._kubeClient.ListNamespacedCustomObjectAsync(
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace, ClusterVersionEntity.PLURAL)
            )?["items"]?.ToObject<ClusterVersionEntity[]>();

        if (versions == null) return null;

        return versions.FirstOrDefault(v => v.ClusterId == this._clusterOptions.ClusterId &&
                                            v.Metadata.Name == this._clusterOptions.ClusterId);
    }

    private async Task<IReadOnlyList<SiloEntity>> GetSilos()
    {
        var silos = ((JObject)await this._kubeClient.ListNamespacedCustomObjectAsync(
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace, SiloEntity.PLURAL)
            )?["items"]?.ToObject<SiloEntity[]>();

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
            Metadata = new V1ObjectMeta { Name = ConstructSiloEntityId(membershipEntry.SiloAddress) },
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
            ApiVersion = $"{Constants.ORLEANS_GROUP}/{Constants.PROVIDER_MODEL_VERSION}",
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
            Metadata = new V1ObjectMeta
            {
                Name = this._clusterOptions.ClusterId,
                ResourceVersion = tableVersion.VersionEtag
            },
            Kind = ClusterVersionEntity.KIND,
            ApiVersion = $"{Constants.ORLEANS_GROUP}/{Constants.PROVIDER_MODEL_VERSION}",
        };
    }

    private static string ConstructSiloEntityId(SiloAddress silo) => $"{silo.Endpoint.Address}-{silo.Endpoint.Port}-{silo.Generation}";

    public async Task CleanupDefunctSiloEntries(DateTimeOffset beforeDate)
    {
        var allSilos = await this.GetSilos();
        if (allSilos.Count == 0) return;

        var toDelete = allSilos.Where(s => s.Status == SiloStatus.Dead && s.IAmAliveTime < beforeDate);

        foreach (var deadSilo in toDelete)
        {
            await this._kubeClient.DeleteNamespacedCustomObjectAsync(
                Constants.ORLEANS_GROUP,
                Constants.PROVIDER_MODEL_VERSION,
                this._namespace,
                SiloEntity.PLURAL,
                deadSilo.Metadata.Name
            );
        }
    }

    private string GetNamespace()
    {
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