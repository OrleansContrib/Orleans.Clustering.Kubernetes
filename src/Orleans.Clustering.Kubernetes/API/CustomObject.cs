using k8s.Models;
using Newtonsoft.Json;

namespace Orleans.Clustering.Kubernetes.API;

internal abstract class CustomObject
{
    [JsonProperty("apiVersion")]
    public string ApiVersion { get; set; }

    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonProperty("metadata")]
    public V1ObjectMeta Metadata { get; set; }
}