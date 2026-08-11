![NotifyGen Banner](https://raw.githubusercontent.com/georgepwall1991/NotifyGen/master/assets/header.png)

<p align="center">
  <img src="https://raw.githubusercontent.com/georgepwall1991/NotifyGen/master/assets/icon.png" alt="NotifyGen Icon" width="128" height="128" />
</p>

# NotifyGen

[![NuGet](https://img.shields.io/nuget/v/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NotifyGen.svg)](https://www.nuget.org/packages/NotifyGen/)
[![Build Status](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml/badge.svg)](https://github.com/georgepwall1991/NotifyGen/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-Standard%202.0%2B-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Stop writing INotifyPropertyChanged boilerplate. Let the compiler do it.**

## The Problem

Every WPF, MAUI, or Blazor developer knows this pain. You want one property:

```csharp
private string _name;
```

But you end up writing this:

```csharp
private string _name;
public string Name
{
    get => _name;
    set
    {
        if (_name != value)
        {
            _name = value;
            OnPropertyChanged();
        }
    }
}
```

Multiply that by every property in your ViewModels. It's tedious, error-prone, and clutters your code with repetitive boilerplate.

## The Solution

```csharp
using NotifyGen;

[Notify]
public partial class Person
{
    private string _name;
    private int _age;
    private string? _email;
}
```

NotifyGen generates the rest at compile time. No runtime reflection. No IL weaving. Just clean, debuggable C#.

## What Gets Generated

For the `Person` class above, NotifyGen generates:

```csharp
public partial class Person : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_name, value)) return;
            OnNameChanging(_name, value);
            _name = value;
            OnPropertyChanged();
            OnNameChanged();
        }
    }

    public int Age
    {
        get => _age;
        set
        {
            if (_age == value) return;  // Direct comparison for primitives
            OnAgeChanging(_age, value);
            _age = value;
            OnPropertyChanged();
            OnAgeChanged();
        }
    }

    public string? Email
    {
        get => _email;
        set
        {
            if (EqualityComparer<string?>.Default.Equals(_email, value)) return;
            OnEmailChanging(_email, value);
            _email = value;
            OnPropertyChanged();
            OnEmailChanged();
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    partial void OnNameChanging(string oldValue, string newValue);
    partial void OnNameChanged();
    partial void OnAgeChanging(int oldValue, int newValue);
    partial void OnAgeChanged();
    partial void OnEmailChanging(string? oldValue, string? newValue);
    partial void OnEmailChanged();
}
```

This generated code is visible in your IDE (look under Dependencies → Analyzers → NotifyGen). You can step through it in the debugger.

## Installation

```bash
dotnet add package NotifyGen
```

Or via Package Manager:
```
Install-Package NotifyGen
```

## Real-World Example

Here's a more complete ViewModel showing several features working together:

```csharp
using NotifyGen;

[Notify(ImplementChanging = true)]  // Enable PropertyChanging for undo/redo
[NotifySuppressable]                 // Enable batch notification suppression
public partial class CustomerViewModel
{
    // Basic properties - just declare the field
    [NotifyAlso("FullName")]
    private string _firstName;

    [NotifyAlso("FullName")]
    private string _lastName;

    private string? _email;

    // Notify dependent properties and refresh save command
    [NotifyAlso("CanSave")]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _company;

    // Private setter - can only be set internally
    [NotifySetter(AccessLevel.Private)]
    private int _id;

    // Custom property name with command notification
    [NotifyName("IsPreferredCustomer")]
    [NotifyCanExecuteChangedFor(nameof(ApplyDiscountCommand))]
    private bool _preferred;

    // Exclude from generation - manage manually
    [NotifyIgnore]
    private readonly ICustomerService _customerService;

    // Computed property that depends on FirstName and LastName
    public string FullName => $"{FirstName} {LastName}".Trim();

    // Validation property
    public bool CanSave => !string.IsNullOrWhiteSpace(FirstName)
                        && !string.IsNullOrWhiteSpace(Company);

    // Commands with auto-refreshing CanExecute
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand ApplyDiscountCommand { get; }

    public CustomerViewModel(ICustomerService customerService)
    {
        _customerService = customerService;
        SaveCommand = new RelayCommand(Save, () => CanSave);
        ApplyDiscountCommand = new RelayCommand(ApplyDiscount, () => IsPreferredCustomer);
    }

    // Bulk update without intermediate notifications
    public void LoadFromDto(CustomerDto dto)
    {
        using (SuppressNotifications())
        {
            FirstName = dto.FirstName;
            LastName = dto.LastName;
            Email = dto.Email;
            Company = dto.Company;
        }  // Single batch of PropertyChanged events fires here
    }

    // Hook into property changes for validation
    partial void OnFirstNameChanging(string oldValue, string newValue)
    {
        if (newValue?.Length > 100)
            throw new ArgumentException("First name too long");
    }

    // React to changes
    partial void OnEmailChanged()
    {
        ValidateEmail();
    }

    private void Save() { /* ... */ }
    private void ApplyDiscount() { /* ... */ }
    private void ValidateEmail() { /* ... */ }
}
```

Bind it in XAML:

```xml
<StackPanel DataContext="{Binding CustomerViewModel}">
    <TextBox Text="{Binding FirstName, UpdateSourceTrigger=PropertyChanged}" />
    <TextBox Text="{Binding LastName, UpdateSourceTrigger=PropertyChanged}" />
    <TextBlock Text="{Binding FullName}" />
    <CheckBox IsChecked="{Binding IsPreferredCustomer}" Content="Preferred Customer" />
    <Button Content="Save" IsEnabled="{Binding CanSave}" />
</StackPanel>
```

## Features

### Field Naming Convention

NotifyGen uses underscore-prefixed private fields:

| Field | Generated Property |
|-------|-------------------|
| `_name` | `Name` |
| `_firstName` | `FirstName` |
| `_isEnabled` | `IsEnabled` |
| `_id` | `Id` |

The underscore is stripped and the first letter is capitalized.

### Partial-property mode (C# 14)

When the project uses a C# 14/preview compiler, `[Notify]` can also implement
an incomplete partial property. This is useful when a type already inherits
from another base class and cannot adopt an MVVM framework base type:

```csharp
[Notify]
public partial class PlainEntity : FrameworkEntity
{
    [NotifyAlso(nameof(DisplayName))]
    public partial string Name { get; set; }

    public string DisplayName => Name.Trim();
}
```

NotifyGen supplies the `field` implementation, equality guard, existing
old/new partial hooks, and `NotifyAlso` notifications while retaining the
user's base class. This mode adds no runtime dependency. Existing underscore
field mode remains available for older language versions. See the
[design proof](docs/design/partial-properties.md) and
[`PartialPropertyTests`](tests/NotifyGen.Tests/PartialPropertyTests.cs).

### Property Metadata Forwarding

Attributes on an eligible field that are valid for properties are copied to the
generated property. This keeps validation, serialization, and binding metadata
attached without making NotifyGen implement those frameworks:

```csharp
[Notify]
public partial class Person
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.Text.Json.Serialization.JsonPropertyName("displayName")]
    private string _displayName = string.Empty;
}
```

Field-only attributes and NotifyGen control attributes are not copied. File-local
attribute types (or file-local types used as attribute arguments) are skipped
because generated source is a separate file. See the
[metadata design](docs/design/property-metadata-forwarding.md) and
[`PropertyMetadataTests`](tests/NotifyGen.Tests/PropertyMetadataTests.cs).

### What Fields Are Eligible?

NotifyGen generates properties only for mutable, private instance fields with an underscore prefix. The `private` modifier may be explicit or implicit. Here's what works and what doesn't:

**✅ Eligible Fields:**
```csharp
private string _name;           // ✓ Instance, private, underscore
private int _age;               // ✓ All types work (primitives, classes, structs)
private bool? _isActive;        // ✓ Nullable types supported
string _implicitPrivate;        // ✓ Fields without a modifier are private in a class
```

**❌ Ineligible Fields:**
```csharp
public string _name;            // ✗ Must be private
protected string _name;         // ✗ Must be private
internal string _name;          // ✗ Must be private

static string _name;            // ✗ Static fields cannot trigger instance events
const string _name = "John";    // ✗ Const fields are immutable
readonly string _name;          // ✗ Readonly fields cannot have setters

private string name;            // ✗ Missing underscore prefix
private string _;               // ✗ Too short (need at least 2 characters)
```

**Diagnostics Help You:**

If you mark a class with `[Notify]` but have no eligible fields, NotifyGen will show:
```
NOTIFY002: Class 'MyClass' is marked with [Notify] but has no private fields with underscore prefix (e.g., '_fieldName'). No properties will be generated.
```

**Excluding Fields with [NotifyIgnore]:**
```csharp
[Notify]
public partial class ViewModel
{
    private string _name;           // ✓ Generates Name property

    [NotifyIgnore]                  // Explicitly excluded
    private readonly ILogger _logger;
}
```

Use `[NotifyIgnore]` on fields you want to exclude from generation (e.g., services, readonly state).

### Equality Guards

Every generated setter checks if the value actually changed before doing anything:

```csharp
// For primitive types (int, bool, double, etc.) - direct comparison
if (_age == value) return;

// For reference types and complex value types - EqualityComparer
if (EqualityComparer<string>.Default.Equals(_name, value)) return;
```

NotifyGen automatically detects primitive types and uses direct `==` comparison for maximum performance. This prevents unnecessary `PropertyChanged` events and infinite loops from two-way bindings. Works correctly with nulls, value types, and reference types.

### Dependent Properties with `[NotifyAlso]`

When one property affects another, use `[NotifyAlso]` to notify both:

```csharp
[Notify]
public partial class Rectangle
{
    [NotifyAlso("Area")]
    [NotifyAlso("Perimeter")]
    private double _width;

    [NotifyAlso("Area")]
    [NotifyAlso("Perimeter")]
    private double _height;

    public double Area => Width * Height;
    public double Perimeter => 2 * (Width + Height);
}
```

When `Width` changes, `PropertyChanged` fires for `Width`, `Area`, and `Perimeter`.

A dependent generated property can declare its source instead, which is useful
when the dependency is easiest to read from the target side:

```csharp
[NotifyAlso(nameof(FirstName), NotifyFrom = true)]
private string _displayName;
```

This says that changing generated `FirstName` also raises generated
`DisplayName`. `NotifyFrom = true` remains an explicit same-type graph edge; it
does not inspect getter expressions. Its source must be another property
generated by NotifyGen. See the [target-side dependency design](docs/design/notifyalso-target-dependencies.md).

Dependencies may be chained. If `FirstName` notifies `DisplayName` and
`DisplayName` notifies `SearchText`, changing `FirstName` raises all three
names. A diamond is deduplicated, so each reachable name is raised once. A
cycle is rejected at compile time with `NOTIFY008`, including the dependency
path in the diagnostic instead of silently bounding the traversal. See the
[transitive dependency proof](docs/design/notifyalso-transitive-cycles.md) and
[`IntegrationTests.NotifyAlso_TransitiveChainAndDiamond_AreDeduplicated`](tests/NotifyGen.Tests/IntegrationTests.cs).

### Child Property Notifications with `NotifyOnSubPropertyChanged`

For a computed property that depends on a child object's changes, opt in on the
existing `[NotifyAlso]` attribute:

```csharp
[Notify]
public partial class CustomerViewModel
{
    [NotifyAlso(nameof(DisplayName), NotifyOnSubPropertyChanged = true)]
    private Address? _address;

    public string DisplayName => Address?.City ?? string.Empty;
}

public sealed class Address : INotifyPropertyChanged
{
    public string City { get; set; } = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
}
```

NotifyGen subscribes to the child's ordinary `INotifyPropertyChanged` event,
detaches the old value when `Address` is replaced, and raises `DisplayName` for
any child change. Tracking starts when the generated source property is first
accessed or assigned; source generators cannot inject initialization into every
user constructor. The option is direct and explicit: it does not inspect getter
expressions, traverse arbitrary object graphs, or subscribe to collections.
`NOTIFY010` warns when the source type is not a reference value implementing
`INotifyPropertyChanged`. See the [design proof](docs/design/notifyalso-subproperty.md)
and [`SubPropertyNotificationTests`](tests/NotifyGen.Tests/SubPropertyNotificationTests.cs).

### Hosting an Existing `INotifyPropertyChanged` Implementation

A `[Notify]` type may inherit an existing `INotifyPropertyChanged`
implementation. NotifyGen reuses an accessible instance
`OnPropertyChanged(string)` (including nullable/optional string parameters) or
`OnPropertyChanged(PropertyChangedEventArgs)` method instead of generating a
second interface, event, or helper. This supports framework view-model bases
without reflection or a runtime package dependency:

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(PropertyChangedEventArgs args) =>
        PropertyChanged?.Invoke(this, args);
}

[Notify]
public partial class Person : ViewModelBase
{
    private string _name = string.Empty;
}
```

If an existing INPC implementation has no callable ordinary invoker (for
example, it only has an explicit interface event), NotifyGen withholds the
extension and reports `NOTIFY013` rather than emitting uncompilable code.
When `ImplementChanging = true`, the equivalent
`OnPropertyChanging(string)`/`OnPropertyChanging(PropertyChangingEventArgs)`
host is required and `NOTIFY017` reports an incompatible host.

### Collection Membership Notifications

Use `NotifyOnCollectionChanged = true` on a source-side `[NotifyAlso]`
declaration to notify a computed property when the source collection raises
`INotifyCollectionChanged.CollectionChanged`:

```csharp
[Notify]
public partial class Basket
{
    [NotifyAlso(nameof(Count), NotifyOnCollectionChanged = true)]
    private ObservableCollection<string> _items = new();

    public int Count => Items.Count;
}
```

NotifyGen attaches lazily on first access or assignment, detaches replaced and
null collections, and raises each declared target once per collection event.
It does not bubble item `INotifyPropertyChanged` events, infer dependencies
from getter expressions, or add a runtime collection helper. Collection
tracking is source-side only; `NotifyFrom = true` with this option reports
`NOTIFY014`. A value source reports `NOTIFY015`, while a reference source is
safely checked at runtime so custom collection implementations are supported.
See the [collection design proof](docs/design/notifyalso-collection-changes.md)
and [`Cycle4Tests`](tests/NotifyGen.Tests/Cycle4Tests.cs).

### Custom Property Names with `[NotifyName]`

Override the default naming:

```csharp
[NotifyName("IsVisible")]
private bool _shown;  // Generates IsVisible, not Shown

[NotifyName("CustomerID")]
private int _custId;  // Generates CustomerID, not CustId
```

### Setter Access Control with `[NotifySetter]`

Restrict who can set the property:

```csharp
[NotifySetter(AccessLevel.Private)]
private int _id;
// Result: public int Id { get; private set; }

[NotifySetter(AccessLevel.Protected)]
private string _internalState;
// Result: public string InternalState { get; protected set; }

[NotifySetter(AccessLevel.Internal)]
private DateTime _lastModified;
// Result: public DateTime LastModified { get; internal set; }
```

Available levels: `Public`, `Private`, `Protected`, `Internal`, `ProtectedInternal`, `PrivateProtected`

### Excluding Fields with `[NotifyIgnore]`

Some fields shouldn't become properties:

```csharp
[Notify]
public partial class ViewModel
{
    private string _name;  // Generates property

    [NotifyIgnore]
    private readonly ILogger _logger;  // No property

    [NotifyIgnore]
    private Dictionary<string, object> _cache;  // No property
}
```

### Partial Hooks

Every property gets two optional hooks:

**`On{Property}Changing(oldValue, newValue)`** - Called before the value changes. Use for validation:

```csharp
partial void OnAgeChanging(int oldValue, int newValue)
{
    if (newValue < 0 || newValue > 150)
        throw new ArgumentOutOfRangeException(nameof(newValue), "Invalid age");
}
```

**`On{Property}Changed()`** - Called after the value changes. Use for side effects:

```csharp
partial void OnSelectedItemChanged()
{
    LoadItemDetails();
    UpdateCommandStates();
}
```

An optional typed overload receives both values:

```csharp
partial void OnNameChanged(string oldValue, string newValue)
{
    AuditChange(oldValue, newValue);
}
```

The typed overload runs after assignment and the parameterless overload. If you
don't implement these methods, the compiler removes the calls entirely—no
performance cost.

### Integration with Validation Frameworks

NotifyGen's partial hooks make it easy to integrate with validation libraries:

#### FluentValidation

```csharp
using FluentValidation;
using NotifyGen;

[Notify]
public partial class CustomerViewModel : INotifyDataErrorInfo
{
    private string _name;
    private string _email;
    private readonly CustomerValidator _validator = new();
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Any();
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    // Validate after property changes
    partial void OnNameChanged() => ValidateProperty(nameof(Name));
    partial void OnEmailChanged() => ValidateProperty(nameof(Email));

    private void ValidateProperty(string propertyName)
    {
        var result = _validator.Validate(this);
        var propertyErrors = result.Errors
            .Where(e => e.PropertyName == propertyName)
            .Select(e => e.ErrorMessage)
            .ToList();

        if (propertyErrors.Any())
            _errors[propertyName] = propertyErrors;
        else
            _errors.Remove(propertyName);

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    public IEnumerable GetErrors(string? propertyName)
    {
        return propertyName != null && _errors.ContainsKey(propertyName)
            ? _errors[propertyName]
            : Enumerable.Empty<string>();
    }
}

public class CustomerValidator : AbstractValidator<CustomerViewModel>
{
    public CustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress();
    }
}
```

#### DataAnnotations

```csharp
using System.ComponentModel.DataAnnotations;
using NotifyGen;

[Notify]
public partial class PersonViewModel : INotifyDataErrorInfo
{
    [Required]
    [MaxLength(100)]
    private string _name;

    [EmailAddress]
    private string? _email;

    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Any();
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    // Validate after each property change
    partial void OnNameChanged() => ValidateProperty(nameof(Name), Name);
    partial void OnEmailChanged() => ValidateProperty(nameof(Email), Email);

    private void ValidateProperty(string propertyName, object? value)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this) { MemberName = propertyName };

        Validator.TryValidateProperty(value, context, results);

        if (results.Any())
            _errors[propertyName] = results.Select(r => r.ErrorMessage!).ToList();
        else
            _errors.Remove(propertyName);

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    public IEnumerable GetErrors(string? propertyName)
    {
        return propertyName != null && _errors.ContainsKey(propertyName)
            ? _errors[propertyName]
            : Enumerable.Empty<string>();
    }
}
```

**Note:** NotifyGen focuses on `INotifyPropertyChanged` generation. For validation errors (`INotifyDataErrorInfo`), implement that interface manually and trigger validation in partial hooks as shown above.

### INotifyPropertyChanging with `ImplementChanging`

For undo/redo scenarios, you may need the `PropertyChanging` event that fires *before* the value changes:

```csharp
[Notify(ImplementChanging = true)]
public partial class Document
{
    private string _content;
}
```

This generates:

```csharp
public partial class Document : INotifyPropertyChanged, INotifyPropertyChanging
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;

    public string Content
    {
        get => _content;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_content, value)) return;
            OnPropertyChanging();        // Fires BEFORE change
            OnContentChanging(_content, value);
            _content = value;
            OnPropertyChanged();         // Fires AFTER change
            OnContentChanged();
        }
    }

    protected virtual void OnPropertyChanging([CallerMemberName] string? propertyName = null)
        => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
}
```

If your base class already implements `INotifyPropertyChanging`, NotifyGen detects this and won't duplicate the interface or events.

### Command CanExecute with `[NotifyCanExecuteChangedFor]`

When a property change should refresh a command's `CanExecute` state, use `[NotifyCanExecuteChangedFor]`:

```csharp
[Notify]
public partial class EditorViewModel
{
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private string _content;

    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand UndoCommand { get; }
}
```

This generates calls to `NotifyCanExecuteChanged()` in the setter:

```csharp
public string Content
{
    set
    {
        if (EqualityComparer<string>.Default.Equals(_content, value)) return;
        OnContentChanging(_content, value);
        _content = value;
        OnPropertyChanged();
        SaveCommand?.NotifyCanExecuteChanged();  // Auto-generated
        UndoCommand?.NotifyCanExecuteChanged();  // Auto-generated
        OnContentChanged();
    }
}
```

Works with any command type that has a `NotifyCanExecuteChanged()` method (CommunityToolkit.Mvvm `IRelayCommand`, Prism `DelegateCommand`, etc.).

### Batch Notification Suppression with `[NotifySuppressable]`

For bulk updates where you want to defer `PropertyChanged` events until all changes complete:

```csharp
[Notify]
[NotifySuppressable]
public partial class Person
{
    private string _firstName;
    private string _lastName;
    private string _email;
}

