using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace Zip711.Models;

/// <summary>
/// Shared column widths for the file list. The header row and every item row
/// bind to the same instance, so they always line up and can be resized together.
/// </summary>
public sealed class ColumnWidths : INotifyPropertyChanged
{
    private double _size = 110;
    private double _modified = 170;

    public double Size
    {
        get => _size;
        set { _size = System.Math.Clamp(value, 60, 400); OnChanged(nameof(Size)); OnChanged(nameof(SizeLen)); }
    }

    public double Modified
    {
        get => _modified;
        set { _modified = System.Math.Clamp(value, 80, 500); OnChanged(nameof(Modified)); OnChanged(nameof(ModifiedLen)); }
    }

    public GridLength SizeLen => new(_size);
    public GridLength ModifiedLen => new(_modified);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
