# Generator Correctness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make NotifyGen emit compilation-clean properties for eligible fields and preserve the full declaration identity of nested notified classes.

**Architecture:** Replace flat `ClassInfo` with an immutable declaration graph that models every containing type, then render that graph around the existing generated target body. Share one field classifier between analyzer and generator, encode full metadata identity into source hints, and make output-compilation errors the primary generator test oracle.

**Tech Stack:** C#; .NET Standard 2.0 generator and attributes; Roslyn `IIncrementalGenerator`, analyzers, and code fixes at Microsoft.CodeAnalysis 4.8.0; xUnit; FluentAssertions; .NET 10 local test runtime.

## Global Constraints

- `[Notify]` remains class-only; do not add record or struct targets.
- A notified class may be nested inside a partial class, struct, record class, record struct, or interface.
- Every target and containing type must be partial before the generator emits source.
- Keep `NOTIFY002` as Warning and `NOTIFY004`/`NOTIFY005` as Info; add `NOTIFY006` as Error.
- Add no package dependency and do not change target frameworks or Microsoft.CodeAnalysis 4.8.0.
- Keep incremental values immutable and include every emitted value in equality and hashing.
- Use a deterministic bijective encoding of full metadata identity for collision-proof source hints.
- Clean cutover: delete flat `ClassInfo` and duplicate field-eligibility logic; add no aliases or shims.
- Release metadata, samples, changelog entries, string-target diagnostics, suppression disposal, and benchmarks are outside this implementation.
- Follow red-green-refactor. Do not write implementation before the failing test for each task.
- The C# LSP is currently unconfigured in this workspace. Retry per-file diagnostics during execution and report the limitation if it remains unavailable.

---

## File Structure

### Create

- `src/NotifyGen.Generator/FieldEligibility.cs` — shared finite field classification used by analyzer and generator.
- `src/NotifyGen.Generator/TypeDeclarationInfo.cs` — declaration kind plus immutable source-shape metadata for one target/container.
- `src/NotifyGen.Generator/NotificationTypeInfo.cs` — immutable aggregate replacing `ClassInfo`.
- `src/NotifyGen.Generator/TypeDeclarationInfoFactory.cs` — converts Roslyn symbols/syntax into an outermost-to-target declaration chain.
- `src/NotifyGen.Generator/SourceHintName.cs` — portable, bijective metadata-identity encoding.

### Modify

- `src/NotifyGen.Generator/NotifyGenerator.cs:39-535` — consume the new model, shared field classifier, nested wrappers, and unique hints.
- `src/NotifyGen.Generator/NotifyAnalyzer.cs:21-194` — consume shared field classification and report non-partial containers.
- `src/NotifyGen.Generator/FieldInfo.cs:91-117` — hash every emitted collection value instead of only the first.
- `src/NotifyGen.Generator/DiagnosticDescriptors.cs:8-69` — add `NOTIFY006`.
- `src/NotifyGen.Generator/NotifyCodeFixProvider.cs:14-104` — make any `TypeDeclarationSyntax` partial for `NOTIFY001` or `NOTIFY006`.
- `tests/NotifyGen.Tests/GeneratorTestHelper.cs:11-74` — add a compilation-clean test entrypoint.
- `tests/NotifyGen.Tests/EdgeCaseTests.cs:101-169,563-599` — replace false nested/static/readonly assertions with behavioral contracts.
- `tests/NotifyGen.Tests/GeneratorTests.cs:8-1155` — route valid generator scenarios through the compilation-clean helper.
- `tests/NotifyGen.Tests/AnalyzerTests.cs:18-712` — add field and containing-type diagnostics/code-fix coverage.
- `tests/NotifyGen.Tests/EqualityTests.cs:14-219` — replace `ClassInfo` tests with declaration-graph equality tests.

### Delete

- `src/NotifyGen.Generator/ClassInfo.cs` — superseded by `NotificationTypeInfo` and `TypeDeclarationInfo`.

---

### Task 1: Unify Field Eligibility

**Files:**
- Create: `src/NotifyGen.Generator/FieldEligibility.cs`
- Modify: `src/NotifyGen.Generator/NotifyGenerator.cs:21-27,163-225`
- Modify: `src/NotifyGen.Generator/NotifyAnalyzer.cs:16-19,73-164`
- Modify: `tests/NotifyGen.Tests/GeneratorTestHelper.cs:11-62`
- Test: `tests/NotifyGen.Tests/EdgeCaseTests.cs:132-169`
- Test: `tests/NotifyGen.Tests/AnalyzerTests.cs`

**Interfaces:**
- Produces: `FieldEligibility FieldEligibilityClassifier.Classify(IFieldSymbol field)`.
- Produces: `GeneratorTestHelper.RunGeneratorAndAssertCompiles(string source)` with the same tuple shape as `RunGenerator`.
- Consumes: existing `NotifyIgnoreAttribute` metadata name and `IFieldSymbol` properties.

- [ ] **Step 1: Add the compilation-clean helper**

Add FluentAssertions and this method to `GeneratorTestHelper`:

```csharp
using FluentAssertions;

public static (Compilation OutputCompilation, ImmutableArray<Diagnostic> Diagnostics, GeneratorDriverRunResult RunResult)
    RunGeneratorAndAssertCompiles(string source)
{
    var result = RunGenerator(source);
    var errors = result.OutputCompilation
        .GetDiagnostics()
        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .Select(static diagnostic => diagnostic.ToString())
        .ToArray();

    errors.Should().BeEmpty("valid source plus generated output must compile");
    return result;
}
```

- [ ] **Step 2: Replace the mixed-field test with a failing generated-compilation contract**

Update `Generator_WithMixedFields_OnlyGeneratesUnderscoreFields` so its source contains `_validField`, `_readonlyField`, `_constField`, and `_staticField`; call `RunGeneratorAndAssertCompiles`; assert only `ValidField` exists:

