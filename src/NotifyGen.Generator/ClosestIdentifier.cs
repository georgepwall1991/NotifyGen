using System;
using System.Collections.Generic;

namespace NotifyGen.Generator;

internal static class ClosestIdentifier
{
    public static string? Find(string value, IEnumerable<string> candidates)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (
                string.IsNullOrEmpty(candidate)
                || string.Equals(candidate, value, StringComparison.Ordinal)
            )
                continue;

            var distance = Levenshtein(value, candidate);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate;
        }

        if (best is null)
            return null;

        var threshold = Math.Max(1, Math.Min(3, (value.Length + 2) / 3));
        return bestDistance <= threshold ? best : null;
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost
                );
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
