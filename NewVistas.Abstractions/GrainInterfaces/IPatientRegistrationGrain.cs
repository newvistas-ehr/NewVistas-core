// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton patient registration workflow. Issues an ICN, creates the
/// patient workflow grain keyed by that ICN, populates demographics + DFN,
/// registers the patient with the local cluster's MPI correlation and
/// search index, and hands off to the configured eligibility policy.
///
/// Grain key: the constant <c>"REGISTRATION"</c>. There is exactly one
/// activation per cluster.
///
/// <para>
/// <b>Invocation pattern.</b> This is a workflow grain — internal UIs
/// (Blazor, WPF, CharUI) invoke it directly via the Orleans client (the
/// JWT identity is propagated through Orleans <c>RequestContext</c> and
/// the grain-side <c>AuthorizationCallFilter</c> enforces the
/// <see cref="SecurityKeys.CanRegisterPatients"/> gate). There is no REST
/// surface by design: PatientPortal cannot self-register clinical records
/// and no third-party integration creates patients.
/// </para>
///
/// See <see href="../Docs/Architect-decisions/ADR-001-Patient-Identity-Strategy.md">ADR-001</see>:
/// every patient grain is keyed by ICN. DFN is preserved on the patient
/// state for forensic purposes but is not used as a lookup key.
/// </summary>
public interface IPatientRegistrationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Register a new patient. Returns the assigned ICN — the caller must
    /// use this value as the patient grain key for all subsequent operations.
    /// </summary>
    /// <remarks>
    /// To register a patient with an externally-supplied ICN (future AITC
    /// integration), set <see cref="RegistrationRequest.ExternallySuppliedIcn"/>
    /// on the request; in that case the local
    /// <see cref="IIcnIssuerGrain"/> is bypassed.
    /// </remarks>
    [RequiresSecurityKey(SecurityKeys.CanRegisterPatients)]
    [AuditAction("REGISTRATION", "REGISTER", EntityType = "PATIENT", IsClinicalWrite = true)]
    Task<string> RegisterPatientAsync(RegistrationRequest request);
}

/// <summary>
/// Input to <see cref="IPatientRegistrationGrain.RegisterPatientAsync"/>.
/// All fields except <see cref="ExternallySuppliedIcn"/> are required.
/// </summary>
[GenerateSerializer]
public class RegistrationRequest
{
    /// <summary>Patient name in <c>Last,First M</c> format.</summary>
    [Id(0)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Social security number, 9 digits, no dashes.</summary>
    [Id(1)]
    public string Ssn { get; set; } = string.Empty;

    /// <summary>Date of birth.</summary>
    [Id(2)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Sex: <c>"M"</c> or <c>"F"</c>.</summary>
    [Id(3)]
    public string? Sex { get; set; }

    /// <summary>
    /// Local facility IEN (DFN) for this patient. Stored on the patient
    /// state as legacy data per ADR-001; not used as a grain key.
    /// </summary>
    [Id(4)]
    public string FacilityDfn { get; set; } = string.Empty;

    /// <summary>
    /// Optional externally-supplied ICN. When set, the local
    /// <see cref="IIcnIssuerGrain"/> is bypassed and this value is used as
    /// the patient's ICN. Used by future AITC-MVI integration.
    /// </summary>
    [Id(5)]
    public string? ExternallySuppliedIcn { get; set; }

    // ─── Optional eligibility hints ─────────────────────────────────────────
    // These fields are read by the configured IRegistrationEligibilityPolicy.
    // VA-aligned deployments use them to drive the §17.36 priority-group
    // determination at registration time. Non-VA deployments (NoOp policy)
    // ignore them. Future organization-specific policies may add their own
    // hint fields below.

    /// <summary>(VA) Whether this patient is a US veteran. Required for VA enrollment processing.</summary>
    [Id(6)]
    public bool? IsVeteran { get; set; }

    /// <summary>(VA) Service-connected disability percentage, 0–100.</summary>
    [Id(7)]
    public int? ServiceConnectedPercentage { get; set; }

    /// <summary>(VA) Whether this patient was awarded the Purple Heart.</summary>
    [Id(8)]
    public bool? IsPurpleHeart { get; set; }

    /// <summary>(VA) Whether this patient is a former prisoner of war.</summary>
    [Id(9)]
    public bool? IsFormerPow { get; set; }

    /// <summary>(VA) Whether this patient is catastrophically disabled.</summary>
    [Id(10)]
    public bool? IsCatastrophicallyDisabled { get; set; }

    /// <summary>(VA) Whether this patient receives a VA pension.</summary>
    [Id(11)]
    public bool? ReceivesVaPension { get; set; }

    /// <summary>(VA) Primary VistA eligibility code (e.g., "SC LESS THAN 50%", "NSC").</summary>
    [Id(12)]
    public string? PrimaryEligibilityCode { get; set; }

    // ─── Optional IHS / tribal eligibility hints ────────────────────────────
    // Read by IhsTribalEligibilityPolicy under 38 CFR Part 136 rules.
    // Non-IHS deployments leave these null.

    /// <summary>(IHS) Whether this patient is an enrolled member of a federally-recognized tribe.</summary>
    [Id(13)]
    public bool? IsTribalMember { get; set; }

    /// <summary>(IHS) Name of the federally-recognized tribe the patient is enrolled in.</summary>
    [Id(14)]
    public string? TribalAffiliation { get; set; }

    /// <summary>(IHS) Certificate of Degree of Indian Blood number, if known.</summary>
    [Id(15)]
    public string? CdibNumber { get; set; }

    /// <summary>(IHS/CHS) Whether this patient resides within the Contract Health Service Delivery Area (CHSDA).</summary>
    [Id(16)]
    public bool? ResidesInChsda { get; set; }

    /// <summary>(IHS/CHS) Number of days the patient has resided in the CHSDA. Used for the 180-day CHS residency requirement.</summary>
    [Id(17)]
    public int? ChsdaResidencyDays { get; set; }

    /// <summary>(IHS) Optional eligibility-by-category code for non-Indian patients (e.g., "PREGNANT-NON-INDIAN" — pregnant by an eligible Indian, eligible during pregnancy and 6 weeks postpartum).</summary>
    [Id(18)]
    public string? IhsEligibleByCategory { get; set; }
}
