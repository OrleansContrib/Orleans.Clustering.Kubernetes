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
        public const string SHORT_NAME = "oso";

        [JsonIgnore]
        public const string KIND = "OrleansSilo";

        [JsonProperty(nameof(Address))]
        public string Address { get; set; }

        [JsonProperty(nameof(Port))]
        public int Port { get; set; }

        [JsonProperty(nameof(Generation))]
        public int Generation { get; set; }

        [JsonProperty(nameof(Hostname))]
        public string Hostname { get; set; }

        [JsonProperty(nameof(Status))]
        [JsonConverter(typeof(StringEnumConverter))]
        public SiloStatus Status { get; set; }

        [JsonProperty(nameof(ProxyPort))]
        public int? ProxyPort { get; set; }

        [JsonProperty(nameof(SiloName))]
        public string SiloName { get; set; }

        [JsonProperty(nameof(SuspectingSilos))]
        public List<string> SuspectingSilos { get; set; } = new List<string>();

        [JsonProperty(nameof(SuspectingTimes))]
        public List<string> SuspectingTimes { get; set; } = new List<string>();

        [JsonProperty(nameof(StartTime))]
        public DateTimeOffset StartTime { get; set; }

        [JsonProperty(nameof(IAmAliveTime))]
        public DateTimeOffset IAmAliveTime { get; set; }
    }
}
