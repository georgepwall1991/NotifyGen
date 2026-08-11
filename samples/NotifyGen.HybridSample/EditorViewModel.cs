using CommunityToolkit.Mvvm.Input;

namespace NotifyGen.HybridSample;

/// <summary>
/// Recommended stack: NotifyGen for INPC properties, CommunityToolkit for commands.
/// </summary>
[Notify]
public partial class EditorViewModel
{
    [NotifyAlso(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = "";

    [NotifyAlso(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _body = "";

    private string _status = "Edit a title and body, then Save.";

    public bool CanSave =>
        !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Body);

    public IRelayCommand SaveCommand { get; }

    public EditorViewModel()
    {
        SaveCommand = new RelayCommand(Save, () => CanSave);
    }

    private void Save()
    {
        Status = $"Saved \"{Title}\" ({Body.Length} chars) at {DateTime.Now:HH:mm:ss}";
    }
}
