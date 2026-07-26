using System;
using System.ComponentModel;

namespace Zip711.Models;

/// <summary>
/// Shared, live-updatable sizing for the icon (grid) view modes. The large-icon
/// template and its wrap-grid cells all bind to one instance, so Ctrl+wheel can
/// grow/shrink the icons smoothly by mutating <see cref="IconSize"/>.
/// </summary>
public sealed class ViewState : INotifyPropertyChanged
{
    private double _iconSize = 48;

    public const double MinIcon = 24;
    public const double MaxIcon = 112;

    public double IconSize
    {
        get => _iconSize;
        set
        {
            var v = Math.Clamp(value, MinIcon, MaxIcon);
            if (Math.Abs(v - _iconSize) < 0.01) return;
            _iconSize = v;
            Changed(nameof(IconSize));
            Changed(nameof(TileWidth));
            Changed(nameof(TileHeight));
            Changed(nameof(NameWidth));
        }
    }

    public double TileWidth => _iconSize + 52;
    public double TileHeight => _iconSize + 56;
    public double NameWidth => _iconSize + 44;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
