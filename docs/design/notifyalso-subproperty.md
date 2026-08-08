# Cycle 2 design: opt-in child-property notifications

## Contract

`NotifyAlsoAttribute` gains one optional property:

```csharp
[NotifyAlso(nameof(DisplayName), NotifyOnSubPropertyChanged = true)]
private Address? _address;
```

When the source member is generated, NotifyGen subscribes to the source
property value if it implements `System.ComponentModel.INotifyPropertyChanged`.
After attachment, any child `PropertyChanged` event raises `PropertyChanged`
for the direct `DisplayName` target. The subscription is replaced when the generated source
property is assigned and removed when it is replaced with `null` or another
child. The same behavior is available for C# 14 partial-property definitions.

The option is explicit and direct: it does not inspect getter expressions,
walk arbitrary object graphs, subscribe to collections, or infer nested paths.
Every child event raises the opted-in direct target because the existing
attribute has no child-property-name argument. Existing flat `[NotifyAlso]`
behavior and transitive same-type closure remain unchanged.

## Generated mechanics

- Generated state stores only a BCL `INotifyPropertyChanged` interface reference,
  an initialization flag, and one handler per source property.
- The generated getter lazily attaches to an initialized backing value, which
  also handles field/partial-property initializers without constructor rewriting.
  A child event that occurs before the generated source property is first
  accessed or assigned cannot be observed: source generators cannot inject an
  initialization call into every user constructor. Call the generated property
  once (as a binding normally does) before relying on child events.
- The setter ensures the old value is observed, retains the equality guard,
  assigns the new value, unsubscribes the old child, and subscribes the new one.
- Child handlers call the existing `OnPropertyChanged` path, so suppression,
  event handlers, and arbitrary existing base implementations retain their
  normal semantics.
- `NOTIFY010` warns at the attribute when the source type is not a reference
  value implementing `INotifyPropertyChanged`; generation remains safe and
  simply observes no child.

## Safety and non-goals

Null replacement is null-safe, old children are detached, duplicate source
assignment does not produce duplicate subscriptions, and no external runtime
package is introduced. Nested subscriptions are intentionally not generated
for unsupported pointer/function-pointer fields. No commands, messaging,
validation, DI, navigation, or automatic graph traversal are included.
