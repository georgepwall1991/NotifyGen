# NotifyGen 2.0 — zero-runtime INPC that plays with CommunityToolkit

NotifyGen is a narrow Roslyn source generator for `INotifyPropertyChanged`. One `[Notify]` on a partial class turns underscore fields (or C# 14 incomplete partial properties) into equality-guarded, debuggable properties — with no runtime package and no required `ObservableObject` base.

## What 2.0 adds

- **Accessor-target metadata:** write `[property: JsonPropertyName("x")]`, `[get: Obsolete]`, or `[set: MemberNotNull(...)]` on fields; NotifyGen emits them on the generated property/accessors and suppresses CS0657/CS0658.
- **Adoption assets:** WPF, Avalonia, and MAUI samples for host INPC, child/collection notify, and suppressable bulk load; a hybrid UI sample that pairs NotifyGen properties with CommunityToolkit `RelayCommand`.
- **Docs:** migrate-from-CommunityToolkit guide, diagnostics catalog, and a browsable docs site with generated-code before/after.

## Recommended stack

Use **NotifyGen for properties** and **CommunityToolkit.Mvvm for commands**:

```csharp
[Notify]
public partial class EditorViewModel
{
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = "";

    public IRelayCommand SaveCommand { get; }
}
```

## Migrating from `[ObservableProperty]`?

Keep CT for commands and messengers. Swap property generation in one ViewModel first — attribute map and checklist:

→ [Migrate from CommunityToolkit](migrate-from-communitytoolkit.md)

## Get it

```bash
dotnet add package NotifyGen
```

Repo: https://github.com/georgepwall1991/NotifyGen  
NuGet: https://www.nuget.org/packages/NotifyGen/  
Samples: https://github.com/georgepwall1991/NotifyGen/tree/master/samples  
Docs: https://github.com/georgepwall1991/NotifyGen/blob/master/docs/features.md

## Channel checklist

See [launch-checklist.md](launch-checklist.md) and [launch-channels.md](launch-channels.md).
