// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── NICU depth (Phase 2) — respiratory support, phototherapy, problems, nutrition, procedures ──

/// <summary>Level of neonatal respiratory support (escalating).</summary>
public enum RespiratorySupportType
{
    RoomAir = 0,
    NasalCannula = 1,
    HighFlowNasalCannula = 2,
    Cpap = 3,                   // continuous positive airway pressure
    Nippv = 4,                  // non-invasive positive pressure
    ConventionalVentilation = 5,
    Hfov = 6,                   // high-frequency oscillatory ventilation
    Ecmo = 7
}

/// <summary>Phototherapy intensity for hyperbilirubinemia.</summary>
public enum PhototherapyIntensity
{
    Single = 0,
    Double = 1,
    Triple = 2,
    Intensive = 3
}

/// <summary>Status of a neonatal problem.</summary>
public enum NeonatalProblemStatus
{
    Active = 0,
    Resolved = 1
}

/// <summary>Route of neonatal nutrition / fluids.</summary>
public enum NeonatalNutritionRoute
{
    Npo = 0,
    IvFluids = 1,
    Tpn = 2,            // total parenteral nutrition
    EnteralGavage = 3,  // tube feeds
    EnteralOral = 4,    // PO / breast / bottle
    Mixed = 5
}

/// <summary>A NICU bedside procedure.</summary>
public enum NeonatalProcedureType
{
    Intubation = 0,
    SurfactantAdministration = 1,
    UmbilicalVenousCatheter = 2,
    UmbilicalArterialCatheter = 3,
    PiccLine = 4,
    LumbarPuncture = 5,
    ExchangeTransfusion = 6,
    BloodTransfusion = 7,
    Other = 8
}

/// <summary>A respiratory-support state change on the timeline (FiO2 / settings, with optional end).</summary>
[GenerateSerializer]
public class RespiratorySupportEntry
{
    [Id(0)] public DateTime RecordedAt { get; set; }
    [Id(1)] public RespiratorySupportType SupportType { get; set; }
    /// <summary>FiO2 as a percent (21–100).</summary>
    [Id(2)] public int? FiO2Percent { get; set; }
    /// <summary>Settings free-text, e.g. "CPAP +6, RR 30" or "HFNC 4 L/min".</summary>
    [Id(3)] public string Settings { get; set; } = string.Empty;
    [Id(4)] public DateTime? EndedAt { get; set; }
    [Id(5)] public string Notes { get; set; } = string.Empty;
}

/// <summary>A phototherapy episode for hyperbilirubinemia.</summary>
[GenerateSerializer]
public class PhototherapyEntry
{
    [Id(0)] public DateTime StartedAt { get; set; }
    [Id(1)] public PhototherapyIntensity Intensity { get; set; }
    [Id(2)] public string Indication { get; set; } = string.Empty;
    [Id(3)] public decimal? BilirubinAtStartMgDl { get; set; }
    [Id(4)] public DateTime? EndedAt { get; set; }
    [Id(5)] public string Notes { get; set; } = string.Empty;
}

/// <summary>A neonatal problem / diagnosis (RDS, sepsis, jaundice, IVH, NEC, hypoglycemia, …).</summary>
[GenerateSerializer]
public class NeonatalProblemEntry
{
    [Id(0)] public string ProblemId { get; set; } = string.Empty;
    [Id(1)] public string Problem { get; set; } = string.Empty;
    [Id(2)] public string Icd10Code { get; set; } = string.Empty;
    [Id(3)] public DateTime? OnsetDate { get; set; }
    [Id(4)] public NeonatalProblemStatus Status { get; set; } = NeonatalProblemStatus.Active;
    [Id(5)] public string Notes { get; set; } = string.Empty;
}

/// <summary>A neonatal nutrition / fluid record.</summary>
[GenerateSerializer]
public class NeonatalNutritionEntry
{
    [Id(0)] public DateTime RecordedAt { get; set; }
    [Id(1)] public NeonatalNutritionRoute Route { get; set; }
    /// <summary>Total fluid intake target (mL/kg/day).</summary>
    [Id(2)] public int? TotalFluidMlPerKgPerDay { get; set; }
    /// <summary>Composition/detail, e.g. "TPN: dextrose 12.5%, AA 3.5 g/kg, lipids 3 g/kg" or "EBM 20 mL q3h gavage".</summary>
    [Id(3)] public string Detail { get; set; } = string.Empty;
    [Id(4)] public string Notes { get; set; } = string.Empty;
}

/// <summary>A NICU bedside procedure record.</summary>
[GenerateSerializer]
public class NeonatalProcedureEntry
{
    [Id(0)] public string ProcedureId { get; set; } = string.Empty;
    [Id(1)] public NeonatalProcedureType ProcedureType { get; set; }
    [Id(2)] public DateTime PerformedAt { get; set; }
    [Id(3)] public string PerformedBy { get; set; } = string.Empty;
    [Id(4)] public string Notes { get; set; } = string.Empty;
}
