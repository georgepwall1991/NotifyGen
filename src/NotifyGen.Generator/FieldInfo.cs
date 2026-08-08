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
    /// Direct dependent properties to notify when a child INPC raises a change.
    /// </summary>
    public ImmutableArray<string> SubPropertyNotify { get; }

    /// <summary>
    /// Command names to call NotifyCanExecuteChanged() on when this field changes.
    /// </summary>
    public ImmutableArray<string> CommandsToNotify { get; }

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

    /// <summary>
    /// Whether emitting this field's type requires an unsafe declaration context.
    /// </summary>
    public bool RequiresUnsafe { get; }

    /// <summary>
    /// Whether this metadata describes an incomplete partial property rather than a field.
    /// </summary>
    public bool IsPartialProperty { get; }

    /// <summary>
    /// The accessibility of the declared property.
    /// </summary>
    public string PropertyAccessibility { get; }

    /// <summary>
    /// Whether a partial property's synthesized field needs nullable-flow attributes.
    /// </summary>
    public bool NeedsNullableBackingField { get; }

    /// <summary>
    /// An explicit getter accessibility modifier, if any.
    /// </summary>
    public string? GetterAccess { get; }

    /// <summary>
    /// Source attributes that are valid on the generated property.
    /// </summary>
    public ImmutableArray<string> PropertyAttributes { get; }

    public FieldInfo(
        string fieldName,
        string propertyName,
        string typeName,
        bool isNullable,
        ImmutableArray<string> alsoNotify,
        ImmutableArray<string> commandsToNotify,
        string? setterAccess = null,
        bool isPrimitiveType = false,
        bool requiresUnsafe = false,
        bool isPartialProperty = false,
        string propertyAccessibility = "public",
        bool needsNullableBackingField = false,
        string? getterAccess = null,
        ImmutableArray<string> propertyAttributes = default,
        ImmutableArray<string> subPropertyNotify = default
    )
    {
        FieldName = fieldName;
        PropertyName = propertyName;
        TypeName = typeName;
        IsNullable = isNullable;
        AlsoNotify = alsoNotify;
        SubPropertyNotify = subPropertyNotify;
        CommandsToNotify = commandsToNotify;
        SetterAccess = setterAccess;
        IsPrimitiveType = isPrimitiveType;
        RequiresUnsafe = requiresUnsafe;
        IsPartialProperty = isPartialProperty;
        PropertyAccessibility = propertyAccessibility;
        NeedsNullableBackingField = needsNullableBackingField;
        GetterAccess = getterAccess;
        PropertyAttributes = propertyAttributes.IsDefault
            ? ImmutableArray<string>.Empty
            : propertyAttributes;
        SubPropertyNotify = subPropertyNotify.IsDefault
            ? ImmutableArray<string>.Empty
            : subPropertyNotify;
    }

    public FieldInfo WithAlsoNotify(ImmutableArray<string> alsoNotify) =>
        new(
            FieldName,
            PropertyName,
            TypeName,
            IsNullable,
            alsoNotify,
            CommandsToNotify,
            SetterAccess,
            IsPrimitiveType,
            RequiresUnsafe,
            IsPartialProperty,
            PropertyAccessibility,
            NeedsNullableBackingField,
            GetterAccess,
            PropertyAttributes,
            SubPropertyNotify
        );

    public bool Equals(FieldInfo other)
    {
        return FieldName == other.FieldName
            && PropertyName == other.PropertyName
            && TypeName == other.TypeName
            && IsNullable == other.IsNullable
            && SetterAccess == other.SetterAccess
            && IsPrimitiveType == other.IsPrimitiveType
            && RequiresUnsafe == other.RequiresUnsafe
            && IsPartialProperty == other.IsPartialProperty
            && PropertyAccessibility == other.PropertyAccessibility
            && NeedsNullableBackingField == other.NeedsNullableBackingField
            && GetterAccess == other.GetterAccess
            && PropertyAttributes.SequenceEqual(other.PropertyAttributes)
            && SubPropertyNotify.SequenceEqual(other.SubPropertyNotify)
            && AlsoNotify.SequenceEqual(other.AlsoNotify)
            && CommandsToNotify.SequenceEqual(other.CommandsToNotify);
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
            hash = hash * 31 + RequiresUnsafe.GetHashCode();
            hash = hash * 31 + IsPartialProperty.GetHashCode();
            hash = hash * 31 + (PropertyAccessibility?.GetHashCode() ?? 0);
            hash = hash * 31 + NeedsNullableBackingField.GetHashCode();
            hash = hash * 31 + (GetterAccess?.GetHashCode() ?? 0);
            foreach (var attribute in PropertyAttributes)
                hash = hash * 31 + (attribute?.GetHashCode() ?? 0);
            foreach (var propertyName in SubPropertyNotify)
                hash = hash * 31 + (propertyName?.GetHashCode() ?? 0);
            foreach (var propertyName in AlsoNotify)
                hash = hash * 31 + (propertyName?.GetHashCode() ?? 0);
            foreach (var commandName in CommandsToNotify)
                hash = hash * 31 + (commandName?.GetHashCode() ?? 0);

            return hash;
        }
    }

    public static bool operator ==(FieldInfo left, FieldInfo right) => left.Equals(right);

    public static bool operator !=(FieldInfo left, FieldInfo right) => !left.Equals(right);
}
