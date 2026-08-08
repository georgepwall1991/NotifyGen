# Design: transitive `[NotifyAlso]` dependencies and cycle diagnostics

## Decision

Deepen the existing `[NotifyAlso]` feature without adding an attribute:
calculate the transitive closure of generated-property dependency edges, emit
one notification per reachable property, and report a compile-time diagnostic
for cycles. Direct field-to-manual-property behavior remains unchanged.

```csharp
[Notify]
public partial class OrderViewModel
{
    [NotifyAlso(nameof(DisplayName))]
    private string _name = "";

    [NotifyAlso(nameof(SearchText))]
    private string _displayName = "";

    private string _searchText = "";
}
```

Changing `Name` now raises `Name`, `DisplayName`, and `SearchText` exactly once
(each reachable generated property is deduplicated). A cycle such as
`Name -> DisplayName -> Name` produces `NOTIFY008` at the offending
`[NotifyAlso]` attribute, with the participating property names in the message.
No new attribute and no runtime graph are introduced.

## Leading competitors

Fody's weaver already computes a transitive closure in
`PropertyDataWalker.GetFullDependencies` / `ComputeDependenciesRecursively`:
[upstream source](https://github.com/Fody/PropertyChanged/blob/14a0870a7afcd44f334b4e122f3fb189106d16fa/PropertyChanged.Fody/PropertyDataWalker.cs#L91-L127).
Its `DependsOn` reader builds edges from compiled property definitions:
[reader](https://github.com/Fody/PropertyChanged/blob/14a0870a7afcd44f334b4e122f3fb189106d16fa/PropertyChanged.Fody/DependsOnDataAttributeReader.cs#L24-L64).
PropertyChanged.SourceGenerator also recursively discovers computed-property
references and bounds recursion with a visited set
([source](https://github.com/canton7/PropertyChanged.SourceGenerator/blob/b51c349df7611edf0224645370fbf20a1d402eaa/src/PropertyChanged.SourceGenerator/Analysis/Analyser_DependsOn.cs#L102-L181)).

## Concrete limitations and our win

Fody's closure prevents unbounded recursion, but a cycle is not surfaced as a
source diagnostic at the attribute; the user receives no explanation of which
edge made the graph cyclic. The source-generator competitor's recursion guard
has the same limitation for its auto-analysis. Both competitors work with
broader property models, but neither gives NotifyGen's explicit attribute edge
an actionable cycle diagnostic.

**The case where ours wins:** a large generated view model with a typo or
refactor that creates `A -> B -> A` through explicit `[NotifyAlso]` attributes.
NotifyGen reports `NOTIFY008` on the attribute before a consumer ships a
silently bounded graph, while valid `A -> B -> C` chains compile to a
source-visible, deduplicated setter. The committed analyzer test proves the
location/severity/message; the committed generator/runtime tests prove the
three-node event sequence and diamond deduplication.

This belongs in an INPC generator because the graph maps property changes to
`INotifyPropertyChanged` event names and is resolved entirely from the
attribute/property model at compile time. It does not add commands, messaging,
DI, validation, or a runtime dependency.

## Compatibility and constraints

- Existing direct edges retain their order before newly discovered transitive
  edges; each property name is emitted once. This keeps old simple cases stable
  while defining deterministic depth-first traversal for new chains.
- Cycles are a build error (`NOTIFY008`). Generation still uses a visited set so
  an IDE can display bounded generated source while the diagnostic explains the
  invalid graph; valid projects are unaffected.
- Manual computed properties are leaves: NotifyGen follows edges only when the
  target is another generated property with `[NotifyAlso]` metadata.
- The public attribute surface is additive only. Release notes document the
  new transitive behavior and the new cycle error so projects that previously
  relied on silently bounded cycles can fix them explicitly.
