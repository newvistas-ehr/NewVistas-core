// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Visual field status by eye (VistA File #783 field .08).</summary>
[GenerateSerializer]
public enum VisualField
{
    /// <summary>Normal (greater than 120 degrees).</summary>
    Normal = 0,
    /// <summary>Moderate constriction (20-120 degrees).</summary>
    ModerateConstriction = 1,
    /// <summary>Severe constriction (less than 20 degrees — meets legal blindness criterion).</summary>
    SevereConstriction = 2,
    /// <summary>No measurable field.</summary>
    NoField = 3,
    /// <summary>Not tested.</summary>
    NotTested = 4
}

/// <summary>Onset type for the visual impairment (VistA File #782 field .07).</summary>
[GenerateSerializer]
public enum BROnsetType
{
    Unknown = 0,
    Congenital = 1,
    Acquired = 2,
    Progressive = 3
}

/// <summary>Patient eligibility status for blind rehabilitation services (VistA File #782 field .08).</summary>
[GenerateSerializer]
public enum BREligibilityStatus
{
    Unknown = 0,
    /// <summary>Legally blind (20/200 or worse best-corrected, or visual field &lt; 20°).</summary>
    LegallyBlind = 1,
    /// <summary>Severe visual impairment but not meeting legal blindness criteria.</summary>
    SevereImpairment = 2,
    /// <summary>Service-connected visual impairment.</summary>
    ServiceConnected = 3,
    /// <summary>Eligibility pending further evaluation.</summary>
    Pending = 4,
    /// <summary>Not eligible for BR services.</summary>
    NotEligible = 5
}

/// <summary>Blind rehabilitation training program area (VistA File #782 field .10).</summary>
[GenerateSerializer]
public enum BRTrainingArea
{
    /// <summary>Orientation and Mobility (cane travel, wayfinding).</summary>
    OrientationAndMobility = 0,
    /// <summary>Activities of Daily Living (cooking, grooming, home management).</summary>
    ActivitiesOfDailyLiving = 1,
    /// <summary>Low Vision / optical devices (magnifiers, telescopes).</summary>
    LowVision = 2,
    /// <summary>Manual skills (braille, tactile crafts).</summary>
    ManualSkills = 3,
    /// <summary>Computer Access Technology (screen readers, JAWS, NVDA).</summary>
    ComputerAccessTechnology = 4,
    /// <summary>Communication skills (writing guides, talking devices).</summary>
    CommunicationSkills = 5,
    /// <summary>Recreational therapy.</summary>
    RecreationalTherapy = 6,
    /// <summary>Guide dog training.</summary>
    GuideDog = 7,
    /// <summary>Visual Skills Training (eccentric viewing, scanning).</summary>
    VisualSkillsTraining = 8
}

// ─── Supporting Records ───────────────────────────────────────────────────────

/// <summary>An assistive device or adaptive equipment item issued to the patient.</summary>
[GenerateSerializer]
public class BRDeviceEntry
{
    /// <summary>Name of the device or equipment item (.01).</summary>
    [Id(0)]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Category of the device (e.g., Mobility, Low Vision, Technology).</summary>
    [Id(1)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Serial number or identifier of the specific item.</summary>
    [Id(2)]
    public string? SerialNumber { get; set; }

    /// <summary>Date the device was issued to the patient.</summary>
    [Id(3)]
    public DateTime IssuedDate { get; set; }

    /// <summary>Name of the provider or specialist who issued the device.</summary>
    [Id(4)]
    public string IssuedBy { get; set; } = string.Empty;

    /// <summary>Whether the device was returned or is still held by the patient.</summary>
    [Id(5)]
    public bool Returned { get; set; }

    /// <summary>Date the device was returned (if applicable).</summary>
    [Id(6)]
    public DateTime? ReturnedDate { get; set; }

    /// <summary>Additional notes about the device or its use.</summary>
    [Id(7)]
    public string? Notes { get; set; }
}

/// <summary>A training goal recorded for the blind rehabilitation patient.</summary>
[GenerateSerializer]
public class BRTrainingGoalEntry
{
    /// <summary>Description of the training goal.</summary>
    [Id(0)]
    public string Goal { get; set; } = string.Empty;

    /// <summary>Training area this goal belongs to.</summary>
    [Id(1)]
    public BRTrainingArea Area { get; set; }

    /// <summary>Date the goal was recorded.</summary>
    [Id(2)]
    public DateTime RecordedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Whether this goal has been achieved.</summary>
    [Id(3)]
    public bool Achieved { get; set; }

