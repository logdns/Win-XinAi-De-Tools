namespace PortManager.Models;

public enum NetworkResetMode { FlushDns, AdapterDhcp, SystemStack }

public sealed record NetworkResetRequest(NetworkResetMode Mode, Guid AdapterId = default);
public sealed record NetworkResetStep(string Name, bool Success, string Output);
public sealed record NetworkResetResult(string BackupPath, bool RestartRequired, IReadOnlyList<NetworkResetStep> Steps)
{
    public bool Success => Steps.Count > 0 && Steps.All(step => step.Success);
}
