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

Live site (branch `gh-pages`): https://georgepwall1991.github.io/NotifyGen/
