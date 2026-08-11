using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

/// <summary>
/// Represents metadata about a field that will become a generated property.
/// </summary>
internal readonly struct FieldInfo : IEquatable<FieldInfo>
{
    public string FieldName { get; }
    public string PropertyName { get; }
    public string TypeName { get; }
    public bool IsNullable { get; }
    public ImmutableArray<string> AlsoNotify { get; }
    public ImmutableArray<string> SubPropertyNotify { get; }
    public ImmutableArray<string> CollectionNotify { get; }
    public ImmutableArray<string> CommandsToNotify { get; }
    public string? SetterAccess { get; }
    public bool IsPrimitiveType { get; }
    public bool RequiresUnsafe { get; }
    public bool IsPartialProperty { get; }
    public string PropertyAccessibility { get; }
    public bool NeedsNullableBackingField { get; }
    public string? GetterAccess { get; }
    public bool HasNonPartialTypedChangedHook { get; }
    public string? ExistingTypedChangedHookParameterTypeName { get; }
    public string? ExistingTypedChangedHookNewParameterTypeName { get; }
    public ImmutableArray<string> PropertyAttributes { get; }
    public ImmutableArray<string> GetterAttributes { get; }
    public ImmutableArray<string> SetterAttributes { get; }

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
        ImmutableArray<string> getterAttributes = default,
        ImmutableArray<string> setterAttributes = default,
        ImmutableArray<string> subPropertyNotify = default,
        ImmutableArray<string> collectionNotify = default,
        bool hasNonPartialTypedChangedHook = false,
        string? existingTypedChangedHookParameterTypeName = null,
        string? existingTypedChangedHookNewParameterTypeName = null
    )
    {
        FieldName = fieldName;
        PropertyName = propertyName;
        TypeName = typeName;
        IsNullable = isNullable;
        AlsoNotify = alsoNotify;
        CommandsToNotify = commandsToNotify;
        SetterAccess = setterAccess;
        IsPrimitiveType = isPrimitiveType;
        RequiresUnsafe = requiresUnsafe;
        IsPartialProperty = isPartialProperty;
        PropertyAccessibility = propertyAccessibility;
        NeedsNullableBackingField = needsNullableBackingField;
        GetterAccess = getterAccess;
        HasNonPartialTypedChangedHook = hasNonPartialTypedChangedHook;
        ExistingTypedChangedHookParameterTypeName = existingTypedChangedHookParameterTypeName;
        ExistingTypedChangedHookNewParameterTypeName = existingTypedChangedHookNewParameterTypeName;
        PropertyAttributes = propertyAttributes.IsDefault
            ? ImmutableArray<string>.Empty
            : propertyAttributes;
        GetterAttributes = getterAttributes.IsDefault
            ? ImmutableArray<string>.Empty
            : getterAttributes;
        SetterAttributes = setterAttributes.IsDefault
            ? ImmutableArray<string>.Empty
            : setterAttributes;
        SubPropertyNotify = subPropertyNotify.IsDefault
            ? ImmutableArray<string>.Empty
            : subPropertyNotify;
        CollectionNotify = collectionNotify.IsDefault
            ? ImmutableArray<string>.Empty
            : collectionNotify;
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
            GetterAttributes,
            SetterAttributes,
            SubPropertyNotify,
            CollectionNotify,
            HasNonPartialTypedChangedHook,
            ExistingTypedChangedHookParameterTypeName,
            ExistingTypedChangedHookNewParameterTypeName
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
            && HasNonPartialTypedChangedHook == other.HasNonPartialTypedChangedHook
            && ExistingTypedChangedHookParameterTypeName == other.ExistingTypedChangedHookParameterTypeName
            && ExistingTypedChangedHookNewParameterTypeName == other.ExistingTypedChangedHookNewParameterTypeName
            && PropertyAttributes.SequenceEqual(other.PropertyAttributes)
            && GetterAttributes.SequenceEqual(other.GetterAttributes)
            && SetterAttributes.SequenceEqual(other.SetterAttributes)
            && SubPropertyNotify.SequenceEqual(other.SubPropertyNotify)
            && CollectionNotify.SequenceEqual(other.CollectionNotify)
            && AlsoNotify.SequenceEqual(other.AlsoNotify)
            && CommandsToNotify.SequenceEqual(other.CommandsToNotify);
    }

    public override bool Equals(object? obj) => obj is FieldInfo other && Equals(other);

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
            hash = hash * 31 + HasNonPartialTypedChangedHook.GetHashCode();
            hash = hash * 31 + (ExistingTypedChangedHookParameterTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ExistingTypedChangedHookNewParameterTypeName?.GetHashCode() ?? 0);
            foreach (var attribute in PropertyAttributes)
                hash = hash * 31 + (attribute?.GetHashCode() ?? 0);
            foreach (var attribute in GetterAttributes)
                hash = hash * 31 + (attribute?.GetHashCode() ?? 0);
            foreach (var attribute in SetterAttributes)
                hash = hash * 31 + (attribute?.GetHashCode() ?? 0);
            foreach (var propertyName in SubPropertyNotify)
                hash = hash * 31 + (propertyName?.GetHashCode() ?? 0);
            foreach (var propertyName in CollectionNotify)
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
