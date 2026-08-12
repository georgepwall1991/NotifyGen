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

    /// <summary>
    /// NOTIFY013: Existing INPC host has no compatible invocation method.
    /// </summary>
    public static readonly DiagnosticDescriptor ExistingInpcRequiresInvoker = new(
        id: "NOTIFY013",
        title: "Existing INPC host has no compatible invoker",
        messageFormat: "Type '{0}' implements INotifyPropertyChanged but has no accessible instance OnPropertyChanged(string) or OnPropertyChanged(PropertyChangedEventArgs) method. NotifyGen cannot safely emit property notifications.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When a type reuses an existing INotifyPropertyChanged implementation, generated setters require an accessible ordinary OnPropertyChanged invoker."
    );

    /// <summary>
    /// NOTIFY014: Target-side collection tracking is unsupported.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyAlsoTargetCollectionUnsupported = new(
        id: "NOTIFY014",
        title: "Target-side NotifyAlso cannot track collection changes",
        messageFormat: "Target-side NotifyAlso on '{0}' cannot use NotifyOnCollectionChanged. Put collection tracking on the generated collection source member instead.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyFrom=true declares a dependency edge; collection tracking is source-side because it requires subscribing to the generated collection property."
    );

    /// <summary>
    /// NOTIFY015: Collection tracking requires a reference source.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyAlsoCollectionRequiresReference = new(
        id: "NOTIFY015",
        title: "Collection tracking requires a reference value",
        messageFormat: "Member '{0}' opts into NotifyOnCollectionChanged, but its type is not a reference value. CollectionChanged cannot be observed.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyOnCollectionChanged uses a runtime INotifyCollectionChanged subscription and therefore requires a reference-valued source."
    );

    /// <summary>
    /// NOTIFY016: Generated property name is not a C# identifier.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGeneratedPropertyName = new(
        id: "NOTIFY016",
        title: "Generated property name is invalid",
        messageFormat: "Member '{0}' requests generated property name '{1}', which is not a valid C# identifier. Generation is skipped for this member.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "NotifyGen cannot emit a property whose requested name is not a valid C# identifier."
    );

    /// <summary>
    /// NOTIFY017: Existing INPC changing host has no compatible invocation method.
    /// </summary>
    public static readonly DiagnosticDescriptor ExistingInpcChangingRequiresInvoker = new(
        id: "NOTIFY017",
        title: "Existing INPC changing host has no compatible invoker",
        messageFormat: "Type '{0}' implements INotifyPropertyChanging but has no accessible instance OnPropertyChanging(string) or OnPropertyChanging(PropertyChangingEventArgs) method. NotifyGen cannot safely emit changing notifications.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When a type reuses an existing INotifyPropertyChanging implementation, generated setters require an accessible ordinary OnPropertyChanging invoker."
    );

    /// <summary>
    /// NOTIFY018: [NotifyComputed] has no recognizable dependencies.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyComputedEmptyDependencies = new(
        id: "NOTIFY018",
        title: "NotifyComputed has no dependencies",
        messageFormat: "Property '{0}' is marked with [NotifyComputed] but has no recognizable this-property dependencies. Pass explicit names or read generated properties in the getter.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyComputed must name or infer at least one same-type property so NotifyGen can raise PropertyChanged for the computed member."
    );

    /// <summary>
    /// NOTIFY019: [NotifyComputed] cannot mark a generated source member.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyComputedOnGeneratedMember = new(
        id: "NOTIFY019",
        title: "NotifyComputed cannot mark a generated member",
        messageFormat: "Property '{0}' is generated by NotifyGen and cannot also be marked [NotifyComputed]. Use [NotifyComputed] on a read-only computed property instead.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Generated fields and incomplete partial properties are sources. Computed targets must be ordinary read-only properties."
    );

    /// <summary>
    /// NOTIFY020: [NotifyComputed] requires a get-only property.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyComputedRequiresGetOnlyProperty = new(
        id: "NOTIFY020",
        title: "NotifyComputed requires a get-only property",
        messageFormat: "Property '{0}' is marked with [NotifyComputed] but is not a get-only instance property. Use a non-static, non-indexer, get-only computed property.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyComputed only raises PropertyChanged for an existing getter. A setter would leave a stored value stale after a dependency change."
    );

    /// <summary>
    /// NOTIFY021: [NotifyComputed] getter is outside the allow-list.
    /// </summary>
    public static readonly DiagnosticDescriptor NotifyComputedUnsupportedGetter = new(
        id: "NOTIFY021",
        title: "NotifyComputed getter is not analyzable",
        messageFormat: "Property '{0}' is marked with [NotifyComputed] but its getter uses constructs NotifyGen does not walk. Pass explicit DependsOn names instead.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Auto-detect only accepts this-property reads, eligible underscore fields, and a closed set of expression operations. Method calls and foreign members require explicit DependsOn."
    );

    /// <summary>
    /// NOTIFY022: CommunityToolkit property attributes remain on a [Notify] type.
    /// </summary>
    public static readonly DiagnosticDescriptor ConvertCommunityToolkitOnNotifyType = new(
        id: "NOTIFY022",
        title: "Convert CommunityToolkit property attributes to NotifyGen",
        messageFormat: "Type '{0}' is marked with [Notify] but still has CommunityToolkit [ObservableProperty] or [NotifyPropertyChangedFor]. Convert them so NotifyGen owns the members and the CommunityToolkit generator does not emit duplicates.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [Notify] type that still carries CommunityToolkit property attributes should be converted to [NotifyProperty] / [NotifyComputed] to avoid dual generation."
    );

    /// <summary>
    /// NOTIFY023: CommunityToolkit property attributes without [Notify].
    /// </summary>
    public static readonly DiagnosticDescriptor ConvertCommunityToolkitType = new(
        id: "NOTIFY023",
        title: "Use NotifyGen for this CommunityToolkit type",
        messageFormat: "Type '{0}' uses CommunityToolkit [ObservableProperty] or [NotifyPropertyChangedFor]. Convert it to [Notify] with [NotifyProperty] so unmarked underscore fields stay private.",
        category: "NotifyGen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "NotifyGen can take over CommunityToolkit property generation one type at a time without publishing unmarked underscore fields."
    );
}
