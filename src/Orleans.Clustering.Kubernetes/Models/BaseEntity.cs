using Orleans.Clustering.Kubernetes.API;

namespace Orleans.Clustering.Kubernetes.Models
{
    internal class BaseEntity : CustomObject
    {
        public string ClusterId { get; set; }
    }
}
