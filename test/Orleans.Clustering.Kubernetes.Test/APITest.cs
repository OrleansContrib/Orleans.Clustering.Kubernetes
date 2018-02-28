using Orleans.Clustering.Kubernetes.API;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Orleans.Clustering.Kubernetes.Test
{
    public class APITest : IClassFixture<APIFixture>
    {
        private KubeClient _kubeClient;

        public APITest(APIFixture fixture)
        {
            this._kubeClient = fixture.Client;
        }

        [Fact]
        public async Task CRDTest()
        {
            var crds = await this._kubeClient.ListCRDs();

            var crdToCleanUp = crds.SingleOrDefault(c => c.Metadata.Name == "crontabs.stable.example.com");

            if (crdToCleanUp != null)
                await this._kubeClient.DeleteCRD(crdToCleanUp);

            var newCrd = new CustomResourceDefinition
            {
                ApiVersion = "apiextensions.k8s.io/v1beta1",
                Kind = "CustomResourceDefinition",
                Metadata = new ObjectMetadata
                {
                    Name = "crontabs.stable.example.com"
                },
                Spec = new CustomResourceDefinitionSpec
                {
                    Group = "stable.example.com",
                    Version = "v1",
                    Scope = "Namespaced",
                    Names = new CustomResourceDefinitionNames
                    {
                        Plural = "crontabs",
                        Singular = "crontab",
                        Kind = "CronTab",
                        ShortNames = new List<string> { "ct" }
                    }
                }
            };

            var crdCreated = await this._kubeClient.CreateCRD(newCrd);

            crds = await this._kubeClient.ListCRDs();
            Assert.NotNull(crds);
            Assert.True(crds.Count == 1);

            var newCustomObj = new TestCustomObject
            {
                ApiVersion = "stable.example.com/v1",
                Kind = "CronTab",
                Metadata = new ObjectMetadata
                {
                    Name = "my-new-cron-object"
                },
                CronSpec = "* * * * */5",
                Image = "my-awesome-cron-image"
            };

            var customObjCreated = await this._kubeClient.CreateCustomObject("v1", "crontabs", newCustomObj);
            Assert.NotNull(customObjCreated);

            var customObjs = await this._kubeClient.ListCustomObjects<TestCustomObject>("v1", "crontabs");
            Assert.NotNull(customObjs);
            Assert.True(customObjs.Count == 1);

            var customObjFound = await this._kubeClient.GetCustomObject<TestCustomObject>("my-new-cron-object", "v1", "crontabs");
            Assert.NotNull(customObjFound);

            await this._kubeClient.DeleteCustomObject(customObjCreated.Metadata.Name, "v1", "crontabs");

            crdToCleanUp = crds.SingleOrDefault(c => c.Metadata.Name == "crontabs.stable.example.com");
            Assert.NotNull(crdToCleanUp);

            await this._kubeClient.DeleteCRD(crdToCleanUp);
        }

        private class TestCustomObject : CustomObject
        {
            public string CronSpec { get; set; }

            public string Image { get; set; }
        }
    }

    public class APIFixture
    {
        internal KubeClient Client { get; set; } = new KubeClient(null, null, "http://localhost:8001", group: "stable.example.com");
    }
}
