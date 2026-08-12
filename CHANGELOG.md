# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.2.1] - 2026-08-12

### Changed

- **NOTIFY023** is Hidden so adding NotifyGen to a CommunityToolkit-only type does not fail `TreatWarningsAsErrors`. The convert lightbulb / Fix All still rewrites the type. **NOTIFY022** stays Warning (leftover `[ObservableProperty]` on a `[Notify]` type still dual-generates).
- README and pack verify require the live docs site: https://georgepwall1991.github.io/NotifyGen/

## [2.2.0] - 2026-08-12

### Added

- `[NotifyProperty]` (and CommunityToolkit `[ObservableProperty]`) switches a `[Notify]` type to opt-in generation so unmarked underscore fields stay private
- **NOTIFY022** / **NOTIFY023** convert CommunityToolkit `[ObservableProperty]` / `[NotifyPropertyChangedFor]` to `[NotifyProperty]` / `[NotifyComputed]` with Fix All

### Changed

- Migration guide and hybrid sample show the lightbulb path instead of “delete `[ObservableProperty]` and generate every `_field`”

## [2.1.0] - 2026-08-12

### Added

- `[NotifyComputed]` wires `PropertyChanged` for a read-only derived property from a bounded getter walk or an explicit DependsOn list
- **NOTIFY018** / **NOTIFY019** / **NOTIFY021** for empty, generated-member, and unsupported computed getters
- Computed-property runtime and generator benchmarks

### Changed

- Samples and conversion docs show `[NotifyComputed]` as the default FullName / CanSave pattern

## [2.0.2] - 2026-08-12

### Added

- Code fixes for **NOTIFY002** (prefix private instance fields with `_`) and **NOTIFY003** (replace an unknown `[NotifyAlso]` name with the closest known property)
- Durable discoverability tests and `scripts/verify-packages.sh` pack gate

### Changed

- NuGet description leads with compile-time INPC, no runtime package, and no required `ObservableObject`
- Package tags drop `ReactiveUI` and OS stuffing; keep truthful UI/MVVM/Roslyn terms
- README links are absolute HTTPS so NuGet.org renders the migration funnel
- README no longer advertises the GitHub Pages URL while Pages is disabled
- Local `dotnet test` targets `net10.0` only; CI still runs net8.0/net9.0/net10.0

## [2.0.1] - 2026-08-11

### Added

- MAUI sample (`samples/NotifyGen.MauiSample`) mirroring WPF/Avalonia host INPC patterns
- Hybrid Avalonia UI sample (NotifyGen properties + CommunityToolkit `RelayCommand`)
- Docs site scaffold (`docs/site/`) with before/after generated-code page and GitHub Pages workflow
- Discussion templates (migration, show-and-tell, Q&A) and 2.0 launch checklist / channel copy
- README demo GIF (`assets/demo.gif`) and elevated CommunityToolkit migration funnel
- Cycle 6 demand-gated research scaffold (`docs/research/cycle-6.md`)

### Changed

- Split `NotifyGenerator` into partials (Discovery, Hooks, Attributes, Formatting, Emission, Subscriptions)
- Refresh complexity log for the new generator layout

## [2.0.0] - 2026-08-11

### Added

- Forward explicit `[property:]`, `[get:]`, and `[set:]` field attributes onto generated properties and accessors
- Add **NOTIFYSPR0001** / **NOTIFYSPR0002** suppressions for CS0657/CS0658 on `[Notify]` fields
- Add Avalonia sample, CommunityToolkit hybrid sample, migration guide, and diagnostics catalog
- Add pack-and-consume CI smoke job

### Changed

- Slim root README to pitch/quickstart; move full feature reference to `docs/features.md`

## [1.10.0] - 2026-08-09

### Added

- Reuse accessible existing `INotifyPropertyChanged` and `INotifyPropertyChanging` host invokers without duplicate interfaces, events, or helpers
- Add direct `[NotifyAlso(NotifyOnCollectionChanged = true)]` membership tracking with lazy subscription, replacement/null detachment, and deduplicated targets
- Add **NOTIFY013–NOTIFY017** safety diagnostics for incompatible hosts, unsupported collection declarations, and invalid generated names
- Add Cycle 4 research/design evidence and adversarial compile, Emit, reflection, host, collection, partial-property, suppression, and replacement tests

## [1.9.0] - 2026-08-09

### Added

- Add explicit target-side `[NotifyAlso(NotifyFrom = true)]` dependency declarations with transitive graph validation
- Add typed `On{Property}Changed(oldValue, newValue)` partial hooks with equality suppression and field/partial-property support

## [1.8.0] - 2026-08-09

### Added

- Forward property-targetable attributes from eligible fields to generated properties, preserving bound constructor/named values and safely skipping file-local symbols
- Opt into direct child `INotifyPropertyChanged` notifications with `NotifyAlso.NotifyOnSubPropertyChanged`, including replacement/unsubscription handling and **NOTIFY010** guidance

## [1.7.0] - 2026-08-08

### Added

- Generate C# 14/preview partial properties from the existing `[Notify]` class attribute, including old/new partial hooks and `[NotifyAlso]` targets, without a runtime dependency
- Reject ambiguous generated property names with **NOTIFY009**
- Transitive `[NotifyAlso]` closure now deduplicates reachable notifications and reports cycles with **NOTIFY008**

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
