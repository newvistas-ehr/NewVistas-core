// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single remote patient monitoring measurement (blood pressure, weight, glucose, etc.).
/// Based on VistA HOME TELEHEALTH MEASUREMENT file (#720.5).
/// </summary>
[GenerateSerializer]
public class HomeTelehealthReadingState
{
    /// <summary>
    /// Unique reading identifier (.01).
    /// </summary>
    [Id(0)]
    public string ReadingId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier (.02) — pointer to PATIENT file (#2).
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Type of measurement recorded (.03).
    /// </summary>
    [Id(2)]
    public HtMeasurementType MeasurementType { get; set; }

    /// <summary>
    /// Primary measurement value (.04).
    /// Systolic BP, weight (lbs), glucose (mg/dL), SpO2 (%), HR (bpm), temp (°F), peak flow (L/min), pain (0–10).
    /// </summary>
    [Id(3)]
    public decimal? Value1 { get; set; }

    /// <summary>
    /// Secondary measurement value (.05).
    /// Diastolic BP, or pulse rate from pulse oximeter.
    /// </summary>
    [Id(4)]
    public decimal? Value2 { get; set; }

    /// <summary>
    /// Unit of measure for Value1 (.06) (e.g., "mmHg", "lbs", "mg/dL", "%", "bpm", "°F", "L/min").
    /// </summary>
    [Id(5)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Date and time the reading was taken (.07).
    /// </summary>
    [Id(6)]
    public DateTime ReadingDateTime { get; set; }

    /// <summary>
    /// How the reading was entered (.08).
    /// </summary>
    [Id(7)]
    public HtReadingSource Source { get; set; }

    /// <summary>
    /// Device ID used to take this reading (.09).
    /// </summary>
    [Id(8)]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Clinical notes about this reading (.10).
    /// </summary>
    [Id(9)]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this reading triggered an alert (.11).
    /// </summary>
    [Id(10)]
    public bool AlertGenerated { get; set; }

    /// <summary>
    /// Alert grain key if an alert was generated (.12).
    /// </summary>
    [Id(11)]
    public string? AlertId { get; set; }

    /// <summary>
    /// Whether a clinician has reviewed this reading (.13).
    /// </summary>
    [Id(12)]
    public bool IsReviewed { get; set; }

    /// <summary>
    /// ID of the clinician who reviewed this reading (.14).
    /// </summary>
    [Id(13)]
    public string? ReviewedById { get; set; }

    /// <summary>
    /// Name of the clinician who reviewed this reading (.15).
    /// </summary>
    [Id(14)]
    public string? ReviewedByName { get; set; }

    /// <summary>
    /// Date/time the reading was reviewed (.16).
    /// </summary>
    [Id(15)]
    public DateTime? ReviewedDate { get; set; }

    /// <summary>
    /// Date the reading record was created.
    /// </summary>
    [Id(16)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date the reading record was last modified.
    /// </summary>
    [Id(17)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of physiological measurement taken remotely.
/// </summary>
[GenerateSerializer]
public enum HtMeasurementType
{
    BloodPressure = 0,
    Weight = 1,
    BloodGlucose = 2,
    PulseOximetry = 3,
    HeartRate = 4,
    Temperature = 5,
    PeakFlow = 6,
    PainScale = 7
}

/// <summary>
/// Origin of a telehealth reading.
/// </summary>
[GenerateSerializer]
public enum HtReadingSource
{
    PatientEntry = 0,
    DeviceTransmission = 1,
    ClinicalStaff = 2,
    Telehealth = 3
}

/// <summary>
/// Summary entry stored in the patient reading index.
/// </summary>
[GenerateSerializer]
public class HtReadingIndexEntry
{
    /// <summary>Reading grain key.</summary>
    [Id(0)]
    public string ReadingId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Type of measurement.</summary>
    [Id(2)]
    public HtMeasurementType MeasurementType { get; set; }

    /// <summary>Primary measurement value.</summary>
    [Id(3)]
    public decimal? Value1 { get; set; }

    /// <summary>Secondary measurement value (e.g. diastolic).</summary>
    [Id(4)]
    public decimal? Value2 { get; set; }

    /// <summary>Unit of measure.</summary>
    [Id(5)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>Date and time the reading was taken.</summary>
    [Id(6)]
    public DateTime ReadingDateTime { get; set; }

    /// <summary>Whether this reading triggered an alert.</summary>
    [Id(7)]
    public bool AlertGenerated { get; set; }

    /// <summary>Whether a clinician has reviewed this reading.</summary>
    [Id(8)]
    public bool IsReviewed { get; set; }

    /// <summary>Reading entry source.</summary>
    [Id(9)]
    public HtReadingSource Source { get; set; }
}

/// <summary>
/// Index of all readings for a single patient.
/// Grain key: "HT-READING-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class HomeTelehealthReadingIndexState
{
    /// <summary>
    /// All reading summaries for this patient, ordered by ReadingDateTime descending.
    /// </summary>
    [Id(0)]
    public List<HtReadingIndexEntry> Entries { get; set; } = new();
}
