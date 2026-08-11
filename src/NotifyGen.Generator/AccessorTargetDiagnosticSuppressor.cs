using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NotifyGen.Generator;

/// <summary>
/// Suppresses CS0657/CS0658 for explicit property/get/set attribute targets on [Notify] fields.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AccessorTargetDiagnosticSuppressor : DiagnosticSuppressor
{
    private const string NotifyAttributeMetadataName = "NotifyGen.NotifyAttribute";

    public static readonly SuppressionDescriptor PropertyTargetOnNotifyField = new(
        id: "NOTIFYSPR0001",
        suppressedDiagnosticId: "CS0657",
        justification: "NotifyGen forwards [property:] attributes from fields onto generated properties."
    );

    public static readonly SuppressionDescriptor AccessorTargetOnNotifyField = new(
        id: "NOTIFYSPR0002",
        suppressedDiagnosticId: "CS0658",
        justification: "NotifyGen forwards [get:]/[set:] attributes from fields onto generated accessors."
    );

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(PropertyTargetOnNotifyField, AccessorTargetOnNotifyField);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var notifyAttribute = context.Compilation.GetTypeByMetadataName(
            NotifyAttributeMetadataName
        );
        if (notifyAttribute is null)
            return;

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is null)
                continue;

            var root = diagnostic.Location.SourceTree.GetRoot(context.CancellationToken);
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (
                node
                is not AttributeTargetSpecifierSyntax
                {
                    Parent.Parent: FieldDeclarationSyntax
                    {
                        Declaration.Variables.Count: > 0
                    } fieldDeclaration
                } target
            )
            {
                continue;
            }

            if (
                !target.Identifier.IsKind(SyntaxKind.PropertyKeyword)
                && !target.Identifier.IsKind(SyntaxKind.GetKeyword)
                && !target.Identifier.IsKind(SyntaxKind.SetKeyword)
            )
            {
                continue;
            }

            var semanticModel = context.GetSemanticModel(diagnostic.Location.SourceTree);
            var fieldSymbol = semanticModel.GetDeclaredSymbol(
                fieldDeclaration.Declaration.Variables[0],
                context.CancellationToken
            ) as IFieldSymbol;
            if (fieldSymbol?.ContainingType is null)
                continue;

            var hasNotify = fieldSymbol
                .ContainingType.GetAttributes()
                .Any(attribute =>
                    SymbolEqualityComparer.Default.Equals(
                        attribute.AttributeClass,
                        notifyAttribute
                    )
                );
            if (!hasNotify)
                continue;

            if (target.Identifier.IsKind(SyntaxKind.PropertyKeyword))
                context.ReportSuppression(Suppression.Create(PropertyTargetOnNotifyField, diagnostic));
            else
                context.ReportSuppression(Suppression.Create(AccessorTargetOnNotifyField, diagnostic));
        }
    }
}