```csharp
var (outputCompilation, _, runResult) =
    GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

var generatedSource = runResult.Results.Single().GeneratedSources.Single().SourceText.ToString();
generatedSource.Should().Contain("public string ValidField");
generatedSource.Should().NotContain("ReadonlyField");
generatedSource.Should().NotContain("ConstField");
generatedSource.Should().NotContain("StaticField");
outputCompilation.GetTypeByMetadataName("TestNamespace.Mixed")!
    .GetMembers().OfType<IPropertySymbol>().Select(static property => property.Name)
    .Should().Equal("ValidField");
```

Use these exact ineligible declarations in the source:

```csharp
private readonly string _readonlyField = "";
private const string _constField = "";
private static string _staticField = "";
```

- [ ] **Step 3: Run the focused test and verify red**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --filter "FullyQualifiedName~Generator_WithMixedFields_OnlyGeneratesUnderscoreFields" --verbosity minimal
```

Expected: FAIL because generated setters assign readonly/const fields and because static/readonly/const properties are present.

- [ ] **Step 4: Implement the shared finite classifier**

Create `FieldEligibility.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NotifyGen.Generator;

internal enum FieldEligibility
{
    Eligible,
    Ignored,
    NotPrivate,
    InvalidFieldName,
    StaticOrConst,
    Readonly,
}

internal static class FieldEligibilityClassifier
{
    private const string NotifyIgnoreAttributeName = "NotifyGen.NotifyIgnoreAttribute";

    public static FieldEligibility Classify(IFieldSymbol field)
    {
        if (field.GetAttributes().Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == NotifyIgnoreAttributeName))
            return FieldEligibility.Ignored;

        if (field.DeclaredAccessibility != Accessibility.Private)
            return FieldEligibility.NotPrivate;

        if (!field.Name.StartsWith("_", StringComparison.Ordinal) || field.Name.Length < 2)
            return FieldEligibility.InvalidFieldName;

        if (field.IsStatic || field.IsConst)
            return FieldEligibility.StaticOrConst;

        if (field.IsReadOnly)
            return FieldEligibility.Readonly;

        return FieldEligibility.Eligible;
    }
}
```

- [ ] **Step 5: Replace generator and analyzer eligibility logic**

In `NotifyGenerator.ExtractFields`, replace `.Where(IsEligibleField)` with:

```csharp
.Where(static field => FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible)
```

Delete `NotifyGenerator.IsEligibleField` and delete its now-unused `NotifyIgnoreAttributeName` constant and `HasAttribute` helper.

In `NotifyAnalyzer.AnalyzeFieldEligibility`, classify each field once and switch on the result:

```csharp
var eligibility = FieldEligibilityClassifier.Classify(field);
switch (eligibility)
{
    case FieldEligibility.Eligible:
        hasEligibleFields = true;
        break;
    case FieldEligibility.StaticOrConst:
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.StaticOrConstField,
            GetFieldLocation(field, classDeclaration, context.CancellationToken),
            field.Name));
        break;
    case FieldEligibility.Readonly:
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ReadonlyField,
            GetFieldLocation(field, classDeclaration, context.CancellationToken),
            field.Name));
        break;
}
```

Add the exact helper:

```csharp
private static Location GetFieldLocation(
    IFieldSymbol field,
    ClassDeclarationSyntax classDeclaration,
    System.Threading.CancellationToken cancellationToken)
{
    return field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken).GetLocation()
        ?? classDeclaration.Identifier.GetLocation();
}
```

In `AnalyzeNotifyAlsoReferences`, filter generated properties with the same classifier:

```csharp
.Where(static field =>
    FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible)
```

Delete the analyzer's `NotifyIgnoreAttributeName` constant and duplicated private/name/ignore predicate.

- [ ] **Step 6: Add analyzer regression tests**

Add one parameterized analyzer test:

```csharp
[Theory]
[InlineData("private static string _invalidField = \"\";", "NOTIFY004")]
[InlineData("private readonly string _invalidField = \"\";", "NOTIFY005")]
public async Task Analyzer_IneligibleField_ReportsSpecificDiagnostic(
    string invalidField,
    string diagnosticId)
{
    var source = $$"""
        using NotifyGen;

        [Notify]
        public partial class Person
        {
            private string _validField = "";
            {{invalidField}}
        }
        """;

    var diagnostics = await GetDiagnosticsAsync(source);

    diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == diagnosticId);
    diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "NOTIFY002");
}
```

- [ ] **Step 7: Run focused and full local tests**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --filter "FullyQualifiedName~MixedFields|FullyQualifiedName~IneligibleField" --verbosity minimal
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity minimal
```

Expected: focused tests PASS; full suite PASS with at least 155 tests.

- [ ] **Step 8: Commit the field correctness slice**

```bash
git add src/NotifyGen.Generator/FieldEligibility.cs src/NotifyGen.Generator/NotifyGenerator.cs src/NotifyGen.Generator/NotifyAnalyzer.cs tests/NotifyGen.Tests/GeneratorTestHelper.cs tests/NotifyGen.Tests/EdgeCaseTests.cs tests/NotifyGen.Tests/AnalyzerTests.cs
git commit -m "fix: align field eligibility — prevent invalid generated setters"
```

---

### Task 2: Model and Emit Nested Declarations

**Files:**
- Create: `src/NotifyGen.Generator/TypeDeclarationInfo.cs`
- Create: `src/NotifyGen.Generator/NotificationTypeInfo.cs`
- Create: `src/NotifyGen.Generator/TypeDeclarationInfoFactory.cs`
- Create: `src/NotifyGen.Generator/SourceHintName.cs`
- Delete: `src/NotifyGen.Generator/ClassInfo.cs`
- Modify: `src/NotifyGen.Generator/FieldInfo.cs:91-117`
- Modify: `src/NotifyGen.Generator/NotifyGenerator.cs:39-535`
- Test: `tests/NotifyGen.Tests/EdgeCaseTests.cs:101-130,563-599`
- Test: `tests/NotifyGen.Tests/EqualityTests.cs:14-219`

