using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NotifyGen.WpfSample;

/// <summary>
/// Existing INPC host reused by NotifyGen (Cycle 4) — no duplicate event/helper generation.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
