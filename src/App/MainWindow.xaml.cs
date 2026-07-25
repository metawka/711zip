using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Zip711;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Win11 look: Mica backdrop + content extended into the title bar.
        this.SystemBackdrop = new MicaBackdrop();
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);
        this.Title = "711zip";

        ApplyTheme(ElementTheme.Default);
        ReportEngine();
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        var next = RootGrid.ActualTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;
        ApplyTheme(next);
    }

    private void ApplyTheme(ElementTheme theme)
    {
        RootGrid.RequestedTheme = theme;
        // Sun (E706) while dark -> tap for light; Moon (E708) while light -> tap for dark.
        int glyph = RootGrid.ActualTheme == ElementTheme.Dark ? 0xE706 : 0xE708;
        ThemeGlyph.Glyph = char.ConvertFromUtf32(glyph);
    }

    private void ReportEngine()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "7z.dll");
        EngineStatus.Text = File.Exists(dll)
            ? $"Движок 7-Zip подключён · {new FileInfo(dll).Length / 1024} КБ"
            : "Движок 7z.dll не найден";
    }
}
