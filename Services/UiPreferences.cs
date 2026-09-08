using System.Text.Json;

namespace PortManager.Services;

public enum AppTheme { System, Light, Dark }

public sealed class UiPreferences
{
    public AppTheme Theme { get; set; }
    public AppLanguage Language { get; set; }

    public static UiPreferences Load(string path)
    {
        try
        {
            var preferences = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(path)) ?? new();
            if (!Enum.IsDefined(preferences.Theme)) preferences.Theme = AppTheme.System;
            if (!Enum.IsDefined(preferences.Language)) preferences.Language = AppLanguage.Chinese;
            return preferences;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, path, overwrite: true);
    }
}
