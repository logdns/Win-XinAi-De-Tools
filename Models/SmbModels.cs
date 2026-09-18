using System;

namespace PortManager.Models;

public sealed class SmbFeatureStatus
{
    public bool SmbDirectEnabled { get; init; }
    public bool Smb1Enabled { get; init; }
    public string ShareName { get; init; } = "share";
    public string SharePath { get; init; } = string.Empty;
    public bool ShareExists { get; init; }
}

public sealed class SmbFeatureRequest
{
    public bool SmbDirectEnabled { get; init; }
    public bool Smb1Enabled { get; init; }
    public bool ForceRestart { get; init; }
}

public sealed record SmbAccessAddress(string WindowsPath, string MacUrl);

public sealed class SmbOperationException : Exception
{
    public SmbOperationException(string message) : base(message) { }
}
