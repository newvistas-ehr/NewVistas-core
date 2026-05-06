// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Nursing Assessment — VistA File #212.1 (NURSING ASSESSMENT sub-record of NURSING PATIENT).
/// Head-to-toe assessment performed at admission, each shift, or on demand.
/// </summary>
[GenerateSerializer]
public class NursingAssessmentState
{
    /// <summary>Unique assessment ID (GUID).</summary>
    [Id(0)] public string AssessmentId { get; set; } = string.Empty;

    /// <summary>Patient ID (key of this patient's workflow grain).</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Date and time the assessment was performed.</summary>
    [Id(2)] public DateTime AssessmentDateTime { get; set; }

    /// <summary>Type: Initial, Shift, Focused, Discharge, PRN.</summary>
    [Id(3)] public string AssessmentType { get; set; } = string.Empty;

    /// <summary>Performing nurse identifier.</summary>
    [Id(4)] public string NurseId { get; set; } = string.Empty;

    /// <summary>Performing nurse display name.</summary>
    [Id(5)] public string NurseName { get; set; } = string.Empty;

    [Id(6)] public string? LocationId { get; set; }
    [Id(7)] public string? LocationName { get; set; }

    /// <summary>Document workflow status: Draft, Signed, Amended.</summary>
    [Id(8)] public NursingAssessmentStatus Status { get; set; } = NursingAssessmentStatus.Draft;

    [Id(9)]  public string? SignedById { get; set; }
    [Id(10)] public string? SignedByName { get; set; }
    [Id(11)] public DateTime? SignedDateTime { get; set; }

    // ─── Neurological ─────────────────────────────────────────────────────────

    /// <summary>Level of consciousness: Alert, Verbal, Pain, Unresponsive (AVPU scale).</summary>
    [Id(12)] public string? LevelOfConsciousness { get; set; }

    /// <summary>Orientation domains intact: Person, Place, Time, Situation.</summary>
    [Id(13)] public List<string> Orientation { get; set; } = new();

    /// <summary>Pupil response (e.g., PERRL, Anisocoria, Fixed/Dilated).</summary>
    [Id(14)] public string? PupilResponse { get; set; }

    [Id(15)] public string? NeurologicalNotes { get; set; }

    // ─── Respiratory ──────────────────────────────────────────────────────────

    /// <summary>Respiratory rate (breaths/min).</summary>
    [Id(16)] public int? RespiratoryRate { get; set; }

    /// <summary>Breath sounds: Clear, Diminished, Crackles, Wheezes, Rhonchi, Absent.</summary>
    [Id(17)] public string? BreathSounds { get; set; }

    /// <summary>Oxygen delivery device: RoomAir, NasalCannula, SimpleMask, NonRebreather, Vent, HFNC.</summary>
    [Id(18)] public string? OxygenTherapy { get; set; }

    /// <summary>Supplemental oxygen flow rate (L/min).</summary>
    [Id(19)] public decimal? OxygenLitersPerMinute { get; set; }

    /// <summary>Pulse oximetry SpO2 (%).</summary>
    [Id(20)] public decimal? SpO2 { get; set; }

    [Id(21)] public string? RespiratoryNotes { get; set; }

    // ─── Cardiovascular ───────────────────────────────────────────────────────

    /// <summary>Heart rate (bpm).</summary>
    [Id(22)] public int? HeartRate { get; set; }

    /// <summary>Cardiac rhythm: Regular, Irregular, Paced.</summary>
    [Id(23)] public string? HeartRhythm { get; set; }

    /// <summary>Heart sounds: Normal S1/S2, S3, S4, Murmur.</summary>
    [Id(24)] public string? HeartSounds { get; set; }

    /// <summary>Peripheral pulses description (e.g., 2+ bilateral).</summary>
    [Id(25)] public string? PeripheralPulses { get; set; }

    /// <summary>Pitting edema scale: None, 1+, 2+, 3+, 4+.</summary>
    [Id(26)] public string? Edema { get; set; }

    [Id(27)] public string? CardiovascularNotes { get; set; }

    // ─── Skin / Wound ─────────────────────────────────────────────────────────

    /// <summary>Skin integrity: Intact, Impaired.</summary>
    [Id(28)] public string? SkinIntegrity { get; set; }

    /// <summary>Skin color: Normal, Pale, Jaundiced, Cyanotic, Flushed, Mottled.</summary>
    [Id(29)] public string? SkinColor { get; set; }

