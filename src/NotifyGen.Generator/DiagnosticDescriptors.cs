using Microsoft.CodeAnalysis;

namespace NotifyGen.Generator;

/// <summary>
/// Diagnostic descriptors for NotifyGen analyzer warnings and errors.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>
    /// NOTIFY001: Class marked with [Notify] must be partial.
    /// </summary>
    public static readonly DiagnosticDescriptor ClassMustBePartial = new(
        id: "NOTIFY001",
        title: "Class must be partial",
        messageFormat: "Class '{0}' is marked with [Notify] but is not partial. Add the 'partial' modifier to enable source generation.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Classes marked with the [Notify] attribute must be declared as partial to allow the source generator to add the INotifyPropertyChanged implementation.");

    /// <summary>
    /// NOTIFY002: No eligible fields found in class.
    /// </summary>
    public static readonly DiagnosticDescriptor NoEligibleFields = new(
        id: "NOTIFY002",
        title: "No eligible fields found",
        messageFormat: "Class '{0}' is marked with [Notify] but has no private fields with underscore prefix (e.g., '_fieldName'). No properties will be generated.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [Notify] attribute generates properties for private fields that follow the underscore naming convention (e.g., '_name' generates 'Name' property).");

    /// <summary>
    /// NOTIFY003: NotifyAlso references unknown property.
    /// </summary>
    public static readonly DiagnosticDescriptor UnknownNotifyAlsoProperty = new(
        id: "NOTIFY003",
        title: "Unknown property in NotifyAlso",
        messageFormat: "Field '{0}' has [NotifyAlso(\"{1}\")] but property '{1}' does not exist on the class. This notification will have no effect.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [NotifyAlso] attribute should reference an existing property name. Check for typos in the property name.");
}