// Usage:
using (person.SuppressNotifications())
{
    person.FirstName = "John";
    person.LastName = "Doe";
    person.Email = "john@example.com";
}  // All three PropertyChanged events fire here
```

This generates suppression infrastructure:

```csharp
public partial class Person : INotifyPropertyChanged
{
    private int _notificationSuppressionCount;
    private HashSet<string>? _pendingNotifications;

    public IDisposable SuppressNotifications()
    {
        _notificationSuppressionCount++;
        return new NotificationSuppressor(this);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (_notificationSuppressionCount > 0)
        {
            _pendingNotifications ??= new HashSet<string>();
            _pendingNotifications.Add(propertyName!);
            return;
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    // ... ResumeNotifications and NotificationSuppressor class
}
```

**Features:**
- Nested suppression scopes supported (uses reference counting)
- Duplicate property names deduplicated (HashSet)
- Zero allocations when not suppressing
- Designed for the owning UI thread; suppression state is not synchronized

#### Selective Suppression with `AlwaysNotify`

Some properties should always notify immediately, even during suppression (e.g., loading indicators, error flags):

```csharp
[Notify]
[NotifySuppressable(AlwaysNotify = new[] { nameof(IsLoading), nameof(HasErrors) })]
public partial class ViewModel
{
    private string _name;
    private int _age;
    private bool _isLoading;     // Always fires immediately
    private bool _hasErrors;     // Always fires immediately
}

// Usage:
using (vm.SuppressNotifications())
{
    vm.Name = "John";            // Deferred
    vm.Age = 30;                 // Deferred
    vm.IsLoading = true;         // ✓ Fires immediately (AlwaysNotify)
    vm.HasErrors = false;        // ✓ Fires immediately (AlwaysNotify)
}  // Name and Age notifications fire here
```

**Use cases:**
- **Loading indicators** - UI should show spinners immediately, even during bulk updates
- **Error flags** - Critical state that must notify immediately
- **Validation status** - UX requires immediate feedback
- **Progress tracking** - Progress bars should update in real-time

**Implementation:**
```csharp
private static readonly HashSet<string> _neverSuppressedProperties = new()
{
    "IsLoading",
    "HasErrors"
};

protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
{
    // Check if property should never be suppressed
    if (_notificationSuppressionCount > 0 && !_neverSuppressedProperties.Contains(propertyName ?? ""))
    {
        _pendingNotifications ??= new HashSet<string>();
        _pendingNotifications.Add(propertyName!);
        return;
    }
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

**Performance:** Adds a static `HashSet<string>` lookup (~O(1)) per `OnPropertyChanged` call when suppression is active. Negligible cost for typical use cases.

### Working with Existing INotifyPropertyChanged

If your class already implements `INotifyPropertyChanged` (e.g., from a base class), NotifyGen detects this and won't generate a duplicate implementation:

```csharp
public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

[Notify]
public partial class MyViewModel : ViewModelBase
{
    private string _title;  // Uses base class OnPropertyChanged
}
```

### Nested `[Notify]` Classes

NotifyGen supports `[Notify]` classes nested inside classes, structs, interfaces, records, and record structs. Every declaration in the containing chain must be `partial` so the generator can reopen it:

```csharp
public partial class Workspace<T>
    where T : class
{
    [Notify]
    public partial class Editor
    {
        private string _title;
    }
}
```

Generic parameters, accessibility, declaration kind, and required modifiers such as `unsafe` are preserved in generated code, which remains compatible with constrained generic declaration chains. If a containing type is not partial, `NOTIFY006` identifies that declaration and offers the same **Make type partial** code fix as `NOTIFY001`. File-local targets and containers are not supported because generated partial declarations are emitted in a separate source file; `NOTIFY007` reports the unsupported declaration and generation is withheld.

## Built-in Analyzers & Code Fixes

NotifyGen includes analyzers that catch mistakes at compile time:

| Code | Severity | Description | Auto-Fix |
|------|----------|-------------|----------|
| NOTIFY001 | Error | Class with `[Notify]` must be declared `partial` | Yes |
| NOTIFY002 | Warning | No eligible fields found (need mutable private `_underscore` fields) | — |
| NOTIFY003 | Warning | `[NotifyAlso("Xyz")]` references property `Xyz` that doesn't exist | — |
| NOTIFY004 | Info | Static or const field cannot generate an instance property | — |
| NOTIFY005 | Info | Readonly field cannot generate a property with a setter | — |
| NOTIFY006 | Error | A type containing a nested `[Notify]` class must be `partial` | Yes |
| NOTIFY007 | Error | A `[Notify]` target or containing type has file-local accessibility | — |
| NOTIFY008 | Error | `[NotifyAlso]` dependencies contain a cycle | — |
| NOTIFY009 | Error | Multiple members would generate the same property name | — |
| NOTIFY010 | Warning | `NotifyOnSubPropertyChanged` requires a reference child implementing `INotifyPropertyChanged` | — |
| NOTIFY011 | Warning | Target-side `NotifyAlso` source is not generated by NotifyGen | — |
| NOTIFY012 | Warning | Target-side `NotifyAlso` cannot request child tracking | — |
| NOTIFY013 | Error | Existing INPC host has no callable `OnPropertyChanged` invoker | — |
| NOTIFY014 | Warning | Target-side collection tracking is unsupported | — |
| NOTIFY015 | Warning | Collection tracking requires a reference source | — |
| NOTIFY016 | Error | Requested generated property name is not a valid C# identifier | — |
| NOTIFY017 | Error | Existing INPC changing host has no callable `OnPropertyChanging` invoker | — |

**NOTIFY001 and NOTIFY006 have a code fix** — click the lightbulb (or press `Ctrl+.` / `Cmd+.`) and select "Make type partial" to add the required modifier.

## Performance

NotifyGen is built for large codebases:

- **Incremental generation** - Only regenerates code for classes that actually changed
- **No runtime overhead** - All code is generated at compile time
- **Efficient equality checks** - Uses `EqualityComparer<T>.Default` for optimal performance
- **Lean generated setters** - No reflection, LINQ, closures, or avoidable boxing

The generator uses Roslyn's incremental compilation pipeline with proper `IEquatable<T>` implementations on all data structures, so your IDE stays responsive even with hundreds of `[Notify]` classes.

### Benchmark Results

Comparison against popular INPC libraries on .NET 9.0 (Apple M4):

#### Property Setters (String)

| Library | Mean | Ratio | Allocated |
|---------|-----:|------:|----------:|
| **NotifyGen** | **17.57 ns** | **1.00** | 48 B |
| CommunityToolkit.Mvvm | 18.47 ns | 1.05 | 48 B |
| Fody PropertyChanged | 18.50 ns | 1.05 | 48 B |
| Prism | 26.28 ns | 1.50 | 72 B |

#### Property Setters (Int)

| Library | Mean | Ratio | Allocated |
|---------|-----:|------:|----------:|
| Fody PropertyChanged | 0.46 ns | 0.92 | - |
| **NotifyGen** | **0.50 ns** | **1.00** | - |
| CommunityToolkit.Mvvm | 0.91 ns | 1.81 | - |
| Prism | 5.01 ns | 9.99 | 24 B |

#### Equality Guards (Same Value - No Event)

| Library | Mean | Ratio |
|---------|-----:|------:|
| Fody PropertyChanged | 0.48 ns | 0.93 |
| Prism | 0.50 ns | 0.97 |
| **NotifyGen** | **0.52 ns** | **1.00** |
| CommunityToolkit.Mvvm | 0.52 ns | 1.01 |

NotifyGen is the **fastest for string property setters** and competitive across all benchmarks. Primitive types (int, bool, double, etc.) use direct `==` comparison for optimal performance.

Run benchmarks yourself:
```bash
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks -- --filter *CompetitorBenchmarks*
```

## How It Compares

| | NotifyGen | Fody.PropertyChanged | CommunityToolkit.Mvvm |
|---|-----------|---------------------|----------------------|
| Approach | Source Generator | IL Weaving | Source Generator |
| Runtime dependency | None | None | Runtime library required |
| Debugging | Full—step through generated code | Limited—IL is modified | Full—step through generated code |
| Build impact | Runs during compile | Post-build step | Runs during compile |
| Equality checks | Always built-in | Configurable | Opt-in with attribute |
| Partial properties | ✅ C# 14/preview | ❌ Weaves existing properties | ✅ C# 14/preview |
| Property metadata forwarding | ✅ Property-targetable field attributes | ✅ Existing property metadata | ✅ Property/accessor metadata |
| Child property notifications | ✅ Opt-in BCL subscription | ✅ Weaving support varies | Runtime/reactive APIs |
| Existing INPC host reuse | ✅ Accessible invoker, no duplicate event | ✅ Weaves existing types | Base contract/runtime APIs |
| Collection membership notifications | ✅ Opt-in BCL subscription | ✅ Weaving support varies | Runtime/reactive APIs |
| Partial hooks | `OnXxxChanging` + `OnXxxChanged` | Intercept methods | `OnXxxChanging` only |
| INotifyPropertyChanging | ✅ `ImplementChanging = true` | ✅ Built-in | ✅ Separate attribute |
| Command CanExecute refresh | ✅ `[NotifyCanExecuteChangedFor]` | ❌ Manual | ✅ `[NotifyCanExecuteChangedFor]` |
| Batch notification suppression | ✅ `[NotifySuppressable]` | ❌ Not available | ❌ Not available |
| Learning curve | One attribute | Multiple attributes + config | Multiple attributes |
| **Performance** | **Fastest** | Fast | Good |

**When to use NotifyGen:** You want to eliminate INPC boilerplate with minimal setup. One attribute, done.

**When to use CommunityToolkit.Mvvm:** You need a full MVVM framework with commands, messaging, dependency injection, and more.

**When to use Fody:** You have an existing codebase using Fody, or you need IL-level modifications for other reasons.

## Requirements

- **.NET Standard 2.0+** — Compatible with:
  - .NET Framework 4.6.1+
  - .NET Core 3.1+
  - .NET 5, 6, 7, 8, 9, 10
  - Mono, Xamarin, Unity (2021.2+)
- **C# 9.0+** — Required for source generator support
- **C# 14/preview** — Required only for partial-property mode; field mode works with C# 9+

## Quick Reference

```csharp
[Notify]                              // Enable generation for this class
[Notify(ImplementChanging = true)]    // Also implement INotifyPropertyChanging
[NotifySuppressable]                  // Enable batch notification suppression
public partial class MyViewModel      // Must be partial
{
    private string _name;             // → public string Name { get; set; }

    [NotifyIgnore]
    private int _internal;            // No property generated

    [NotifyAlso("FullName")]
    private string _firstName;        // Also raises PropertyChanged for FullName

    [NotifyName("IsActive")]
    private bool _active;             // → public bool IsActive { get; set; }

    [NotifySetter(AccessLevel.Private)]
    private int _id;                  // → public int Id { get; private set; }

    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _canSave;            // Calls SaveCommand.NotifyCanExecuteChanged()

    public IRelayCommand SaveCommand { get; }

    // Optional hooks - implement only what you need
    partial void OnNameChanging(string oldValue, string newValue);
    partial void OnNameChanged();
}

// Batch updates (when [NotifySuppressable] is applied):
using (viewModel.SuppressNotifications())
{
    viewModel.Name = "New Name";
    viewModel.IsActive = true;
}  // PropertyChanged fires for both here
```

## Troubleshooting

**Properties not generating?**
1. Add `partial` to your class declaration
2. Make sure fields are `private` (not `public`, `protected`, or `internal`)
3. Fields must start with underscore: `_name`, not `name` or `m_name`
4. Rebuild the solution (Ctrl+Shift+B)

**IntelliSense not showing generated properties?**
- Restart Visual Studio/Rider
- Check Dependencies → Analyzers → NotifyGen in Solution Explorer
- Ensure the project builds successfully

**NOTIFY001: Class must be partial?**
```csharp
// Wrong
[Notify]
public class MyClass { }

// Right
[Notify]
public partial class MyClass { }
```

**NOTIFY002: No eligible fields?**

The diagnostic reports that the class has no eligible fields:
```
NOTIFY002: Class 'MyClass' is marked with [Notify] but has no private fields with underscore prefix (e.g., '_fieldName'). No properties will be generated.
```

Common fixes:
```csharp
// Wrong - no underscore prefix
private string name;           // Fix: private string _name;

// Wrong - readonly field
private readonly string _logger;   // Fix: Remove readonly or add [NotifyIgnore]

// Wrong - static field
private static string _shared;     // Fix: Remove static (static fields can't notify)

// Right - eligible fields
private string _name;
private int _age;
private bool? _isActive;
```

**NOTIFY004/NOTIFY005: Static or readonly fields?**

These are informational diagnostics. If you see them:
- Remove `static`, `const`, or `readonly` modifier if you want property generation
- Add `[NotifyIgnore]` to suppress the warning if the field should not generate a property

**NOTIFY006: Containing type is not partial?**

Every class, struct, interface, record, or record struct around a nested `[Notify]` class must be `partial`:
```csharp
// Wrong
public class Workspace
{
    [Notify]
    public partial class Editor { private string _title; }
}

// Right
public partial class Workspace
{
    [Notify]
    public partial class Editor { private string _title; }
}
```

Use the **Make type partial** code fix on each reported containing declaration.

**NOTIFY007: File-local type is not supported?**

C# restricts a `file` type to its declaring source file. NotifyGen emits partial declarations into a generated file, so neither a `[Notify]` class nor any of its containing types can use file-local accessibility:

```csharp
// Wrong - generated source cannot reopen FileOnlyViewModel
[Notify]
file partial class FileOnlyViewModel
{
    private string _name;
}

// Right
[Notify]
internal partial class ViewModel
{
    private string _name;
}
```

Replace `file` with an appropriate non-file accessibility modifier to enable generation.

**How to debug generated code?**
1. Build the project successfully
2. In Solution Explorer: Expand **Dependencies → Analyzers → NotifyGen → NotifyGen.Generator → NotifyGenerator**
3. You'll see `YourClass.g.cs` files - this is the generated code
4. Open them to see exactly what was generated
5. Set breakpoints in generated code during debugging

**Custom property names for migrations?**

`[NotifyName]` changes the generated property name, but the field must still follow the private underscore convention:
```csharp
[NotifyName("Name")]
private string _legacyName;  // Generates Name

[NotifyName("CustomerID")]
private int _legacyCustomerId;  // Generates CustomerID
```

Use this when the public property name must preserve an existing API. `[NotifyName]` does not make `m_name`, `mName`, or other ineligible field shapes eligible.

## Samples

The repository includes sample projects to help you get started:

### Console Sample (Cross-Platform)

A simple console app demonstrating NotifyGen's core property-generation features without UI framework dependencies:

```bash
dotnet run --project samples/NotifyGen.ConsoleSample
```

This sample shows:
- Basic property generation
- `[NotifyAlso]` for dependent properties
- `[NotifyName]` for custom property names
- `[NotifySetter]` for access control
- `[NotifyIgnore]` for excluded fields
- Partial hooks for validation and side effects
- Equality guards preventing duplicate events

### WPF Sample (Windows)

A WPF application demonstrating data binding with generated properties:

```bash
dotnet run --project samples/NotifyGen.WpfSample
```

## Benchmarks

[Performance benchmarks](benchmarks/NotifyGen.Benchmarks/README.md) compare generated setters with equivalent hand-written notification code:

```bash
# Run all benchmarks
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks

# Run competitor comparison
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks -- --filter *CompetitorBenchmarks*

# Run setter performance (NotifyGen vs hand-written)
dotnet run -c Release --project benchmarks/NotifyGen.Benchmarks -- --filter *SetterBenchmarks*
```

Benchmarks include:
- **Competitor comparison** — NotifyGen vs CommunityToolkit.Mvvm, Prism, and Fody PropertyChanged
- **Setter performance** — Generated setters vs hand-written (should be identical)
- **Generator performance** — Compilation time for 1, 10, and 100 classes
- **Incremental rebuild** — Time to rebuild when only one class changes

Multi-framework support: Benchmarks target .NET 8.0, 9.0, and 10.0.

## Contributing

Found a bug? Have a feature request? [Open an issue](https://github.com/georgepwall1991/NotifyGen/issues).

Want to contribute code? PRs are welcome. Please include tests for new functionality.

## License

MIT License — use it in personal projects, commercial projects, wherever. See [LICENSE](LICENSE) for details.
