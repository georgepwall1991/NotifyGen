# Docs site

Browsable documentation for NotifyGen (DocFX).

## Local

```bash
dotnet tool install -g docfx   # or update
docfx build docs/site/docfx.json
# open docs/site/_site/index.html
```

## GitHub Pages

Workflow: [`.github/workflows/docs.yml`](../../.github/workflows/docs.yml).

Maintainer: enable **Settings → Pages → Source: GitHub Actions**, then merge to `master` (or run the workflow manually).

Published URL (once Pages is on): https://georgepwall1991.github.io/NotifyGen/
