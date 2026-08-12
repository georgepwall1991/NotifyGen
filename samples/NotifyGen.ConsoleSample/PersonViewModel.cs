using System.ComponentModel;

namespace NotifyGen.ConsoleSample;

/// <summary>
/// Demonstrates all NotifyGen features in a cross-platform console app.
/// </summary>
[Notify]
public partial class PersonViewModel
{
    // Basic property - just declare the field with underscore prefix.
    // FullName is wired from [NotifyComputed] on the derived property.
    private string _firstName = "";

    private string _lastName = "";

    private string _middleName = "";

    // Simple numeric property
    private int _age;

    // Nullable property
    private string? _email;

    // [NotifyName] - custom property name
    [NotifyName("IsActive")]
    private bool _active;

    // [NotifySetter] - control setter accessibility
    [NotifySetter(AccessLevel.Private)]
    private int _id;

    // [NotifyIgnore] - this field won't become a property
    [NotifyIgnore]
    private readonly List<string> _changeLog = new();

    /// <summary>
    /// Computed property that depends on FirstName, MiddleName, and LastName.
    /// LINQ is outside the getter allow-list, so DependsOn is explicit.
    /// </summary>
    [NotifyComputed(nameof(FirstName), nameof(MiddleName), nameof(LastName))]
    public string FullName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, LastName }.Where(s =>
                !string.IsNullOrWhiteSpace(s)
            );
            return string.Join(" ", parts);
        }
    }

    /// <summary>
    /// Validation property - can only save if required fields are filled.
    /// </summary>
    [NotifyComputed(nameof(FirstName), nameof(LastName))]
    public bool CanSave =>
        !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);

    /// <summary>
    /// Gets the change log for debugging.
    /// </summary>
    public IReadOnlyList<string> ChangeLog => _changeLog;

    /// <summary>
    /// Sets the ID (demonstrates private setter).
    /// </summary>
    public void Initialize(int id)
    {
        Id = id;
    }

    // Partial hook - called BEFORE FirstName changes
    partial void OnFirstNameChanging(string oldValue, string newValue)
    {
        _changeLog.Add($"FirstName changing: '{oldValue}' -> '{newValue}'");
    }

    // Partial hook - called AFTER FirstName changes
    partial void OnFirstNameChanged()
    {
        _changeLog.Add($"FirstName changed to: '{FirstName}'");
    }

    // Partial hook - validate Age
    partial void OnAgeChanging(int oldValue, int newValue)
    {
        if (newValue < 0)
            throw new ArgumentOutOfRangeException(nameof(newValue), "Age cannot be negative");
        if (newValue > 150)
            throw new ArgumentOutOfRangeException(nameof(newValue), "Age cannot exceed 150");
    }

    partial void OnAgeChanged()
    {
        _changeLog.Add($"Age changed to: {Age}");
    }

    partial void OnLastNameChanged()
    {
        _changeLog.Add($"LastName changed to: '{LastName}'");
    }

    partial void OnEmailChanged()
    {
        _changeLog.Add($"Email changed to: '{Email}'");
    }
}
