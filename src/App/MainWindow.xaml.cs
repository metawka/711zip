using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
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

    private enum View { Drives, Folder, Archive, Favorites }

    private readonly SevenZipRunner _engine = new();
    private readonly AppSettings _settings = AppSettings.Load();

    public ObservableCollection<FileItem> Items { get; } = new();

    // Navigation state.
    private View _view = View.Drives;
    private string? _currentDir;
    private string? _openArchive;
    private IReadOnlyList<SevenZipRunner.ArchiveEntry> _archiveEntries = Array.Empty<SevenZipRunner.ArchiveEntry>();
    private string _archiveSubPath = "";       // "" = archive root, otherwise "dir\sub\"
    private readonly Stack<Action> _back = new();

    // Internal clipboard for cut/copy/paste.
    private List<string> _clip = new();
    private bool _clipCut;

    public MainWindow()
    {
        this.InitializeComponent();

        this.SystemBackdrop = new MicaBackdrop();
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);
        this.Title = "711zip";
        SetWindowIcon();

        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = false;

        FileList.SelectionChanged += (_, _) => UpdateCommandState();
        FileList.KeyDown += OnListKeyDown;

        ApplyTheme(ParseTheme(_settings.Theme));
        OpenFromCommandLine();
    }

    private void SetWindowIcon()
    {
        try
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(ico)) AppWindow.SetIcon(ico);
        }
        catch { /* icon is cosmetic */ }
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

    private static ElementTheme ParseTheme(string s) => s switch
    {
        "Light" => ElementTheme.Light,
        "Dark" => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        var next = RootGrid.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        ApplyTheme(next);
        _settings.Theme = next.ToString();
        _settings.Save();
    }

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
        _view = View.Drives;
        _currentDir = null; _openArchive = null;
        Items.Clear();
        Items.Add(new FileItem { Name = "Избранное", FullPath = "::favorites", Kind = ItemKind.Favorites });
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
        SetStatus($"Дисков: {Items.Count - 1}");
        UpdateCommandState();
    }

    private void ShowFavorites(bool pushHistory = true)
    {
        if (pushHistory) PushCurrent();
        _view = View.Favorites;
        _currentDir = null; _openArchive = null;
        Items.Clear();
        Items.Add(new FileItem { Name = "..", FullPath = "..", Kind = ItemKind.UpDir });
        foreach (var f in _settings.Favorites)
        {
            bool exists = f.Kind == ItemKind.Folder ? Directory.Exists(f.Path) : File.Exists(f.Path);
            Items.Add(new FileItem
            {
                Name = f.Name,
                FullPath = f.Path,
                Kind = f.Kind,
                Modified = exists ? (f.Kind == ItemKind.Folder ? new DirectoryInfo(f.Path).LastWriteTime : new FileInfo(f.Path).LastWriteTime) : null,
                Size = exists && f.Kind != ItemKind.Folder ? new FileInfo(f.Path).Length : 0,
            });
        }
        PathBox.Text = "Избранное";
        SetStatus($"В избранном: {_settings.Favorites.Count}");
        UpdateCommandState();
    }

    private void NavigateToFolder(string path, bool pushHistory = true)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists) { ShowDrives(); return; }
            if (pushHistory) PushCurrent();

            _view = View.Folder;
            _currentDir = dir.FullName;
            _openArchive = null;
            Items.Clear();

            Items.Add(new FileItem { Name = "..", FullPath = "..", Kind = ItemKind.UpDir });

            foreach (var sub in dir.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!_settings.ShowHidden && (sub.Attributes & (System.IO.FileAttributes.Hidden | System.IO.FileAttributes.System)) != 0) continue;
                Items.Add(new FileItem { Name = sub.Name, FullPath = sub.FullName, Kind = ItemKind.Folder, Modified = sub.LastWriteTime });
            }
            foreach (var f in dir.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!_settings.ShowHidden && (f.Attributes & (System.IO.FileAttributes.Hidden | System.IO.FileAttributes.System)) != 0) continue;
                Items.Add(new FileItem
                {
                    Name = f.Name, FullPath = f.FullName,
                    Kind = IsArchive(f.Name) ? ItemKind.Archive : ItemKind.File,
                    Size = f.Length, Modified = f.LastWriteTime,
                });
            }
            PathBox.Text = dir.FullName;
            SetStatus($"Элементов: {Items.Count - 1}");
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
            _view = View.Archive;
            _openArchive = archivePath;
            _archiveEntries = entries;
            _archiveSubPath = "";
            RenderArchiveLevel();
        }
        catch (Exception ex)
        {
            await ShowError("Не удалось открыть архив", ex.Message);
        }
        finally { SetBusy(false); }
    }

    private async Task RestoreArchiveAsync(string archivePath, string subPath)
    {
        await OpenArchiveAsync(archivePath, pushHistory: false);
        if (_view == View.Archive && _openArchive == archivePath && subPath.Length > 0)
        {
            _archiveSubPath = subPath;
            RenderArchiveLevel();
        }
    }

    private async Task ReloadArchiveAsync()
    {
        if (_openArchive is null) return;
        _archiveEntries = await _engine.ListAsync(_openArchive);
        RenderArchiveLevel();
    }

    private void RenderArchiveLevel()
    {
        Items.Clear();
        Items.Add(new FileItem { Name = "..", FullPath = "..", Kind = ItemKind.UpDir });
        var prefix = _archiveSubPath;
        var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<FileItem>();
        foreach (var e in _archiveEntries)
        {
            var p = e.Path.Replace('/', '\\');
            if (prefix.Length > 0 && !p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = p.Substring(prefix.Length);
            if (rest.Length == 0) continue;
            int slash = rest.IndexOf('\\');
            if (slash >= 0) { folders.Add(rest[..slash]); }
            else if (e.IsDirectory) { folders.Add(rest); }
            else
            {
                files.Add(new FileItem
                {
                    Name = rest, FullPath = p,
                    Kind = IsArchive(rest) ? ItemKind.Archive : ItemKind.File,
                    Size = e.Size, Modified = e.Modified,
                });
            }
        }
        foreach (var f in folders.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            Items.Add(new FileItem { Name = f, FullPath = prefix + f, Kind = ItemKind.Folder });
        foreach (var f in files.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            Items.Add(f);

        PathBox.Text = prefix.Length > 0 ? $"{_openArchive}\\{prefix.TrimEnd('\\')}" : _openArchive!;
        SetStatus($"Архив: {files.Count} файлов, {folders.Count} папок");
        UpdateCommandState();
    }

    private void OnItemActivated(object sender, DoubleTappedRoutedEventArgs e)
        => ActivateItem(FileList.SelectedItem as FileItem);

    private void ActivateItem(FileItem? item)
    {
        if (item is null) return;
        switch (item.Kind)
        {
            case ItemKind.UpDir: OnUp(this, null!); break;
            case ItemKind.Favorites: ShowFavorites(); break;
            case ItemKind.Drive: NavigateToFolder(item.FullPath); break;
            case ItemKind.Folder when _view == View.Archive:
                _archiveSubPath = item.FullPath + "\\"; RenderArchiveLevel(); break;
            case ItemKind.Folder when _view == View.Favorites:
                NavigateToFolder(item.FullPath); break;
            case ItemKind.Folder:
                NavigateToFolder(item.FullPath); break;
            case ItemKind.Archive when _view == View.Favorites:
                _ = OpenArchiveAsync(item.FullPath); break;
            case ItemKind.Archive when _view == View.Folder:
                _ = OpenArchiveAsync(item.FullPath); break;
            case ItemKind.Archive: // inside another archive: extract+open
            case ItemKind.File when _view == View.Archive:
                _ = ViewArchiveEntryAsync(item); break;
            case ItemKind.File:
                LaunchPath(item.FullPath); break;
        }
    }

    private void OnUp(object sender, RoutedEventArgs e)
    {
        switch (_view)
        {
            case View.Archive when _archiveSubPath.Length > 0:
                var trimmed = _archiveSubPath.TrimEnd('\\');
                int i = trimmed.LastIndexOf('\\');
                _archiveSubPath = i >= 0 ? trimmed[..(i + 1)] : "";
                RenderArchiveLevel();
                break;
            case View.Archive:
                var folder = Path.GetDirectoryName(_openArchive);
                if (folder is not null) NavigateToFolder(folder); else ShowDrives();
                break;
            case View.Favorites:
                ShowDrives();
                break;
            case View.Folder:
                var parent = _currentDir is null ? null : Directory.GetParent(_currentDir);
                if (parent is not null) NavigateToFolder(parent.FullName); else ShowDrives();
                break;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_back.Count > 0) _back.Pop().Invoke();
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => RefreshCurrent();

    private void RefreshCurrent()
    {
        switch (_view)
        {
            case View.Archive: _ = ReloadArchiveAsync(); break;
            case View.Folder when _currentDir is not null: NavigateToFolder(_currentDir, pushHistory: false); break;
            case View.Favorites: ShowFavorites(pushHistory: false); break;
            default: ShowDrives(pushHistory: false); break;
        }
    }

    private void OnPathKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        var text = PathBox.Text.Trim().Trim('"');
        if (string.IsNullOrEmpty(text)) return;
        if (text is "Этот компьютер" or "Избранное") { ShowDrives(); return; }
        if (Directory.Exists(text)) { NavigateToFolder(text); return; }
        if (File.Exists(text) && IsArchive(text)) { _ = OpenArchiveAsync(text); return; }
        if (File.Exists(text)) { LaunchPath(text); return; }
        _ = ShowError("Путь не найден", $"Не удалось перейти:\n{text}");
    }

    private void OnOpenFavorites(object sender, RoutedEventArgs e) => ShowFavorites();

    // ---------- Keyboard ----------

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        switch (e.Key)
        {
            case VirtualKey.Enter: ActivateItem(FileList.SelectedItem as FileItem); e.Handled = true; break;
            case VirtualKey.Back: OnUp(this, null!); e.Handled = true; break;
            case VirtualKey.Delete: _ = ActionDeleteAsync(); e.Handled = true; break;
            case VirtualKey.F2: _ = ActionRenameAsync(); e.Handled = true; break;
            case VirtualKey.F5: RefreshCurrent(); e.Handled = true; break;
            case VirtualKey.C when ctrl: _ = ActionClipboardAsync(cut: false); e.Handled = true; break;
            case VirtualKey.X when ctrl: _ = ActionClipboardAsync(cut: true); e.Handled = true; break;
            case VirtualKey.V when ctrl: _ = ActionPasteAsync(); e.Handled = true; break;
        }
    }

    // ---------- Context menu ----------

    private void OnListRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var item = (e.OriginalSource as FrameworkElement)?.DataContext as FileItem;
        var menu = new MenuFlyout();

        if (item is not null && item.Kind != ItemKind.UpDir && item.Kind != ItemKind.Favorites)
        {
            if (!FileList.SelectedItems.Contains(item)) FileList.SelectedItem = item;
            BuildItemMenu(menu, item);
        }
        else
        {
            BuildBackgroundMenu(menu);
        }

        if (menu.Items.Count > 0)
            menu.ShowAt(FileList, new FlyoutShowOptions { Position = e.GetPosition(FileList) });
    }

    private void BuildItemMenu(MenuFlyout menu, FileItem item)
    {
        var selCount = FileList.SelectedItems.OfType<FileItem>().Count(i => i.Kind != ItemKind.UpDir);

        menu.Items.Add(Mi("Открыть", 0xE8E5, (_, _) => ActivateItem(item)));

        if (_view == View.Archive)
        {
            menu.Items.Add(Mi("Извлечь", 0xE8DE, (_, _) => OnExtract(this, null!)));
            if (item.Kind is ItemKind.File or ItemKind.Archive)
                menu.Items.Add(Mi("Просмотр", 0xE890, (_, _) => _ = ViewArchiveEntryAsync(item)));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Mi("Переименовать", 0xE8AC, (_, _) => _ = ActionRenameAsync(), enabled: selCount == 1));
            menu.Items.Add(Mi("Удалить из архива", 0xE74D, (_, _) => _ = ActionDeleteFromArchiveAsync()));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Mi("Свойства", 0xE946, (_, _) => _ = ShowPropertiesAsync(item)));
            return;
        }

        if (_view == View.Favorites)
        {
            menu.Items.Add(Mi("Убрать из избранного", 0xE8D9, (_, _) => RemoveFavorite(item)));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Mi("Свойства", 0xE946, (_, _) => _ = ShowPropertiesAsync(item)));
            return;
        }

        // Drive / Folder view
        if (item.Kind == ItemKind.Archive)
            menu.Items.Add(Mi("Извлечь", 0xE8DE, (_, _) => OnExtract(this, null!)));
        if (item.Kind is ItemKind.File or ItemKind.Archive)
            menu.Items.Add(Mi("Просмотр", 0xE890, (_, _) => LaunchPath(item.FullPath)));

        if (item.Kind != ItemKind.Drive)
        {
            menu.Items.Add(Mi("Добавить в архив", 0xE710, (_, _) => OnCompress(this, null!)));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Mi("Вырезать", 0xE8C6, (_, _) => _ = ActionClipboardAsync(cut: true)));
            menu.Items.Add(Mi("Копировать", 0xE8C8, (_, _) => _ = ActionClipboardAsync(cut: false)));
            menu.Items.Add(Mi("Переименовать", 0xE8AC, (_, _) => _ = ActionRenameAsync(), enabled: selCount == 1));
            menu.Items.Add(Mi("Удалить", 0xE74D, (_, _) => _ = ActionDeleteAsync()));
            menu.Items.Add(new MenuFlyoutSeparator());
        }
        menu.Items.Add(Mi("В избранное", 0xE734, (_, _) => AddSelectedToFavorites()));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(Mi("Свойства", 0xE946, (_, _) => _ = ShowPropertiesAsync(item)));
    }

    private void BuildBackgroundMenu(MenuFlyout menu)
    {
        if (_view == View.Folder && _currentDir is not null)
        {
            menu.Items.Add(Mi("Создать папку", 0xE8F4, (_, _) => _ = ActionNewFolderAsync()));
            menu.Items.Add(Mi("Создать файл", 0xE7C3, (_, _) => _ = ActionNewFileAsync()));
            menu.Items.Add(Mi("Вставить", 0xE77F, (_, _) => _ = ActionPasteAsync(), enabled: _clip.Count > 0 || ClipboardHasFiles()));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Mi("Обновить", 0xE72C, (_, _) => RefreshCurrent()));
            menu.Items.Add(Mi("Папку в избранное", 0xE734, (_, _) => AddFavoritePath(_currentDir!)));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(Mi("Свойства", 0xE946, (_, _) => _ = ShowPropertiesAsync(
                new FileItem { Name = new DirectoryInfo(_currentDir).Name, FullPath = _currentDir, Kind = ItemKind.Folder })));
        }
        else
        {
            menu.Items.Add(Mi("Обновить", 0xE72C, (_, _) => RefreshCurrent()));
            if (_view == View.Archive && _openArchive is not null)
                menu.Items.Add(Mi("Свойства архива", 0xE946, (_, _) => _ = ShowPropertiesAsync(
                    new FileItem { Name = Path.GetFileName(_openArchive), FullPath = _openArchive, Kind = ItemKind.Archive })));
        }
    }

    private static MenuFlyoutItem Mi(string text, int glyph, RoutedEventHandler onClick, bool enabled = true)
    {
        var mi = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = char.ConvertFromUtf32(glyph) }, IsEnabled = enabled };
        mi.Click += onClick;
        return mi;
    }

    // ---------- Extract / Compress ----------

    private async void OnExtract(object sender, RoutedEventArgs e)
    {
        try
        {
            string archive;
            IEnumerable<string>? only = null;

            if (_view == View.Archive && _openArchive is not null)
            {
                archive = _openArchive;
                var sel = FileList.SelectedItems.OfType<FileItem>().Where(i => i.Kind != ItemKind.UpDir).ToList();
                if (sel.Count > 0)
                    only = sel.Select(i => i.Kind == ItemKind.Folder ? i.FullPath + "\\*" : i.FullPath).ToList();
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

    private async void OnCompress(object sender, RoutedEventArgs e)
    {
        if (_view != View.Folder || _currentDir is null) return;
        var sources = FileList.SelectedItems.OfType<FileItem>()
            .Where(i => i.Kind != ItemKind.UpDir).Select(i => i.FullPath).ToList();
        if (sources.Count == 0) { await ShowError("Нечего сжимать", "Выберите файлы или папки."); return; }

        var defaultName = sources.Count == 1
            ? Path.GetFileNameWithoutExtension(sources[0]) is var n && n.Length > 0 ? n : Path.GetFileName(sources[0])
            : new DirectoryInfo(_currentDir).Name;

        var opts = await AskCompressOptionsAsync(defaultName);
        if (opts is null) return;

        var (name, format, level, password) = opts.Value;
        var archivePath = Path.Combine(_currentDir, $"{name}.{format}");

        SetBusy(true);
        try
        {
            await _engine.CreateAsync(archivePath, sources, format, level, password);
            _settings.DefaultFormat = format; _settings.DefaultLevel = level; _settings.Save();
            SetBusy(false);
            NavigateToFolder(_currentDir, pushHistory: false);
            await ShowInfo("Готово", $"Создан архив:\n{archivePath}");
        }
        catch (Exception ex)
        {
            SetBusy(false);
            await ShowError("Ошибка сжатия", ex.Message);
        }
    }

    private async Task<(string name, string format, int level, string? password)?> AskCompressOptionsAsync(string defaultName)
    {
        var nameBox = new TextBox { Text = defaultName, Header = "Имя архива" };
        var formatBox = new ComboBox { Header = "Формат", HorizontalAlignment = HorizontalAlignment.Stretch };
        formatBox.Items.Add("7z"); formatBox.Items.Add("zip");
        formatBox.SelectedIndex = _settings.DefaultFormat == "zip" ? 1 : 0;
        var levels = new (string label, int value)[]
        {
            ("Без сжатия", 0), ("Быстрый", 1), ("Обычный", 5), ("Максимальный", 7), ("Ультра", 9)
        };
        var levelBox = new ComboBox { Header = "Уровень сжатия", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var l in levels) levelBox.Items.Add(l.label);
        levelBox.SelectedIndex = Math.Max(0, Array.FindIndex(levels, l => l.value == _settings.DefaultLevel));
        if (levelBox.SelectedIndex < 0) levelBox.SelectedIndex = 2;
        var passBox = new PasswordBox { Header = "Пароль (необязательно)", PlaceholderText = "оставьте пустым — без пароля" };

        var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
        panel.Children.Add(nameBox); panel.Children.Add(formatBox);
        panel.Children.Add(levelBox); panel.Children.Add(passBox);

        var dlg = new ContentDialog
        {
            Title = "Создать архив", Content = panel,
            PrimaryButtonText = "Создать", CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = RootGrid.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return null;

        var name = string.IsNullOrWhiteSpace(nameBox.Text) ? defaultName : nameBox.Text.Trim();
        var format = (string)formatBox.SelectedItem;
        var level = levels[levelBox.SelectedIndex].value;
        var password = string.IsNullOrEmpty(passBox.Password) ? null : passBox.Password;
        return (name, format, level, password);
    }

    // ---------- File operations ----------

    private async Task ActionRenameAsync()
    {
        if (FileList.SelectedItem is not FileItem item || item.Kind == ItemKind.UpDir) return;

        var box = new TextBox { Text = item.Name, SelectionStart = 0, SelectionLength = item.Name.Length };
        var dlg = new ContentDialog
        {
            Title = "Переименовать", Content = box,
            PrimaryButtonText = "OK", CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = RootGrid.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var newName = box.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == item.Name) return;

        try
        {
            if (_view == View.Archive && _openArchive is not null)
            {
                var newInternal = _archiveSubPath + newName;
                SetBusy(true);
                await _engine.RenameEntryAsync(_openArchive, item.FullPath, newInternal);
                await ReloadArchiveAsync();
                SetBusy(false);
            }
            else
            {
                var dir = Path.GetDirectoryName(item.FullPath)!;
                var target = Path.Combine(dir, newName);
                if (Directory.Exists(item.FullPath)) Directory.Move(item.FullPath, target);
                else File.Move(item.FullPath, target);
                if (_currentDir is not null) NavigateToFolder(_currentDir, pushHistory: false);
            }
        }
        catch (Exception ex) { SetBusy(false); await ShowError("Не удалось переименовать", ex.Message); }
    }

    private async Task ActionDeleteAsync()
    {
        var sel = FileList.SelectedItems.OfType<FileItem>()
            .Where(i => i.Kind is not ItemKind.UpDir and not ItemKind.Drive and not ItemKind.Favorites)
            .Select(i => i.FullPath).ToList();
        if (sel.Count == 0) return;

        if (_settings.ConfirmDelete)
        {
            var text = sel.Count == 1 ? $"Переместить в корзину:\n{sel[0]}?" : $"Переместить в корзину: {sel.Count} элемент(ов)?";
            if (!await Confirm("Удаление", text)) return;
        }
        try
        {
            await Task.Run(() => ShellOps.RecycleDelete(sel));
            if (_currentDir is not null) NavigateToFolder(_currentDir, pushHistory: false);
        }
        catch (Exception ex) { await ShowError("Не удалось удалить", ex.Message); }
    }

    private async Task ActionDeleteFromArchiveAsync()
    {
        if (_openArchive is null) return;
        var sel = FileList.SelectedItems.OfType<FileItem>().Where(i => i.Kind != ItemKind.UpDir)
            .Select(i => i.Kind == ItemKind.Folder ? i.FullPath + "\\*" : i.FullPath).ToList();
        if (sel.Count == 0) return;
        if (!await Confirm("Удаление из архива", $"Удалить {sel.Count} элемент(ов) из архива?")) return;
        try
        {
            SetBusy(true);
            await _engine.DeleteEntriesAsync(_openArchive, sel);
            await ReloadArchiveAsync();
            SetBusy(false);
        }
        catch (Exception ex) { SetBusy(false); await ShowError("Не удалось удалить из архива", ex.Message); }
    }

    private async Task ActionNewFolderAsync()
    {
        if (_currentDir is null) return;
        var box = new TextBox { Text = "Новая папка" };
        var dlg = new ContentDialog { Title = "Создать папку", Content = box, PrimaryButtonText = "Создать", CloseButtonText = "Отмена", DefaultButton = ContentDialogButton.Primary, XamlRoot = RootGrid.XamlRoot };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        try { Directory.CreateDirectory(Path.Combine(_currentDir, box.Text.Trim())); NavigateToFolder(_currentDir, false); }
        catch (Exception ex) { await ShowError("Не удалось создать папку", ex.Message); }
    }

    private async Task ActionNewFileAsync()
    {
        if (_currentDir is null) return;
        var box = new TextBox { Text = "Новый файл.txt" };
        var dlg = new ContentDialog { Title = "Создать файл", Content = box, PrimaryButtonText = "Создать", CloseButtonText = "Отмена", DefaultButton = ContentDialogButton.Primary, XamlRoot = RootGrid.XamlRoot };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            var path = Path.Combine(_currentDir, box.Text.Trim());
            if (!File.Exists(path)) File.Create(path).Dispose();
            NavigateToFolder(_currentDir, false);
        }
        catch (Exception ex) { await ShowError("Не удалось создать файл", ex.Message); }
    }

    // ---------- Clipboard ----------

    private async Task ActionClipboardAsync(bool cut)
    {
        var sel = FileList.SelectedItems.OfType<FileItem>()
            .Where(i => i.Kind is not ItemKind.UpDir and not ItemKind.Drive and not ItemKind.Favorites)
            .Select(i => i.FullPath).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (sel.Count == 0) return;
        _clip = sel; _clipCut = cut;

        try
        {
            var items = new List<IStorageItem>();
            foreach (var p in sel)
            {
                if (Directory.Exists(p)) items.Add(await StorageFolder.GetFolderFromPathAsync(p));
                else items.Add(await StorageFile.GetFileFromPathAsync(p));
            }
            var dp = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
            dp.SetStorageItems(items);
            Clipboard.SetContent(dp);
        }
        catch { /* internal _clip still works even if the system clipboard refuses */ }
        SetStatus($"{(cut ? "Вырезано" : "Скопировано")}: {sel.Count}");
    }

    private static bool ClipboardHasFiles()
    {
        try { return Clipboard.GetContent().Contains(StandardDataFormats.StorageItems); }
        catch { return false; }
    }

    private async Task ActionPasteAsync()
    {
        if (_view != View.Folder || _currentDir is null) return;
        try
        {
            List<string> paths;
            bool move;
            if (_clip.Count > 0) { paths = _clip; move = _clipCut; }
            else
            {
                var view = Clipboard.GetContent();
                if (!view.Contains(StandardDataFormats.StorageItems)) return;
                var items = await view.GetStorageItemsAsync();
                paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
                move = false;
            }
            if (paths.Count == 0) return;

            var dest = _currentDir;
            SetBusy(true);
            await Task.Run(() =>
            {
                foreach (var p in paths)
                {
                    if (move) ShellOps.MoveInto(p, dest);
                    else ShellOps.CopyInto(p, dest);
                }
            });
            SetBusy(false);
            if (_clipCut) { _clip = new(); _clipCut = false; }
            NavigateToFolder(dest, pushHistory: false);
        }
        catch (Exception ex) { SetBusy(false); await ShowError("Не удалось вставить", ex.Message); }
    }

    // ---------- Favorites ----------

    private void AddSelectedToFavorites()
    {
        foreach (var i in FileList.SelectedItems.OfType<FileItem>()
                     .Where(i => i.Kind is ItemKind.Folder or ItemKind.File or ItemKind.Archive))
            AddFavoritePath(i.FullPath);
    }

    private void AddFavoritePath(string path)
    {
        if (_settings.Favorites.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) return;
        ItemKind kind = Directory.Exists(path) ? ItemKind.Folder : IsArchive(path) ? ItemKind.Archive : ItemKind.File;
        _settings.Favorites.Add(new FavoriteEntry { Name = Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } n ? n : path, Path = path, Kind = kind });
        _settings.Save();
        SetStatus("Добавлено в избранное");
        if (_view == View.Favorites) ShowFavorites(pushHistory: false);
    }

    private void RemoveFavorite(FileItem item)
    {
        _settings.Favorites.RemoveAll(f => string.Equals(f.Path, item.FullPath, StringComparison.OrdinalIgnoreCase));
        _settings.Save();
        ShowFavorites(pushHistory: false);
    }

    // ---------- Drag & drop ----------

    private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var sel = e.Items.OfType<FileItem>()
            .Where(i => i.Kind is not ItemKind.UpDir and not ItemKind.Drive and not ItemKind.Favorites).ToList();
        if (sel.Count == 0 || _view is View.Drives) { e.Cancel = true; return; }

        e.Data.RequestedOperation = DataPackageOperation.Copy;
        e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var items = await BuildDragStorageItemsAsync(sel);
                if (items.Count > 0) request.SetData(items);
            }
            catch { /* nothing to hand over */ }
            finally { deferral.Complete(); }
        });
    }

    private async Task<List<IStorageItem>> BuildDragStorageItemsAsync(List<FileItem> sel)
    {
        var list = new List<IStorageItem>();
        if (_view == View.Archive && _openArchive is not null)
        {
            var temp = Path.Combine(Path.GetTempPath(), "711zip_drag", Guid.NewGuid().ToString("N"));
            var include = sel.Select(i => i.Kind == ItemKind.Folder ? i.FullPath + "\\*" : i.FullPath).ToList();
            await _engine.ExtractAsync(_openArchive, temp, include);
            foreach (var i in sel)
            {
                var outPath = Path.Combine(temp, i.FullPath);
                if (i.Kind == ItemKind.Folder && Directory.Exists(outPath)) list.Add(await StorageFolder.GetFolderFromPathAsync(outPath));
                else if (File.Exists(outPath)) list.Add(await StorageFile.GetFileFromPathAsync(outPath));
            }
        }
        else
        {
            foreach (var i in sel)
            {
                if (Directory.Exists(i.FullPath)) list.Add(await StorageFolder.GetFolderFromPathAsync(i.FullPath));
                else if (File.Exists(i.FullPath)) list.Add(await StorageFile.GetFileFromPathAsync(i.FullPath));
            }
        }
        return list;
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        bool canDrop = e.DataView.Contains(StandardDataFormats.StorageItems)
                       && _view is View.Folder or View.Archive or View.Favorites;
        if (canDrop)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.Caption = _view switch
            {
                View.Archive => "Добавить в архив",
                View.Favorites => "В избранное",
                _ => "Копировать сюда",
            };
        }
        else e.AcceptedOperation = DataPackageOperation.None;
    }

    private async void OnListDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var def = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (paths.Count == 0) return;

            if (_view == View.Archive && _openArchive is not null)
            {
                SetBusy(true); await _engine.AddAsync(_openArchive, paths); await ReloadArchiveAsync(); SetBusy(false);
            }
            else if (_view == View.Folder && _currentDir is not null)
            {
                var dest = _currentDir;
                SetBusy(true);
                await Task.Run(() => { foreach (var p in paths) ShellOps.CopyInto(p, dest); });
                SetBusy(false);
                NavigateToFolder(dest, pushHistory: false);
            }
            else if (_view == View.Favorites)
            {
                foreach (var p in paths) AddFavoritePath(p);
            }
        }
        catch (Exception ex) { SetBusy(false); await ShowError("Не удалось принять перетаскивание", ex.Message); }
        finally { def.Complete(); }
    }

    // ---------- View archive entry ----------

    private async Task ViewArchiveEntryAsync(FileItem item)
    {
        if (_openArchive is null) return;
        try
        {
            var temp = Path.Combine(Path.GetTempPath(), "711zip_view", Guid.NewGuid().ToString("N"));
            SetBusy(true);
            await _engine.ExtractAsync(_openArchive, temp, new[] { item.FullPath });
            SetBusy(false);
            var outPath = Path.Combine(temp, item.FullPath);
            if (File.Exists(outPath)) LaunchPath(outPath);
        }
        catch (Exception ex) { SetBusy(false); await ShowError("Не удалось открыть файл", ex.Message); }
    }

    // ---------- Properties ----------

    private async Task ShowPropertiesAsync(FileItem item)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Имя: {item.Name}");
        sb.AppendLine($"Путь: {item.FullPath}");
        sb.AppendLine($"Тип: {KindLabel(item.Kind)}");

        try
        {
            if (_view == View.Archive)
            {
                sb.AppendLine($"Размер: {FileItem.FormatSize(item.Size)}");
                if (item.Modified is not null) sb.AppendLine($"Изменён: {item.ModifiedText}");
            }
            else if (item.Kind == ItemKind.Archive && File.Exists(item.FullPath))
            {
                var fi = new FileInfo(item.FullPath);
                sb.AppendLine($"Размер архива: {FileItem.FormatSize(fi.Length)}");
                sb.AppendLine($"Изменён: {fi.LastWriteTime:yyyy-MM-dd HH:mm}");
                var entries = await _engine.ListAsync(item.FullPath);
                long total = entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
                sb.AppendLine($"Файлов внутри: {entries.Count(e => !e.IsDirectory)}");
                sb.AppendLine($"Распакованный размер: {FileItem.FormatSize(total)}");
                if (fi.Length > 0) sb.AppendLine($"Степень сжатия: {(int)(100 - fi.Length * 100.0 / Math.Max(1, total))}%");
            }
            else if (Directory.Exists(item.FullPath))
            {
                var di = new DirectoryInfo(item.FullPath);
                sb.AppendLine($"Изменён: {di.LastWriteTime:yyyy-MM-dd HH:mm}");
                try
                {
                    sb.AppendLine($"Вложенных папок: {di.EnumerateDirectories().Count()}");
                    sb.AppendLine($"Файлов: {di.EnumerateFiles().Count()}");
                }
                catch { }
            }
            else if (File.Exists(item.FullPath))
            {
                var fi = new FileInfo(item.FullPath);
                sb.AppendLine($"Размер: {FileItem.FormatSize(fi.Length)}");
                sb.AppendLine($"Изменён: {fi.LastWriteTime:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"Создан: {fi.CreationTime:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"Атрибуты: {fi.Attributes}");
            }
        }
        catch (Exception ex) { sb.AppendLine($"(Не удалось прочитать детали: {ex.Message})"); }

        await ShowDialog("Свойства", sb.ToString());
    }

    private static string KindLabel(ItemKind k) => k switch
    {
        ItemKind.Drive => "Диск", ItemKind.Folder => "Папка", ItemKind.Archive => "Архив",
        ItemKind.File => "Файл", _ => "Элемент",
    };

    // ---------- Settings / About ----------

    private async void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var themeBox = new ComboBox { Header = "Тема", HorizontalAlignment = HorizontalAlignment.Stretch };
        themeBox.Items.Add("Системная"); themeBox.Items.Add("Светлая"); themeBox.Items.Add("Тёмная");
        themeBox.SelectedIndex = _settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };

        var formatBox = new ComboBox { Header = "Формат по умолчанию", HorizontalAlignment = HorizontalAlignment.Stretch };
        formatBox.Items.Add("7z"); formatBox.Items.Add("zip");
        formatBox.SelectedIndex = _settings.DefaultFormat == "zip" ? 1 : 0;

        var levels = new (string label, int value)[] { ("Без сжатия", 0), ("Быстрый", 1), ("Обычный", 5), ("Максимальный", 7), ("Ультра", 9) };
        var levelBox = new ComboBox { Header = "Уровень сжатия по умолчанию", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var l in levels) levelBox.Items.Add(l.label);
        levelBox.SelectedIndex = Math.Max(0, Array.FindIndex(levels, l => l.value == _settings.DefaultLevel));

        var hiddenSwitch = new ToggleSwitch { Header = "Показывать скрытые и системные файлы", IsOn = _settings.ShowHidden };
        var confirmSwitch = new ToggleSwitch { Header = "Подтверждать удаление", IsOn = _settings.ConfirmDelete };

        var panel = new StackPanel { Spacing = 12, MinWidth = 340 };
        panel.Children.Add(themeBox); panel.Children.Add(formatBox); panel.Children.Add(levelBox);
        panel.Children.Add(hiddenSwitch); panel.Children.Add(confirmSwitch);

        var dlg = new ContentDialog
        {
            Title = "Настройки", Content = new ScrollViewer { Content = panel },
            PrimaryButtonText = "Сохранить", CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary, XamlRoot = RootGrid.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        _settings.Theme = themeBox.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "Default" };
        _settings.DefaultFormat = (string)formatBox.SelectedItem;
        _settings.DefaultLevel = levels[levelBox.SelectedIndex].value;
        _settings.ShowHidden = hiddenSwitch.IsOn;
        _settings.ConfirmDelete = confirmSwitch.IsOn;
        _settings.Save();

        ApplyTheme(ParseTheme(_settings.Theme));
        RefreshCurrent();
    }

    private async void OnAbout(object sender, RoutedEventArgs e)
    {
        var ver = typeof(App).Assembly.GetName().Version;
        var panel = new StackPanel { Spacing = 8, MinWidth = 320 };
        panel.Children.Add(new TextBlock { Text = "711zip", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"Версия {ver?.ToString(3)}", Opacity = 0.8 });
        panel.Children.Add(new TextBlock { Text = "Интерфейс в стиле Windows 11 для 7-Zip.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Работает на движке 7-Zip (7z.exe).", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        panel.Children.Add(new HyperlinkButton { Content = "github.com/metawka/711zip", NavigateUri = new Uri("https://github.com/metawka/711zip") });
        panel.Children.Add(new TextBlock { Text = "© 2026 metawka · Лицензия LGPL-2.1", Opacity = 0.7, FontSize = 12 });

        var dlg = new ContentDialog { Title = "О программе", Content = panel, CloseButtonText = "Закрыть", XamlRoot = RootGrid.XamlRoot };
        await dlg.ShowAsync();
    }

    // ---------- Helpers ----------

    private static bool IsArchive(string name) => ArchiveExts.Contains(Path.GetExtension(name));

    private static void LaunchPath(string path)
    {
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch { }
    }

    private void PushCurrent()
    {
        var view = _view; var dir = _currentDir; var arc = _openArchive; var sub = _archiveSubPath;
        _back.Push(() =>
        {
            switch (view)
            {
                case View.Archive when arc is not null: _ = RestoreArchiveAsync(arc, sub); break;
                case View.Folder when dir is not null: NavigateToFolder(dir, pushHistory: false); break;
                case View.Favorites: ShowFavorites(pushHistory: false); break;
                default: ShowDrives(pushHistory: false); break;
            }
        });
    }

    private void UpdateCommandState()
    {
        UpButton.IsEnabled = _view != View.Drives;
        BackButton.IsEnabled = _back.Count > 0;

        bool canExtract = _view == View.Archive
            || FileList.SelectedItems.OfType<FileItem>().Any(i => i.Kind == ItemKind.Archive);
        ExtractButton.IsEnabled = canExtract && _engine.EngineAvailable;

        bool canCompress = _view == View.Folder
            && FileList.SelectedItems.OfType<FileItem>().Any(i => i.Kind != ItemKind.UpDir);
        CompressButton.IsEnabled = canCompress && _engine.EngineAvailable;
    }

    private void SetBusy(bool busy) => Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    private void SetStatus(string text) => StatusText.Text = text;

    private Task ShowInfo(string title, string message) => ShowDialog(title, message);
    private Task ShowError(string title, string message) => ShowDialog(title, message);

    private async Task<bool> Confirm(string title, string message)
    {
        var dlg = new ContentDialog
        {
            Title = title, Content = message,
            PrimaryButtonText = "Да", CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close, XamlRoot = RootGrid.XamlRoot,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowDialog(string title, string message)
    {
        var dlg = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } },
            CloseButtonText = "OK", XamlRoot = RootGrid.XamlRoot,
        };
        await dlg.ShowAsync();
    }
}
