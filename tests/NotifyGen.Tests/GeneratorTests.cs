using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace NotifyGen.Tests;

public class GeneratorTests
{
    [Fact]
    public void Generator_WithSimpleClass_GeneratesPropertyAndInpc()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    private string _name;
                    private int _age;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public partial class Person : INotifyPropertyChanged");
        generatedSource.Should().Contain("public string Name");
        generatedSource.Should().Contain("public int Age");
        generatedSource.Should().Contain("public event PropertyChangedEventHandler? PropertyChanged");
        generatedSource.Should().Contain("partial void OnNameChanged()");
        generatedSource.Should().Contain("partial void OnAgeChanged()");
    }

    [Fact]
    public void Generator_WithNullableField_HandlesNullability()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    private string? _email;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public string? Email");
    }

    [Fact]
    public void Generator_WithNotifyIgnore_ExcludesField()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    private string _name;

                    [NotifyIgnore]
                    private string _internalId;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public string Name");
        generatedSource.Should().NotContain("InternalId");
    }

    [Fact]
    public void Generator_WithNotifyAlso_AddsAdditionalNotifications()
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
                    private string _firstName;

                    [NotifyAlso("FullName")]
                    private string _lastName;
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
    }

    [Fact]
    public void Generator_WithNoUnderscoredFields_GeneratesNoProperties()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Empty
                {
                    public string publicField;
                    private string noUnderscore;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        // Should not generate any file since there are no valid fields
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Empty.g.cs");
        generatedSource.Should().BeNull();
    }

    [Fact]
    public void Generator_GeneratesEqualityGuard()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Person
                {
                    private string _name;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("EqualityComparer<");
        generatedSource.Should().Contain(".Equals(_name, value)");
    }

    [Fact]
    public void Generator_WithGlobalNamespace_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            [Notify]
            public partial class GlobalPerson
            {
                private string _name;
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "GlobalPerson.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public partial class GlobalPerson : INotifyPropertyChanged");
        generatedSource.Should().NotContain("namespace");
    }

    [Fact]
    public void Generator_WithInternalClass_PreservesAccessibility()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                internal partial class InternalPerson
                {
                    private string _name;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "InternalPerson.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("internal partial class InternalPerson : INotifyPropertyChanged");
    }
}
