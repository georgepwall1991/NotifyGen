# NotifyGen diagnostics

| Id | Severity | Meaning | Typical fix |
|----|----------|---------|-------------|
| **NOTIFY001** | Error | `[Notify]` type is not `partial` | Add `partial` |
| **NOTIFY002** | Warning | No eligible underscore fields / incomplete partial properties | Add `_field` members or incomplete partial properties; code fix prefixes private instance fields with `_` |
| **NOTIFY003** | Warning | `[NotifyAlso]` names an unknown property | Fix the name or declare the dependent; code fix offers the closest known property when the typo is near |
| **NOTIFY004** | Info | Static/const field cannot generate a property | Remove static/const or add `[NotifyIgnore]` |
| **NOTIFY005** | Info | Readonly field cannot generate a setter | Remove `readonly` or add `[NotifyIgnore]` |
| **NOTIFY006** | Error | Containing type of a nested `[Notify]` type is not partial | Make every container `partial` |
| **NOTIFY007** | Error | File-local type cannot be extended from generated source | Remove `file` accessibility |
| **NOTIFY008** | Error | `[NotifyAlso]` dependency cycle | Break the cycle |
| **NOTIFY009** | Error | Two members generate the same property name | Rename one member / `[NotifyName]` |
| **NOTIFY010** | Warning | `NotifyOnSubPropertyChanged` on a non-INPC reference | Use a reference type implementing `INotifyPropertyChanged` |
| **NOTIFY011** | Warning | Target-side `NotifyFrom` / `[NotifyComputed]` names a non-generated, non-computed source | Point at a generated property or another `[NotifyComputed]` member |
| **NOTIFY012** | Warning | Target-side `NotifyAlso` cannot use `NotifyOnSubPropertyChanged` | Put child tracking on the source member |
| **NOTIFY013** | Error | Existing INPC host has no callable `OnPropertyChanged` invoker | Add an accessible string/EventArgs invoker |
| **NOTIFY014** | Warning | Target-side `NotifyAlso` cannot use `NotifyOnCollectionChanged` | Put collection tracking on the source member |
| **NOTIFY015** | Warning | `NotifyOnCollectionChanged` requires a reference value | Use a reference-typed collection |
| **NOTIFY016** | Error | Generated property name is not a valid identifier | Fix `[NotifyName]` / field naming |
| **NOTIFY017** | Error | Existing INPC-changing host has no callable `OnPropertyChanging` invoker | Add an accessible changing invoker |
| **NOTIFY018** | Warning | `[NotifyComputed]` has no recognizable this-property dependencies | Read generated properties in the getter or pass explicit names |
| **NOTIFY019** | Warning | `[NotifyComputed]` is on a generated source member | Use it on a read-only computed property |
| **NOTIFY020** | Warning | `[NotifyComputed]` is not a get-only instance property | Use a non-static, non-indexer, get-only computed property |
| **NOTIFY021** | Warning | `[NotifyComputed]` getter is outside the allow-list | Pass explicit `DependsOn` names (LINQ / helpers) |
| **NOTIFY022** | Warning | `[Notify]` type still has CommunityToolkit `[ObservableProperty]` / `[NotifyPropertyChangedFor]` | Code-fix converts to `[NotifyProperty]` / `[NotifyComputed]` |
| **NOTIFY023** | Warning | Type has CommunityToolkit property attributes and no `[Notify]` | Code-fix adds `[Notify]` + opt-in `[NotifyProperty]` and leaves unmarked `_fields` private |

## Suppressions

| Id | Suppresses | When |
|----|------------|------|
| **NOTIFYSPR0001** | CS0657 | `[property: …]` on a field inside a `[Notify]` type |
| **NOTIFYSPR0002** | CS0658 | `[get: …]` / `[set: …]` on a field inside a `[Notify]` type |

Code fixes: **NOTIFY001** / **NOTIFY006** offer **Make type partial**. **NOTIFY002** prefixes private instance fields with `_`. **NOTIFY003** replaces a nearby unknown `[NotifyAlso]` name. **NOTIFY022** / **NOTIFY023** convert CommunityToolkit property attributes to NotifyGen opt-in members.
