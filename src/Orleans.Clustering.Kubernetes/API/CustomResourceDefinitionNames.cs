using Newtonsoft.Json;
using System.Collections.Generic;

namespace Orleans.Clustering.Kubernetes.API
{
    internal class CustomResourceDefinitionNames
    {
        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        [JsonProperty(PropertyName = "listKind")]
        public string ListKind { get; set; }

        [JsonProperty(PropertyName = "plural")]
        public string Plural { get; set; }

        [JsonProperty(PropertyName = "shortNames")]
        public IList<string> ShortNames { get; set; }

        [JsonProperty(PropertyName = "singular")]
        public string Singular { get; set; }
    }
}
