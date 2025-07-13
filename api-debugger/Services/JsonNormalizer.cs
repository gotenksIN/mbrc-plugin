using System;
using System.Collections.Generic;
using System.Linq;
using MusicBeeRemote.ApiDebugger.Models;
using Newtonsoft.Json.Linq;

namespace MusicBeeRemote.ApiDebugger.Services;

/// <summary>
/// Normalizes JSON for comparison by removing ignored fields and sorting keys.
/// </summary>
public static class JsonNormalizer
{
    /// <summary>
    /// Normalizes a JSON string for comparison.
    /// </summary>
    /// <param name="json">The JSON string to normalize.</param>
    /// <param name="options">Comparison options specifying fields to ignore.</param>
    /// <returns>Normalized JSON string.</returns>
    public static string Normalize(string json, ComparisonOptions options)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";

        try
        {
            var token = JToken.Parse(json);
            var normalized = NormalizeToken(token, options);
            return normalized.ToString(Newtonsoft.Json.Formatting.Indented);
        }
        catch
        {
            // If parsing fails, return as-is (trimmed)
            return options.NormalizeWhitespace ? json.Trim() : json;
        }
    }

    /// <summary>
    /// Compares two JSON strings and returns a list of differences.
    /// </summary>
    /// <param name="jsonA">First JSON string.</param>
    /// <param name="jsonB">Second JSON string.</param>
    /// <param name="options">Comparison options.</param>
    /// <returns>List of difference descriptions.</returns>
    public static List<string> GetDifferences(string? jsonA, string? jsonB, ComparisonOptions options)
    {
        var diffs = new List<string>();

        if (string.IsNullOrWhiteSpace(jsonA) && string.IsNullOrWhiteSpace(jsonB))
            return diffs;

        if (string.IsNullOrWhiteSpace(jsonA))
        {
            diffs.Add("Value only in B (missing in A)");
            return diffs;
        }

        if (string.IsNullOrWhiteSpace(jsonB))
        {
            diffs.Add("Value only in A (missing in B)");
            return diffs;
        }

        try
        {
            var tokenA = JToken.Parse(jsonA);
            var tokenB = JToken.Parse(jsonB);

            var normalizedA = NormalizeToken(tokenA, options);
            var normalizedB = NormalizeToken(tokenB, options);

            CompareTokens(normalizedA, normalizedB, "", diffs, options);
        }
        catch (Exception ex)
        {
            diffs.Add($"JSON parse error: {ex.Message}");
        }

        return diffs;
    }

    /// <summary>
    /// Checks if two JSON strings are equal after normalization.
    /// </summary>
    public static bool AreEqual(string? jsonA, string? jsonB, ComparisonOptions options)
    {
        if (string.IsNullOrWhiteSpace(jsonA) && string.IsNullOrWhiteSpace(jsonB))
            return true;

        if (string.IsNullOrWhiteSpace(jsonA) || string.IsNullOrWhiteSpace(jsonB))
            return false;

        var normalizedA = Normalize(jsonA, options);
        var normalizedB = Normalize(jsonB, options);

        return string.Equals(normalizedA, normalizedB, StringComparison.Ordinal);
    }

    private static JToken NormalizeToken(JToken token, ComparisonOptions options)
    {
        return token switch
        {
            JObject obj => NormalizeObject(obj, options),
            JArray arr => NormalizeArray(arr, options),
            JValue val => val.DeepClone(),
            _ => token.DeepClone()
        };
    }

    private static JObject NormalizeObject(JObject obj, ComparisonOptions options)
    {
        var result = new JObject();

        // Sort properties by name for consistent comparison
        var sortedProperties = obj.Properties()
            .Where(p => !options.IgnoreFields.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.Ordinal);

        foreach (var prop in sortedProperties)
        {
            result[prop.Name] = NormalizeToken(prop.Value, options);
        }

        return result;
    }

    private static JArray NormalizeArray(JArray arr, ComparisonOptions options)
    {
        var normalizedItems = arr.Select(item => NormalizeToken(item, options)).ToList();

        if (options.IgnoreArrayOrder)
        {
            // Sort array items by their string representation for order-independent comparison
            normalizedItems = normalizedItems
                .OrderBy(item => item.ToString(Newtonsoft.Json.Formatting.None), StringComparer.Ordinal)
                .ToList();
        }

        return new JArray(normalizedItems);
    }

    private static void CompareTokens(JToken tokenA, JToken tokenB, string path, List<string> diffs, ComparisonOptions options)
    {
        if (tokenA.Type != tokenB.Type)
        {
            diffs.Add($"{path}: Type changed from {tokenA.Type} to {tokenB.Type}");
            return;
        }

        switch (tokenA)
        {
            case JObject objA when tokenB is JObject objB:
                CompareObjects(objA, objB, path, diffs, options);
                break;

            case JArray arrA when tokenB is JArray arrB:
                CompareArrays(arrA, arrB, path, diffs, options);
                break;

            case JValue valA when tokenB is JValue valB:
                if (!JToken.DeepEquals(valA, valB))
                {
                    var valueA = FormatValue(valA);
                    var valueB = FormatValue(valB);
                    diffs.Add($"{path}: {valueA} → {valueB}");
                }
                break;
        }
    }

    private static void CompareObjects(JObject objA, JObject objB, string path, List<string> diffs, ComparisonOptions options)
    {
        var keysA = objA.Properties().Select(p => p.Name).ToHashSet();
        var keysB = objB.Properties().Select(p => p.Name).ToHashSet();

        // Keys only in A
        foreach (var key in keysA.Except(keysB))
        {
            if (!options.IgnoreFields.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                var keyPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
                diffs.Add($"{keyPath}: Removed (was {FormatValue(objA[key])})");
            }
        }

        // Keys only in B
        foreach (var key in keysB.Except(keysA))
        {
            if (!options.IgnoreFields.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                var keyPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
                diffs.Add($"{keyPath}: Added ({FormatValue(objB[key])})");
            }
        }

        // Keys in both
        foreach (var key in keysA.Intersect(keysB))
        {
            if (!options.IgnoreFields.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                var keyPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
                CompareTokens(objA[key]!, objB[key]!, keyPath, diffs, options);
            }
        }
    }

    private static void CompareArrays(JArray arrA, JArray arrB, string path, List<string> diffs, ComparisonOptions options)
    {
        if (arrA.Count != arrB.Count)
        {
            diffs.Add($"{path}: Array length changed from {arrA.Count} to {arrB.Count}");
        }

        var minLength = Math.Min(arrA.Count, arrB.Count);
        for (var i = 0; i < minLength; i++)
        {
            CompareTokens(arrA[i], arrB[i], $"{path}[{i}]", diffs, options);
        }

        // Report extra items in A
        for (var i = minLength; i < arrA.Count; i++)
        {
            diffs.Add($"{path}[{i}]: Removed ({FormatValue(arrA[i])})");
        }

        // Report extra items in B
        for (var i = minLength; i < arrB.Count; i++)
        {
            diffs.Add($"{path}[{i}]: Added ({FormatValue(arrB[i])})");
        }
    }

    private static string FormatValue(JToken? token)
    {
        if (token == null)
            return "null";

        var str = token.ToString(Newtonsoft.Json.Formatting.None);

        // Truncate long values
        const int maxLength = 50;
        if (str.Length > maxLength)
        {
            str = str[..(maxLength - 3)] + "...";
        }

        return str;
    }
}
