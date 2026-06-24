// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Where the safety advisory originated. Drives provenance display and how the
/// message text should be cited to the patient.
/// </summary>
public enum AdvisorySourceType
{
    /// <summary>A free-text / manually authored advisory.</summary>
    Manual = 0,

    /// <summary>An FDA Drug Safety Communication (the narrative MedWatch document).</summary>
    FdaDrugSafetyCommunication = 1,

    /// <summary>Warning text pulled from a Structured Product Label via openFDA.</summary>
    OpenFdaLabel = 2,

    /// <summary>Warning text pulled from DailyMed SPL.</summary>
    DailyMedLabel = 3,

    /// <summary>An Rx-to-OTC market switch (e.g., Prilosec OTC) — triggers reconciliation.</summary>
    RxToOtcSwitch = 4,
}

/// <summary>Relative urgency of an advisory, used for triage and display.</summary>
public enum AdvisorySeverity
{
    Info = 0,
    Moderate = 1,
    High = 2,

    /// <summary>Corresponds to an FDA Boxed ("black box") Warning.</summary>
    BoxedWarning = 3,
}

/// <summary>Lifecycle state of an advisory.</summary>
public enum AdvisoryStatus
{
    /// <summary>Authored but not yet released for provider dispatch.</summary>
    Draft = 0,

    /// <summary>Released — providers may review and dispatch to patients.</summary>
    Active = 1,

    /// <summary>Withdrawn / superseded; no longer dispatchable.</summary>
    Retired = 2,
}

/// <summary>What the provider is being asked to do with the affected cohort.</summary>
public enum AdvisoryActionType
{
    /// <summary>Send the warning text to affected patients.</summary>
    WarnPatient = 0,

    /// <summary>
    /// Reconcile the medication — e.g., after an Rx→OTC switch, confirm whether the
    /// patient is still taking it and document it as a Non-VA / patient-reported med.
    /// </summary>
    ReconcileMedication = 1,
}

/// <summary>Channel a patient advisory was delivered through.</summary>
public enum AdvisoryChannel
{
    PatientPortal = 0,
    SecureMessage = 1,
    MailLetter = 2,
    InPersonCounseling = 3,
}

/// <summary>
/// A drug safety advisory targeting one or more VA therapeutic drug classes
/// (File #50.605). Authored from an FDA source (or an Rx→OTC switch event), reviewed,
/// then dispatched by individual providers to their affected patients.
///
/// Keyed by a stable advisory id. The canonical example is the May 2010 FDA Drug
/// Safety Communication on proton pump inhibitors (PPIs) and fracture risk, which
/// targets VA class <c>GA301</c>.
/// </summary>
[GenerateSerializer]
public class DrugSafetyAdvisoryState
{
    /// <summary>Stable advisory identifier (the grain key).</summary>
    [Id(0)]
    public string AdvisoryId { get; set; } = string.Empty;

    /// <summary>Short human title, e.g., "PPIs and risk of bone fracture".</summary>
    [Id(1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Where this advisory came from.</summary>
    [Id(2)]
    public AdvisorySourceType SourceType { get; set; } = AdvisorySourceType.Manual;

    /// <summary>
    /// Citation back to the source — an FDA DSC URL, an openFDA <c>set_id</c>, or a
    /// DailyMed setId. Lets the message cite an authoritative reference.
    /// </summary>
    [Id(3)]
    public string SourceReference { get; set; } = string.Empty;

    /// <summary>Date the source was published by FDA/NLM, when known.</summary>
    [Id(4)]
    public DateTime? SourcePublishedDate { get; set; }

    /// <summary>Relative urgency for triage and display.</summary>
    [Id(5)]
    public AdvisorySeverity Severity { get; set; } = AdvisorySeverity.Moderate;

    /// <summary>
    /// VA drug class codes this advisory applies to. A patient is "affected" when
    /// any active medication belongs to one of these classes — primary OR secondary,
    /// so multi-class drugs are matched against every class they belong to.
    /// </summary>
    [Id(6)]
    public List<string> TargetDrugClassCodes { get; set; } = new();

    /// <summary>
    /// Default patient-facing message. Providers may edit this per dispatch; the
    /// exact text sent to each patient is recorded on that patient's receipt.
    /// </summary>
    [Id(7)]
    public string DefaultMessage { get; set; } = string.Empty;

    /// <summary>Provider-facing clinical context (why this matters / what to do).</summary>
    [Id(8)]
    public string ClinicalSummary { get; set; } = string.Empty;

    /// <summary>What the provider is being asked to do (warn vs reconcile).</summary>
    [Id(9)]
    public AdvisoryActionType ActionType { get; set; } = AdvisoryActionType.WarnPatient;

    /// <summary>Lifecycle status.</summary>
    [Id(10)]
    public AdvisoryStatus Status { get; set; } = AdvisoryStatus.Draft;

    /// <summary>Distinct patient ids this advisory has been dispatched to (dedupe + reach).</summary>
    [Id(11)]
    public List<string> DispatchedPatientIds { get; set; } = new();

    /// <summary>Running count of patients reached.</summary>
    [Id(12)]
    public int TotalDispatched { get; set; }

    /// <summary>Most recent dispatch timestamp.</summary>
    [Id(13)]
    public DateTime? LastDispatchedDate { get; set; }

    /// <summary>User who authored the advisory.</summary>
    [Id(14)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Date the advisory was created.</summary>
    [Id(15)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the advisory was last modified.</summary>
    [Id(16)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of dispatching an advisory to a set of patients. Patients already reached
/// by a prior dispatch are skipped so no one is double-warned.
/// </summary>
[GenerateSerializer]
public class AdvisoryDispatchResult
{
    /// <summary>Number of patients newly sent the advisory by this call.</summary>
    [Id(0)]
    public int SentCount { get; set; }

    /// <summary>Patients skipped because they had already received this advisory.</summary>
    [Id(1)]
    public List<string> SkippedAlreadySent { get; set; } = new();
}
