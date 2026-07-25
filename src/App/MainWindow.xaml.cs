using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Zip711.Models;
using Zip711.Services;

namespace Zip711;

public sealed partial class MainWindow : Window
{
    private static readonly HashSet<string> ArchiveExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".7z", ".zip", ".rar", ".gz", ".tar", ".bz2", ".xz", ".tgz", ".tbz",
        ".cab", ".iso", ".wim", ".lzh", ".lzma", ".arj", ".z", ".zst", ".001"
    };

    private readonly SevenZipRunner _engine = new();

    public ObservableCollection<FileItem> Items { get; } = new();

    // Navigation state.
    private string? _currentDir;      // null => "This PC" (drive list)
    private string? _openArchive;     // non-null => browsing inside an archive
    private readonly Stack<Action> _back = new();

    public MainWindow()
    {
        this.InitializeComponent();

        this.SystemBackdrop = new MicaBackdrop();
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);
        this.Title = "711zip";

        FileList.SelectionChanged += (_, _) => UpdateCommandState();

        ApplyTheme(ElementTheme.Default);
        OpenFromCommandLine();
    }

    private void OpenFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            var target = args[1];
            if (Directory.Exists(target)) { NavigateToFolder(target, pushHistory: false); return; }
            if (File.Exists(target) && IsArchive(target)) { _ = OpenArchiveAsync(target, pushHistory: false); return; }
            if (File.Exists(target))
            {
                var dir = Path.GetDirectoryName(target);
                if (dir is not null) { NavigateToFolder(dir, pushHistory: false); return; }
            }
        }
        ShowDrives(pushHistory: false);
    }

    // ---------- Theme ----------

    private void OnToggleTheme(object sender, RoutedEventArgs e)
        => ApplyTheme(RootGrid.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark);

    private void ApplyTheme(ElementTheme theme)
    {
        RootGrid.RequestedTheme = theme;
        int glyph = RootGrid.ActualTheme == ElementTheme.Dark ? 0xE706 : 0xE708;
        ThemeGlyph.Glyph = char.ConvertFromUtf32(glyph);
    }

    // ---------- Navigation ----------

    private void ShowDrives(bool pushHistory = true)
    {
        if (pushHistory) PushCurrent();
        _currentDir = null;
        _openArchive = null;
        Items.Clear();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            Items.Add(new FileItem
            {
                Name = string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.Name : $"{d.VolumeLabel} ({d.Name.TrimEnd('\\')})",
                FullPath = d.RootDirectory.FullName,
                Kind = ItemKind.Drive,
            });
        }
        PathBox.Text = "Этот компьютер";
        SetStatus($"Дисков: {Items.Count}");
        UpdateCommandState();
    }

    private void NavigateToFolder(string path, bool pushHistory = true)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists) { ShowDrives(); return; }
            if (pushHistory) PushCurrent();

            _currentDir = dir.FullName;
            _openArchive = null;
            Items.Clear();

            if (dir.Parent is not null || Path.GetPathRoot(dir.FullName) == dir.FullName)
                Items.Add(new FileItem { Name = "..", FullPath = "..", Kind = ItemKind.UpDir });

            foreach (var sub in dir.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if ((sub.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                Items.Add(new FileItem
                {
                    Name = sub.Name, FullPath = sub.FullName, Kind = ItemKind.Folder,
                    Modified = sub.LastWriteTime,
                });
            }
            foreach (var f in dir.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                if ((f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                Items.Add(new FileItem
                {
                    Name = f.Name, FullPath = f.FullName,
                    Kind = IsArchive(f.Name) ? ItemKind.Archive : ItemKind.File,
                    Size = f.Length, Modified = f.LastWriteTime,
                });
            }
            PathBox.Text = dir.FullName;
            SetStatus($"Элементов: {Items.Count(i => i.Kind != ItemKind.UpDir)}");
            UpdateCommandState();
        }
        catch (Exception ex)
        {
            _ = ShowError("Не удалось открыть папку", ex.Message);
        }
    }

    private async Task OpenArchiveAsync(string archivePath, bool pushHistory = true)
    {
        SetBusy(true);
        try
        {
            var entries = await _engine.ListAsync(archivePath);
            if (pushHistory) PushCurrent();
            _openArchive = archivePath;
            Items.Clear();
            Items.Add(new FileItem { Name = "..", FullPath = "..", Kind = ItemKind.UpDir });
            foreach (var e in entries.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase))
            {
                Items.Add(new FileItem
                {
                    Name = e.Path,
                    FullPath = e.Path,
                    Kind = e.IsDirectory ? ItemKind.Folder : ItemKind.File,
                    Size = e.Size, Modified = e.Modified,
                });
            }
            PathBox.Text = archivePath;
            SetStatus($"Архив: {entries.Count(e => !e.IsDirectory)} файлов");
            UpdateCommandState();
        }
        catch (Exception ex)
        {
            await ShowError("Не удалось открыть архив", ex.Message);
        }
        finally { SetBusy(false); }
    }

    private void OnItemActivated(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FileList.SelectedItem is not FileItem item) return;
        switch (item.Kind)
        {
            case ItemKind.UpDir: OnUp(sender, e); break;
            case ItemKind.Drive:
            case ItemKind.Folder when _openArchive is null:
                NavigateToFolder(item.FullPath); break;
            case ItemKind.Archive:
                _ = OpenArchiveAsync(item.FullPath); break;
            // Folders inside an archive and plain files: no-op for now.
        }
    }

    private void OnUp(object sender, RoutedEventArgs e)
    {
        if (_openArchive is not null)
        {
            var folder = Path.GetDirectoryName(_openArchive);
            if (folder is not null) NavigateToFolder(folder); else ShowDrives();
            return;
        }
        if (_currentDir is null) return; // already at This PC
        var parent = Directory.GetParent(_currentDir);
        if (parent is not null) NavigateToFolder(parent.FullName); else ShowDrives();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_back.Count > 0) _back.Pop().Invoke();
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (_openArchive is not null) _ = OpenArchiveAsync(_openArchive, pushHistory: false);
        else if (_currentDir is not null) NavigateToFolder(_currentDir, pushHistory: false);
        else ShowDrives(pushHistory: false);
    }

    // ---------- Extract ----------

    private async void OnExtract(object sender, RoutedEventArgs e)
    {
        try
        {
            string archive;
            IEnumerable<string>? only = null;

            if (_openArchive is not null)
            {
                archive = _openArchive;
                var sel = FileList.SelectedItems.OfType<FileItem>()
                    .Where(i => i.Kind != ItemKind.UpDir).Select(i => i.FullPath).ToList();
                if (sel.Count > 0) only = sel;
            }
            else
            {
                if (FileList.SelectedItem is not FileItem fi || fi.Kind != ItemKind.Archive)
                {
                    await ShowError("Нечего распаковывать", "Выберите архив.");
                    return;
                }
                archive = fi.FullPath;
            }

            var name = Path.GetFileNameWithoutExtension(archive);
            var dest = Path.Combine(Path.GetDirectoryName(archive)!, name);

            SetBusy(true);
            await _engine.ExtractAsync(archive, dest, only);
            SetBusy(false);
            await ShowInfo("Готово", $"Распаковано в:\n{dest}");
        }
        catch (Exception ex)
        {
            SetBusy(false);
            await ShowError("Ошибка распаковки", ex.Message);
        }
    }

    // ---------- Helpers ----------

    private static bool IsArchive(string name) => ArchiveExts.Contains(Path.GetExtension(name));

    private void PushCurrent()
    {
        var dir = _currentDir; var arc = _openArchive;
        _back.Push(() =>
        {
            if (arc is not null) _ = OpenArchiveAsync(arc, pushHistory: false);
            else if (dir is not null) NavigateToFolder(dir, pushHistory: false);
            else ShowDrives(pushHistory: false);
        });
    }

    private void UpdateCommandState()
    {
        UpButton.IsEnabled = _currentDir is not null || _openArchive is not null;
        BackButton.IsEnabled = _back.Count > 0;
        bool canExtract = _openArchive is not null
            || FileList.SelectedItems.OfType<FileItem>().Any(i => i.Kind == ItemKind.Archive);
        ExtractButton.IsEnabled = canExtract && _engine.EngineAvailable;
    }

    private void SetBusy(bool busy) => Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    private void SetStatus(string text) => StatusText.Text = text;

    private Task ShowInfo(string title, string message) => ShowDialog(title, message);
    private Task ShowError(string title, string message) => ShowDialog(title, message);

    private async Task ShowDialog(string title, string message)
    {
        var dlg = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dlg.ShowAsync();
    }
}
