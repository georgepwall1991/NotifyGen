using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NotifyGen.Generator;
using Xunit;

namespace NotifyGen.Tests;

public class Cycle5Tests
{
    [Fact]
    public void Generator_ForwardsPropertyGetAndSetAttributeTargets()
    {
        const string source = """
            using System;
            using System.Diagnostics.CodeAnalysis;
            using System.Text.Json.Serialization;
            using NotifyGen;

            namespace AccessorFixture;

            [Notify]
            public partial class Person
            {
                [property: JsonPropertyName("display_name")]
                [get: Obsolete("prefer DisplayLabel")]
                [set: MemberNotNull(nameof(_displayName))]
                private string _displayName = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(
            result.RunResult,
            "Person.g.cs"
        );

        generated.Should().NotBeNull();
        generated.Should().Contain(
            "[global::System.Text.Json.Serialization.JsonPropertyNameAttribute(\"display_name\")]"
        );
        generated.Should().Contain(
            "[global::System.ObsoleteAttribute(\"prefer DisplayLabel\")]"
        );
        generated.Should().Contain(
            "[global::System.Diagnostics.CodeAnalysis.MemberNotNullAttribute(\"_displayName\")]"
        );
        generated.Should().Contain("get");
        generated.Should().Contain("set");

        // Property attribute must appear before the property declaration.
        var jsonIndex = generated!.IndexOf("JsonPropertyNameAttribute", StringComparison.Ordinal);
        var propertyIndex = generated.IndexOf("public string DisplayName", StringComparison.Ordinal);
        var obsoleteIndex = generated.IndexOf("ObsoleteAttribute", StringComparison.Ordinal);
        var memberNotNullIndex = generated.IndexOf("MemberNotNullAttribute", StringComparison.Ordinal);
        jsonIndex.Should().BeLessThan(propertyIndex);
        obsoleteIndex.Should().BeGreaterThan(propertyIndex);
        memberNotNullIndex.Should().BeGreaterThan(obsoleteIndex);

        using var assemblyStream = new System.IO.MemoryStream();
        result.OutputCompilation.Emit(assemblyStream).Success.Should().BeTrue();
        var type = Assembly.Load(assemblyStream.ToArray()).GetType("AccessorFixture.Person")!;
        var property = type.GetProperty("DisplayName")!;
        property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name.Should().Be("display_name");
    }

    [Fact]
    public void Generator_CombinesUntargetedAndPropertyTargetAttributes()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;
            using System.Text.Json.Serialization;
            using NotifyGen;

            [Notify]
            public partial class Entity
            {
                [Required]
                [property: JsonPropertyName("title")]
                private string _title = string.Empty;
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(result.RunResult, "Entity.g.cs");
        generated.Should().Contain("RequiredAttribute");
        generated.Should().Contain("JsonPropertyNameAttribute(\"title\")");
    }

    [Fact]
    public async Task Suppressor_SuppressesCs0657AndCs0658ForNotifyFields()
    {
        const string source = """
            using System;
            using System.Text.Json.Serialization;
            using NotifyGen;

            [Notify]
            public partial class Person
            {
                [property: JsonPropertyName("name")]
                [get: Obsolete]
                [set: Obsolete]
                private string _name = string.Empty;
            }
            """;

        var compilation = CreateCompilation(source);
        var withoutSuppressor = compilation.GetDiagnostics()
            .Where(d => d.Id is "CS0657" or "CS0658")
            .Select(d => d.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        withoutSuppressor.Should().Contain("CS0657");
        withoutSuppressor.Should().Contain("CS0658");

        var analyzers = new DiagnosticAnalyzer[] { new AccessorTargetDiagnosticSuppressor() };
        var withSuppressor = (
            await compilation
                .WithAnalyzers(System.Collections.Immutable.ImmutableArray.Create(analyzers))
                .GetAllDiagnosticsAsync()
        )
            .Where(d => d.Id is "CS0657" or "CS0658")
            .ToArray();

        withSuppressor.Should().BeEmpty();
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source);
        var references = (
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("missing platform assemblies")
        )
            .Split(Path.PathSeparator)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(NotifyAttribute).Assembly.Location));

        return Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "SuppressorTest",
            new[] { syntaxTree },
            references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary
            )
        );
    }
}