**Interfaces:**
- Produces: `TypeDeclarationInfoFactory.TryCreateChain(SemanticModel, ClassDeclarationSyntax, CancellationToken, out ImmutableArray<TypeDeclarationInfo>)`.
- Produces: `NotificationTypeInfo` with `TypeDeclarations`, `TargetType`, `Namespace`, capability flags, options, and fields.
- Produces: `SourceHintName.Create(string metadataIdentity, string targetName)`.
- Consumes: `FieldEligibilityClassifier.Classify` from Task 1.

- [ ] **Step 1: Write failing behavioral tests for correct nested identity**

Replace the current nested test source with a partial outer and assert the property exists on the nested symbol, not a flattened top-level symbol:

```csharp
var source = """
    using NotifyGen;

    namespace TestNamespace
    {
        public partial class Outer
        {
            [Notify]
            public partial class Inner
            {
                private string _value = "";
            }
        }
    }
    """;

var (outputCompilation, _, _) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
outputCompilation.GetTypeByMetadataName("TestNamespace.Outer+Inner")!
    .GetMembers("Value").Should().ContainSingle();
outputCompilation.GetTypeByMetadataName("TestNamespace.Inner").Should().BeNull();
```

Replace the deep-nesting source so `Level1`, `Level2`, and `Level3` are all partial, then assert `TestNamespace.Level1+Level2+Level3+DeepNested` owns `Value`.

- [ ] **Step 2: Add failing collision and supported-container tests**

Add a collision test containing `A.Model` and `B.Model`, both notified. Assert two generated sources, distinct hint names, and a generated `Name` property on both metadata types:

```csharp
var (outputCompilation, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
var generatedSources = runResult.Results.Single().GeneratedSources;
generatedSources.Should().HaveCount(2);
generatedSources.Select(static result => result.HintName).Should().OnlyHaveUniqueItems();
outputCompilation.GetTypeByMetadataName("A.Model")!.GetMembers("Name").Should().ContainSingle();
outputCompilation.GetTypeByMetadataName("B.Model")!.GetMembers("Name").Should().ContainSingle();
```

Add this theory for legal containing declarations:

```csharp
[Theory]
[InlineData("public partial class Container")]
[InlineData("public partial struct Container")]
[InlineData("public partial record class Container")]
[InlineData("public partial record struct Container")]
[InlineData("public partial interface Container")]
public void Generator_WithSupportedContainingType_GeneratesOnNestedType(string containerDeclaration)
{
    var source = $$"""
        using NotifyGen;

        namespace TestNamespace
        {
            {{containerDeclaration}}
            {
                [Notify]
                public partial class Inner
                {
                    private int _value;
                }
            }
        }
        """;

    var (outputCompilation, _, _) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
    outputCompilation.GetTypeByMetadataName("TestNamespace.Container+Inner")!
        .GetMembers("Value").Should().ContainSingle();
}
```

Add a generic/modifier test using:

```csharp
public static partial class Outer<T> where T : class, new()
{
    [Notify]
    public partial class Inner<TValue> where TValue : struct
    {
        private TValue _value;
    }
}
```

Assert compilation is clean, metadata type `TestNamespace.Outer`1+Inner`1` owns `Value`, and generated text contains `public static partial class Outer<T>` plus `public partial class Inner<TValue>`. Do not require duplicate `where` clauses in generated output: C# merges constraints from the original partial declaration, and omission prevents copied unqualified constraint types from depending on missing `using` directives.

- [ ] **Step 3: Run nested tests and verify red**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --filter "FullyQualifiedName~Nested|FullyQualifiedName~SupportedContainingType|FullyQualifiedName~SameSimpleName|FullyQualifiedName~GenericContaining" --verbosity minimal
```

Expected: FAIL because nested targets are flattened and same-name hints collide.

- [ ] **Step 4: Add the declaration kind and immutable declaration model**

Create `TypeDeclarationInfo.cs` with this public shape inside the assembly:

```csharp
using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

internal enum TypeDeclarationKind
{
    Class,
    Struct,
    Interface,
    RecordClass,
    RecordStruct,
}

internal readonly struct TypeDeclarationInfo : IEquatable<TypeDeclarationInfo>
{
    public TypeDeclarationKind Kind { get; }
    public string Name { get; }
    public string MetadataName { get; }
    public string Accessibility { get; }
    public ImmutableArray<string> RequiredModifiers { get; }
    public ImmutableArray<string> TypeParameters { get; }
    public ImmutableArray<string> ConstraintClauses { get; }
    public string MetadataIdentity { get; }
    public bool IsPartial { get; }

    public string Keyword => Kind switch
    {
        TypeDeclarationKind.Class => "class",
        TypeDeclarationKind.Struct => "struct",
        TypeDeclarationKind.Interface => "interface",
        TypeDeclarationKind.RecordClass => "record class",
        TypeDeclarationKind.RecordStruct => "record struct",
        _ => throw new InvalidOperationException($"Unsupported declaration kind: {Kind}"),
    };

    public string TypeParameterList => TypeParameters.Length == 0
        ? string.Empty
        : $"<{string.Join(", ", TypeParameters)}>";

    public TypeDeclarationInfo(
        TypeDeclarationKind kind,
        string name,
        string metadataName,
        string accessibility,
        ImmutableArray<string> requiredModifiers,
        ImmutableArray<string> typeParameters,
        ImmutableArray<string> constraintClauses,
        string metadataIdentity,
        bool isPartial)
    {
        Kind = kind;
        Name = name;
        MetadataName = metadataName;
        Accessibility = accessibility;
        RequiredModifiers = requiredModifiers;
        TypeParameters = typeParameters;
        ConstraintClauses = constraintClauses;
        MetadataIdentity = metadataIdentity;
        IsPartial = isPartial;
    }

    public bool Equals(TypeDeclarationInfo other) =>
        Kind == other.Kind
        && Name == other.Name
        && MetadataName == other.MetadataName
        && Accessibility == other.Accessibility
        && RequiredModifiers.SequenceEqual(other.RequiredModifiers)
        && TypeParameters.SequenceEqual(other.TypeParameters)
        && ConstraintClauses.SequenceEqual(other.ConstraintClauses)
        && MetadataIdentity == other.MetadataIdentity
        && IsPartial == other.IsPartial;

    public override bool Equals(object? obj) => obj is TypeDeclarationInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + Kind.GetHashCode();
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + MetadataName.GetHashCode();
            hash = hash * 31 + Accessibility.GetHashCode();
            foreach (var modifier in RequiredModifiers) hash = hash * 31 + modifier.GetHashCode();
            foreach (var parameter in TypeParameters) hash = hash * 31 + parameter.GetHashCode();
            foreach (var clause in ConstraintClauses) hash = hash * 31 + clause.GetHashCode();
            hash = hash * 31 + MetadataIdentity.GetHashCode();
            hash = hash * 31 + IsPartial.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(TypeDeclarationInfo left, TypeDeclarationInfo right) => left.Equals(right);
    public static bool operator !=(TypeDeclarationInfo left, TypeDeclarationInfo right) => !left.Equals(right);
}
```

- [ ] **Step 5: Replace ClassInfo with NotificationTypeInfo**

Create `NotificationTypeInfo.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

