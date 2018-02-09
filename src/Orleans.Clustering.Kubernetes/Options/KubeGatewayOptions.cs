namespace Orleans.Clustering.Kubernetes
{
    public class KubeGatewayOptions
    {
        public string Group { get; set; }
        public string APIEndpoint { get; set; }
        public string APIToken { get; set; }
        public string CertificateData { get; set; }
    }
}
