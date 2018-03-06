namespace Orleans.Clustering.Kubernetes.API
{
    internal class CustomResourceDefinitionSpec
    {
        public string Group { get; set; }

        public CustomResourceDefinitionNames Names { get; set; }

        public string Scope { get; set; }

        public string Version { get; set; }
    }
}
