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
            if (EqualityComparer<int>.Default.Equals(_age, value)) return;
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

[Notify]
public partial class CustomerViewModel
{
    // Basic properties - just declare the field
    private string _firstName;
    private string _lastName;
    private string? _email;

    // Notify dependent properties when these change
    [NotifyAlso("FullName")]
    [NotifyAlso("CanSave")]
    private string _company;

    // Private setter - can only be set internally
    [NotifySetter(AccessLevel.Private)]
    private int _id;

    // Custom property name
    [NotifyName("IsPreferredCustomer")]
    private bool _preferred;

    // Exclude from generation - manage manually
    [NotifyIgnore]
    private readonly ICustomerService _customerService;

    // Computed property that depends on FirstName and LastName
    public string FullName => $"{FirstName} {LastName}".Trim();

    // Validation property
    public bool CanSave => !string.IsNullOrWhiteSpace(FirstName)
                        && !string.IsNullOrWhiteSpace(Company);

    public CustomerViewModel(ICustomerService customerService)
    {
        _customerService = customerService;
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
        // Maybe validate email format, update UI state, etc.
        ValidateEmail();
    }
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
if (EqualityComparer<string>.Default.Equals(_name, value)) return;
```

This prevents unnecessary `PropertyChanged` events and infinite loops from two-way bindings. Works correctly with nulls, value types, and reference types.

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
| **NotifyGen** | **16.68 ns** | **1.00** | 48 B |
| CommunityToolkit.Mvvm | 18.46 ns | 1.11 | 48 B |
| Fody PropertyChanged | 18.44 ns | 1.11 | 48 B |
| Prism | 25.60 ns | 1.54 | 72 B |

#### Property Setters (Int)

| Library | Mean | Ratio | Allocated |
|---------|-----:|------:|----------:|
| **NotifyGen** | **0.38 ns** | **1.00** | - |
| Fody PropertyChanged | 0.48 ns | 1.28 | - |
| CommunityToolkit.Mvvm | 0.83 ns | 2.21 | - |
| Prism | 4.77 ns | 12.67 | 24 B |

#### Equality Guards (Same Value - No Event)

| Library | Mean | Ratio |
|---------|-----:|------:|
| **NotifyGen** | **0.07 ns** | **1.00** |
| Fody PropertyChanged | 0.46 ns | 6.65 |
| Prism | 0.49 ns | 6.94 |
| CommunityToolkit.Mvvm | 0.57 ns | 8.13 |

NotifyGen's generated code is the fastest across all benchmarks—identical to hand-written code.

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

    // Optional hooks - implement only what you need
    partial void OnNameChanging(string oldValue, string newValue);
    partial void OnNameChanged();
}
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
