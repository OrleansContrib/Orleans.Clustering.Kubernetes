using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Orleans.Clustering.Kubernetes.Models;

internal class SiloEntity : BaseEntity
{
    [JsonIgnore]
    public const string PLURAL = "silos";

    [JsonIgnore]
    public const string SINGULAR = "silo";

    [JsonIgnore]
    public static readonly List<string> SHORT_NAME = new() { "oso", "os" };

    [JsonIgnore]
    public const string KIND = "OrleansSilo";

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("generation")]
    public int Generation { get; set; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("status")]
    public SiloStatus Status { get; set; }

    [JsonPropertyName("proxyPort")]
    public int? ProxyPort { get; set; }

    [JsonPropertyName("siloName")]
    public string SiloName { get; set; }

    [JsonPropertyName("suspectingSilos")]
    public List<string> SuspectingSilos { get; set; } = new List<string>();

    [JsonPropertyName("suspectingTimes")]
    public List<string> SuspectingTimes { get; set; } = new List<string>();

    [JsonPropertyName("startTime")]
    public DateTimeOffset StartTime { get; set; }

    [JsonPropertyName("iAmAliveTime")]
    public DateTimeOffset IAmAliveTime { get; set; }

    public SiloEntity()
    {
        this.Kind = KIND;
    }
}