using System.Text;

namespace BzsOIDC.Idp.Components.Admin;

public static class AdminTableSearch
{
    public static int? Score(string? rawQuery, params string?[] rawFields)
    {
        var query = Normalize(rawQuery);
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        var fields = rawFields
            .Select(Normalize)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (fields.Length == 0)
        {
            return null;
        }

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var combined = string.Join(' ', fields);
        var compactQuery = RemoveSpaces(query);
        var compactCombined = RemoveSpaces(combined);

        var best = fields
            .Select(field => ScoreSingleField(query, compactQuery, tokens, field))
            .Where(static score => score.HasValue)
            .Min();

        var combinedScore = ScoreSingleField(query, compactQuery, tokens, combined);
        if (combinedScore.HasValue)
        {
            best = !best.HasValue || combinedScore.Value < best.Value
                ? combinedScore
                : best;
        }

        if (best.HasValue)
        {
            return best;
        }

        if (IsSubsequence(compactQuery, compactCombined, out var penalty))
        {
            return 400 + penalty;
        }

        return null;
    }

    private static int? ScoreSingleField(string query, string compactQuery, string[] tokens, string field)
    {
        if (string.Equals(field, query, StringComparison.Ordinal))
        {
            return 0;
        }

        if (field.StartsWith(query, StringComparison.Ordinal))
        {
            return 20 + Math.Max(0, field.Length - query.Length);
        }

        var containsIndex = field.IndexOf(query, StringComparison.Ordinal);
        if (containsIndex >= 0)
        {
            return 60 + containsIndex;
        }

        if (tokens.Length > 1)
        {
            var tokenPositions = tokens
                .Select(token => field.IndexOf(token, StringComparison.Ordinal))
                .ToArray();

            if (tokenPositions.All(static position => position >= 0))
            {
                return 120 + tokenPositions.Sum();
            }
        }

        var compactField = RemoveSpaces(field);
        if (compactField.StartsWith(compactQuery, StringComparison.Ordinal))
        {
            return 180 + Math.Max(0, compactField.Length - compactQuery.Length);
        }

        if (IsSubsequence(compactQuery, compactField, out var penalty))
        {
            return 260 + penalty;
        }

        return null;
    }

    private static bool IsSubsequence(string query, string field, out int penalty)
    {
        penalty = 0;
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(field))
        {
            return false;
        }

        var queryIndex = 0;
        var lastMatchIndex = -1;

        for (var fieldIndex = 0; fieldIndex < field.Length && queryIndex < query.Length; fieldIndex++)
        {
            if (field[fieldIndex] != query[queryIndex])
            {
                continue;
            }

            if (lastMatchIndex >= 0)
            {
                penalty += fieldIndex - lastMatchIndex - 1;
            }

            lastMatchIndex = fieldIndex;
            queryIndex++;
        }

        return queryIndex == query.Length;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
                continue;
            }

            if (previousWasSpace)
            {
                continue;
            }

            builder.Append(' ');
            previousWasSpace = true;
        }

        return builder.ToString().Trim();
    }

    private static string RemoveSpaces(string value)
    {
        return value.Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
