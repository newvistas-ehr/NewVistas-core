// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single peer review or root cause analysis record.
/// Grain key: "QM-REVIEW:{guid}"
/// Implements VA Patient Safety Manager / QM Review workflows.
/// </summary>
public interface IQMReviewGrain : IGrainWithStringKey
{
    /// <summary>
    /// Assigns the review to a reviewer with a due date.
    /// Status → Pending.
    /// </summary>
    Task AssignReviewAsync(
        string incidentId,
        GrainStates.QMReviewType reviewType,
        string assignedTo,
        string reviewerName,
        string reviewerTitle,
        DateTime dueDate,
        bool confidential);

    /// <summary>Marks the reviewer as having started work. Status → InProgress.</summary>
    Task StartReviewAsync();

    /// <summary>Records the clinical and systems analysis findings.</summary>
    Task RecordFindingsAsync(
        string summary,
        GrainStates.ReviewFinding primaryFinding,
        List<string> contributingFactors,
        string rootCause,
        List<string> systemFailures,
        string humanFactors,
        string environmentalFactors);

    /// <summary>Appends a formal recommendation to the review.</summary>
    Task AddRecommendationAsync(string recommendation);

    /// <summary>Adds a corrective action item to the review.</summary>
    Task AddActionItemAsync(string description, string assignedTo, DateTime dueDate);

    /// <summary>Completes the review with a final conclusion. Status → Completed.</summary>
    Task CompleteReviewAsync(string finalConclusion, string lessonsLearned);

    /// <summary>Approves the completed review. Status → Approved.</summary>
    Task ApproveReviewAsync();

    /// <summary>Returns the full state of this review record.</summary>
    Task<GrainStates.QMReviewState> GetReviewAsync();
}
