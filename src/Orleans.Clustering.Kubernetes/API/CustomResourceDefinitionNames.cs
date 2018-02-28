using System.Collections.Generic;

namespace Orleans.Clustering.Kubernetes.API
{
    internal class CustomResourceDefinitionNames
    {
        public string Kind { get; set; }

        public string ListKind { get; set; }

        public string Plural { get; set; }

        public IList<string> ShortNames { get; set; }

        public string Singular { get; set; }
    }
}
