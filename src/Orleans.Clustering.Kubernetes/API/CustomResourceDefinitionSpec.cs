using Newtonsoft.Json;

namespace Orleans.Clustering.Kubernetes.API
{
    internal class CustomResourceDefinitionSpec
    {
        [JsonProperty(PropertyName = "group")]
        public string Group { get; set; }

        [JsonProperty(PropertyName = "names")]
        public CustomResourceDefinitionNames Names { get; set; }

        [JsonProperty(PropertyName = "scope")]
        public string Scope { get; set; }
        
        [JsonProperty(PropertyName = "version")]
        public string Version { get; set; }
    }
}
