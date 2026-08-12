# GitHub discoverability checklist

## Keyword map (2.0.2)

| Term | Where it lives |
|------|----------------|
| INotifyPropertyChanged / INPC | Title, Description, PackageTags, README hook |
| source generator / Roslyn | Description, PackageTags |
| no runtime / no required ObservableObject | Description first sentence, README Why table |
| CommunityToolkit coexistence | Description, README migration section |
| WPF / MAUI / Avalonia / WinUI / Blazor | PackageTags, README hook |
| NotifyAlso / dependent properties | README sample + highlights |
| NotifyComputed / computed / derived properties | README quick start + highlights, package description |
| NotifyProperty / ObservableProperty conversion | README migration hook, NOTIFY022/023, package description |

Dropped as untruthful or stuffed: `ReactiveUI`, `android`, `ios`, `mac`, `linux`.

## Done (automated / in-repo)

- [x] Discussion templates under `.github/DISCUSSION_TEMPLATE/`
- [x] Launch checklist + channel copy: [launch-checklist.md](launch-checklist.md), [launch-channels.md](launch-channels.md)
- [x] Migration link elevated in root README
- [x] Docs site scaffold + `gh-pages` deploy workflow (`docs/site/`, `.github/workflows/docs.yml`)
- [x] NotifyGen **2.0.1** released (GitHub + NuGet)
- [x] Launch announcement issue: https://github.com/georgepwall1991/NotifyGen/issues/17
- [x] Stale remote branches deleted (only `master` remains)
- [x] **2.0.2** honest tags/description, absolute README hrefs, packed `assets/`, discoverability tests, pack verify script
- [x] **2.2.1** README + pack verify require the live Pages URL
- [x] Topics typo `source-genertator` removed
- [x] Discussions enabled
- [x] Pages live: https://georgepwall1991.github.io/NotifyGen/
- [x] 2.2 announcement discussion: https://github.com/georgepwall1991/NotifyGen/discussions/22 (pin is UI-only; GraphQL has no pinDiscussion)

## Manual (George's identity — do not post as an agent)

1. Pin the 2.2 Discussions announcement if the API pin failed
2. Publish external posts from [launch-channels.md](launch-channels.md) — **do not post** #1175 or Reddit as an agent
