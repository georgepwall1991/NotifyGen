# NotifyGen 2.0 — zero-runtime INPC that plays with CommunityToolkit

NotifyGen is a narrow Roslyn source generator for `INotifyPropertyChanged`. One `[Notify]` on a partial class turns underscore fields (or C# 14 incomplete partial properties) into equality-guarded, debuggable properties — with no runtime package and no required `ObservableObject` base.

## What 2.0 adds

- **Accessor-target metadata:** write `[property: JsonPropertyName("x")]`, `[get: Obsolete]`, or `[set: MemberNotNull(...)]` on fields; NotifyGen emits them on the generated property/accessors and suppresses CS0657/CS0658.
- **Adoption assets:** WPF + Avalonia samples for host INPC, child/collection notify, and suppressable bulk load; a hybrid sample that pairs NotifyGen properties with CommunityToolkit `RelayCommand`.
- **Docs:** migrate-from-CommunityToolkit guide and a full diagnostics catalog.

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

## Get it

```bash
dotnet add package NotifyGen
```

Repo: https://github.com/georgepwall1991/NotifyGen  
NuGet: https://www.nuget.org/packages/NotifyGen/
