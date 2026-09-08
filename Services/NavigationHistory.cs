namespace PortManager.Services;

/// <summary>Tag history independent of page instances and display language.</summary>
public sealed class NavigationHistory
{
    private readonly List<string> _backStack = new();
    public string Current { get; private set; } = "Dashboard";
    public bool CanGoBack => _backStack.Count > 0;
    public string? Previous => CanGoBack ? _backStack[^1] : null;

    public void Visit(string tag)
    {
        if (tag == Current) return;
        _backStack.Add(Current);
        if (_backStack.Count > 64) _backStack.RemoveAt(0);
        Current = tag;
    }

    public void GoBack()
    {
        if (!CanGoBack) return;
        Current = _backStack[^1];
        _backStack.RemoveAt(_backStack.Count - 1);
    }
}
