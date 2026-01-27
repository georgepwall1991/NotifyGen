using FluentAssertions;
using Microsoft.CodeAnalysis;

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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Wrapper.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public partial class Wrapper<T> : INotifyPropertyChanged");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Config.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public string VeryLongFieldNameThatExceedsTypicalNamingConventionsButShouldStillWork");
    }

    [Fact]
    public void Generator_WithNestedClass_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                public class Outer
                {
                    [Notify]
                    public partial class Inner
                    {
                        private string _value;
                    }
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Inner.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public partial class Inner : INotifyPropertyChanged");
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
                    private string _validField;
                    private string noUnderscoreField;
                    public string PublicField;
                    protected string ProtectedField;
                    internal string InternalField;
                    private readonly string _readonlyField;
                    private const string ConstField = "const";
                    private static string _staticField;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Mixed.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public string ValidField");
        generatedSource.Should().NotContain("NoUnderscoreField");
        generatedSource.Should().NotContain("noUnderscoreField");
        generatedSource.Should().NotContain("PublicField");
        generatedSource.Should().NotContain("ProtectedField");
        generatedSource.Should().NotContain("InternalField");
        // Note: readonly and static fields are skipped by the compiler
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "ComplexTypes.g.cs");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "FileScopedPerson.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("namespace TestNamespace");
        generatedSource.Should().Contain("public partial class FileScopedPerson : INotifyPropertyChanged");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "NullableValues.g.cs");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "TupleContainer.g.cs");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "UnderscoreEdge.g.cs");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "NumericFields.g.cs");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "CombinedAttrs.g.cs");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        // May have warnings about types, but should compile
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "RegexContainer.g.cs");
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
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

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
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                public class Level1
                {
                    public class Level2
                    {
                        public class Level3
                        {
                            [Notify]
                            public partial class DeepNested
                            {
                                private string _value;
                            }
                        }
                    }
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "DeepNested.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public partial class DeepNested : INotifyPropertyChanged");
        generatedSource.Should().Contain("public string Value");
    }
}
