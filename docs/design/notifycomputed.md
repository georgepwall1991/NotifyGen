# Design: `[NotifyComputed]` (2.1)

## Contract

`[NotifyComputed]` marks a read-only computed property as a notification target.
NotifyGen does **not** generate the property. It adds reverse edges into the
existing `AlsoNotify` graph so generated setters raise `PropertyChanged` for
the computed name.

```csharp
[Notify]
public partial class Person
{
    private string _firstName;
    private string _lastName;

    [NotifyComputed]
    public string FullName => $"{FirstName} {LastName}";

    [NotifyComputed(nameof(FirstName), nameof(LastName))]
    public string Initials => string.Concat(FirstName.AsSpan(0, 1), LastName.AsSpan(0, 1));
}
```

- Parameterless: walk a bounded getter.
- `params string[]`: skip the walk and use those names.
- Merge with `[NotifyAlso]` / `NotifyFrom`. Deduped transitive closure is unchanged.
- Dependents still get `PropertyChanged` only (not `PropertyChanging`).
- Same-value equality guards suppress the whole fan-out.
- No runtime package, no cached computed backing field, no unmarked-getter crawl.

## Allow-list (fail-closed)

Syntax walk of the getter (expression-bodied or `get` block):

Accepted: this-property identifiers (including names that only exist after
generation), eligible underscore fields mapped to their property name,
`this.Name`, interpolation, literals, `default`, parentheses, casts, unary /
binary / conditional / coalesce, element access on an accepted member,
`return` inside a block.

Rejected (**NOTIFY021**, no inferred edges): method calls, LINQ, object
creation, `await`, assignment, foreign members (`Address.City`).

Empty inferred or empty explicit list → **NOTIFY018**.
`[NotifyComputed]` on a generated incomplete partial property → **NOTIFY019**.
A property with a setter → **NOTIFY020** (no edges).
Unknown explicit names reuse **NOTIFY003**.
A known source that is neither generated nor `[NotifyComputed]` reuses **NOTIFY011** (NotifyGen cannot observe a handwritten setter).
Cycles including computed nodes reuse **NOTIFY008**.
A block getter that declares locals is fail-closed (**NOTIFY021**), so a local that shadows a generated property name does not create an edge.
Split partial properties keep scanning declaration parts until a getter body is found.
`[NotifyAlso]` / `NotifyFrom` edges whose source is a `[NotifyComputed]` property join the same graph.

Generated properties are absent from the input compilation, so the walker uses
syntax plus the known generated-name set rather than bound `IPropertySymbol`s.

## Graph

Computed edges are reversed (`source → computed`). Computed-to-computed sources
become phantom `FieldInfo` nodes (`IsComputedTarget`) so `ExpandAlsoNotify` and
cycle detection see them. Emission skips phantoms (no setter, no hooks).

## Proof

`NotifyComputedTests` covers interpolation, field mapping, block/`this`,
transitive flatten, cycle, fail-closed method calls, explicit LINQ DependsOn,
unknown names, empty getters, generated partials, equality guards, merge with
`NotifyAlso`, and computed-vs-explicit `AlsoNotify` name parity.