internal readonly struct NotificationTypeInfo : IEquatable<NotificationTypeInfo>
{
    public string Namespace { get; }
    public ImmutableArray<TypeDeclarationInfo> TypeDeclarations { get; }
    public bool AlreadyImplementsInpc { get; }
    public bool AlreadyImplementsInpcChanging { get; }
    public bool ImplementChanging { get; }
    public bool IsSuppressable { get; }
    public ImmutableArray<string> AlwaysNotifyProperties { get; }
    public ImmutableArray<FieldInfo> Fields { get; }
    public TypeDeclarationInfo TargetType => TypeDeclarations[TypeDeclarations.Length - 1];
    public bool CanGenerate => TypeDeclarations.Length > 0
        && TypeDeclarations.All(static declaration => declaration.IsPartial)
        && Fields.Length > 0;

    public NotificationTypeInfo(
        string @namespace,
        ImmutableArray<TypeDeclarationInfo> typeDeclarations,
        bool alreadyImplementsInpc,
        bool alreadyImplementsInpcChanging,
        bool implementChanging,
        bool isSuppressable,
        ImmutableArray<string> alwaysNotifyProperties,
        ImmutableArray<FieldInfo> fields)
    {
        Namespace = @namespace;
        TypeDeclarations = typeDeclarations;
        AlreadyImplementsInpc = alreadyImplementsInpc;
        AlreadyImplementsInpcChanging = alreadyImplementsInpcChanging;
        ImplementChanging = implementChanging;
        IsSuppressable = isSuppressable;
        AlwaysNotifyProperties = alwaysNotifyProperties;
        Fields = fields;
    }

    public bool Equals(NotificationTypeInfo other) =>
        Namespace == other.Namespace
        && TypeDeclarations.SequenceEqual(other.TypeDeclarations)
        && AlreadyImplementsInpc == other.AlreadyImplementsInpc
        && AlreadyImplementsInpcChanging == other.AlreadyImplementsInpcChanging
        && ImplementChanging == other.ImplementChanging
        && IsSuppressable == other.IsSuppressable
        && AlwaysNotifyProperties.SequenceEqual(other.AlwaysNotifyProperties)
        && Fields.SequenceEqual(other.Fields);

    public override bool Equals(object? obj) =>
        obj is NotificationTypeInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + Namespace.GetHashCode();
            foreach (var declaration in TypeDeclarations) hash = hash * 31 + declaration.GetHashCode();
            hash = hash * 31 + AlreadyImplementsInpc.GetHashCode();
            hash = hash * 31 + AlreadyImplementsInpcChanging.GetHashCode();
            hash = hash * 31 + ImplementChanging.GetHashCode();
            hash = hash * 31 + IsSuppressable.GetHashCode();
            foreach (var propertyName in AlwaysNotifyProperties) hash = hash * 31 + propertyName.GetHashCode();
            foreach (var field in Fields) hash = hash * 31 + field.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(NotificationTypeInfo left, NotificationTypeInfo right) =>
        left.Equals(right);

    public static bool operator !=(NotificationTypeInfo left, NotificationTypeInfo right) =>
        !left.Equals(right);
}
```

Delete `ClassInfo.cs` after all references compile. Do not retain an alias or conversion constructor.

- [ ] **Step 6: Build the declaration chain from syntax and symbols**

Create `TypeDeclarationInfoFactory.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotifyGen.Generator;

internal static class TypeDeclarationInfoFactory
{
    public static bool TryCreateChain(
        SemanticModel semanticModel,
        ClassDeclarationSyntax targetDeclaration,
        CancellationToken cancellationToken,
        out ImmutableArray<TypeDeclarationInfo> declarations)
    {
        var builder = ImmutableArray.CreateBuilder<TypeDeclarationInfo>();
        var syntaxChain = targetDeclaration.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .Reverse();

        foreach (var declaration in syntaxChain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken)
                is not INamedTypeSymbol symbol)
            {
                declarations = ImmutableArray<TypeDeclarationInfo>.Empty;
                return false;
            }

            var typeParameters = declaration.TypeParameterList is { } typeParameterList
                ? typeParameterList.Parameters
                    .Select(static parameter => parameter.Identifier.ValueText)
                    .ToImmutableArray()
                : ImmutableArray<string>.Empty;
            var requiredModifiers = declaration.Modifiers
                .Where(IsRequiredModifier)
                .Select(static token => token.ValueText)
                .ToImmutableArray();
            var constraintClauses = declaration.ConstraintClauses
                .Select(static clause => clause.NormalizeWhitespace().ToFullString())
                .ToImmutableArray();

