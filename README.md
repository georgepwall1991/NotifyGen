# NotifyGen

[![NuGet](https://img.shields.io/nuget/v/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![Build Status](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml/badge.svg)](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**A C# source generator that eliminates `INotifyPropertyChanged` boilerplate.**

NotifyGen automatically generates property implementations with change notification from annotated fields. No runtime reflection, no IL weaving, just clean generated code.

## Features

- **Zero boilerplate** - Just add `[Notify]` to your class
- **Compile-time generation** - No runtime overhead
- **Equality guards** - Only raises `PropertyChanged` when values actually change
- **Partial hooks** - `OnXxxChanged()` methods for custom logic
- **Dependent properties** - `[NotifyAlso]` for computed properties
- **IDE support** - Full IntelliSense for generated properties
- **Nullable aware** - Handles nullable reference types correctly

## Installation

```bash
dotnet add package NotifyGen
```

Or via NuGet Package Manager:
```
Install-Package NotifyGen
```

## Quick Start

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

That's it! NotifyGen generates the following:

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
            _email = value;
            OnPropertyChanged();
            OnEmailChanged();
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    partial void OnNameChanged();
    partial void OnAgeChanged();
    partial void OnEmailChanged();
}
```

## Attributes

### `[Notify]`

Apply to a `partial` class to enable property generation for all private fields with underscore prefix.

```csharp
[Notify]
public partial class ViewModel
{
    private string _title;     // Generates Title property
    private int _count;        // Generates Count property
}
```

**Requirements:**
- Class must be `partial`
- Fields must be `private`
- Fields must start with underscore (`_fieldName` → `FieldName`)

### `[NotifyIgnore]`

Exclude a field from property generation.

```csharp
[Notify]
public partial class ViewModel
{
    private string _name;           // Generates Name property

    [NotifyIgnore]
    private string _internalCache;  // No property generated
}
```

### `[NotifyAlso]`

Notify additional properties when this field changes. Useful for computed/dependent properties.

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

When `FirstName` or `LastName` changes, `PropertyChanged` is also raised for `FullName`.

You can notify multiple properties:

```csharp
[NotifyAlso("FullName")]
[NotifyAlso("DisplayName")]
private string _firstName;
```

## Partial Hooks

Every generated property includes a partial method hook that's called after the value changes:

```csharp
[Notify]
public partial class Settings
{
    private bool _darkMode;

    partial void OnDarkModeChanged()
    {
        // Called whenever DarkMode changes
        ApplyTheme(DarkMode ? Theme.Dark : Theme.Light);
    }
}
```

## Analyzer Diagnostics

NotifyGen includes analyzers to catch common mistakes:

| Code | Severity | Description |
|------|----------|-------------|
| NOTIFY001 | Error | Class marked with `[Notify]` must be `partial` |
| NOTIFY002 | Warning | No eligible fields found (no underscore-prefixed private fields) |

## Requirements

- .NET Standard 2.0+ (works with .NET Framework 4.6.1+, .NET Core, .NET 5+)
- C# 9.0+ (for source generators)

## Comparison to Alternatives

| Feature | NotifyGen | Fody.PropertyChanged | CommunityToolkit.Mvvm |
|---------|-----------|---------------------|----------------------|
| Generation | Source Generator | IL Weaving | Source Generator |
| Runtime dependency | None | None | Runtime library |
| Build speed | Fast | Slower | Fast |
| Debugging | Full support | Limited | Full support |
| Configuration | Attributes | Attributes + Config | Attributes |
| Equality check | Built-in | Built-in | Optional |
| Partial hooks | Yes | Limited | Yes |

## Troubleshooting

### Properties not generating?

1. Ensure your class is `partial`
2. Ensure fields are `private`
3. Ensure fields start with underscore (`_name`, not `name`)
4. Rebuild the solution

### IntelliSense not showing generated properties?

1. Rebuild the solution
2. Restart your IDE
3. Check that the NotifyGen package is properly referenced

### Build errors after adding [Notify]?

Check the Error List for NOTIFY001/NOTIFY002 diagnostics - they provide guidance on what's wrong.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
