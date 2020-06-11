using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Orleans.Runtime;
using System;
using System.Collections.Generic;

namespace Orleans.Clustering.Kubernetes.Models
{
    internal class SiloEntity : BaseEntity
    {
        [JsonIgnore]
        public const string PLURAL = "silos";

        [JsonIgnore]
        public const string SINGULAR = "silo";

        [JsonIgnore]
        public static readonly List<string> SHORT_NAME = new List<string> { "oso", "os" };

        [JsonIgnore]
        public const string KIND = "OrleansSilo";

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("generation")]
        public int Generation { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("status")]
        public SiloStatus Status { get; set; }

        [JsonProperty("proxyPort")]
        public int? ProxyPort { get; set; }

        [JsonProperty("siloName")]
        public string SiloName { get; set; }

        [JsonProperty("suspectingSilos")]
        public List<string> SuspectingSilos { get; set; } = new List<string>();

        [JsonProperty("suspectingTimes")]
        public List<string> SuspectingTimes { get; set; } = new List<string>();

        [JsonProperty("startTime")]
        public DateTimeOffset StartTime { get; set; }

        [JsonProperty("iAmAliveTime")]
        public DateTimeOffset IAmAliveTime { get; set; }

        public SiloEntity()
        {
            this.Kind = KIND;
        }
    }
}
