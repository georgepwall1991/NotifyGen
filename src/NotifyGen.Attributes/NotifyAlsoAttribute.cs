using System;

namespace NotifyGen;

/// <summary>
/// Indicates that changing this field should also raise PropertyChanged for additional properties.
/// Useful for computed properties that depend on this field.
/// </summary>
/// <example>
/// <code>
/// [NotifyAlso("FullName")]
/// private string _firstName;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public sealed class NotifyAlsoAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the property that should also be notified when this field changes.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Creates a new instance of NotifyAlsoAttribute.
    /// </summary>
    /// <param name="propertyName">The name of the dependent property to notify.</param>
    public NotifyAlsoAttribute(string propertyName)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
    }
}
