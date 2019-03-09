using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Messaging;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Orleans.Clustering.Kubernetes.Test
{
    internal static class SiloInstanceTableTestConstants
    {
        internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

        internal static readonly bool DeleteEntriesAfterTest = true; // false; // Set to false for Debug mode

        internal static readonly string INSTANCE_STATUS_CREATED = SiloStatus.Created.ToString();  //"Created";
        internal static readonly string INSTANCE_STATUS_ACTIVE = SiloStatus.Active.ToString();    //"Active";
        internal static readonly string INSTANCE_STATUS_DEAD = SiloStatus.Dead.ToString();        //"Dead";
    }

    [Collection("Default")]
    public abstract class MembershipTableTestsBase : IDisposable //, IClassFixture<ConnectionStringFixture>
    {
        private static readonly string hostName = Dns.GetHostName();
        private readonly ILogger logger;
        private readonly IMembershipTable membershipTable;
        private readonly IGatewayListProvider gatewayListProvider;
        protected readonly string clusterId;
        protected ILoggerFactory loggerFactory;
        protected IOptions<ClusterOptions> clusterOptions;
        //protected readonly ClientConfiguration clientConfiguration;
        protected MembershipTableTestsBase(/*ConnectionStringFixture fixture, TestEnvironmentFixture environment, */LoggerFilterOptions filters)
        {
            //this.environment = environment;
            //loggerFactory = TestingUtils.CreateDefaultLoggerFactory($"{this.GetType()}.log", filters);
            //logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.clusterId = "test-" + Guid.NewGuid();

            this.logger?.Info("ClusterId={0}", this.clusterId);

            //fixture.InitializeConnectionStringAccessor(GetConnectionString);
            //this.connectionString = fixture.ConnectionString;
            this.clusterOptions = Options.Create(new ClusterOptions { ClusterId = this.clusterId });
           
            var adoVariant = GetAdoInvariant();

            this.membershipTable = CreateMembershipTable(this.logger);
            this.membershipTable.InitializeMembershipTable(true).WithTimeout(TimeSpan.FromMinutes(10)).Wait();

            //this.clientConfiguration = new ClientConfiguration
            //{
            //    ClusterId = this.clusterId,
            //    AdoInvariant = adoVariant,
            //    //DataConnectionString = fixture.ConnectionString
            //};

            this.gatewayListProvider = CreateGatewayListProvider(this.logger);
            this.gatewayListProvider.InitializeGatewayListProvider().WithTimeout(TimeSpan.FromMinutes(3)).Wait();
        }

        //public IGrainFactory GrainFactory => this.environment.GrainFactory;

        //public IGrainReferenceConverter GrainReferenceConverter => this.environment.Services.GetRequiredService<IGrainReferenceConverter>();

        //public IServiceProvider Services => this.environment.Services;

        public void Dispose()
        {
            if (this.membershipTable != null && SiloInstanceTableTestConstants.DeleteEntriesAfterTest)
            {
                this.membershipTable.DeleteMembershipTableEntries(this.clusterId).Wait();
            }
            //this.loggerFactory.Dispose();
        }

        protected abstract IGatewayListProvider CreateGatewayListProvider(ILogger logger);
        protected abstract IMembershipTable CreateMembershipTable(ILogger logger);
        protected abstract Task<string> GetConnectionString();

        protected virtual string GetAdoInvariant()
        {
            return null;
        }

        protected async Task MembershipTable_GetGateways()
        {
            var membershipEntries = Enumerable.Range(0, 10).Select(i => CreateMembershipEntryForTest()).ToArray();

            membershipEntries[3].Status = SiloStatus.Active;
            membershipEntries[3].ProxyPort = 0;
            membershipEntries[5].Status = SiloStatus.Active;
            membershipEntries[9].Status = SiloStatus.Active;

            var data = await this.membershipTable.ReadAll();
            Assert.NotNull(data);
            Assert.Equal(0, data.Members.Count);

            var version = data.Version;
            foreach (var membershipEntry in membershipEntries)
            {
                Assert.True(await this.membershipTable.InsertRow(membershipEntry, version.Next()));
                version = (await this.membershipTable.ReadRow(membershipEntry.SiloAddress)).Version;
            }

            var gateways = await this.gatewayListProvider.GetGateways();

            var entries = new List<string>(gateways.Select(g => g.ToString()));

            // only members with a non-zero Gateway port
            Assert.DoesNotContain(membershipEntries[3].SiloAddress.ToGatewayUri().ToString(), entries);

            // only Active members
            Assert.Contains(membershipEntries[5].SiloAddress.ToGatewayUri().ToString(), entries);
            Assert.Contains(membershipEntries[9].SiloAddress.ToGatewayUri().ToString(), entries);
            Assert.Equal(2, entries.Count);
        }

        protected async Task MembershipTable_ReadAll_EmptyTable()
        {
            var data = await this.membershipTable.ReadAll();
            Assert.NotNull(data);

            this.logger?.Info("Membership.ReadAll returned VableVersion={0} Data={1}", data.Version, data);

            Assert.Equal(0, data.Members.Count);
            Assert.NotNull(data.Version.VersionEtag);
            Assert.Equal(0, data.Version.Version);
        }

        protected async Task MembershipTable_InsertRow(bool extendedProtocol = true)
        {
            var membershipEntry = CreateMembershipEntryForTest();

            var data = await this.membershipTable.ReadAll();
            Assert.NotNull(data);
            Assert.Equal(0, data.Members.Count);

            TableVersion nextTableVersion = data.Version.Next();

            bool ok = await this.membershipTable.InsertRow(membershipEntry, nextTableVersion);
            Assert.True(ok, "InsertRow failed");

            data = await this.membershipTable.ReadAll();

            if (extendedProtocol)
                Assert.Equal(1, data.Version.Version);

            Assert.Equal(1, data.Members.Count);
        }

        protected async Task MembershipTable_ReadRow_Insert_Read(bool extendedProtocol = true)
        {
            MembershipTableData data = await this.membershipTable.ReadAll();

            this.logger?.Info("Membership.ReadAll returned VableVersion={0} Data={1}", data.Version, data);

            Assert.Equal(0, data.Members.Count);

            TableVersion newTableVersion = data.Version.Next();

            MembershipEntry newEntry = CreateMembershipEntryForTest();
            bool ok = await this.membershipTable.InsertRow(newEntry, newTableVersion);

            Assert.True(ok, "InsertRow failed");

            ok = await this.membershipTable.InsertRow(newEntry, newTableVersion);
            Assert.False(ok, "InsertRow should have failed - same entry, old table version");

            if (extendedProtocol)
            {
                ok = await this.membershipTable.InsertRow(CreateMembershipEntryForTest(), newTableVersion);
                Assert.False(ok, "InsertRow should have failed - new entry, old table version");
            }

            data = await this.membershipTable.ReadAll();

            if (extendedProtocol)
                Assert.Equal(1, data.Version.Version);

            TableVersion nextTableVersion = data.Version.Next();

            ok = await this.membershipTable.InsertRow(newEntry, nextTableVersion);
            Assert.False(ok, "InsertRow should have failed - duplicate entry");

            data = await this.membershipTable.ReadAll();

            Assert.Equal(1, data.Members.Count);

            data = await this.membershipTable.ReadRow(newEntry.SiloAddress);
            if (extendedProtocol)
                Assert.Equal(newTableVersion.Version, data.Version.Version);

            this.logger?.Info("Membership.ReadRow returned VableVersion={0} Data={1}", data.Version, data);

            Assert.Equal(1, data.Members.Count);
            Assert.NotNull(data.Version.VersionEtag);
            if (extendedProtocol)
            {
                Assert.NotEqual(newTableVersion.VersionEtag, data.Version.VersionEtag);
                Assert.Equal(newTableVersion.Version, data.Version.Version);
            }
            var membershipEntry = data.Members[0].Item1;
            string eTag = data.Members[0].Item2;
            this.logger?.Info("Membership.ReadRow returned MembershipEntry ETag={0} Entry={1}", eTag, membershipEntry);

            Assert.NotNull(eTag);
            Assert.NotNull(membershipEntry);
        }

        protected async Task MembershipTable_ReadAll_Insert_ReadAll(bool extendedProtocol = true)
        {
            MembershipTableData data = await this.membershipTable.ReadAll();
            this.logger?.Info("Membership.ReadAll returned VableVersion={0} Data={1}", data.Version, data);

            Assert.Equal(0, data.Members.Count);

            TableVersion newTableVersion = data.Version.Next();

            MembershipEntry newEntry = CreateMembershipEntryForTest();
            bool ok = await this.membershipTable.InsertRow(newEntry, newTableVersion);

            Assert.True(ok, "InsertRow failed");

            data = await this.membershipTable.ReadAll();
            this.logger?.Info("Membership.ReadAll returned VableVersion={0} Data={1}", data.Version, data);

            Assert.Equal(1, data.Members.Count);
            Assert.NotNull(data.Version.VersionEtag);

            if (extendedProtocol)
            {
                Assert.NotEqual(newTableVersion.VersionEtag, data.Version.VersionEtag);
                Assert.Equal(newTableVersion.Version, data.Version.Version);
            }

            var membershipEntry = data.Members[0].Item1;
            string eTag = data.Members[0].Item2;
            this.logger?.Info("Membership.ReadAll returned MembershipEntry ETag={0} Entry={1}", eTag, membershipEntry);

            Assert.NotNull(eTag);
            Assert.NotNull(membershipEntry);
        }

        protected async Task MembershipTable_UpdateRow(bool extendedProtocol = true)
        {
            var tableData = await this.membershipTable.ReadAll();
            Assert.NotNull(tableData.Version);

            Assert.Equal(0, tableData.Version.Version);
            Assert.Equal(0, tableData.Members.Count);

            for (int i = 1; i < 10; i++)
            {
                var siloEntry = CreateMembershipEntryForTest();

                siloEntry.SuspectTimes =
                    new List<Tuple<SiloAddress, DateTime>>
                    {
                        new Tuple<SiloAddress, DateTime>(CreateSiloAddressForTest(), GetUtcNowWithSecondsResolution().AddSeconds(1)),
                        new Tuple<SiloAddress, DateTime>(CreateSiloAddressForTest(), GetUtcNowWithSecondsResolution().AddSeconds(2))
                    };

                TableVersion tableVersion = tableData.Version.Next();

                this.logger?.Info("Calling InsertRow with Entry = {0} TableVersion = {1}", siloEntry, tableVersion);
                bool ok = await this.membershipTable.InsertRow(siloEntry, tableVersion);

                Assert.True(ok, "InsertRow failed");

                tableData = await this.membershipTable.ReadAll();

                var etagBefore = tableData.Get(siloEntry.SiloAddress).Item2;

                Assert.NotNull(etagBefore);

                if (extendedProtocol)
                {
                    this.logger?.Info("Calling UpdateRow with Entry = {0} correct eTag = {1} old version={2}", siloEntry,
                                etagBefore, tableVersion != null ? tableVersion.ToString() : "null");
                    ok = await this.membershipTable.UpdateRow(siloEntry, etagBefore, tableVersion);
                    Assert.False(ok, $"row update should have failed - Table Data = {tableData}");
                    tableData = await this.membershipTable.ReadAll();
                }

                tableVersion = tableData.Version.Next();

                this.logger?.Info("Calling UpdateRow with Entry = {0} correct eTag = {1} correct version={2}", siloEntry,
                    etagBefore, tableVersion != null ? tableVersion.ToString() : "null");

                ok = await this.membershipTable.UpdateRow(siloEntry, etagBefore, tableVersion);

                Assert.True(ok, $"UpdateRow failed - Table Data = {tableData}");

                this.logger?.Info("Calling UpdateRow with Entry = {0} old eTag = {1} old version={2}", siloEntry,
                    etagBefore, tableVersion != null ? tableVersion.ToString() : "null");
                ok = await this.membershipTable.UpdateRow(siloEntry, etagBefore, tableVersion);
                Assert.False(ok, $"row update should have failed - Table Data = {tableData}");

                tableData = await this.membershipTable.ReadAll();

                var tuple = tableData.Get(siloEntry.SiloAddress);

                //Assert.Equal(tuple.Item1.ToFullString(true), siloEntry.ToFullString(true));

                var etagAfter = tuple.Item2;

                if (extendedProtocol)
                {
                    this.logger?.Info("Calling UpdateRow with Entry = {0} correct eTag = {1} old version={2}", siloEntry,
                                etagAfter, tableVersion != null ? tableVersion.ToString() : "null");

                    ok = await this.membershipTable.UpdateRow(siloEntry, etagAfter, tableVersion);

                    Assert.False(ok, $"row update should have failed - Table Data = {tableData}");
                }

                tableData = await this.membershipTable.ReadAll();

                etagBefore = etagAfter;

                etagAfter = tableData.Get(siloEntry.SiloAddress).Item2;

                Assert.Equal(etagBefore, etagAfter);
                Assert.NotNull(tableData.Version);
                if (extendedProtocol)
                    Assert.Equal(tableVersion.Version, tableData.Version.Version);

                Assert.Equal(i, tableData.Members.Count);
            }
        }

        protected async Task MembershipTable_UpdateRowInParallel(bool extendedProtocol = true)
        {
            var tableData = await this.membershipTable.ReadAll();

            var data = CreateMembershipEntryForTest();

            TableVersion newTableVer = tableData.Version.Next();

            var insertions = Task.WhenAll(Enumerable.Range(1, 20).Select(i => this.membershipTable.InsertRow(data, newTableVer)));

            Assert.True((await insertions).Single(x => x), "InsertRow failed");

            await Task.WhenAll(Enumerable.Range(1, 19).Select(async i =>
            {
                bool done;
                do
                {
                    var updatedTableData = await this.membershipTable.ReadAll();
                    var updatedRow = updatedTableData.Get(data.SiloAddress);

                    TableVersion tableVersion = updatedTableData.Version.Next();

                    await Task.Delay(10);
                    done = await this.membershipTable.UpdateRow(updatedRow.Item1, updatedRow.Item2, tableVersion);
                } while (!done);
            })).WithTimeout(TimeSpan.FromSeconds(30));


            tableData = await this.membershipTable.ReadAll();
            Assert.NotNull(tableData.Version);

            if (extendedProtocol)
                Assert.Equal(20, tableData.Version.Version);

            Assert.Equal(1, tableData.Members.Count);
        }

        protected async Task MembershipTable_UpdateIAmAlive(bool extendedProtocol = true)
        {
            MembershipTableData tableData = await this.membershipTable.ReadAll();

            TableVersion newTableVersion = tableData.Version.Next();
            MembershipEntry newEntry = CreateMembershipEntryForTest();
            bool ok = await this.membershipTable.InsertRow(newEntry, newTableVersion);
            Assert.True(ok);


            var amAliveTime = DateTime.UtcNow;

            // This mimics the arguments MembershipOracle.OnIAmAliveUpdateInTableTimer passes in
            var entry = new MembershipEntry
            {
                SiloAddress = newEntry.SiloAddress,
                IAmAliveTime = amAliveTime
            };

            await this.membershipTable.UpdateIAmAlive(entry);

            tableData = await this.membershipTable.ReadAll();
            Tuple<MembershipEntry, string> member = tableData.Members.First();
            // compare that the value is close to what we passed in, but not exactly, as the underlying store can set its own precision settings
            // (ie: in SQL Server this is defined as datetime2(3), so we don't expect precision to account for less than 0.001s values)
            Assert.True((amAliveTime - member.Item1.IAmAliveTime).Duration() < TimeSpan.FromMilliseconds(50), (amAliveTime - member.Item1.IAmAliveTime).Duration().ToString());
        }

        protected async Task MembershipTable_CleanupDefunctSiloEntries(bool extendedProtocol = true)
        {
            MembershipTableData tableData = await this.membershipTable.ReadAll();

            TableVersion newTableVersion = tableData.Version.Next();
            MembershipEntry newEntry = CreateMembershipEntryForTest();
            bool ok = await this.membershipTable.InsertRow(newEntry, newTableVersion);
            Assert.True(ok);
            
            var amAliveTime = DateTime.UtcNow;

            // This mimics the arguments MembershipOracle.OnIAmAliveUpdateInTableTimer passes in
            var entry = new MembershipEntry
            {
                SiloAddress = newEntry.SiloAddress,
                IAmAliveTime = amAliveTime
            };

            await this.membershipTable.UpdateIAmAlive(entry);

            tableData = await this.membershipTable.ReadAll();
            Tuple<MembershipEntry, string> member = tableData.Members.First();
            // compare that the value is close to what we passed in, but not exactly, as the underlying store can set its own precision settings
            // (ie: in SQL Server this is defined as datetime2(3), so we don't expect precision to account for less than 0.001s values)
            Assert.True((amAliveTime - member.Item1.IAmAliveTime).Duration() < TimeSpan.FromMilliseconds(50), (amAliveTime - member.Item1.IAmAliveTime).Duration().ToString());

            await Task.Delay(1000);

            await this.membershipTable.CleanupDefunctSiloEntries(DateTimeOffset.UtcNow);

            tableData = await this.membershipTable.ReadAll();
            Assert.Empty(tableData.Members);
        }

        private static int generation;


        // Utility methods
        private static MembershipEntry CreateMembershipEntryForTest()
        {
            SiloAddress siloAddress = CreateSiloAddressForTest();

            var membershipEntry = new MembershipEntry
            {
                SiloAddress = siloAddress,
                HostName = hostName,
                SiloName = "TestSiloName",
                Status = SiloStatus.Joining,
                ProxyPort = siloAddress.Endpoint.Port,
                StartTime = GetUtcNowWithSecondsResolution(),
                IAmAliveTime = GetUtcNowWithSecondsResolution()
            };

            return membershipEntry;
        }

        private static DateTime GetUtcNowWithSecondsResolution()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        }

        private static SiloAddress CreateSiloAddressForTest()
        {
            var siloAddress = SiloAddressUtils.NewLocalSiloAddress(Interlocked.Increment(ref generation));
            siloAddress.Endpoint.Port = 12345;
            return siloAddress;
        }
    }
}
