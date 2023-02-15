using System.Text.Json.Serialization;
using k8s.Models;

namespace Orleans.Clustering.Kubernetes.API;

internal abstract class CustomObject
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("metadata")]
    public V1ObjectMeta Metadata { get; set; }
}