            builder.Add(new TypeDeclarationInfo(
                GetKind(declaration),
                symbol.Name,
                symbol.MetadataName,
                GetAccessibility(symbol.DeclaredAccessibility),
                requiredModifiers,
                typeParameters,
                constraintClauses,
                GetMetadataIdentity(symbol),
                declaration.Modifiers.Any(SyntaxKind.PartialKeyword)));
        }

        declarations = builder.ToImmutable();
        return declarations.Length > 0;
    }

    private static TypeDeclarationKind GetKind(TypeDeclarationSyntax declaration) =>
        declaration switch
        {
            ClassDeclarationSyntax => TypeDeclarationKind.Class,
            StructDeclarationSyntax => TypeDeclarationKind.Struct,
            InterfaceDeclarationSyntax => TypeDeclarationKind.Interface,
            RecordDeclarationSyntax record
                when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                    => TypeDeclarationKind.RecordStruct,
            RecordDeclarationSyntax => TypeDeclarationKind.RecordClass,
            _ => throw new InvalidOperationException(
                $"Unsupported containing type syntax: {declaration.Kind()}"),
        };

    private static bool IsRequiredModifier(SyntaxToken token) =>
        token.IsKind(SyntaxKind.StaticKeyword)
        || token.IsKind(SyntaxKind.AbstractKeyword)
        || token.IsKind(SyntaxKind.SealedKeyword)
        || token.IsKind(SyntaxKind.ReadOnlyKeyword)
        || token.IsKind(SyntaxKind.RefKeyword);

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal",
        };

    private static string GetMetadataIdentity(INamedTypeSymbol symbol)
    {
        var names = new Stack<string>();
        for (var current = symbol; current != null; current = current.ContainingType)
            names.Push(current.MetadataName);

        var typeIdentity = string.Join("+", names);
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? typeIdentity
            : $"{symbol.ContainingNamespace.ToDisplayString()}.{typeIdentity}";
    }
}
```

Constraint clauses remain in the equality-complete general model, but the emitter deliberately omits them. C# combines constraints from the original partial declaration; re-emitting source spelling could break when a constraint relies on a `using` directive absent from the generated file.

- [ ] **Step 7: Add a portable unique hint encoder**

Create `SourceHintName.cs`:

```csharp
using System;
using System.Text;

namespace NotifyGen.Generator;

internal static class SourceHintName
{
    public static string Create(string metadataIdentity, string targetName)
    {
        var encodedIdentity = Convert.ToBase64String(Encoding.UTF8.GetBytes(metadataIdentity))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var readableName = IsPortableAsciiIdentifier(targetName) ? targetName : "Type";
        return $"NotifyGen.{encodedIdentity}.{readableName}.g.cs";
    }

    private static bool IsPortableAsciiIdentifier(string value)
    {
        foreach (var character in value)
        {
            if (!((character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character == '_'))
                return false;
        }

        return value.Length > 0;
    }
}
```

The Base64Url transform is bijective because it preserves all payload bits and removes only derivable padding.

- [ ] **Step 8: Convert the generator pipeline to NotificationTypeInfo**

Rename `GetClassInfo` to `GetNotificationTypeInfo` and return `NotificationTypeInfo?`. After resolving the class symbol, call the factory:

```csharp
if (!TypeDeclarationInfoFactory.TryCreateChain(
        semanticModel, classDecl, ct, out var typeDeclarations))
    return null;
```

Construct the aggregate exactly once:

```csharp
var containingNamespace = classSymbol.ContainingNamespace;
var namespaceName = containingNamespace.IsGlobalNamespace
    ? string.Empty
    : containingNamespace.ToDisplayString();

return new NotificationTypeInfo(
    namespaceName,
    typeDeclarations,
    alreadyImplementsInpc,
    alreadyImplementsInpcChanging,
    implementChanging,
    isSuppressable,
    alwaysNotifyProperties,
    fields);
```

Delete the old accessibility and type-parameter extraction from `NotifyGenerator`; the factory owns both.

Keep the fast syntax predicate class-only and partial:

```csharp
return node is ClassDeclarationSyntax classDeclaration
    && classDeclaration.AttributeLists.Count > 0
    && classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);
```

- [ ] **Step 9: Emit the declaration graph around the existing target body**

Change `GenerateSource` to accept `NotificationTypeInfo info` and return when `!info.CanGenerate`.

After namespace emission, open every declaration in order:

```csharp
var indent = hasNamespace ? "    " : string.Empty;
for (var index = 0; index < info.TypeDeclarations.Length; index++)
{
    var declaration = info.TypeDeclarations[index];
    var requiredModifiers = declaration.RequiredModifiers.Length == 0
        ? string.Empty
        : string.Join(" ", declaration.RequiredModifiers) + " ";
    var interfaces = index == info.TypeDeclarations.Length - 1
        ? BuildInterfaceList(info)
        : string.Empty;

    sb.AppendLine($"{indent}{declaration.Accessibility} {requiredModifiers}partial {declaration.Keyword} {declaration.Name}{declaration.TypeParameterList}{interfaces}");
    sb.AppendLine($"{indent}{{");
    indent += "    ";
}
```

Move the current target-member emission under the resulting `indent`, replacing `classInfo` with `info`. In `NotificationSuppressor`, use:

```csharp
var targetTypeName = info.TargetType.Name + info.TargetType.TypeParameterList;
```

After target members, close all declarations:

```csharp
for (var index = info.TypeDeclarations.Length - 1; index >= 0; index--)
{
    indent = indent.Substring(0, indent.Length - 4);
    sb.AppendLine($"{indent}}}");
}
```

Add source with:

```csharp
context.AddSource(
    SourceHintName.Create(info.TargetType.MetadataIdentity, info.TargetType.Name),
    SourceText.From(sb.ToString(), Encoding.UTF8));
```

Add the exact helper:

```csharp
private static string BuildInterfaceList(NotificationTypeInfo info)
{
    var interfaces = new List<string>();
    if (!info.AlreadyImplementsInpc)
        interfaces.Add("INotifyPropertyChanged");
    if (info.ImplementChanging && !info.AlreadyImplementsInpcChanging)
        interfaces.Add("INotifyPropertyChanging");

    return interfaces.Count == 0
        ? string.Empty
        : " : " + string.Join(", ", interfaces);
}
```

- [ ] **Step 10: Complete model hashing and replace ClassInfo equality tests**

Before replacing the tests, update `FieldInfo.GetHashCode` so every emitted notification target participates:

```csharp
foreach (var propertyName in AlsoNotify)
    hash = hash * 31 + propertyName.GetHashCode();
