using System;
using System.Collections.Immutable;
using System.Linq;

namespace NotifyGen.Generator;

/// <summary>
/// Represents metadata about a class marked with [Notify].
/// </summary>
internal readonly struct ClassInfo : IEquatable<ClassInfo>
{
    /// <summary>
    /// The namespace containing the class (empty string if global namespace).
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// The class name (without type parameters).
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// The type parameters (e.g., "&lt;T&gt;" or "&lt;TKey, TValue&gt;"), empty if not generic.
    /// </summary>
    public string TypeParameters { get; }

    /// <summary>
    /// The accessibility modifier (public, internal, etc.).
    /// </summary>
    public string Accessibility { get; }

    /// <summary>
    /// Whether the class already implements INotifyPropertyChanged.
    /// </summary>
    public bool AlreadyImplementsInpc { get; }

    /// <summary>
    /// Whether the class already implements INotifyPropertyChanging.
    /// </summary>
    public bool AlreadyImplementsInpcChanging { get; }

    /// <summary>
    /// Whether to implement INotifyPropertyChanging (from NotifyAttribute.ImplementChanging).
    /// </summary>
    public bool ImplementChanging { get; }

    /// <summary>
    /// Whether to implement notification suppression (from NotifySuppressableAttribute).
    /// </summary>
    public bool IsSuppressable { get; }

    /// <summary>
    /// The fields to generate properties for.
    /// </summary>
    public ImmutableArray<FieldInfo> Fields { get; }

    public ClassInfo(
        string @namespace,
        string className,
        string typeParameters,
        string accessibility,
        bool alreadyImplementsInpc,
        bool alreadyImplementsInpcChanging,
        bool implementChanging,
        bool isSuppressable,
        ImmutableArray<FieldInfo> fields)
    {
        Namespace = @namespace;
        ClassName = className;
        TypeParameters = typeParameters;
        Accessibility = accessibility;
        AlreadyImplementsInpc = alreadyImplementsInpc;
        AlreadyImplementsInpcChanging = alreadyImplementsInpcChanging;
        ImplementChanging = implementChanging;
        IsSuppressable = isSuppressable;
        Fields = fields;
    }

    public bool Equals(ClassInfo other)
    {
        return Namespace == other.Namespace
            && ClassName == other.ClassName
            && TypeParameters == other.TypeParameters
            && Accessibility == other.Accessibility
            && AlreadyImplementsInpc == other.AlreadyImplementsInpc
            && AlreadyImplementsInpcChanging == other.AlreadyImplementsInpcChanging
            && ImplementChanging == other.ImplementChanging
            && IsSuppressable == other.IsSuppressable
            && Fields.SequenceEqual(other.Fields);
    }

    public override bool Equals(object? obj)
    {
        return obj is ClassInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
            hash = hash * 31 + (ClassName?.GetHashCode() ?? 0);
            hash = hash * 31 + (TypeParameters?.GetHashCode() ?? 0);
            hash = hash * 31 + (Accessibility?.GetHashCode() ?? 0);
            hash = hash * 31 + AlreadyImplementsInpc.GetHashCode();
            hash = hash * 31 + AlreadyImplementsInpcChanging.GetHashCode();
            hash = hash * 31 + ImplementChanging.GetHashCode();
            hash = hash * 31 + IsSuppressable.GetHashCode();
            hash = hash * 31 + Fields.Length;

            // Include first field's hash for better distribution
            if (Fields.Length > 0)
            {
                hash = hash * 31 + Fields[0].GetHashCode();
            }

            return hash;
        }
    }

    public static bool operator ==(ClassInfo left, ClassInfo right) => left.Equals(right);
    public static bool operator !=(ClassInfo left, ClassInfo right) => !left.Equals(right);
}
