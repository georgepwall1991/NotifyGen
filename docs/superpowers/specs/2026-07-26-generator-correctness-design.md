# Generator Correctness Design

**Date:** 2026-07-26  
**Status:** Approved design  
**Scope:** NotifyGen source generation and its analyzer/code-fix contract

## Problem

NotifyGen's generator and analyzer disagree about supported fields. The analyzer reports static, const, and readonly underscore-prefixed fields as unsupported, but the generator still admits them. Readonly fields therefore produce illegal setters, and static fields produce misleading instance properties over shared state.

Nested `[Notify]` classes are also not implemented correctly. The generator records only the target's namespace and simple name, then emits a top-level partial class named after the nested target. Existing nested-type tests inspect generated text but do not verify the output compilation, so this defect remains green. Simple-name source hints such as `Inner.g.cs` can also collide across namespaces or containing types.

The result is a source generator that can pass its own tests while causing consumer compilation failures or generating members on the wrong type.

## Evidence

- `NotifyGenerator.IsEligibleField` does not reject static, const, or readonly fields.
- `NotifyAnalyzer.AnalyzeFieldEligibility` does reject those fields through `NOTIFY004` and `NOTIFY005`.
- `ClassInfo` has no containing-type representation.
- `NotifyGenerator.GenerateSource` emits only namespace plus one top-level `partial class`.
- `GeneratorTestHelper` returns `OutputCompilation`, but generator and edge-case tests usually assert driver diagnostics and generated substrings instead of output-compilation errors.
- The local `net10.0` test baseline is 153 passing tests.

## Goals

1. Make generator and analyzer field eligibility identical.
2. Support a notified class nested inside legal partial type containers.
3. Preserve each containing declaration's source shape sufficiently for a valid partial declaration.
4. Prevent source-hint collisions for equal simple names.
5. Make output-compilation success the primary generator correctness oracle.
6. Preserve all existing valid top-level generation behavior.
7. Preserve incremental-generator cache correctness by making every emitted value immutable and equality-complete.

## Non-goals

- Adding `[Notify]` support to record or struct targets.
- Fixing notification-suppression disposal behavior.
- Adding diagnostics for invalid `NotifyName`, command, or `AlwaysNotify` strings.
- Changing release metadata, samples, changelog content, or benchmark methodology.
- Adding a compatibility path for output that is currently invalid.

## Product Contract

`[Notify]` remains class-only. A notified class may be nested inside a partial class, struct, record class, record struct, or interface. Every source-declared type in the containing chain must be partial because a source generator can extend the nested target only by reopening each container.

A valid target generates exactly one source file. The source reopens the complete namespace and containing-type chain, then adds notification interfaces and generated members only to the notified target.

Static, const, readonly, ignored, incorrectly named, and non-private fields do not enter generation. Existing diagnostic severity and meaning for `NOTIFY002`, `NOTIFY004`, and `NOTIFY005` remain unchanged.

## Architecture

### Declaration Model

Replace the flat target identity with a general immutable declaration graph.

`TypeDeclarationInfo` represents one source type declaration and contains:

- declaration kind: class, struct, record class, record struct, or interface;
- source name and metadata name;
- accessibility;
- required modifiers needed for a compatible partial declaration;
- type parameter names and generic arity;
- normalized constraint clauses;
- stable fully qualified metadata identity.

`NotificationTypeInfo` contains:

- namespace;
- an outermost-to-target immutable array of `TypeDeclarationInfo` values;
- existing `INotifyPropertyChanged` and `INotifyPropertyChanging` capability flags;
- `ImplementChanging`, suppression, and `AlwaysNotify` options;
- the immutable array of eligible fields.

Every value that affects generated source participates in equality and hash-code calculation. There are no mutable collections in incremental values.

The model can describe all legal containing declaration kinds, but this pass populates a notified target only from `ClassDeclarationSyntax`. That keeps the public generation contract class-only while avoiding another flat class-specific representation.

### Shared Field Eligibility

Introduce one field classifier used by both generator and analyzer. It returns a finite result rather than a boolean so the analyzer can preserve specific diagnostics while the generator consumes only `Eligible` fields.

Required classifications:

- `Eligible`;
- `Ignored`;
- `NotPrivate`;
- `InvalidFieldName`;
- `StaticOrConst`;
- `Readonly`.

Classification precedence is deterministic. Ignored fields report nothing. Private underscore-prefixed static/const and readonly fields retain `NOTIFY004` and `NOTIFY005`. `NOTIFY002` is computed from the same classifications, eliminating drift.

### Generation Pipeline

