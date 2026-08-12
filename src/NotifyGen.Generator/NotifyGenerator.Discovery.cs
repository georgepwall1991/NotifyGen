using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace NotifyGen.Generator;

public sealed partial class NotifyGenerator
{
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
            notifyAttribute.ApplicationSyntaxReference?.GetSyntax(ct) is { } attributeSyntax
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

        var propertyChangedInvoker = alreadyImplementsInpc
            ? PropertyChangedInvoker.Find(classSymbol)
            : PropertyChangedInvokerKind.Generated;

        var inpcChangingInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanging"
        );
        var alreadyImplementsInpcChanging =
            inpcChangingInterface != null
            && classSymbol.AllInterfaces.Contains(
                inpcChangingInterface,
                SymbolEqualityComparer.Default
            );
        var propertyChangingInvoker = alreadyImplementsInpcChanging
            ? PropertyChangingInvoker.Find(classSymbol)
            : PropertyChangingInvokerKind.Generated;

        var containingNamespace = classSymbol.ContainingNamespace;
        var namespaceName = containingNamespace.IsGlobalNamespace
            ? string.Empty
            : containingNamespace.ToDisplayString();

        return new NotificationTypeInfo(
            namespaceName,
            typeDeclarations,
            alreadyImplementsInpc,
            propertyChangedInvoker,
            alreadyImplementsInpcChanging,
            propertyChangingInvoker,
            implementChanging,
            isSuppressable,
            alwaysNotifyProperties,
            ExtractFields(classSymbol, semanticModel, ct),
            classSymbol.GetMembers().Select(static member => member.Name).ToImmutableArray()
        );
    }

    /// <summary>
    /// Extracts field and incomplete partial-property information from the class.
    /// </summary>
    private static ImmutableArray<FieldInfo> ExtractFields(
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var compilation = semanticModel.Compilation;
        var optIn = NotifyMemberSelection.TypeUsesOptIn(classSymbol, ct);
        var members = ImmutableArray.CreateBuilder<FieldInfo>();
        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (
                member is IFieldSymbol field
                && NotifyMemberSelection.ShouldGenerateField(field, optIn)
            )
            {
                if (
                    GeneratedPropertyNameValidation.IsValid(GetPropertyName(field))
                    && !IsFileLocalType(field.Type, ct)
                )
                    members.Add(CreateFieldInfo(field, compilation, ct));
            }
            else if (
                member is IPropertySymbol property
                && NotifyMemberSelection.ShouldGeneratePartial(property, optIn, ct)
                && !IsFileLocalType(property.Type, ct)
            )
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
                .Where(property =>
                    !NotifyMemberSelection.ShouldGeneratePartial(property, optIn, ct)
                )
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

        var directTargets = directMembers.ToDictionary(
            static member => member.PropertyName,
            static member => member.AlsoNotify.ToBuilder(),
            StringComparer.Ordinal
        );
        var targetPropertyNames = new HashSet<string>(
            classSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Select(static property => property.Name),
            StringComparer.Ordinal
        );
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (
                NotifyMemberSelection.ShouldGenerateField(field, optIn)
                && GeneratedPropertyNameValidation.IsValid(GetPropertyName(field))
            )
                targetPropertyNames.Add(GetPropertyName(field));
        }

        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            var targetName = member switch
            {
                IFieldSymbol field
                    when NotifyMemberSelection.ShouldGenerateField(field, optIn)
                        && GeneratedPropertyNameValidation.IsValid(GetPropertyName(field)) =>
                    GetPropertyName(field),
                IPropertySymbol property => property.Name,
                _ => null,
            };
            if (targetName == null || !targetPropertyNames.Contains(targetName))
                continue;

            foreach (var attribute in GetNotifyAlsoAttributes(member))
            {
                if (!RequestsNotifyFrom(attribute))
                    continue;

                var sourceName = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                if (
                    !string.IsNullOrEmpty(sourceName)
                    && directTargets.TryGetValue(sourceName!, out var targets)
                )
                {
                    targets.Add(targetName);
                }
            }
        }

        var computedPhantoms = MergeComputedDependencies(
            classSymbol,
            semanticModel,
            directTargets,
            optIn,
            ct
        );

        var mergedMembers = directMembers.ToDictionary(
            static member => member.PropertyName,
            member => member.WithAlsoNotify(directTargets[member.PropertyName].ToImmutable()),
            StringComparer.Ordinal
        );
        foreach (var phantom in computedPhantoms)
            mergedMembers[phantom.PropertyName] = phantom;

        var result = ImmutableArray.CreateBuilder<FieldInfo>(directMembers.Length);
        foreach (var member in directMembers)
        {
            ct.ThrowIfCancellationRequested();
            result.Add(
                member.WithAlsoNotify(
                    ExpandAlsoNotify(
                        member.PropertyName,
                        mergedMembers[member.PropertyName].AlsoNotify,
                        mergedMembers
                    )
                )
            );
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<FieldInfo> MergeComputedDependencies(
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        Dictionary<string, ImmutableArray<string>.Builder> directTargets,
        bool optIn,
        CancellationToken ct
    )
    {
        var fieldToProperty = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (
                !NotifyMemberSelection.ShouldGenerateField(field, optIn)
                || !GeneratedPropertyNameValidation.IsValid(GetPropertyName(field))
            )
            {
                continue;
            }

            fieldToProperty[field.Name] = GetPropertyName(field);
        }

        var computedAdjacency = new Dictionary<string, ImmutableArray<string>.Builder>(
            StringComparer.Ordinal
        );
        var computedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (ComputedDependencyWalker.HasAttribute(property))
                computedNames.Add(property.Name);
        }

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (!computedNames.Contains(property.Name))
                continue;

            foreach (var attribute in GetNotifyAlsoAttributes(property))
            {
                if (RequestsNotifyFrom(attribute))
                    continue;

                if (
                    RequestsSubPropertyNotification(attribute)
                    || RequestsCollectionNotification(attribute)
                )
                {
                    continue;
                }

                if (
                    attribute.ConstructorArguments.FirstOrDefault().Value is not string targetName
                    || string.IsNullOrEmpty(targetName)
                    || !IsKnownAlsoNotifyTarget(
                        classSymbol,
                        directTargets,
                        computedNames,
                        targetName
                    )
                )
                {
                    continue;
                }

                if (!computedAdjacency.TryGetValue(property.Name, out var sourceSideTargets))
                {
                    sourceSideTargets = ImmutableArray.CreateBuilder<string>();
                    computedAdjacency[property.Name] = sourceSideTargets;
                }

                sourceSideTargets.Add(targetName);
            }
        }

        foreach (var member in classSymbol.GetMembers())
        {
            foreach (var attribute in GetNotifyAlsoAttributes(member))
            {
                if (!RequestsNotifyFrom(attribute))
                    continue;

                if (
                    attribute.ConstructorArguments.FirstOrDefault().Value is not string sourceName
                    || !computedNames.Contains(sourceName)
                )
                {
                    continue;
                }

                var targetName = member switch
                {
                    IFieldSymbol field
                        when NotifyMemberSelection.ShouldGenerateField(field, optIn) =>
                        GetPropertyName(field),
                    IPropertySymbol property => property.Name,
                    _ => null,
                };
                if (string.IsNullOrEmpty(targetName))
                    continue;

                if (!computedAdjacency.TryGetValue(sourceName, out var notifyFromTargets))
                {
                    notifyFromTargets = ImmutableArray.CreateBuilder<string>();
                    computedAdjacency[sourceName] = notifyFromTargets;
                }

                notifyFromTargets.Add(targetName!);
            }
        }

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            ct.ThrowIfCancellationRequested();
            if (!ComputedDependencyWalker.HasAttribute(property))
                continue;

            var walk = ComputedDependencyWalker.Analyze(
                property,
                semanticModel,
                classSymbol,
                fieldToProperty,
                candidate => NotifyMemberSelection.ShouldGeneratePartial(candidate, optIn, ct),
                ct
            );
            if (
                walk.Status
                is ComputedWalkStatus.OnGeneratedMember
                    or ComputedWalkStatus.Unsupported
                    or ComputedWalkStatus.Empty
                    or ComputedWalkStatus.WritableTarget
            )
            {
                continue;
            }

            foreach (var sourceName in walk.Dependencies)
            {
                if (directTargets.TryGetValue(sourceName, out var generatedTargets))
                {
                    generatedTargets.Add(property.Name);
                    continue;
                }

                if (!computedNames.Contains(sourceName))
                    continue;

                if (!computedAdjacency.TryGetValue(sourceName, out var computedTargets))
                {
                    computedTargets = ImmutableArray.CreateBuilder<string>();
                    computedAdjacency[sourceName] = computedTargets;
                }

                computedTargets.Add(property.Name);
            }
        }

        if (computedAdjacency.Count == 0)
            return ImmutableArray<FieldInfo>.Empty;

        var phantoms = ImmutableArray.CreateBuilder<FieldInfo>(computedAdjacency.Count);
        foreach (var pair in computedAdjacency)
        {
            if (directTargets.ContainsKey(pair.Key))
                continue;

            phantoms.Add(CreateComputedTargetInfo(pair.Key, pair.Value.ToImmutable()));
        }

        return phantoms.ToImmutable();
    }

    private static bool IsKnownAlsoNotifyTarget(
        INamedTypeSymbol classSymbol,
        Dictionary<string, ImmutableArray<string>.Builder> generatedSources,
        HashSet<string> computedNames,
        string targetName
    )
    {
        if (generatedSources.ContainsKey(targetName) || computedNames.Contains(targetName))
            return true;

        return classSymbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Any(property => property.Name == targetName);
    }

    private static FieldInfo CreateComputedTargetInfo(
        string propertyName,
        ImmutableArray<string> alsoNotify
    ) =>
        new(
            fieldName: string.Empty,
            propertyName: propertyName,
            typeName: "object",
            isNullable: false,
            alsoNotify: alsoNotify,
            commandsToNotify: ImmutableArray<string>.Empty,
            isComputedTarget: true
        );

    private static ImmutableArray<string> ExpandAlsoNotify(
        string sourcePropertyName,
        ImmutableArray<string> directTargets,
        IReadOnlyDictionary<string, FieldInfo> membersByPropertyName
    )
    {
        var expanded = ImmutableArray.CreateBuilder<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { sourcePropertyName };

        // Preserve the existing direct-edge order before walking newly discovered edges.
        foreach (var target in directTargets)
        {
            if (visited.Add(target))
                expanded.Add(target);
        }

        foreach (var target in directTargets)
        {
            AppendTransitiveAlsoNotify(target, expanded, visited, membersByPropertyName);
        }

        return expanded.ToImmutable();
    }

    private static void AppendTransitiveAlsoNotify(
        string source,
        ImmutableArray<string>.Builder expanded,
        HashSet<string> visited,
        IReadOnlyDictionary<string, FieldInfo> membersByPropertyName
    )
    {
        if (!membersByPropertyName.TryGetValue(source, out var sourceMember))
            return;

        foreach (var target in sourceMember.AlsoNotify)
        {
            if (visited.Add(target))
            {
                expanded.Add(target);
                AppendTransitiveAlsoNotify(target, expanded, visited, membersByPropertyName);
            }
        }
    }

    /// <summary>
    /// Creates metadata from an eligible field symbol.
    /// </summary>
    private static FieldInfo CreateFieldInfo(
        IFieldSymbol field,
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var propertyName = GetPropertyName(field);
        var typeName = field.Type.ToDisplayString(TypeDisplayFormat);
        var isNullable = IsNullableType(field.Type);
        var alsoNotify = GetSourceNotifyAlsoValues(field);
        var commandsToNotify = GetAttributeValues(field, NotifyCanExecuteChangedForAttributeName);
        var setterAccess = GetSetterAccessLevel(field);
        var isPrimitiveType = IsPrimitiveValueType(field.Type);
        var requiresUnsafe = RequiresUnsafeContext(field.Type);
        var typedHook = FindNonPartialTypedChangedHook(
            field.ContainingType,
            propertyName,
            field.Type
        );
        CollectForwardedAttributes(
            field,
            compilation,
            cancellationToken,
            out var propertyAttributes,
            out var getterAttributes,
            out var setterAttributes
        );

        return new FieldInfo(
            field.Name,
            propertyName,
            typeName,
            isNullable,
            alsoNotify,
            commandsToNotify,
            setterAccess,
            isPrimitiveType,
            requiresUnsafe,
            propertyAttributes: propertyAttributes,
            getterAttributes: getterAttributes,
            setterAttributes: setterAttributes,
            subPropertyNotify: GetSubPropertyNotifyTargets(field),
            collectionNotify: GetCollectionNotifyTargets(field),
            hasNonPartialTypedChangedHook: typedHook is not null,
            existingTypedChangedHookParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                field.Type,
                0
            ),
            existingTypedChangedHookNewParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                field.Type,
                1
            )
        );
    }

    /// <summary>
    /// Creates metadata from an incomplete C# 14 partial property definition.
    /// </summary>
    private static FieldInfo CreatePartialPropertyInfo(IPropertySymbol property)
    {
        var typeName = property.Type.ToDisplayString(TypeDisplayFormat);
        var typedHook = FindNonPartialTypedChangedHook(
            property.ContainingType,
            property.Name,
            property.Type
        );
        return new FieldInfo(
            "field",
            property.Name,
            typeName,
            IsNullableType(property.Type),
            GetSourceNotifyAlsoValues(property),
            GetAttributeValues(property, NotifyCanExecuteChangedForAttributeName),
            GetAccessorAccessLevel(property.SetMethod, property.DeclaredAccessibility),
            isPrimitiveType: IsPrimitiveValueType(property.Type),
            requiresUnsafe: RequiresUnsafeContext(property.Type),
            isPartialProperty: true,
            propertyAccessibility: GetAccessibilityText(property.DeclaredAccessibility),
            needsNullableBackingField: IsNonNullableReferenceType(property.Type),
            getterAccess: GetAccessorAccessLevel(
                property.GetMethod,
                property.DeclaredAccessibility
            ),
            subPropertyNotify: GetSubPropertyNotifyTargets(property),
            collectionNotify: GetCollectionNotifyTargets(property),
            hasNonPartialTypedChangedHook: typedHook is not null,
            existingTypedChangedHookParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                property.Type,
                0
            ),
            existingTypedChangedHookNewParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                property.Type,
                1
            )
        );
    }

    private static bool IsIncompletePartialProperty(
        IPropertySymbol property,
        CancellationToken ct
    ) => PartialPropertyEligibility.IsSupported(property, ct);

    /// <summary>
    /// Gets the property name from [NotifyName] or derives it from the field name.
    /// </summary>
    private static string GetPropertyName(IFieldSymbol field) =>
        NotifyMemberSelection.GetGeneratedPropertyName(field);

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

    private static ImmutableArray<string> GetSubPropertyNotifyTargets(ISymbol member)
    {
        var memberType = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
        if (
            memberType is null
            || memberType is not ITypeParameterSymbol && !memberType.IsReferenceType
            || memberType is ITypeParameterSymbol parameter && !parameter.HasReferenceTypeConstraint
        )
        {
            return ImmutableArray<string>.Empty;
        }

        return member
            .GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName
            )
            .Where(attribute => !RequestsNotifyFrom(attribute))
            .Where(attribute =>
                attribute.NamedArguments.Any(named =>
                    named.Key == "NotifyOnSubPropertyChanged" && named.Value.Value is true
                )
            )
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(static value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> GetCollectionNotifyTargets(ISymbol member)
    {
        return GetNotifyAlsoAttributes(member)
            .Where(attribute => !RequestsNotifyFrom(attribute))
            .Where(attribute =>
                attribute.NamedArguments.Any(named =>
                    named.Key == "NotifyOnCollectionChanged" && named.Value.Value is true
                )
            )
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(static value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> GetSourceNotifyAlsoValues(ISymbol member)
    {
        return GetNotifyAlsoAttributes(member)
            .Where(attribute => !RequestsNotifyFrom(attribute))
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(static value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .ToImmutableArray();
    }

    private static IEnumerable<AttributeData> GetNotifyAlsoAttributes(ISymbol member) =>
        member
            .GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName
            );

    private static bool RequestsNotifyFrom(AttributeData attribute) =>
        attribute.NamedArguments.Any(named =>
            named.Key == "NotifyFrom" && named.Value.Value is true
        );

    private static bool RequestsSubPropertyNotification(AttributeData attribute) =>
        attribute.NamedArguments.Any(named =>
            named.Key == "NotifyOnSubPropertyChanged" && named.Value.Value is true
        );

    private static bool RequestsCollectionNotification(AttributeData attribute) =>
        attribute.NamedArguments.Any(named =>
            named.Key == "NotifyOnCollectionChanged" && named.Value.Value is true
        );

    /// <summary>
    /// Extracts string values from multiple instances of an attribute.
    /// </summary>
    private static ImmutableArray<string> GetAttributeValues(ISymbol member, string attributeName)
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
}
