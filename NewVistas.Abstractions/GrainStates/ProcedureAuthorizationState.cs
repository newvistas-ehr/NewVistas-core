// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lifecycle status of a medical/procedure prior-authorization request. Parallel to the drug PA
/// (<see cref="PriorAuthorizationState"/>) but a real, append-only enum (mirrors
/// <c>EligibilityInquiryStatus</c>). <see cref="Pended"/> = the payer asked for more information.
/// </summary>
[GenerateSerializer]
public enum ProcedureAuthorizationStatus
{
    Draft = 0,
    Submitted = 1,
    Pended = 2,
    Approved = 3,
    Denied = 4,
    Expired = 5,
    Cancelled = 6
}

/// <summary>
/// How a procedure prior-auth was (or will be) submitted to the payer. <see cref="Electronic"/> is the
/// deferred X12 278 / Da Vinci PAS transmitter seam; the rest are the manual-first reality (a payer that
/// won't take electronic PA still gets tracked here).
/// </summary>
[GenerateSerializer]
public enum ProcedureAuthSubmissionChannel
{
    Electronic = 0,
    PayerPortal = 1,
    Phone = 2,
    Fax = 3
}

/// <summary>
/// The shared documentation-requirement vocabulary — the single language the curated baseline
/// (<c>PriorAuthRequirementCatalog</c>) and the learned-from-denials KB both speak, so they merge
/// cleanly into one "fill these boxes" checklist. Append-only. <see cref="Other"/> is the catch-all for
/// a denial reason that doesn't map to a real category (surfaced, never dropped).
/// </summary>
[GenerateSerializer]
public enum PriorAuthRequirementCategory
{
    ConservativeTherapyTrial = 0,
    ImagingEvidence = 1,
    FailedPriorTreatment = 2,
    MedicalNecessityNarrative = 3,
    SiteOfServiceJustification = 4,
    LabResults = 5,
    ContraindicationDocs = 6,
    DeviceOrImplantJustification = 7,
    SpecialistEvaluation = 8,
    ClinicalGuidelineCitation = 9,
    Other = 99
}

/// <summary>Where a checklist line came from — the curated baseline, the learned denial history, or both.</summary>
[GenerateSerializer]
public enum RequirementSource
{
    Baseline = 0,
    Learned = 1,
    Both = 2
}

/// <summary>A single denial reason on a procedure PA, mapped to the shared requirement category it cites.</summary>
[GenerateSerializer]
public record ProcedureDenialReason
{
    [Id(0)] public PriorAuthRequirementCategory Category { get; set; }
    [Id(1)] public string ReasonText { get; set; } = string.Empty;
    [Id(2)] public DateTime RecordedOn { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A medical/procedure prior-authorization request — the parallel to the drug PA grain, keyed on a CPT
/// procedure + payer rather than a drug. Grain key: <c>PROC-AUTH:{guid}</c>. Aligned with the X12 278
/// authorization concept (Claims Tracking, VistA #356.x) but manual-first: the submission channel is
/// tracked so a phone/portal/fax PA is fully modeled, and its outcome feeds the requirements KB.
/// </summary>
[GenerateSerializer]
public class ProcedureAuthorizationState
{
    [Id(0)] public string ProcAuthId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>The procedure being authorized (CPT File #81 code, e.g. "27447" = TKA).</summary>
    [Id(2)] public string CptCode { get; set; } = string.Empty;
    [Id(3)] public string CptDescription { get; set; } = string.Empty;

    /// <summary>The payer (PayerConfig trading-partner id, e.g. "PAYER-BCBS-FL").</summary>
    [Id(4)] public string PayerId { get; set; } = string.Empty;
    [Id(5)] public string PayerName { get; set; } = string.Empty;

    [Id(6)] public string OrderingProviderId { get; set; } = string.Empty;
    [Id(7)] public string OrderingProviderName { get; set; } = string.Empty;

    [Id(8)] public List<string> DiagnosisCodes { get; set; } = new();
    [Id(9)] public string ClinicalJustification { get; set; } = string.Empty;

    [Id(10)] public DateTime? RequestedServiceStartDate { get; set; }
    [Id(11)] public DateTime? RequestedServiceEndDate { get; set; }

    [Id(12)] public ProcedureAuthSubmissionChannel SubmissionChannel { get; set; } = ProcedureAuthSubmissionChannel.PayerPortal;
    [Id(13)] public ProcedureAuthorizationStatus Status { get; set; } = ProcedureAuthorizationStatus.Draft;

    [Id(14)] public string AuthorizationNumber { get; set; } = string.Empty;
    [Id(15)] public DateTime? SubmittedDate { get; set; }
    [Id(16)] public DateTime? DecisionDate { get; set; }
    [Id(17)] public DateTime? ExpirationDate { get; set; }
    [Id(18)] public string DecisionById { get; set; } = string.Empty;
    [Id(19)] public string DecisionByName { get; set; } = string.Empty;

    /// <summary>Denial reasons (mapped to categories) — the signal that feeds the requirements KB.</summary>
    [Id(20)] public List<ProcedureDenialReason> DenialReasons { get; set; } = new();

    /// <summary>Additional-information-requested note when the payer pends the request.</summary>
    [Id(21)] public string? PendedInfoRequested { get; set; }

    // Optional links back to what triggered the PA.
    [Id(22)] public string? OrderId { get; set; }
    [Id(23)] public string? ConsultId { get; set; }
    [Id(24)] public string? ExternalReferralId { get; set; }

    /// <summary>What the electronic transmitter reported (or the offline stand-in), for audit.</summary>
    [Id(25)] public string? TransmissionDetail { get; set; }

    [Id(26)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(27)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Roster entry for the per-patient procedure-PA index.</summary>
[GenerateSerializer]
public record ProcedureAuthIndexEntry
{
    [Id(0)] public string ProcAuthId { get; set; } = string.Empty;
    [Id(1)] public string CptCode { get; set; } = string.Empty;
    [Id(2)] public string CptDescription { get; set; } = string.Empty;
    [Id(3)] public string PayerName { get; set; } = string.Empty;
    [Id(4)] public ProcedureAuthorizationStatus Status { get; set; }
    [Id(5)] public DateTime? SubmittedDate { get; set; }
    [Id(6)] public DateTime? ExpirationDate { get; set; }
    [Id(7)] public string OrderingProviderName { get; set; } = string.Empty;
}

/// <summary>Per-patient index of procedure-PA requests. Grain key: <c>PROC-AUTH-IDX:{patientId}</c>.</summary>
[GenerateSerializer]
public class ProcedureAuthorizationIndexState
{
    [Id(0)] public List<ProcedureAuthIndexEntry> Entries { get; set; } = new();
}
