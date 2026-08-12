# Samples

| Sample | Purpose |
|--------|---------|
| [NotifyGen.ConsoleSample](NotifyGen.ConsoleSample) | Cross-platform attribute tour |
| [NotifyGen.WpfSample](NotifyGen.WpfSample) | Host INPC, child/collection notify, typed hooks, suppressable bulk load |
| [NotifyGen.AvaloniaSample](NotifyGen.AvaloniaSample) | Same patterns on Avalonia |
| [NotifyGen.MauiSample](NotifyGen.MauiSample) | Same patterns on .NET MAUI (requires MAUI workload; not in CI solution build) |
| [NotifyGen.HybridSample](NotifyGen.HybridSample) | Avalonia UI: NotifyGen properties + CommunityToolkit `RelayCommand` |

Recommended production stack: **NotifyGen for INPC**, **CommunityToolkit.Mvvm for commands**.

`[NotifyComputed]` on `FullName` / `CanSave` is the 2.1 pattern; the console sample uses explicit DependsOn because its `FullName` getter uses LINQ.
