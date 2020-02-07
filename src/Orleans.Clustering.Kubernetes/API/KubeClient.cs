using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Clustering.Kubernetes.API
{
    // TODO: Add proper logging
    internal class KubeClient
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Culture = CultureInfo.InvariantCulture,
            Converters = new []
            {
                new StringEnumConverter(true)
            },
#if DEBUG
            Formatting = Formatting.Indented
#else
            Formatting = Formatting.None
#endif
        };
        private static readonly Encoding _encoding = Encoding.UTF8;
        private static readonly IReadOnlyList<CustomResourceDefinition> _emptyCustomResourceDefinitionList = new List<CustomResourceDefinition>();

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
        internal const string ORLEANS_GROUP = "orleans.dot.net";
        private const string ORLEANS_NAMESPACE = "orleanstest";

        private readonly ILogger _logger;
        private readonly string _namespace;
        private readonly string _group;
        private readonly HttpClient _client;

        internal X509Certificate2 RootCertificate { get; }

        public KubeClient(ILoggerFactory loggerFactory, HttpClientHandler httpClientHandler = null, string apiEndpoint = null, string group = null, string apiToken = null, string certificate = null)
        {
            this._logger = loggerFactory?.CreateLogger<KubeClient>();

            var namespaceFilePath = Path.Combine(SERVICE_ACCOUNT_PATH, SERVICE_ACCOUNT_NAMESPACE_FILENAME);

            if (!File.Exists(namespaceFilePath))
            {
                this._logger?.LogWarning("Namespace file {namespaceFilePath} wasn't found. Are we running in a pod? If you are running unit tests outside a pod, please create the test namespace '{namespace}'.", namespaceFilePath, ORLEANS_NAMESPACE);

                this._namespace = ORLEANS_NAMESPACE;
            }
            else
            {
                this._namespace = File.ReadAllText(namespaceFilePath);
            }

            this._group = string.IsNullOrWhiteSpace(group) ? ORLEANS_GROUP : group.ToLowerInvariant();

            var endpointUri = new Uri(string.IsNullOrWhiteSpace(apiEndpoint) ? IN_CLUSTER_KUBE_ENDPOINT : apiEndpoint);

            var certificateData = certificate;
            var isRootCertificateLoaded = false;

            if (string.IsNullOrWhiteSpace(certificateData))
            {
                var rootCertificateFilePath = Path.Combine(SERVICE_ACCOUNT_PATH, SERVICE_ACCOUNT_ROOTCA_FILENAME);

                if (File.Exists(rootCertificateFilePath))
                {
                    this.RootCertificate = new X509Certificate2(rootCertificateFilePath);
                    isRootCertificateLoaded = true;

                }
                else
                {
                    this._logger?.LogWarning("Root Certificate file {rootCertificateFilePath} wasn't found, no certificate will be used.", rootCertificateFilePath);
                }
            }

            if (!isRootCertificateLoaded && !string.IsNullOrWhiteSpace(certificateData))
            {
                certificateData = certificateData
                    .Replace(BEGIN_CERT_LINE, string.Empty)
                    .Replace(END_CERT_LINE, string.Empty)
                    .Replace(RETURN_CHAR, string.Empty)
                    .Replace(NEWLINE_CHAR, string.Empty);

                this.RootCertificate = new X509Certificate2(Convert.FromBase64String(certificateData));
            }

            var handler = httpClientHandler;

            if (handler == null)
            {
                handler = new HttpClientHandler();

                // If the base url is a secure one, install a certificate handler if we've a root certificate configured.
                if (endpointUri.Scheme == "https" && this.RootCertificate != null)
                {
                    handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, serverCertificate, chain, sslPolicyErrors) =>
                         {
                             // If the certificate is a valid, signed certificate, return true.
                             if (sslPolicyErrors == SslPolicyErrors.None)
                             {
                                 return true;
                             }

                             // If there are errors in the certificate chain, look at each error to determine the cause.
                             if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                             {
                                 chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                                 // add all your extra certificate chain
                                 chain.ChainPolicy.ExtraStore.Add(this.RootCertificate);
                                 chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                                 var isValid = chain.Build(serverCertificate);

                                 return isValid;
                             }

                             // In all other cases, return false.
                             return false;
                         };
                }
            }

            this._client = new HttpClient(handler)
            {
                BaseAddress = endpointUri
            };

            var bearerToken = apiToken;

            // If no apiToken was passed in, then try to load from file if exists.
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                var tokenFilePath = Path.Combine(SERVICE_ACCOUNT_PATH, SERVICE_ACCOUNT_TOKEN_FILENAME);

                if (File.Exists(tokenFilePath))
                {
                    bearerToken = File.ReadAllText(tokenFilePath);
                }
                else
                {
                    this._logger?.LogWarning("Token file {tokenFilePath} wasn't found, no API token will be used.", tokenFilePath);
                }
            }

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", bearerToken);
            }
        }

        #region Custom Resource Definition

        public async Task<IReadOnlyList<CustomResourceDefinition>> ListCRDs()
        {
            var response = await this._client.GetAsync(CRD_ENDPOINT).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return _emptyCustomResourceDefinitionList;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    this._logger?.LogError("Failure listing CRDs: {error}", error);
                }

                return _emptyCustomResourceDefinitionList;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jsonObject = JObject.Parse(json);
            var crdList = jsonObject["items"].ToObject<CustomResourceDefinition[]>()
                .Where(crd => crd.Spec.Group == this._group).ToList();

            return crdList;
        }

        public async Task<CustomResourceDefinition> CreateCRD(CustomResourceDefinition crd)
        {
            var response = await this._client.PostAsync(CRD_ENDPOINT,
                new StringContent(JsonConvert.SerializeObject(crd, _jsonSettings),
                _encoding,
                MEDIA_TYPE)).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.Created)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                this._logger?.LogError("Failure creating CRD: {error}", error);

                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);

            return JsonConvert.DeserializeObject<CustomResourceDefinition>(json, _jsonSettings);
        }

        public async Task DeleteCRD(CustomResourceDefinition crd)
        {
            var response = await this._client.DeleteAsync($"/apis/apiextensions.k8s.io/v1beta1/customresourcedefinitions/{crd.Metadata.Name}").ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                this._logger?.LogError("Failure deleting CRD: {error}", error);

                return;
            }

            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);
        }

        #endregion

        #region Custom Objects

        public async Task<TObject> CreateCustomObject<TObject>(string version, string plural, TObject obj) where TObject : CustomObject
        {
            var response = await this._client.PostAsync(
                $"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}",
                new StringContent(JsonConvert.SerializeObject(obj, _jsonSettings),
                _encoding,
                MEDIA_TYPE)).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.Created)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                this._logger?.LogError("Failure creating Custom Object: {error}", error);

                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);

            return JsonConvert.DeserializeObject<TObject>(json, _jsonSettings);
        }

        public async Task DeleteCustomObject(string name, string version, string plural)
        {
            var response = await this._client.DeleteAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}/{name}").ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                this._logger?.LogError("Failure deleting Custom Object: {error}", error);

                return;
            }

            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);
        }

        public async Task<TObject> GetCustomObject<TObject>(string name, string version, string plural) where TObject : CustomObject
        {
            var response = await this._client.GetAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}/{name}").ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    this._logger?.LogError("Failure getting Custom Object: {error}", error);
                }

                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<TObject>(json, _jsonSettings);
        }

        public async Task<IReadOnlyList<TObject>> ListCustomObjects<TObject>(string version, string plural) where TObject : CustomObject
        {
            var response = await this._client.GetAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}").ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<TObject>();
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    this._logger?.LogError("Failure listing Custom Object: {error}", error);
                }

                return new List<TObject>();
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jsonObject = JObject.Parse(json);
            var customObjectList = jsonObject["items"].ToObject<List<TObject>>();

            return customObjectList;
        }

        public async Task<TObject> UpdateCustomObject<TObject>(string version, string plural, TObject obj) where TObject : CustomObject
        {
            var response = await this._client.PutAsync($"/apis/{this._group}/{version}/namespaces/{this._namespace}/{plural}/{obj.Metadata.Name}",
                new StringContent(JsonConvert.SerializeObject(obj, _jsonSettings),
                _encoding,
                MEDIA_TYPE)).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Conflict ||
                response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("Invalid Kubernetes object version");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                this._logger?.LogError("Failure updating Custom Object: {error}", error);

                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(300);

            return JsonConvert.DeserializeObject<TObject>(json, _jsonSettings);
        }

        #endregion
    }
}
