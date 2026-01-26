# NotifyGen

[![NuGet](https://img.shields.io/nuget/v/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![Build Status](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml/badge.svg)](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml)
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

That's 6 lines. NotifyGen generates the rest at compile time—properties, change notification, equality checks, and partial hooks. No runtime reflection. No IL weaving. Just clean, debuggable C#.

## What You Get

For each field, NotifyGen generates:

- **A public property** with proper getter and setter
- **Equality guard** - `PropertyChanged` only fires when the value actually changes
- **Partial hooks** - `OnXxxChanging()` and `OnXxxChanged()` for validation or side effects
- **Full INotifyPropertyChanged implementation** if your class doesn't already have one

The generated code is visible in your IDE. You can step through it in the debugger. It's just regular C#.

## Installation

```bash
dotnet add package NotifyGen
```

## Features

### Dependent Properties with `[NotifyAlso]`

Got a computed property that depends on other fields? Tell NotifyGen to notify it too:

```csharp
[Notify]
public partial class Person
{
    [NotifyAlso("FullName")]
    private string _firstName;

    [NotifyAlso("FullName")]
    private string _lastName;

    public string FullName => $"{FirstName} {LastName}";
}
```

Now when `FirstName` or `LastName` changes, your UI updates `FullName` automatically.

### Custom Property Names with `[NotifyName]`

Don't like the generated name? Override it:

```csharp
[NotifyName("IsVisible")]
private bool _shown;  // Generates IsVisible, not Shown
```

### Restricted Setters with `[NotifySetter]`

Need a read-only property from outside the class?

```csharp
[NotifySetter(AccessLevel.Private)]
private int _id;  // public int Id { get; private set; }
```

### Exclude Fields with `[NotifyIgnore]`

Keep some fields out of the generated properties:

```csharp
[NotifyIgnore]
private string _internalCache;  // No property generated
```

### Partial Hooks for Custom Logic

Every property gets two hooks you can implement:

```csharp
[Notify]
public partial class Order
{
    private decimal _total;

    partial void OnTotalChanging(decimal oldValue, decimal newValue)
    {
        // Validate before the change
        if (newValue < 0)
            throw new ArgumentException("Total cannot be negative");
    }

    partial void OnTotalChanged()
    {
        // React after the change
        UpdateTaxCalculation();
    }
}
```

Don't need them? Don't implement them. The compiler optimizes away unused partial methods.

## Built-in Analyzers

NotifyGen catches mistakes at compile time:

| Code | What it catches |
|------|-----------------|
| NOTIFY001 | Class isn't marked `partial` |
| NOTIFY002 | No private underscore-prefixed fields found |
| NOTIFY003 | `[NotifyAlso]` references a property that doesn't exist |

## How It Compares

| | NotifyGen | Fody.PropertyChanged | CommunityToolkit.Mvvm |
|---|-----------|---------------------|----------------------|
| Approach | Source Generator | IL Weaving | Source Generator |
| Runtime dependency | None | None | Yes (runtime library) |
| Debugging | Step through generated code | Limited | Step through generated code |
| Build impact | Minimal | Adds post-build step | Minimal |
| Equality checks | Always (built-in) | Configurable | Optional attribute |

NotifyGen takes a focused approach: one thing, done well. If you need a full MVVM framework with commands, messaging, and DI, look at CommunityToolkit.Mvvm. If you just want to stop writing property boilerplate, NotifyGen is all you need.

## Requirements

- **.NET Standard 2.0+** — works with .NET Framework 4.6.1+, .NET Core 3.1+, .NET 5/6/7/8/9
- **C# 9.0+** — required for source generators

## Quick Reference

```csharp
[Notify]                          // Enable generation for this class
public partial class MyViewModel  // Must be partial
{
    private string _name;         // Generates Name property

    [NotifyIgnore]
    private int _ignored;         // No property generated

    [NotifyAlso("FullName")]
    private string _firstName;    // Also notifies FullName when changed

    [NotifyName("IsActive")]
    private bool _active;         // Generates IsActive, not Active

    [NotifySetter(AccessLevel.Private)]
    private int _id;              // public int Id { get; private set; }
}
```

## Troubleshooting

**Properties not generating?**
1. Class must be `partial`
2. Fields must be `private`
3. Fields must start with `_` (e.g., `_name` → `Name`)
4. Rebuild the solution

**IntelliSense not working?**
Restart your IDE. Source generators sometimes need a kick.

## Contributing

Found a bug? Have an idea? [Open an issue](https://github.com/georgepwall1991/NotifyGen/issues) or submit a PR.

## License

MIT — use it however you want.
