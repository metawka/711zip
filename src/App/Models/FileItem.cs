using System;

namespace Zip711.Models;

public enum ItemKind { Drive, Folder, Archive, File, UpDir }

public sealed class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public ItemKind Kind { get; set; }
    public long Size { get; set; }
    public DateTimeOffset? Modified { get; set; }

    public bool IsContainer => Kind is ItemKind.Drive or ItemKind.Folder or ItemKind.Archive or ItemKind.UpDir;

    // Segoe Fluent Icons glyphs (\u escapes so no private-use chars live in source).
    public string Glyph => Kind switch
    {
        ItemKind.Drive   => "", // Hard drive
        ItemKind.Folder  => "", // Folder
        ItemKind.Archive => "", // Zip folder
        ItemKind.UpDir   => "", // Up
        _                => "", // Document
    };

    public string SizeText => Kind is ItemKind.Folder or ItemKind.Drive or ItemKind.UpDir
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
