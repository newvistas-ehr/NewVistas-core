// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Which home-care program an episode belongs to. The whole module is built around
/// Home-Based Primary Care (Phase 1); <see cref="MedicareSkilledHomeHealth"/> is the
/// reserved Phase-2 program that activates the certification / OASIS / PDGM machinery.
/// </summary>
public enum HomeCareProgramType
{
    /// <summary>VA-style longitudinal, team-based primary care in the home (clinical-need eligibility).</summary>
    HomeBasedPrimaryCare,
    /// <summary>Reserved (Phase 2): episodic, certified, OASIS/PDGM-driven Medicare skilled home health.</summary>
    MedicareSkilledHomeHealth,
    /// <summary>
    /// Acute, inpatient-substitutive care delivered in the home (CMS "Acute Hospital Care at Home").
    /// The hospital renders hospital-level care in the home to free an inpatient bed — so a
    /// HospitalAtHome episode is ALWAYS <see cref="HomeCareDeliveryModel.HospitalProvided"/>.
    /// Appended last: Orleans persists enums by integer — never reorder or insert.
    /// </summary>
    HospitalAtHome
}

/// <summary>
/// WHO delivers the home care — orthogonal to <see cref="HomeCareProgramType"/> (what kind of care).
/// Defaults to <see cref="HospitalProvided"/> so every pre-existing episode (which has no stored value)
/// deserializes to the original implicit model with no data migration.
/// </summary>
public enum HomeCareDeliveryModel
{
    /// <summary>Our own program/staff deliver the care (the original implicit model, made explicit).</summary>
    HospitalProvided = 0,
    /// <summary>An independent home-health agency delivers the care; we coordinate (a coordination shell).</summary>
    ExternalAgency = 1
}

/// <summary>A tracked milestone in an externally-delivered (agency) episode we coordinate but do not staff.</summary>
public enum AgencyMilestoneType
{
    ReferralSent,
    StartOfCare,
    Recertification,
    PlanOfCareSigned,
    Hospitalization,
    Discharge,
    Other
}

/// <summary>Where the patient was admitted to home care from. VistA File #750 admission source.</summary>
public enum HomeCareAdmissionSource
{
    Community,
    AcuteHospital,
    SkilledNursingFacility,
    InpatientRehab,
    Other
}

/// <summary>Level of care intensity for the home-care episode. VistA File #750 (.05).</summary>
public enum HomeCareLevelOfCare
{
    Basic,
    Enhanced,
    Palliative
}

/// <summary>Lifecycle status of a home-care episode. VistA File #750 (.04).</summary>
public enum HomeCareEpisodeStatus
{
    Active,
    OnHold,
    Discharged,
    Deceased
}

/// <summary>Reason a home-care episode was discharged. VistA File #750 (.08).</summary>
public enum HomeCareDischargeReason
{
    GoalsMet,
    PatientDeclined,
    MovedOutOfArea,
    TransferredToFacility,
    SafetyConcern,
    Hospitalized,
    Deceased,
    Other
}

/// <summary>Discipline of a home-care team member or visit. VistA File #750.1 (.03).</summary>
public enum HomeCareDiscipline
{
    Physician,
    NursePractitioner,
    SkilledNursing,
    PhysicalTherapy,
    OccupationalTherapy,
    SpeechLanguagePathology,
    HomeHealthAide,
    MedicalSocialWork,
    Dietitian,
    Pharmacy,
    MentalHealth,
    Other
}

/// <summary>
/// Reserved (Phase 2): the skilled service that establishes Medicare home-health eligibility.
/// Unused in Phase 1 (HBPC eligibility is clinical-need, not skilled-need).
/// </summary>
public enum SkilledNeedType
{
    None,
    SkilledNursing,
    PhysicalTherapy,
    SpeechTherapy
}

/// <summary>
/// Eligibility for the home-care episode. Holds BOTH worlds so the Medicare extension is a
/// natural bolt-on: HBPC uses <see cref="ClinicalNeedNarrative"/>; the Medicare gates
/// (<see cref="IsHomebound"/> + <see cref="SkilledNeed"/>) are reserved for Phase 2.
/// </summary>
[GenerateSerializer]
public class HomeCareEligibility
{
    /// <summary>HBPC eligibility basis — complex/chronic clinical need (free text).</summary>
    [Id(0)] public string ClinicalNeedNarrative { get; set; } = string.Empty;

