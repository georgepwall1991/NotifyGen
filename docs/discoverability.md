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
- [x] Root README no longer advertises the Pages URL while it 404s

## Manual (requires repo admin — cloud agent token returns 403)

1. **Topics** — remove typo `source-genertator`; keep `source-generators` / `source-generator` plus: `csharp`, `dotnet`, `mvvm`, `inotifypropertychanged`, `roslyn`, `wpf`, `maui`, `avalonia`, `blazor`, `nuget`, `code-generation`
2. **Enable Discussions** — Settings → Features → Discussions; pin a copy of issue #17 as Announcement
3. **Enable Pages** — Settings → Pages → Deploy from branch → **`gh-pages` / root** (after the Docs workflow has published once). Restore the Pages URL in the README only after `https://georgepwall1991.github.io/NotifyGen/` returns 200.
4. Publish external posts from [launch-channels.md](launch-channels.md)
