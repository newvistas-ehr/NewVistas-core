// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab EDI Result Grain Interface — represents an inbound result set
/// received from an external reference laboratory.
///
/// Grain Key: "LAB-EDI-RES:{guid}"
///
/// Results arrive via ORU^R01 messages from reference labs. Each grain
/// represents one result set (which may contain multiple test results)
/// tied to a specific outbound order.
///
/// Lifecycle: Received → Reviewed → Accepted (filed to patient record)
///                                → Rejected (discarded with reason)
/// </summary>
public interface ILabEdiResultGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete EDI result state.
    /// </summary>
    Task<LabEdiResultState> GetResultAsync();

    /// <summary>
    /// Records a result set received from a reference lab.
    /// </summary>
    Task RecordResultAsync(
        string orderId,
        string patientId,
        string referenceLab,
        string referenceLabId,
        string hl7MessageId,
        List<LabEdiResultItem> results,
        DateTime resultDate,
        string? performingLabClia);

    /// <summary>
    /// Reviews the result — marks as reviewed by a lab professional.
    /// </summary>
    Task ReviewResultAsync(
        string reviewedBy,
        string reviewedByName,
        DateTime reviewedDate,
        string? comments);

    /// <summary>
    /// Accepts the result and files it into the patient's lab record.
    /// </summary>
    Task AcceptResultAsync(string acceptedBy);

    /// <summary>
    /// Rejects the result with a reason — will not be filed.
    /// </summary>
    Task RejectResultAsync(string rejectedBy, string reason);
}
