using Microsoft.UI.Xaml;
using Zip711.Models;

namespace Zip711;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    /// <summary>Shared, resizable file-list column widths (the "Cols" app resource).</summary>
    public static ColumnWidths? Cols => (Current?.Resources.TryGetValue("Cols", out var c) ?? false) ? c as ColumnWidths : null;

    public App()
    {
        this.InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Explorer context-menu verbs: extract without opening the UI.
        var cli = System.Environment.GetCommandLineArgs();
        if (cli.Length >= 3 && (cli[1] == "--extract" || cli[1] == "--extract-here"))
        {
            await RunHeadlessExtractAsync(cli[1], cli[2]);
            Exit();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private static async System.Threading.Tasks.Task RunHeadlessExtractAsync(string verb, string archive)
    {
        try
        {
            var engine = new Services.SevenZipRunner();
            var dir = System.IO.Path.GetDirectoryName(archive) ?? System.Environment.CurrentDirectory;
            string dest = verb == "--extract-here"
                ? dir
                : System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(archive));
            await engine.ExtractAsync(archive, dest, null);
        }
        catch { /* headless: nothing to surface to */ }
    }
}
