namespace Orleans.Clustering.Kubernetes;

internal static class Constants
{
    public const string SERVICE_ACCOUNT_PATH = "/var/run/secrets/kubernetes.io/serviceaccount/";
    public const string SERVICE_ACCOUNT_NAMESPACE_FILENAME = "namespace";
    public const string ORLEANS_GROUP = "orleans.dot.net";
    public const string ORLEANS_NAMESPACE = "orleans";
    public const string PROVIDER_MODEL_VERSION = "v1";
}