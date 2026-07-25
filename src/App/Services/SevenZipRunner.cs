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
