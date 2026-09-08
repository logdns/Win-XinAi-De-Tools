using PortManager.Services;
using Xunit;

namespace WinXinAiDeTools.Tests;

public class NavigationHistoryTests
{
    [Fact]
    public void BackFollowsOriginIncludingTopLevelTools()
    {
        var history = new NavigationHistory();
        history.Visit("NetworkSettings");
        history.Visit("SmbSettings");
        Assert.Equal("NetworkSettings", history.Previous);
        history.GoBack();
        Assert.Equal("NetworkSettings", history.Current);
        history.GoBack();
        Assert.Equal("Dashboard", history.Current);
        Assert.False(history.CanGoBack);
        history.GoBack();
        Assert.Equal("Dashboard", history.Current);
    }

    [Fact]
    public void RepeatedRouteDoesNotCreateLoopAndBranchUsesCurrentOrigin()
    {
        var history = new NavigationHistory();
        history.Visit("ComingSoon");
        history.Visit("AuditLog");
        history.Visit("AuditLog");
        history.GoBack();
        Assert.Equal("ComingSoon", history.Current);
        history.Visit("RuleTransfer");
        history.GoBack();
        history.GoBack();
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void HistoryIsBoundedDuringLongSessions()
    {
        var history = new NavigationHistory();
        for (var i = 0; i < 100; i++) history.Visit($"page-{i}");
        var count = 0;
        while (history.CanGoBack) { history.GoBack(); count++; }
        Assert.Equal(64, count);
    }
}
