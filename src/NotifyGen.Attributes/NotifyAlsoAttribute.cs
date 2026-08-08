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
///
/// [NotifyAlso(nameof(DisplayName), NotifyOnSubPropertyChanged = true)]
/// private Address? _address;
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = true
)]
public sealed class NotifyAlsoAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the property that should also be notified when this field changes.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets or sets whether changes raised by the source property's child
    /// INotifyPropertyChanged object should also notify <see cref="PropertyName"/>.
    /// </summary>
    public bool NotifyOnSubPropertyChanged { get; set; }

    /// <summary>
    /// Creates a new instance of NotifyAlsoAttribute.
    /// </summary>
    /// <param name="propertyName">The name of the dependent property to notify.</param>
    public NotifyAlsoAttribute(string propertyName)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
    }
}
