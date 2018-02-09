namespace Orleans.Clustering.Kubernetes
{
    public class KubeClusteringOptions
    {
        public string Group { get; set; }
        public string APIEndpoint { get; set; }
        public string APIToken { get; set; }
        public string CertificateData { get; set; }
        public bool CanCreateResources { get; set; }
        public bool DropResourcesOnInit { get; set; }
    }
}
