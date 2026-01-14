using System.Collections.Immutable;

namespace NotifyGen.Generator;

/// <summary>
/// Represents metadata about a class marked with [Notify].
/// </summary>
internal readonly struct ClassInfo
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
    /// The fields to generate properties for.
    /// </summary>
    public ImmutableArray<FieldInfo> Fields { get; }

    public ClassInfo(
        string @namespace,
        string className,
        string typeParameters,
        string accessibility,
        bool alreadyImplementsInpc,
        ImmutableArray<FieldInfo> fields)
    {
        Namespace = @namespace;
        ClassName = className;
        TypeParameters = typeParameters;
        Accessibility = accessibility;
        AlreadyImplementsInpc = alreadyImplementsInpc;
        Fields = fields;
    }
}
