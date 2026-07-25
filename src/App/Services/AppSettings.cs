using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zip711.Models;

namespace Zip711.Services;

/// <summary>A single pinned entry in the Favorites virtual folder.</summary>
public sealed class FavoriteEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public ItemKind Kind { get; set; } = ItemKind.Folder;
}

/// <summary>
/// User settings persisted to %AppData%\711zip\settings.json.
/// Kept deliberately small — theme, favorites and default archive options.
/// </summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Default";        // Default | Light | Dark
    public string DefaultFormat { get; set; } = "7z";      // 7z | zip
    public int DefaultLevel { get; set; } = 5;             // 0..9 (7z -mx)
    public bool ShowHidden { get; set; } = false;
    public bool ConfirmDelete { get; set; } = true;
    public string LastPath { get; set; } = "";              // reopened on next launch
    public List<FavoriteEntry> Favorites { get; set; } = new();

    // ---------- Persistence ----------

    [JsonIgnore]
    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "711zip");

    [JsonIgnore]
    public static string FilePath => Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (s is not null) return s;
            }
        }
        catch { /* corrupt/locked settings fall back to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* never let a settings write crash the app */ }
    }
}
