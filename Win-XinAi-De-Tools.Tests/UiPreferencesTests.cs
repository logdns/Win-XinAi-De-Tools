using System;
using System.IO;
using PortManager.Services;
using Xunit;

namespace WinXinAiDeTools.Tests;

public sealed class UiPreferencesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WinXinAiUiTests", Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "ui-preferences.json");

    [Fact]
    public void ThemeAndLanguageSurviveRestartAndReplacement()
    {
        var preferences = new UiPreferences { Theme = AppTheme.Dark, Language = AppLanguage.English };
        preferences.Save(SettingsPath);
        var restored = UiPreferences.Load(SettingsPath);
        Assert.Equal(AppTheme.Dark, restored.Theme);
        Assert.Equal(AppLanguage.English, restored.Language);
        restored.Theme = AppTheme.System;
        restored.Save(SettingsPath);
        Assert.Equal(AppTheme.System, UiPreferences.Load(SettingsPath).Theme);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Theory]
    [InlineData("invalid json")]
    [InlineData("null")]
    [InlineData("{\"Theme\":999,\"Language\":-1}")]
    public void DamagedPreferencesFallBackToDefaults(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, json);
        Assert.Equal(AppTheme.System, UiPreferences.Load(SettingsPath).Theme);
        Assert.Equal(AppLanguage.Chinese, UiPreferences.Load(SettingsPath).Language);
    }

    [Fact]
    public void FirstLaunchNeedsNoExistingFile()
    {
        Assert.Equal(AppTheme.System, UiPreferences.Load(SettingsPath).Theme);
        Assert.False(Directory.Exists(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
