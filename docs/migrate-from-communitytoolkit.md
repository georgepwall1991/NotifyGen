# Migrate from CommunityToolkit.Mvvm `[ObservableProperty]`

NotifyGen is a **narrow INPC source generator**. Keep CommunityToolkit.Mvvm for `RelayCommand`, messengers, and DI helpers — use NotifyGen for properties.

## Migration checklist

1. **Keep CT packages** — do not remove `CommunityToolkit.Mvvm` if you use commands, messengers, or DI helpers.
2. **Add NotifyGen** — `dotnet add package NotifyGen`.
3. **Pick one ViewModel** — migrate a single type first (not the whole solution).
4. **Lightbulb / Fix All** — **NOTIFY023** (no `[Notify]` yet) or **NOTIFY022** (already `[Notify]`) converts the type. Do not delete `[ObservableProperty]` by hand.
5. **What the fixer does** — adds `[Notify]` + `partial`, replaces `[ObservableProperty]` with `[NotifyProperty]` (opt-in), maps `[NotifyPropertyChangedFor]` to `[NotifyComputed]` on get-only dependents, leaves `RelayCommand` alone.
6. **Unmarked `_fields` stay private** — `_logger`, `_disposed`, and other underscore fields without `[NotifyProperty]` are not published.
7. **Map leftover dependents by hand only if needed** — `[NotifyComputed]` on the derived property, or `[NotifyAlso]` on a source.
8. **Map commands** — keep `[NotifyCanExecuteChangedFor]` pointing at CT `IRelayCommand` properties.
9. **Base class** — `ObservableObject` is optional. Prefer your own INPC host; NotifyGen reuses accessible `OnPropertyChanged`.
10. **Leave out of scope on CT** — messenger recipients, validation/INDEI, navigation.
11. **Verify** — build, bind once in UI, then migrate the next ViewModel.

Paste-friendly status for Discussions / Stack Overflow:

```text
Migrating VM: ________
Kept on CT: RelayCommand / Messenger / other: ________
Blocked on: ________
```

## Attribute map

| CommunityToolkit | NotifyGen |
|------------------|-----------|
| `[ObservableProperty]` on a field | `[Notify]` on the class + `[NotifyProperty]` on the field (opt-in; unmarked `_logger` stays private) |
| C# partial properties with CT | C# 14 incomplete partial properties under `[Notify]` |
| `[NotifyPropertyChangedFor(nameof(X))]` | `[NotifyComputed]` on `X`, or `[NotifyAlso(nameof(X))]` on each source |
| Proposed `[ComputedProperty]` (CT #1175) | `[NotifyComputed]` / `[NotifyComputed(nameof(...))]` |
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

// NotifyGen (or accept the NOTIFY023 code-fix)
[Notify]
public partial class Person
{
    [NotifyProperty]
    [NotifyAlso(nameof(FullName))]
    private string _firstName;

    private readonly ILogger _logger; // stays private
}
```

### Commands (hybrid — paste this)

```csharp
// Before: CT owns properties + commands
public partial class EditorViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() { }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);
}

// After: NotifyGen owns properties; CT owns RelayCommand
[Notify]
public partial class EditorViewModel
{
    [NotifyAlso(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = "";

    public bool CanSave => !string.IsNullOrWhiteSpace(Title);

    public IRelayCommand SaveCommand { get; }

    public EditorViewModel()
    {
        SaveCommand = new RelayCommand(Save, () => CanSave);
    }

    private void Save() { }
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

See the Avalonia UI hybrid sample: [`samples/NotifyGen.HybridSample`](../samples/NotifyGen.HybridSample).

## What NotifyGen will not become

Messenger, DI, navigation, collection item bubbling, or a validation runtime. Those stay with CT or your app code.

## Discussion prompts

- **I migrated X** — Show and tell template
- **Hybrid setup help** — Migration template
- **Why not a full MVVM toolkit?** — Q&A template