    /// <summary>Date the goal was achieved (if applicable).</summary>
    [Id(4)]
    public DateTime? AchievedDate { get; set; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// Blind Rehabilitation Patient State — the patient's BR master record.
/// Maps to VistA BLIND REHABILITATION PATIENT file (#782) and VISUAL ACUITY file (#783).
/// </summary>
[GenerateSerializer]
public class BRPatientState
{
    /// <summary>Patient identifier (.01).</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient eligibility status for BR services (.08).</summary>
    [Id(1)]
    public BREligibilityStatus EligibilityStatus { get; set; } = BREligibilityStatus.Unknown;

    /// <summary>Reason for eligibility determination or denial.</summary>
    [Id(2)]
    public string? EligibilityReason { get; set; }

    // ─── Visual Acuity (File #783) ───────────────────────────────────────────

    /// <summary>Right eye distance visual acuity in Snellen notation (e.g., "20/200") (.04).</summary>
    [Id(3)]
    public string RightEyeDistance { get; set; } = string.Empty;

    /// <summary>Left eye distance visual acuity in Snellen notation (.05).</summary>
    [Id(4)]
    public string LeftEyeDistance { get; set; } = string.Empty;

    /// <summary>Best corrected visual acuity — right eye (.06).</summary>
    [Id(5)]
    public string BestCorrectedRight { get; set; } = string.Empty;

    /// <summary>Best corrected visual acuity — left eye (.07).</summary>
    [Id(6)]
    public string BestCorrectedLeft { get; set; } = string.Empty;

    /// <summary>Visual field status — right eye (.08).</summary>
    [Id(7)]
    public VisualField VisualFieldRight { get; set; } = VisualField.NotTested;

    /// <summary>Visual field status — left eye (.09).</summary>
    [Id(8)]
    public VisualField VisualFieldLeft { get; set; } = VisualField.NotTested;

    /// <summary>Contrast sensitivity result (free text) (.10).</summary>
    [Id(9)]
    public string? ContrastSensitivity { get; set; }

    /// <summary>Date of most recent visual acuity exam (.11).</summary>
    [Id(10)]
    public DateTime? LastExamDate { get; set; }

    /// <summary>Identifier of examiner who performed the visual acuity assessment (.12).</summary>
    [Id(11)]
    public string? ExaminerId { get; set; }

    /// <summary>Name of examiner who performed the visual acuity assessment (.13).</summary>
    [Id(12)]
    public string? ExaminerName { get; set; }

    /// <summary>Visual acuity exam notes.</summary>
    [Id(13)]
    public string? AcuityNotes { get; set; }

    // ─── Diagnosis (.06/.07) ─────────────────────────────────────────────────

    /// <summary>Primary visual diagnosis (e.g., Macular Degeneration, Glaucoma) (.06).</summary>
    [Id(14)]
    public string PrimaryDiagnosis { get; set; } = string.Empty;

    /// <summary>Secondary visual diagnosis (.07).</summary>
    [Id(15)]
    public string? SecondaryDiagnosis { get; set; }

    /// <summary>Onset type: congenital, acquired, or progressive.</summary>
    [Id(16)]
    public BROnsetType OnsetType { get; set; } = BROnsetType.Unknown;

    /// <summary>Date or approximate date of onset of visual impairment.</summary>
    [Id(17)]
    public DateTime? OnsetDate { get; set; }

    /// <summary>Whether the visual impairment is service-connected.</summary>
    [Id(18)]
    public bool ServiceConnected { get; set; }

    /// <summary>Service-connected disability percentage (0–100).</summary>
    [Id(19)]
    public int? ServiceConnectedPercentage { get; set; }

    /// <summary>ICD-10 code for the primary visual diagnosis.</summary>
    [Id(20)]
    public string? Icd10Code { get; set; }

    /// <summary>Diagnosis notes.</summary>
    [Id(21)]
    public string? DiagnosisNotes { get; set; }

    // ─── Devices &amp; Equipment ────────────────────────────────────────────────────

    /// <summary>List of assistive devices and adaptive equipment issued to the patient.</summary>
    [Id(22)]
    public List<BRDeviceEntry> Devices { get; set; } = new();

    // ─── Training Goals ──────────────────────────────────────────────────────

    /// <summary>List of training goals for the patient.</summary>
    [Id(23)]
    public List<BRTrainingGoalEntry> TrainingGoals { get; set; } = new();

    // ─── Audit ───────────────────────────────────────────────────────────────

    /// <summary>Date the BR patient record was created.</summary>
    [Id(24)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the BR patient record was last modified.</summary>
    [Id(25)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
