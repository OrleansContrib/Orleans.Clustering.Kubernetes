using Newtonsoft.Json;

namespace Orleans.Clustering.Kubernetes.API
{
    internal class CustomResourceDefinition
    {
        [JsonIgnore]
        public const string KIND = "CustomResourceDefinition";

        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        [JsonProperty(PropertyName = "metadata")]
        public ObjectMetadata Metadata { get; set; }

        [JsonProperty(PropertyName = "spec")]
        public CustomResourceDefinitionSpec Spec { get; set; }
    }
}
