# GitHub discoverability checklist

## Done (automated / in-repo)

- [x] Discussion templates under `.github/DISCUSSION_TEMPLATE/`
- [x] Launch checklist + channel copy: [launch-checklist.md](launch-checklist.md), [launch-channels.md](launch-channels.md)
- [x] Migration link elevated in root README
- [x] Docs site scaffold + `gh-pages` deploy workflow (`docs/site/`, `.github/workflows/docs.yml`)
- [x] NotifyGen **2.0.1** released (GitHub + NuGet)
- [x] Launch announcement issue: https://github.com/georgepwall1991/NotifyGen/issues/17
- [x] Stale remote branches deleted (only `master` remains)

## Manual (requires repo admin — cloud agent token returns 403)

1. **Topics** — remove typo `source-genertator`; keep `source-generators` / `source-generator` plus: `csharp`, `dotnet`, `mvvm`, `inotifypropertychanged`, `roslyn`, `wpf`, `maui`, `avalonia`, `blazor`, `nuget`, `code-generation`
2. **Enable Discussions** — Settings → Features → Discussions; pin a copy of issue #17 as Announcement
3. **Enable Pages** — Settings → Pages → Deploy from branch → **`gh-pages`** / root (after the Docs workflow has published once)
4. Publish external posts from [launch-channels.md](launch-channels.md)
