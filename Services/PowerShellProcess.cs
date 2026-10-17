using System.Diagnostics;
using System.Text;

namespace PortManager.Services;

internal static class PowerShellProcess
{
    internal static ProcessStartInfo CreateStartInfo(string script)
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var directory = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(system, directory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command" })
            info.ArgumentList.Add(argument);
        // Windows PowerShell 5.1 otherwise writes using the console's legacy code page.
        info.ArgumentList.Add("[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $OutputEncoding=[Console]::OutputEncoding; " + script);
        return info;
    }
}
