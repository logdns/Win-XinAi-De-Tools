using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace PortManager.Models;

public sealed class NetworkAdapterModel
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public int InterfaceIndex { get; init; }
    public Guid InterfaceGuid { get; init; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(MacAddress)
        ? $"{Name} ({Status})"
        : $"{Name} ({Status}, {MacAddress})";
}

public sealed class NetworkConfigurationModel
{
    public string InterfaceAlias { get; init; } = string.Empty;
    public int InterfaceIndex { get; init; }
    public bool DhcpEnabled { get; init; }
    public string IPv4Address { get; init; } = string.Empty;
    public int PrefixLength { get; init; }
    public string Gateway { get; init; } = string.Empty;
    public List<string> DnsServers { get; init; } = new();
    public int DefaultRouteMetric { get; init; }

    [JsonIgnore]
    public string DnsDisplay => DnsServers.Count == 0 ? "-" : string.Join(", ", DnsServers);

    public static bool IsValidIpv4(string value) =>
        IPAddress.TryParse(value, out var address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
}

public sealed class NetworkConfigurationRequest
{
    public bool UseDhcp { get; init; }
    public string IPv4Address { get; init; } = string.Empty;
    public int PrefixLength { get; init; }
    public string Gateway { get; init; } = string.Empty;
    public string PrimaryDns { get; init; } = string.Empty;
    public string SecondaryDns { get; init; } = string.Empty;
    public int RouteMetric { get; init; } = 25;
}

public sealed class NetworkConfigurationException : Exception
{
    public NetworkConfigurationException(string message) : base(message) { }
}