foreach (var commandName in CommandsToNotify)
    hash = hash * 31 + commandName.GetHashCode();
```

Delete the old `AlsoNotify.Length`, `CommandsToNotify.Length`, and first-element-only branches.

Delete ClassInfo-specific tests. Add these construction helpers and equality contracts:

```csharp
private static TypeDeclarationInfo CreateDeclaration(
    TypeDeclarationKind kind = TypeDeclarationKind.Class,
    string name = "Person",
    string metadataName = "Person",
    string accessibility = "public",
    ImmutableArray<string> requiredModifiers = default,
    ImmutableArray<string> typeParameters = default,
    ImmutableArray<string> constraintClauses = default,
    string metadataIdentity = "TestNamespace.Person",
    bool isPartial = true) =>
    new(
        kind,
        name,
        metadataName,
        accessibility,
        requiredModifiers.IsDefault ? ImmutableArray<string>.Empty : requiredModifiers,
        typeParameters.IsDefault ? ImmutableArray<string>.Empty : typeParameters,
        constraintClauses.IsDefault ? ImmutableArray<string>.Empty : constraintClauses,
        metadataIdentity,
        isPartial);

[Fact]
public void TypeDeclarationInfo_Equality_IncludesCompleteDeclarationShape()
{
    var baseline = CreateDeclaration();
    var identical = CreateDeclaration();
    var variants = new[]
    {
        CreateDeclaration(kind: TypeDeclarationKind.Struct),
        CreateDeclaration(name: "Other"),
        CreateDeclaration(metadataName: "Person`1"),
        CreateDeclaration(accessibility: "internal"),
        CreateDeclaration(requiredModifiers: ImmutableArray.Create("sealed")),
        CreateDeclaration(typeParameters: ImmutableArray.Create("T")),
        CreateDeclaration(constraintClauses: ImmutableArray.Create("where T : class")),
        CreateDeclaration(metadataIdentity: "Other.Person"),
        CreateDeclaration(isPartial: false),
    };

    baseline.Should().Be(identical);
    baseline.GetHashCode().Should().Be(identical.GetHashCode());
    variants.Should().OnlyContain(candidate => candidate != baseline);
}

[Fact]
public void NotificationTypeInfo_Equality_IncludesCompleteGenerationState()
{
    var declaration = CreateDeclaration();
    var changedDeclaration = CreateDeclaration(metadataIdentity: "Other.Person");
    var field = new FieldInfo(
        "_name",
        "Name",
        "string",
        false,
        ImmutableArray.Create("DisplayName"),
        ImmutableArray.Create("SaveCommand"),
        null,
        false);
    var changedField = new FieldInfo(
        "_age",
        "Age",
        "int",
        false,
        ImmutableArray<string>.Empty,
        ImmutableArray<string>.Empty,
        null,
        true);
    var baseline = new NotificationTypeInfo(
        "TestNamespace",
        ImmutableArray.Create(declaration),
        false,
        false,
        true,
        true,
        ImmutableArray.Create("Name"),
        ImmutableArray.Create(field));
    var identical = new NotificationTypeInfo(
        "TestNamespace",
        ImmutableArray.Create(declaration),
        false,
        false,
        true,
        true,
        ImmutableArray.Create("Name"),
        ImmutableArray.Create(field));
    var variants = new[]
    {
        new NotificationTypeInfo("Other", ImmutableArray.Create(declaration), false, false, true, true, ImmutableArray.Create("Name"), ImmutableArray.Create(field)),
        new NotificationTypeInfo("TestNamespace", ImmutableArray.Create(changedDeclaration), false, false, true, true, ImmutableArray.Create("Name"), ImmutableArray.Create(field)),
        new NotificationTypeInfo("TestNamespace", ImmutableArray.Create(declaration), true, false, true, true, ImmutableArray.Create("Name"), ImmutableArray.Create(field)),
        new NotificationTypeInfo("TestNamespace", ImmutableArray.Create(declaration), false, true, true, true, ImmutableArray.Create("Name"), ImmutableArray.Create(field)),
        new NotificationTypeInfo("TestNamespace", ImmutableArray.Create(declaration), false, false, false, true, ImmutableArray.Create("Name"), ImmutableArray.Create(field)),
        new NotificationTypeInfo("TestNamespace", ImmutableArray.Create(declaration), false, false, true, false, ImmutableArray.Create("Name"), ImmutableArray.Create(field)),
        new NotificationTypeInfo("TestNamespace", ImmutableArray.Create(declaration), false, false, true, true, ImmutableArray.Create("Other"), ImmutableArray.Create(field)),
        new NotificationTypeInfo("TestNamespace", ImmutableArray.Create(declaration), false, false, true, true, ImmutableArray.Create("Name"), ImmutableArray.Create(changedField)),
    };

    baseline.Should().Be(identical);
    baseline.GetHashCode().Should().Be(identical.GetHashCode());
    variants.Should().OnlyContain(candidate => candidate != baseline);
}

[Fact]
public void FieldInfo_Equality_IncludesLaterNotificationTargets()
{
    var baseline = new FieldInfo(
        "_name",
        "Name",
        "string",
        false,
        ImmutableArray.Create("First", "Second"),
        ImmutableArray.Create("SaveCommand", "UndoCommand"));
    var changedProperty = new FieldInfo(
        "_name",
        "Name",
        "string",
        false,
        ImmutableArray.Create("First", "Changed"),
        ImmutableArray.Create("SaveCommand", "UndoCommand"));
    var changedCommand = new FieldInfo(
        "_name",
        "Name",
        "string",
        false,
        ImmutableArray.Create("First", "Second"),
        ImmutableArray.Create("SaveCommand", "ChangedCommand"));

    baseline.Should().NotBe(changedProperty);
    baseline.Should().NotBe(changedCommand);
}
```

Do not assert that unequal values always have unequal hash codes; hash collisions are legal. The implementation requirement is that every emitted value is folded into the hash.

- [ ] **Step 11: Run nested and full tests**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --filter "FullyQualifiedName~Nested|FullyQualifiedName~SupportedContainingType|FullyQualifiedName~SameSimpleName|FullyQualifiedName~GenericContaining|FullyQualifiedName~TypeDeclarationInfo|FullyQualifiedName~NotificationTypeInfo" --verbosity minimal
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity minimal
```

