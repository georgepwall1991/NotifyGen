using System.Text;

namespace NotifyGen.Generator;

internal static class SourceHintName
{
    public static string Create(string metadataIdentity, string targetName)
    {
        const int maxSegmentLength = 100;
        const string hexDigits = "0123456789abcdef";
        var identityBytes = Encoding.UTF8.GetBytes(metadataIdentity);
        var encodedIdentity = new char[identityBytes.Length * 2];
        for (var index = 0; index < identityBytes.Length; index++)
        {
            var value = identityBytes[index];
            encodedIdentity[index * 2] = hexDigits[value >> 4];
            encodedIdentity[index * 2 + 1] = hexDigits[value & 0x0F];
        }

        var readableName =
            targetName.Length <= maxSegmentLength - ".g.cs".Length
            && IsPortableAsciiIdentifier(targetName)
                ? targetName
                : "Type";
        var hintName = new StringBuilder(encodedIdentity.Length + readableName.Length + 32);
        hintName.Append("NotifyGen");
        for (var offset = 0; offset < encodedIdentity.Length; offset += maxSegmentLength)
        {
            hintName.Append('/');
            hintName.Append(
                encodedIdentity,
                offset,
                System.Math.Min(maxSegmentLength, encodedIdentity.Length - offset)
            );
        }
        hintName.Append('/');
        hintName.Append(readableName);
        hintName.Append(".g.cs");
        return hintName.ToString();
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