    /// <summary>Reserved (Phase 2): Medicare "homebound" determination.</summary>
    [Id(1)] public bool? IsHomebound { get; set; }

    /// <summary>Reserved (Phase 2): justification supporting homebound status.</summary>
    [Id(2)] public string HomeboundJustification { get; set; } = string.Empty;

    /// <summary>Reserved (Phase 2): the skilled need establishing Medicare eligibility.</summary>
    [Id(3)] public SkilledNeedType? SkilledNeed { get; set; }
}

/// <summary>A clinician on the home-care interdisciplinary team (IDT). VistA File #750 care team.</summary>
[GenerateSerializer]
public class HomeCareTeamMember
{
    [Id(0)] public string ProviderId { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public HomeCareDiscipline Discipline { get; set; }
    /// <summary>Free-text role (e.g. "Medical Director", "Primary RN").</summary>
    [Id(3)] public string RoleTitle { get; set; } = string.Empty;
    [Id(4)] public bool IsPrimary { get; set; }
    [Id(5)] public DateTime AssignedDate { get; set; }
    [Id(6)] public DateTime? UnassignedDate { get; set; }
}

/// <summary>
/// Reserved (Phase 2): a 60-day Medicare certification period, holding its two 30-day payment
/// periods. Empty in Phase 1 (HBPC episodes are open-ended, not certified in periods).
/// </summary>
[GenerateSerializer]
public class CertificationPeriod
{
    [Id(0)] public string PeriodId { get; set; } = string.Empty;
    [Id(1)] public DateTime StartDate { get; set; }
    [Id(2)] public DateTime EndDate { get; set; }
    [Id(3)] public bool IsRecertification { get; set; }
    [Id(4)] public string CertifyingProviderId { get; set; } = string.Empty;
    [Id(5)] public DateTime? FaceToFaceEncounterDate { get; set; }
    [Id(6)] public List<PaymentPeriod> PaymentPeriods { get; set; } = new();
}

/// <summary>Reserved (Phase 2): a 30-day PDGM payment period within a certification period.</summary>
[GenerateSerializer]
public class PaymentPeriod
{
    [Id(0)] public string PeriodId { get; set; } = string.Empty;
    [Id(1)] public DateTime StartDate { get; set; }
    [Id(2)] public DateTime EndDate { get; set; }
    [Id(3)] public PdgmGroupingResult? Grouping { get; set; }
}

/// <summary>Reserved (Phase 2): the PDGM case-mix classification for a payment period.</summary>
[GenerateSerializer]
public class PdgmGroupingResult
{
    [Id(0)] public string CaseMixGroup { get; set; } = string.Empty;
    [Id(1)] public string AdmissionSource { get; set; } = string.Empty;
    [Id(2)] public string Timing { get; set; } = string.Empty;
    [Id(3)] public string ClinicalGrouping { get; set; } = string.Empty;
    [Id(4)] public string FunctionalLevel { get; set; } = string.Empty;
    [Id(5)] public string ComorbidityAdjustment { get; set; } = string.Empty;
    [Id(6)] public bool IsLupa { get; set; }
}

/// <summary>
/// A milestone in an agency-delivered episode (a coordination shell). We track the agency's
/// start-of-care / recert / discharge as a thin timeline — NOT full visits (their staff render those).
/// </summary>
[GenerateSerializer]
public class AgencyCareMilestone
{
    [Id(0)] public string MilestoneId { get; set; } = string.Empty;
    [Id(1)] public AgencyMilestoneType Type { get; set; }
    [Id(2)] public DateTime Date { get; set; }
    [Id(3)] public string Note { get; set; } = string.Empty;
    [Id(4)] public string RecordedById { get; set; } = string.Empty;
    [Id(5)] public string RecordedByName { get; set; } = string.Empty;
}

/// <summary>
/// Coordination detail for an <see cref="HomeCareDeliveryModel.ExternalAgency"/> episode. Null on
/// hospital-provided episodes. Denormalizes the delivering agency's identity (from the
/// <c>HHA-DIRECTORY</c>) and holds the coordinated-care milestone timeline.
/// </summary>
[GenerateSerializer]
public class HomeCareAgencyCoordination
{
    /// <summary>Delivering agency id → <c>HHA-DIRECTORY</c> entry.</summary>
    [Id(0)] public string AgencyId { get; set; } = string.Empty;
    /// <summary>Agency name (denormalized for roster/detail display).</summary>
    [Id(1)] public string AgencyName { get; set; } = string.Empty;
    [Id(2)] public string? AgencyNpi { get; set; }
    /// <summary>CMS Certification Number (the agency's Medicare provider number).</summary>
    [Id(3)] public string? AgencyCcn { get; set; }
    /// <summary>Optional link to the community-care referral (<c>EXT-REF:{guid}</c>) that sent the patient out.</summary>
    [Id(4)] public string? ExternalReferralId { get; set; }
    /// <summary>Our internal coordinator/case-manager for the referred-out patient.</summary>
    [Id(5)] public string CoordinatorProviderId { get; set; } = string.Empty;
    [Id(6)] public string CoordinatorName { get; set; } = string.Empty;
    /// <summary>The coordinated-care milestone timeline (start-of-care, recert, discharge…).</summary>
    [Id(7)] public List<AgencyCareMilestone> Milestones { get; set; } = new();
}

/// <summary>
/// Acute-substitution detail for a <see cref="HomeCareProgramType.HospitalAtHome"/> episode. Null on
/// other episodes. A soft handoff link to the inpatient admission/bed the home care substitutes for —
/// "we freed this bed by moving the patient to Hospital-at-Home." Normal discharge already releases the
/// bed; this is a reference, not a bed-management rewrite.
/// </summary>
[GenerateSerializer]
public class HospitalAtHomeContext
{
    /// <summary>The ADT/bed admission id this episode substitutes for.</summary>
    [Id(0)] public string SourceAdmissionId { get; set; } = string.Empty;
    /// <summary>Institution id of the discharging facility (e.g. "500").</summary>
    [Id(1)] public string SourceFacilityId { get; set; } = string.Empty;
    [Id(2)] public string SourceFacilityName { get; set; } = string.Empty;
    /// <summary>The freed inpatient unit (optional).</summary>
    [Id(3)] public string? SourceUnitId { get; set; }
    /// <summary>The freed inpatient bed (optional) — the bed the substitution released.</summary>
    [Id(4)] public string? SourceBedId { get; set; }
    /// <summary>When acute-at-home substitution began.</summary>
    [Id(5)] public DateTime? SubstitutionStartDate { get; set; }
    /// <summary>Clinical rationale for treating this acute problem at home instead of a ward bed.</summary>
    [Id(6)] public string ClinicalRationale { get; set; } = string.Empty;
}

/// <summary>
/// A home-care episode — the spine of the Home-Based Care module. One open-ended episode per
/// HBPC enrollment (a patient may have a history of episodes; they are GUID-keyed and indexed
/// per patient). Designed so the reserved Phase-2 fields light up the Medicare program without
/// reshaping persisted HBPC data.
/// Key pattern: "HHC-EPISODE:{guid}". VistA File #750 (HOME BASED PRIMARY CARE). HBPC.m, HBHOME.m
/// </summary>
[GenerateSerializer]
public class HomeCareEpisodeState
{
    /// <summary>Unique episode identifier (grain key).</summary>
    [Id(0)] public string EpisodeId { get; set; } = string.Empty;

