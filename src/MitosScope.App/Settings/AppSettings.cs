using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MitosScope.App.Settings;

public sealed class AppSettings
{
    /// <summary>
    /// Executable names of the game client (without ".exe"). When a process with one of these
    /// names starts or exits, the API ports are scanned. Empty = scan periodically instead.
    /// </summary>
    public List<string> GameProcessNames { get; set; } = ["Mitosis"];

    /// <summary>Position/size of each panel, keyed by panel id.</summary>
    public Dictionary<string, PanelLayout> Panels { get; set; } = [];
}

public sealed class PanelLayout
{
    public int X { get; set; }
    public int Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    /// <summary>Master switch (tray checkbox / ✕ button in edit mode).</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Show the panel while the player is in the menu (not in a match, dead, replay, no client).</summary>
    public bool ShowInMenu { get; set; } = true;

    /// <summary>Show the panel while the player is in a match (game.in_game).</summary>
    public bool ShowInMatch { get; set; } = true;
}

/// <summary>Reads/writes %APPDATA%\MitosScope\settings.json.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MitosScope");

    public static string FilePath { get; } = Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"[MitosScope] Failed to read settings, using defaults: {ex.Message}");
        }

        var settings = new AppSettings();
        Save(settings); // create the file so the user can find and edit it
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"[MitosScope] Failed to save settings: {ex.Message}");
        }
    }
}
