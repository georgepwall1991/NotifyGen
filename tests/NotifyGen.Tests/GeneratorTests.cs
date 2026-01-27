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

    #region INotifyPropertyChanging Tests

    [Fact]
    public void Generator_WithImplementChanging_GeneratesINotifyPropertyChanging()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify(ImplementChanging = true)]
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
        generatedSource.Should().Contain("INotifyPropertyChanging");
        generatedSource.Should().Contain("public event PropertyChangingEventHandler? PropertyChanging");
        generatedSource.Should().Contain("protected virtual void OnPropertyChanging");
        generatedSource.Should().Contain("PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName))");
    }

    [Fact]
    public void Generator_WithImplementChanging_CallsOnPropertyChangingBeforeAssignment()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify(ImplementChanging = true)]
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

        // Verify order: OnPropertyChanging before assignment
        var changingIndex = generatedSource!.IndexOf("OnPropertyChanging()");
        var assignmentIndex = generatedSource.IndexOf("_name = value");

        changingIndex.Should().BeGreaterThan(-1, "OnPropertyChanging should be called");
        assignmentIndex.Should().BeGreaterThan(-1, "Assignment should exist");
        changingIndex.Should().BeLessThan(assignmentIndex, "OnPropertyChanging should be called before assignment");
    }

    [Fact]
    public void Generator_WithoutImplementChanging_DoesNotGeneratePropertyChanging()
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
        generatedSource.Should().NotContain("INotifyPropertyChanging");
        generatedSource.Should().NotContain("PropertyChangingEventHandler");
    }

    #endregion

    #region NotifyCanExecuteChangedFor Tests

    [Fact]
    public void Generator_WithNotifyCanExecuteChangedFor_GeneratesCommandNotification()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class ViewModel
                {
                    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
                    private string _name;

                    public object SaveCommand { get; }
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "ViewModel.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("SaveCommand?.NotifyCanExecuteChanged()");
    }

    [Fact]
    public void Generator_WithMultipleNotifyCanExecuteChangedFor_GeneratesAllNotifications()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class ViewModel
                {
                    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
                    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
                    private string _name;

                    public object SaveCommand { get; }
                    public object DeleteCommand { get; }
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "ViewModel.g.cs");
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("SaveCommand?.NotifyCanExecuteChanged()");
        generatedSource.Should().Contain("DeleteCommand?.NotifyCanExecuteChanged()");
    }

    #endregion

    #region NotifySuppressable Tests

    [Fact]
    public void Generator_WithNotifySuppressable_GeneratesSuppressNotificationsMethod()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                [NotifySuppressable]
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
        generatedSource.Should().Contain("using System;");
        generatedSource.Should().Contain("private int _notificationSuppressionCount");
        generatedSource.Should().Contain("private HashSet<string>? _pendingNotifications");
        generatedSource.Should().Contain("public IDisposable SuppressNotifications()");
        generatedSource.Should().Contain("private void ResumeNotifications()");
        generatedSource.Should().Contain("private sealed class NotificationSuppressor : IDisposable");
    }

    [Fact]
    public void Generator_WithNotifySuppressable_OnPropertyChangedChecksSuppression()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                [NotifySuppressable]
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
        generatedSource.Should().Contain("if (_notificationSuppressionCount > 0)");
        generatedSource.Should().Contain("_pendingNotifications ??= new HashSet<string>()");
        generatedSource.Should().Contain("_pendingNotifications.Add(propertyName!)");
    }

    [Fact]
    public void Generator_WithoutNotifySuppressable_DoesNotGenerateSuppressionCode()
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
        generatedSource.Should().NotContain("SuppressNotifications");
        generatedSource.Should().NotContain("_notificationSuppressionCount");
        generatedSource.Should().NotContain("_pendingNotifications");
    }

    [Fact]
    public void Generator_WithBothImplementChangingAndSuppressable_GeneratesBoth()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify(ImplementChanging = true)]
                [NotifySuppressable]
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

        // Should have both interfaces
        generatedSource.Should().Contain("INotifyPropertyChanged");
        generatedSource.Should().Contain("INotifyPropertyChanging");

        // Should have PropertyChanging event and method
        generatedSource.Should().Contain("PropertyChangingEventHandler");
        generatedSource.Should().Contain("OnPropertyChanging()");

        // Should have suppression infrastructure
        generatedSource.Should().Contain("SuppressNotifications()");
        generatedSource.Should().Contain("_notificationSuppressionCount");
    }

    [Fact]
    public void Generator_WithNotifySuppressable_ResumeNotificationsFiringEvents()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                [NotifySuppressable]
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

        // Verify ResumeNotifications fires pending events
        generatedSource.Should().Contain("if (--_notificationSuppressionCount == 0 && _pendingNotifications != null)");
        generatedSource.Should().Contain("foreach (var propertyName in _pendingNotifications)");
        generatedSource.Should().Contain("PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))");
        generatedSource.Should().Contain("_pendingNotifications.Clear()");
    }

    [Fact]
    public void Generator_WithNotifySuppressable_SupportsNestedSuppression()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                [NotifySuppressable]
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

        // Verify nested suppression support through increment/decrement pattern
        generatedSource.Should().Contain("_notificationSuppressionCount++");
        generatedSource.Should().Contain("--_notificationSuppressionCount == 0");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Generator_WithImplementChangingAndExistingBase_DoesNotDuplicateChangingInterface()
    {
        // Arrange
        var source = """
            using NotifyGen;
            using System.ComponentModel;

            namespace TestNamespace
            {
                public class ViewModelBase : INotifyPropertyChanged, INotifyPropertyChanging
                {
                    public event PropertyChangedEventHandler? PropertyChanged;
                    public event PropertyChangingEventHandler? PropertyChanging;
                    protected virtual void OnPropertyChanged(string? propertyName = null)
                        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                    protected virtual void OnPropertyChanging(string? propertyName = null)
                        => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
                }

                [Notify(ImplementChanging = true)]
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

        // Should NOT generate interface or events (base already has them)
        generatedSource.Should().NotContain(": INotifyPropertyChanged");
        generatedSource.Should().NotContain(": INotifyPropertyChanging");
        generatedSource.Should().NotContain("public event PropertyChangedEventHandler?");
        generatedSource.Should().NotContain("public event PropertyChangingEventHandler?");

        // Should still call OnPropertyChanging in setter
        generatedSource.Should().Contain("OnPropertyChanging()");
    }

    [Fact]
    public void Generator_WithAllFeaturesOnGenericClass_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify(ImplementChanging = true)]
                [NotifySuppressable]
                public partial class Container<T>
                {
                    [NotifyAlso("HasValue")]
                    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
                    private T _value;

                    public object ClearCommand { get; }
                    public bool HasValue => _value != null;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Container.g.cs");
        generatedSource.Should().NotBeNull();

        // Generic class with type parameter
        generatedSource.Should().Contain("public partial class Container<T>");

        // INotifyPropertyChanging
        generatedSource.Should().Contain("INotifyPropertyChanging");
        generatedSource.Should().Contain("OnPropertyChanging()");

        // Suppression with generic type in nested class
        generatedSource.Should().Contain("private readonly Container<T> _owner");
        generatedSource.Should().Contain("SuppressNotifications()");

        // NotifyAlso
        generatedSource.Should().Contain("OnPropertyChanged(\"HasValue\")");

        // NotifyCanExecuteChangedFor
        generatedSource.Should().Contain("ClearCommand?.NotifyCanExecuteChanged()");
    }

    [Fact]
    public void Generator_WithNotifyCanExecuteChangedForAndNotifyAlso_GeneratesInCorrectOrder()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify]
                public partial class ViewModel
                {
                    [NotifyAlso("CanSave")]
                    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
                    private string _name;

                    public object SaveCommand { get; }
                    public bool CanSave => !string.IsNullOrEmpty(Name);
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "ViewModel.g.cs");
        generatedSource.Should().NotBeNull();

        // Verify order: NotifyAlso before NotifyCanExecuteChangedFor before OnChanged
        var alsoIndex = generatedSource!.IndexOf("OnPropertyChanged(\"CanSave\")");
        var commandIndex = generatedSource.IndexOf("SaveCommand?.NotifyCanExecuteChanged()");
        var changedIndex = generatedSource.IndexOf("OnNameChanged()");

        alsoIndex.Should().BeGreaterThan(-1);
        commandIndex.Should().BeGreaterThan(-1);
        changedIndex.Should().BeGreaterThan(-1);

        alsoIndex.Should().BeLessThan(commandIndex, "NotifyAlso should come before command notification");
        commandIndex.Should().BeLessThan(changedIndex, "Command notification should come before OnChanged");
    }

    [Fact]
    public void Generator_WithImplementChangingAndPrimitiveType_UsesDirectComparison()
    {
        // Arrange
        var source = """
            using NotifyGen;

            namespace TestNamespace
            {
                [Notify(ImplementChanging = true)]
                public partial class Counter
                {
                    private int _count;
                }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "Counter.g.cs");
        generatedSource.Should().NotBeNull();

        // Should use direct comparison for int
        generatedSource.Should().Contain("if (_count == value) return;");

        // Should still call OnPropertyChanging
        generatedSource.Should().Contain("OnPropertyChanging()");
    }

    [Fact]
    public void Generator_GlobalNamespaceWithAllFeatures_GeneratesCorrectly()
    {
        // Arrange
        var source = """
            using NotifyGen;

            [Notify(ImplementChanging = true)]
            [NotifySuppressable]
            public partial class GlobalViewModel
            {
                [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
                private string _name;

                public object SaveCommand { get; }
            }
            """;

        // Act
        var (_, diagnostics, runResult) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedSource = GeneratorTestHelper.GetGeneratedSource(runResult, "GlobalViewModel.g.cs");
        generatedSource.Should().NotBeNull();

        // No namespace declaration
        generatedSource.Should().NotContain("namespace");

        // All features work
        generatedSource.Should().Contain("INotifyPropertyChanging");
        generatedSource.Should().Contain("SuppressNotifications()");
        generatedSource.Should().Contain("SaveCommand?.NotifyCanExecuteChanged()");
    }

    #endregion
}
