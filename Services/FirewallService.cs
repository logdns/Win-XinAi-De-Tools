using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PortManager.Models;

namespace PortManager.Services;

public static class FirewallService
{
    private const int ActionAllow = 1;
    private const int DirectionInbound = 1;
    private const int DirectionOutbound = 2;
    private const int ProtocolTcp = 6;
    private const int ProtocolUdp = 17;
    private const int AllProfiles = int.MaxValue;
    private const int FileNotFoundHResult = unchecked((int)0x80070002);
    private const int ElementNotFoundHResult = unchecked((int)0x80070490);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(3);
    private static readonly SemaphoreSlim OperationGate = new(1, 1);
    private static List<FirewallRule>? _cachedRules;
    private static DateTime _cacheExpiresUtc;

    public static async Task<List<FirewallRule>> ListRulesAsync()
    {
        await OperationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cachedRules is not null && DateTime.UtcNow < _cacheExpiresUtc)
                return new List<FirewallRule>(_cachedRules);

            var rules = await RunNativeAsync(ListRulesCore, "Could not read Windows Firewall rules.")
                .ConfigureAwait(false);
            _cachedRules = rules;
            _cacheExpiresUtc = DateTime.UtcNow.Add(CacheLifetime);
            return new List<FirewallRule>(rules);
        }
        finally
        {
            OperationGate.Release();
        }
    }

    public static async Task<List<FirewallRule>> QueryPortAsync(int port)
    {
        var all = await ListRulesAsync().ConfigureAwait(false);
        return all.Where(rule =>
                PortRangeMatcher.Matches(rule.LocalPort, port) ||
                PortRangeMatcher.Matches(rule.RemotePort, port))
            .ToList();
    }

    public static async Task<OperationResult> AddRuleAsync(
        int port, string protocol, string direction, string ruleName, string? applicationPath = null)
    {
        await OperationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await RunNativeAsync(
                    () => AddRulesCore(port, protocol, direction, ruleName, applicationPath),
                    "Could not add the Windows Firewall rule.")
                .ConfigureAwait(false);
            InvalidateCache();
            return result;
        }
        finally
        {
            OperationGate.Release();
        }
    }

    public static async Task<bool> DeleteRuleAsync(string ruleName)
    {
        await OperationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var deleted = await RunNativeAsync(
                    () => DeleteRuleCore(ruleName),
                    "Could not delete the Windows Firewall rule.")
                .ConfigureAwait(false);
            InvalidateCache();
            return deleted;
        }
        finally
        {
            OperationGate.Release();
        }
    }

    public static async Task<OperationResult> ModifyRuleAsync(
        string oldName, int port, string protocol, string direction, string newName)
    {
        if (!await DeleteRuleAsync(oldName).ConfigureAwait(false))
        {
            return new OperationResult
            {
                Success = false,
                FailedCount = 1,
                Message = "The existing rule could not be removed."
            };
        }

        return await AddRuleAsync(port, protocol, direction, newName).ConfigureAwait(false);
    }

    public static async Task<OperationResult> ImportRulesAsync(IEnumerable<FirewallRule> rules)
    {
        var imported = rules?.ToList() ?? throw new ArgumentNullException(nameof(rules));
        await OperationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await RunNativeAsync(
                    () => ImportRulesCore(imported),
                    "Could not import the Windows Firewall rules.")
                .ConfigureAwait(false);
            InvalidateCache();
            return result;
        }
        finally
        {
            OperationGate.Release();
        }
    }

    internal static FirewallRule CreateRuleModel(
        string name,
        int direction,
        int protocol,
        string? localPorts,
        string? remotePorts,
        int profiles,
        bool enabled) => new()
        {
            Name = name,
            Direction = direction == DirectionOutbound ? "Outbound" : "Inbound",
            Protocol = protocol switch
            {
                ProtocolTcp => "TCP",
                ProtocolUdp => "UDP",
                256 => "Any",
                _ => protocol.ToString(CultureInfo.InvariantCulture)
            },
            LocalPort = NormalizePorts(localPorts),
            RemotePort = NormalizePorts(remotePorts),
            Profile = FormatProfiles(profiles),
            Enabled = enabled.ToString()
        };

    internal static string NormalizePorts(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "*" ? "Any" : value;

    internal static bool HasSpecificPort(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Equals("Any", StringComparison.OrdinalIgnoreCase) &&
        value != "*";

    private static List<FirewallRule> ListRulesCore()
    {
        object? policy = null;
        object? rules = null;
        var result = new List<FirewallRule>();

        try
        {
            policy = CreateComObject("HNetCfg.FwPolicy2");
            rules = ((dynamic)policy).Rules;

            foreach (object item in (dynamic)rules)
            {
                try
                {
                    dynamic rule = item;
                    if (!(bool)rule.Enabled || (int)rule.Action != ActionAllow)
                        continue;

                    var localPorts = NormalizePorts((string?)rule.LocalPorts);
                    var remotePorts = NormalizePorts((string?)rule.RemotePorts);
                    if (!HasSpecificPort(localPorts) && !HasSpecificPort(remotePorts))
                        continue;

                    result.Add(CreateRuleModel(
                        (string)rule.Name,
                        (int)rule.Direction,
                        (int)rule.Protocol,
                        localPorts,
                        remotePorts,
                        (int)rule.Profiles,
                        (bool)rule.Enabled));
                }
                catch (COMException)
                {
                    // A malformed third-party rule must not prevent the remaining rules from loading.
                }
                finally
                {
                    ReleaseComObject(item);
                }
            }

            return result
                .OrderBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        finally
        {
            ReleaseComObject(rules);
            ReleaseComObject(policy);
        }
    }

    private static OperationResult AddRulesCore(
        int port, string protocol, string direction, string ruleName, string? applicationPath)
    {
        object? policy = null;
        object? rules = null;

        try
        {
            policy = CreateComObject("HNetCfg.FwPolicy2");
            rules = ((dynamic)policy).Rules;
            var protocols = protocol == "ANY" ? new[] { "TCP", "UDP" } : new[] { protocol };
            var directions = direction switch
            {
                "Both" => new[] { "in", "out" },
                "out" => new[] { "out" },
                _ => new[] { "in" }
            };

            var result = new OperationResult();
            var errors = new List<string>();
            foreach (var dir in directions)
            {
                foreach (var proto in protocols)
                {
                    var name = ruleName;
                    if (protocols.Length > 1)
                        name += $"_{proto}";
                    if (directions.Length > 1)
                        name += dir == "in" ? "_Inbound" : "_Outbound";

                    try
                    {
                        AddSingleRule(rules, name, port, proto, dir, applicationPath);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        errors.Add(ex.Message);
                    }
                }
            }

            result.Success = result.FailedCount == 0;
            result.Message = result.Success
                ? $"Added {result.SuccessCount} firewall rule(s)."
                : $"Added {result.SuccessCount}; failed {result.FailedCount}.";
            result.ErrorMessage = string.Join(Environment.NewLine, errors.Distinct());
            return result;
        }
        finally
        {
            ReleaseComObject(rules);
            ReleaseComObject(policy);
        }
    }

    private static OperationResult ImportRulesCore(IReadOnlyList<FirewallRule> imported)
    {
        object? policy = null;
        object? rules = null;
        try
        {
            policy = CreateComObject("HNetCfg.FwPolicy2");
            rules = ((dynamic)policy).Rules;
            var result = new OperationResult();
            var errors = new List<string>();
            foreach (var source in imported)
            {
                try
                {
                    var protocols = ParseProtocols(source.Protocol);
                    var directions = ParseDirections(source.Direction);
                    var profiles = ParseProfiles(source.Profile);
                    foreach (var protocol in protocols)
                    foreach (var direction in directions)
                    {
                        var suffix = protocols.Count > 1 || directions.Count > 1
                            ? $"_{protocol}_{direction}"
                            : string.Empty;
                        var name = protocols.Count > 1 || directions.Count > 1
                            ? source.Name + suffix
                            : source.Name;
                        AddImportedRule(rules, name, source, protocol, direction, profiles);
                        result.SuccessCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    errors.Add($"{source.Name}: {ex.Message}");
                }
            }

            result.Success = result.FailedCount == 0;
            result.Message = result.Success
                ? $"Imported {result.SuccessCount} firewall rule(s)."
                : $"Imported {result.SuccessCount}; failed {result.FailedCount}.";
            result.ErrorMessage = string.Join(Environment.NewLine, errors.Distinct());
            return result;
        }
        finally
        {
            ReleaseComObject(rules);
            ReleaseComObject(policy);
        }
    }

    private static void AddImportedRule(object rules, string name, FirewallRule source, string protocol, string direction, int profiles)
    {
        object? ruleObject = null;
        try
        {
            ruleObject = CreateComObject("HNetCfg.FWRule");
            dynamic rule = ruleObject;
            rule.Name = name;
            rule.Description = "Managed by Win-XinAi-De-Tools";
            rule.Grouping = "Win-XinAi-De-Tools";
            rule.Protocol = protocol switch
            {
                "TCP" => ProtocolTcp,
                "UDP" => ProtocolUdp,
                _ => 256
            };
            rule.Direction = direction == "Outbound" ? DirectionOutbound : DirectionInbound;
            if (direction == "Outbound")
            {
                if (HasSpecificPort(source.RemotePort)) rule.RemotePorts = source.RemotePort;
                if (HasSpecificPort(source.LocalPort)) rule.LocalPorts = source.LocalPort;
            }
            else
            {
                if (HasSpecificPort(source.LocalPort)) rule.LocalPorts = source.LocalPort;
                if (HasSpecificPort(source.RemotePort)) rule.RemotePorts = source.RemotePort;
            }
            rule.Enabled = !source.Enabled.Equals("False", StringComparison.OrdinalIgnoreCase) &&
                           !source.Enabled.Equals("0", StringComparison.OrdinalIgnoreCase);
            rule.Profiles = profiles;
            rule.Action = ActionAllow;
            ((dynamic)rules).Add(rule);
        }
        finally
        {
            ReleaseComObject(ruleObject);
        }
    }

    private static List<string> ParseProtocols(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is "ANY" or "256" or "*" or ""
            ? new List<string> { "ANY" }
            : normalized is "TCP" or "6" ? new List<string> { "TCP" }
            : normalized is "UDP" or "17" ? new List<string> { "UDP" }
            : throw new FirewallOperationException($"Unsupported protocol: {value}");
    }

    private static List<string> ParseDirections(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "INBOUND" or "IN" or "1" => new List<string> { "Inbound" },
            "OUTBOUND" or "OUT" or "2" => new List<string> { "Outbound" },
            "BOTH" or "ANY" or "" => new List<string> { "Inbound", "Outbound" },
            _ => throw new FirewallOperationException($"Unsupported direction: {value}")
        };
    }

    private static int ParseProfiles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("Any", StringComparison.OrdinalIgnoreCase) || value == "*" || value == "2147483647")
            return AllProfiles;

        var profiles = 0;
        foreach (var token in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            profiles |= token.ToUpperInvariant() switch
            {
                "DOMAIN" or "1" => 1,
                "PRIVATE" or "2" => 2,
                "PUBLIC" or "4" => 4,
                _ => 0
            };
        }
        return profiles == 0 ? AllProfiles : profiles;
    }

    private static void AddSingleRule(object rules, string name, int port, string protocol, string direction, string? applicationPath)
    {
        object? ruleObject = null;
        try
        {
            ruleObject = CreateComObject("HNetCfg.FWRule");
            dynamic rule = ruleObject;
            rule.Name = name;
            rule.Description = "Managed by Win-XinAi-De-Tools";
            rule.Grouping = "Win-XinAi-De-Tools";
            if (applicationPath is not null) rule.ApplicationName = applicationPath;
            rule.Protocol = protocol == "UDP" ? ProtocolUdp : ProtocolTcp;
            rule.Direction = direction == "out" ? DirectionOutbound : DirectionInbound;
            if (direction == "out")
                rule.RemotePorts = port.ToString(CultureInfo.InvariantCulture);
            else
                rule.LocalPorts = port.ToString(CultureInfo.InvariantCulture);
            rule.Enabled = true;
            rule.Profiles = AllProfiles;
            rule.Action = ActionAllow;
            ((dynamic)rules).Add(rule);
        }
        finally
        {
            ReleaseComObject(ruleObject);
        }
    }

    private static bool DeleteRuleCore(string ruleName)
    {
        object? policy = null;
        object? rules = null;
        object? existingRule = null;

        try
        {
            policy = CreateComObject("HNetCfg.FwPolicy2");
            rules = ((dynamic)policy).Rules;
            try
            {
                existingRule = ((dynamic)rules).Item(ruleName);
            }
            catch (Exception ex) when (IsNotFound(ex))
            {
                return false;
            }

            ((dynamic)rules).Remove(ruleName);
            return true;
        }
        finally
        {
            ReleaseComObject(existingRule);
            ReleaseComObject(rules);
            ReleaseComObject(policy);
        }
    }

    private static object CreateComObject(string programmaticId)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows Firewall is available only on Windows.");

        var type = Type.GetTypeFromProgID(programmaticId, throwOnError: false)
            ?? throw new FirewallOperationException($"Windows component {programmaticId} is unavailable.");
        return Activator.CreateInstance(type)
            ?? throw new FirewallOperationException($"Windows component {programmaticId} could not be created.");
    }

    private static async Task<T> RunNativeAsync<T>(Func<T> operation, string failureMessage)
    {
        try
        {
            return await Task.Run(operation).ConfigureAwait(false);
        }
        catch (FirewallOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FirewallOperationException(failureMessage, ex);
        }
    }

    private static string FormatProfiles(int profiles)
    {
        if (profiles == AllProfiles)
            return "Any";

        var names = new List<string>();
        if ((profiles & 1) != 0) names.Add("Domain");
        if ((profiles & 2) != 0) names.Add("Private");
        if ((profiles & 4) != 0) names.Add("Public");
        return names.Count == 0 ? profiles.ToString(CultureInfo.InvariantCulture) : string.Join(", ", names);
    }

    private static bool IsNotFound(Exception exception) =>
        exception is FileNotFoundException ||
        exception is COMException comException &&
        comException.HResult is FileNotFoundHResult or ElementNotFoundHResult;

    private static void InvalidateCache()
    {
        _cachedRules = null;
        _cacheExpiresUtc = DateTime.MinValue;
    }

    private static void ReleaseComObject(object? instance)
    {
        if (OperatingSystem.IsWindows() && instance is not null && Marshal.IsComObject(instance))
            Marshal.FinalReleaseComObject(instance);
    }
}

public sealed class FirewallOperationException : Exception
{
    public FirewallOperationException(string message) : base(message)
    {
    }

    public FirewallOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
