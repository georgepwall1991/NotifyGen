using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NotifyGen.Generator;

/// <summary>
/// Incremental source generator that generates INotifyPropertyChanged implementation
/// for classes marked with [Notify].
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NotifyGenerator : IIncrementalGenerator
{
    private const string NotifyAttributeName = "NotifyGen.NotifyAttribute";
    private const string NotifyIgnoreAttributeName = "NotifyGen.NotifyIgnoreAttribute";
    private const string NotifyAlsoAttributeName = "NotifyGen.NotifyAlsoAttribute";
    private const string NotifyNameAttributeName = "NotifyGen.NotifyNameAttribute";
    private const string NotifySetterAttributeName = "NotifyGen.NotifySetterAttribute";
    private const string NotifyCanExecuteChangedForAttributeName = "NotifyGen.NotifyCanExecuteChangedForAttribute";
    private const string NotifySuppressableAttributeName = "NotifyGen.NotifySuppressableAttribute";

    /// <summary>
    /// Cached SymbolDisplayFormat for type names to avoid repeated allocations.
    /// </summary>
    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations with [Notify] attribute
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, ct) => GetClassInfo(ctx, ct))
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value);

        // Generate source for each class
        context.RegisterSourceOutput(classDeclarations, GenerateSource);
    }

    /// <summary>
    /// Quick syntax check to filter candidate classes.
    /// </summary>
    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl
            && classDecl.AttributeLists.Count > 0
            && classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    /// Extracts class information from the semantic model.
    /// </summary>
    private static ClassInfo? GetClassInfo(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Get the class symbol
        if (semanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol classSymbol)
            return null;

        // Check if it has [Notify] attribute
        var notifyAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyAttributeName);

        if (notifyAttribute == null)
            return null;

        // Check for ImplementChanging property on the attribute
        var implementChanging = notifyAttribute.NamedArguments
            .FirstOrDefault(a => a.Key == "ImplementChanging")
            .Value.Value is true;

        // Check for [NotifySuppressable] attribute and extract AlwaysNotify property
        var suppressableAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifySuppressableAttributeName);

        var isSuppressable = suppressableAttribute != null;
        var alwaysNotifyProperties = ImmutableArray<string>.Empty;

        if (suppressableAttribute != null)
        {
            // Extract AlwaysNotify property from the attribute
            var alwaysNotifyArg = suppressableAttribute.NamedArguments
                .FirstOrDefault(a => a.Key == "AlwaysNotify");

            if (alwaysNotifyArg.Value.Kind == TypedConstantKind.Array)
            {
                alwaysNotifyProperties = alwaysNotifyArg.Value.Values
                    .Where(v => v.Value is string)
                    .Select(v => (string)v.Value!)
                    .ToImmutableArray();
            }
        }

        // Check if already implements INotifyPropertyChanged
        var inpcInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanged");
        var alreadyImplementsInpc = inpcInterface != null
            && classSymbol.AllInterfaces.Contains(inpcInterface, SymbolEqualityComparer.Default);

        // Check if already implements INotifyPropertyChanging
        var inpcChangingInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanging");
        var alreadyImplementsInpcChanging = inpcChangingInterface != null
            && classSymbol.AllInterfaces.Contains(inpcChangingInterface, SymbolEqualityComparer.Default);

        // Extract field information
        var fields = ExtractFields(classSymbol, ct);

        // Get accessibility
        var accessibility = classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal"
        };

        // Get namespace (check for global namespace)
        var ns = classSymbol.ContainingNamespace;
        var namespaceName = ns != null && !ns.IsGlobalNamespace
            ? ns.ToDisplayString()
            : string.Empty;

        // Get type parameters for generic classes
        var typeParameters = classSymbol.TypeParameters.Length > 0
            ? "<" + string.Join(", ", classSymbol.TypeParameters.Select(tp => tp.Name)) + ">"
            : string.Empty;

        return new ClassInfo(
            namespaceName,
            classSymbol.Name,
            typeParameters,
            accessibility,
            alreadyImplementsInpc,
            alreadyImplementsInpcChanging,
            implementChanging,
            isSuppressable,
            alwaysNotifyProperties,
            fields);
    }

    /// <summary>
    /// Extracts field information from the class.
    /// </summary>
    private static ImmutableArray<FieldInfo> ExtractFields(INamedTypeSymbol classSymbol, CancellationToken ct)
    {
        return classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(IsEligibleField)
            .Select(f =>
            {
                ct.ThrowIfCancellationRequested();
                return CreateFieldInfo(f);
            })
            .ToImmutableArray();
    }

    /// <summary>
    /// Determines if a field is eligible for property generation.
    /// </summary>
    private static bool IsEligibleField(IFieldSymbol field)
    {
        // Only private fields
        if (field.DeclaredAccessibility != Accessibility.Private)
            return false;

        // Must start with underscore and have at least 2 characters
        if (!field.Name.StartsWith("_", StringComparison.Ordinal) || field.Name.Length < 2)
            return false;

        // Check for [NotifyIgnore]
        if (HasAttribute(field, NotifyIgnoreAttributeName))
            return false;

        return true;
    }

    /// <summary>
    /// Creates a FieldInfo record from a field symbol.
    /// </summary>
    private static FieldInfo CreateFieldInfo(IFieldSymbol field)
    {
        var propertyName = GetPropertyName(field);
        var typeName = field.Type.ToDisplayString(TypeDisplayFormat);
        var isNullable = IsNullableType(field.Type);
        var alsoNotify = GetAttributeValues(field, NotifyAlsoAttributeName);
        var commandsToNotify = GetAttributeValues(field, NotifyCanExecuteChangedForAttributeName);
        var setterAccess = GetSetterAccessLevel(field);
        var isPrimitiveType = IsPrimitiveValueType(field.Type);

        return new FieldInfo(
            field.Name,
            propertyName,
            typeName,
            isNullable,
            alsoNotify,
            commandsToNotify,
            setterAccess,
            isPrimitiveType);
    }

    /// <summary>
    /// Checks if a field has a specific attribute.
    /// </summary>
    private static bool HasAttribute(IFieldSymbol field, string attributeName)
    {
        return field.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeName);
    }

    /// <summary>
    /// Gets the property name from [NotifyName] or derives it from the field name.
    /// </summary>
    private static string GetPropertyName(IFieldSymbol field)
    {
        // Get property name from [NotifyName] or derive from field name (_name -> Name)
        var notifyNameAttr = field.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyNameAttributeName);

        if (notifyNameAttr?.ConstructorArguments.FirstOrDefault().Value is string customName)
            return customName;

        return char.ToUpperInvariant(field.Name[1]) + field.Name.Substring(2);
    }

    /// <summary>
    /// Checks if a type is nullable.
    /// </summary>
    private static bool IsNullableType(ITypeSymbol type)
    {
        return type.NullableAnnotation == NullableAnnotation.Annotated
            || (type is INamedTypeSymbol namedType
                && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);
    }

    /// <summary>
    /// Extracts string values from multiple instances of an attribute.
    /// </summary>
    private static ImmutableArray<string> GetAttributeValues(IFieldSymbol field, string attributeName)
    {
        return field.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == attributeName)
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToImmutableArray();
    }

    /// <summary>
    /// Gets the setter access level from [NotifySetter] attribute.
    /// </summary>
    private static string? GetSetterAccessLevel(IFieldSymbol field)
    {
        var setterAttr = field.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifySetterAttributeName);

        if (setterAttr == null || setterAttr.ConstructorArguments.Length == 0)
            return null;

        var accessLevel = (int)setterAttr.ConstructorArguments[0].Value!;
        return accessLevel switch
        {
            0 => null, // Public - same as property, no modifier needed
            1 => "protected",
            2 => "internal",
            3 => "private",
            4 => "protected internal",
            5 => "private protected",
            _ => null
        };
    }

    /// <summary>
    /// Determines if the type is a primitive value type that supports direct == comparison.
    /// </summary>
    private static bool IsPrimitiveValueType(ITypeSymbol type)
    {
        // Handle Nullable<T> - get the underlying type
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            type = namedType.TypeArguments[0];
        }

        return type.IsValueType && type.SpecialType switch
        {
            SpecialType.System_Boolean => true,
            SpecialType.System_Char => true,
            SpecialType.System_SByte => true,
            SpecialType.System_Byte => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Single => true,
            SpecialType.System_Double => true,
            SpecialType.System_Decimal => true,
            _ => false
        };
    }

    /// <summary>
    /// Generates the source code for a class.
    /// </summary>
    private static void GenerateSource(SourceProductionContext context, ClassInfo classInfo)
    {
        if (classInfo.Fields.Length == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (classInfo.IsSuppressable)
            sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();

        var hasNamespace = !string.IsNullOrEmpty(classInfo.Namespace);
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {classInfo.Namespace}");
            sb.AppendLine("{");
        }

        var indent = hasNamespace ? "    " : "";

        // Class declaration with interface(s)
        var interfaceList = new List<string>();
        if (!classInfo.AlreadyImplementsInpc)
            interfaceList.Add("INotifyPropertyChanged");
        if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
            interfaceList.Add("INotifyPropertyChanging");
        var interfaces = interfaceList.Count > 0 ? " : " + string.Join(", ", interfaceList) : "";
        sb.AppendLine($"{indent}{classInfo.Accessibility} partial class {classInfo.ClassName}{classInfo.TypeParameters}{interfaces}");
        sb.AppendLine($"{indent}{{");

        // PropertyChanged event (only if not already implemented)
        if (!classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine($"{indent}    public event PropertyChangedEventHandler? PropertyChanged;");
            sb.AppendLine();
        }

        // PropertyChanging event (only if ImplementChanging is true and not already implemented)
        if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
        {
            sb.AppendLine($"{indent}    public event PropertyChangingEventHandler? PropertyChanging;");
            sb.AppendLine();
        }

        // Suppression fields (only if IsSuppressable)
        if (classInfo.IsSuppressable)
        {
            // Generate static HashSet for AlwaysNotify properties
            if (classInfo.AlwaysNotifyProperties.Length > 0)
            {
                sb.AppendLine($"{indent}    private static readonly HashSet<string> _neverSuppressedProperties = new()");
                sb.AppendLine($"{indent}    {{");
                foreach (var prop in classInfo.AlwaysNotifyProperties)
                {
                    sb.AppendLine($"{indent}        \"{prop}\",");
                }
                sb.AppendLine($"{indent}    }};");
                sb.AppendLine();
            }

            sb.AppendLine($"{indent}    private int _notificationSuppressionCount;");
            sb.AppendLine($"{indent}    private HashSet<string>? _pendingNotifications;");
            sb.AppendLine();
        }

        // Generate properties
        foreach (var field in classInfo.Fields)
        {
            GenerateProperty(sb, field, indent, classInfo.ImplementChanging);
            sb.AppendLine();
        }

        // OnPropertyChanged method (only if not already implemented)
        if (!classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine($"{indent}    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)");
            sb.AppendLine($"{indent}    {{");
            if (classInfo.IsSuppressable)
            {
                if (classInfo.AlwaysNotifyProperties.Length > 0)
                {
                    // Check if property should never be suppressed
                    sb.AppendLine($"{indent}        if (_notificationSuppressionCount > 0 && !_neverSuppressedProperties.Contains(propertyName ?? \"\"))");
                }
                else
                {
                    sb.AppendLine($"{indent}        if (_notificationSuppressionCount > 0)");
                }
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            _pendingNotifications ??= new HashSet<string>();");
                sb.AppendLine($"{indent}            _pendingNotifications.Add(propertyName!);");
                sb.AppendLine($"{indent}            return;");
                sb.AppendLine($"{indent}        }}");
            }
            sb.AppendLine($"{indent}        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // OnPropertyChanging method (only if ImplementChanging is true and not already implemented)
        if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
        {
            sb.AppendLine($"{indent}    protected virtual void OnPropertyChanging([CallerMemberName] string? propertyName = null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // Generate partial hooks
        foreach (var field in classInfo.Fields)
        {
            sb.AppendLine($"{indent}    partial void On{field.PropertyName}Changing({field.TypeName} oldValue, {field.TypeName} newValue);");
            sb.AppendLine($"{indent}    partial void On{field.PropertyName}Changed();");
        }

        // Suppression methods (only if IsSuppressable)
        if (classInfo.IsSuppressable)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// Suppresses PropertyChanged notifications until the returned IDisposable is disposed.");
            sb.AppendLine($"{indent}    /// Supports nested suppression scopes.");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public IDisposable SuppressNotifications()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _notificationSuppressionCount++;");
            sb.AppendLine($"{indent}        return new NotificationSuppressor(this);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    private void ResumeNotifications()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        if (--_notificationSuppressionCount == 0 && _pendingNotifications != null)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            foreach (var propertyName in _pendingNotifications)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            _pendingNotifications.Clear();");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    private sealed class NotificationSuppressor : IDisposable");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        private readonly {classInfo.ClassName}{classInfo.TypeParameters} _owner;");
            sb.AppendLine($"{indent}        public NotificationSuppressor({classInfo.ClassName}{classInfo.TypeParameters} owner) => _owner = owner;");
            sb.AppendLine($"{indent}        public void Dispose() => _owner.ResumeNotifications();");
            sb.AppendLine($"{indent}    }}");
        }

        sb.AppendLine($"{indent}}}");

        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        var sourceText = SourceText.From(sb.ToString(), Encoding.UTF8);
        context.AddSource($"{classInfo.ClassName}.g.cs", sourceText);
    }

    /// <summary>
    /// Generates a single property.
    /// </summary>
    private static void GenerateProperty(StringBuilder sb, FieldInfo field, string indent, bool implementChanging)
    {
        sb.AppendLine($"{indent}    public {field.TypeName} {field.PropertyName}");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        get => {field.FieldName};");
        var setterModifier = field.SetterAccess != null ? $"{field.SetterAccess} " : "";
        sb.AppendLine($"{indent}        {setterModifier}set");
        sb.AppendLine($"{indent}        {{");
        // Use direct == for primitive types (faster), EqualityComparer for complex types
        if (field.IsPrimitiveType)
        {
            sb.AppendLine($"{indent}            if ({field.FieldName} == value) return;");
        }
        else
        {
            sb.AppendLine($"{indent}            if (EqualityComparer<{field.TypeName}>.Default.Equals({field.FieldName}, value)) return;");
        }
        // Fire PropertyChanging event if enabled
        if (implementChanging)
        {
            sb.AppendLine($"{indent}            OnPropertyChanging();");
        }
        sb.AppendLine($"{indent}            On{field.PropertyName}Changing({field.FieldName}, value);");
        sb.AppendLine($"{indent}            {field.FieldName} = value;");
        sb.AppendLine($"{indent}            OnPropertyChanged();");

        // NotifyAlso properties
        foreach (var alsoNotify in field.AlsoNotify)
        {
            sb.AppendLine($"{indent}            OnPropertyChanged(\"{alsoNotify}\");");
        }

        // NotifyCanExecuteChanged for commands (requires IRelayCommand or compatible type)
        foreach (var command in field.CommandsToNotify)
        {
            sb.AppendLine($"{indent}            {command}?.NotifyCanExecuteChanged();");
        }

        sb.AppendLine($"{indent}            On{field.PropertyName}Changed();");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
    }
}
