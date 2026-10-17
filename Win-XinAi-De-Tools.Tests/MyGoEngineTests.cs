extern alias Engine;
using System.Text.Json;
using Xunit;
using Dispatcher = Engine::WinXinAi.Engine.Dispatcher;
using Request = Engine::WinXinAi.Engine.Request;

namespace PortManager.Tests;

public sealed class MyGoEngineTests
{
    [Theory]
    [InlineData("network.reset")]
    [InlineData("network.apply")]
    [InlineData("hyperv.complete")]
    [InlineData("firewall.delete")]
    [InlineData("wsl.unregister")]
    [InlineData("smb.share")]
    [InlineData("connections.terminate")]
    [InlineData("http.start")]
    public async Task MutationWithoutConfirmationIsRejectedBeforeTouchingWindows(string op)
    {
        await using var engine = new Dispatcher();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ExecuteAsync(new Request(1, op, JsonSerializer.SerializeToElement(new { }))));
        Assert.Contains("confirmation", error.Message);
    }

    [Fact]
    public async Task HealthAndShutdownArePortableReadOnlyOperations()
    {
        await using var engine = new Dispatcher();
        var health = await engine.ExecuteAsync(new Request(1, "health", default));
        Assert.Contains("1.12.1", JsonSerializer.Serialize(health));
        await engine.ExecuteAsync(new Request(2, "shutdown", default));
    }

    [Fact]
    public async Task UnknownOperationNeverFallsThroughToShellExecution()
    {
        await using var engine = new Dispatcher();
        await Assert.ThrowsAsync<ArgumentException>(() => engine.ExecuteAsync(new Request(1, "cmd.exe /c whoami", default, true)));
    }
}
