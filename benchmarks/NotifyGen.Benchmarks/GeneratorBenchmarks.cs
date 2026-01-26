using System.Collections.Immutable;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NotifyGen.Generator;

namespace NotifyGen.Benchmarks;

/// <summary>
/// Benchmarks for the NotifyGen source generator execution time.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class GeneratorBenchmarks
{
    private Compilation _compilation1Class = null!;
    private Compilation _compilation10Classes = null!;
    private Compilation _compilation100Classes = null!;
    private GeneratorDriver _driver = null!;

    [GlobalSetup]
    public void Setup()
    {
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location),
        };

        // Add runtime references
        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (runtimeAssembly != null)
            references = references.Append(MetadataReference.CreateFromFile(runtimeAssembly.Location)).ToArray();

        var netstandardAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "netstandard");
        if (netstandardAssembly != null)
            references = references.Append(MetadataReference.CreateFromFile(netstandardAssembly.Location)).ToArray();

        _compilation1Class = CreateCompilation(GenerateSource(1), references);
        _compilation10Classes = CreateCompilation(GenerateSource(10), references);
        _compilation100Classes = CreateCompilation(GenerateSource(100), references);

        _driver = CSharpGeneratorDriver.Create(new NotifyGenerator());
    }

    [Benchmark]
    public GeneratorDriverRunResult Generate_1Class()
    {
        var driver = _driver.RunGenerators(_compilation1Class);
        return driver.GetRunResult();
    }

    [Benchmark]
    public GeneratorDriverRunResult Generate_10Classes()
    {
        var driver = _driver.RunGenerators(_compilation10Classes);
        return driver.GetRunResult();
    }

    [Benchmark]
    public GeneratorDriverRunResult Generate_100Classes()
    {
        var driver = _driver.RunGenerators(_compilation100Classes);
        return driver.GetRunResult();
    }

    [Benchmark]
    public GeneratorDriverRunResult IncrementalRebuild_1ClassChange()
    {
        // Simulate incremental build - run once, then modify and run again
        var driver = _driver.RunGenerators(_compilation10Classes);

        // Modify one class (change a field name)
        var modifiedSource = GenerateSource(10, modifyClass: 5);
        var modifiedCompilation = CreateCompilation(modifiedSource, _compilation10Classes.References.ToArray());

        driver = driver.RunGenerators(modifiedCompilation);
        return driver.GetRunResult();
    }

    private static Compilation CreateCompilation(string source, MetadataReference[] references)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            "BenchmarkAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    private static string GenerateSource(int classCount, int? modifyClass = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using NotifyGen;");
        sb.AppendLine();
        sb.AppendLine("namespace BenchmarkNamespace");
        sb.AppendLine("{");

        for (var i = 0; i < classCount; i++)
        {
            var fieldSuffix = modifyClass == i ? "_modified" : "";
            sb.AppendLine($"    [Notify]");
            sb.AppendLine($"    public partial class ViewModel{i}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private string _name{fieldSuffix} = \"\";");
            sb.AppendLine($"        private int _age{fieldSuffix};");
            sb.AppendLine($"        private string? _email{fieldSuffix};");
            sb.AppendLine($"        private bool _isActive{fieldSuffix};");
            sb.AppendLine($"        private double _score{fieldSuffix};");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
