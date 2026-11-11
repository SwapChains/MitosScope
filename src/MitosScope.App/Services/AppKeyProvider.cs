using System;
using System.IO;

namespace MitosScope.App.Services;

/// <summary>
/// Loads the addon app key from API_KEY.txt next to the executable (copied from the repo root at
/// build time; the file is git-ignored). ADDON_API.md: the key ships with the addon, it's not a
/// secret from the player, but it shouldn't be published separately.
/// </summary>
public static class AppKeyProvider
{
    public const string FileName = "API_KEY.txt";

    public static string? TryLoad()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        if (!File.Exists(path))
            return null;
        var key = File.ReadAllText(path).Trim();
        return key.Length > 0 ? key : null;
    }
}
