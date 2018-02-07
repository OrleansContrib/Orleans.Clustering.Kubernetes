using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Clustering.Kubernetes;
using Orleans.Clustering.Kubernetes.Options;
using Orleans.Clustering.Kubernetes.Test;
using Orleans.Messaging;
using Orleans.Runtime;
using System.Threading.Tasks;
using Xunit;

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
        //TestUtils.CheckForAzureStorage();
        var options = new KubeClusteringOptions()
        {
            APIEndpoint = "http://localhost:8001",
            CanCreateResources = true,
            DropResourcesOnInit = true,
            Namespace = "orleanstest"
        };
        return new KubeMembershipTable(this.loggerFactory,
            Microsoft.Extensions.Options.Options.Create(new SiloOptions { ClusterId = this.clusterId }), Microsoft.Extensions.Options.Options.Create(options));
    }

    protected override IGatewayListProvider CreateGatewayListProvider(ILogger logger)
    {
        var options = new KubeGatewayOptions()
        {
            APIEndpoint = "http://localhost:8001",
            Namespace = "orleanstest"
        };
        return new KubeGatewayListProvider(this.loggerFactory, Microsoft.Extensions.Options.Options.Create(options), this.clientConfiguration);
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
}
