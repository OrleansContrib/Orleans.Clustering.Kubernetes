using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Clustering.Kubernetes.API
{
    // TODO: Add proper logging
    internal class KubeClient
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        private static readonly Encoding _encoding = Encoding.GetEncoding("utf-8");
        private const string CRD_ENDPOINT = "/apis/apiextensions.k8s.io/v1beta1/customresourcedefinitions";
        private const string MEDIA_TYPE = "application/json";
        private const string SERVICE_ACCOUNT_PATH = "/var/run/secrets/kubernetes.io/serviceaccount/";
        private const string SERVICE_ACCOUNT_NAMESPACE_FILENAME = "namespace";
        private const string SERVICE_ACCOUNT_TOKEN_FILENAME = "token";
        private const string SERVICE_ACCOUNT_ROOTCA_FILENAME = "ca.crt";
        private const string BEGIN_CERT_LINE = "-----BEGIN CERTIFICATE-----";
        private const string END_CERT_LINE = "-----END CERTIFICATE-----";
        private const string RETURN_CHAR = "\r";
        private const string NEWLINE_CHAR = "\n";
        private const string IN_CLUSTER_KUBE_ENDPOINT = "https://kubernetes.default.svc.cluster.local";
        private const string ORLEANS_GROUP = "orleans.dot.net";

        private readonly HttpClient _client;
        private readonly X509Certificate2 _cert;
        private readonly ILogger _logger;
        private readonly string _namespace;
        private readonly string _group;

        public KubeClient(
            ILoggerFactory loggerFactory, string apiEndpoint = "",
            string group = "", string apiToken = "", string certificate = "")
        {
            this._logger = loggerFactory?.CreateLogger<KubeClient>();

            var namespaceFile = Path.Combine(SERVICE_ACCOUNT_PATH, SERVICE_ACCOUNT_NAMESPACE_FILENAME);
            if (!File.Exists(namespaceFile))
            {
                this._logger?.LogWarning(
                    $"Namespace file {namespaceFile} wasn't found. Are we running in a pod? If you are running unit tests outside a pod, please create the namespace 'orleanstest'.");
                this._namespace = "orleanstest";
            }
            else
            {
                this._namespace = File.ReadAllText(namespaceFile);
            }

            this._group = string.IsNullOrWhiteSpace(group) ? ORLEANS_GROUP : group.ToLowerInvariant();

            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) =>
                    {
                        // TODO: Validate it properly using _cert.
                        return true;
                    }
            };

            string endpoint = string.IsNullOrWhiteSpace(apiEndpoint) ? IN_CLUSTER_KUBE_ENDPOINT : apiEndpoint;

            this._client = new HttpClient(handler)
            {
                BaseAddress = new Uri(endpoint)
            };

            if (apiToken != "test")
            {
                string token = !string.IsNullOrWhiteSpace(apiToken) ? apiToken : File.ReadAllText(Path.Combine(SERVICE_ACCOUNT_PATH, SERVICE_ACCOUNT_TOKEN_FILENAME));
                if (!string.IsNullOrWhiteSpace(token))
                {
                    this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token);
                }
            }

            if (certificate != "test")
            {
                string certdata = string.Empty;
                if (!string.IsNullOrWhiteSpace(certificate))
                {
                    certdata = certificate.Replace(BEGIN_CERT_LINE, string.Empty)
                        .Replace(END_CERT_LINE, string.Empty)
                        .Replace(RETURN_CHAR, string.Empty)
                        .Replace(NEWLINE_CHAR, string.Empty);
                }
                else
                {
                    var rootCAFile = Path.Combine(SERVICE_ACCOUNT_PATH, SERVICE_ACCOUNT_ROOTCA_FILENAME);
                    certdata = File.ReadAllText(rootCAFile)
                        .Replace(BEGIN_CERT_LINE, string.Empty)
                        .Replace(END_CERT_LINE, string.Empty)
                        .Replace(RETURN_CHAR, string.Empty)
                        .Replace(NEWLINE_CHAR, string.Empty);
                }

                this._cert = new X509Certificate2(Convert.FromBase64String(certdata));
            }
        }

        #region Custom Resource Definition

        public async Task<IReadOnlyList<CustomResourceDefinition>> ListCRDs()
        {
            var resp = await this._client.GetAsync(CRD_ENDPOINT);

            if (resp.StatusCode == HttpStatusCode.NotFound) return new List<CustomResourceDefinition>();

            if (resp.StatusCode != HttpStatusCode.OK)
            {
                if (resp.StatusCode != HttpStatusCode.NotFound)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    this._logger?.LogError($"Failure listing CRDs: {err}"); 
                }
                return new List<CustomResourceDefinition>();
            }

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jobj = JObject.Parse(json);
            var crds = jobj["items"].ToObject<List<CustomResourceDefinition>>()
                .Where(crd => crd.Spec.Group == this._group).ToList();
            return crds;
        }

        public async Task<CustomResourceDefinition> CreateCRD(CustomResourceDefinition crd)
        {
            var resp = await this._client.PostAsync(CRD_ENDPOINT,
                new StringContent(JsonConvert.SerializeObject(crd, _jsonSettings),
                _encoding, MEDIA_TYPE));

            if (resp.StatusCode != HttpStatusCode.OK &&
                resp.StatusCode != HttpStatusCode.Created)
            {
                var err = await resp.Content.ReadAsStringAsync();
                this._logger?.LogError($"Failure creating CRD: {err}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);
            return JsonConvert.DeserializeObject<CustomResourceDefinition>(json);
        }

        public async Task DeleteCRD(CustomResourceDefinition crd)
        {
            var resp = await this._client.DeleteAsync($"/apis/apiextensions.k8s.io/v1beta1/customresourcedefinitions/{crd.Metadata.Name}");
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                this._logger?.LogError($"Failure deleting CRD: {err}");
                return;
            }

            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);
        }

        #endregion

        #region Custom Objects

        public async Task<TObject> CreateCustomObject<TObject>(
            string version, string plural, TObject obj) where TObject : CustomObject
        {
            var resp = await this._client.PostAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}",
                new StringContent(JsonConvert.SerializeObject(obj, _jsonSettings), _encoding, MEDIA_TYPE));
            if (resp.StatusCode != HttpStatusCode.OK &&
                resp.StatusCode != HttpStatusCode.Created)
            {
                var err = await resp.Content.ReadAsStringAsync();
                this._logger?.LogError($"Failure creating Custom Object: {err}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);
            return JsonConvert.DeserializeObject<TObject>(json);
        }

        public async Task DeleteCustomObject(string name, string version, string plural)
        {
            var resp = await this._client.DeleteAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}/{name}");
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                this._logger?.LogError($"Failure deleting Custom Object: {err}");
                return;
            }
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);
        }

        public async Task<TObject> GetCustomObject<TObject>(
            string name, string version,
            string plural) where TObject : CustomObject
        {
            var resp = await this._client.GetAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}/{name}");
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    this._logger?.LogError($"Failure getting Custom Object: {err}");
                }
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TObject>(json);
        }

        public async Task<IReadOnlyList<TObject>> ListCustomObjects<TObject>(
            string version, string plural)
            where TObject : CustomObject
        {
            var resp = await this._client.GetAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}");

            if (resp.StatusCode == HttpStatusCode.NotFound) return new List<TObject>();

            if (resp.StatusCode != HttpStatusCode.OK)
            {
                if (resp.StatusCode != HttpStatusCode.NotFound)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    this._logger?.LogError($"Failure listing Custom Object: {err}");
                }
                return new List<TObject>();
            }

            var json = await resp.Content.ReadAsStringAsync();
            var jobj = JObject.Parse(json);
            var customObjs = jobj["items"].ToObject<List<TObject>>();
            return customObjs;
        }

        public async Task<TObject> UpdateCustomObject<TObject>(
            string version, string plural, TObject obj) where TObject : CustomObject
        {
            var resp = await this._client.PutAsync(
                $"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}/{obj.Metadata.Name}",
                new StringContent(JsonConvert.SerializeObject(obj, _jsonSettings), _encoding, MEDIA_TYPE));

            if (resp.StatusCode == HttpStatusCode.Conflict ||
                resp.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("Invalid Kubernetes object version");
            }

            if (resp.StatusCode != HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                this._logger?.LogError($"Failure updating Custom Object: {err}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);
            return JsonConvert.DeserializeObject<TObject>(json);
        }
        #endregion
    }
}
