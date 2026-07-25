using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Zip711.Services;

/// <summary>
/// Filesystem side-effects used by the file manager: delete to the Recycle Bin
/// (via SHFileOperation so items are recoverable) plus recursive copy/move.
/// </summary>
public static class ShellOps
{
    // ---------- Recycle Bin ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_SILENT = 0x0004;

    /// <summary>Sends the given paths to the Recycle Bin. Confirmation is handled by the caller.</summary>
    public static void RecycleDelete(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        // pFrom must be a double-null-terminated list of full paths.
        var buffer = string.Join('\0', paths) + "\0\0";
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = buffer,
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT),
        };
        int rc = SHFileOperation(ref op);
        if (rc != 0)
            throw new IOException($"Не удалось удалить (код {rc}).");
    }

    // ---------- Copy / Move ----------

    /// <summary>Copies a file or directory tree into destDir under its own name.</summary>
    public static void CopyInto(string source, string destDir)
    {
        Directory.CreateDirectory(destDir);
        if (Directory.Exists(source))
            CopyDir(source, Path.Combine(destDir, new DirectoryInfo(source).Name));
        else
            File.Copy(source, UniqueTarget(Path.Combine(destDir, Path.GetFileName(source))), overwrite: false);
    }

    /// <summary>Moves a file or directory into destDir under its own name.</summary>
    public static void MoveInto(string source, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var name = Directory.Exists(source) ? new DirectoryInfo(source).Name : Path.GetFileName(source);
        var target = UniqueTarget(Path.Combine(destDir, name));
        if (Directory.Exists(source)) Directory.Move(source, target);
        else File.Move(source, target);
    }

    private static void CopyDir(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: false);
        foreach (var sub in Directory.EnumerateDirectories(sourceDir))
            CopyDir(sub, Path.Combine(targetDir, Path.GetFileName(sub)));
    }

    /// <summary>Avoids clobbering an existing target by appending " (2)", " (3)"…</summary>
    private static string UniqueTarget(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}
