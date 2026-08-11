using System.Collections.ObjectModel;

namespace NotifyGen.WpfSample;

/// <summary>
/// Showcases Cycles 2–4: host INPC reuse, child/collection tracking, typed hooks, and suppressable bulk load.
/// </summary>
[Notify]
[NotifySuppressable]
public partial class MainViewModel : ViewModelBase
{
    [NotifyAlso(nameof(FullName))]
    private string _firstName = "John";

    [NotifyAlso(nameof(FullName))]
    private string _lastName = "Doe";

    private int _age = 30;

    [NotifyAlso(nameof(CitySummary), NotifyOnSubPropertyChanged = true)]
    private Address? _address = new();

    [NotifyAlso(nameof(TagCount), NotifyOnCollectionChanged = true)]
    private ObservableCollection<string> _tags = new() { "wpf", "notifygen" };

    private string _statusLog = "Property changes will appear here...\n";

    public string FullName => $"{FirstName} {LastName}";

    public string CitySummary => Address is null ? "(no address)" : $"{Address.City}, {Address.PostalCode}";

    public int TagCount => Tags.Count;

    public void BulkLoadDemoData()
    {
        using (SuppressNotifications())
        {
            FirstName = "Ada";
            LastName = "Lovelace";
            Age = 36;
            Tags = new ObservableCollection<string> { "math", "computing", "poetry" };
            Address = new Address { City = "London", PostalCode = "SW1A 1AA" };
        }

        StatusLog += "[bulk] SuppressNotifications released — dependents refresh once.\n";
    }

    partial void OnFirstNameChanged(string oldValue, string newValue)
    {
        LogChange(nameof(FirstName), $"'{oldValue}' → '{newValue}'");
    }

    partial void OnLastNameChanged(string oldValue, string newValue)
    {
        LogChange(nameof(LastName), $"'{oldValue}' → '{newValue}'");
    }

    partial void OnAgeChanged(int oldValue, int newValue)
    {
        LogChange(nameof(Age), $"{oldValue} → {newValue}");
    }

    partial void OnAddressChanged()
    {
        LogChange(nameof(Address), CitySummary);
    }

    partial void OnTagsChanged()
    {
        LogChange(nameof(Tags), $"count={TagCount}");
    }

    private void LogChange(string propertyName, string detail)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        StatusLog += $"[{timestamp}] {propertyName} = {detail}\n";
    }
}
