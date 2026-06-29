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
    MedicareSkilledHomeHealth
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
}
