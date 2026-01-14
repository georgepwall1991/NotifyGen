using System;

namespace NotifyGen;

/// <summary>
/// Specifies a custom property name for the generated property instead of using the default
/// naming convention (e.g., _fieldName -> FieldName).
/// </summary>
/// <example>
/// <code>
/// [NotifyName("IsVisible")]
/// private bool _visibleState;  // Generates IsVisible instead of VisibleState
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class NotifyNameAttribute : Attribute
{
    /// <summary>
    /// Gets the custom name for the generated property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a new instance of NotifyNameAttribute.
    /// </summary>
    /// <param name="name">The custom name for the generated property.</param>
    public NotifyNameAttribute(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Property name cannot be null or empty.", nameof(name));
        Name = name;
    }
}
