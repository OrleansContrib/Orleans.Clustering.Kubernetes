using Newtonsoft.Json;
using Orleans.Clustering.Kubernetes.API;

namespace Orleans.Clustering.Kubernetes.Models;

internal class BaseEntity : CustomObject
{
    [JsonProperty("clusterId")]
    public string ClusterId { get; set; }
}