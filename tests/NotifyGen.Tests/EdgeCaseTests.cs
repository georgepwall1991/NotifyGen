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
        generatedSource.Should().Contain("EqualityComparer<int>.Default.Equals");
        generatedSource.Should().Contain("EqualityComparer<double>.Default.Equals");
        generatedSource.Should().Contain("EqualityComparer<decimal>.Default.Equals");
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
}
