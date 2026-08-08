# Design: partial-property generation

## Decision

Extend the existing `[Notify]` class generator to accept incomplete C# 14
partial properties in addition to its existing eligible fields. This adds no
attribute. Existing field declarations keep exactly their current generated
shape.

Example:

```csharp
[Notify]
public partial class PlainEntity : FrameworkEntity
{
    [NotifyAlso(nameof(DisplayName))]
    public partial string Name { get; set; }

    public string DisplayName => Name.Trim();
}
```

The generated declaration implements the partial property using the C#
`field` contextual backing field and applies the same equality guard,
`OnNameChanging(oldValue, newValue)`, assignment, `OnPropertyChanged`,
`NotifyAlso`, and `OnNameChanged` order as field-backed properties. The
property's declared accessibility and setter accessibility are retained.
`[NotifyAlso]` is extended to `Field | Property` so the existing dependency
attribute works on this new member kind; this is additive and requires no
migration for field users.

## Leading competitor: CommunityToolkit.Mvvm

CommunityToolkit's `ObservablePropertyGenerator` accepts fields and partial
properties, checks C# 14/preview and Roslyn support, and emits an implementation
whose getter/setter use `field`. Its source and tests are pinned in
[`docs/research/cycle-1.md`](../research/cycle-1.md), especially:

- [candidate checks and partial-symbol validation](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm.SourceGenerators/ComponentModel/ObservablePropertyGenerator.Execute.cs#L40-L167)
- [partial-property implementation](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm.SourceGenerators/ComponentModel/ObservablePropertyGenerator.Execute.cs#L1168-L1360)
- [partial-property parity tests](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/tests/CommunityToolkit.Mvvm.Roslyn4120.UnitTests/Test_ObservablePropertyAttribute_PartialProperties.cs#L1070-L1138)

## Concrete limitations and our win

1. The competitor requires the CommunityToolkit runtime composition contract:
   `ObservableObject`, `[ObservableObject]`, or
   `[INotifyPropertyChanged]`. A consumer with an existing arbitrary base class
   cannot simply use the generated property without adapting that base.
2. Its partial-property path requires the C# 14/preview `field` toolchain and
   its package's runtime/framework surface. NotifyGen's package remains a
   source-only, zero-runtime-dependency INPC generator.
3. CommunityToolkit's partial-property generated implementation uses its own
   hook/recipient/validation surface. A consumer that wants NotifyGen's
   existing old/new partial hooks and explicit `[NotifyAlso]` semantics would
   otherwise have to translate APIs.

**The case where ours wins:** a library type already derives from
`FrameworkEntity` (which cannot derive from `ObservableObject`) and must keep a
zero-runtime-dependency package. With `[Notify]` and a partial property, NotifyGen
can compile the type, add INPC, and retain the existing base class. The committed
`PartialPropertyTests` fixture is the proof for this exact case; it compiles the
output and asserts runtime notification and old/new hook order without a
CommunityToolkit reference.

This is an INPC-generator concern: backing storage, setter equality, event
ordering, and generated property hooks are all part of the property notification
contract. Commands, messaging, validation, navigation, and DI are not involved.

## Compatibility and constraints

- C# 14/preview is required only for the new syntax. Existing field mode stays
  available to older language versions.
- Existing generated public API for fields is unchanged.
- A partial property must be an incomplete `partial` property with both `get`
  and `set`, must be an ordinary instance property, and may use only ordinary
  accessibility modifiers. Static/indexer/ref/pointer/`required`/virtual/
  override/other modifier shapes are ignored rather than producing a duplicate
  implementation. If another partial declaration already supplies the
  implementation, NotifyGen does not emit a second one.
- If a generated name collides with another generated member or an existing
  manual property, `NOTIFY009` identifies the collision and generation is
  withheld instead of throwing from the generator.
- No runtime dependency is added. No existing attribute is removed or
  renamed. Release notes must mention the additive `[NotifyAlso]` target
  expansion and partial-property syntax requirement.
