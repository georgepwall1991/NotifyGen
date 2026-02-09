using System;

namespace NotifyGen;

/// <summary>
/// Enables batch notification suppression for a [Notify] class.
/// When applied, the generated class includes a SuppressNotifications() method
/// that returns an IDisposable to temporarily suppress PropertyChanged events.
/// All suppressed notifications are fired when the suppression scope ends.
/// </summary>
/// <remarks>
/// This feature is useful for bulk updates where you want to avoid firing
/// multiple PropertyChanged events during a batch operation.
/// </remarks>
/// <example>
/// <code>
/// [Notify]
/// [NotifySuppressable]
/// public partial class Person
/// {
///     private string _firstName;
///     private string _lastName;
/// }
///
/// // Usage:
/// using (person.SuppressNotifications())
/// {
///     person.FirstName = "John";
///     person.LastName = "Doe";
/// }  // PropertyChanged fires for FirstName and LastName here
///
/// // With AlwaysNotify - some properties always fire immediately:
/// [Notify]
/// [NotifySuppressable(AlwaysNotify = new[] { nameof(IsLoading) })]
/// public partial class ViewModel
/// {
///     private string _name;
///     private bool _isLoading;
/// }
///
/// using (vm.SuppressNotifications())
/// {
///     vm.Name = "John";        // Deferred
///     vm.IsLoading = true;     // Fires immediately (AlwaysNotify)
/// }  // Name notification fires here
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NotifySuppressableAttribute : Attribute
{
    /// <summary>
    /// Gets or sets property names that should NEVER be suppressed.
    /// These properties will fire PropertyChanged events immediately,
    /// even during a SuppressNotifications() scope.
    /// </summary>
    /// <remarks>
    /// Use this for critical properties that must always notify immediately,
    /// such as loading indicators or error flags.
    /// </remarks>
    public string[]? AlwaysNotify { get; set; }
}
