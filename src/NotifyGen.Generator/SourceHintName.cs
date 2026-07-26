using System;
using System.Text;

namespace NotifyGen.Generator;

internal static class SourceHintName
{
    public static string Create(string metadataIdentity, string targetName)
    {
        var encodedIdentity = Convert
            .ToBase64String(Encoding.UTF8.GetBytes(metadataIdentity))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var readableName = IsPortableAsciiIdentifier(targetName) ? targetName : "Type";
        return $"NotifyGen.{encodedIdentity}.{readableName}.g.cs";
    }

    private static bool IsPortableAsciiIdentifier(string value)
    {
        foreach (var character in value)
        {
            if (
                !(
                    (character >= 'A' && character <= 'Z')
                    || (character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9')
                    || character == '_'
                )
            )
                return false;
        }

        return value.Length > 0;
    }
}
