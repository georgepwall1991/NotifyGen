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
    private static bool HasSubPropertySubscription(FieldInfo field) =>
        field.SubPropertyNotify.Length > 0 && !field.RequiresUnsafe;

    private static bool HasCollectionSubscription(FieldInfo field) =>
        field.CollectionNotify.Length > 0 && !field.RequiresUnsafe;

    private static string GetSubPropertyMemberPrefix(FieldInfo field)
    {
        var builder = new StringBuilder("__notifyGenSubProperty_");
        foreach (var character in field.PropertyName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in field.PropertyName + "|" + field.FieldName)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            builder.Append('_').Append(hash.ToString("X8"));
        }

        return builder.ToString();
    }

    private static string GetCollectionMemberPrefix(FieldInfo field)
    {
        var builder = new StringBuilder("__notifyGenCollection_");
        foreach (var character in field.PropertyName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in field.PropertyName + "|" + field.FieldName)
            {
                hash ^= character;
                hash *= 16777619;
            }

            builder.Append('_').Append(hash.ToString("X8"));
        }

        return builder.ToString();
    }

    private static void GenerateSubPropertyMembers(
        StringBuilder sb,
        FieldInfo field,
        string indent,
        PropertyChangedInvokerKind invoker,
        bool useSuppressionWrapper
    )
    {
        var prefix = GetSubPropertyMemberPrefix(field);
        sb.AppendLine(
            $"{indent}    private global::System.ComponentModel.INotifyPropertyChanged? {prefix}Source;"
        );
        sb.AppendLine($"{indent}    private bool {prefix}Initialized;");
        sb.AppendLine($"{indent}    private void {prefix}Changed(");
        sb.AppendLine(
            $"{indent}        object? sender, global::System.ComponentModel.PropertyChangedEventArgs e"
        );
        sb.AppendLine($"{indent}    )");
        sb.AppendLine($"{indent}    {{");
        foreach (var propertyName in field.SubPropertyNotify)
        {
            AppendPropertyChangedCall(
                sb,
                indent + "        ",
                propertyName,
                invoker,
                useSuppressionWrapper: useSuppressionWrapper
            );
        }
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Ensure(object? currentValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        if ({prefix}Initialized)");
        sb.AppendLine($"{indent}            return;");
        sb.AppendLine();
        sb.AppendLine($"{indent}        {prefix}Initialized = true;");
        sb.AppendLine(
            $"{indent}        if (currentValue is global::System.ComponentModel.INotifyPropertyChanged currentSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = currentSource;");
        sb.AppendLine(
            $"{indent}            currentSource.PropertyChanged += {prefix}Changed;"
        );
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Update(object? newValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        {prefix}Ensure(newValue);");
        sb.AppendLine($"{indent}        if ({prefix}Source is not null)");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine(
            $"{indent}            {prefix}Source.PropertyChanged -= {prefix}Changed;"
        );
        sb.AppendLine($"{indent}            {prefix}Source = null;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine(
            $"{indent}        if (newValue is global::System.ComponentModel.INotifyPropertyChanged newSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = newSource;");
        sb.AppendLine(
            $"{indent}            newSource.PropertyChanged += {prefix}Changed;"
        );
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
    }

    private static void GenerateCollectionMembers(
        StringBuilder sb,
        FieldInfo field,
        string indent,
        PropertyChangedInvokerKind invoker,
        bool useSuppressionWrapper
    )
    {
        var prefix = GetCollectionMemberPrefix(field);
        sb.AppendLine(
            $"{indent}    private global::System.Collections.Specialized.INotifyCollectionChanged? {prefix}Source;"
        );
        sb.AppendLine($"{indent}    private bool {prefix}Initialized;");
        sb.AppendLine($"{indent}    private void {prefix}Changed(");
        sb.AppendLine(
            $"{indent}        object? sender, global::System.Collections.Specialized.NotifyCollectionChangedEventArgs e"
        );
        sb.AppendLine($"{indent}    )");
        sb.AppendLine($"{indent}    {{");
        foreach (var propertyName in field.CollectionNotify)
        {
            AppendPropertyChangedCall(
                sb,
                indent + "        ",
                propertyName,
                invoker,
                useSuppressionWrapper: useSuppressionWrapper
            );
        }
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Ensure(object? currentValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        if ({prefix}Initialized)");
        sb.AppendLine($"{indent}            return;");
        sb.AppendLine();
        sb.AppendLine($"{indent}        {prefix}Initialized = true;");
        sb.AppendLine(
            $"{indent}        if (currentValue is global::System.Collections.Specialized.INotifyCollectionChanged currentSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = currentSource;");
        sb.AppendLine($"{indent}            currentSource.CollectionChanged += {prefix}Changed;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Update(object? newValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        {prefix}Ensure(newValue);");
        sb.AppendLine($"{indent}        if ({prefix}Source is not null)");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source.CollectionChanged -= {prefix}Changed;");
        sb.AppendLine($"{indent}            {prefix}Source = null;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine(
            $"{indent}        if (newValue is global::System.Collections.Specialized.INotifyCollectionChanged newSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = newSource;");
        sb.AppendLine($"{indent}            newSource.CollectionChanged += {prefix}Changed;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
    }

}
