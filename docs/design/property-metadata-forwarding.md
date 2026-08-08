# Cycle 2 design: property metadata forwarding

## Contract

For an eligible underscore field, NotifyGen forwards source attributes whose
`AttributeUsage` permits `AttributeTargets.Property` onto the generated
property. Constructor arguments and named arguments are serialized from
Roslyn's bound `AttributeData`, so `nameof`, aliases, and source-file `using`
directives do not have to remain in scope in the generated file.

```csharp
[Notify]
public partial class Person
{
    [Required(AllowEmptyStrings = true)]
    [JsonPropertyName(nameof(DisplayName))]
    private string _displayName;
}
```

The generated `DisplayName` property carries equivalent metadata. Field-only
attributes and NotifyGen control attributes are not copied. The capability is
metadata-only: it does not introduce validation, serialization, reflection, or
runtime dependencies. Partial-property declarations already own their property
attributes and remain unchanged.

## Safety boundaries

- Forward only attributes bound to a source field and valid on a property,
  including the effective `AttributeUsage` inherited by a derived attribute.
- Skip all `NotifyGen.*` control attributes to avoid duplicating generator
  control state into generated source.
- Skip file-local attribute types and attributes whose bound arguments mention a
  file-local type; such symbols cannot be named from the separate generated
  source file.
- Render attribute type names globally and render bound constructor/named values
  from `AttributeData`; do not paste unqualified source syntax into a generated
  file. Named argument keywords are escaped, and narrow integral constants keep
  their source type when boxed (for example, `(byte)7`).
- Preserve array, enum, `typeof`, string, character, and null constants where
  their referenced types are accessible.
- Prove both reflection-visible metadata and field-only filtering with emit and
  runtime tests.

## Non-goals

No generated validation interface, serialization runtime, accessor-target
forwarding, new attribute, command behavior, or changes to partial-property
attribute semantics are included in this item.
