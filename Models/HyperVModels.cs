namespace PortManager.Models;

public enum HyperVSwitchType { Internal, Private, External }

public sealed class HyperVFeature
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string DisplayName => $"{Name}: {State}";
}

public sealed class HyperVAdapter
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DisplayName => $"{Name} ({Status})";
}

public sealed class HyperVSwitch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SwitchType { get; set; } = string.Empty;
    public bool AllowManagementOS { get; set; }
    public bool CanRemove => Name.StartsWith("WinXinAi-", StringComparison.OrdinalIgnoreCase);
    public string DisplayName => $"{Name} ({SwitchType}; Host={AllowManagementOS})";
}

public sealed class HyperVStatus
{
    public List<HyperVFeature> Features { get; set; } = new();
    public List<HyperVAdapter> VirtualAdapters { get; set; } = new();
    public List<HyperVAdapter> PhysicalAdapters { get; set; } = new();
    public List<HyperVSwitch> Switches { get; set; } = new();
    public bool HypervisorPresent { get; set; }
    public string BootLaunchType { get; set; } = string.Empty;
    public bool ManagementAvailable { get; set; }
    public string NetworkError { get; set; } = string.Empty;
    public string SwitchError { get; set; } = string.Empty;
}

public sealed record HyperVSwitchRequest(string Name, HyperVSwitchType Type, Guid PhysicalAdapterId, bool AllowManagementOS);
