# Cycle 3 design: typed post-change hooks

## Contract

Every generated property currently declares a parameterless partial hook:

```csharp
partial void OnNameChanged();
```

Cycle 3 adds an overload carrying the values already available in the setter:

```csharp
partial void OnNameChanged(string oldValue, string newValue);
```

The generated setter calls the typed hook after assignment and after the normal
`OnPropertyChanged`/`NotifyAlso` notifications, preserving the existing
parameterless hook call. Implementers can provide either, both, or neither;
partial methods remain optional and erased when not implemented. If an ordinary
method already has the exact typed signature, its declaration is reused instead
of generating a duplicate partial declaration; accessible inherited methods are
included, and nullable/dynamic annotations do not change the metadata signature.
The typed hook is generated for
field mode and C# 14 partial-property mode, with the exact property type
(including nullable annotations) used by the generated setter.

This is an INPC callback surface only. It does not change `PropertyChangedEventArgs`,
add a messaging/runtime dependency, infer old values for child events, or alter
the equality guard. Same-value assignments call neither hook, as before.

## Ordering

For a changed source value, the generated setter order is:

1. `OnPropertyChanging()` when `ImplementChanging = true`;
2. `On{Name}Changing(oldValue, newValue)`;
3. backing-field assignment;
4. subscription replacement when child tracking is enabled;
5. `OnPropertyChanged()` and direct dependent notifications;
6. parameterless `On{Name}Changed()`;
7. typed `On{Name}Changed(oldValue, newValue)`.

The two post-change overloads are intentionally consecutive and both observe
the assigned value. Existing source-side/transitive `NotifyAlso` behavior is
unchanged; target-side graph declarations are already normalized into the same
`AlsoNotify` list.

## Safety and proof

The generator emits typed partial declarations from the field/property symbol,
not source text. This preserves generic, nullable, tuple, unsafe, and escaped
type handling already covered by property generation. Tests compile implemented
and unimplemented overloads, verify runtime old/new values and order, verify
same-value suppression, and cover field/partial-property modes. No new
attribute or runtime reference is required.