Expected: focused tests PASS; full suite PASS with the Task 1 additions.

- [ ] **Step 12: Commit nested type support**

```bash
git add src/NotifyGen.Generator/TypeDeclarationInfo.cs src/NotifyGen.Generator/NotificationTypeInfo.cs src/NotifyGen.Generator/TypeDeclarationInfoFactory.cs src/NotifyGen.Generator/SourceHintName.cs src/NotifyGen.Generator/NotifyGenerator.cs src/NotifyGen.Generator/FieldInfo.cs src/NotifyGen.Generator/ClassInfo.cs tests/NotifyGen.Tests/EdgeCaseTests.cs tests/NotifyGen.Tests/EqualityTests.cs
git commit -m "feat: support nested notified classes — preserve declaring type identity"
```

---

### Task 3: Diagnose Non-Partial Containers

**Files:**
- Modify: `src/NotifyGen.Generator/DiagnosticDescriptors.cs:8-69`
- Modify: `src/NotifyGen.Generator/NotifyAnalyzer.cs:21-71`
- Modify: `src/NotifyGen.Generator/NotifyCodeFixProvider.cs:14-104`
- Test: `tests/NotifyGen.Tests/AnalyzerTests.cs:18-712`
- Test: `tests/NotifyGen.Tests/EdgeCaseTests.cs`

**Interfaces:**
- Produces: `DiagnosticDescriptors.ContainingTypeMustBePartial` with ID `NOTIFY006` and Error severity.
- Produces: one shared make-partial code action for `TypeDeclarationSyntax`.
- Consumes: `NotificationTypeInfo.CanGenerate` from Task 2, which already skips any non-partial chain.

- [ ] **Step 1: Write failing analyzer and code-fix tests**

Add `Analyzer_NestedNotifyInNonPartialContainer_ReportsNotify006` using:

```csharp
public class Outer
{
    [Notify]
    public partial class Inner
    {
        private string _value = "";
    }
}
```

Assert one error diagnostic with ID `NOTIFY006`, message arguments `Outer` and `Inner`, and a location on `Outer`.

Add a deep case with non-partial `Level1` and `Level2` around a partial notified target; assert exactly two `NOTIFY006` diagnostics.

Add `CodeFix_ContainingClass_AddsPartialModifier` with this expected output:

```csharp
public partial class Outer
{
    [Notify]
    public partial class Inner
    {
        private string _value = "";
    }
}
```

Add the same code-fix contract for `public record class Outer`, expecting `public partial record class Outer`.

- [ ] **Step 2: Run NOTIFY006 tests and verify red**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --filter "FullyQualifiedName~NOTIFY006|FullyQualifiedName~NonPartialContainer|FullyQualifiedName~ContainingClass|FullyQualifiedName~ContainingRecord" --verbosity minimal
```

Expected: FAIL because `NOTIFY006` and its code-fix registration do not exist.

- [ ] **Step 3: Add NOTIFY006**

Append to `DiagnosticDescriptors`:

```csharp
public static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
    id: "NOTIFY006",
    title: "Containing type must be partial",
    messageFormat: "Containing type '{0}' must be partial to generate notifications for nested class '{1}'.",
    category: "NotifyGen",
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "Every containing type of a nested class marked with [Notify] must be partial so the generator can reopen the declaration chain.");
```

Add it to `NotifyAnalyzer.SupportedDiagnostics`.

- [ ] **Step 4: Report every blocking container**

After the target's existing `NOTIFY001` check and before field analysis, call:

```csharp
AnalyzeContainingTypes(context, classDeclaration, classSymbol);
```

Implement:

```csharp
private static void AnalyzeContainingTypes(
    SyntaxNodeAnalysisContext context,
    ClassDeclarationSyntax targetDeclaration,
    INamedTypeSymbol targetSymbol)
{
    foreach (var containingDeclaration in targetDeclaration.Ancestors().OfType<TypeDeclarationSyntax>())
    {
        if (containingDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            continue;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ContainingTypeMustBePartial,
            containingDeclaration.Identifier.GetLocation(),
            containingDeclaration.Identifier.ValueText,
            targetSymbol.Name));
    }
}
```

Do not return after the first container; the contract reports every blocker in one analysis pass.

- [ ] **Step 5: Generalize the existing code fix**

Change the title to `Make type partial`. Register both IDs:

```csharp
public override ImmutableArray<string> FixableDiagnosticIds =>
    ImmutableArray.Create(
        DiagnosticDescriptors.ClassMustBePartial.Id,
        DiagnosticDescriptors.ContainingTypeMustBePartial.Id);
```

In `RegisterCodeFixesAsync`, find `.OfType<TypeDeclarationSyntax>().FirstOrDefault()` instead of `ClassDeclarationSyntax`. Change `AddPartialModifierAsync` to accept `TypeDeclarationSyntax`, then call `WithModifiers` and replace that node. Retain the current trivia-preserving modifier insertion exactly.

Change the test helper to:

```csharp
private static async Task<string> ApplyCodeFixAsync(
    string source,
    string diagnosticId = "NOTIFY001")
```

Use `diagnosticId` for both diagnostic lookups. Update the existing fixable-ID test to assert equivalent set membership:

```csharp
codeFixer.FixableDiagnosticIds.Should().BeEquivalentTo("NOTIFY001", "NOTIFY006");
```

- [ ] **Step 6: Assert the generator emits nothing for an invalid chain**

Add an edge-case test with a non-partial outer and partial notified inner. Run `RunGenerator`, then assert:

```csharp
runResult.Results.Single().GeneratedSources.Should().BeEmpty();
```

Do not use `RunGeneratorAndAssertCompiles` here: the source is intentionally invalid under the NotifyGen contract and the analyzer test owns the user-facing error.

- [ ] **Step 7: Run focused and full tests**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --filter "FullyQualifiedName~NOTIFY006|FullyQualifiedName~NonPartialContainer|FullyQualifiedName~ContainingClass|FullyQualifiedName~ContainingRecord" --verbosity minimal
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity minimal
```

