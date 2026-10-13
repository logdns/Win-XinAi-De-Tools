using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PortManager.Models;
using PortManager.Services;
using Xunit;

namespace PortManager.Tests;

public sealed class HyperVServiceTests
{
    private static readonly Guid AdapterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SwitchId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void FeatureOnlyDisablePreservesOtherPlatformsAndBootSetting()
    {
        var script = HyperVService.BuildFeatureScript(false);
        Assert.Contains("Disable-WindowsOptionalFeature", script);
        Assert.Contains("-NoRestart", script);
        Assert.Contains("Get-WindowsOptionalFeature -Online -ErrorAction Stop", script);
        Assert.Contains("DisabledWithPayloadRemoved", script);
        Assert.DoesNotContain("-ErrorAction SilentlyContinue", script);
        Assert.DoesNotContain("VirtualMachinePlatform", script);
        Assert.DoesNotContain("HypervisorPlatform", script);
        Assert.DoesNotContain("bcdedit", script);
        Assert.DoesNotContain("Restart-Computer", script);
    }

    [Fact]
    public void CompleteDisableIncludesPlatformsAndBootButNeverPurgesDataOrNetworking()
    {
        var script = HyperVService.BuildFeatureScript(false, true);
        Assert.Contains("Microsoft-Hyper-V-All", script);
        Assert.Contains("VirtualMachinePlatform", script);
        Assert.Contains("HypervisorPlatform", script);
        Assert.Contains("hypervisorlaunchtype off", script);
        Assert.Contains("$LASTEXITCODE", script);
        Assert.Contains("-NoRestart", script);
        foreach (var dangerous in new[] { "Remove-Item", "Remove-NetNat", "Stop-Service", "Disable-NetAdapter", "Restart-Computer", "Remove-VMSwitch", "--unregister" })
            Assert.DoesNotContain(dangerous, script);
    }

