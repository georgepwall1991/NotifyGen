# Channel copy for NotifyGen 2.0 / 2.2

Paste-ready posts. Prefer one high-signal post per venue; do not spam.
**Do not post** these drafts as an agent — George posts in his own name.

## CommunityToolkit/dotnet#1175 (do not post as George)

Paste into https://github.com/CommunityToolkit/dotnet/discussions/1175 only from George's account.

CommunityToolkit still has no `[ComputedProperty]`. NotifyGen 2.2 ships that shape and a one-ViewModel convert:

- `[NotifyComputed]` / `[NotifyComputed(nameof(FirstName), nameof(LastName))]` — bounded getter walk or explicit DependsOn
- Add `NotifyGen`, click the lightbulb on `[ObservableProperty]` (**NOTIFY023**, Hidden so `TreatWarningsAsErrors` stays green), Fix All. Unmarked `_logger` stays private. Keep `RelayCommand` on CommunityToolkit.

Docs: https://georgepwall1991.github.io/NotifyGen/migrate.html  
NuGet: https://www.nuget.org/packages/NotifyGen/  
I am the NotifyGen maintainer. This is an offer to try the missing API, not a request to take a dependency in the Toolkit.

## Reddit r/csharp (do not post as George)

**Title:** NotifyGen 2.2 — `[NotifyComputed]` plus one-click convert from `[ObservableProperty]`

NotifyGen is a narrow Roslyn source generator for `INotifyPropertyChanged`. One `[Notify]` on a partial class turns underscore fields into equality-guarded, inspectable properties — no runtime package, no required `ObservableObject`.

2.2:

- `[NotifyComputed]` for derived properties (the API CommunityToolkit/dotnet#1175 asked for)
- Lightbulb / Fix All on `[ObservableProperty]` (**NOTIFY023**) converts one ViewModel. `_logger` stays private. Keep CommunityToolkit for `RelayCommand`.

Docs: https://georgepwall1991.github.io/NotifyGen/  
Migrate: https://georgepwall1991.github.io/NotifyGen/migrate.html  
NuGet: https://www.nuget.org/packages/NotifyGen/

## Short (Reddit / Discord / Slack)

**Title:** NotifyGen 2.0 — zero-runtime INPC that plays with CommunityToolkit

NotifyGen is a narrow Roslyn source generator for `INotifyPropertyChanged`. One `[Notify]` on a partial class turns underscore fields (or C# 14 incomplete partials) into equality-guarded, debuggable properties — no runtime package, no required `ObservableObject`.

**Recommended stack:** NotifyGen for properties, CommunityToolkit.Mvvm for `RelayCommand` / messaging.

What's new in 2.2:
- One-click CommunityToolkit conversion (**NOTIFY023**): `[NotifyProperty]` opt-in so `_logger` stays private
- `[NotifyComputed]` for derived properties (CommunityToolkit discussion #1175 still open)

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
5. Pin GitHub Discussion https://github.com/georgepwall1991/NotifyGen/discussions/22 (API cannot pin; UI-only)
