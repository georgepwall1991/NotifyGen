using System.ComponentModel;
using FluentAssertions;

namespace NotifyGen.Tests;

/// <summary>
/// Integration tests that verify PropertyChanged fires correctly at runtime.
/// These tests use actual classes marked with [Notify].
/// </summary>
public class IntegrationTests
{
    [Fact]
    public void PropertyChanged_Fires_WhenPropertyChanges()
    {
        // Arrange
        var person = new TestPerson();
        var changedProperties = new List<string>();
        person.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        person.Name = "John";

        // Assert
        changedProperties.Should().Contain("Name");
    }

    [Fact]
    public void PropertyChanged_DoesNotFire_WhenValueIsSame()
    {
        // Arrange
        var person = new TestPerson { Name = "John" };
        var changedProperties = new List<string>();
        person.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        person.Name = "John"; // Same value

        // Assert
        changedProperties.Should().BeEmpty();
    }

    [Fact]
    public void NotifyAlso_RaisesPropertyChanged_ForDependentProperty()
    {
        // Arrange
        var person = new TestPersonWithFullName();
        var changedProperties = new List<string>();
        person.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        person.FirstName = "John";

        // Assert
        changedProperties.Should().Contain("FirstName");
        changedProperties.Should().Contain("FullName");
    }

    [Fact]
    public void PartialHook_IsCalled_WhenPropertyChanges()
    {
        // Arrange
        var person = new TestPersonWithHook();

        // Act
        person.Name = "John";

        // Assert
        person.HookWasCalled.Should().BeTrue();
    }

    [Fact]
    public void NullableProperty_WorksCorrectly()
    {
        // Arrange
        var person = new TestPersonNullable();
        var changedProperties = new List<string>();
        person.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        person.Email = "test@example.com";
        person.Email = null;

        // Assert
        changedProperties.Should().HaveCount(2);
        changedProperties.Should().AllBe("Email");
    }
}

/// <summary>
/// Test class for basic property notification.
/// </summary>
[Notify]
public partial class TestPerson
{
    private string _name = string.Empty;
    private int _age;
}

/// <summary>
/// Test class for NotifyAlso functionality.
/// </summary>
[Notify]
public partial class TestPersonWithFullName
{
    [NotifyAlso("FullName")]
    private string _firstName = string.Empty;

    [NotifyAlso("FullName")]
    private string _lastName = string.Empty;

    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>
/// Test class for partial hook functionality.
/// </summary>
[Notify]
public partial class TestPersonWithHook
{
    private string _name = string.Empty;

    public bool HookWasCalled { get; private set; }

    partial void OnNameChanged()
    {
        HookWasCalled = true;
    }
}

/// <summary>
/// Test class for nullable property handling.
/// </summary>
[Notify]
public partial class TestPersonNullable
{
    private string? _email;
}
