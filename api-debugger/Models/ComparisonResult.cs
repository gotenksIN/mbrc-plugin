using System.Collections.Generic;

namespace MusicBeeRemote.ApiDebugger.Models;

/// <summary>
/// Result of comparing two recorded sessions.
/// </summary>
public class SessionComparisonResult
{
    /// <summary>
    /// The baseline session (typically release version).
    /// </summary>
    public RecordedSession SessionA { get; set; } = null!;

    /// <summary>
    /// The comparison session (typically development version).
    /// </summary>
    public RecordedSession SessionB { get; set; } = null!;

    /// <summary>
    /// Comparison results for each pair, matched by context.
    /// </summary>
    public List<PairComparisonResult> Results { get; set; } = [];

    /// <summary>
    /// Number of pairs that matched exactly.
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Number of pairs that differed.
    /// </summary>
    public int DiffCount { get; set; }

    /// <summary>
    /// Number of pairs only present in session A.
    /// </summary>
    public int OnlyInACount { get; set; }

    /// <summary>
    /// Number of pairs only present in session B.
    /// </summary>
    public int OnlyInBCount { get; set; }

    /// <summary>
    /// Total number of comparison results.
    /// </summary>
    public int TotalCount => Results.Count;

    /// <summary>
    /// Summary text for display.
    /// </summary>
    public string Summary => $"{MatchCount} match, {DiffCount} differ, {OnlyInACount} only in A, {OnlyInBCount} only in B";
}

/// <summary>
/// Comparison result for a single request-response pair.
/// </summary>
public class PairComparisonResult
{
    /// <summary>
    /// The context/command name being compared.
    /// </summary>
    public string Context { get; set; } = "";

    /// <summary>
    /// Sequence number in session A, or null if not present.
    /// </summary>
    public int? SequenceA { get; set; }

    /// <summary>
    /// Sequence number in session B, or null if not present.
    /// </summary>
    public int? SequenceB { get; set; }

    /// <summary>
    /// Status of the request comparison.
    /// </summary>
    public ComparisonStatus RequestStatus { get; set; }

    /// <summary>
    /// Status of the response comparison.
    /// </summary>
    public ComparisonStatus ResponseStatus { get; set; }

    /// <summary>
    /// The pair from session A, if present.
    /// </summary>
    public RequestResponsePair? PairA { get; set; }

    /// <summary>
    /// The pair from session B, if present.
    /// </summary>
    public RequestResponsePair? PairB { get; set; }

    /// <summary>
    /// List of differences found in the request (JSON paths and values).
    /// </summary>
    public List<string> RequestDiffs { get; set; } = [];

    /// <summary>
    /// List of differences found in the response (JSON paths and values).
    /// </summary>
    public List<string> ResponseDiffs { get; set; } = [];

    /// <summary>
    /// Response time from session A in milliseconds.
    /// </summary>
    public int? ResponseTimeMsA => PairA?.ResponseTimeMs;

    /// <summary>
    /// Response time from session B in milliseconds.
    /// </summary>
    public int? ResponseTimeMsB => PairB?.ResponseTimeMs;

    /// <summary>
    /// Overall status combining request and response statuses.
    /// </summary>
    public ComparisonStatus OverallStatus
    {
        get
        {
            if (RequestStatus == ComparisonStatus.OnlyInA || ResponseStatus == ComparisonStatus.OnlyInA)
                return ComparisonStatus.OnlyInA;
            if (RequestStatus == ComparisonStatus.OnlyInB || ResponseStatus == ComparisonStatus.OnlyInB)
                return ComparisonStatus.OnlyInB;
            if (RequestStatus == ComparisonStatus.Different || ResponseStatus == ComparisonStatus.Different)
                return ComparisonStatus.Different;
            return ComparisonStatus.Match;
        }
    }

    /// <summary>
    /// Whether this result has any differences.
    /// </summary>
    public bool HasDifferences => OverallStatus != ComparisonStatus.Match;
}

/// <summary>
/// Status of a comparison between two values.
/// </summary>
public enum ComparisonStatus
{
    /// <summary>
    /// Values are identical after normalization.
    /// </summary>
    Match,

    /// <summary>
    /// Both values exist but differ.
    /// </summary>
    Different,

    /// <summary>
    /// Value only exists in session A (missing in B).
    /// </summary>
    OnlyInA,

    /// <summary>
    /// Value only exists in session B (missing in A).
    /// </summary>
    OnlyInB,

    /// <summary>
    /// No data in this position (e.g., push notification with no request).
    /// </summary>
    NoData
}

/// <summary>
/// Options for session comparison.
/// </summary>
public class ComparisonOptions
{
    /// <summary>
    /// JSON fields to ignore during comparison (e.g., "client_id", "timestamp").
    /// </summary>
    public List<string> IgnoreFields { get; set; } = ["client_id", "timestamp"];

    /// <summary>
    /// Whether to ignore array element ordering.
    /// </summary>
    public bool IgnoreArrayOrder { get; set; }

    /// <summary>
    /// Whether to normalize whitespace in JSON strings.
    /// </summary>
    public bool NormalizeWhitespace { get; set; } = true;
}