Expected: focused and full suites PASS.

- [ ] **Step 8: Commit containing-type diagnostics**

```bash
git add src/NotifyGen.Generator/DiagnosticDescriptors.cs src/NotifyGen.Generator/NotifyAnalyzer.cs src/NotifyGen.Generator/NotifyCodeFixProvider.cs tests/NotifyGen.Tests/AnalyzerTests.cs tests/NotifyGen.Tests/EdgeCaseTests.cs
git commit -m "feat: diagnose non-partial containers — make nested generation actionable"
```

---

### Task 4: Enforce Compilation-Clean Generator Tests

**Files:**
- Modify: `tests/NotifyGen.Tests/GeneratorTests.cs:8-1155`
- Modify: `tests/NotifyGen.Tests/EdgeCaseTests.cs:11-600`
- Modify: `tests/NotifyGen.Tests/GeneratorTestHelper.cs:11-86`

**Interfaces:**
- Consumes: `RunGeneratorAndAssertCompiles` from Task 1.
- Produces: all valid generator tests fail immediately on generated C# compilation errors.

- [ ] **Step 1: Route every valid generator scenario through the compile assertion**

Replace calls to `GeneratorTestHelper.RunGenerator(source)` with `GeneratorTestHelper.RunGeneratorAndAssertCompiles(source)` throughout `GeneratorTests.cs` and valid cases in `EdgeCaseTests.cs`.

Keep `RunGenerator` only for tests whose source intentionally violates the NotifyGen contract, including the Task 3 non-partial-container no-output test.

Do not remove exact generated-text assertions that defend setter ordering, equality guards, hooks, notifications, setter access, constraints, or declaration wrappers.

- [ ] **Step 2: Run the two generator-focused test classes**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --filter "FullyQualifiedName~NotifyGen.Tests.GeneratorTests|FullyQualifiedName~NotifyGen.Tests.EdgeCaseTests" --verbosity minimal
```

Expected: PASS with no output-compilation errors.

- [ ] **Step 3: Run the complete local suite**

Run:

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-restore --verbosity minimal
```

Expected: PASS; zero failed and zero skipped tests.

- [ ] **Step 4: Commit the permanent test oracle**

```bash
git add tests/NotifyGen.Tests/GeneratorTests.cs tests/NotifyGen.Tests/EdgeCaseTests.cs tests/NotifyGen.Tests/GeneratorTestHelper.cs
git commit -m "test: enforce compilation-clean generator outputs — catch invalid emitted code"
```

---

## Completion Gate

Run only after Tasks 1-4 are green.

- [ ] **C# diagnostics**

Retry LSP diagnostics for every changed `.cs` file. If the project still reports `No language servers configured`, record that exact limitation and continue with compiler proof; do not claim LSP diagnostics passed.

- [ ] **Release build**

```bash
dotnet build NotifyGen.sln --configuration Release --no-restore
```

Expected: build succeeds with zero errors.

- [ ] **Local full test target**

```bash
dotnet test tests/NotifyGen.Tests/NotifyGen.Tests.csproj --configuration Release --framework net10.0 --no-build --verbosity minimal
```

Expected: all tests pass. Do not claim net8.0/net9.0 runtime execution locally; those testhost runtimes are absent and CI owns those targets.

- [ ] **Real consumer smoke test**

Create `/tmp/NotifyGen.Consumer/NotifyGen.Consumer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="/Users/georgewall/NotifyGen/src/NotifyGen.Attributes/NotifyGen.Attributes.csproj" />
    <ProjectReference Include="/Users/georgewall/NotifyGen/src/NotifyGen.Generator/NotifyGen.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

Create `/tmp/NotifyGen.Consumer/Program.cs`:

```csharp
using NotifyGen;

namespace A
{
    public static partial class Outer<T> where T : class, new()
    {
        [Notify]
        public partial class Model
        {
            private string _name = "";
        }
    }
}

namespace B
{
    [Notify]
    public partial class Model
    {
        private string _name = "";
    }
}

internal static class Program
{
    private static void Main()
    {
        var nested = new A.Outer<object>.Model { Name = "nested" };
        var peer = new B.Model { Name = "peer" };
        Console.WriteLine($"{nested.Name}/{peer.Name}");
    }
}
```

Run:

```bash
dotnet run --project /tmp/NotifyGen.Consumer/NotifyGen.Consumer.csproj --configuration Release
```

Expected stdout: `nested/peer`.

- [ ] **Package smoke test**

```bash
dotnet pack src/NotifyGen.Generator/NotifyGen.Generator.csproj --configuration Release --no-build --output /tmp/notifygen-correctness-pack
```

Expected: package creation succeeds. The package remains `NotifyGen.1.4.0.nupkg`; version repair is explicitly outside this plan.

- [ ] **Change-quality gates**

Invoke `dotnet-slopwatch`, then scan changed files for debug statements, commented-out code, unresolved TODOs, disabled tests, warning suppressions, and hardcoded secrets. Invoke `requesting-code-review` and use a reviewer model different from the implementation author: Claude reviews Codex-authored work; `codex review --base master` reviews Claude-authored work. Fix every actionable finding and rerun the affected checks.

- [ ] **Final exact-diff verification**

Confirm every spec acceptance criterion maps to passing evidence: shared eligibility, excluded static/const/readonly fields, nested wrapper identity, generic constraints/modifiers, unique hints, `NOTIFY006` plus code fix, compilation-clean tests, consumer run, and successful pack. Do not change release metadata, samples, changelog, suppression behavior, string-target diagnostics, or benchmarks in this branch.
