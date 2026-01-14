using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NotifyGen.Generator;

namespace NotifyGen.Tests;

/// <summary>
/// Helper class for running the source generator in tests.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Runs the NotifyGenerator on the provided source code.
    /// </summary>
    public static (Compilation OutputCompilation, ImmutableArray<Diagnostic> Diagnostics, GeneratorDriverRunResult RunResult)
        RunGenerator(string source)
    {
        // Create syntax tree
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Create compilation with references
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location),
        };

        // Add System.Runtime reference for netstandard compatibility
        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (runtimeAssembly != null)
        {
            references = references.Append(MetadataReference.CreateFromFile(runtimeAssembly.Location)).ToArray();
        }

        // Add netstandard reference
        var netstandardAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "netstandard");
        if (netstandardAssembly != null)
        {
            references = references.Append(MetadataReference.CreateFromFile(netstandardAssembly.Location)).ToArray();
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        // Create generator driver
        var generator = new NotifyGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run generator
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        var runResult = driver.GetRunResult();

        return (outputCompilation, diagnostics, runResult);
    }

    /// <summary>
    /// Gets the generated source for a specific file.
    /// </summary>
    public static string? GetGeneratedSource(GeneratorDriverRunResult runResult, string fileName)
    {
        return runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith(fileName))
            ?.GetText()
            .ToString();
    }
}
