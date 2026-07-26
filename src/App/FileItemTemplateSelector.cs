using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Zip711.Models;

namespace Zip711;

/// <summary>
/// Picks the row template for the current view mode, and always uses the group
/// header template for <see cref="ItemKind.Header"/> rows. <see cref="Mode"/> is
/// switched in code when the user changes the view; the list is rebuilt so the
/// new template takes effect.
/// </summary>
public sealed partial class FileItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Details { get; set; }
    public DataTemplate? List { get; set; }
    public DataTemplate? LargeIcons { get; set; }
    public DataTemplate? SmallIcons { get; set; }
    public DataTemplate? Header { get; set; }

    public string Mode { get; set; } = "Details";

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (item is FileItem { Kind: ItemKind.Header }) return Header!;
        return Mode switch
        {
            "List" => List!,
            "LargeIcons" => LargeIcons!,
            "SmallIcons" => SmallIcons!,
            _ => Details!,
        };
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
