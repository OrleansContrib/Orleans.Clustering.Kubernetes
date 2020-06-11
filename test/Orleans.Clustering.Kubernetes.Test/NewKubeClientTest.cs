using Xunit;
using k8s;
using System.Threading.Tasks;
using System.Linq;
using k8s.Models;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Orleans.Clustering.Kubernetes.Test
{
    public class NewKubeClientTest : IClassFixture<KubeFixture>
    {
        private k8s.Kubernetes _kubeClient;

        public NewKubeClientTest(KubeFixture fixture)
        {
            this._kubeClient = fixture.Client;
        }

        [Fact]
        public async Task CRDTest()
        {
            const string crdName = "crontabs.stable.example.com";
            var crds = await this._kubeClient.ListCustomResourceDefinitionAsync();

            var crdToCleanUp = crds.Items.SingleOrDefault(c => c.Metadata.Name == crdName);

            if (crdToCleanUp != null)
            {
                await this._kubeClient.DeleteCustomResourceDefinitionAsync(crdName);
            }

            var newCrd = new V1CustomResourceDefinition
            {
                ApiVersion = "apiextensions.k8s.io/v1",
                Kind = "CustomResourceDefinition",
                Metadata = new V1ObjectMeta
                {
                    Name = "crontabs.stable.example.com"
                },
                Spec = new V1CustomResourceDefinitionSpec
                {
                    Group = "stable.example.com",
                    Versions = new List<V1CustomResourceDefinitionVersion>{
                        new V1CustomResourceDefinitionVersion
                        {
                            Name = "v1",
                            Served = true,
                            Storage = true,
                            Schema = new V1CustomResourceValidation{
                                OpenAPIV3Schema = new V1JSONSchemaProps
                                {
                                    Required = new List<string>{ "CronSpec", "Image" },
                                    Type = "object",
                                    Properties = new Dictionary<string, V1JSONSchemaProps>
                                    {
                                        { "CronSpec", new V1JSONSchemaProps{ Type = "string" } },
                                        { "Image", new V1JSONSchemaProps{ Type = "string" } }
                                    }
                                }
                            }
                        }
                    },
                    Scope = "Namespaced",
                    Names = new V1CustomResourceDefinitionNames
                    {
                        Plural = "crontabs",
                        Singular = "crontab",
                        Kind = "CronTab",
                        ShortNames = new List<string> { "ct" }
                    }
                }
            };

            var crdCreated = await this._kubeClient.CreateCustomResourceDefinitionAsync(newCrd);

            crds = await this._kubeClient.ListCustomResourceDefinitionAsync();
            Assert.NotNull(crds);
            Assert.NotNull(crds.Items.SingleOrDefault(c => c.Metadata.Name == crdName));

            var newCustomObj = new TestCustomObject
            {
                ApiVersion = "stable.example.com/v1",
                Kind = "CronTab",
                Metadata = new V1ObjectMeta
                {
                    Name = "my-new-cron-object"
                },
                CronSpec = "* * * * */5",
                Image = "my-awesome-cron-image"
            };

            var customObjCreated = ((JObject)await this._kubeClient.CreateNamespacedCustomObjectAsync(newCustomObj, "stable.example.com", "v1", "default", "crontabs")).ToObject<TestCustomObject>();
            Assert.NotNull(customObjCreated);

            var a = await this._kubeClient.ListNamespacedCustomObjectAsync("stable.example.com", "v1", "default", "crontabs");

            var customObjs = ((JObject)await this._kubeClient.ListNamespacedCustomObjectAsync("stable.example.com", "v1", "default", "crontabs"))["items"].ToObject<TestCustomObject[]>();
            Assert.NotNull(customObjs);
            Assert.True(customObjs.Length == 1);

            var customObjFound = ((JObject)await this._kubeClient.GetNamespacedCustomObjectAsync("stable.example.com", "v1", "default", "crontabs", "my-new-cron-object")).ToObject<TestCustomObject>();
            Assert.NotNull(customObjFound);

            await this._kubeClient.DeleteNamespacedCustomObjectAsync("stable.example.com", "v1", "default", "crontabs", "my-new-cron-object");

            await this._kubeClient.DeleteCustomResourceDefinitionAsync(crdName);
        }

        private class TestCustomObject
        {
            [JsonProperty("apiVersion")]
            public string ApiVersion { get; set; }

            [JsonProperty("kind")]
            public string Kind { get; set; }

            [JsonProperty("CronSpec")]
            public string CronSpec { get; set; }

            [JsonProperty("Image")]
            public string Image { get; set; }

            [JsonProperty("metadata")]
            public V1ObjectMeta Metadata { get; set; }
        }
    }

    public class KubeFixture
    {
        internal k8s.Kubernetes Client { get; set; } = new k8s.Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());
    }
}