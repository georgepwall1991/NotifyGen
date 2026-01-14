using System.Text;

namespace NotifyGen.WpfSample;

/// <summary>
/// Sample ViewModel demonstrating NotifyGen features:
/// - [Notify] for automatic INotifyPropertyChanged
/// - [NotifyAlso] for dependent/computed properties
/// - Partial hooks (OnXxxChanged) for custom logic
/// </summary>
[Notify]
public partial class MainViewModel
{
    // Using [NotifyAlso] to notify FullName when FirstName changes
    [NotifyAlso("FullName")]
    private string _firstName = "John";

    // Using [NotifyAlso] to notify FullName when LastName changes
    [NotifyAlso("FullName")]
    private string _lastName = "Doe";

    // Simple property - just needs the underscore prefix
    private int _age = 30;

    // This field stores our log - we'll update it via partial hooks
    private string _statusLog = "Property changes will appear here...\n";

    /// <summary>
    /// Computed property that depends on FirstName and LastName.
    /// Thanks to [NotifyAlso], this updates automatically!
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    // Partial hook - called whenever FirstName changes
    partial void OnFirstNameChanged()
    {
        LogChange(nameof(FirstName), FirstName);
    }

    // Partial hook - called whenever LastName changes
    partial void OnLastNameChanged()
    {
        LogChange(nameof(LastName), LastName);
    }

    // Partial hook - called whenever Age changes
    partial void OnAgeChanged()
    {
        LogChange(nameof(Age), Age.ToString());
    }

    private void LogChange(string propertyName, string newValue)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        StatusLog += $"[{timestamp}] {propertyName} = \"{newValue}\"\n";
    }
}
