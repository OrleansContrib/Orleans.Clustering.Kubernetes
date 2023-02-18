using k8s.Autorest;

namespace Orleans.Clustering.Kubernetes.Extensions;

public static class HttpOperationExceptionExtensions
{
    public static string GetServerResponse(this HttpOperationException exception)
    {
        try
        {
            return exception.Response.Content;
        }
        catch
        {
            return null;
        }
    }
}