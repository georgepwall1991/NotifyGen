![NotifyGen Banner](https://raw.githubusercontent.com/georgepwall1991/NotifyGen/master/assets/header.png)

<p align="center">
  <img src="https://raw.githubusercontent.com/georgepwall1991/NotifyGen/master/assets/icon.png" alt="NotifyGen Icon" width="128" height="128" />
</p>

# NotifyGen

[![NuGet](https://img.shields.io/nuget/v/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![Build Status](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml/badge.svg)](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0%2B-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Stop writing INotifyPropertyChanged boilerplate. Let the compiler do it.**

## The Problem

Every WPF, MAUI, or Blazor developer knows this pain. You want one property:

```csharp
private string _name;
```

But you end up writing this:

```csharp
private string _name;
public string Name
{
    get => _name;
    set
    {
        if (_name != value)
        {
            _name = value;
            OnPropertyChanged();
        }
    }
}
```

Multiply that by every property in your ViewModels. It's tedious, error-prone, and clutters your code with repetitive boilerplate.

## The Solution

```csharp
using NotifyGen;

[Notify]
public partial class Person
{
    private string _name;
    private int _age;
    private string? _email;
}
```

NotifyGen generates the rest at compile time. No runtime reflection. No IL weaving. Just clean, debuggable C#.

## What Gets Generated

For the `Person` class above, NotifyGen generates:

```csharp
public partial class Person : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_name, value)) return;
            OnNameChanging(_name, value);
            _name = value;
            OnPropertyChanged();
            OnNameChanged();
        }
    }

    public int Age
    {
        get => _age;
        set
        {
            if (_age == value) return;  // Direct comparison for primitives
            OnAgeChanging(_age, value);
            _age = value;
            OnPropertyChanged();
            OnAgeChanged();
        }
    }

    public string? Email
    {
        get => _email;
        set
        {
            if (EqualityComparer<string?>.Default.Equals(_email, value)) return;
            OnEmailChanging(_email, value);
            _email = value;
            OnPropertyChanged();
            OnEmailChanged();
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    partial void OnNameChanging(string oldValue, string newValue);
    partial void OnNameChanged();
    partial void OnAgeChanging(int oldValue, int newValue);
    partial void OnAgeChanged();
    partial void OnEmailChanging(string? oldValue, string? newValue);
    partial void OnEmailChanged();
}
```

This generated code is visible in your IDE (look under Dependencies → Analyzers → NotifyGen). You can step through it in the debugger.

## Installation

```bash
dotnet add package NotifyGen
```

Or via Package Manager:
```
Install-Package NotifyGen
```

## Real-World Example

Here's a more complete ViewModel showing several features working together:

```csharp
using NotifyGen;

[Notify(ImplementChanging = true)]  // Enable PropertyChanging for undo/redo
[NotifySuppressable]                 // Enable batch notification suppression
public partial class CustomerViewModel
{
    // Basic properties - just declare the field
    [NotifyAlso("FullName")]
    private string _firstName;

    [NotifyAlso("FullName")]
    private string _lastName;

    private string? _email;

    // Notify dependent properties and refresh save command
    [NotifyAlso("CanSave")]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _company;

    // Private setter - can only be set internally
    [NotifySetter(AccessLevel.Private)]
    private int _id;

    // Custom property name with command notification
    [NotifyName("IsPreferredCustomer")]
    [NotifyCanExecuteChangedFor(nameof(ApplyDiscountCommand))]
    private bool _preferred;

    // Exclude from generation - manage manually
    [NotifyIgnore]
    private readonly ICustomerService _customerService;

    // Computed property that depends on FirstName and LastName
    public string FullName => $"{FirstName} {LastName}".Trim();

    // Validation property
    public bool CanSave => !string.IsNullOrWhiteSpace(FirstName)
                        && !string.IsNullOrWhiteSpace(Company);

    // Commands with auto-refreshing CanExecute
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand ApplyDiscountCommand { get; }

    public CustomerViewModel(ICustomerService customerService)
    {
        _customerService = customerService;
        SaveCommand = new RelayCommand(Save, () => CanSave);
        ApplyDiscountCommand = new RelayCommand(ApplyDiscount, () => IsPreferredCustomer);
    }

    // Bulk update without intermediate notifications
    public void LoadFromDto(CustomerDto dto)
    {
        using (SuppressNotifications())
        {
            FirstName = dto.FirstName;
            LastName = dto.LastName;
            Email = dto.Email;
            Company = dto.Company;
        }  // Single batch of PropertyChanged events fires here
    }

    // Hook into property changes for validation
    partial void OnFirstNameChanging(string oldValue, string newValue)
    {
        if (newValue?.Length > 100)
            throw new ArgumentException("First name too long");
    }

    // React to changes
    partial void OnEmailChanged()
    {
        ValidateEmail();
    }

    private void Save() { /* ... */ }
    private void ApplyDiscount() { /* ... */ }
    private void ValidateEmail() { /* ... */ }
}
```

Bind it in XAML:

```xml
<StackPanel DataContext="{Binding CustomerViewModel}">
    <TextBox Text="{Binding FirstName, UpdateSourceTrigger=PropertyChanged}" />
    <TextBox Text="{Binding LastName, UpdateSourceTrigger=PropertyChanged}" />
    <TextBlock Text="{Binding FullName}" />
    <CheckBox IsChecked="{Binding IsPreferredCustomer}" Content="Preferred Customer" />
    <Button Content="Save" IsEnabled="{Binding CanSave}" />
</StackPanel>
```

## Features

### Field Naming Convention

NotifyGen uses underscore-prefixed private fields:

| Field | Generated Property |
|-------|-------------------|
| `_name` | `Name` |
| `_firstName` | `FirstName` |
| `_isEnabled` | `IsEnabled` |
| `_id` | `Id` |

The underscore is stripped and the first letter is capitalized.

### Equality Guards

Every generated setter checks if the value actually changed before doing anything:

```csharp
// For primitive types (int, bool, double, etc.) - direct comparison
if (_age == value) return;

// For reference types and complex value types - EqualityComparer
if (EqualityComparer<string>.Default.Equals(_name, value)) return;
```

NotifyGen automatically detects primitive types and uses direct `==` comparison for maximum performance. This prevents unnecessary `PropertyChanged` events and infinite loops from two-way bindings. Works correctly with nulls, value types, and reference types.

### Dependent Properties with `[NotifyAlso]`

When one property affects another, use `[NotifyAlso]` to notify both:

```csharp
[Notify]
public partial class Rectangle
{
    [NotifyAlso("Area")]
    [NotifyAlso("Perimeter")]
    private double _width;

    [NotifyAlso("Area")]
    [NotifyAlso("Perimeter")]
    private double _height;

    public double Area => Width * Height;
    public double Perimeter => 2 * (Width + Height);
}
```

When `Width` changes, `PropertyChanged` fires for `Width`, `Area`, and `Perimeter`.

### Custom Property Names with `[NotifyName]`

Override the default naming:

```csharp
[NotifyName("IsVisible")]
private bool _shown;  // Generates IsVisible, not Shown

[NotifyName("CustomerID")]
private int _custId;  // Generates CustomerID, not CustId
```

### Setter Access Control with `[NotifySetter]`

Restrict who can set the property:

```csharp
[NotifySetter(AccessLevel.Private)]
private int _id;
// Result: public int Id { get; private set; }

[NotifySetter(AccessLevel.Protected)]
private string _internalState;
// Result: public string InternalState { get; protected set; }

[NotifySetter(AccessLevel.Internal)]
private DateTime _lastModified;
// Result: public DateTime LastModified { get; internal set; }
```

Available levels: `Public`, `Private`, `Protected`, `Internal`, `ProtectedInternal`, `PrivateProtected`

### Excluding Fields with `[NotifyIgnore]`

Some fields shouldn't become properties:

```csharp
[Notify]
public partial class ViewModel
{
    private string _name;  // Generates property

    [NotifyIgnore]
    private readonly ILogger _logger;  // No property

    [NotifyIgnore]
    private Dictionary<string, object> _cache;  // No property
}
```

### Partial Hooks

Every property gets two optional hooks:

**`On{Property}Changing(oldValue, newValue)`** - Called before the value changes. Use for validation:

```csharp
partial void OnAgeChanging(int oldValue, int newValue)
{
    if (newValue < 0 || newValue > 150)
        throw new ArgumentOutOfRangeException(nameof(newValue), "Invalid age");
}
```

**`On{Property}Changed()`** - Called after the value changes. Use for side effects:

```csharp
partial void OnSelectedItemChanged()
{
    LoadItemDetails();
    UpdateCommandStates();
}
```

If you don't implement these methods, the compiler removes the calls entirely—no performance cost.

### INotifyPropertyChanging with `ImplementChanging`

For undo/redo scenarios, you may need the `PropertyChanging` event that fires *before* the value changes:

```csharp
[Notify(ImplementChanging = true)]
public partial class Document
{
    private string _content;
}
```

This generates:

```csharp
public partial class Document : INotifyPropertyChanged, INotifyPropertyChanging
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;

    public string Content
    {
        get => _content;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_content, value)) return;
            OnPropertyChanging();        // Fires BEFORE change
            OnContentChanging(_content, value);
            _content = value;
            OnPropertyChanged();         // Fires AFTER change
            OnContentChanged();
        }
    }

    protected virtual void OnPropertyChanging([CallerMemberName] string? propertyName = null)
        => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
}
```

If your base class already implements `INotifyPropertyChanging`, NotifyGen detects this and won't duplicate the interface or events.

### Command CanExecute with `[NotifyCanExecuteChangedFor]`

When a property change should refresh a command's `CanExecute` state, use `[NotifyCanExecuteChangedFor]`:

```csharp
[Notify]
public partial class EditorViewModel
{
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private string _content;

    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand UndoCommand { get; }
}
```

This generates calls to `NotifyCanExecuteChanged()` in the setter:

```csharp
public string Content
{
    set
    {
        if (EqualityComparer<string>.Default.Equals(_content, value)) return;
        OnContentChanging(_content, value);
        _content = value;
        OnPropertyChanged();
        SaveCommand?.NotifyCanExecuteChanged();  // Auto-generated
        UndoCommand?.NotifyCanExecuteChanged();  // Auto-generated
        OnContentChanged();
    }
}
```

Works with any command type that has a `NotifyCanExecuteChanged()` method (CommunityToolkit.Mvvm `IRelayCommand`, Prism `DelegateCommand`, etc.).

### Batch Notification Suppression with `[NotifySuppressable]`

For bulk updates where you want to defer `PropertyChanged` events until all changes complete:

```csharp
[Notify]
[NotifySuppressable]
public partial class Person
{
    private string _firstName;
    private string _lastName;
    private string _email;
}

// Usage:
using (person.SuppressNotifications())
{
    person.FirstName = "John";
    person.LastName = "Doe";
    person.Email = "john@example.com";
}  // All three PropertyChanged events fire here
```

This generates suppression infrastructure:

```csharp
public partial class Person : INotifyPropertyChanged
{
    private int _notificationSuppressionCount;
    private HashSet<string>? _pendingNotifications;

    public IDisposable SuppressNotifications()
    {
        _notificationSuppressionCount++;
        return new NotificationSuppressor(this);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (_notificationSuppressionCount > 0)
        {
            _pendingNotifications ??= new HashSet<string>();
            _pendingNotifications.Add(propertyName!);
            return;
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    // ... ResumeNotifications and NotificationSuppressor class
}
```

**Features:**
- Nested suppression scopes supported (uses reference counting)
- Duplicate property names deduplicated (HashSet)
- Zero allocations when not suppressing
- Thread-safe within a single scope

### Working with Existing INotifyPropertyChanged

If your class already implements `INotifyPropertyChanged` (e.g., from a base class), NotifyGen detects this and won't generate a duplicate implementation:

```csharp
public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

[Notify]
public partial class MyViewModel : ViewModelBase
{
    private string _title;  // Uses base class OnPropertyChanged
}
```

## Built-in Analyzers & Code Fixes

NotifyGen includes analyzers that catch mistakes at compile time:

| Code | Severity | Description | Auto-Fix |
|------|----------|-------------|----------|
| NOTIFY001 | Error | Class with `[Notify]` must be declared `partial` | Yes |
| NOTIFY002 | Warning | No eligible fields found (need private `_underscore` fields) | — |
| NOTIFY003 | Warning | `[NotifyAlso("Xyz")]` references property `Xyz` that doesn't exist | — |

**NOTIFY001 has a code fix** — when you see the error, click the lightbulb (or press `Ctrl+.` / `Cmd+.`) and select "Make class partial" to automatically add the `partial` modifier.

## Performance

NotifyGen is built for large codebases:

- **Incremental generation** - Only regenerates code for classes that actually changed
- **No runtime overhead** - All code is generated at compile time
- **Efficient equality checks** - Uses `EqualityComparer<T>.Default` for optimal performance
- **Zero allocations in setters** - No boxing, no LINQ, no closures

The generator uses Roslyn's incremental compilation pipeline with proper `IEquatable<T>` implementations on all data structures, so your IDE stays responsive even with hundreds of `[Notify]` classes.

### Benchmark Results

Comparison against popular INPC libraries on .NET 9.0 (Apple M4):

#### Property Setters (String)

| Library | Mean | Ratio | Allocated |
|---------|-----:|------:|----------:|
| **NotifyGen** | **17.57 ns** | **1.00** | 48 B |
| CommunityToolkit.Mvvm | 18.47 ns | 1.05 | 48 B |
| Fody PropertyChanged | 18.50 ns | 1.05 | 48 B |
| Prism | 26.28 ns | 1.50 | 72 B |

#### Property Setters (Int)

| Library | Mean | Ratio | Allocated |
|---------|-----:|------:|----------:|
| Fody PropertyChanged | 0.46 ns | 0.92 | - |
| **NotifyGen** | **0.50 ns** | **1.00** | - |
| CommunityToolkit.Mvvm | 0.91 ns | 1.81 | - |
| Prism | 5.01 ns | 9.99 | 24 B |

#### Equality Guards (Same Value - No Event)

| Library | Mean | Ratio |
|---------|-----:|------:|
| Fody PropertyChanged | 0.48 ns | 0.93 |
| Prism | 0.50 ns | 0.97 |
| **NotifyGen** | **0.52 ns** | **1.00** |
| CommunityToolkit.Mvvm | 0.52 ns | 1.01 |

NotifyGen is the **fastest for string property setters** and competitive across all benchmarks. Primitive types (int, bool, double, etc.) use direct `==` comparison for optimal performance.

Run benchmarks yourself:
```bash
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks -- --filter *CompetitorBenchmarks*
```

## How It Compares

| | NotifyGen | Fody.PropertyChanged | CommunityToolkit.Mvvm |
|---|-----------|---------------------|----------------------|
| Approach | Source Generator | IL Weaving | Source Generator |
| Runtime dependency | None | None | Runtime library required |
| Debugging | Full—step through generated code | Limited—IL is modified | Full—step through generated code |
| Build impact | Runs during compile | Post-build step | Runs during compile |
| Equality checks | Always built-in | Configurable | Opt-in with attribute |
| Partial hooks | `OnXxxChanging` + `OnXxxChanged` | Intercept methods | `OnXxxChanging` only |
| INotifyPropertyChanging | ✅ `ImplementChanging = true` | ✅ Built-in | ✅ Separate attribute |
| Command CanExecute refresh | ✅ `[NotifyCanExecuteChangedFor]` | ❌ Manual | ✅ `[NotifyCanExecuteChangedFor]` |
| Batch notification suppression | ✅ `[NotifySuppressable]` | ❌ Not available | ❌ Not available |
| Learning curve | One attribute | Multiple attributes + config | Multiple attributes |
| **Performance** | **Fastest** | Fast | Good |

**When to use NotifyGen:** You want to eliminate INPC boilerplate with minimal setup. One attribute, done.

**When to use CommunityToolkit.Mvvm:** You need a full MVVM framework with commands, messaging, dependency injection, and more.

**When to use Fody:** You have an existing codebase using Fody, or you need IL-level modifications for other reasons.

## Requirements

- **.NET Standard 2.0+** — Compatible with:
  - .NET Framework 4.6.1+
  - .NET Core 3.1+
  - .NET 5, 6, 7, 8, 9, 10
  - Mono, Xamarin, Unity (2021.2+)
- **C# 9.0+** — Required for source generator support

## Quick Reference

```csharp
[Notify]                              // Enable generation for this class
[Notify(ImplementChanging = true)]    // Also implement INotifyPropertyChanging
[NotifySuppressable]                  // Enable batch notification suppression
public partial class MyViewModel      // Must be partial
{
    private string _name;             // → public string Name { get; set; }

    [NotifyIgnore]
    private int _internal;            // No property generated

    [NotifyAlso("FullName")]
    private string _firstName;        // Also raises PropertyChanged for FullName

    [NotifyName("IsActive")]
    private bool _active;             // → public bool IsActive { get; set; }

    [NotifySetter(AccessLevel.Private)]
    private int _id;                  // → public int Id { get; private set; }

    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _canSave;            // Calls SaveCommand.NotifyCanExecuteChanged()

    public IRelayCommand SaveCommand { get; }

    // Optional hooks - implement only what you need
    partial void OnNameChanging(string oldValue, string newValue);
    partial void OnNameChanged();
}

// Batch updates (when [NotifySuppressable] is applied):
using (viewModel.SuppressNotifications())
{
    viewModel.Name = "New Name";
    viewModel.IsActive = true;
}  // PropertyChanged fires for both here
```
```

## Troubleshooting

**Properties not generating?**
1. Add `partial` to your class declaration
2. Make sure fields are `private` (not `public`, `protected`, or `internal`)
3. Fields must start with underscore: `_name`, not `name` or `m_name`
4. Rebuild the solution (Ctrl+Shift+B)

**IntelliSense not showing generated properties?**
- Restart Visual Studio/Rider
- Check Dependencies → Analyzers → NotifyGen in Solution Explorer
- Ensure the project builds successfully

**NOTIFY001: Class must be partial?**
```csharp
// Wrong
[Notify]
public class MyClass { }

// Right
[Notify]
public partial class MyClass { }
```

**NOTIFY002: No eligible fields?**
```csharp
// Wrong - no underscore prefix
[Notify]
public partial class MyClass
{
    private string name;
}

// Right
[Notify]
public partial class MyClass
{
    private string _name;
}
```

## Samples

The repository includes sample projects to help you get started:

### Console Sample (Cross-Platform)

A simple console app demonstrating all NotifyGen features without any UI framework dependencies:

```bash
dotnet run --project samples/NotifyGen.ConsoleSample
```

This sample shows:
- Basic property generation
- `[NotifyAlso]` for dependent properties
- `[NotifyName]` for custom property names
- `[NotifySetter]` for access control
- `[NotifyIgnore]` for excluded fields
- `[NotifyCanExecuteChangedFor]` for command refresh
- `[NotifySuppressable]` for batch updates
- `ImplementChanging = true` for PropertyChanging events
- Partial hooks for validation and side effects
- Equality guards preventing duplicate events

### WPF Sample (Windows)

A WPF application demonstrating data binding with generated properties:

```bash
dotnet run --project samples/NotifyGen.WpfSample
```

## Benchmarks

Performance benchmarks are available to verify NotifyGen adds zero runtime overhead:

```bash
# Run all benchmarks
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks

# Run competitor comparison
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks -- --filter *CompetitorBenchmarks*

# Run setter performance (NotifyGen vs hand-written)
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks -- --filter *SetterBenchmarks*
```

Benchmarks include:
- **Competitor comparison** — NotifyGen vs CommunityToolkit.Mvvm, Prism, and Fody PropertyChanged
- **Setter performance** — Generated setters vs hand-written (should be identical)
- **Generator performance** — Compilation time for 1, 10, and 100 classes
- **Incremental rebuild** — Time to rebuild when only one class changes

Multi-framework support: Benchmarks target .NET 8.0, 9.0, and 10.0.

## Contributing

Found a bug? Have a feature request? [Open an issue](https://github.com/georgepwall1991/NotifyGen/issues).

Want to contribute code? PRs are welcome. Please include tests for new functionality.

## License

MIT License — use it in personal projects, commercial projects, wherever. See [LICENSE](LICENSE) for details.
