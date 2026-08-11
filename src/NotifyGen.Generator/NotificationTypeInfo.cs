using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

internal readonly struct NotificationTypeInfo : IEquatable<NotificationTypeInfo>
{
    public string Namespace { get; }
    public ImmutableArray<TypeDeclarationInfo> TypeDeclarations { get; }
    public bool AlreadyImplementsInpc { get; }
    public PropertyChangedInvokerKind PropertyChangedInvoker { get; }
    public bool AlreadyImplementsInpcChanging { get; }
    public PropertyChangingInvokerKind PropertyChangingInvoker { get; }
    public bool ImplementChanging { get; }
    public bool IsSuppressable { get; }
    public ImmutableArray<string> AlwaysNotifyProperties { get; }
    public ImmutableArray<string> MemberNames { get; }
    public ImmutableArray<FieldInfo> Fields { get; }
    public TypeDeclarationInfo TargetType => TypeDeclarations[TypeDeclarations.Length - 1];
    public bool CanGenerate =>
        TypeDeclarations.Length > 0
        && TypeDeclarations.All(static declaration => declaration.IsPartial)
        && Fields.Length > 0
        && (!AlreadyImplementsInpc || PropertyChangedInvoker != PropertyChangedInvokerKind.None)
        && (!ImplementChanging || !AlreadyImplementsInpcChanging
            || PropertyChangingInvoker != PropertyChangingInvokerKind.None);

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
        : this(
            @namespace,
            typeDeclarations,
            alreadyImplementsInpc,
            PropertyChangedInvokerKind.Generated,
            alreadyImplementsInpcChanging,
            PropertyChangingInvokerKind.Generated,
            implementChanging,
            isSuppressable,
            alwaysNotifyProperties,
            fields
        ) { }

    public NotificationTypeInfo(
        string @namespace,
        ImmutableArray<TypeDeclarationInfo> typeDeclarations,
        bool alreadyImplementsInpc,
        PropertyChangedInvokerKind propertyChangedInvoker,
        bool alreadyImplementsInpcChanging,
        PropertyChangingInvokerKind propertyChangingInvoker,
        bool implementChanging,
        bool isSuppressable,
        ImmutableArray<string> alwaysNotifyProperties,
        ImmutableArray<FieldInfo> fields,
        ImmutableArray<string> memberNames = default
    )
    {
        Namespace = @namespace;
        TypeDeclarations = typeDeclarations;
        AlreadyImplementsInpc = alreadyImplementsInpc;
        PropertyChangedInvoker = propertyChangedInvoker;
        AlreadyImplementsInpcChanging = alreadyImplementsInpcChanging;
        PropertyChangingInvoker = propertyChangingInvoker;
        ImplementChanging = implementChanging;
        IsSuppressable = isSuppressable;
        AlwaysNotifyProperties = alwaysNotifyProperties;
        MemberNames = memberNames.IsDefault
            ? ImmutableArray<string>.Empty
            : memberNames;
        Fields = fields;
    }

    public bool Equals(NotificationTypeInfo other) =>
        Namespace == other.Namespace
        && TypeDeclarations.SequenceEqual(other.TypeDeclarations)
        && AlreadyImplementsInpc == other.AlreadyImplementsInpc
        && PropertyChangedInvoker == other.PropertyChangedInvoker
        && AlreadyImplementsInpcChanging == other.AlreadyImplementsInpcChanging
        && PropertyChangingInvoker == other.PropertyChangingInvoker
        && ImplementChanging == other.ImplementChanging
        && IsSuppressable == other.IsSuppressable
        && AlwaysNotifyProperties.SequenceEqual(other.AlwaysNotifyProperties)
        && MemberNames.SequenceEqual(other.MemberNames)
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
            hash = hash * 31 + PropertyChangedInvoker.GetHashCode();
            hash = hash * 31 + AlreadyImplementsInpcChanging.GetHashCode();
            hash = hash * 31 + PropertyChangingInvoker.GetHashCode();
            hash = hash * 31 + ImplementChanging.GetHashCode();
            hash = hash * 31 + IsSuppressable.GetHashCode();
            foreach (var propertyName in AlwaysNotifyProperties)
                hash = hash * 31 + propertyName.GetHashCode();
            foreach (var memberName in MemberNames)
                hash = hash * 31 + memberName.GetHashCode();
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
