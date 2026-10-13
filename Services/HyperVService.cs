using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PortManager.Models;

namespace PortManager.Services;

public static class HyperVService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim OperationGate = new(1, 1);
    private const string Preamble = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false); ";
    // Classify by the Windows driver service, never by an editable/localized adapter alias.
    internal const string VirtualAdapterQuery = "$ids=@(Get-CimInstance Win32_NetworkAdapter -ErrorAction Stop | Where-Object { $_.ServiceName -eq 'VMSMP' -and $_.GUID } | ForEach-Object { [guid]$_.GUID }); $virtual=@(Get-NetAdapter -IncludeHidden -ErrorAction Stop | Where-Object { $ids -contains [guid]$_.InterfaceGuid }); ";

    internal const string StatusScript = Preamble + """
        [Console]::Error.WriteLine('Hyper-V probe: optional features');
        $availableFeatures=@(Get-WindowsOptionalFeature -Online -ErrorAction Stop);
        $features=@(foreach($name in @('Microsoft-Hyper-V-All','VirtualMachinePlatform','HypervisorPlatform')) {
            $f=$availableFeatures | Where-Object FeatureName -eq $name | Select-Object -First 1
            if($f){[pscustomobject]@{Name=$name;State=[string]$f.State}}else{[pscustomobject]@{Name=$name;State='Unavailable'}}
        });
        $virtualRows=@(); $physicalRows=@(); $networkError='';
        [Console]::Error.WriteLine('Hyper-V probe: network adapters');
        try {
        """ + VirtualAdapterQuery + """
            $virtualRows=@($virtual | ForEach-Object { [pscustomobject]@{Id=[string]$_.InterfaceGuid;Name=$_.Name;Status=[string]$_.Status} });
            $physicalRows=@(Get-NetAdapter -Physical | Where-Object { $_.HardwareInterface -and -not $_.Virtual } | ForEach-Object { [pscustomobject]@{Id=[string]$_.InterfaceGuid;Name=$_.Name;Status=[string]$_.Status} });
        } catch { $networkError=$_.Exception.Message }
        $available=$null -ne (Get-Command Get-VMSwitch -ErrorAction SilentlyContinue); $switches=@(); $switchError='';
        [Console]::Error.WriteLine('Hyper-V probe: virtual switches');
        if($available){try{$switches=@(Get-VMSwitch | ForEach-Object { [pscustomobject]@{Id=[string]$_.Id;Name=$_.Name;SwitchType=[string]$_.SwitchType;AllowManagementOS=[bool]$_.AllowManagementOS} })}catch{$switchError=$_.Exception.Message}}
        [Console]::Error.WriteLine('Hyper-V probe: boot configuration');
        $boot=& "$env:SystemRoot\System32\bcdedit.exe" /enum '{current}' 2>&1;
        if($LASTEXITCODE -ne 0){throw "Cannot read boot configuration: $boot"}
        $launch='Default'; foreach($line in $boot){if([string]$line -match '^\s*hypervisorlaunchtype\s+(\S+)'){$launch=$Matches[1]}}
        [Console]::Error.WriteLine('Hyper-V probe: hypervisor presence');
        [pscustomobject]@{Features=$features;VirtualAdapters=$virtualRows;PhysicalAdapters=$physicalRows;Switches=$switches;HypervisorPresent=[bool](Get-CimInstance Win32_ComputerSystem).HypervisorPresent;BootLaunchType=$launch;ManagementAvailable=$available;NetworkError=$networkError;SwitchError=$switchError} | ConvertTo-Json -Compress -Depth 5
        """;

    public static Task<HyperVStatus> GetStatusAsync() => Task.Run(() => RunAsync<HyperVStatus>(StatusScript, timeoutDuration: TimeSpan.FromSeconds(45)));

    internal static string BuildFeatureScript(bool enable, bool completeDisable = false)
    {
        if (enable && completeDisable) throw new ArgumentException("Enable and complete disable are mutually exclusive.");
        var names = completeDisable
            ? "@('Microsoft-Hyper-V-All','VirtualMachinePlatform','HypervisorPlatform')"
            : "@('Microsoft-Hyper-V-All')";
        var command = enable
            ? "if([string]$f.State -ne 'Enabled'){Enable-WindowsOptionalFeature -Online -FeatureName $f.FeatureName -All -NoRestart | Out-Null}"
            : "if([string]$f.State -eq 'Enabled'){Disable-WindowsOptionalFeature -Online -FeatureName $f.FeatureName -NoRestart | Out-Null}";
        // Check all features first. A missing optional component is skipped only on complete disable (e.g. Home).
        return Preamble + $"$names={names}; $available=@(Get-WindowsOptionalFeature -Online -ErrorAction Stop); $features=@(); foreach($name in $names){{$f=$available | Where-Object FeatureName -eq $name | Select-Object -First 1; if($f){{$features+=,$f}}elseif(-not ${completeDisable.ToString().ToLowerInvariant()}){{throw 'Hyper-V is unavailable on this Windows edition or architecture.'}}}}; "
            + "foreach($f in $features){if([string]$f.State -notin @('Disabled','Enabled','DisabledWithPayloadRemoved')){throw 'A feature is pending or unavailable. Restart Windows and refresh before retrying.'}}; "
            + $"foreach($f in $features){{{command}}}; "
            + (enable || completeDisable
                ? $"& \"$env:SystemRoot\\System32\\bcdedit.exe\" /set '{{current}}' hypervisorlaunchtype {(enable ? "auto" : "off")} | Out-Null; if($LASTEXITCODE -ne 0){{throw 'Boot configuration update failed. Feature changes may already have been applied; refresh and restart Windows.'}}; "
                : string.Empty);
    }

    public static Task EnableAsync() => RunMutationAsync(BuildFeatureScript(true));
    public static Task DisableFeatureAsync() => RunMutationAsync(BuildFeatureScript(false));
    public static Task CompleteDisableAsync() => RunMutationAsync(BuildFeatureScript(false, completeDisable: true));

    internal static string BuildAdapterScript(IReadOnlyCollection<Guid> ids, bool enable)
    {
        if (ids.Count == 0 || ids.Any(id => id == Guid.Empty)) throw new ArgumentException("Select at least one valid virtual adapter.");
        var selected = string.Join(",", ids.Distinct().Select(id => $"[guid]'{id:D}'"));
        return Preamble + VirtualAdapterQuery + $"$selected=@({selected}); $targets=@(); foreach($id in $selected){{$a=@($virtual | Where-Object {{[guid]$_.InterfaceGuid -eq $id}}); if($a.Count -ne 1){{throw 'A selected Hyper-V adapter no longer exists. Refresh before retrying.'}}; $targets+=,$a[0]}}; "
            + $"foreach($a in $targets){{$a | {(enable ? "Enable" : "Disable")}-NetAdapter -Confirm:$false -ErrorAction Stop}}; ";
    }

    public static Task SetAdaptersEnabledAsync(IReadOnlyCollection<Guid> ids, bool enable) => RunMutationAsync(BuildAdapterScript(ids, enable));

    internal static string BuildCreateSwitchScript(HyperVSwitchRequest request)
    {
        var name = NormalizeSwitchName(request.Name);
        if (!Enum.IsDefined(request.Type)) throw new ArgumentException("Unsupported switch type.");
        if (request.Type == HyperVSwitchType.External && request.PhysicalAdapterId == Guid.Empty)
            throw new ArgumentException("Select a physical adapter for the external switch.");
        var script = Preamble + $"$name='{Quote(name)}'; if(@(Get-VMSwitch | Where-Object {{$_.Name -eq $name}}).Count -gt 0){{throw 'A switch with this name already exists.'}}; ";
        if (request.Type != HyperVSwitchType.External)
            return script + $"New-VMSwitch -Name $name -SwitchType {request.Type} | Out-Null; ";
        // Resolve the physical NIC again immediately before mutation; -NetAdapterName accepts wildcards.
        return script + $"$a=@(Get-NetAdapter -Physical | Where-Object {{[guid]$_.InterfaceGuid -eq [guid]'{request.PhysicalAdapterId:D}' -and $_.HardwareInterface -and -not $_.Virtual}}); if($a.Count -ne 1){{throw 'Physical adapter unavailable. Refresh before retrying.'}}; "
            + "if($a[0].Name.IndexOfAny([char[]]'*?[]') -ge 0){throw 'Rename the physical adapter to remove wildcard characters first.'}; "
            + $"New-VMSwitch -Name $name -NetAdapterName $a[0].Name -AllowManagementOS:${request.AllowManagementOS.ToString().ToLowerInvariant()} | Out-Null; ";
    }

    internal static string NormalizeSwitchName(string value)
    {
        value = value.Trim();
        if (value.Length is < 1 or > 64 || value.Any(char.IsControl) || value.IndexOfAny(['*', '?', '[', ']']) >= 0)
            throw new ArgumentException("Enter a switch name of 1-64 characters without control or wildcard characters.");
        return "WinXinAi-" + value;
    }

    public static Task CreateSwitchAsync(HyperVSwitchRequest request) => RunMutationAsync(BuildCreateSwitchScript(request));

    internal static string BuildRemoveSwitchScript(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Select a valid virtual switch.");
        return Preamble + $"$s=Get-VMSwitch -Id '{id:D}'; if(-not $s.Name.StartsWith('WinXinAi-',[System.StringComparison]::OrdinalIgnoreCase)){{throw 'Only WinXinAi-prefixed switches can be removed. System and other switches are protected.'}}; "
            + "if(@(Get-VMNetworkAdapter -All | Where-Object { -not $_.IsManagementOs -and $_.SwitchId -eq $s.Id }).Count -gt 0){throw 'Disconnect all VM adapters from this switch in Hyper-V Manager before removing it.'}; "
            + "$s | Remove-VMSwitch -Force -Confirm:$false -ErrorAction Stop; ";
    }

    public static Task RemoveSwitchAsync(Guid id) => RunMutationAsync(BuildRemoveSwitchScript(id));

    private static string Quote(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static async Task RunMutationAsync(string script)
    {
        await OperationGate.WaitAsync();
        try { await RunAsync<JsonElement>(script, parseOutput: false); }
        finally { OperationGate.Release(); }
    }

    private static async Task<T> RunAsync<T>(string script, bool parseOutput = true, TimeSpan? timeoutDuration = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Hyper-V configuration is available only on Windows.");
        // Use the inbox executable, not a PATH-resolved replacement. Sysnative is needed from x86 on x64.
        var system = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var directory = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
        var executable = Path.Combine(system, directory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                ArgumentList = { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }
            }
        };
        if (!process.Start()) throw new InvalidOperationException("Could not start Windows PowerShell.");
        // Do not inherit an open CI/GUI input pipe: servicing cmdlets must see EOF, never wait for input.
        process.StandardInput.Close();
        // Drain both pipes concurrently so verbose DISM output cannot deadlock the application.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(timeoutDuration ?? TimeSpan.FromMinutes(15));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            throw new TimeoutException("Hyper-V operation timed out. Changes may be partial; refresh status and restart Windows before retrying. " + await errorTask.ConfigureAwait(false));
        }
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Hyper-V command failed ({process.ExitCode}). Refresh to check for partial changes." : error.Trim());
        if (!parseOutput) return default!;
        return JsonSerializer.Deserialize<T>(output.Trim(), JsonOptions) ?? throw new InvalidOperationException("Windows returned no Hyper-V status.");
    }
}
