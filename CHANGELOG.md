# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
