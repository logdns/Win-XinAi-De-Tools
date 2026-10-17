using System.Linq;
using System.Text;
using PortManager.Models;
using PortManager.Services;
using Xunit;

namespace PortManager.Tests;

public sealed class WslServiceTests
{
    [Fact]
    public void DecodeLinuxOutputAndWindowsLauncherErrorsWithoutMojibake()
    {
        const string message = "适用于 Linux 的 Windows 子系统没有已安装的分发。\r\nWsl/Service/WSL_E_DISTRO_NOT_FOUND\r\n";
        foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode })
        {
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(message)).ToArray();
            Assert.Equal(message, WslService.DecodeWslOutput(bytes, Encoding.UTF8));
        }
        Assert.Equal(message, WslService.DecodeWslOutput(Encoding.Unicode.GetBytes(message), Encoding.UTF8));
        const string linux = "文件系统 Size Used Avail Use% Mounted on\n/dev/sda 100G 10G 90G 10% /\n";
        Assert.Equal(linux, WslService.DecodeWslOutput(Encoding.UTF8.GetBytes(linux), Encoding.UTF8));
        Assert.Equal("", WslService.DecodeWslOutput(Array.Empty<byte>(), Encoding.UTF8));
    }

    [Fact]
    public void ParseDistributions_HandlesDefaultAndStates()
    {
        var rows = WslService.ParseDistributions("  NAME                   STATE           VERSION\n* Ubuntu                 Running         2\n  Debian                 Stopped         2\n");

        Assert.Equal(2, rows.Count);
        var ubuntu = Assert.Single(rows, row => row.Name == "Ubuntu");
        Assert.True(ubuntu.IsDefault);
        Assert.Equal("Running", ubuntu.State);
        Assert.Equal(2, ubuntu.Version);
        var debian = Assert.Single(rows, row => row.Name == "Debian");
        Assert.False(debian.IsDefault);
    }

    [Theory]
    [InlineData("WslRegisterDistribution failed because WSL is not installed.")]
    [InlineData("未安装适用于 Linux 的 Windows 子系统。")]
    [InlineData("Please enable the Virtual Machine Platform Windows feature.")]
    public void IsNotInstalledMessage_RecognizesSetupErrors(string message)
    {
        Assert.True(WslService.IsNotInstalledMessage(message));
    }

    [Theory]
    [InlineData("There are no installed distributions.")]
    [InlineData("没有安装发行版。")]
    public void IsNoDistributionMessage_RecognizesEmptyInstall(string message)
    {
        Assert.True(WslService.IsNoDistributionMessage(message));
    }

    [Fact]
    public void ParseDistributions_SkipsLocalizedHeader()
    {
        var rows = WslService.ParseDistributions("名称 状态 版本\n* Ubuntu Running 2\n");

        var distro = Assert.Single(rows);
        Assert.Equal("Ubuntu", distro.Name);
        Assert.True(distro.IsDefault);
    }

    [Fact]
    public void ApplyRunningStates_DoesNotTrustLocalizedVerboseState()
    {
        var rows = WslService.ParseDistributions("名称 状态 版本\n* Ubuntu 已停止 2\n  Debian 正在运行 2\n");

        var normalized = WslService.ApplyRunningStates(rows, new[] { "Ubuntu" });

        Assert.Equal("Running", Assert.Single(normalized, row => row.Name == "Ubuntu").State);
        Assert.Equal("Stopped", Assert.Single(normalized, row => row.Name == "Debian").State);
    }

    [Fact]
    public void ParseQuietDistributionNames_HandlesUtf16NullsAndWhitespace()
    {
        var rows = WslService.ParseQuietDistributionNames("Ubuntu\0\r\n Debian \0\r\n");

        Assert.Equal(new[] { "Ubuntu", "Debian" }, rows);
    }

    [Fact]
    public void ParseOnlineDistributions_HandlesLocalizedHeaders()
    {
        var english = WslService.ParseOnlineDistributions("The following distributions can be installed.\n\nNAME FRIENDLY NAME\nUbuntu Ubuntu\nDebian Debian GNU/Linux\n");
        var chinese = WslService.ParseOnlineDistributions("以下是可安装的有效发行版列表。\n\n名称 友好名称\nUbuntu Ubuntu\nkali-linux Kali Linux Rolling\n");

        Assert.Equal(new[] { "Ubuntu", "Debian" }, english);
        Assert.Equal(new[] { "Ubuntu", "kali-linux" }, chinese);
    }

    [Fact]
    public void EnsureSuccessfulInstallerExitCode_AcceptsSuccess()
    {
        WslService.EnsureSuccessfulInstallerExitCode(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void EnsureSuccessfulInstallerExitCode_ReportsFailure(int exitCode)
    {
        var error = Assert.Throws<WslOperationException>(() => WslService.EnsureSuccessfulInstallerExitCode(exitCode));

        Assert.Contains(exitCode.ToString(), error.Message);
        Assert.Contains("0x", error.Message);
    }

    [Theory]
    [InlineData("Ubuntu", "Ubuntu")]
    [InlineData("", "\"\"")]
    [InlineData("Ubuntu Dev", "\"Ubuntu Dev\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("C:\\WSL Data\\", "\"C:\\WSL Data\\\\\"")]
    public void QuoteWindowsArgument_ProtectsScheduledTaskValues(string value, string expected)
    {
        Assert.Equal(expected, WslService.QuoteWindowsArgument(value));
    }
}
