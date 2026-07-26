using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

internal enum TypeDeclarationKind
{
    Class,
    Struct,
    Interface,
    RecordClass,
    RecordStruct,
}

internal readonly struct TypeDeclarationInfo : IEquatable<TypeDeclarationInfo>
{
    public TypeDeclarationKind Kind { get; }
    public string Name { get; }
    public string MetadataName { get; }
    public string Accessibility { get; }
    public ImmutableArray<string> RequiredModifiers { get; }
    public ImmutableArray<string> TypeParameters { get; }
    public ImmutableArray<string> ConstraintClauses { get; }
    public string MetadataIdentity { get; }
    public bool IsPartial { get; }

    public string Keyword =>
        Kind switch
        {
            TypeDeclarationKind.Class => "class",
            TypeDeclarationKind.Struct => "struct",
            TypeDeclarationKind.Interface => "interface",
            TypeDeclarationKind.RecordClass => "record class",
            TypeDeclarationKind.RecordStruct => "record struct",
            _ => throw new InvalidOperationException($"Unsupported declaration kind: {Kind}"),
        };

    public string TypeParameterList =>
        TypeParameters.Length == 0 ? string.Empty : $"<{string.Join(", ", TypeParameters)}>";

    public TypeDeclarationInfo(
        TypeDeclarationKind kind,
        string name,
        string metadataName,
        string accessibility,
        ImmutableArray<string> requiredModifiers,
        ImmutableArray<string> typeParameters,
        ImmutableArray<string> constraintClauses,
        string metadataIdentity,
        bool isPartial
    )
    {
        Kind = kind;
        Name = name;
        MetadataName = metadataName;
        Accessibility = accessibility;
        RequiredModifiers = requiredModifiers;
        TypeParameters = typeParameters;
        ConstraintClauses = constraintClauses;
        MetadataIdentity = metadataIdentity;
        IsPartial = isPartial;
    }

    public bool Equals(TypeDeclarationInfo other) =>
        Kind == other.Kind
        && Name == other.Name
        && MetadataName == other.MetadataName
        && Accessibility == other.Accessibility
        && RequiredModifiers.SequenceEqual(other.RequiredModifiers)
        && TypeParameters.SequenceEqual(other.TypeParameters)
        && ConstraintClauses.SequenceEqual(other.ConstraintClauses)
        && MetadataIdentity == other.MetadataIdentity
        && IsPartial == other.IsPartial;

    public override bool Equals(object? obj) => obj is TypeDeclarationInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + Kind.GetHashCode();
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + MetadataName.GetHashCode();
            hash = hash * 31 + Accessibility.GetHashCode();
            foreach (var modifier in RequiredModifiers)
                hash = hash * 31 + modifier.GetHashCode();
            foreach (var parameter in TypeParameters)
                hash = hash * 31 + parameter.GetHashCode();
            foreach (var clause in ConstraintClauses)
                hash = hash * 31 + clause.GetHashCode();
            hash = hash * 31 + MetadataIdentity.GetHashCode();
            hash = hash * 31 + IsPartial.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(TypeDeclarationInfo left, TypeDeclarationInfo right) =>
        left.Equals(right);

    public static bool operator !=(TypeDeclarationInfo left, TypeDeclarationInfo right) =>
        !left.Equals(right);
}