    /// <summary>Braden Scale score (6–23): pressure injury risk (≤18 = at risk).</summary>
    [Id(30)] public int? BradenScore { get; set; }

    /// <summary>Count of wounds or pressure injuries currently present.</summary>
    [Id(31)] public int? WoundCount { get; set; }

    [Id(32)] public string? SkinNotes { get; set; }

    // ─── Pain ─────────────────────────────────────────────────────────────────

    /// <summary>Pain intensity (0–10 numeric rating scale).</summary>
    [Id(33)] public int? PainScore { get; set; }

    [Id(34)] public string? PainLocation { get; set; }

    /// <summary>Quality: Sharp, Dull, Burning, Throbbing, Pressure, Aching.</summary>
    [Id(35)] public string? PainDescription { get; set; }

    [Id(36)] public string? PainInterventions { get; set; }

    // ─── GI / Nutrition ───────────────────────────────────────────────────────

    /// <summary>Abdominal assessment: Soft, Distended, Rigid, Tender.</summary>
    [Id(37)] public string? AbdominalAssessment { get; set; }

    /// <summary>Bowel sounds: Active, Hyperactive, Hypoactive, Absent.</summary>
    [Id(38)] public string? BowelSounds { get; set; }

    [Id(39)] public DateTime? LastBowelMovement { get; set; }

    /// <summary>Ordered diet (e.g., Regular, Cardiac, Renal, Soft, NPO).</summary>
    [Id(40)] public string? DietType { get; set; }

    /// <summary>Appetite: Good, Fair, Poor, NPO.</summary>
    [Id(41)] public string? AppetiteAssessment { get; set; }

    [Id(42)] public string? GiNotes { get; set; }

    // ─── GU / Urinary ─────────────────────────────────────────────────────────

    /// <summary>Measured urine output this shift (mL).</summary>
    [Id(43)] public decimal? UrineOutput { get; set; }

    /// <summary>Urine characteristics: Clear, Cloudy, Amber, Hematuria, Dark.</summary>
    [Id(44)] public string? UrineCharacteristics { get; set; }

    /// <summary>Foley (indwelling urinary catheter) present.</summary>
    [Id(45)] public bool HasFoley { get; set; }

    [Id(46)] public DateTime? FoleyInsertedDate { get; set; }
    [Id(47)] public string? GuNotes { get; set; }

    // ─── Psychosocial ─────────────────────────────────────────────────────────

    /// <summary>Anxiety level: None, Mild, Moderate, Severe, Panic.</summary>
    [Id(48)] public string? AnxietyLevel { get; set; }

    /// <summary>Mood/affect: Calm, Anxious, Agitated, Depressed, Flat, Euphoric.</summary>
    [Id(49)] public string? Mood { get; set; }

    [Id(50)] public string? PsychosocialNotes { get; set; }

    // ─── Falls Risk ───────────────────────────────────────────────────────────

    /// <summary>Morse Fall Scale total (0–125): 0-24=Low, 25-50=Moderate, ≥51=High.</summary>
    [Id(51)] public int? MorseScoreTotal { get; set; }

    /// <summary>Derived risk tier: Low, Moderate, High.</summary>
    [Id(52)] public string? FallRiskLevel { get; set; }

    /// <summary>Active fall-prevention precautions in place.</summary>
    [Id(53)] public List<string> FallPrecautions { get; set; } = new();

    // ─── Functional Status / ADLs ─────────────────────────────────────────────

    /// <summary>Mobility: Independent, AssistedDevice, Assist1, Assist2, Dependent, Bedrest.</summary>
    [Id(54)] public string? AdlMobility { get; set; }

    /// <summary>Bathing/hygiene: Independent, Assist, Dependent.</summary>
    [Id(55)] public string? AdlBathing { get; set; }

    /// <summary>Dressing: Independent, Assist, Dependent.</summary>
    [Id(56)] public string? AdlDressing { get; set; }

    /// <summary>Toileting: Independent, Assist, Dependent, Incontinent.</summary>
    [Id(57)] public string? AdlToileting { get; set; }

    // ─── Discharge Planning ────────────────────────────────────────────────────

    [Id(58)] public DateTime? AnticipatedDischargeDate { get; set; }
    [Id(59)] public string? DischargeDisposition { get; set; }
    [Id(60)] public string? DischargeNotes { get; set; }

    // ─── General ──────────────────────────────────────────────────────────────

    /// <summary>Free-text narrative nursing notes.</summary>
    [Id(61)] public string? NarrativeNotes { get; set; }

    [Id(62)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
