using PortManager.Models;
using PortManager.Services;
using Xunit;
using System.Diagnostics;
using System.Text;

namespace PortManager.Tests;

public sealed class NetworkResetTests
{
    [Fact]
    public void DnsResetHasNoAdapterOrStackMutations()
    {
        var script = NetworkResetService.BuildResetScript(new(NetworkResetMode.FlushDns));
        Assert.Contains("Clear-DnsClientCache", script);
        Assert.DoesNotContain("Remove-", script);
        Assert.DoesNotContain("netsh", script);
    }

    [Fact]
    public void AdapterResetIsGuidScopedAndPreservesOtherNetworks()
    {
        var id = Guid.NewGuid();
        var script = NetworkResetService.BuildResetScript(new(NetworkResetMode.AdapterDhcp, id));
        Assert.Contains(id.ToString("D"), script);
        Assert.Contains("$a.Count -ne 1", script);
        Assert.Contains("-InterfaceIndex $index", script);
        Assert.Contains("PrefixOrigin -eq 'Manual'", script);
        Assert.Contains("DestinationPrefix -eq '0.0.0.0/0'", script);
        Assert.Contains("-ResetServerAddresses", script);
        Assert.DoesNotContain("Remove-NetAdapter", script);
        Assert.DoesNotContain("Restart-Computer", script);
        Assert.DoesNotContain("Remove-VMSwitch", script);
    }

    [Fact]
    public void InvalidRequestsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => NetworkResetService.BuildResetScript(new(NetworkResetMode.AdapterDhcp)));
        Assert.Throws<ArgumentException>(() => NetworkResetService.BuildResetScript(new((NetworkResetMode)99)));
        Assert.Throws<ArgumentException>(() => NetworkResetService.BuildResetScript(new(NetworkResetMode.SystemStack)));
    }

    [Fact]
    public void SnapshotIncludesManualRecoveryData()
    {
        foreach (var command in new[] { "Get-NetAdapter", "Get-NetIPAddress", "Get-NetIPInterface", "Get-DnsClientServerAddress", "Get-NetRoute" })
            Assert.Contains(command, NetworkResetService.SnapshotScript);
        Assert.DoesNotContain("Set-", NetworkResetService.SnapshotScript);
    }

    [Fact]
    public void PartialResultsCannotReportSuccess()
    {
        Assert.False(new NetworkResetResult("backup", true, new[] { new NetworkResetStep("winsock", true, ""), new NetworkResetStep("tcpip", false, "error") }).Success);
        Assert.False(new NetworkResetResult("backup", false, Array.Empty<NetworkResetStep>()).Success);
    }

    [Fact]
    [Trait("Category", "NetworkResetContract")]
    public async Task WindowsParserAcceptsScriptsWithoutExecutingThem()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var script in new[] { NetworkResetService.SnapshotScript, NetworkResetService.BuildResetScript(new(NetworkResetMode.FlushDns)), NetworkResetService.BuildResetScript(new(NetworkResetMode.AdapterDhcp, Guid.NewGuid())) })
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            await RunHarnessAsync($"$tokens=$null;$errors=$null;[System.Management.Automation.Language.Parser]::ParseInput([Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{encoded}')),[ref]$tokens,[ref]$errors)|Out-Null;if($errors.Count){{throw ($errors|Out-String)}}");
        }
    }

    [Theory]
    [InlineData("Up", true)]
    [InlineData("Disabled", false)]
    [InlineData("Missing", false)]
    [Trait("Category", "NetworkResetContract")]
    public async Task MockedResetRejectsStaleTargetsAndScopesEveryMutation(string status, bool shouldMutate)
    {
        if (!OperatingSystem.IsWindows()) return;
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var fixture = $$"""
            $script:calls=@();
            function Get-NetAdapter { param([switch]$IncludeHidden) if('{{status}}' -ne 'Missing'){[pscustomobject]@{InterfaceGuid='{{id:D}}';ifIndex=47;Status='{{status}}'} }; [pscustomobject]@{InterfaceGuid='22222222-2222-2222-2222-222222222222';ifIndex=99;Status='Up'} }
            function Get-NetRoute { param($InterfaceIndex,$AddressFamily,$ErrorAction) if($InterfaceIndex -ne 47){throw 'Wrong route interface'}; [pscustomobject]@{Protocol='NetMgmt';DestinationPrefix='0.0.0.0/0'};[pscustomobject]@{Protocol='Dhcp';DestinationPrefix='0.0.0.0/0'};[pscustomobject]@{Protocol='NetMgmt';DestinationPrefix='10.0.0.0/8'} }
            function Remove-NetRoute { param([Parameter(ValueFromPipeline=$true)]$InputObject,[switch]$Confirm) process { if($InputObject.Protocol -ne 'NetMgmt' -or $InputObject.DestinationPrefix -ne '0.0.0.0/0'){throw 'Wrong route removed'}; $script:calls+='route' } }
            function Get-NetIPAddress { param($InterfaceIndex,$AddressFamily,$ErrorAction) if($InterfaceIndex -ne 47){throw 'Wrong IP interface'}; [pscustomobject]@{PrefixOrigin='Manual'};[pscustomobject]@{PrefixOrigin='Dhcp'} }
            function Remove-NetIPAddress { param([Parameter(ValueFromPipeline=$true)]$InputObject,[switch]$Confirm) process { if($InputObject.PrefixOrigin -ne 'Manual'){throw 'Wrong IP removed'}; $script:calls+='ip' } }
            function Set-NetIPInterface { param($InterfaceIndex,$AddressFamily,$Dhcp,$ErrorAction) if($InterfaceIndex -ne 47 -or $Dhcp -ne 'Enabled'){throw 'Wrong DHCP target'}; $script:calls+='dhcp' }
            function Set-DnsClientServerAddress { param($InterfaceIndex,[switch]$ResetServerAddresses,$ErrorAction) if($InterfaceIndex -ne 47 -or !$ResetServerAddresses){throw 'Wrong DNS target'}; $script:calls+='dns' }
            """;
        var script = NetworkResetService.BuildResetScript(new(NetworkResetMode.AdapterDhcp, id));
        await RunHarnessAsync(fixture + "try { " + script + " } catch { if($_.Exception.Message -notlike '*missing or disabled*'){throw} }; " + $"if($calls.Count -ne {(shouldMutate ? 4 : 0)}){{throw 'Unexpected mutation count'}}");
    }

    private static async Task RunHarnessAsync(string script)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + script + "; exit 0")) } })!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await error + await output);
    }
}
