using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private HttpClient _client;

        public KubeClient() : this("http://localhost:8001") { }

        public KubeClient(string apiEndpoint)
        {
            this._client = new HttpClient
            {
                BaseAddress = new Uri(apiEndpoint)
            };
        }

        #region Custom Resource Definition

        public async Task<IReadOnlyList<CustomResourceDefinition>> ListCRDs(string group)
        {
            var resp = await this._client.GetAsync(CRD_ENDPOINT);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return new List<CustomResourceDefinition>();

            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jobj = JObject.Parse(json);
            var crds = jobj["items"].ToObject<List<CustomResourceDefinition>>()
                .Where(crd => crd.Spec.Group == group).ToList();
            return crds;
        }

        public async Task<CustomResourceDefinition> CreateCRD(CustomResourceDefinition crd)
        {
            var resp = await this._client.PostAsync(CRD_ENDPOINT, new StringContent(JsonConvert.SerializeObject(crd, _jsonSettings), _encoding, MEDIA_TYPE));
            if (resp.StatusCode != System.Net.HttpStatusCode.OK &&
                resp.StatusCode != System.Net.HttpStatusCode.Created)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(200);
            return JsonConvert.DeserializeObject<CustomResourceDefinition>(json);
        }

        public async Task DeleteCRD(CustomResourceDefinition crd)
        {
            var resp = await this._client.DeleteAsync($"/apis/apiextensions.k8s.io/v1beta1/customresourcedefinitions/{crd.Metadata.Name}");
            if (resp.StatusCode != System.Net.HttpStatusCode.OK &&
                resp.StatusCode != System.Net.HttpStatusCode.Created)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return;
            }

            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(200);
        }

        #endregion

        #region Custom Objects

        public async Task<TObject> CreateCustomObject<TObject>(
            string group, string version, string @namespace,
            string plural, TObject obj) where TObject : CustomObject
        {
            var resp = await this._client.PostAsync($"/apis/{group}/{version}/namespaces/{@namespace}/{plural}",
                new StringContent(JsonConvert.SerializeObject(obj, _jsonSettings), _encoding, MEDIA_TYPE));
            if (resp.StatusCode != System.Net.HttpStatusCode.OK &&
                resp.StatusCode != System.Net.HttpStatusCode.Created)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(200);
            return JsonConvert.DeserializeObject<TObject>(json);
        }

        public async Task DeleteCustomObject(string name, string group, string version, string @namespace, string plural)
        {
            var resp = await this._client.DeleteAsync($"/apis/{group}/{version}/namespaces/{@namespace}/{plural}/{name}");
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return;
            }
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(200);
        }

        public async Task<TObject> GetCustomObject<TObject>(
            string name, string group, string version,
            string @namespace, string plural) where TObject : CustomObject
        {
            var resp = await this._client.GetAsync($"/apis/{group}/{version}/namespaces/{@namespace}/{plural}/{name}");
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TObject>(json);
        }

        public async Task<IReadOnlyList<TObject>> ListCustomObjects<TObject>(
            string group, string version,
            string @namespace, string plural)
            where TObject : CustomObject
        {
            var resp = await this._client.GetAsync($"/apis/{group}/{version}/namespaces/{@namespace}/{plural}");

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return new List<TObject>();

            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            var jobj = JObject.Parse(json);
            var customObjs = jobj["items"].ToObject<List<TObject>>();
            return customObjs;
        }

        public async Task<TObject> UpdateCustomObject<TObject>(
            string group, string version, string @namespace,
            string plural, TObject obj) where TObject : CustomObject
        {
            var resp = await this._client.PutAsync(
                $"/apis/{group}/{version}/namespaces/{@namespace}/{plural}/{obj.Metadata.Name}",
                new StringContent(JsonConvert.SerializeObject(obj, _jsonSettings), _encoding, MEDIA_TYPE));

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict ||
                resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("Invalid Kubernetes object version");
            }

            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            // TODO: Investigate how to wait for Kube to commit the objects on etcd before move forward without those delays.
            await Task.Delay(200);
            return JsonConvert.DeserializeObject<TObject>(json);
        }
        #endregion
    }
}
