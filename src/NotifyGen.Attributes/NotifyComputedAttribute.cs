using System;

namespace NotifyGen;

/// <summary>
/// Marks a read-only computed property so NotifyGen raises PropertyChanged for it
/// when its dependencies change. With no arguments the generator walks a bounded
/// getter; pass property names to declare the graph explicitly.
/// </summary>
/// <example>
/// <code>
/// [NotifyComputed]
/// public string FullName => $"{FirstName} {LastName}";
///
/// [NotifyComputed(nameof(FirstName), nameof(LastName))]
/// public string Initials => string.Concat(FirstName.AsSpan(0, 1), LastName.AsSpan(0, 1));
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class NotifyComputedAttribute : Attribute
{
    /// <summary>
    /// Gets the explicit source property names, or an empty list when the
    /// generator should infer dependencies from the getter.
    /// </summary>
    public string[] DependsOn { get; }

    /// <summary>
    /// Creates a computed-property marker that infers dependencies from the getter.
    /// </summary>
    public NotifyComputedAttribute()
    {
        DependsOn = Array.Empty<string>();
    }

    /// <summary>
    /// Creates a computed-property marker with an explicit dependency list.
    /// </summary>
    /// <param name="dependsOn">Generated or computed property names this value reads.</param>
    public NotifyComputedAttribute(params string[] dependsOn)
    {
        DependsOn = dependsOn ?? throw new ArgumentNullException(nameof(dependsOn));
    }
}
