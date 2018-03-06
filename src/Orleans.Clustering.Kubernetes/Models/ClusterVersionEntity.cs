using Newtonsoft.Json;

namespace Orleans.Clustering.Kubernetes.Models
{
    internal class ClusterVersionEntity : BaseEntity
    {
        [JsonIgnore]
        public const string PLURAL = "clusterversions";

        [JsonIgnore]
        public const string SINGULAR = "clusterversion";

        [JsonIgnore]
        public const string SHORT_NAME = "ocv";

        [JsonIgnore]
        public const string KIND = "OrleansClusterVersion";

        public int ClusterVersion { get; set; } = 0;
    }
}
