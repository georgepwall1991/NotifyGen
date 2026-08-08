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
    private const string NotifyAlsoAttributeName = "NotifyGen.NotifyAlsoAttribute";
    private const string NotifyNameAttributeName = "NotifyGen.NotifyNameAttribute";
    private const string NotifySetterAttributeName = "NotifyGen.NotifySetterAttribute";
    private const string NotifyCanExecuteChangedForAttributeName =
        "NotifyGen.NotifyCanExecuteChangedForAttribute";
    private const string NotifySuppressableAttributeName = "NotifyGen.NotifySuppressableAttribute";

    /// <summary>
    /// Cached SymbolDisplayFormat for type names to avoid repeated allocations.
    /// </summary>
    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations with [Notify] attribute
        var classDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, ct) => GetClassInfo(ctx, ct)
            )
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
    private static NotificationTypeInfo? GetClassInfo(
        GeneratorSyntaxContext context,
        CancellationToken ct
    )
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        if (
            semanticModel.GetDeclaredSymbol(classDeclaration, ct)
            is not INamedTypeSymbol classSymbol
        )
            return null;

        var notifyAttribute = classSymbol
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyAttributeName);
        if (notifyAttribute == null)
            return null;

        if (
            notifyAttribute.ApplicationSyntaxReference?.GetSyntax(ct)
                is { } attributeSyntax
            && (
                attributeSyntax.SyntaxTree != classDeclaration.SyntaxTree
                || !attributeSyntax
                    .AncestorsAndSelf()
                    .OfType<ClassDeclarationSyntax>()
                    .Any(declaration => declaration.Span == classDeclaration.Span)
            )
        )
        {
            return null;
        }

        if (
            !TypeDeclarationInfoFactory.TryCreateChain(
                semanticModel,
                classDeclaration,
                ct,
                out var typeDeclarations
            )
        )
            return null;

        var implementChanging =
            notifyAttribute
                .NamedArguments.FirstOrDefault(a => a.Key == "ImplementChanging")
                .Value.Value
            is true;

        var suppressableAttribute = classSymbol
            .GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == NotifySuppressableAttributeName
            );
        var isSuppressable = suppressableAttribute != null;
        var alwaysNotifyProperties = ImmutableArray<string>.Empty;
        if (suppressableAttribute != null)
        {
            var alwaysNotifyArg = suppressableAttribute.NamedArguments.FirstOrDefault(a =>
                a.Key == "AlwaysNotify"
            );
            if (alwaysNotifyArg.Value.Kind == TypedConstantKind.Array)
            {
                alwaysNotifyProperties = alwaysNotifyArg
                    .Value.Values.Where(v => v.Value is string)
                    .Select(v => (string)v.Value!)
                    .ToImmutableArray();
            }
        }

        var inpcInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanged"
        );
        var alreadyImplementsInpc =
            inpcInterface != null
            && classSymbol.AllInterfaces.Contains(inpcInterface, SymbolEqualityComparer.Default);

        var inpcChangingInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanging"
        );
        var alreadyImplementsInpcChanging =
            inpcChangingInterface != null
            && classSymbol.AllInterfaces.Contains(
                inpcChangingInterface,
                SymbolEqualityComparer.Default
            );

        var containingNamespace = classSymbol.ContainingNamespace;
        var namespaceName = containingNamespace.IsGlobalNamespace
            ? string.Empty
            : containingNamespace.ToDisplayString();

        return new NotificationTypeInfo(
            namespaceName,
            typeDeclarations,
            alreadyImplementsInpc,
            alreadyImplementsInpcChanging,
            implementChanging,
            isSuppressable,
            alwaysNotifyProperties,
            ExtractFields(classSymbol, ct)
        );
    }

    /// <summary>
    /// Extracts field and incomplete partial-property information from the class.
    /// </summary>
    private static ImmutableArray<FieldInfo> ExtractFields(
        INamedTypeSymbol classSymbol,
        CancellationToken ct
    )
    {
        var members = ImmutableArray.CreateBuilder<FieldInfo>();
        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (
                member is IFieldSymbol field
                && FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible
            )
            {
                members.Add(CreateFieldInfo(field));
            }
            else if (member is IPropertySymbol property && IsIncompletePartialProperty(property, ct))
            {
                members.Add(CreatePartialPropertyInfo(property));
            }
        }

        var directMembers = members.ToImmutable();
        if (
            directMembers
                .GroupBy(static member => member.PropertyName)
                .Any(static group => group.Skip(1).Any())
            || classSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => !IsIncompletePartialProperty(property, ct))
                .Select(static property => property.Name)
                .Intersect(
                    directMembers.Select(static member => member.PropertyName),
                    StringComparer.Ordinal
                )
                .Any()
        )
        {
            return ImmutableArray<FieldInfo>.Empty;
        }

        return directMembers;
    }

    /// <summary>
    /// Creates metadata from an eligible field symbol.
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
        var requiresUnsafe = RequiresUnsafeContext(field.Type);

        return new FieldInfo(
            field.Name,
            propertyName,
            typeName,
            isNullable,
            alsoNotify,
            commandsToNotify,
            setterAccess,
            isPrimitiveType,
            requiresUnsafe
        );
    }

    /// <summary>
    /// Creates metadata from an incomplete C# 14 partial property definition.
    /// </summary>
    private static FieldInfo CreatePartialPropertyInfo(IPropertySymbol property)
    {
        var typeName = property.Type.ToDisplayString(TypeDisplayFormat);
        return new FieldInfo(
            "field",
            property.Name,
            typeName,
            IsNullableType(property.Type),
            GetAttributeValues(property, NotifyAlsoAttributeName),
            GetAttributeValues(property, NotifyCanExecuteChangedForAttributeName),
            GetAccessorAccessLevel(property.SetMethod, property.DeclaredAccessibility),
            isPrimitiveType: IsPrimitiveValueType(property.Type),
            requiresUnsafe: RequiresUnsafeContext(property.Type),
            isPartialProperty: true,
            propertyAccessibility: GetAccessibilityText(property.DeclaredAccessibility),
            needsNullableBackingField: IsNonNullableReferenceType(property.Type),
            getterAccess: GetAccessorAccessLevel(property.GetMethod, property.DeclaredAccessibility)
        );
    }

    private static bool IsIncompletePartialProperty(
        IPropertySymbol property,
        CancellationToken ct
    ) => PartialPropertyEligibility.IsSupported(property, ct);

    /// <summary>
    /// Gets the property name from [NotifyName] or derives it from the field name.
    /// </summary>
    private static string GetPropertyName(IFieldSymbol field)
    {
        // Get property name from [NotifyName] or derive from field name (_name -> Name)
        var notifyNameAttr = field
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyNameAttributeName);

        if (notifyNameAttr?.ConstructorArguments.FirstOrDefault().Value is string customName)
            return customName;

        return char.ToUpperInvariant(field.Name[1]) + field.Name.Substring(2);
    }

    private static bool IsNonNullableReferenceType(ITypeSymbol type) =>
        type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.Annotated;

    /// <summary>
    /// Checks if a type is nullable.
    /// </summary>
    private static bool IsNullableType(ITypeSymbol type)
    {
        return type.NullableAnnotation == NullableAnnotation.Annotated
            || (
                type is INamedTypeSymbol namedType
                && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            );
    }

    /// <summary>
    /// Extracts string values from multiple instances of an attribute.
    /// </summary>
    private static ImmutableArray<string> GetAttributeValues(
        ISymbol member,
        string attributeName
    )
    {
        return member
            .GetAttributes()
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
        var setterAttr = field
            .GetAttributes()
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
            _ => null,
        };
    }

    private static string? GetAccessorAccessLevel(
        IMethodSymbol? accessor,
        Accessibility propertyAccessibility
    )
    {
        if (accessor == null || accessor.DeclaredAccessibility == propertyAccessibility)
            return null;

        return GetAccessibilityText(accessor.DeclaredAccessibility);
    }

    private static string GetAccessibilityText(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "public",
        };

    /// <summary>
    /// Determines whether emitting the type requires an unsafe declaration context.
    /// </summary>
    private static bool RequiresUnsafeContext(ITypeSymbol type)
    {
        return type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer
            || (type is IArrayTypeSymbol arrayType && RequiresUnsafeContext(arrayType.ElementType));
    }

    /// <summary>
    /// Determines if the type is a primitive value type that supports direct == comparison.
    /// </summary>
    private static bool IsPrimitiveValueType(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
            return true;

        // Handle Nullable<T> - get the underlying type
        if (
            type is INamedTypeSymbol namedType
            && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        )
        {
            type = namedType.TypeArguments[0];
        }

        return type.IsValueType
            && type.SpecialType switch
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
                _ => false,
            };
    }

    /// <summary>
    /// Generates the source code for a class.
    /// </summary>
    private static void GenerateSource(
        SourceProductionContext context,
        NotificationTypeInfo classInfo
    )
    {
        if (!classInfo.CanGenerate)
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
        for (var index = 0; index < classInfo.TypeDeclarations.Length; index++)
        {
            var declaration = classInfo.TypeDeclarations[index];
            var isTarget = index == classInfo.TypeDeclarations.Length - 1;
            var requiredModifiers =
                declaration.RequiredModifiers.Length == 0
                    ? string.Empty
                    : string.Join(" ", declaration.RequiredModifiers) + " ";
            if (
                isTarget
                && classInfo.Fields.Any(static field => field.RequiresUnsafe)
                && !declaration.RequiredModifiers.Contains("unsafe")
            )
            {
                requiredModifiers += "unsafe ";
            }

            var interfaces = string.Empty;
            if (isTarget)
            {
                var interfaceList = new List<string>();
                if (!classInfo.AlreadyImplementsInpc)
                    interfaceList.Add("INotifyPropertyChanged");
                if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
                    interfaceList.Add("INotifyPropertyChanging");
                interfaces =
                    interfaceList.Count == 0
                        ? string.Empty
                        : " : " + string.Join(", ", interfaceList);
            }

            sb.AppendLine(
                $"{indent}{declaration.Accessibility} {requiredModifiers}partial {declaration.Keyword} {declaration.Name}{declaration.TypeParameterList}{interfaces}"
            );
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        // PropertyChanged event (only if not already implemented)
        if (!classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine(
                $"{indent}    public event PropertyChangedEventHandler? PropertyChanged;"
            );
            sb.AppendLine();
        }

        // PropertyChanging event (only if ImplementChanging is true and not already implemented)
        if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
        {
            sb.AppendLine(
                $"{indent}    public event PropertyChangingEventHandler? PropertyChanging;"
            );
            sb.AppendLine();
        }

        // Suppression fields (only if IsSuppressable)
        if (classInfo.IsSuppressable)
        {
            // Generate static HashSet for AlwaysNotify properties
            if (classInfo.AlwaysNotifyProperties.Length > 0)
            {
                sb.AppendLine(
                    $"{indent}    private static readonly HashSet<string> _neverSuppressedProperties = new()"
                );
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
            sb.AppendLine(
                $"{indent}    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)"
            );
            sb.AppendLine($"{indent}    {{");
            if (classInfo.IsSuppressable)
            {
                if (classInfo.AlwaysNotifyProperties.Length > 0)
                {
                    // Check if property should never be suppressed
                    sb.AppendLine(
                        $"{indent}        if (_notificationSuppressionCount > 0 && !_neverSuppressedProperties.Contains(propertyName ?? \"\"))"
                    );
                }
                else
                {
                    sb.AppendLine($"{indent}        if (_notificationSuppressionCount > 0)");
                }
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine(
                    $"{indent}            _pendingNotifications ??= new HashSet<string>();"
                );
                sb.AppendLine($"{indent}            _pendingNotifications.Add(propertyName!);");
                sb.AppendLine($"{indent}            return;");
                sb.AppendLine($"{indent}        }}");
            }
            sb.AppendLine(
                $"{indent}        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));"
            );
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // OnPropertyChanging method (only if ImplementChanging is true and not already implemented)
        if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
        {
            sb.AppendLine(
                $"{indent}    protected virtual void OnPropertyChanging([CallerMemberName] string? propertyName = null)"
            );
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine(
                $"{indent}        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));"
            );
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // Generate partial hooks
        foreach (var field in classInfo.Fields)
        {
            sb.AppendLine(
                $"{indent}    partial void On{field.PropertyName}Changing({field.TypeName} oldValue, {field.TypeName} newValue);"
            );
            sb.AppendLine($"{indent}    partial void On{field.PropertyName}Changed();");
        }

        // Suppression methods (only if IsSuppressable)
        if (classInfo.IsSuppressable)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine(
                $"{indent}    /// Suppresses PropertyChanged notifications until the returned IDisposable is disposed."
            );
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
            sb.AppendLine(
                $"{indent}        if (--_notificationSuppressionCount == 0 && _pendingNotifications != null)"
            );
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine(
                $"{indent}            foreach (var propertyName in _pendingNotifications)"
            );
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine(
                $"{indent}                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));"
            );
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            _pendingNotifications.Clear();");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    private sealed class NotificationSuppressor : IDisposable");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine(
                $"{indent}        private readonly {classInfo.TargetType.Name}{classInfo.TargetType.TypeParameterList} _owner;"
            );
            sb.AppendLine(
                $"{indent}        public NotificationSuppressor({classInfo.TargetType.Name}{classInfo.TargetType.TypeParameterList} owner) => _owner = owner;"
            );
            sb.AppendLine(
                $"{indent}        public void Dispose() => _owner.ResumeNotifications();"
            );
            sb.AppendLine($"{indent}    }}");
        }

        for (var index = classInfo.TypeDeclarations.Length - 1; index >= 0; index--)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.AppendLine($"{indent}}}");
        }

        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        var sourceText = SourceText.From(sb.ToString(), Encoding.UTF8);
        context.AddSource(
            SourceHintName.Create(classInfo.TargetType.MetadataIdentity, classInfo.TargetType.Name),
            sourceText
        );
    }

    /// <summary>
    /// Generates a single property.
    /// </summary>
    private static void GenerateProperty(
        StringBuilder sb,
        FieldInfo field,
        string indent,
        bool implementChanging
    )
    {
        if (field.IsPartialProperty && field.NeedsNullableBackingField)
        {
            sb.AppendLine(
                $"{indent}    [field: global::System.Diagnostics.CodeAnalysis.MaybeNull, global::System.Diagnostics.CodeAnalysis.AllowNull]"
            );
        }

        var partialModifier = field.IsPartialProperty ? " partial" : string.Empty;
        var backingValue = field.NeedsNullableBackingField
            ? $"{field.FieldName}!"
            : field.FieldName;
        sb.AppendLine(
            $"{indent}    {field.PropertyAccessibility}{partialModifier} {field.TypeName} {field.PropertyName}"
        );
        sb.AppendLine($"{indent}    {{");
        var getterModifier = field.GetterAccess != null ? $"{field.GetterAccess} " : "";
        sb.AppendLine($"{indent}        {getterModifier}get => {backingValue};");
        var setterModifier = field.SetterAccess != null ? $"{field.SetterAccess} " : "";
        sb.AppendLine($"{indent}        {setterModifier}set");
        sb.AppendLine($"{indent}        {{");
        // Pointer-like types use native-int equality to avoid function-pointer comparison warnings.
        if (field.IsPrimitiveType && field.RequiresUnsafe)
        {
            sb.AppendLine(
                $"{indent}            if ((nint){field.FieldName} == (nint)value) return;"
            );
        }
        else if (field.IsPrimitiveType)
        {
            sb.AppendLine($"{indent}            if ({backingValue} == value) return;");
        }
        else
        {
            sb.AppendLine(
                $"{indent}            if (EqualityComparer<{field.TypeName}>.Default.Equals({backingValue}, value)) return;"
            );
        }
        // Fire PropertyChanging event if enabled
        if (implementChanging)
        {
            sb.AppendLine($"{indent}            OnPropertyChanging();");
        }
        sb.AppendLine(
            $"{indent}            On{field.PropertyName}Changing({backingValue}, value);"
        );
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
