namespace PortManager.Services;

internal sealed class TemporaryHttpFirewall : ITemporaryHttpFirewall
{
    public async Task OpenAsync(string ruleName, int port)
    {
        var result = await FirewallService.AddRuleAsync(port, "TCP", "in", ruleName, Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to identify the HTTP hosting executable.")).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
    }

    public async Task CloseAsync(string ruleName)
    {
        // Missing is already clean; other COM failures are surfaced and remain retryable.
        await FirewallService.DeleteRuleAsync(ruleName).ConfigureAwait(false);
    }
}