1. Identify a class declaration carrying `[Notify]` and resolve its symbol.
2. Build the complete containing-type chain from symbols and source declarations.
3. Validate that the target and every container can be reopened as partial declarations.
4. Classify fields through the shared eligibility component.
5. Produce one immutable `NotificationTypeInfo` value.
6. Emit namespace, containing wrappers, and the target declaration.
7. Attach notification interfaces, events, generated properties, hooks, and suppression infrastructure only to the target.
8. Close type wrappers in reverse order.
9. Add the source with a portable, collision-proof hint derived from the fully qualified metadata identity.

Flat `ClassInfo` identity and duplicate generator/analyzer eligibility logic are removed in the cutover.

### Source Hint Identity

A hint name must distinguish namespace, every containing type, target name, and generic arity. It must also remain portable across file systems.

Use a deterministic, bijective encoding of the fully qualified metadata identity for the collision-proof portion of the hint. A readable target-name prefix may be included, but correctness must not depend on lossy character replacement or a probabilistic hash alone.

## Diagnostics and Failure Behavior

### Existing Diagnostics

- `NOTIFY001` remains the error for a notified target that is not partial.
- `NOTIFY002` remains the warning for a notified class with no eligible fields.
- `NOTIFY004` remains the informational diagnostic for static or const fields.
- `NOTIFY005` remains the informational diagnostic for readonly fields.

The generator emits no source for a non-partial notified target, preventing a cascading `CS0260` caused by generated output.

### New Containing-Type Diagnostic

Add `NOTIFY006` as an error for a non-partial containing type. It is located on the containing declaration and names both the container and the notified nested target.

Report each non-partial container that blocks generation. Emit no source for the target until the entire chain is valid.

Extend the existing make-partial code-fix path to support `NOTIFY006`; do not add a separate fixer implementation.

### Modeling Failures

A legal supported declaration graph is a total modeling case and must produce a complete model. Known source-shape failures (`NOTIFY001` and `NOTIFY006`) are detected by the analyzer; the generator consumes the same validity result, emits nothing for the invalid target, and does not report duplicate diagnostics.

Incomplete or error symbols from an already-invalid compilation are skipped without malformed fallback output. Unexpected implementation exceptions are not swallowed. This keeps user-actionable diagnostics authoritative while ensuring valid nested targets cannot be silently flattened.

## Testing Strategy

Implementation follows red-green-refactor.

### Red Tests

Add failing tests before implementation for:

1. readonly underscore fields causing output-compilation errors;
2. static and const fields being admitted by generation;
3. a notified class nested in a partial class being flattened;
4. deep generic containing chains losing declaration shape;
5. same simple names producing colliding source hints;
6. non-partial containers lacking an actionable diagnostic;
7. the make-partial code fix not handling a containing type.

### Supported Shapes

Compilation-clean tests cover notified classes nested inside:

- a partial class;
- a partial struct;
- a partial record class;
- a partial record struct;
- a partial interface;
- multiple nested containers;
- generic containers and a generic target with constraints;
- containers with required accessibility and modifiers.

Tests also cover equal simple names in different namespaces and different containing chains.

### Compilation Oracle

Add a focused helper that asserts `OutputCompilation.GetDiagnostics()` contains no errors for valid generator scenarios. Migrate generator and edge-case tests to this helper. Keep exact generated-text assertions only when they defend an emitted behavioral contract.

Analyzer and code-fix tests remain separate because they intentionally compile invalid source to verify diagnostics and fixes.

### Regression Coverage

Existing top-level generation, equality guards, dependent notifications, custom names, setter access, hooks, `INotifyPropertyChanging`, command refresh, suppression, and `AlwaysNotify` behavior must remain green.

## Verification

1. Run C# LSP diagnostics on every changed C# file if the project has a configured server. The server is currently unavailable for this workspace, so the implementation must record that limitation rather than claim diagnostics passed.
2. Build the solution in Release configuration.
3. Run the complete locally available test target; the current machine supports `net10.0`, while CI remains responsible for net8.0 and net9.0 runtime execution.
4. Build and run a temporary consumer that exercises nested generics and same-named types through the actual generator.
5. Pack the analyzer to confirm package composition still succeeds.
6. Run Slopwatch after code changes.
7. Run an independent Codex review and resolve every actionable finding.

## Acceptance Criteria

- Generator and analyzer use one field-eligibility classification.
- Static, const, and readonly fields never generate properties.
- Valid nested notified classes compile under the correct namespace and containing-type chain.
- Generic parameters, constraints, accessibility, declaration kind, and required modifiers remain compatible in generated partial declarations.
- Same simple names never collide in `AddSource` hint identity.
- Non-partial targets and containers produce one actionable diagnostic path and no malformed source.
- `NOTIFY006` has a working make-partial code fix.
- Valid generator tests fail on any output-compilation error.
- All existing valid behavior remains green.
- The temporary consumer builds and runs successfully through the real generator.
- Analyzer packaging still succeeds.
