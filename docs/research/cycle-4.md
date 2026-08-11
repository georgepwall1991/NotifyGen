# Cycle 4 research, evidence, and selection

Date of snapshot: 2026-08-09. NotifyGen `v1.9.0` has shipped cycles 1–3 (`d0fb621` / `v1.9.0`). This cycle uses competitor source/tests and independent demand records; README prose is not treated as proof.

## Capability: host existing INPC implementations

CommunityToolkit issue [#620](https://github.com/CommunityToolkit/dotnet/issues/620) requests using generator attributes on a derived type whose base already owns `INotifyPropertyChanged`, its event, and its notification helper: **15 raw reactions (14 +1, 1 eyes), 6 comments**. The independent Stack Overflow question [#9053764](https://stackoverflow.com/questions/9053764/mvvm-inotifypropertychanged-conflict-with-base-class-propertychange) has **score 2, 6,049 views, and 3 answers**. NotifyGen already avoids emitting a duplicate interface/event when `AllInterfaces` contains INPC, but generated setters still assume a callable `OnPropertyChanged(string?)`; a base may instead expose an event/helper shape that is inaccessible or takes `PropertyChangedEventArgs`.

Selected bounded scope: for `[Notify]` classes that already implement or inherit INPC, discover a compatible accessible host invoker (`OnPropertyChanged(string?)` or `OnPropertyChanged(PropertyChangedEventArgs)`). Reuse it and omit duplicate interface/event/helper generation. If no compatible accessible invoker exists, report a diagnostic rather than shipping uncompilable generated code. No reflection, runtime helper, or automatic base mutation.

## Deepening: explicit collection membership notifications

Cycle 2 shipped opt-in direct child INPC tracking, but not collection membership changes. Independent Stack Overflow demand includes [#77550952](https://stackoverflow.com/questions/77550952/using-notifypropertychangedfor-source-generator-on-observablecollection-is-not-updating-ui), where collection-backed dependent notifications fail (**score 3, 979 views, 1 answer**), and [#13257888](https://stackoverflow.com/questions/13257888/is-there-a-way-to-trigger-some-kind-of-onpropertychanged-event-for-a-computed-property-that-uses-a-property-of-a-child-entity-in-a-collection), (**score 4, 2,264 views, 2 answers**). Fody discussion [#221](https://github.com/Fody/PropertyChanged/issues/221) has **0 reactions but 10 comments**, implementation evidence only.

Selected bounded scope: add `NotifyOnCollectionChanged = true` to an existing `NotifyAlso` declaration. For a generated collection property whose runtime value implements `INotifyCollectionChanged`, subscribe/unsubscribe directly to `CollectionChanged` and raise the declared dependent targets once for Add/Remove/Replace/Move/Reset. Replacement and null handling must detach stale collections. Do not traverse item property changes, infer getter dependencies, generate collection proxies, or add external runtime dependencies; item bubbling remains a future explicit slice.

## Rejected alternatives

- Automatic getter inference: PSG issue [#36](https://github.com/canton7/PropertyChanged.SourceGenerator/issues/36), **0 reactions/2 comments**; ambiguous and duplicates cycle-3 explicit graph semantics.
- Full POCO/entity graph proxies: CT [#1069](https://github.com/CommunityToolkit/dotnet/issues/1069), only **2 +1/0 comments**; runtime-heavy.
- Generated validation/`INotifyDataErrorInfo`: CT [#788](https://github.com/CommunityToolkit/dotnet/issues/788), **2 +1/0 comments**; framework/runtime scope. FluentValidation integration [#597](https://github.com/CommunityToolkit/dotnet/issues/597) has only **3 +1/1 comment** and adds an external dependency.
- Accessor-target metadata was evaluated but requires a diagnostic suppressor and syntax reconstruction because C# reports `[property:]`/`[get:]`/`[set:]` on fields as CS0657/CS0658; it is deferred rather than under-scoped.
- Comparer customization: Fody [#162](https://github.com/Fody/PropertyChanged/issues/162), **0 reactions/21 comments**; evidence insufficient.
- Collection item-property bubbling: ReactiveUI [#3302](https://github.com/reactiveui/ReactiveUI/issues/3302), **0 reactions**; deferred beyond membership-only changes.

## Evidence gates

Implementation must include analyzer diagnostics for incompatible host invokers and unsupported collection opt-in, compile/Emit tests on net8/net9/net10, runtime event-order/replacement tests, reflection checks, no-runtime-dependency packaging checks, ordering/deduplication tests, and adversarial negative fixtures. Release is gated on CI, package version `1.10.0`, tag/release `v1.10.0`, and NuGet availability.
