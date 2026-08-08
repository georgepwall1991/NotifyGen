# Cycle 1 research, evidence, and selection

Date of snapshot: 2026-08-08. NotifyGen was at `75a80c0` / `v1.6.0` on the
`cycle/1-inpc-evidence` branch. The competitor repositories below were cloned
and inspected from generator/runtime source and tests. READMEs were not used
as source evidence.

## CommunityToolkit.Mvvm

**Source pin.** `CommunityToolkit/dotnet` main at
`b135626dd54d33b8f05f2ff31591592c004aa848` (v8.4.2). The generator's
candidate filter accepts fields and partial properties, gates the latter on
C# 14/preview and newer Roslyn, and validates incomplete partial definitions:
[generator source](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm.SourceGenerators/ComponentModel/ObservablePropertyGenerator.Execute.cs#L40-L167).
The attribute and its containing-type requirements are in
[ObservablePropertyAttribute.cs](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm/ComponentModel/Attributes/ObservablePropertyAttribute.cs#L1-L73).
The partial-property parity fixture is
[Test_ObservablePropertyAttribute_PartialProperties.cs](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/tests/CommunityToolkit.Mvvm.Roslyn4120.UnitTests/Test_ObservablePropertyAttribute_PartialProperties.cs#L1070-L1138).

**Generates that NotifyGen does not.** It supports `[ObservableProperty]` on a
partial property, uses the C# `field` contextual backing field, and preserves
property/accessor accessibility and selected modifiers
([generator](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm.SourceGenerators/ComponentModel/ObservablePropertyGenerator.Execute.cs#L992-L1097)).
Its tests cover value and old/new partial hooks, validation/data-annotation
forwarding, command invalidation, recipient broadcasts, `MemberNotNull`, and
cached event args ([partial-property tests](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/tests/CommunityToolkit.Mvvm.Roslyn4120.UnitTests/Test_ObservablePropertyAttribute_PartialProperties.cs#L430-L514),
[setter generation](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm.SourceGenerators/ComponentModel/ObservablePropertyGenerator.Execute.cs#L1188-L1336)). NotifyGen currently has field-only generation and no attribute-forwarding or recipient/validation generation.

**Generates worse than NotifyGen.** Partial properties require the newer
preview/C# 14 toolchain; the attribute source documents the requirement
([source](https://github.com/CommunityToolkit/dotnet/blob/b135626dd54d33b8f05f2ff31591592c004aa848/src/CommunityToolkit.Mvvm/ComponentModel/Attributes/ObservablePropertyAttribute.cs#L37-L72)).
The generated path requires `ObservableObject`, `[ObservableObject]`, or
`[INotifyPropertyChanged]` composition. That is a runtime/framework choice,
whereas NotifyGen's `[Notify]` emits the interface on an otherwise arbitrary
partial class. The current generator also emits `<inheritdoc/>` for partial
properties, which users report obscures documentation (issue #1068, raw
reactions **8 total: +1 8**, 1 comment):
[issue #1068](https://github.com/CommunityToolkit/dotnet/issues/1068).

**User-reliant behaviour our attribute surface cannot express.** A user cannot
ask NotifyGen to forward `property:`, `get:`, `set:`, or validation metadata;
request `ValidateProperty`/`INotifyDataErrorInfo`; broadcast old/new values to
messenger recipients; request value-only changed hooks; or declare exact
property/getter modifiers. Those are generator features in the cited source,
not evidence that NotifyGen should become an MVVM framework.

## Fody.PropertyChanged

**Source pin.** `Fody/PropertyChanged` at
`14a0870a7afcd44f334b4e122f3fb189106d16fa` (2026-07-09). The inspected
implementation is IL weaving: `TypeProcessor` runs `PropertyWeaver` and
`EqualityCheckWeaver` over compiled setters
([TypeProcessor.cs](https://github.com/Fody/PropertyChanged/blob/14a0870a7afcd44f334b4e122f3fb189106d16fa/PropertyChanged.Fody/TypeProcessor.cs#L1-L47),
[PropertyWeaver.cs](https://github.com/Fody/PropertyChanged/blob/14a0870a7afcd44f334b4e122f3fb189106d16fa/PropertyChanged.Fody/PropertyWeaver.cs#L17-L61)).
Dependency metadata is resolved from `[DependsOn]` and `[AlsoNotifyFor]`; the
transitive closure is computed by `GetFullDependencies` and
`ComputeDependenciesRecursively`
([PropertyDataWalker.cs](https://github.com/Fody/PropertyChanged/blob/14a0870a7afcd44f334b4e122f3fb189106d16fa/PropertyChanged.Fody/PropertyDataWalker.cs#L35-L127)).
The clone's tests include `TestAssemblies/AssemblyToProcess/TransitiveDependencies.cs`,
`ClassDependsOn.cs`, and `ClassAlsoNotifyFor.cs`.

**Generates that NotifyGen does not.** Fody can weave existing auto/manual
properties, infer dependencies from getter IL, compute transitive dependency
notifications, inject `IsChanged`, and discover several `OnChanged` callback
signatures. It also has project-level equality and dependent-property
configuration. Those are in the weaver and its source/fixture tests, rather
than a new NotifyGen attribute surface.

**Generates worse than NotifyGen.** The output is post-build IL, not inspectable
source. It can fail when it cannot find a usable event invoker or when a custom
explicit event is inaccessible; the checked test suite contains those failure
fixtures (`AssemblyExplicitPropertyChanged` and
`AssemblyWithBlockingClass`). A Stack Overflow user asking about Fody on a
struct recorded raw score **0**, raw views **320**, and **0 answers**:
[question 25598886](https://stackoverflow.com/questions/25598886/fody-propertychanged-and-struct).
Fody's transitive closure uses a visited set but has no source-generator
cycle diagnostic; cycles are silently bounded rather than explained at the
attribute location. It has no NotifyGen-style batch suppression/`AlwaysNotify`.

**User-reliant behaviour our attribute surface cannot express.** Users relying
on Fody can annotate already-written properties, infer dependencies from
getter bodies, configure project-wide weaving, use `IsChanged`, or intercept
existing methods without declaring a generated backing field. NotifyGen's
attribute model deliberately cannot express IL rewriting, automatic getter
analysis, or those project-wide weaving switches.

## PropertyChanged.SourceGenerator

**Source pin.** `canton7/PropertyChanged.SourceGenerator` at
`b51c349df7611edf0224645370fbf20a1d402eaa` (v1.1.2). The incremental entry
point scans annotated fields and properties
([PropertyChangedSourceGenerator.cs](https://github.com/canton7/PropertyChanged.SourceGenerator/blob/b51c349df7611edf0224645370fbf20a1d402eaa/src/PropertyChanged.SourceGenerator/PropertyChangedSourceGenerator.cs#L10-L47)).
Generation uses `EqualityComparer<T>.Default`, old/new callbacks, generated
property attributes, access modifiers, virtual properties, and cached event
args ([Generator.cs](https://github.com/canton7/PropertyChanged.SourceGenerator/blob/b51c349df7611edf0224645370fbf20a1d402eaa/src/PropertyChanged.SourceGenerator/Generator.cs#L277-L351)).
Its tests include 188 NUnit cases and 147 verified generated files, including
`AlsoNotifyTests.cs`, `AutoDependsOnTests.cs`, `DependsOnTests.cs`,
`TypeGenerationTests.cs`, and the `OnAnyProperty*` suites.

**Generates that NotifyGen does not.** It accepts annotated fields *or backing
properties*, supports virtual/accessor combinations, arbitrary generated
property attributes, configurable hooks and raise-method overloads, automatic
getter-based dependency analysis, base-type dependencies, indexer names, and
`[IsChanged]`. `ResolveAutoDependsOn` recursively analyses computed property
references ([source](https://github.com/canton7/PropertyChanged.SourceGenerator/blob/b51c349df7611edf0224645370fbf20a1d402eaa/src/PropertyChanged.SourceGenerator/Analysis/Analyser_DependsOn.cs#L102-L181)).

**Generates worse than NotifyGen.** It requires every member to be individually
annotated and has no class-level “all eligible underscore fields” mode; it has
no command refresh, suppression scopes, or `AlwaysNotify`. Its current issue
#39 reports generated code that does not compile/misses `OnPropertyChanged`:
[issue #39](https://github.com/canton7/PropertyChanged.SourceGenerator/issues/39).
Issue #47 reports derived-class dependencies not working as documented:
[issue #47](https://github.com/canton7/PropertyChanged.SourceGenerator/issues/47).
The clone built its generator/tests but could not execute the test assembly on
this machine because only .NET 10 arm64 was installed (the tests target .NET
7); this is an environment limitation, not a claim about upstream test
correctness.

**User-reliant behaviour our attribute surface cannot express.** Users relying
on this generator can toggle automatic dependency discovery, declare
`OnAnyPropertyChanged`/`OnAnyPropertyChanging` overloads, forward arbitrary
property attributes, and adapt to existing event/raise-method signatures.
NotifyGen cannot request those from its current attributes; it intentionally
keeps a smaller explicit dependency model.

## MvvmGen

**Source pin.** `thomasclaudiushuber/mvvmgen` at
`5f53e628746737f83b0a038135de65f7a3502cca` (2026-06-12). The generator's
property path handles `[Property]` on fields and partial properties and emits
setter invalidations/callbacks/events
([PropertyGenerator.cs](https://github.com/thomasclaudiushuber/mvvmgen/blob/5f53e628746737f83b0a038135de65f7a3502cca/src/MvvmGen.SourceGenerators/Generators/PropertyGenerator.cs#L25-L97),
[ViewModelMemberInspector.cs](https://github.com/thomasclaudiushuber/mvvmgen/blob/5f53e628746737f83b0a038135de65f7a3502cca/src/MvvmGen.SourceGenerators/Inspectors/ViewModelMemberInspector.cs#L100-L235)).
Its property tests include field naming, partial-property access modifiers,
custom names, invalidation, method calls, and published events.

**Generates that NotifyGen does not.** MvvmGen supports `[Property]` partial
properties, model wrapping (including inherited and read-only model properties),
`[PropertyInvalidate]`, setter method calls and conditional event publication,
constructors/`OnInitialize`, commands, generated interfaces, and factories.
Those latter command/DI/factory features are MVVM framework features and are
out of scope for NotifyGen.

**Generates worse than NotifyGen.** Its generated field-backed setter uses
`backingField != value`, so a type without a `!=` operator can fail to compile
and equality semantics differ from NotifyGen's `EqualityComparer<T>.Default`
guard. It requires a runtime `ViewModelBase`, does not generate
`INotifyPropertyChanging`, has no batch suppression, and its source inspection
has no equivalent diagnostic/code-fix coverage for several invalid inputs. The
open issue asking for invalid-input diagnostics (#6) has raw **0 comments**:
[issue #6](https://github.com/thomasclaudiushuber/mvvmgen/issues/6).

**User-reliant behaviour our attribute surface cannot express.** MvvmGen users
can wrap a model and have generated model projections, invoke arbitrary named
methods or publish conditional events in setters, generate command/factory/
interface infrastructure, and initialize through `OnInitialize`. NotifyGen
cannot express those with INPC attributes and will not add them merely because
they are large gaps.

## ReactiveUI INPC support

**Source pin.** `reactiveui/ReactiveUI` at
`27b0e3c20011cd88f85356f7162df29549f9580b` (2026-08-02). This is runtime INPC
support, not an INPC source generator. `ReactiveObject` implements both INPC
interfaces and exposes reactive streams plus suppression/delay methods
([ReactiveObject.cs](https://github.com/reactiveui/ReactiveUI/blob/27b0e3c20011cd88f85356f7162df29549f9580b/src/ReactiveUI.Shared/ReactiveObject/ReactiveObject.cs#L21-L117)).
`RaiseAndSetIfChanged` performs the equality guard and event ordering
([IReactiveObjectExtensions.cs](https://github.com/reactiveui/ReactiveUI/blob/27b0e3c20011cd88f85356f7162df29549f9580b/src/ReactiveUI.Shared/ReactiveObject/IReactiveObjectExtensions.cs#L25-L59)).
Tests cover deferred/nested notifications, subscriber exceptions, and
suppression (`ReactiveObjectTests.cs`, especially lines 78-152 and 205-229).

**Generates that NotifyGen does not.** ReactiveUI supplies `Changing`,
`Changed`, `ThrownExceptions`, expression-based nested property observables,
`RaiseAndSetIfChanged`, suppression, and delayed/debounced notification state.
These are runtime APIs and observables, not generated properties.

**Generates worse than NotifyGen.** Every user property still needs a hand-written
setter calling `RaiseAndSetIfChanged`, and the type carries ReactiveUI/Splat
runtime dependencies and a `ReactiveObject`/`IReactiveObject` composition
contract. NotifyGen emits ordinary source with no runtime dependency and
provides generated partial hooks. ReactiveUI's tests deliberately marshal
subscriber exceptions through `ThrownExceptions`; NotifyGen's ordinary
`PropertyChanged` event follows .NET event semantics instead of silently
turning a subscriber exception into an observable.

**User-reliant behaviour our attribute surface cannot express.** Users relying
on ReactiveUI can subscribe to nested expression chains, observe before/after
streams, debounce/delay notifications, query `AreChangeNotificationsEnabled`,
and collect thrown subscriber exceptions. Those require a reactive runtime and
are out of scope for an INPC generator.

## Evidence and selection

The following candidate gaps were checked before selection. Counts are raw
figures as read from the linked page/API snapshot; they are not inferred from
stars or README marketing.

| Candidate | Evidence read | Result |
|---|---|---|
| Partial-property input | CommunityToolkit issue #555: **68 reactions total** (**+1 44, heart 17, rocket 3, eyes 4**), 61 comments: [issue](https://github.com/CommunityToolkit/dotnet/issues/555). Stack Overflow searches found no direct partial-property feature-request question; the linked GitHub issue is the resolvable direct demand signal. | Clears the bar. Current source confirms the feature is shipped, so the seeded candidate is verified rather than assumed. |
| Transitive `[NotifyAlso]` notifications and cycle diagnostics | Fody issue #179 “Allow nested dependencies”: **18 reactions total** (**+1 16, heart 2**): [issue](https://github.com/Fody/PropertyChanged/issues/179). Stack Overflow “MVVM: delegating PropertyChanged notifications A -> B (readonly) -> C”: raw score **5**, **1 answer**, **1,121 views**: [question](https://stackoverflow.com/questions/76579939). | Clears the bar. It is directly about dependent INPC chains and can be implemented in the generator/analyzer. |
| Equality guards with custom comparers | Fody issue #162 “Doesn't use custom operator ==” was retrieved but no non-zero reaction figure was available in the snapshot; no NotifyGen issue or validated Stack Overflow demand for a custom-comparer attribute was found. | **No evidence found; dropped.** |
| `[NotifySuppressable]` exception safety/async scopes | No NotifyGen issue, competitor issue, or validated Stack Overflow question with a feature-demand reaction/score specific to this API was found. ReactiveUI has suppression tests, but that is runtime behavior, not demand for NotifyGen's batch API. | **No evidence found; dropped.** |
| Diagnostic/code-fix expansion | MvvmGen issue #6 asks for invalid-input diagnostics but has raw **0 comments**: [issue](https://github.com/thomasclaudiushuber/mvvmgen/issues/6). No stronger reaction/Stack Overflow/NotifyGen issue evidence was found for a specific NotifyGen diagnostic gap. | **No evidence found for a candidate clearing the bar; dropped.** |

The NotifyGen issues endpoint returned **9 records**, all pull requests in the
snapshot; none specifically requests custom equality, transitive/cycle
`NotifyAlso`, or suppression exception/async semantics:
[NotifyGen issues](https://github.com/georgepwall1991/NotifyGen/issues). For
example, PR #9 has **1 +1 reaction** and PR #8 has **0 reactions**, but neither
is feature-demand evidence. The absence of a request is not treated as demand.

### Exact selection

**New capability:** partial-property generation under the existing `[Notify]`
class attribute. It beats the other new-capability candidates because it has
the largest direct, feature-specific demand signal above (68 raw reactions,
including 44 `+1`s), and the source confirms a mature competitor implementation
and test parity suite. It belongs in an INPC generator because it changes the
shape of generated properties/backing storage and notification hooks; commands,
messaging, validation frameworks, and DI are not required. The winning case is
a partial property on a class with an arbitrary existing base type: NotifyGen
can generate `INotifyPropertyChanged` with no CommunityToolkit runtime or base
class, while preserving the user's declared property surface.

**Deepening:** transitive `[NotifyAlso]` closure plus a compile-time cycle
diagnostic. It beats the other deepening candidates on direct evidence (Fody's
18 raw reactions and the chain question's score 5), while remaining a pure
INPC dependency graph. Fody already computes a bounded transitive closure, but
its visited set does not explain a cycle at the attribute location. NotifyGen
will make chains work without repeating every edge and will fail clearly on a
cycle instead of silently emitting an ambiguous graph. This is deliberately
not automatic getter analysis or an MVVM framework feature.

Both designs are additive and use existing attributes; no new attribute is
introduced in cycle 1. Candidates without evidence are recorded above and not
implemented.
