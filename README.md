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

<p align="center">
  <img src="https://raw.githubusercontent.com/georgepwall1991/NotifyGen/master/assets/demo.gif" alt="NotifyGen binding demo: FirstName/LastName update FullName via NotifyAlso" width="560" />
</p>

## Migrating from `[ObservableProperty]`?

Keep **CommunityToolkit.Mvvm** for `RelayCommand` / messaging. Use **NotifyGen** for properties — no required `ObservableObject`, zero runtime package.

→ **[Migration checklist & attribute map](https://github.com/georgepwall1991/NotifyGen/blob/master/docs/migrate-from-communitytoolkit.md)** · [Hybrid UI sample](https://github.com/georgepwall1991/NotifyGen/tree/master/samples/NotifyGen.HybridSample)

## Quick start

```bash
dotnet add package NotifyGen
```

```xml
<PackageReference Include="NotifyGen" Version="2.0.2">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
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

**Recommended stack:** NotifyGen for properties, CommunityToolkit for `RelayCommand` / messaging. See the [hybrid sample](https://github.com/georgepwall1991/NotifyGen/tree/master/samples/NotifyGen.HybridSample).

## Highlights

- Field → property (`_name` → `Name`) and C# 14 partial-property mode
- `[NotifyAlso]` with transitive closure, target-side `NotifyFrom`, child INPC, and collection membership
- Typed `On{Property}Changed(old, new)` hooks, changing events, suppressable bulk updates
- Property / `property:` / `get:` / `set:` metadata forwarding
- Analyzer diagnostics **NOTIFY001–NOTIFY017**; code fixes for missing `partial` (**NOTIFY001** / **NOTIFY006**), underscore field names (**NOTIFY002**), and unknown `[NotifyAlso]` names (**NOTIFY003**)

## Docs

- [Full feature reference](https://github.com/georgepwall1991/NotifyGen/blob/master/docs/features.md)
- [Migrate from CommunityToolkit `[ObservableProperty]`](https://github.com/georgepwall1991/NotifyGen/blob/master/docs/migrate-from-communitytoolkit.md)
- [Diagnostics catalog](https://github.com/georgepwall1991/NotifyGen/blob/master/docs/diagnostics.md)
- [Samples](https://github.com/georgepwall1991/NotifyGen/blob/master/samples/README.md) — WPF, Avalonia, MAUI, Hybrid
- [Changelog](https://github.com/georgepwall1991/NotifyGen/blob/master/CHANGELOG.md)
- [2.0 launch post](https://github.com/georgepwall1991/NotifyGen/blob/master/docs/launch-post-2.0.md)

The GitHub Pages site is generated from `docs/site/` once Pages is enabled from the `gh-pages` branch. Until then the documents above are the source of truth.

## Requirements

- .NET Standard 2.0+ consumers; Roslyn 4.8+ / modern .NET SDK for generation
- C# 14 / preview language version for incomplete partial-property mode

## License

MIT — see [LICENSE](https://github.com/georgepwall1991/NotifyGen/blob/master/LICENSE).
