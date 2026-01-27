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
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NotifySuppressableAttribute : Attribute
{
}
