using System.IO;
using Xunit;

namespace Orleans.Clustering.Kubernetes.Test
{
    public sealed class IgnoreInsidePodFact : FactAttribute
    {
        private const string rootCertificatePath = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";

        public IgnoreInsidePodFact() {
            if (File.Exists(rootCertificatePath)) {
                this.Skip = "Ignore when running inside a Pod";
            }
        }
    }
}