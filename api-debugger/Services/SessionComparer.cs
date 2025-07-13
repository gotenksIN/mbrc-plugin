using System.Collections.Generic;
using System.Linq;
using MusicBeeRemote.ApiDebugger.Models;

namespace MusicBeeRemote.ApiDebugger.Services;

/// <summary>
/// Compares two recorded sessions to find API differences.
/// </summary>
public class SessionComparer
{
    /// <summary>
    /// Compares two sessions and returns detailed comparison results.
    /// </summary>
    /// <param name="sessionA">The baseline session (e.g., release version).</param>
    /// <param name="sessionB">The comparison session (e.g., development version).</param>
    /// <param name="options">Comparison options.</param>
    /// <returns>Comparison results with detailed diffs.</returns>
    public static SessionComparisonResult Compare(
        RecordedSession sessionA,
        RecordedSession sessionB,
        ComparisonOptions options)
    {
        var result = new SessionComparisonResult
        {
            SessionA = sessionA,
            SessionB = sessionB
        };

        // Group pairs by context for matching
        var pairsA = sessionA.Pairs
            .GroupBy(p => p.Context)
            .ToDictionary(g => g.Key, g => g.ToList());

        var pairsB = sessionB.Pairs
            .GroupBy(p => p.Context)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allContexts = pairsA.Keys.Union(pairsB.Keys).OrderBy(c => c);

        foreach (var context in allContexts)
        {
            pairsA.TryGetValue(context, out var listA);
            pairsB.TryGetValue(context, out var listB);

            var contextResults = ComparePairsForContext(context, listA, listB, options);
            result.Results.AddRange(contextResults);
        }

        // Calculate summary counts
        result.MatchCount = result.Results.Count(r => r.OverallStatus == ComparisonStatus.Match);
        result.DiffCount = result.Results.Count(r => r.OverallStatus == ComparisonStatus.Different);
        result.OnlyInACount = result.Results.Count(r => r.OverallStatus == ComparisonStatus.OnlyInA);
        result.OnlyInBCount = result.Results.Count(r => r.OverallStatus == ComparisonStatus.OnlyInB);

        return result;
    }

    private static List<PairComparisonResult> ComparePairsForContext(
        string context,
        List<RequestResponsePair>? listA,
        List<RequestResponsePair>? listB,
        ComparisonOptions options)
    {
        var results = new List<PairComparisonResult>();

        var countA = listA?.Count ?? 0;
        var countB = listB?.Count ?? 0;
        var maxCount = System.Math.Max(countA, countB);

        for (var i = 0; i < maxCount; i++)
        {
            var pairA = i < countA ? listA![i] : null;
            var pairB = i < countB ? listB![i] : null;

            var comparison = ComparePair(context, pairA, pairB, options);
            results.Add(comparison);
        }

        return results;
    }

    private static PairComparisonResult ComparePair(
        string context,
        RequestResponsePair? pairA,
        RequestResponsePair? pairB,
        ComparisonOptions options)
    {
        var result = new PairComparisonResult
        {
            Context = context,
            PairA = pairA,
            PairB = pairB,
            SequenceA = pairA?.SequenceNumber,
            SequenceB = pairB?.SequenceNumber
        };

        // Compare requests
        result.RequestStatus = CompareMessages(
            pairA?.Request?.RawJson,
            pairB?.Request?.RawJson,
            options,
            result.RequestDiffs);

        // Compare responses
        result.ResponseStatus = CompareMessages(
            pairA?.Response?.RawJson,
            pairB?.Response?.RawJson,
            options,
            result.ResponseDiffs);

        return result;
    }

    private static ComparisonStatus CompareMessages(
        string? jsonA,
        string? jsonB,
        ComparisonOptions options,
        List<string> diffs)
    {
        var hasA = !string.IsNullOrWhiteSpace(jsonA);
        var hasB = !string.IsNullOrWhiteSpace(jsonB);

        if (!hasA && !hasB)
            return ComparisonStatus.NoData;

        if (!hasA)
            return ComparisonStatus.OnlyInB;

        if (!hasB)
            return ComparisonStatus.OnlyInA;

        // Both have data - compare them
        if (JsonNormalizer.AreEqual(jsonA, jsonB, options))
            return ComparisonStatus.Match;

        // Get differences
        var differences = JsonNormalizer.GetDifferences(jsonA, jsonB, options);
        diffs.AddRange(differences);

        return ComparisonStatus.Different;
    }
}
