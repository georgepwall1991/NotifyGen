# Migrate from CommunityToolkit.Mvvm `[ObservableProperty]`

NotifyGen is a **narrow INPC source generator**. Keep CommunityToolkit.Mvvm for `RelayCommand`, messengers, and DI helpers — use NotifyGen for properties.

## Attribute map

| CommunityToolkit | NotifyGen |
|------------------|-----------|
| `[ObservableProperty]` on a field | `[Notify]` on the **class** + underscore field (`_name` → `Name`) |
| C# partial properties with CT | C# 14 incomplete partial properties under `[Notify]` |
| `[NotifyPropertyChangedFor(nameof(X))]` | `[NotifyAlso(nameof(X))]` |
| Target-side depends-on (CT PR style) | `[NotifyAlso(nameof(Source), NotifyFrom = true)]` on the dependent |
| Child object refresh | `[NotifyAlso(nameof(X), NotifyOnSubPropertyChanged = true)]` |
| Collection membership refresh | `[NotifyAlso(nameof(X), NotifyOnCollectionChanged = true)]` |
| `[NotifyCanExecuteChangedFor]` | Same name — works with CT `IRelayCommand` |
| `[NotifyPropertyChangedRecipients]` / messenger | Out of scope — keep CT |
| Validation / INDEI generation | Out of scope — forward DataAnnotations; validate yourself |
| Base `ObservableObject` | Optional. Prefer your own INPC host; NotifyGen reuses accessible `OnPropertyChanged` |

## Before / after

```csharp
// CommunityToolkit
public partial class Person : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullName))]
    private string _firstName;
}

// NotifyGen
[Notify]
public partial class Person
{
    [NotifyAlso(nameof(FullName))]
    private string _firstName;
}
```

## Host / base-class migration

If a base type already owns `INotifyPropertyChanged` and an accessible `OnPropertyChanged(string?)` or `OnPropertyChanged(PropertyChangedEventArgs)`:

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged { /* event + OnPropertyChanged */ }

[Notify]
public partial class EditorViewModel : ViewModelBase
{
    private string _title;
}
```

NotifyGen reuses the host invoker and does not emit a second event. Incompatible hosts report **NOTIFY013** / **NOTIFY017**.

## Metadata

- Untargeted attributes valid on properties forward automatically (Cycle 2).
- Explicit `[property:]` / `[get:]` / `[set:]` on fields forward onto the generated property/accessors (Cycle 5 / 2.0). CS0657/CS0658 are suppressed for `[Notify]` fields.

## Commands

```csharp
[Notify]
public partial class EditorViewModel
{
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title;

    public IRelayCommand SaveCommand { get; } // CommunityToolkit.Mvvm
}
```

See [`samples/NotifyGen.HybridSample`](../samples/NotifyGen.HybridSample).

## What NotifyGen will not become

Messenger, DI, navigation, collection item bubbling, or a validation runtime. Those stay with CT or your app code.
