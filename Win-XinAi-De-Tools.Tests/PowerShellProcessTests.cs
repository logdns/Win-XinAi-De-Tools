using System.Diagnostics;
using System.Text.Json;
using PortManager.Services;
using Xunit;

namespace PortManager.Tests;

public sealed class PowerShellProcessTests
{
    [Fact]
    public void ConfiguresUtf8AtBothEndsWithoutChangingSystemCodePage()
    {
        const string script = "[pscustomobject]@{Name='以太网';InterfaceAlias='本地连接'} | ConvertTo-Json -Compress";
        var info = PowerShellProcess.CreateStartInfo(script);
        Assert.Equal(65001, info.StandardOutputEncoding!.CodePage);
        Assert.Equal(65001, info.StandardErrorEncoding!.CodePage);
        Assert.Contains("[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)", info.ArgumentList[^1]);
        Assert.Contains("$OutputEncoding=[Console]::OutputEncoding", info.ArgumentList[^1]);
        Assert.EndsWith(script, info.ArgumentList[^1]);
        Assert.EndsWith(Path.Combine("WindowsPowerShell", "v1.0", "powershell.exe"), info.FileName);
        Assert.DoesNotContain("chcp", info.ArgumentList[^1]);
        Assert.False(info.UseShellExecute);
        Assert.True(info.RedirectStandardInput);
    }

    [Fact]
    public async Task WindowsPowerShellPreservesChineseJsonAndErrorBytes()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string script = "[pscustomobject]@{Name='以太网';InterfaceAlias='本地连接';SharePath='C:\\共享目录'} | ConvertTo-Json -Compress; [Console]::Error.WriteLine('中文错误：网卡不可用'); Write-Error '中文操作失败' -ErrorAction Continue; exit 7";
        using var process = Process.Start(PowerShellProcess.CreateStartInfo(script))!;
        process.StandardInput.Close();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        var output = await outputTask;
        using var json = JsonDocument.Parse(output);
        Assert.Equal("以太网", json.RootElement.GetProperty("Name").GetString());
        Assert.Equal("本地连接", json.RootElement.GetProperty("InterfaceAlias").GetString());
        Assert.Equal("C:\\共享目录", json.RootElement.GetProperty("SharePath").GetString());
        var error = await errorTask;
        Assert.Contains("中文错误：网卡不可用", error);
        Assert.Contains("中文操作失败", error);
        Assert.DoesNotContain('\uFFFD', output);
        Assert.Equal(7, process.ExitCode);
    }
}
