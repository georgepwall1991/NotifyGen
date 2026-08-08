# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Generate C# 14/preview partial properties from the existing `[Notify]` class attribute, including old/new partial hooks and `[NotifyAlso]` targets, without a runtime dependency
- Reject ambiguous generated property names with **NOTIFY009**

## [1.6.0] - 2026-07-26

### Added

- Nested `[Notify]` classes now generate correctly inside partial classes, structs, interfaces, records, and record structs, including generic declaration chains
- **NOTIFY006 Diagnostic and Code Fix**: Reports every non-partial containing type and offers **Make type partial**
- **NOTIFY007 Diagnostic**: Rejects file-local `[Notify]` targets and containing types before they can produce inaccessible generated declarations

### Fixed

- Static, const, and readonly fields are now rejected consistently by both the generator and analyzer, preventing invalid generated setters
- Generated declarations preserve accessibility, required modifiers, generic parameters, and escaped identifiers while remaining compatible with constrained generic chains
- Source hint names are collision-safe across nested types, same simple names, case-folding file systems, and long metadata identities
- Non-partial containing chains now report every blocking declaration and withhold generation instead of producing malformed nested output
- Unsafe declaration contexts and member-scoped pointer or function-pointer fields now generate compilable code with warning-free native-integer equality guards
- `[NotifyAlso]` validation no longer treats ineligible fields as future generated properties, so missing references produce `NOTIFY003`

### For contributors

- Valid generator fixtures now compile generated output instead of relying only on source-text assertions
- Added regression coverage for nested declaration shapes, source hint identity, field eligibility, and multi-level diagnostics

## [1.5.0] - 2026-02-09

### Added

- `[NotifySuppressable(AlwaysNotify = ...)]` allows selected properties to notify immediately during a suppression scope
- **NOTIFY004** and **NOTIFY005** identify static, const, and readonly fields that cannot generate properties
- Runtime integration coverage for notification suppression, `INotifyPropertyChanging`, and command refresh

### Release note

- The `v1.5.0` GitHub release was created with a `NotifyGen.1.4.0.nupkg` asset because package metadata was not advanced. NuGet therefore has no `1.5.0` package.

## [1.4.0] - 2026-01-27

### Added

- Optional `INotifyPropertyChanging` generation through `[Notify(ImplementChanging = true)]`
- `[NotifyCanExecuteChangedFor]` for automatic command `CanExecute` refresh
- `[NotifySuppressable]` for deferred, deduplicated batch notifications

## [1.3.2] - 2026-01-26

### Changed

- Updated package branding, icon, header artwork, and NuGet metadata

## [1.3.1] - 2026-01-26

### Added

- .NET 10 test coverage alongside .NET 8 and .NET 9
- Expanded NuGet tags and project artwork

## [1.3.0] - 2026-01-26

### Added

- Competitor, setter, generator, and incremental rebuild benchmarks
- Multi-target test, sample, and benchmark coverage for supported .NET SDKs

## [1.2.0] - 2026-01-26

### Changed

- Generator value models implement structural equality so Roslyn can reuse incremental pipeline outputs

## [1.1.0] - 2026-01-14

### Added

- **OnChanging Hook**: New `On{Property}Changing(T oldValue, T newValue)` partial method called *before* the property value is assigned, enabling validation and rejection by throwing
- **Custom Property Names**: New `[NotifyName("CustomName")]` attribute to override the default property name derived from field name
- **Setter Access Modifiers**: New `[NotifySetter(AccessLevel)]` attribute to control setter visibility (Private, Protected, Internal, etc.)
- **NOTIFY003 Diagnostic**: New analyzer warning when `[NotifyAlso]` references a non-existent property

### Changed

- Generated setter now calls `OnChanging` before assignment, then `OnPropertyChanged`, then `OnChanged`

## [1.0.0] - 2025-01-14

### Added

- Initial release of NotifyGen source generator
- `[Notify]` attribute for automatic `INotifyPropertyChanged` implementation
- `[NotifyIgnore]` attribute to exclude fields from generation
- `[NotifyAlso]` attribute for dependent property notifications
- Equality guards using `EqualityComparer<T>.Default` (no boxing for value types)
- Partial method hooks (`OnXxxChanged()`) for custom logic
- Nullable reference type support
- Analyzer diagnostics:
  - NOTIFY001: Class must be partial
  - NOTIFY002: No eligible fields found
- Full IDE support with IntelliSense for generated properties

### Technical Details

- Targets .NET Standard 2.0 for broad compatibility
- Uses `IIncrementalGenerator` for optimal IDE performance
- Zero runtime dependencies
