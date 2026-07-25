using System;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Zip711;

/// <summary>
/// First-run guide. A few flip pages with the useful features and shortcuts.
/// The "Пропустить" button unlocks after 3 seconds on first launch.
/// </summary>
internal static class Onboarding
{
    public static async Task ShowAsync(XamlRoot root, bool skippable = true)
    {
        var flip = new FlipView { Width = 560, Height = 360 };
        flip.Items.Add(Page(0xE7C1, "Навигация",
            "Назад и Вперёд слева вверху, или боковые кнопки мыши.",
            "Строка пути понимает ввод адреса и подсказывает папки.",
            "Двойной клик открывает папку, «..» поднимает на уровень выше."));
        flip.Items.Add(Page(0xF012, "Архивы",
            "Открой архив двойным кликом и ходи по нему как по папке.",
            "Кнопка «Извлечь» распакует выбранное или весь архив.",
            "«Добавить» создаёт архив: формат, уровень, пароль и папка назначения."));
        flip.Items.Add(Page(0xE890, "Предпросмотр",
            "Пробел открывает предпросмотр файла, Esc закрывает.",
            "Картинки, видео и текст показываются прямо в окне.",
            "Остальное открывается в стандартном приложении."));
        flip.Items.Add(Page(0xE734, "Избранное и поиск",
            "Звёздочка вверху открывает избранное, рядом виден полный путь.",
            "Правый клик по файлу или папке, «В избранное».",
            "Ctrl+F включает поиск по текущей папке."));
        flip.Items.Add(Page(0xE765, "Горячие клавиши",
            "Ctrl+F поиск, Ctrl+R или F5 обновить, Ctrl+L строка пути.",
            "F2 переименовать, Del в корзину, Ctrl+C, Ctrl+X, Ctrl+V.",
            "Alt+← назад, Alt+→ вперёд, Пробел предпросмотр."));

        var dlg = new ContentDialog
        {
            Title = "Знакомство с 711-zip",
            Content = flip,
            PrimaryButtonText = "Далее",
            SecondaryButtonText = skippable ? "Пропустить (3)" : "Закрыть",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        dlg.IsSecondaryButtonEnabled = !skippable;

        dlg.PrimaryButtonClick += (_, args) =>
        {
            if (flip.SelectedIndex < flip.Items.Count - 1)
            {
                args.Cancel = true;
                flip.SelectedIndex++;
            }
        };
        flip.SelectionChanged += (_, _) =>
            dlg.PrimaryButtonText = flip.SelectedIndex == flip.Items.Count - 1 ? "Готово" : "Далее";

        Microsoft.UI.Dispatching.DispatcherQueueTimer? timer = null;
        if (skippable)
        {
            int left = 3;
            timer = dlg.DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.IsRepeating = true;
            timer.Tick += (t, _) =>
            {
                left--;
                if (left <= 0)
                {
                    dlg.SecondaryButtonText = "Пропустить";
                    dlg.IsSecondaryButtonEnabled = true;
                    t.Stop();
                }
                else dlg.SecondaryButtonText = $"Пропустить ({left})";
            };
            timer.Start();
        }

        try { await dlg.ShowAsync(); }
        finally { timer?.Stop(); }
    }

    private static UIElement Page(int glyph, string title, params string[] bullets)
    {
        var panel = new StackPanel { Spacing = 14, Padding = new Thickness(24, 12, 24, 12) };

        var badge = new Border
        {
            Width = 72, Height = 72, CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            Child = new FontIcon
            {
                Glyph = char.ConvertFromUtf32(glyph), FontSize = 34,
                Foreground = (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"],
            },
        };
        panel.Children.Add(badge);
        panel.Children.Add(new TextBlock
        {
            Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var list = new StackPanel { Spacing = 8 };
        foreach (var b in bullets)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new FontIcon
            {
                Glyph = char.ConvertFromUtf32(0xE73E), FontSize = 13, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            });
            row.Children.Add(new TextBlock { Text = b, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
            list.Children.Add(row);
        }
        panel.Children.Add(list);
        return panel;
    }
}
