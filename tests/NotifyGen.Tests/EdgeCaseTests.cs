using FluentAssertions;
using Microsoft.CodeAnalysis;
using NotifyGen.Generator;

namespace NotifyGen.Tests;

/// <summary>
/// Tests for edge cases and advanced scenarios.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void Generator_WithGenericClass_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Wrapper<T>
                {
                    private T _value;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Wrapper.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource
            .Should()
            .Contain("public partial class Wrapper<T> : INotifyPropertyChanged");
        generatedSource.Should().Contain("public T Value");
    }

    [Fact]
    public void Generator_WithMultipleNotifyAlso_GeneratesAllNotifications()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    [NotifyAlso("FullName")]
                    [NotifyAlso("DisplayName")]
                    [NotifyAlso("Greeting")]
                    private string _firstName;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("OnPropertyChanged(\"FullName\")");
        generatedSource.Should().Contain("OnPropertyChanged(\"DisplayName\")");
        generatedSource.Should().Contain("OnPropertyChanged(\"Greeting\")");
    }

    [Fact]
    public void Generator_WithLongFieldName_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Config
                {
                    private string _veryLongFieldNameThatExceedsTypicalNamingConventionsButShouldStillWork;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Config.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource
            .Should()
            .Contain(
                "public string VeryLongFieldNameThatExceedsTypicalNamingConventionsButShouldStillWork"
            );
    }

    [Fact]
    public void Generator_WithNestedClass_GeneratesCorrectly()
    {
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

        outputCompilation
            .GetTypeByMetadataName("TestNamespace.Outer+Inner")!
            .GetMembers("Value")
            .Should()
            .ContainSingle();
        outputCompilation.GetTypeByMetadataName("TestNamespace.Inner").Should().BeNull();
    }

    [Fact]
    public void Generator_WithMixedFields_OnlyGeneratesUnderscoreFields()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Mixed
                {
                    private string _validField = "";
                    private string noUnderscoreField = "";
                    public string PublicField = "";
                    protected string ProtectedField = "";
                    internal string InternalField = "";
                    private readonly string _readonlyField = "";
                    private const string _constField = "";
                    private static string _staticField = "";
                }
            }
            """;

        // Act
        var (outputCompilation, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(
            source
        );

        // Assert
        var generatedSource = runResult
            .Results.Single()
            .GeneratedSources.Single()
            .SourceText.ToString();
        generatedSource.Should().Contain("public string ValidField");
        generatedSource.Should().NotContain("NoUnderscoreField");
        generatedSource.Should().NotContain("PublicField");
        generatedSource.Should().NotContain("ProtectedField");
        generatedSource.Should().NotContain("InternalField");
        generatedSource.Should().NotContain("ReadonlyField");
        generatedSource.Should().NotContain("ConstField");
        generatedSource.Should().NotContain("StaticField");
        outputCompilation
            .GetTypeByMetadataName("TestNamespace.Mixed")!
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(static property => property.Name)
            .Should()
            .Equal("ValidField");
    }

    [Fact]
    public void Generator_WithValueTypes_GeneratesCorrectEqualityGuards()
    {
        // Arrange
        var source = """
            using NotifyGen;
            using System;

            namespace TestNamespace
            {
                [Notify]
                public partial class ValueTypes
                {
                    private int _intValue;
                    private double _doubleValue;
                    private decimal _decimalValue;
                    private DateTime _dateValue;
                    private Guid _guidValue;
                    private bool _boolValue;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "ValueTypes.g.cs");
        generatedSource.Should().NotBeNull();
        // Primitive types use direct == for performance
        generatedSource.Should().Contain("if (_intValue == value) return;");
        generatedSource.Should().Contain("if (_doubleValue == value) return;");
        generatedSource.Should().Contain("if (_decimalValue == value) return;");
        generatedSource.Should().Contain("if (_boolValue == value) return;");
        // Complex value types (DateTime, Guid) still use EqualityComparer
        generatedSource.Should().Contain("EqualityComparer<System.DateTime>.Default.Equals");
        generatedSource.Should().Contain("EqualityComparer<System.Guid>.Default.Equals");
    }

    [Fact]
    public void Generator_WithCollectionTypes_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;
            using System.Collections.Generic;

            namespace TestNamespace
            {
                [Notify]
                public partial class Collections
                {
                    private List<string> _items;
                    private Dictionary<string, int> _lookup;
                    private string[] _array;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Collections.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("List<string> Items");
        generatedSource.Should().Contain("Dictionary<string, int> Lookup");
        generatedSource.Should().Contain("string[] Array");
    }

    [Fact]
    public void Generator_WithSingleUnderscoreField_SkipsIt()
    {
        // Arrange - field name "_" should be skipped (no valid property name)
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Edge
                {
                    private string _;
                    private string _a;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Edge.g.cs");
        generatedSource.Should().NotBeNull();
        // Should have A property but not a property for "_"
        generatedSource.Should().Contain("public string A");
        // Count properties - should only be one
        var propertyCount = generatedSource!.Split("public string").Length - 1;
        propertyCount.Should().Be(1);
    }

    [Fact]
    public void Generator_WithNestedGenericType_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;
            using System.Collections.Generic;

            namespace TestNamespace
            {
                [Notify]
                public partial class ComplexTypes
                {
                    private List<Dictionary<string, int>> _nestedGeneric;
                    private Dictionary<string, List<int>> _anotherNested;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "ComplexTypes.g.cs"
        );
        generatedSource.Should().NotBeNull();
        // Generator outputs fully qualified type names
        generatedSource.Should().Contain("NestedGeneric");
        generatedSource.Should().Contain("AnotherNested");
        generatedSource.Should().Contain("List<");
        generatedSource.Should().Contain("Dictionary<");
    }

    [Fact]
    public void Generator_WithFileScopedNamespace_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace;

            [Notify]
            public partial class FileScopedPerson
            {
                private string _name;
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "FileScopedPerson.g.cs"
        );
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("namespace TestNamespace");
        generatedSource
            .Should()
            .Contain("public partial class FileScopedPerson : INotifyPropertyChanged");
        generatedSource.Should().Contain("public string Name");
    }

    [Fact]
    public void Generator_WithNullableValueType_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class NullableValues
                {
                    private int? _nullableInt;
                    private double? _nullableDouble;
                    private System.DateTime? _nullableDateTime;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "NullableValues.g.cs"
        );
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("int? NullableInt");
        generatedSource.Should().Contain("double? NullableDouble");
    }

    [Fact]
    public void Generator_WithTupleType_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class TupleContainer
                {
                    private (string, int) _simpleTuple;
                    private (string Name, int Age) _namedTuple;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "TupleContainer.g.cs"
        );
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("SimpleTuple");
        generatedSource.Should().Contain("NamedTuple");
    }

    [Fact]
    public void Generator_WithFieldNameStartingWithMultipleUnderscores_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class UnderscoreEdge
                {
                    private string __doubleUnderscore;
                    private string ___tripleUnderscore;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "UnderscoreEdge.g.cs"
        );
        generatedSource.Should().NotBeNull();
        // The generator converts _name to Name, so __name becomes _name (property name)
        generatedSource.Should().Contain("public string _doubleUnderscore");
        generatedSource.Should().Contain("public string __tripleUnderscore");
    }

    [Fact]
    public void Generator_WithNumericFieldNameSuffix_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class NumericFields
                {
                    private string _field1;
                    private string _field2;
                    private int _value123;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "NumericFields.g.cs"
        );
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public string Field1");
        generatedSource.Should().Contain("public string Field2");
        generatedSource.Should().Contain("public int Value123");
    }

    [Fact]
    public void Generator_WithCombinedAttributes_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class CombinedAttrs
                {
                    [NotifyName("DisplayName")]
                    [NotifyAlso("FullTitle")]
                    [NotifySetter(AccessLevel.Private)]
                    private string _internalName;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "CombinedAttrs.g.cs"
        );
        generatedSource.Should().NotBeNull();
        // Should use custom name
        generatedSource.Should().Contain("public string DisplayName");
        generatedSource.Should().NotContain("InternalName");
        // Should have private setter
        generatedSource.Should().Contain("private set");
        // Should notify FullTitle
        generatedSource.Should().Contain("OnPropertyChanged(\"FullTitle\")");
        // Hooks should use the custom name
        generatedSource.Should().Contain("OnDisplayNameChanging");
        generatedSource.Should().Contain("OnDisplayNameChanged");
    }

    [Fact]
    public void Generator_WithCustomTypeFromDifferentNamespace_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;
            using System.Text.RegularExpressions;

            namespace TestNamespace
            {
                [Notify]
                public partial class RegexContainer
                {
                    private Regex _pattern;
                    private System.Uri _uri;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        // May have warnings about types, but should compile
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(
            runResult,
            "RegexContainer.g.cs"
        );
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("Pattern");
        generatedSource.Should().Contain("Uri");
    }

    [Fact]
    public void Generator_WithArrayTypes_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class ArrayTypes
                {
                    private int[] _numbers;
                    private string[][] _jaggedArray;
                    private int[,] _multiDimensional;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "ArrayTypes.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("int[] Numbers");
        generatedSource.Should().Contain("string[][] JaggedArray");
        generatedSource.Should().Contain("int[,] MultiDimensional");
    }

    [Fact]
    public void Generator_WithDeeplyNestedClass_GeneratesCorrectly()
    {
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                public partial class Level1
                {
                    public partial class Level2
                    {
                        public partial class Level3
                        {
                            [Notify]
                            public partial class DeepNested
                            {
                                private string _value = "";
                            }
                        }
                    }
                }
            }
            """;

        var (outputCompilation, _, _) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);

        outputCompilation
            .GetTypeByMetadataName("TestNamespace.Level1+Level2+Level3+DeepNested")!
            .GetMembers("Value")
            .Should()
            .ContainSingle();
        outputCompilation.GetTypeByMetadataName("TestNamespace.DeepNested").Should().BeNull();
    }

    [Fact]
    public void Generator_WithSameSimpleName_UsesUniqueSourceHints()
    {
        var source = """
            using NotifyGen;

            namespace A
            {
                [Notify]
                public partial class Model
                {
                    private string _name = "";
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

            namespace C
            {
                public partial class OuterA
                {
                    [Notify]
                    public partial class Model
                    {
                        private string _name = "";
                    }
                }

                public partial struct OuterB
                {
                    [Notify]
                    public partial class Model
                    {
                        private string _name = "";
                    }
                }
            }
            """;

        var (outputCompilation, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(
            source
        );
        var generatedSources = runResult.Results.Single().GeneratedSources;

        generatedSources.Should().HaveCount(4);
        generatedSources.Select(static result => result.HintName).Should().OnlyHaveUniqueItems();
        outputCompilation
            .GetTypeByMetadataName("A.Model")!
            .GetMembers("Name")
            .Should()
            .ContainSingle();
        outputCompilation
            .GetTypeByMetadataName("B.Model")!
            .GetMembers("Name")
            .Should()
            .ContainSingle();
        outputCompilation
            .GetTypeByMetadataName("C.OuterA+Model")!
            .GetMembers("Name")
            .Should()
            .ContainSingle();
        outputCompilation
            .GetTypeByMetadataName("C.OuterB+Model")!
            .GetMembers("Name")
            .Should()
            .ContainSingle();
    }

    [Theory]
    [InlineData("public partial class Container")]
    [InlineData("public partial struct Container")]
    [InlineData("public partial record class Container")]
    [InlineData("public partial record struct Container")]
    [InlineData("public partial interface Container")]
    [InlineData("internal partial class Container")]
    [InlineData("public static partial class Container")]
    [InlineData("public abstract partial class Container")]
    [InlineData("public sealed partial class Container")]
    [InlineData("public readonly partial struct Container")]
    [InlineData("public ref partial struct Container")]
    public void Generator_WithSupportedContainingType_GeneratesOnNestedType(
        string containerDeclaration
    )
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

        outputCompilation
            .GetTypeByMetadataName("TestNamespace.Container+Inner")!
            .GetMembers("Value")
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Generator_WithGenericContainingType_PreservesDeclarationShape()
    {
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                public static partial class Outer<T> where T : class, new()
                {
                    [Notify]
                    public partial class Inner<TValue> where TValue : struct
                    {
                        private TValue _value;
                    }
                }
            }
            """;

        var (outputCompilation, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(
            source
        );
        var generatedSource = runResult
            .Results.Single()
            .GeneratedSources.Single()
            .SourceText.ToString();

        outputCompilation
            .GetTypeByMetadataName("TestNamespace.Outer`1+Inner`1")!
            .GetMembers("Value")
            .Should()
            .ContainSingle();
        generatedSource.Should().Contain("public static partial class Outer<T>");
        generatedSource.Should().Contain("public partial class Inner<TValue>");
    }

    [Fact]
    public void Generator_WithEscapedIdentifiers_PreservesSourceSyntax()
    {
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                public partial interface @interface<@class>
                {
                    [Notify]
                    public partial class Inner<@struct> where @struct : struct
                    {
                        private @struct _value;
                    }
                }
            }
            """;

        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var generatedSource = runResult
            .Results.Single()
            .GeneratedSources.Single()
            .SourceText.ToString();

        generatedSource.Should().Contain("public partial interface @interface<@class>");
        generatedSource.Should().Contain("public partial class Inner<@struct>");
    }

    [Fact]
    public void Generator_SourceHints_AreUniqueUnderOrdinalIgnoreCaseComparison()
    {
        var source = """
            using NotifyGen;

            namespace AAG
            {
                [Notify]
                public partial class A
                {
                    private int _value;
                }
            }

            namespace AAa
            {
                [Notify]
                public partial class A
                {
                    private int _value;
                }
            }
            """;

        var (_, _, runResult) = GeneratorTestHelper.RunGeneratorAndAssertCompiles(source);
        var hintNames = runResult
            .Results.Single()
            .GeneratedSources.Select(static sourceResult => sourceResult.HintName);

        hintNames.Distinct(StringComparer.OrdinalIgnoreCase).Should().HaveCount(2);
    }

    [Fact]
    public void SourceHintName_WithLongIdentity_BoundsEveryPathSegment()
    {
        var hintName = SourceHintName.Create(new string('N', 300) + ".Model", new string('T', 300));

        hintName.Split('/').Should().OnlyContain(static segment => segment.Length <= 100);
        hintName.Should().EndWith("/Type.g.cs");
    }
}
