![NotifyGen Banner](https://raw.githubusercontent.com/georgepwall1991/NotifyGen/master/assets/header.png)

<p align="center">
  <img src="https://raw.githubusercontent.com/georgepwall1991/NotifyGen/master/assets/icon.png" alt="NotifyGen Icon" width="128" height="128" />
</p>

# NotifyGen

[![NuGet](https://img.shields.io/nuget/v/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![Build Status](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml/badge.svg)](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/codecov/c/github/georgepwall1991/NotifyGen)](https://codecov.io/gh/georgepwall1991/NotifyGen)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0%2B-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Stop writing INotifyPropertyChanged boilerplate. Let the compiler do it.**

Zero runtime reflection. No IL weaving. No required base class. Inspectable generated C# for WPF, MAUI, Avalonia, Uno, WinUI, and Blazor ViewModels.

## Quick start

```bash
dotnet add package NotifyGen
```

```csharp
using NotifyGen;

[Notify]
public partial class Person
{
    [NotifyAlso(nameof(FullName))]
    private string _firstName;

    [NotifyAlso(nameof(FullName))]
    private string _lastName;

    public string FullName => $"{FirstName} {LastName}";
}
```

NotifyGen generates equality-guarded setters, `PropertyChanged`, and optional hooks:

```csharp
partial void OnFirstNameChanged(string oldValue, string newValue);
```

## Why NotifyGen

| | NotifyGen | CommunityToolkit.Mvvm | Fody.PropertyChanged |
|--|-----------|----------------------|----------------------|
| Runtime dependency | None | Toolkit runtime | Weaver |
| Inspectable C# | Yes | Yes | No (IL) |
| Required base class | No (reuses host INPC) | `ObservableObject` typical | No |
| Scope | Narrow INPC generator | Full MVVM toolkit | Property weaver |

**Recommended stack:** NotifyGen for properties, CommunityToolkit for `RelayCommand` / messaging. See the [hybrid sample](samples/NotifyGen.HybridSample).

## Highlights

- Field → property (`_name` → `Name`) and C# 14 partial-property mode
- `[NotifyAlso]` with transitive closure, target-side `NotifyFrom`, child INPC, and collection membership
- Typed `On{Property}Changed(old, new)` hooks, changing events, suppressable bulk updates
- Property / `property:` / `get:` / `set:` metadata forwarding
- Analyzer diagnostics **NOTIFY001–NOTIFY017** + code fixes

## Docs

- [Full feature reference](docs/features.md)
- [Migrate from CommunityToolkit `[ObservableProperty]`](docs/migrate-from-communitytoolkit.md)
- [Diagnostics catalog](docs/diagnostics.md)
- [Samples](samples/README.md)
- [Changelog](CHANGELOG.md)

## Requirements

- .NET Standard 2.0+ consumers; Roslyn 4.8+ / modern .NET SDK for generation
- C# 14 / preview language version for incomplete partial-property mode

## License

MIT — see [LICENSE](LICENSE).
