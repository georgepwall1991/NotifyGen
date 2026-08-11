# Cycle 5 research, evidence, and selection

Date of snapshot: 2026-08-11. NotifyGen `v1.10.0` has shipped cycles 1–4. This cycle re-checks competitor source/tests and independent demand; README prose is not treated as proof.

## Capability: accessor-target metadata forwarding

CommunityToolkit reconstructs `[property:]`, `[get:]`, and `[set:]` attributes written on fields and emits them on the generated property or accessors. Collection happens by walking `AttributeListSyntax` targets because Roslyn does not bind invalid field targets into `AttributeData` ([attribute gathering](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm.SourceGenerators/ComponentModel/ObservablePropertyGenerator.Execute.cs)). Emission strips the target specifier and attaches attributes to the property, get accessor, or set accessor. A diagnostic suppressor clears CS0657 (`property:`) and CS0658 (`get:`/`set:`) on fields that carry the generator marker.

Demand cluster (strict records already cited in cycles 2–4):

- CommunityToolkit [#208](https://github.com/CommunityToolkit/dotnet/issues/208): **3 reactions**, **17 comments** (serialization/metadata on generated properties).
- CommunityToolkit [#854](https://github.com/CommunityToolkit/dotnet/issues/854): **0 reactions**, **4 comments** (supporting record for accessor placement).
- Stack Overflow [#75243887](https://stackoverflow.com/questions/75243887): score **2**, **1 answer**, **1,313 views**.
- Cycle 2 shipped untargeted property-targetable forwarding; cycles 3–4 deferred the remaining accessor-target syntax reconstruction as higher risk, not under-scoped.

Selected bounded scope: for eligible underscore fields, forward explicit `[property:]` / `[get:]` / `[set:]` attribute lists onto the generated property/accessors via syntax reconstruction, and suppress CS0657/CS0658 when `[Notify]` owns the containing type's field. Keep existing untargeted property-targetable forwarding. No validation runtime, no serialization dependency, no changes to partial-property attribute ownership.

## Rejected alternatives

- Automatic getter inference: PSG [#36](https://github.com/canton7/PropertyChanged.SourceGenerator/issues/36), **0 reactions**; ambiguous.
- Custom comparers: Fody [#162](https://github.com/Fody/PropertyChanged/issues/162), **0 reactions/21 comments**; evidence insufficient.
- Collection item-property bubbling: ReactiveUI [#3302](https://github.com/reactiveui/ReactiveUI/issues/3302), **0 reactions**; deferred.
- Generated `INotifyDataErrorInfo` / FluentValidation runtime: remains framework scope.

## Evidence gates

Implementation must include compile/Emit tests for property/get/set targets, reflection visibility where applicable, suppressor coverage for CS0657/CS0658, file-local skip behavior, interaction with existing untargeted forwarding, and no-runtime-dependency packaging. Release is gated on package version `2.0.0`, tag/release `v2.0.0`, and NuGet availability.
