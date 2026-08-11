# Cycle 5 design: accessor-target metadata forwarding

## Contract

For an eligible underscore field under `[Notify]`, NotifyGen forwards attributes declared with explicit attribute targets:

```csharp
[Notify]
public partial class Person
{
    [property: JsonPropertyName("display_name")]
    [get: Obsolete("Prefer DisplayLabel")]
    [set: System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_displayName))]
    private string _displayName = "";
}
```

Generated shape (targets stripped; placement preserved):

```csharp
[JsonPropertyName("display_name")]
public string DisplayName
{
    [Obsolete("Prefer DisplayLabel")]
    get => _displayName;
    [MemberNotNull("_displayName")]
    set { ... }
}
```

Untargeted attributes that already pass Cycle 2 `AttributeTargets.Property` checks continue to forward onto the property. Partial-property declarations already own their attributes and are unchanged.

## Collection

Roslyn does not populate `AttributeData` for invalid field targets, so collection walks each field's `AttributeListSyntax`:

- `property:` → property attribute list
- `get:` → get accessor attribute list
- `set:` → set accessor attribute list

Attribute types are resolved through `SemanticModel.GetSymbolInfo` with candidate-symbol fallback. Constructor and named arguments are rebuilt from constant/`typeof`/array operations so generated source does not depend on consumer `using` aliases.

Skip NotifyGen control attributes, file-local attribute types, and arguments that mention file-local types.

## Suppression

Ship a `DiagnosticSuppressor` in the generator assembly:

| Descriptor | Suppresses | When |
|------------|------------|------|
| NOTIFYSPR0001 | CS0657 | `property:` on a field in a `[Notify]` type |
| NOTIFYSPR0002 | CS0658 | `get:` / `set:` on a field in a `[Notify]` type |

## Safety boundaries

- Metadata-only: no validation interface, no serialization runtime, no new public attributes.
- Do not invent targets for untargeted attributes beyond Cycle 2 property forwarding.
- Prove Emit + reflection for property attributes; prove generated text placement for get/set attributes.
- Package suppressor with the existing analyzer DLL under `analyzers/dotnet/cs`.

## Non-goals

Item bubbling, custom comparers, getter inference, messenger/DI/commands, `INotifyDataErrorInfo`.
