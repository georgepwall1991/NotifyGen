using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NotifyGen.Generator;

namespace NotifyGen.Tests;

/// <summary>
/// Helper class for running the source generator in tests.
/// </summary>
public static class GeneratorTestHelper
{
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var platformAssemblies =
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException(
                "The .NET runtime did not provide trusted platform assemblies."
            );

        return platformAssemblies
            .Split(Path.PathSeparator)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location))
            .ToImmutableArray();
    }

    /// <summary>
    /// Runs the NotifyGenerator on the provided source code.
    /// </summary>
    public static (
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> Diagnostics,
        GeneratorDriverRunResult RunResult
    ) RunGenerator(string source, bool allowUnsafe = false)
    {
        return RunGeneratorCore(source, LanguageVersion.Default, allowUnsafe);
    }

    /// <summary>
    /// Runs the generator with a specific C# language version.
    /// </summary>
    public static (
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> Diagnostics,
        GeneratorDriverRunResult RunResult
    ) RunGeneratorWithLanguageVersion(
        string source,
        LanguageVersion languageVersion,
        bool allowUnsafe = false
    )
    {
        return RunGeneratorCore(source, languageVersion, allowUnsafe);
    }

    private static (
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> Diagnostics,
        GeneratorDriverRunResult RunResult
    ) RunGeneratorCore(string source, LanguageVersion languageVersion, bool allowUnsafe)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(languageVersion)
        );

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithAllowUnsafe(allowUnsafe)
        );

        var generator = new NotifyGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            parseOptions: new CSharpParseOptions(languageVersion)
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );
        var runResult = driver.GetRunResult();

        return (outputCompilation, diagnostics, runResult);
    }

    /// <summary>
    /// Runs the NotifyGenerator and asserts that the updated compilation has no errors.
    /// </summary>
    public static (
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> Diagnostics,
        GeneratorDriverRunResult RunResult
    ) RunGeneratorAndAssertCompiles(string source, bool allowUnsafe = false)
    {
        return RunGeneratorAndAssertCompilesCore(
            RunGenerator(source, allowUnsafe)
        );
    }

    /// <summary>
    /// Runs the generator with a language version and asserts that the output compiles.
    /// </summary>
    public static (
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> Diagnostics,
        GeneratorDriverRunResult RunResult
    ) RunGeneratorWithLanguageVersionAndAssertCompiles(
        string source,
        LanguageVersion languageVersion,
        bool allowUnsafe = false
    )
    {
        return RunGeneratorAndAssertCompilesCore(
            RunGeneratorWithLanguageVersion(source, languageVersion, allowUnsafe)
        );
    }

    private static (
        Compilation OutputCompilation,
        ImmutableArray<Diagnostic> Diagnostics,
        GeneratorDriverRunResult RunResult
    ) RunGeneratorAndAssertCompilesCore(
        (
            Compilation OutputCompilation,
            ImmutableArray<Diagnostic> Diagnostics,
            GeneratorDriverRunResult RunResult
        ) result
    )
    {
        var errors = result
            .OutputCompilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        errors.Should().BeEmpty("valid source plus generated output must compile");
        return result;
    }

    /// <summary>
    /// Gets the generated source for a specific file.
    /// </summary>
    public static string? GetGeneratedSource(GeneratorDriverRunResult runResult, string fileName)
    {
        return runResult
            .GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith(fileName))
            ?.GetText()
            .ToString();
    }
}
