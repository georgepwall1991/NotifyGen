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

        // Check if already implements INotifyPropertyChanged
        var inpcInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanged");
        var alreadyImplementsInpc = inpcInterface != null
            && classSymbol.AllInterfaces.Contains(inpcInterface, SymbolEqualityComparer.Default);

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
            fields);
    }

    /// <summary>
    /// Extracts field information from the class.
    /// </summary>
    private static ImmutableArray<FieldInfo> ExtractFields(INamedTypeSymbol classSymbol, CancellationToken ct)
    {
        var fields = new List<FieldInfo>();

        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            if (member is not IFieldSymbol fieldSymbol)
                continue;

            // Only private fields
            if (fieldSymbol.DeclaredAccessibility != Accessibility.Private)
                continue;

            // Must start with underscore
            if (!fieldSymbol.Name.StartsWith("_", StringComparison.Ordinal) || fieldSymbol.Name.Length < 2)
                continue;

            // Check for [NotifyIgnore]
            if (fieldSymbol.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == NotifyIgnoreAttributeName))
                continue;

            // Get property name from field name (_name -> Name)
            var propertyName = char.ToUpperInvariant(fieldSymbol.Name[1]) + fieldSymbol.Name.Substring(2);

            // Get type name with nullability (use keyword format: string instead of System.String)
            var typeFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                    | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
            var typeName = fieldSymbol.Type.ToDisplayString(typeFormat);

            // Check nullability
            var isNullable = fieldSymbol.Type.NullableAnnotation == NullableAnnotation.Annotated
                || (fieldSymbol.Type is INamedTypeSymbol namedType
                    && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            // Get [NotifyAlso] attributes
            var alsoNotify = fieldSymbol.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName)
                .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToImmutableArray();

            fields.Add(new FieldInfo(fieldSymbol.Name, propertyName, typeName, isNullable, alsoNotify));
        }

        return fields.ToImmutableArray();
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

        // Class declaration with interface
        var interfaces = classInfo.AlreadyImplementsInpc ? "" : " : INotifyPropertyChanged";
        sb.AppendLine($"{indent}{classInfo.Accessibility} partial class {classInfo.ClassName}{classInfo.TypeParameters}{interfaces}");
        sb.AppendLine($"{indent}{{");

        // PropertyChanged event (only if not already implemented)
        if (!classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine($"{indent}    public event PropertyChangedEventHandler? PropertyChanged;");
            sb.AppendLine();
        }

        // Generate properties
        foreach (var field in classInfo.Fields)
        {
            GenerateProperty(sb, field, indent);
            sb.AppendLine();
        }

        // OnPropertyChanged method (only if not already implemented)
        if (!classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine($"{indent}    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // Generate partial hooks
        foreach (var field in classInfo.Fields)
        {
            sb.AppendLine($"{indent}    partial void On{field.PropertyName}Changed();");
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
    private static void GenerateProperty(StringBuilder sb, FieldInfo field, string indent)
    {
        sb.AppendLine($"{indent}    public {field.TypeName} {field.PropertyName}");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        get => {field.FieldName};");
        sb.AppendLine($"{indent}        set");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            if (EqualityComparer<{field.TypeName}>.Default.Equals({field.FieldName}, value)) return;");
        sb.AppendLine($"{indent}            {field.FieldName} = value;");
        sb.AppendLine($"{indent}            OnPropertyChanged();");

        // NotifyAlso properties
        foreach (var alsoNotify in field.AlsoNotify)
        {
            sb.AppendLine($"{indent}            OnPropertyChanged(\"{alsoNotify}\");");
        }

        sb.AppendLine($"{indent}            On{field.PropertyName}Changed();");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
    }
}
