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
    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;

    private static string FormatType(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedTypeDisplayFormat);

    private static string FormatPrimitive(object value) =>
        value switch
        {
            string text => QuoteString(text),
            char character => QuoteChar(character),
            bool boolean => boolean ? "true" : "false",
            float single => single.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F",
            double doubleValue => doubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D",
            long longValue => longValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
            ulong ulongValue => ulongValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL",
            uint uintValue => uintValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "U",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!
        };

    private static string QuoteString(string value) => $"\"{Escape(value)}\"";

    private static string QuoteChar(char value) => $"'{Escape(value.ToString())}'";

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            if (character == '\\')
                builder.Append('\\').Append('\\');
            else if (character == '"')
                builder.Append('\\').Append('"');
            else if (character == '\'')
                builder.Append('\\').Append('\'');
            else if (character == '\0')
                builder.Append('\\').Append('0');
            else if (character == '\a')
                builder.Append('\\').Append('a');
            else if (character == '\b')
                builder.Append('\\').Append('b');
            else if (character == '\f')
                builder.Append('\\').Append('f');
            else if (character == '\n')
                builder.Append('\\').Append('n');
            else if (character == '\r')
                builder.Append('\\').Append('r');
            else if (character == '\t')
                builder.Append('\\').Append('t');
            else if (character == '\v')
                builder.Append('\\').Append('v');
            else if (char.IsControl(character))
                builder.Append("\\u").Append(((int)character).ToString("X4"));
            else
                builder.Append(character);
        }

        return builder.ToString();
    }

}
