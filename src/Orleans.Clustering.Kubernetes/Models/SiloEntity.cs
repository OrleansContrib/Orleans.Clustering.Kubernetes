using Newtonsoft.Json;
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

        public string Address { get; set; }

        public int Port { get; set; }

        public int Generation { get; set; }

        public string Hostname { get; set; }

        public SiloStatus Status { get; set; }

        public int? ProxyPort { get; set; }

        public string SiloName { get; set; }

        public List<string> SuspectingSilos { get; set; } = new List<string>();

        public List<string> SuspectingTimes { get; set; } = new List<string>();

        public DateTimeOffset StartTime { get; set; }

        public DateTimeOffset IAmAliveTime { get; set; }
    }
}
