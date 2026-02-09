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

    #region NotifySuppressable Runtime Tests

    [Fact]
    public void SuppressNotifications_DefersBatchedEvents()
    {
        // Arrange
        var vm = new TestViewModelSuppressable();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act - modify properties within suppression scope
        using (vm.SuppressNotifications())
        {
            vm.Name = "John";
            vm.Age = 30;
            vm.Email = "john@example.com";

            // Assert - no events fired yet
            changedProperties.Should().BeEmpty();
        }

        // Assert - events fire after using block
        changedProperties.Should().HaveCount(3);
        changedProperties.Should().Contain("Name");
        changedProperties.Should().Contain("Age");
        changedProperties.Should().Contain("Email");
    }

    [Fact]
    public void SuppressNotifications_NestedScopes_WorksCorrectly()
    {
        // Arrange
        var vm = new TestViewModelSuppressable();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act - nested suppression scopes
        using (vm.SuppressNotifications())
        {
            vm.Name = "John";
            changedProperties.Should().BeEmpty(); // Suppressed

            using (vm.SuppressNotifications())
            {
                vm.Age = 30;
                changedProperties.Should().BeEmpty(); // Still suppressed
            }

            changedProperties.Should().BeEmpty(); // Still suppressed (outer scope)
        }

        // Assert - events fire only after all scopes closed
        changedProperties.Should().HaveCount(2);
        changedProperties.Should().Contain("Name");
        changedProperties.Should().Contain("Age");
    }

    [Fact]
    public void SuppressNotifications_DeduplicatesNotifications()
    {
        // Arrange
        var vm = new TestViewModelSuppressable();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act - modify same property multiple times
        using (vm.SuppressNotifications())
        {
            vm.Name = "John";
            vm.Name = "Jane";
            vm.Name = "Bob";
        }

        // Assert - only one event fired for the property
        changedProperties.Should().ContainSingle();
        changedProperties.Should().Contain("Name");
    }

    [Fact]
    public void SuppressNotifications_WithoutSuppression_FiresImmediately()
    {
        // Arrange
        var vm = new TestViewModelSuppressable();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act - modify without suppression
        vm.Name = "John";
        vm.Age = 30;

        // Assert - events fire immediately
        changedProperties.Should().HaveCount(2);
        changedProperties.Should().Contain("Name");
        changedProperties.Should().Contain("Age");
    }

    [Fact]
    public void SuppressNotifications_WithAlwaysNotify_SelectiveProperties_FireImmediately()
    {
        // Arrange
        var vm = new TestViewModelWithAlwaysNotify();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act - modify properties within suppression scope
        using (vm.SuppressNotifications())
        {
            vm.Name = "John";          // Suppressible
            vm.Age = 30;               // Suppressible
            vm.IsLoading = true;       // AlwaysNotify - should fire immediately

            // Assert - only IsLoading fired during suppression
            changedProperties.Should().ContainSingle();
            changedProperties.Should().Contain("IsLoading");
        }

        // Assert - Name and Age fire after using block
        changedProperties.Should().HaveCount(3);
        changedProperties.Should().Contain("Name");
        changedProperties.Should().Contain("Age");
        changedProperties.Should().Contain("IsLoading");
    }

    [Fact]
    public void SuppressNotifications_WithAlwaysNotify_MultipleChanges_DeduplicatesNonAlwaysNotify()
    {
        // Arrange
        var vm = new TestViewModelWithAlwaysNotify();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act - modify same properties multiple times
        using (vm.SuppressNotifications())
        {
            vm.Name = "John";
            vm.IsLoading = true;       // Fires immediately
            vm.Name = "Jane";
            vm.IsLoading = false;      // Fires immediately
            vm.Name = "Bob";
        }

        // Assert - IsLoading fired twice, Name once (after suppression)
        changedProperties.Count.Should().Be(3);
        changedProperties.Count(p => p == "IsLoading").Should().Be(2);
        changedProperties.Count(p => p == "Name").Should().Be(1);
    }

    [Fact]
    public void SuppressNotifications_WithAlwaysNotify_WithoutSuppression_AllFireImmediately()
    {
        // Arrange
        var vm = new TestViewModelWithAlwaysNotify();
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act - modify without suppression
        vm.Name = "John";
        vm.Age = 30;
        vm.IsLoading = true;

        // Assert - all fire immediately
        changedProperties.Should().HaveCount(3);
        changedProperties.Should().Contain("Name");
        changedProperties.Should().Contain("Age");
        changedProperties.Should().Contain("IsLoading");
    }

    #endregion

    #region INotifyPropertyChanging Runtime Tests

    [Fact]
    public void PropertyChanging_FiresBeforeValueChange()
    {
        // Arrange
        var person = new TestPersonWithChanging();
        var eventOrder = new List<string>();
        string? oldValue = null;
        string? newValue = null;

        person.PropertyChanging += (_, e) =>
        {
            eventOrder.Add($"Changing:{e.PropertyName}");
            oldValue = person.Name; // Capture old value
        };

        person.PropertyChanged += (_, e) =>
        {
            eventOrder.Add($"Changed:{e.PropertyName}");
            newValue = person.Name; // Capture new value
        };

        // Act
        person.Name = "John";

        // Assert - Changing fires before Changed
        eventOrder.Should().HaveCount(2);
        eventOrder[0].Should().Be("Changing:Name");
        eventOrder[1].Should().Be("Changed:Name");

        // Assert - old and new values captured correctly
        oldValue.Should().BeEmpty(); // Old value was empty string
        newValue.Should().Be("John"); // New value is "John"
    }

    [Fact]
    public void PropertyChanging_ReceivesOldAndNewValues_InPartialHook()
    {
        // Arrange
        var person = new TestPersonWithChangingHook();

        // Act
        person.Name = "John";

        // Assert - partial hook was called with old and new values
        person.OldValueInHook.Should().BeEmpty();
        person.NewValueInHook.Should().Be("John");
    }

    [Fact]
    public void PropertyChanging_WorksWithBaseImplementation()
    {
        // Arrange
        var person = new TestPersonWithBaseInpc();
        var eventOrder = new List<string>();

        person.PropertyChanging += (_, e) =>
        {
            eventOrder.Add($"Changing:{e.PropertyName}");
        };

        person.PropertyChanged += (_, e) =>
        {
            eventOrder.Add($"Changed:{e.PropertyName}");
        };

        // Act
        person.Name = "Test";

        // Assert - both events fire in correct order
        eventOrder.Should().HaveCount(2);
        eventOrder[0].Should().Be("Changing:Name");
        eventOrder[1].Should().Be("Changed:Name");
    }

    #endregion

    #region NotifyCanExecuteChangedFor Runtime Tests

    [Fact]
    public void CommandNotification_CallsNotifyCanExecuteChanged()
    {
        // Arrange
        var vm = new TestViewModelWithCommand();
        var commandNotified = false;
        vm.SaveCommand = new TestCommand(() => commandNotified = true);

        // Act
        vm.Name = "John";

        // Assert - command was notified
        commandNotified.Should().BeTrue();
    }

    [Fact]
    public void CommandNotification_SupportsMultipleCommands()
    {
        // Arrange
        var vm = new TestViewModelWithMultipleCommands();
        var saveNotified = false;
        var deleteNotified = false;

        vm.SaveCommand = new TestCommand(() => saveNotified = true);
        vm.DeleteCommand = new TestCommand(() => deleteNotified = true);

        // Act
        vm.Name = "John";

        // Assert - both commands were notified
        saveNotified.Should().BeTrue();
        deleteNotified.Should().BeTrue();
    }

    [Fact]
    public void CommandNotification_DoesNotThrow_WhenCommandIsNull()
    {
        // Arrange
        var vm = new TestViewModelWithCommand();
        vm.SaveCommand = null; // Command is null

        // Act & Assert - should not throw
        var act = () => vm.Name = "John";
        act.Should().NotThrow();
    }

    #endregion
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

