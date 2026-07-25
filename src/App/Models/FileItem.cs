using System;
using System.Collections.Generic;
using System.IO;

namespace Zip711.Models;

public enum ItemKind { Drive, Folder, Archive, File, UpDir, Favorites }

public sealed class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public ItemKind Kind { get; set; }
    public long Size { get; set; }
    public DateTimeOffset? Modified { get; set; }

    /// <summary>Optional second line under the name (used to show the full path in Favorites).</summary>
    public string Subtitle { get; set; } = "";
    public Microsoft.UI.Xaml.Visibility SubtitleVisibility =>
        string.IsNullOrEmpty(Subtitle) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public bool IsContainer =>
        Kind is ItemKind.Drive or ItemKind.Folder or ItemKind.Archive or ItemKind.UpDir or ItemKind.Favorites;

    // Segoe Fluent Icons code points (kept as ints so no private-use chars live in source).
    public string Glyph => char.ConvertFromUtf32(Kind switch
    {
        ItemKind.Drive     => 0xEDA2, // Hard drive
        ItemKind.Folder    => 0xE8B7, // Folder
        ItemKind.Archive   => 0xF012, // Zip folder
        ItemKind.UpDir     => 0xE74A, // Up
        ItemKind.Favorites => 0xE735, // Filled star
        _                  => FileGlyph(Name),
    });

    // Distinct glyphs by file type so the list reads at a glance.
    private static int FileGlyph(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (Images.Contains(ext))   return 0xEB9F; // Photo
        if (Video.Contains(ext))    return 0xE714; // Video
        if (Audio.Contains(ext))    return 0xEC4F; // MusicNote
        if (Pdf.Contains(ext))      return 0xEA90; // PDF-ish document
        if (Code.Contains(ext))     return 0xE943; // Code
        if (Docs.Contains(ext))     return 0xE8A5; // Document
        if (Sheets.Contains(ext))   return 0xE9F9; // Calculator/grid
        if (Exe.Contains(ext))      return 0xECAA; // App
        if (Fonts.Contains(ext))    return 0xE8D2; // Font
        return 0xE7C3;                              // Generic page
    }

    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
    { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tif", ".tiff", ".svg", ".heic", ".raw" };
    private static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase)
    { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".m4v", ".mpg", ".mpeg", ".ts" };
    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus", ".mid" };
    private static readonly HashSet<string> Pdf = new(StringComparer.OrdinalIgnoreCase)
    { ".pdf" };
    private static readonly HashSet<string> Code = new(StringComparer.OrdinalIgnoreCase)
    { ".cs", ".js", ".ts", ".jsx", ".tsx", ".html", ".htm", ".css", ".py", ".c", ".cpp", ".cc",
      ".h", ".hpp", ".java", ".go", ".rs", ".sql", ".ps1", ".sh", ".bat", ".cmd", ".json", ".xml",
      ".yml", ".yaml", ".php", ".rb", ".lua", ".xaml", ".toml", ".ini", ".cfg" };
    private static readonly HashSet<string> Docs = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".md", ".log", ".doc", ".docx", ".rtf", ".odt", ".pages" };
    private static readonly HashSet<string> Sheets = new(StringComparer.OrdinalIgnoreCase)
    { ".xls", ".xlsx", ".csv", ".ods" };
    private static readonly HashSet<string> Exe = new(StringComparer.OrdinalIgnoreCase)
    { ".exe", ".msi", ".bat", ".com", ".dll", ".sys", ".appx", ".msix" };
    private static readonly HashSet<string> Fonts = new(StringComparer.OrdinalIgnoreCase)
    { ".ttf", ".otf", ".woff", ".woff2", ".fon" };

    public string SizeText => Kind is ItemKind.Folder or ItemKind.Drive or ItemKind.UpDir or ItemKind.Favorites
        ? ""
        : FormatSize(Size);

    public string ModifiedText => Modified?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "";

    private static readonly string[] Units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 Б";
        double v = bytes; int u = 0;
        while (v >= 1024 && u < Units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} Б" : $"{v:0.#} {Units[u]}";
    }
}
