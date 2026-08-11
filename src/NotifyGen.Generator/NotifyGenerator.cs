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

/// <summary>
/// Incremental source generator that generates INotifyPropertyChanged implementation
/// for classes marked with [Notify].
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class NotifyGenerator : IIncrementalGenerator
{
    private const string NotifyAttributeName = "NotifyGen.NotifyAttribute";
    private const string NotifyAlsoAttributeName = "NotifyGen.NotifyAlsoAttribute";
    private const string NotifyNameAttributeName = "NotifyGen.NotifyNameAttribute";
    private const string NotifySetterAttributeName = "NotifyGen.NotifySetterAttribute";
    private const string NotifyCanExecuteChangedForAttributeName =
        "NotifyGen.NotifyCanExecuteChangedForAttribute";
    private const string NotifySuppressableAttributeName = "NotifyGen.NotifySuppressableAttribute";
    private const string AttributeUsageAttributeName = "System.AttributeUsageAttribute";

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

    private static readonly SymbolDisplayFormat FullyQualifiedTypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
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
}
