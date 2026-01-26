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
        generatedSource.Should().Contain("partial void OnNameChanging(string oldValue, string newValue)");
        generatedSource.Should().Contain("partial void OnNameChanged()");
        generatedSource.Should().Contain("partial void OnAgeChanging(int oldValue, int newValue)");
        generatedSource.Should().Contain("partial void OnAgeChanged()");
    }

    [Fact]
    public void Generator_OnChangingHook_CalledBeforeAssignment()
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

        // OnChanging should be called with old and new values
        generatedSource.Should().Contain("OnNameChanging(_name, value)");

        // Verify the order: OnChanging -> assignment -> OnPropertyChanged -> OnChanged
        var changingIndex = generatedSource!.IndexOf("OnNameChanging(_name, value)");
        var assignmentIndex = generatedSource.IndexOf("_name = value");
        var propertyChangedIndex = generatedSource.IndexOf("OnPropertyChanged()");
        var changedIndex = generatedSource.IndexOf("OnNameChanged()");

        changingIndex.Should().BeLessThan(assignmentIndex, "OnChanging should be called before assignment");
        assignmentIndex.Should().BeLessThan(propertyChangedIndex, "assignment should happen before OnPropertyChanged");
        propertyChangedIndex.Should().BeLessThan(changedIndex, "OnPropertyChanged should be called before OnChanged");
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
    public void Generator_WithNotifyName_UsesCustomPropertyName()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Settings
                {
                    [NotifyName("IsVisible")]
                    private bool _visibleState;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Settings.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public bool IsVisible");
        generatedSource.Should().NotContain("VisibleState");
        generatedSource.Should().Contain("OnIsVisibleChanging");
        generatedSource.Should().Contain("OnIsVisibleChanged");
    }

    [Fact]
    public void Generator_WithNotifySetter_GeneratesPrivateSetter()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Entity
                {
                    [NotifySetter(AccessLevel.Private)]
                    private int _id;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Entity.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public int Id");
        generatedSource.Should().Contain("private set");
    }

    [Fact]
    public void Generator_WithNotifySetter_GeneratesProtectedSetter()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class BaseEntity
                {
                    [NotifySetter(AccessLevel.Protected)]
                    private string _name;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "BaseEntity.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public string Name");
        generatedSource.Should().Contain("protected set");
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

    [Theory]
    [InlineData(AccessLevel.Public, "")]
    [InlineData(AccessLevel.Protected, "protected")]
    [InlineData(AccessLevel.Internal, "internal")]
    [InlineData(AccessLevel.Private, "private")]
    [InlineData(AccessLevel.ProtectedInternal, "protected internal")]
    [InlineData(AccessLevel.PrivateProtected, "private protected")]
    public void Generator_WithNotifySetter_GeneratesCorrectAccessModifier(AccessLevel level, string expected)
    {
        // Arrange
        var accessLevelValue = (int)level;
        var source = $$"""
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class Entity
                {
                    [NotifySetter((AccessLevel){{accessLevelValue}})]
                    private int _id;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Entity.g.cs");
        generatedSource.Should().NotBeNull();

        if (string.IsNullOrEmpty(expected))
        {
            // Public setter - should not have any modifier before 'set'
            generatedSource.Should().Contain("set\n");
        }
        else
        {
            generatedSource.Should().Contain($"{expected} set");
        }
    }

    [Fact]
    public void Generator_ClassInheritingFromInpcBase_DoesNotDuplicateInterface()
    {
        // Arrange
        var source = """
            using NotifyGen;
            using System.ComponentModel;

            namespace TestNamespace
            {
                public class ViewModelBase : INotifyPropertyChanged
                {
                    public event PropertyChangedEventHandler? PropertyChanged;
                    protected virtual void OnPropertyChanged(string? propertyName = null)
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    }
                }

                [Notify]
                public partial class PersonViewModel : ViewModelBase
                {
                    private string _name;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "PersonViewModel.g.cs");
        generatedSource.Should().NotBeNull();
        // Should NOT contain ": INotifyPropertyChanged" since base already implements it
        generatedSource.Should().NotContain(": INotifyPropertyChanged");
        // Should NOT generate PropertyChanged event since base already has it
        generatedSource.Should().NotContain("public event PropertyChangedEventHandler?");
        // Should still generate the property
        generatedSource.Should().Contain("public string Name");
    }

    [Fact]
    public void Generator_WithMultipleTypeParameters_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class KeyValuePair<TKey, TValue>
                {
                    private TKey _key;
                    private TValue _value;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "KeyValuePair.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public partial class KeyValuePair<TKey, TValue> : INotifyPropertyChanged");
        generatedSource.Should().Contain("public TKey Key");
        generatedSource.Should().Contain("public TValue Value");
    }

    [Fact]
    public void Generator_WithConstrainedTypeParameter_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;
            using System;

            namespace TestNamespace
            {
                [Notify]
                public partial class Container<T> where T : class, IComparable<T>
                {
                    private T _item;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Container.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("public partial class Container<T> : INotifyPropertyChanged");
        generatedSource.Should().Contain("public T Item");
    }

    [Fact]
    public void Generator_WithMultipleClasses_GeneratesAllCorrectly()
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

                [Notify]
                public partial class Address
                {
                    private string _street;
                    private string _city;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var personSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Person.g.cs");
        var addressSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Address.g.cs");

        personSource.Should().NotBeNull();
        personSource.Should().Contain("public string Name");

        addressSource.Should().NotBeNull();
        addressSource.Should().Contain("public string Street");
        addressSource.Should().Contain("public string City");
    }
}
