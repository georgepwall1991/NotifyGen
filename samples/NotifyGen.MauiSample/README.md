# NotifyGen MAUI Sample

Demonstrates the same Cycle 2–4 patterns as the WPF/Avalonia samples on **.NET MAUI**:

- Host `INotifyPropertyChanged` reuse (`ViewModelBase`)
- `[NotifyAlso]` + `FullName`
- Child INPC (`NotifyOnSubPropertyChanged`)
- Collection membership (`NotifyOnCollectionChanged`)
- `[NotifySuppressable]` bulk reload

## Requirements

- .NET 8 SDK with the **MAUI workload**: `dotnet workload install maui`
- Android / iOS / Mac Catalyst / Windows workload as needed for your target

This project is **not** in the main solution build (Ubuntu CI has no MAUI workloads). Build it locally:

```bash
dotnet build samples/NotifyGen.MauiSample/NotifyGen.MauiSample.csproj -f net8.0-android
# or
dotnet build samples/NotifyGen.MauiSample/NotifyGen.MauiSample.csproj -f net8.0-windows10.0.19041.0
```

## Run

```bash
dotnet build samples/NotifyGen.MauiSample -t:Run -f net8.0-android
```