    [Fact]
    public void EnableSetsAutomaticBootAndChecksPendingStatesBeforeChangingFeatures()
    {
        var script = HyperVService.BuildFeatureScript(true);
        Assert.Contains("-All -NoRestart", script);
        Assert.Contains("hypervisorlaunchtype auto", script);
        Assert.True(script.IndexOf("pending", StringComparison.Ordinal) < script.IndexOf("Enable-WindowsOptionalFeature", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => HyperVService.BuildFeatureScript(true, true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AdapterOperationsAreGuidScopedAndDriverVerified(bool enable)
    {
        var script = HyperVService.BuildAdapterScript(new[] { AdapterId, AdapterId }, enable);
        Assert.Contains("ServiceName -eq 'VMSMP'", script);
        Assert.Contains("-IncludeHidden", script);
        Assert.Contains($"$selected=@([guid]'{AdapterId:D}')", script);
        Assert.Contains("$a.Count -ne 1", script);
        Assert.Contains($"{(enable ? "Enable" : "Disable")}-NetAdapter", script);
        Assert.DoesNotContain("-Name", script);
        Assert.DoesNotContain("vEthernet*", script);
    }

    [Fact]
    public void EmptyAdapterSelectionsAndSwitchIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => HyperVService.BuildAdapterScript(Array.Empty<Guid>(), false));
        Assert.Throws<ArgumentException>(() => HyperVService.BuildAdapterScript(new[] { Guid.Empty }, false));
        Assert.Throws<ArgumentException>(() => HyperVService.BuildRemoveSwitchScript(Guid.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("bad\nname")]
    [InlineData("bad\0name")]
    [InlineData("wild*card")]
    [InlineData("wild?card")]
    [InlineData("[network]")]
    public void InvalidNamesAreRejected(string value) => Assert.Throws<ArgumentException>(() => HyperVService.NormalizeSwitchName(value));

    [Fact]
    public void LongNameAndInvalidTypeAreRejected()
    {
        Assert.Throws<ArgumentException>(() => HyperVService.NormalizeSwitchName(new string('a', 65)));
        Assert.Throws<ArgumentException>(() => HyperVService.BuildCreateSwitchScript(new("test", (HyperVSwitchType)99, Guid.Empty, true)));
        Assert.Throws<ArgumentException>(() => HyperVService.BuildCreateSwitchScript(new("test", HyperVSwitchType.External, Guid.Empty, true)));
    }

    [Fact]
    public void QuotedNamesStayInALiteralAndExternalAdapterIsRevalidated()
    {
        var script = HyperVService.BuildCreateSwitchScript(new("O'Brien; $(whoami)", HyperVSwitchType.External, AdapterId, false));
        Assert.Contains("$name='WinXinAi-O''Brien; $(whoami)'", script);
        Assert.Contains("-Physical", script);
        Assert.Contains("$_.HardwareInterface -and -not $_.Virtual", script);
        Assert.Contains("IndexOfAny", script);
        Assert.Contains("-AllowManagementOS:$false", script);
    }

    [Theory]
    [InlineData(HyperVSwitchType.Internal)]
    [InlineData(HyperVSwitchType.Private)]
    public void NonExternalSwitchDoesNotBindPhysicalNic(HyperVSwitchType type)
    {
        var script = HyperVService.BuildCreateSwitchScript(new("test", type, Guid.Empty, true));
        Assert.Contains($"-SwitchType {type}", script);
        Assert.DoesNotContain("-NetAdapterName", script);
        Assert.DoesNotContain("AllowManagementOS", script);
    }

    [Theory]
    [InlineData("Default Switch", false)]
    [InlineData("WSL", false)]
    [InlineData("vEthernet (WinXinAi-test)", false)]
    [InlineData("WinXinAi-test", true)]
    [InlineData("winxinai-test", true)]
    public void OnlyToolPrefixedSwitchesCanBeRemoved(string name, bool expected) =>
        Assert.Equal(expected, new HyperVSwitch { Name = name }.CanRemove);

    [Fact]
    public void RemovalRechecksPrefixAndConnectedGuestAdapters()
    {
        var script = HyperVService.BuildRemoveSwitchScript(SwitchId);
        Assert.Contains($"Get-VMSwitch -Id '{SwitchId:D}'", script);
        Assert.Contains("StartsWith('WinXinAi-'", script);
        Assert.Contains("Get-VMNetworkAdapter -All", script);
        Assert.Contains("$_.SwitchId -eq $s.Id", script);
        Assert.True(script.IndexOf("Get-VMNetworkAdapter", StringComparison.Ordinal) < script.IndexOf("Remove-VMSwitch", StringComparison.Ordinal));
    }

    [Fact]
    public void StatusPreservesUnavailableAndPartialQueryErrors()
    {
        Assert.Contains("State='Unavailable'", HyperVService.StatusScript);
        Assert.Contains("NetworkError=$networkError", HyperVService.StatusScript);
        Assert.Contains("SwitchError=$switchError", HyperVService.StatusScript);
        var status = JsonSerializer.Deserialize<HyperVStatus>("""
            {"Features":[{"Name":"Microsoft-Hyper-V-All","State":"Unavailable"}],"VirtualAdapters":[],"PhysicalAdapters":[],"Switches":[],"HypervisorPresent":false,"BootLaunchType":"Off","ManagementAvailable":false}
            """);
        Assert.NotNull(status);
        Assert.Empty(status.VirtualAdapters);
        Assert.Equal("Unavailable", status.Features[0].State);
        Assert.False(status.HypervisorPresent);
    }

    [Fact]
    public async Task NonWindowsCallsFailBeforeStartingAProcess()
    {
        if (OperatingSystem.IsWindows()) return;
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => HyperVService.GetStatusAsync());
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => HyperVService.SetAdaptersEnabledAsync(new[] { AdapterId }, false));
    }

    [Fact]
    [Trait("Category", "HyperVContract")]
    public async Task WindowsPowerShellParsesEveryGeneratedScriptWithoutExecution()
    {
        if (!OperatingSystem.IsWindows()) return;
        var scripts = new List<string> { HyperVService.StatusScript, HyperVService.BuildFeatureScript(true), HyperVService.BuildFeatureScript(false), HyperVService.BuildFeatureScript(false, true),
            HyperVService.BuildAdapterScript(new[] { AdapterId }, true), HyperVService.BuildAdapterScript(new[] { AdapterId }, false), HyperVService.BuildRemoveSwitchScript(SwitchId) };
        scripts.AddRange(Enum.GetValues<HyperVSwitchType>().Select(type => HyperVService.BuildCreateSwitchScript(new("O'Brien; $(whoami)", type, AdapterId, true))));
        foreach (var script in scripts)
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            await RunHarnessAsync($"$tokens=$null; $errors=$null; [System.Management.Automation.Language.Parser]::ParseInput([Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{encoded}')),[ref]$tokens,[ref]$errors) | Out-Null; if($errors.Count){{throw ($errors | Out-String)}}");
        }
    }

    [Fact]
    [Trait("Category", "HyperVContract")]
    public async Task MockedAdapterOperationsRejectPhysicalAndStaleTargetsBeforeAnyMutation()
    {
        if (!OperatingSystem.IsWindows()) return;
        var fixtures = $$"""
            $script:calls=@();
            function Get-CimInstance { [pscustomobject]@{ServiceName='VMSMP';GUID='{{AdapterId:D}}'}; [pscustomobject]@{ServiceName='e1dexpress';GUID='{{SwitchId:D}}'} }
            function Get-NetAdapter { param([switch]$IncludeHidden) [pscustomobject]@{InterfaceGuid='{{AdapterId:D}}';Name='arbitrary renamed adapter'}; [pscustomobject]@{InterfaceGuid='{{SwitchId:D}}';Name='vEthernet (misleading physical name)'} }
            function Disable-NetAdapter { param([Parameter(ValueFromPipeline=$true)]$InputObject,[switch]$Confirm) process { $script:calls += $InputObject.InterfaceGuid } }
            """;
        await RunHarnessAsync(fixtures + HyperVService.BuildAdapterScript(new[] { AdapterId }, false) + $"if($calls.Count -ne 1 -or $calls[0] -ne '{AdapterId:D}'){{throw 'Wrong adapter selected'}}");
        await RunHarnessAsync(fixtures + "try { " + HyperVService.BuildAdapterScript(new[] { AdapterId, SwitchId }, false) + " throw 'Expected validation failure' } catch { if($_.Exception.Message -notlike '*no longer exists*'){throw} }; if($calls.Count){throw 'Mutation occurred before preflight finished'}");
    }

    [Theory]
    [InlineData("Default Switch", false, false)]
    [InlineData("WSL", false, false)]
    [InlineData("WinXinAi-test", true, false)]
    [InlineData("WinXinAi-test", false, true)]
    [Trait("Category", "HyperVContract")]
    public async Task MockedSwitchRemovalProtectsSystemSwitchesAndAttachedVMs(string name, bool guestAttached, bool shouldRemove)
    {
        if (!OperatingSystem.IsWindows()) return;
        var fixtures = $$"""
            $script:removed=$false;
            function Get-VMSwitch { param($Id) [pscustomobject]@{Id=[guid]'{{SwitchId:D}}';Name='{{name}}'} }
            function Get-VMNetworkAdapter { param([switch]$All) if(${{guestAttached.ToString().ToLowerInvariant()}}){ [pscustomobject]@{IsManagementOs=$false;SwitchId=[guid]'{{SwitchId:D}}'} } }
            function Remove-VMSwitch { param([Parameter(ValueFromPipeline=$true)]$InputObject,[switch]$Force,[switch]$Confirm) process { $script:removed=$true } }
            """;
        await RunHarnessAsync(fixtures + "try { " + HyperVService.BuildRemoveSwitchScript(SwitchId) + " } catch { if($_.Exception.Message -notmatch 'Only WinXinAi|Disconnect all'){throw} }; " + $"if($removed -ne ${shouldRemove.ToString().ToLowerInvariant()}){{throw 'Unexpected removal result'}}");
    }

    private static async Task RunHarnessAsync(string script)
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executable, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            // Expected, caught fixture errors can leave PowerShell's $? false; exit only after all assertions ran.
            ArgumentList = { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + script + "; exit 0")) }
        })!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, $"PowerShell fixture exited with {process.ExitCode}: " + await error + await output);
    }
}
