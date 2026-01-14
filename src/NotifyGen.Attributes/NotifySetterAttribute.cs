using System;

namespace NotifyGen;

/// <summary>
/// Specifies the access level for the generated property setter.
/// By default, setters are public. Use this attribute to restrict setter visibility.
/// </summary>
/// <example>
/// <code>
/// [NotifySetter(AccessLevel.Private)]
/// private int _id;  // Generates: public int Id { get; private set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class NotifySetterAttribute : Attribute
{
    /// <summary>
    /// Gets the access level for the setter.
    /// </summary>
    public AccessLevel Access { get; }

    /// <summary>
    /// Creates a new instance of NotifySetterAttribute.
    /// </summary>
    /// <param name="access">The access level for the setter.</param>
    public NotifySetterAttribute(AccessLevel access)
    {
        Access = access;
    }
}
