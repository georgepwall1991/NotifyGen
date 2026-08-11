# NotifyGen 2.0 launch checklist

In-repo assets for this phase live in the repo. GitHub **repository settings** (topics, Discussions) require a maintainer token with `administration` scope — cloud agents often get 403 on those writes.

## Repo (done in-tree)

- [x] Launch post: [launch-post-2.0.md](launch-post-2.0.md)
- [x] External channel copy: [launch-channels.md](launch-channels.md)
- [x] Discussion templates: `.github/DISCUSSION_TEMPLATE/` (migration, show-and-tell, Q&A)
- [x] Migration funnel elevated in root README
- [x] Samples: WPF, Avalonia, MAUI, Hybrid UI
- [x] Demo GIF: `assets/demo.gif`
- [x] Docs site scaffold: `docs/site/` + Pages workflow

## Maintainer (manual on github.com)

1. **Topics** — Settings → General → Topics  
   - Remove typo `source-genertator`  
   - Keep: `csharp`, `dotnet`, `mvvm`, `inotifypropertychanged`, `source-generators`, `roslyn`, `wpf`, `maui`, `avalonia`, `blazor`, `nuget`
2. **Enable Discussions** — Settings → General → Features → Discussions  
3. Seed categories (or use default + templates):
   - Migration / Show and tell → points at migrate guide  
   - Announcements → pin 2.0 launch post body  
4. Create and **pin** a Discussions announcement from [launch-post-2.0.md](launch-post-2.0.md)
5. Publish channel posts from [launch-channels.md](launch-channels.md)

## Leading indicators (weekly)

| Metric | Where |
|--------|--------|
| NuGet downloads | nuget.org/packages/NotifyGen |
| Stars / forks | GitHub About |
| Discussion threads | Discussions tab |
| Inbound issues | Issues |
| Docs site traffic | GitHub Pages / referrers |