    /// <summary>Patient file number. VistA File #750 (.01).</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (denormalized for census/roster display). VistA File #750 (.02).</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Which home-care program this episode belongs to (HBPC in Phase 1).</summary>
    [Id(3)] public HomeCareProgramType ProgramType { get; set; } = HomeCareProgramType.HomeBasedPrimaryCare;

    /// <summary>Date admitted to the home-care program. VistA File #750 (.03).</summary>
    [Id(4)] public DateTime AdmissionDate { get; set; }

    /// <summary>Where the patient was admitted from.</summary>
    [Id(5)] public HomeCareAdmissionSource AdmissionSource { get; set; }

    /// <summary>Referring provider.</summary>
    [Id(6)] public string ReferringProviderId { get; set; } = string.Empty;
    [Id(7)] public string ReferringProviderName { get; set; } = string.Empty;

    /// <summary>Primary admitting diagnosis (ICD-10).</summary>
    [Id(8)] public string PrimaryDiagnosisCode { get; set; } = string.Empty;
    [Id(9)] public string PrimaryDiagnosisText { get; set; } = string.Empty;

    /// <summary>Secondary diagnoses.</summary>
    [Id(10)] public List<string> SecondaryDiagnoses { get; set; } = new();

    /// <summary>Level of care intensity. VistA File #750 (.05).</summary>
    [Id(11)] public HomeCareLevelOfCare LevelOfCare { get; set; }

