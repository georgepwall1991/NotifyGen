# GitHub discoverability checklist

## Automated / in-repo (this phase)

- [x] Discussion templates under `.github/DISCUSSION_TEMPLATE/`
- [x] Launch checklist + channel copy: [launch-checklist.md](launch-checklist.md), [launch-channels.md](launch-channels.md)
- [x] Migration link elevated in root README
- [x] Docs site scaffold for GitHub Pages (`docs/site/`)

## Manual steps (token scopes for cloud agents may block repository metadata writes)

1. Replace topic typo `source-genertator` with `source-generator` (and keep `source-generators`).
2. Keep topics: `csharp`, `dotnet`, `mvvm`, `inotifypropertychanged`, `source-generators`, `roslyn`, `wpf`, `maui`, `avalonia`, `blazor`, `nuget`.
3. Enable Discussions and seed categories using the templates (Migration, Show and tell, Q&A). Point Migration at `docs/migrate-from-communitytoolkit.md`.
4. Pin the 2.0 launch Discussion (body from `docs/launch-post-2.0.md`).
5. Publish external posts from `docs/launch-channels.md`.

API attempt note (2026-08-11): `PUT /repos/.../topics` and `PATCH has_discussions` returned **403 Resource not accessible by integration** for the cloud agent token — maintainer must apply settings in the GitHub UI.
