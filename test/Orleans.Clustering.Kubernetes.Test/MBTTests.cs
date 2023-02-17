using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Messaging;
using System.Threading.Tasks;
using Xunit;

namespace Orleans.Clustering.Kubernetes.Test;

/// <summary>
/// Tests for operation of Orleans Membership Table using Kuberneters Custom Objects
/// </summary>
public class KubeTests : MembershipTableTestsBase/*, IClassFixture<AzureStorageBasicTests>*/
{
    public KubeTests() : base(CreateFilters())
    {
    }

    private static LoggerFilterOptions CreateFilters()
    {
        var filters = new LoggerFilterOptions();
        //filters.AddFilter(typeof(Orleans.Clustering.CosmosDB.AzureTableDataManager<>).FullName, LogLevel.Trace);
        //filters.AddFilter(typeof(OrleansSiloInstanceManager).FullName, LogLevel.Trace);
        //filters.AddFilter("Orleans.Storage", LogLevel.Trace);
        return filters;
    }

    protected override IMembershipTable CreateMembershipTable(ILogger logger)
    {
        return new KubeMembershipTable(this.loggerFactory, Options.Create(new ClusterOptions { ClusterId = this.clusterId }), new k8s.Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile()));
    }

    protected override IGatewayListProvider CreateGatewayListProvider(ILogger logger)
    {
        var options = new KubeGatewayOptions();
        return new KubeGatewayListProvider(
            this.loggerFactory,
            Options.Create(new ClusterOptions { ClusterId = this.clusterId }),
            Options.Create(new GatewayOptions()),
            Options.Create(options),
            new k8s.Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile())
        );
    }

    protected override Task<string> GetConnectionString()
    {
        return Task.FromResult("");
    }

    [Fact]
    public async Task GetGateways()
    {
        await MembershipTable_GetGateways();
    }

    [Fact]
    public async Task ReadAll_EmptyTable()
    {
        await MembershipTable_ReadAll_EmptyTable();
    }

    [Fact]
    public async Task InsertRow()
    {
        await MembershipTable_InsertRow();
    }

    [Fact]
    public async Task ReadRow_Insert_Read()
    {
        await MembershipTable_ReadRow_Insert_Read();
    }

    [Fact]
    public async Task ReadAll_Insert_ReadAll()
    {
        await MembershipTable_ReadAll_Insert_ReadAll();
    }

    [Fact]
    public async Task UpdateRow()
    {
        await MembershipTable_UpdateRow();
    }

    [Fact]
    public async Task UpdateIAmAlive()
    {
        await MembershipTable_UpdateIAmAlive();
    }

    [Fact]
    public async Task CleanUpdDeadSilos()
    {
        await MembershipTable_CleanUp();
    }
}
