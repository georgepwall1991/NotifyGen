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
    private Compilation _compilation100Computed = null!;
    private Compilation _compilation100Explicit = null!;
    private Compilation _compilation100ComputedEdited = null!;
    private GeneratorDriver _driver = null!;
    private GeneratorDriver _warmComputedDriver = null!;

    [GlobalSetup]
    public void Setup()
    {
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location
            ),
            MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location),
        };

        // Add runtime references
        var runtimeAssembly = AppDomain
            .CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (runtimeAssembly != null)
            references = references
                .Append(MetadataReference.CreateFromFile(runtimeAssembly.Location))
                .ToArray();

        var netstandardAssembly = AppDomain
            .CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "netstandard");
        if (netstandardAssembly != null)
            references = references
                .Append(MetadataReference.CreateFromFile(netstandardAssembly.Location))
                .ToArray();

        _compilation1Class = CreateCompilation(GenerateSource(1), references);
        _compilation10Classes = CreateCompilation(GenerateSource(10), references);
        _compilation100Classes = CreateCompilation(GenerateSource(100), references);
        _compilation100Computed = CreateCompilation(GenerateComputedSource(100), references);
        _compilation100Explicit = CreateCompilation(GenerateExplicitAlsoSource(100), references);
        _compilation100ComputedEdited = CreateCompilation(
            GenerateComputedSource(100, editGetter: true),
            references
        );

        _driver = CSharpGeneratorDriver.Create(new NotifyGenerator());
        _warmComputedDriver = CSharpGeneratorDriver
            .Create(new NotifyGenerator())
            .RunGenerators(_compilation100Computed);
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
    public GeneratorDriverRunResult Generate_100Classes_WithNotifyComputed()
    {
        var driver = _driver.RunGenerators(_compilation100Computed);
        return driver.GetRunResult();
    }

    [Benchmark]
    public GeneratorDriverRunResult Generate_100Classes_WithExplicitNotifyAlso()
    {
        var driver = _driver.RunGenerators(_compilation100Explicit);
        return driver.GetRunResult();
    }

    [Benchmark]
    public GeneratorDriverRunResult IncrementalRebuild_ComputedGetterChange()
    {
        return _warmComputedDriver.RunGenerators(_compilation100ComputedEdited).GetRunResult();
    }

    [Benchmark]
    public GeneratorDriverRunResult IncrementalRebuild_1ClassChange()
    {
        // Simulate incremental build - run once, then modify and run again
        var driver = _driver.RunGenerators(_compilation10Classes);

        // Modify one class (change a field name)
        var modifiedSource = GenerateSource(10, modifyClass: 5);
        var modifiedCompilation = CreateCompilation(
            modifiedSource,
            _compilation10Classes.References.ToArray()
        );

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
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            ).WithNullableContextOptions(NullableContextOptions.Enable)
        );
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

    private static string GenerateComputedSource(int classCount, bool editGetter = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using NotifyGen;");
        sb.AppendLine();
        sb.AppendLine("namespace BenchmarkNamespace");
        sb.AppendLine("{");

        for (var i = 0; i < classCount; i++)
        {
            var first = editGetter && i == 0 ? "LastName" : "FirstName";
            sb.AppendLine("    [Notify]");
            sb.AppendLine($"    public partial class ComputedViewModel{i}");
            sb.AppendLine("    {");
            sb.AppendLine("        private string _firstName = \"\";");
            sb.AppendLine("        private string _lastName = \"\";");
            sb.AppendLine("        private string _title = \"\";");
            sb.AppendLine("        private string _city = \"\";");
            sb.AppendLine("        private string _note = \"\";");
            sb.AppendLine();
            sb.AppendLine("        [NotifyComputed]");
            sb.AppendLine($"        public string FullName => $\"{{{first}}} {{LastName}}\";");
            sb.AppendLine("        [NotifyComputed]");
            sb.AppendLine("        public string Headline => $\"{Title} {FullName}\";");
            sb.AppendLine("        [NotifyComputed]");
            sb.AppendLine("        public string Location => City;");
            sb.AppendLine("        [NotifyComputed]");
            sb.AppendLine("        public string Summary => Note;");
            sb.AppendLine("        [NotifyComputed]");
            sb.AppendLine("        public string Label => Title;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateExplicitAlsoSource(int classCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using NotifyGen;");
        sb.AppendLine();
        sb.AppendLine("namespace BenchmarkNamespace");
        sb.AppendLine("{");

        for (var i = 0; i < classCount; i++)
        {
            sb.AppendLine("    [Notify]");
            sb.AppendLine($"    public partial class ExplicitViewModel{i}");
            sb.AppendLine("    {");
            sb.AppendLine("        [NotifyAlso(nameof(FullName))]");
            sb.AppendLine("        [NotifyAlso(nameof(Headline))]");
            sb.AppendLine("        private string _firstName = \"\";");
            sb.AppendLine("        [NotifyAlso(nameof(FullName))]");
            sb.AppendLine("        [NotifyAlso(nameof(Headline))]");
            sb.AppendLine("        private string _lastName = \"\";");
            sb.AppendLine("        [NotifyAlso(nameof(Headline))]");
            sb.AppendLine("        [NotifyAlso(nameof(Label))]");
            sb.AppendLine("        private string _title = \"\";");
            sb.AppendLine("        [NotifyAlso(nameof(Location))]");
            sb.AppendLine("        private string _city = \"\";");
            sb.AppendLine("        [NotifyAlso(nameof(Summary))]");
            sb.AppendLine("        private string _note = \"\";");
            sb.AppendLine();
            sb.AppendLine("        public string FullName => $\"{FirstName} {LastName}\";");
            sb.AppendLine("        public string Headline => $\"{Title} {FullName}\";");
            sb.AppendLine("        public string Location => City;");
            sb.AppendLine("        public string Summary => Note;");
            sb.AppendLine("        public string Label => Title;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
