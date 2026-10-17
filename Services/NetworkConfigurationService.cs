using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using PortManager.Models;

namespace PortManager.Services;

public static class NetworkConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<List<NetworkAdapterModel>> ListAdaptersAsync() => Task.Run(() =>
    {
        EnsureWindows();
        const string script = "@(Get-NetAdapter -ErrorAction Stop | Where-Object Status -ne 'Disabled' | Select-Object Name,Status,MacAddress,ifIndex,@{n='AdapterId';e={([guid]$_.InterfaceGuid).ToString('D')}}) | ConvertTo-Json -Compress";
        var json = RunPowerShell<JsonElement>(script);
        var rows = json.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<AdapterDto>>(json.GetRawText(), JsonOptions) ?? new()
            : json.ValueKind == JsonValueKind.Object
                ? new List<AdapterDto> { JsonSerializer.Deserialize<AdapterDto>(json.GetRawText(), JsonOptions) ?? new() }
                : new List<AdapterDto>();
        return rows.Select(row => new NetworkAdapterModel
        {
            Name = row.Name ?? string.Empty,
            Status = row.Status ?? string.Empty,
            MacAddress = row.MacAddress ?? string.Empty,
            InterfaceIndex = row.InterfaceIndex,
            InterfaceGuid = row.AdapterId
        }).OrderBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase).ToList();
    });

    public static Task<NetworkConfigurationModel> GetConfigurationAsync(NetworkAdapterModel adapter) => Task.Run(() =>
    {
        EnsureWindows();
        var alias = EscapePowerShell(adapter.Name);
        var script = $"$a=Get-NetAdapter -Name '{alias}' -ErrorAction Stop; $ip=Get-NetIPAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {{$_.PrefixOrigin -ne 'RouterAdvertisement' -and $_.IPAddress -notlike '169.254.*'}} | Sort-Object SkipAsSource | Select-Object -First 1; $r=Get-NetRoute -InterfaceIndex $a.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Sort-Object RouteMetric | Select-Object -First 1; $d=Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue; $ipAddress=if($ip){{$ip.IPAddress}}else{{''}}; $prefix=if($ip){{$ip.PrefixLength}}else{{0}}; $gatewayValue=if($r){{$r.NextHop}}else{{''}}; $metric=if($r){{$r.RouteMetric}}else{{0}}; [pscustomobject]@{{InterfaceAlias=$a.Name;InterfaceIndex=$a.ifIndex;DhcpEnabled=((Get-NetIPInterface -InterfaceIndex $a.ifIndex -AddressFamily IPv4).Dhcp -eq 'Enabled');IPv4Address=$ipAddress;PrefixLength=$prefix;Gateway=$gatewayValue;DnsServers=@($d.ServerAddresses);DefaultRouteMetric=$metric}} | ConvertTo-Json -Compress -Depth 4";
        var row = RunPowerShell<ConfigurationDto>(script);
        return new NetworkConfigurationModel
        {
            InterfaceAlias = row.InterfaceAlias ?? adapter.Name,
            InterfaceIndex = row.InterfaceIndex,
            DhcpEnabled = row.DhcpEnabled,
            IPv4Address = row.IPv4Address ?? string.Empty,
            PrefixLength = row.PrefixLength,
            Gateway = row.Gateway ?? string.Empty,
            DnsServers = ReadStringList(row.DnsServers),
            DefaultRouteMetric = row.DefaultRouteMetric
        };
    });

    public static Task ApplyAsync(NetworkAdapterModel adapter, NetworkConfigurationRequest request) => Task.Run(() =>
    {
        EnsureWindows();
        Validate(request);
        var alias = EscapePowerShell(adapter.Name);
        var ip = EscapePowerShell(request.IPv4Address);
        var gateway = EscapePowerShell(request.Gateway);
        var dns = new[] { request.PrimaryDns, request.SecondaryDns }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(EscapePowerShell)
            .ToArray();
        var dnsExpression = dns.Length == 0 ? "@()" : $"@('{string.Join("','", dns)}')";
        var dnsCommand = dns.Length == 0
            ? "Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses -ErrorAction Stop"
            : $"Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ServerAddresses {dnsExpression} -ErrorAction Stop";
        var script = request.UseDhcp
            ? $"$a=Get-NetAdapter -Name '{alias}' -ErrorAction Stop; Get-NetRoute -InterfaceIndex $a.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Where-Object {{$_.Protocol -ne 'Dhcp'}} | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue; Get-NetIPAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {{$_.PrefixOrigin -eq 'Manual' -and $_.IPAddress -notlike '169.254.*'}} | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue; Set-NetIPInterface -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -Dhcp Enabled -ErrorAction Stop; Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses -ErrorAction Stop"
            : $"$a=Get-NetAdapter -Name '{alias}' -ErrorAction Stop; Set-NetIPInterface -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -Dhcp Disabled -ErrorAction Stop; Get-NetIPAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {{$_.IPAddress -notlike '169.254.*'}} | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue; New-NetIPAddress -InterfaceIndex $a.ifIndex -IPAddress '{ip}' -PrefixLength {request.PrefixLength} -AddressFamily IPv4 -ErrorAction Stop | Out-Null; Get-NetRoute -InterfaceIndex $a.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue; if('{gateway}' -ne ''){{New-NetRoute -InterfaceIndex $a.ifIndex -DestinationPrefix '0.0.0.0/0' -NextHop '{gateway}' -RouteMetric {request.RouteMetric} -ErrorAction Stop | Out-Null}}; {dnsCommand}";
        RunPowerShell<object>(script, parseOutput: false);
    });

    private static void Validate(NetworkConfigurationRequest request)
    {
        if (request.UseDhcp) return;
        if (!NetworkConfigurationModel.IsValidIpv4(request.IPv4Address))
            throw new NetworkConfigurationException(Message("请输入有效的 IPv4 地址。", "Enter a valid IPv4 address."));
        if (request.PrefixLength is < 1 or > 32)
            throw new NetworkConfigurationException(Message("IPv4 前缀长度必须是 1 到 32。", "IPv4 prefix length must be between 1 and 32."));
        if (!string.IsNullOrWhiteSpace(request.Gateway) && !NetworkConfigurationModel.IsValidIpv4(request.Gateway))
            throw new NetworkConfigurationException(Message("请输入有效的 IPv4 网关。", "Enter a valid IPv4 gateway."));
        foreach (var dns in new[] { request.PrimaryDns, request.SecondaryDns }.Where(value => !string.IsNullOrWhiteSpace(value)))
            if (!NetworkConfigurationModel.IsValidIpv4(dns))
                throw new NetworkConfigurationException(Message("请输入有效的 IPv4 DNS 地址。", "Enter valid IPv4 DNS server addresses."));
        if (request.RouteMetric is < 1 or > 9999)
            throw new NetworkConfigurationException(Message("默认路由跃点必须是 1 到 9999。", "Default route metric must be between 1 and 9999."));
    }

    private static string Message(string chinese, string english) => LanguageState.IsEnglish ? english : chinese;

    private static T RunPowerShell<T>(string script, bool parseOutput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);
        using var process = Process.Start(startInfo) ?? throw new NetworkConfigurationException("Could not start Windows PowerShell.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new NetworkConfigurationException(string.IsNullOrWhiteSpace(error) ? $"Windows network command failed with exit code {process.ExitCode}." : error.Trim());
        if (!parseOutput) return default!;
        try
        {
            return JsonSerializer.Deserialize<T>(output.Trim(), JsonOptions) ?? throw new NetworkConfigurationException("Windows returned no network configuration.");
        }
        catch (JsonException ex)
        {
            throw new NetworkConfigurationException($"Could not read Windows network configuration: {ex.Message}");
        }
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static List<string> ReadStringList(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToList();
        if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            return new List<string> { value.GetString()! };
        return new List<string>();
    }
    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Network configuration is available only on Windows.");
    }

    private sealed class AdapterDto
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? MacAddress { get; set; }
        [JsonPropertyName("ifIndex")] public int InterfaceIndex { get; set; }
        public Guid AdapterId { get; set; }
    }

    private sealed class ConfigurationDto
    {
        public string? InterfaceAlias { get; set; }
        public int InterfaceIndex { get; set; }
        public bool DhcpEnabled { get; set; }
        public string? IPv4Address { get; set; }
        public int PrefixLength { get; set; }
        public string? Gateway { get; set; }
        public JsonElement DnsServers { get; set; }
        public int DefaultRouteMetric { get; set; }
    }
}
