using System.Text.Json.Serialization;
using Orleans.Clustering.Kubernetes.API;

namespace Orleans.Clustering.Kubernetes.Models;

internal class BaseEntity : CustomObject
{
    [JsonPropertyName("clusterId")]
    public string ClusterId { get; set; }
}