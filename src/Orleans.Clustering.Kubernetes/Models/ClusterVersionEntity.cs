using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Orleans.Clustering.Kubernetes.Models;

internal class ClusterVersionEntity : BaseEntity
{
    [JsonIgnore]
    public const string PLURAL = "clusterversions";

    [JsonIgnore]
    public const string SINGULAR = "clusterversion";

    [JsonIgnore]
    public static readonly List<string> SHORT_NAME = new List<string> { "ocv", "oc" };

    [JsonIgnore]
    public const string KIND = "OrleansClusterVersion";

    [JsonPropertyName("clusterVersion")]
    public int ClusterVersion { get; set; } = 0;

    public ClusterVersionEntity()
    {
        Kind = KIND;
    }
}