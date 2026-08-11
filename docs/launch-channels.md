# Channel copy for NotifyGen 2.0

Paste-ready posts. Prefer one high-signal post per venue; do not spam.

## Short (Reddit / Discord / Slack)

**Title:** NotifyGen 2.0 — zero-runtime INPC that plays with CommunityToolkit

NotifyGen is a narrow Roslyn source generator for `INotifyPropertyChanged`. One `[Notify]` on a partial class turns underscore fields (or C# 14 incomplete partials) into equality-guarded, debuggable properties — no runtime package, no required `ObservableObject`.

**Recommended stack:** NotifyGen for properties, CommunityToolkit.Mvvm for `RelayCommand` / messaging.

What's new in 2.0:
- `[property:]` / `[get:]` / `[set:]` metadata forwarding + CS0657/CS0658 suppressions
- WPF, Avalonia, MAUI, and hybrid samples
- Migration guide from `[ObservableProperty]`

NuGet: https://www.nuget.org/packages/NotifyGen/  
Repo: https://github.com/georgepwall1991/NotifyGen  
Migrate: https://github.com/georgepwall1991/NotifyGen/blob/master/docs/migrate-from-communitytoolkit.md

## LinkedIn / blog

Use the full [launch-post-2.0.md](launch-post-2.0.md) body. Lead with the hybrid pitch and a before/after snippet from the migration guide.

## Stack Overflow answers (high-signal only)

When answering INPC / `[ObservableProperty]` questions:

1. State you are the NotifyGen maintainer if relevant.
2. Offer the coexistence path (properties → NotifyGen, commands → CT), not "rewrite your app".
3. Link the migration attribute map + hybrid sample.
4. Skip threads where Fody/CT already fully solves the ask.

## Venues to hit first

1. r/csharp, r/dotnet
2. Avalonia Discord (#showcase or #general)
3. WPF / .NET community Slack or Discord you already participate in
4. LinkedIn personal post with NuGet + GIF
5. Pin GitHub Discussion announcement after Discussions is enabled