/// <summary>
/// Test class for NotifySuppressable functionality.
/// </summary>
[Notify]
[NotifySuppressable]
public partial class TestViewModelSuppressable
{
    private string _name = string.Empty;
    private int _age;
    private string? _email;
}

/// <summary>
/// Test class for NotifySuppressable with AlwaysNotify feature.
/// </summary>
[Notify]
[NotifySuppressable(AlwaysNotify = new[] { nameof(IsLoading) })]
public partial class TestViewModelWithAlwaysNotify
{
    private string _name = string.Empty;
    private int _age;
    private bool _isLoading;
}

/// <summary>
/// Test class for INotifyPropertyChanging functionality.
/// </summary>
[Notify(ImplementChanging = true)]
public partial class TestPersonWithChanging
{
    private string _name = string.Empty;
}

/// <summary>
/// Test class for INotifyPropertyChanging with partial hooks.
/// </summary>
[Notify(ImplementChanging = true)]
public partial class TestPersonWithChangingHook
{
    private string _name = string.Empty;

    public string OldValueInHook { get; private set; } = string.Empty;
    public string NewValueInHook { get; private set; } = string.Empty;

    partial void OnNameChanging(string oldValue, string newValue)
    {
        OldValueInHook = oldValue;
        NewValueInHook = newValue;
    }
}

/// <summary>
/// Test class for PropertyChanging that doesn't inherit from a base class.
/// Tests that PropertyChanging works correctly without inheritance complications.
/// </summary>
[Notify(ImplementChanging = true)]
public partial class TestPersonWithBaseInpc
{
    private string _name = string.Empty;
}

/// <summary>
/// Test command implementation that tracks NotifyCanExecuteChanged calls.
/// </summary>
public class TestCommand
{
    private readonly Action _onNotifyCanExecuteChanged;

    public TestCommand(Action onNotifyCanExecuteChanged)
    {
        _onNotifyCanExecuteChanged = onNotifyCanExecuteChanged;
    }

    public void NotifyCanExecuteChanged()
    {
        _onNotifyCanExecuteChanged();
    }
}

/// <summary>
/// Test class for NotifyCanExecuteChangedFor functionality.
/// </summary>
[Notify]
public partial class TestViewModelWithCommand
{
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    public TestCommand? SaveCommand { get; set; }
}

/// <summary>
/// Test class for NotifyCanExecuteChangedFor with multiple commands.
/// </summary>
[Notify]
public partial class TestViewModelWithMultipleCommands
{
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private string _name = string.Empty;

    public TestCommand? SaveCommand { get; set; }
    public TestCommand? DeleteCommand { get; set; }
}
