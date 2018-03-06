using Newtonsoft.Json;

namespace Orleans.Clustering.Kubernetes.API
{
    internal class CustomResourceDefinition
    {
        [JsonIgnore]
        public const string KIND = "CustomResourceDefinition";

        public string ApiVersion { get; set; }

        public string Kind { get; set; }

        public ObjectMetadata Metadata { get; set; }

        public CustomResourceDefinitionSpec Spec { get; set; }
    }
}