    /// <summary>Eligibility (HBPC clinical need; reserved Medicare homebound + skilled need).</summary>
    [Id(12)] public HomeCareEligibility Eligibility { get; set; } = new();

    /// <summary>The interdisciplinary care team (IDT).</summary>
    [Id(13)] public List<HomeCareTeamMember> Team { get; set; } = new();

    /// <summary>Primary informal caregiver name.</summary>
    [Id(14)] public string PrimaryCaregiver { get; set; } = string.Empty;

    /// <summary>Patient's home address for visit routing.</summary>
    [Id(15)] public string HomeAddress { get; set; } = string.Empty;

    /// <summary>The current active plan of care (HHC-POC id).</summary>
    [Id(16)] public string PlanOfCareId { get; set; } = string.Empty;

    /// <summary>Visit ids (HHC-VISIT) recorded for this episode.</summary>
    [Id(17)] public List<string> VisitIds { get; set; } = new();

    /// <summary>Assessment ids (HHC-ASSESS) recorded for this episode.</summary>
    [Id(18)] public List<string> AssessmentIds { get; set; } = new();

    /// <summary>Lifecycle status.</summary>
    [Id(19)] public HomeCareEpisodeStatus Status { get; set; } = HomeCareEpisodeStatus.Active;

    /// <summary>Reason the episode is on hold (when Status = OnHold).</summary>
    [Id(20)] public string OnHoldReason { get; set; } = string.Empty;

    /// <summary>Date of the most recent completed visit.</summary>
    [Id(21)] public DateTime? LastVisitDate { get; set; }

    /// <summary>Date of the next scheduled visit.</summary>
    [Id(22)] public DateTime? NextVisitDate { get; set; }

    /// <summary>Discharge details.</summary>
    [Id(23)] public DateTime? DischargeDate { get; set; }
    [Id(24)] public HomeCareDischargeReason? DischargeReason { get; set; }
    [Id(25)] public string DischargeNotes { get; set; } = string.Empty;

    [Id(26)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(27)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Reserved (Phase 2): Medicare 60-day certification periods. Empty for HBPC episodes.
    /// </summary>
    [Id(28)] public List<CertificationPeriod> CertificationPeriods { get; set; } = new();

    /// <summary>
    /// WHO delivers this episode (orthogonal to <see cref="ProgramType"/>). Defaults to
    /// <see cref="HomeCareDeliveryModel.HospitalProvided"/> so pre-existing episodes need no migration.
    /// </summary>
    [Id(29)] public HomeCareDeliveryModel DeliveryModel { get; set; } = HomeCareDeliveryModel.HospitalProvided;

    /// <summary>Agency-coordination detail when <see cref="DeliveryModel"/> is ExternalAgency; null otherwise.</summary>
    [Id(30)] public HomeCareAgencyCoordination? AgencyCoordination { get; set; }

    /// <summary>Acute-substitution detail when <see cref="ProgramType"/> is HospitalAtHome; null otherwise.</summary>
    [Id(31)] public HospitalAtHomeContext? HospitalAtHome { get; set; }
}

/// <summary>Summary entry for the home-care census / caseload roster (the VistA HBH workload analog).</summary>
[GenerateSerializer]
public class HomeCareCensusEntry
{
    [Id(0)] public string EpisodeId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public HomeCareProgramType ProgramType { get; set; }
    [Id(4)] public HomeCareLevelOfCare LevelOfCare { get; set; }
    [Id(5)] public HomeCareEpisodeStatus Status { get; set; }
    [Id(6)] public string PrimaryDiagnosisText { get; set; } = string.Empty;
    [Id(7)] public string PrimaryProviderId { get; set; } = string.Empty;
    [Id(8)] public string PrimaryProviderName { get; set; } = string.Empty;
    [Id(9)] public DateTime AdmissionDate { get; set; }
    [Id(10)] public DateTime? LastVisitDate { get; set; }
    [Id(11)] public DateTime? NextVisitDate { get; set; }
    [Id(12)] public int OpenProblemCount { get; set; }

    /// <summary>Who delivers the episode — for the caseload delivery column/filter.</summary>
    [Id(13)] public HomeCareDeliveryModel DeliveryModel { get; set; } = HomeCareDeliveryModel.HospitalProvided;

    /// <summary>Delivering agency name when ExternalAgency; empty for hospital-provided.</summary>
    [Id(14)] public string AgencyName { get; set; } = string.Empty;
}
