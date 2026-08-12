using System.Collections.ObjectModel;

namespace NotifyGen.AvaloniaSample;

/// <summary>
/// Same Cycle 2–4 patterns as the WPF sample, for cross-platform Avalonia.
/// </summary>
[Notify]
[NotifySuppressable]
public partial class MainViewModel : ViewModelBase
{
    private string _firstName = "Ada";

    private string _lastName = "Lovelace";

    [NotifyAlso(nameof(LocationSummary), NotifyOnSubPropertyChanged = true)]
    private Address? _address = new();

    [NotifyAlso(nameof(ItemCount), NotifyOnCollectionChanged = true)]
    private ObservableCollection<string> _items = new() { "avalonia", "notifygen" };

    private string _status = "Ready";

    [NotifyComputed]
    public string FullName => $"{FirstName} {LastName}";

    public string LocationSummary =>
        Address is null ? "(none)" : $"{Address.City}, {Address.Country}";

    public int ItemCount => Items.Count;

    public void BulkReload()
    {
        using (SuppressNotifications())
        {
            FirstName = "Grace";
            LastName = "Hopper";
            Address = new Address { City = "New York", Country = "USA" };
            Items = new ObservableCollection<string> { "compiler", "cobol", "navy" };
        }

        Status = $"Reloaded — FullName={FullName}, Location={LocationSummary}, Items={ItemCount}";
    }

    partial void OnFirstNameChanged(string oldValue, string newValue) =>
        Status = $"FirstName '{oldValue}' → '{newValue}'";

    partial void OnLastNameChanged(string oldValue, string newValue) =>
        Status = $"LastName '{oldValue}' → '{newValue}'";
}
