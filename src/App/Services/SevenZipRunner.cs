using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Zip711.Services;

/// <summary>
/// Thin wrapper around the bundled 7z.exe. Listing uses "-slt" (technical
/// listing, one "Key = Value" per line) which is stable and easy to parse.
/// </summary>
public sealed class SevenZipRunner
{
    public string ExePath { get; }

    public SevenZipRunner()
    {
        ExePath = Path.Combine(AppContext.BaseDirectory, "engine", "7z.exe");
    }

    public bool EngineAvailable => File.Exists(ExePath);

    public sealed record ArchiveEntry(
        string Path,
        long Size,
        DateTimeOffset? Modified,
        bool IsDirectory);

    /// <summary>Lists the contents of an archive.</summary>
    public async Task<IReadOnlyList<ArchiveEntry>> ListAsync(string archivePath)
    {
        var (exit, stdout, stderr) = await RunAsync("l", "-slt", "-sccUTF-8", archivePath);
        if (exit != 0)
            throw new InvalidOperationException($"7-Zip не смог прочитать архив (код {exit}).\n{stderr}");

        var entries = new List<ArchiveEntry>();
        string? path = null;
        long size = 0;
        DateTimeOffset? modified = null;
        bool isDir = false;
        bool inBody = false;

        void Flush()
        {
            if (path is not null)
                entries.Add(new ArchiveEntry(path, size, modified, isDir));
            path = null; size = 0; modified = null; isDir = false;
        }

        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            // The body (per-file records) starts after the first divider line.
            if (!inBody)
            {
                if (line.StartsWith("----------")) inBody = true;
                continue;
            }

            if (line.Length == 0) { Flush(); continue; }

            int eq = line.IndexOf(" = ", StringComparison.Ordinal);
            if (eq < 0) continue;
            var key = line[..eq];
            var val = line[(eq + 3)..];

            switch (key)
            {
                case "Path": path = val; break;
                case "Size": long.TryParse(val, out size); break;
                case "Folder": isDir = val == "+"; break;
                case "Attributes": if (val.Contains('D')) isDir = true; break;
                case "Modified":
                    if (DateTimeOffset.TryParse(val, out var dt)) modified = dt;
                    break;
            }
        }
        Flush();
        return entries;
    }

    /// <summary>Extracts an archive (optionally a subset of entries) into destDir, keeping folder structure.</summary>
    public async Task ExtractAsync(string archivePath, string destDir, IEnumerable<string>? onlyPaths = null)
    {
        Directory.CreateDirectory(destDir);
        var args = new List<string> { "x", archivePath, $"-o{destDir}", "-y", "-sccUTF-8" };
        if (onlyPaths is not null)
            foreach (var p in onlyPaths)
                args.Add(p); // include filter: literal entry path
        var (exit, _, stderr) = await RunAsync(args.ToArray());
        if (exit != 0)
            throw new InvalidOperationException($"Распаковка не удалась (код {exit}).\n{stderr}");
    }

    /// <summary>Creates (or updates) an archive from the given files/folders.</summary>
    public async Task CreateAsync(
        string archivePath,
        IEnumerable<string> sources,
        string format = "7z",
        int level = 5,
        string? password = null)
    {
        var args = new List<string> { "a", $"-t{format}", $"-mx={level}", "-y", "-sccUTF-8" };
        if (!string.IsNullOrEmpty(password))
        {
            args.Add($"-p{password}");
            if (format.Equals("7z", StringComparison.OrdinalIgnoreCase))
                args.Add("-mhe=on"); // encrypt headers (7z only)
        }
        args.Add(archivePath);
        args.Add("--"); // stop switch parsing; everything after is a path
        foreach (var s in sources) args.Add(s);

        var (exit, _, stderr) = await RunAsync(args.ToArray());
        if (exit != 0)
            throw new InvalidOperationException($"Не удалось создать архив (код {exit}).\n{stderr}");
    }

    /// <summary>Tests archive integrity (7z t). Returns the tool's report text.</summary>
    public async Task<(bool ok, string report)> TestAsync(string archivePath, string? password = null)
    {
        var args = new List<string> { "t", archivePath, "-sccUTF-8" };
        if (!string.IsNullOrEmpty(password)) args.Add($"-p{password}");
        var (exit, stdout, stderr) = await RunAsync(args.ToArray());
        return (exit == 0, exit == 0 ? stdout : stdout + stderr);
    }

    /// <summary>Adds files/folders into an existing archive; format is inferred from its extension.</summary>
    public async Task AddAsync(string archivePath, IEnumerable<string> sources)
    {
        var args = new List<string> { "a", "-y", "-sccUTF-8", archivePath, "--" };
        foreach (var s in sources) args.Add(s);
        var (exit, _, stderr) = await RunAsync(args.ToArray());
        if (exit != 0)
            throw new InvalidOperationException($"Не удалось добавить в архив (код {exit}).\n{stderr}");
    }

    /// <summary>Renames an entry inside an archive (7z rn old new).</summary>
    public async Task RenameEntryAsync(string archivePath, string oldPath, string newPath)
    {
        var (exit, _, stderr) = await RunAsync("rn", archivePath, "-y", "-sccUTF-8", oldPath, newPath);
        if (exit != 0)
            throw new InvalidOperationException($"Не удалось переименовать в архиве (код {exit}).\n{stderr}");
    }

    /// <summary>Deletes entries from an archive (7z d).</summary>
    public async Task DeleteEntriesAsync(string archivePath, IEnumerable<string> entryPaths)
    {
        var args = new List<string> { "d", archivePath, "-y", "-sccUTF-8", "--" };
        foreach (var p in entryPaths) args.Add(p);
        var (exit, _, stderr) = await RunAsync(args.ToArray());
        if (exit != 0)
            throw new InvalidOperationException($"Не удалось удалить из архива (код {exit}).\n{stderr}");
    }

    private async Task<(int exit, string stdout, string stderr)> RunAsync(params string[] args)
    {
        if (!EngineAvailable)
            throw new FileNotFoundException("Движок 7z.exe не найден", ExePath);

        var psi = new ProcessStartInfo
        {
            FileName = ExePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        var outSb = new StringBuilder();
        var errSb = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) outSb.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) errSb.AppendLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, outSb.ToString(), errSb.ToString());
    }
}
