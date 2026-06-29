// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Status of a problem on the home-care plan of care.</summary>
public enum CarePlanProblemStatus
{
    Active,
    Resolved,
    Discontinued
}

/// <summary>
/// One problem on the interdisciplinary plan of care, with its goals and interventions and the
/// discipline responsible for it.
/// </summary>
[GenerateSerializer]
public class CarePlanProblem
{
    [Id(0)] public string ProblemId { get; set; } = string.Empty;
    [Id(1)] public string Problem { get; set; } = string.Empty;
    /// <summary>Etiology / "related to".</summary>
    [Id(2)] public string RelatedTo { get; set; } = string.Empty;
    [Id(3)] public List<string> Goals { get; set; } = new();
    [Id(4)] public List<string> Interventions { get; set; } = new();
    [Id(5)] public HomeCareDiscipline ResponsibleDiscipline { get; set; }
    [Id(6)] public CarePlanProblemStatus Status { get; set; } = CarePlanProblemStatus.Active;
}

/// <summary>
/// The interdisciplinary, problem-oriented plan of care for a home-care episode. HBPC reviews it
/// periodically; the reserved Phase-2 fields carry the Medicare CMS-485 content and the
/// physician certification / recertification.
/// Key pattern: "HHC-POC:{guid}". VistA File #750 plan of care.
/// </summary>
[GenerateSerializer]
public class HomeCarePlanState
{
    [Id(0)] public string PlanId { get; set; } = string.Empty;
    [Id(1)] public string EpisodeId { get; set; } = string.Empty;
    [Id(2)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Problem list (interdisciplinary, problem-oriented).</summary>
    [Id(3)] public List<CarePlanProblem> Problems { get; set; } = new();

    [Id(4)] public string EstablishedById { get; set; } = string.Empty;
    [Id(5)] public string EstablishedByName { get; set; } = string.Empty;
    [Id(6)] public DateTime EstablishedDate { get; set; }
    [Id(7)] public DateTime? LastReviewDate { get; set; }
    [Id(8)] public DateTime? NextReviewDue { get; set; }

    [Id(9)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(10)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── Reserved (Phase 2 / CMS-485 + certification) ─────────────────────────
    /// <summary>Reserved (Phase 2): physician/allowed-practitioner who certified the plan.</summary>
    [Id(11)] public string CertifyingProviderId { get; set; } = string.Empty;
    /// <summary>Reserved (Phase 2): certification date.</summary>
    [Id(12)] public DateTime? CertificationDate { get; set; }
    /// <summary>Reserved (Phase 2): the certification period this plan covers.</summary>
    [Id(13)] public DateTime? CertificationPeriodStart { get; set; }
    [Id(14)] public DateTime? CertificationPeriodEnd { get; set; }
    /// <summary>Reserved (Phase 2): face-to-face encounter date supporting certification.</summary>
    [Id(15)] public DateTime? FaceToFaceEncounterDate { get; set; }
    /// <summary>Reserved (Phase 2): true when this plan is a recertification.</summary>
    [Id(16)] public bool IsRecertification { get; set; }
    /// <summary>Reserved (Phase 2): CMS-485 orders / treatment content.</summary>
    [Id(17)] public string OrdersText { get; set; } = string.Empty;
    /// <summary>Reserved (Phase 2): link to the physician's certification signature (TIU).</summary>
    [Id(18)] public string PhysicianSignatureId { get; set; } = string.Empty;
}
