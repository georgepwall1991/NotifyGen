using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

/// <summary>
/// Represents metadata about a field that will become a generated property.
/// </summary>
internal readonly struct FieldInfo : IEquatable<FieldInfo>
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

    /// <summary>
    /// Whether the type is a primitive value type (int, bool, double, etc.)
    /// that supports direct == comparison for better performance.
    /// </summary>
    public bool IsPrimitiveType { get; }

    public FieldInfo(
        string fieldName,
        string propertyName,
        string typeName,
        bool isNullable,
        ImmutableArray<string> alsoNotify,
        string? setterAccess = null,
        bool isPrimitiveType = false)
    {
        FieldName = fieldName;
        PropertyName = propertyName;
        TypeName = typeName;
        IsNullable = isNullable;
        AlsoNotify = alsoNotify;
        SetterAccess = setterAccess;
        IsPrimitiveType = isPrimitiveType;
    }

    public bool Equals(FieldInfo other)
    {
        return FieldName == other.FieldName
            && PropertyName == other.PropertyName
            && TypeName == other.TypeName
            && IsNullable == other.IsNullable
            && SetterAccess == other.SetterAccess
            && IsPrimitiveType == other.IsPrimitiveType
            && AlsoNotify.SequenceEqual(other.AlsoNotify);
    }

    public override bool Equals(object? obj)
    {
        return obj is FieldInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (FieldName?.GetHashCode() ?? 0);
            hash = hash * 31 + (PropertyName?.GetHashCode() ?? 0);
            hash = hash * 31 + (TypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + IsNullable.GetHashCode();
            hash = hash * 31 + (SetterAccess?.GetHashCode() ?? 0);
            hash = hash * 31 + IsPrimitiveType.GetHashCode();
            hash = hash * 31 + AlsoNotify.Length;

            // Include first element hash for better distribution when arrays differ
            if (AlsoNotify.Length > 0)
            {
                hash = hash * 31 + (AlsoNotify[0]?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public static bool operator ==(FieldInfo left, FieldInfo right) => left.Equals(right);
    public static bool operator !=(FieldInfo left, FieldInfo right) => !left.Equals(right);
}
