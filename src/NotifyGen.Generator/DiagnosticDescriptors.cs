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
        description: "Classes marked with the [Notify] attribute must be declared as partial to allow the source generator to add the INotifyPropertyChanged implementation."
    );

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
        description: "The [Notify] attribute generates properties for private fields that follow the underscore naming convention (e.g., '_name' generates 'Name' property)."
    );

    /// <summary>
    /// NOTIFY003: NotifyAlso references unknown property.
    /// </summary>
    public static readonly DiagnosticDescriptor UnknownNotifyAlsoProperty = new(
        id: "NOTIFY003",
        title: "Unknown property in NotifyAlso",
        messageFormat: "Member '{0}' has [NotifyAlso(\"{1}\")] but property '{1}' does not exist on the class. This notification will have no effect.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [NotifyAlso] attribute on a field or partial property should reference an existing property name. Check for typos in the property name."
    );

    /// <summary>
    /// NOTIFY004: Static or const field cannot be used for property generation.
    /// </summary>
    public static readonly DiagnosticDescriptor StaticOrConstField = new(
        id: "NOTIFY004",
        title: "Static or const field not supported",
        messageFormat: "Static field '{0}' cannot be used for property generation. Only instance fields are supported. Remove the static/const modifier or add [NotifyIgnore].",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "NotifyGen only generates properties for instance fields. Static and const fields cannot trigger PropertyChanged events because they are shared across instances."
    );

    /// <summary>
    /// NOTIFY005: Readonly field cannot generate a property with a setter.
    /// </summary>
    public static readonly DiagnosticDescriptor ReadonlyField = new(
        id: "NOTIFY005",
        title: "Readonly field not supported",
        messageFormat: "Readonly field '{0}' cannot generate a property with a setter. Remove readonly modifier or add [NotifyIgnore] to suppress this warning.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Readonly fields cannot be modified after initialization, so a property setter cannot be generated. Consider making the field mutable or using a computed property instead."
    );

    /// <summary>
    /// NOTIFY006: A containing type of a notified nested class must be partial.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
        id: "NOTIFY006",
        title: "Containing type is not partial",
        messageFormat: "Containing type '{0}' must be partial so NotifyGen can generate members for nested class '{1}'",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every type containing a nested class marked with [Notify] must be partial so the source generator can reopen the complete declaration chain."
    );

    /// <summary>
    /// NOTIFY007: File-local types cannot be extended from generated source.
    /// </summary>
    public static readonly DiagnosticDescriptor FileLocalTypeNotSupported = new(
        id: "NOTIFY007",
        title: "File-local type is not supported",
        messageFormat: "Type '{0}' uses file accessibility and cannot participate in NotifyGen generation because generated source is emitted in a separate file",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "File-local types are visible only within their declaring source file, so a source generator cannot add a partial declaration from a generated file."
    );

    /// <summary>
    /// NOTIFY008: NotifyAlso dependency graph contains a cycle.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyAlsoDependencyCycle = new(
        id: "NOTIFY008",
        title: "NotifyAlso dependency cycle",
        messageFormat: "NotifyAlso dependency cycle detected: {0}. Break the cycle so property notifications have a finite dependency graph.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A NotifyAlso dependency cycle cannot produce a deterministic finite notification closure."
    );

    /// <summary>
    /// NOTIFY009: Multiple members would generate the same property name.
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratedPropertyNameCollision = new(
        id: "NOTIFY009",
        title: "Generated property name collision",
        messageFormat: "Multiple [Notify] members generate the property '{0}'. Rename one member so NotifyGen can generate an unambiguous property.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "NotifyGen cannot emit two properties with the same name."
    );

    /// <summary>
    /// NOTIFY010: Child notification tracking requires INotifyPropertyChanged.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyAlsoSubPropertyRequiresInpc = new(
        id: "NOTIFY010",
        title: "Sub-property notification requires a reference INotifyPropertyChanged child",
        messageFormat: "Member '{0}' opts into NotifyOnSubPropertyChanged, but its type is not a reference value implementing INotifyPropertyChanged. Child changes will not be observed.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyOnSubPropertyChanged can track child changes only for reference values that implement System.ComponentModel.INotifyPropertyChanged."
    );

    /// <summary>
    /// NOTIFY011: Target-side NotifyAlso requires a generated source property.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyAlsoTargetRequiresGeneratedSource = new(
        id: "NOTIFY011",
        title: "Target-side NotifyAlso requires a generated source",
        messageFormat: "Target-side NotifyAlso on '{0}' names source '{1}', but that source is not generated by NotifyGen. No notification edge will be emitted.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyAlso declarations with NotifyFrom=true must name a property generated by NotifyGen in the same type."
    );

    /// <summary>
    /// NOTIFY012: Target-side NotifyAlso cannot request child tracking.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyAlsoTargetSubPropertyUnsupported = new(
        id: "NOTIFY012",
        title: "Target-side NotifyAlso cannot track child changes",
        messageFormat: "Target-side NotifyAlso on '{0}' cannot use NotifyOnSubPropertyChanged. Put child tracking on the generated source member instead.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyFrom=true declares an explicit source-to-target dependency; child tracking must remain on the generated source member."
    );

}
