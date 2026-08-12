using CommunityToolkit.Mvvm.Input;
using NotifyGen;

namespace NotifyGen.HybridSample;

/// <summary>
/// Recommended stack: NotifyGen for INPC properties, CommunityToolkit for commands.
/// </summary>
[Notify]
public partial class EditorViewModel
{
    [NotifyProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = "";

    [NotifyProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _body = "";

    [NotifyProperty]
    private string _status = "Edit a title and body, then Save.";

    // Unmarked underscore fields stay private in opt-in mode.
    private readonly object _log = new();

    [NotifyComputed(nameof(Title), nameof(Body))]
    public bool CanSave => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Body);

    public IRelayCommand SaveCommand { get; }

    public EditorViewModel()
    {
        SaveCommand = new RelayCommand(Save, () => CanSave);
    }

    private void Save()
    {
        _ = _log;
        Status = $"Saved \"{Title}\" ({Body.Length} chars) at {DateTime.Now:HH:mm:ss}";
    }
}
