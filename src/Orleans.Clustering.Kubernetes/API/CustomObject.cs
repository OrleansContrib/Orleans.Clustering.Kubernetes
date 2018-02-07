using Newtonsoft.Json;

namespace Orleans.Clustering.Kubernetes.API
{
    internal abstract class CustomObject
    {
        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        [JsonProperty(PropertyName = "metadata")]
        public ObjectMetadata Metadata { get; set; }
    }
}
