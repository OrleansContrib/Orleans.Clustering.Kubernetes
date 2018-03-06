namespace Orleans.Clustering.Kubernetes.API
{
    internal abstract class CustomObject
    {
        public string ApiVersion { get; set; }

        public string Kind { get; set; }

        public ObjectMetadata Metadata { get; set; }
    }
}
