using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

internal readonly struct NotificationTypeInfo : IEquatable<NotificationTypeInfo>
{
    public string Namespace { get; }
    public ImmutableArray<TypeDeclarationInfo> TypeDeclarations { get; }
    public bool AlreadyImplementsInpc { get; }
    public bool AlreadyImplementsInpcChanging { get; }
    public bool ImplementChanging { get; }
    public bool IsSuppressable { get; }
    public ImmutableArray<string> AlwaysNotifyProperties { get; }
    public ImmutableArray<FieldInfo> Fields { get; }
    public TypeDeclarationInfo TargetType => TypeDeclarations[TypeDeclarations.Length - 1];
    public bool CanGenerate =>
        TypeDeclarations.Length > 0
        && TypeDeclarations.All(static declaration => declaration.IsPartial)
        && Fields.Length > 0;

    public NotificationTypeInfo(
        string @namespace,
        ImmutableArray<TypeDeclarationInfo> typeDeclarations,
        bool alreadyImplementsInpc,
        bool alreadyImplementsInpcChanging,
        bool implementChanging,
        bool isSuppressable,
        ImmutableArray<string> alwaysNotifyProperties,
        ImmutableArray<FieldInfo> fields
    )
    {
        Namespace = @namespace;
        TypeDeclarations = typeDeclarations;
        AlreadyImplementsInpc = alreadyImplementsInpc;
        AlreadyImplementsInpcChanging = alreadyImplementsInpcChanging;
        ImplementChanging = implementChanging;
        IsSuppressable = isSuppressable;
        AlwaysNotifyProperties = alwaysNotifyProperties;
        Fields = fields;
    }

    public bool Equals(NotificationTypeInfo other) =>
        Namespace == other.Namespace
        && TypeDeclarations.SequenceEqual(other.TypeDeclarations)
        && AlreadyImplementsInpc == other.AlreadyImplementsInpc
        && AlreadyImplementsInpcChanging == other.AlreadyImplementsInpcChanging
        && ImplementChanging == other.ImplementChanging
        && IsSuppressable == other.IsSuppressable
        && AlwaysNotifyProperties.SequenceEqual(other.AlwaysNotifyProperties)
        && Fields.SequenceEqual(other.Fields);

    public override bool Equals(object? obj) => obj is NotificationTypeInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + Namespace.GetHashCode();
            foreach (var declaration in TypeDeclarations)
                hash = hash * 31 + declaration.GetHashCode();
            hash = hash * 31 + AlreadyImplementsInpc.GetHashCode();
            hash = hash * 31 + AlreadyImplementsInpcChanging.GetHashCode();
            hash = hash * 31 + ImplementChanging.GetHashCode();
            hash = hash * 31 + IsSuppressable.GetHashCode();
            foreach (var propertyName in AlwaysNotifyProperties)
                hash = hash * 31 + propertyName.GetHashCode();
            foreach (var field in Fields)
                hash = hash * 31 + field.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(NotificationTypeInfo left, NotificationTypeInfo right) =>
        left.Equals(right);

    public static bool operator !=(NotificationTypeInfo left, NotificationTypeInfo right) =>
        !left.Equals(right);
}
