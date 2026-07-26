using System;
using System.Collections.Generic;
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
        // A plain fixed-size host that swaps the current page. FlipView was replaced
        // because its built-in navigation chrome insets the item content asymmetrically,
        // which shifted every page ~30px to the right of centre no matter how the page
        // itself was aligned. A single-cell Grid centres its child exactly.
        var pages = new List<UIElement>
        {
            Page(0xE7C1, "Навигация",
                "Назад и Вперёд слева вверху, или боковые кнопки мыши.",
                "Строка пути понимает ввод адреса и подсказывает папки.",
                "Двойной клик открывает папку, «..» поднимает на уровень выше."),
            Page(0xF012, "Архивы",
                "Открой архив двойным кликом и ходи по нему как по папке.",
                "Кнопка «Извлечь» распакует выбранное или весь архив.",
                "«Добавить» создаёт архив: формат, уровень, пароль и папка назначения."),
            Page(0xE890, "Предпросмотр",
                "Пробел открывает предпросмотр файла, Esc закрывает.",
                "Картинки, видео и текст показываются прямо в окне.",
                "Остальное открывается в стандартном приложении."),
            Page(0xE734, "Избранное и поиск",
                "Звёздочка вверху открывает избранное, рядом виден полный путь.",
                "Правый клик по файлу или папке, «В избранное».",
                "Ctrl+F включает поиск по текущей папке."),
            Page(0xE765, "Горячие клавиши",
                "Ctrl+F поиск, Ctrl+R или F5 обновить, Ctrl+L строка пути.",
                "F2 переименовать, Del в корзину, Ctrl+C, Ctrl+X, Ctrl+V.",
                "Alt+← назад, Alt+→ вперёд, Пробел предпросмотр."),
        };

        int index = 0;
        var host = new Grid { Width = 560, Height = 360 };
        host.Children.Add(pages[0]);

        var dlg = new ContentDialog
        {
            Title = "Знакомство с 711-zip",
            Content = host,
            PrimaryButtonText = "Далее",
            SecondaryButtonText = skippable ? "Пропустить (3)" : "Закрыть",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };
        dlg.IsSecondaryButtonEnabled = !skippable;

        dlg.PrimaryButtonClick += (_, args) =>
        {
            if (index < pages.Count - 1)
            {
                args.Cancel = true;
                index++;
                host.Children.Clear();
                host.Children.Add(pages[index]);
                dlg.PrimaryButtonText = index == pages.Count - 1 ? "Готово" : "Далее";
            }
        };

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
        // A fixed-width column centered inside a filling Grid. Centering the whole
        // column (rather than letting a HorizontalAlignment.Center StackPanel shrink
        // to its widest bullet row) keeps the badge, title and bullets on one
        // symmetric axis — otherwise the content drifts right by the bullet indent.
        var root = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        var panel = new StackPanel
        {
            Width = 460, Spacing = 14, Padding = new Thickness(24, 12, 24, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

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

        var list = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var b in bullets)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new FontIcon
            {
                Glyph = char.ConvertFromUtf32(0xE73E), FontSize = 13, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            });
            row.Children.Add(new TextBlock { Text = b, TextWrapping = TextWrapping.Wrap, MaxWidth = 386 });
            list.Children.Add(row);
        }
        panel.Children.Add(list);
        root.Children.Add(panel);
        return root;
    }
}
