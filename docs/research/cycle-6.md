# Cycle 6 research scaffold (demand-gated)

Date opened: 2026-08-11. NotifyGen `v2.0.0` has shipped cycles 1–5 plus adoption assets.

## Gate — do not implement until signal exists

Open this cycle for **implementation** only after ~2–4 weeks of post-launch signal from:

| Source | What counts |
|--------|-------------|
| GitHub Discussions | Migration / Show-and-tell / Q&A threads with concrete asks |
| Issues | Repro-backed feature requests (not hypothetical checklists) |
| NuGet / SO | Repeat mentions of the same gap |
| Samples feedback | MAUI / Hybrid / Avalonia users hitting a documented wall |

Until then this file is a **watchlist**, not a roadmap commitment.

## Candidate backlog (previously weak evidence)

Revisit only if demand hardens. Cite new reaction counts / issues before selecting.

| Candidate | Prior evidence | Status |
|-----------|----------------|--------|
| Collection **item-property** bubbling | ReactiveUI #3302 — 0 reactions (cycle 4/5) | Deferred |
| Custom equality comparers | Fody #162 — insufficient (cycle 5) | Deferred |
| Automatic getter inference | PSG #36 — 0 reactions (cycle 5) | Deferred |
| Generated `INotifyDataErrorInfo` / FluentValidation | Framework scope | Rejected |

## Explicit non-goals (unchanged)

Messenger, DI, navigation, full MVVM toolkit surface.

## Parallel engineering (this phase — no demand gate)

Shipped alongside the adoption push:

- Split monolithic `NotifyGenerator` into partials: `Discovery`, `Hooks`, `Attributes`, `Formatting`, `Emission`, `Subscriptions`
- Refresh `.complexity-log.md` for the new file layout
- Keep CI coverage ≥80%; Stryker remains advisory until signal justifies stricter gates

## Selection template (when gated)

```md
## Capability: <name>
Demand cluster:
- <link> — N reactions, N comments
Selected bounded scope: ...
Rejected alternatives: ...
Evidence gates (tests): ...
```
