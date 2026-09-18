using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using PortManager.Models;

namespace PortManager.Services;

public static class SmbConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ShareName = "share";

    public static IReadOnlyList<SmbAccessAddress> GetAccessAddresses(string shareName = ShareName)
    {
        if (string.IsNullOrWhiteSpace(shareName)) return Array.Empty<SmbAccessAddress>();

        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                    && adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                    and not NetworkInterfaceType.Tunnel)
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address)
                    && !address.GetAddressBytes().AsSpan().StartsWith(new byte[] { 169, 254 }))
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                .Select(address => new SmbAccessAddress($@"\\{address}\{shareName}", $"smb://{address}/{Uri.EscapeDataString(shareName)}"))
                .ToArray();
        }
        catch (NetworkInformationException)
        {
            return Array.Empty<SmbAccessAddress>();
        }
    }

    public static Task<SmbFeatureStatus> GetStatusAsync() => Task.Run(() =>
    {
        EnsureWindows();
        const string script = "$features=@('SMBDirect','SMB1Protocol') | ForEach-Object { Get-WindowsOptionalFeature -Online -FeatureName $_ -ErrorAction SilentlyContinue }; $direct=$features | Where-Object FeatureName -eq 'SMBDirect'; $smb1=$features | Where-Object FeatureName -eq 'SMB1Protocol'; $share=Get-SmbShare -Name 'share' -ErrorAction SilentlyContinue; $sharePath=''; if($share){$sharePath=$share.Path}; [pscustomobject]@{SmbDirectEnabled=($direct.State -eq 'Enabled');Smb1Enabled=($smb1.State -eq 'Enabled');ShareName='share';SharePath=$sharePath;ShareExists=($null -ne $share)} | ConvertTo-Json -Compress";
        var dto = RunPowerShell<StatusDto>(script);
        return new SmbFeatureStatus
        {
            SmbDirectEnabled = dto.SmbDirectEnabled,
            Smb1Enabled = dto.Smb1Enabled,
            ShareName = string.IsNullOrWhiteSpace(dto.ShareName) ? ShareName : dto.ShareName,
            SharePath = dto.SharePath ?? string.Empty,
            ShareExists = dto.ShareExists
        };
    });

    public static Task ApplyFeaturesAsync(SmbFeatureRequest request) => Task.Run(() =>
    {
        EnsureWindows();
        var directCommand = request.SmbDirectEnabled
            ? "Enable-WindowsOptionalFeature -Online -FeatureName SMBDirect -NoRestart -ErrorAction Stop | Out-Null"
            : "Disable-WindowsOptionalFeature -Online -FeatureName SMBDirect -NoRestart -ErrorAction Stop | Out-Null";
        var smb1Command = request.Smb1Enabled
            ? "Enable-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -All -NoRestart -ErrorAction Stop | Out-Null"
            : "Disable-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -NoRestart -ErrorAction Stop | Out-Null";
        RunPowerShell<object>($"{directCommand}; {smb1Command}", parseOutput: false);
        if (request.ForceRestart)
            RunPowerShell<object>("Restart-Computer -Force", parseOutput: false);
    });

    public static Task SetShareAsync(string path) => Task.Run(() =>
    {
        EnsureWindows();
        if (string.IsNullOrWhiteSpace(path))
            throw new SmbOperationException(Message("请输入共享文件夹路径。", "Enter a shared folder path."));
        var escaped = EscapePowerShell(Path.GetFullPath(path.Trim()));
        var script = $"$ErrorActionPreference='Stop'; $path='{escaped}'; if(-not (Test-Path -LiteralPath $path -PathType Container)){{throw 'Folder does not exist.'}}; $serverCommand=Get-Command Set-SmbServerConfiguration -ErrorAction SilentlyContinue; if(-not $serverCommand -or -not $serverCommand.Parameters.ContainsKey('EnableAuthenticateUserSharing')){{throw 'This Windows version does not support unauthenticated SMB sharing.'}}; $clientCommand=Get-Command Set-SmbClientConfiguration -ErrorAction SilentlyContinue; if(-not $clientCommand -or -not $clientCommand.Parameters.ContainsKey('EnableInsecureGuestLogons')){{throw 'This Windows version does not support insecure SMB guest logons.'}}; $guest=Get-CimInstance Win32_UserAccount -Filter \"LocalAccount=True AND SID LIKE '%-501'\" | Select-Object -First 1; if(-not $guest){{throw 'The built-in Guest account was not found.'}}; & net.exe user $guest.Name /active:yes | Out-Null; if($LASTEXITCODE -ne 0){{throw 'Could not enable the built-in Guest account.'}}; Set-SmbServerConfiguration -EnableAuthenticateUserSharing $false -Force; Set-SmbClientConfiguration -EnableInsecureGuestLogons $true -Force; $existing=Get-SmbShare -Name '{ShareName}' -ErrorAction SilentlyContinue; if($existing -and $existing.Path -ne $path){{Remove-SmbShare -Name '{ShareName}' -Force -ErrorAction Stop; $existing=$null}}; if(-not $existing){{New-SmbShare -Name '{ShareName}' -Path $path -ChangeAccess 'Everyone' -ErrorAction Stop | Out-Null}} else {{Grant-SmbShareAccess -Name '{ShareName}' -AccountName 'Everyone' -AccessRight Change -Force -ErrorAction Stop | Out-Null}}; & icacls.exe $path /grant '*S-1-1-0:(OI)(CI)M' /T /C | Out-Null; if($LASTEXITCODE -ne 0){{throw 'Could not grant Everyone modify access to the shared folder.'}}";
        RunPowerShell<object>(script, parseOutput: false);
    });

    public static Task RemoveShareAsync() => Task.Run(() =>
    {
        EnsureWindows();
        const string script = "$ErrorActionPreference='Stop'; Get-SmbShare -Name 'share' -ErrorAction SilentlyContinue | Remove-SmbShare -Force -ErrorAction SilentlyContinue; Set-SmbServerConfiguration -EnableAuthenticateUserSharing $true -Force; Set-SmbClientConfiguration -EnableInsecureGuestLogons $false -Force; $guest=Get-CimInstance Win32_UserAccount -Filter \"LocalAccount=True AND SID LIKE '%-501'\" | Select-Object -First 1; if($guest){& net.exe user $guest.Name /active:no | Out-Null; if($LASTEXITCODE -ne 0){throw 'Could not disable the built-in Guest account.'}}";
        RunPowerShell<object>(script, parseOutput: false);
    });

    private static T RunPowerShell<T>(string script, bool parseOutput = true)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script }
        }) ?? throw new SmbOperationException(Message("无法启动 PowerShell。", "Could not start PowerShell."));
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new SmbOperationException(string.IsNullOrWhiteSpace(error) ? Message("SMB 操作失败。", "SMB operation failed.") : error.Trim());
        if (!parseOutput) return default!;
        try { return JsonSerializer.Deserialize<T>(output.Trim(), JsonOptions) ?? throw new SmbOperationException(Message("Windows 未返回 SMB 状态。", "Windows returned no SMB status.")); }
        catch (JsonException ex) { throw new SmbOperationException($"SMB status could not be read: {ex.Message}"); }
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static void EnsureWindows() { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("SMB configuration is available only on Windows."); }
    private static string Message(string chinese, string english) => LanguageState.IsEnglish ? english : chinese;

    private sealed class StatusDto
    {
        public bool SmbDirectEnabled { get; set; }
        public bool Smb1Enabled { get; set; }
        public string? ShareName { get; set; }
        public string? SharePath { get; set; }
        public bool ShareExists { get; set; }
    }
}
