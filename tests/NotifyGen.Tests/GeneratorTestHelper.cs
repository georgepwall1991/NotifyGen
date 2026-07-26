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
    ) RunGenerator(string source)
    {
        // Create syntax tree
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Create compilation with references
        var references = References;

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            ).WithNullableContextOptions(NullableContextOptions.Enable)
        );

        // Create generator driver
        var generator = new NotifyGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run generator
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
    ) RunGeneratorAndAssertCompiles(string source)
    {
        var result = RunGenerator(source);
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
