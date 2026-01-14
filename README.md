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
- **Partial hooks** - `OnXxxChanging()` and `OnXxxChanged()` methods for custom logic
- **Dependent properties** - `[NotifyAlso]` for computed properties
- **Custom property names** - `[NotifyName]` to override generated names
- **Setter access control** - `[NotifySetter]` to restrict setter visibility
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
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    partial void OnNameChanging(string oldValue, string newValue);
    partial void OnNameChanged();
    partial void OnAgeChanging(int oldValue, int newValue);
    partial void OnAgeChanged();
    partial void OnEmailChanging(string? oldValue, string? newValue);
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

### `[NotifyName]`

Override the default property name derived from the field name.

```csharp
[Notify]
public partial class Settings
{
    [NotifyName("IsVisible")]
    private bool _visibleState;  // Generates IsVisible property instead of VisibleState
}
```

### `[NotifySetter]`

Control the access level of the generated setter.

```csharp
[Notify]
public partial class Entity
{
    [NotifySetter(AccessLevel.Private)]
    private int _id;  // Generates: public int Id { get; private set; }

    [NotifySetter(AccessLevel.Protected)]
    private string _internalState;  // Generates: public string InternalState { get; protected set; }
}
```

Available access levels:
- `AccessLevel.Public` (default)
- `AccessLevel.Protected`
- `AccessLevel.Internal`
- `AccessLevel.Private`
- `AccessLevel.ProtectedInternal`
- `AccessLevel.PrivateProtected`

## Partial Hooks

Every generated property includes two partial method hooks:

### `On{Property}Changing` - Called Before Assignment

```csharp
[Notify]
public partial class Order
{
    private decimal _total;

    partial void OnTotalChanging(decimal oldValue, decimal newValue)
    {
        // Called before the value changes
        // Useful for validation or logging
        Console.WriteLine($"Total changing from {oldValue} to {newValue}");
    }
}
```

### `On{Property}Changed` - Called After Assignment

```csharp
[Notify]
public partial class Settings
{
    private bool _darkMode;

    partial void OnDarkModeChanged()
    {
        // Called after the value changes
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
| NOTIFY003 | Warning | `[NotifyAlso]` references a property that doesn't exist |

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
