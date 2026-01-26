using System.ComponentModel;
using NotifyGen.ConsoleSample;

Console.WriteLine("NotifyGen Console Sample");
Console.WriteLine("========================\n");

// Create a view model with PropertyChanged handler
var person = new PersonViewModel();
person.PropertyChanged += OnPropertyChanged;

Console.WriteLine("1. Basic Property Changes");
Console.WriteLine("-------------------------");
person.FirstName = "John";
person.LastName = "Doe";
person.Age = 30;
Console.WriteLine();

Console.WriteLine("2. Nullable Property");
Console.WriteLine("--------------------");
person.Email = "john.doe@example.com";
Console.WriteLine();

Console.WriteLine("3. Custom Property Name ([NotifyName])");
Console.WriteLine("--------------------------------------");
Console.WriteLine($"Setting IsActive (field is _active)...");
person.IsActive = true;
Console.WriteLine();

Console.WriteLine("4. Private Setter ([NotifySetter])");
Console.WriteLine("----------------------------------");
Console.WriteLine($"Id before Initialize: {person.Id}");
person.Initialize(42);
Console.WriteLine($"Id after Initialize: {person.Id}");
Console.WriteLine("(Id can only be set from within the class)");
Console.WriteLine();

Console.WriteLine("5. Dependent Properties ([NotifyAlso])");
Console.WriteLine("--------------------------------------");
Console.WriteLine($"FullName: {person.FullName}");
Console.WriteLine($"CanSave: {person.CanSave}");
Console.WriteLine("Changing MiddleName (notifies FullName)...");
person.MiddleName = "William";
Console.WriteLine($"FullName now: {person.FullName}");
Console.WriteLine();

Console.WriteLine("6. Equality Guard (no event if same value)");
Console.WriteLine("------------------------------------------");
Console.WriteLine("Setting FirstName to same value 'John'...");
person.FirstName = "John"; // Should NOT raise PropertyChanged
Console.WriteLine("(No PropertyChanged event expected above)");
Console.WriteLine();

Console.WriteLine("7. Partial Hook Validation");
Console.WriteLine("--------------------------");
Console.WriteLine("Trying to set Age to -1...");
try
{
    person.Age = -1;
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"Validation caught: {ex.Message}");
}
Console.WriteLine();

Console.WriteLine("8. Change Log (from partial hooks)");
Console.WriteLine("-----------------------------------");
foreach (var entry in person.ChangeLog)
{
    Console.WriteLine($"  - {entry}");
}
Console.WriteLine();

Console.WriteLine("Sample completed successfully!");

void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    Console.WriteLine($"  PropertyChanged: {e.PropertyName}");
}
