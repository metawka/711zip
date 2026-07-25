using System;

namespace Zip711.Models;

public enum ItemKind { Drive, Folder, Archive, File, UpDir, Favorites }

public sealed class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public ItemKind Kind { get; set; }
    public long Size { get; set; }
    public DateTimeOffset? Modified { get; set; }

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
        _                  => 0xE8A5, // Document
    });

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
