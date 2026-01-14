using System.Collections.Immutable;

namespace NotifyGen.Generator;

/// <summary>
/// Represents metadata about a field that will become a generated property.
/// </summary>
internal readonly struct FieldInfo
{
    /// <summary>
    /// The field name (e.g., "_name").
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// The generated property name (e.g., "Name").
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// The fully qualified type name of the field.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Whether the type is nullable (reference type with ? or Nullable&lt;T&gt;).
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Additional property names to notify when this field changes.
    /// </summary>
    public ImmutableArray<string> AlsoNotify { get; }

    /// <summary>
    /// The access modifier for the setter (e.g., "private", "protected").
    /// Null means use the same access as the property (public).
    /// </summary>
    public string? SetterAccess { get; }

    public FieldInfo(
        string fieldName,
        string propertyName,
        string typeName,
        bool isNullable,
        ImmutableArray<string> alsoNotify,
        string? setterAccess = null)
    {
        FieldName = fieldName;
        PropertyName = propertyName;
        TypeName = typeName;
        IsNullable = isNullable;
        AlsoNotify = alsoNotify;
        SetterAccess = setterAccess;
    }
}
