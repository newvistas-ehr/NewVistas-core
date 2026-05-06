// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ═══════════════════════════════════════════════════════════════════════════════
// §170.315(e)(3) — Patient Health Information Capture
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// State for a patient-submitted health information packet.
/// §170.315(e)(3) — Patient Health Information Capture.
///
/// Patients submit demographics, health concerns, medications, allergies,
/// social/family history, advance directives, and health goals via the portal.
/// Clinicians review and selectively accept items into the medical record.
///
/// Lifecycle: submitted → under-review → accepted / rejected / partial
///
/// Grain Key: "PATIENT-SUB:{submissionId}"
/// </summary>
[GenerateSerializer]
public class PatientSubmissionState
{
    /// <summary>Unique submission identifier.</summary>
    [Id(0)]
    public string SubmissionId { get; set; } = string.Empty;

    /// <summary>Patient who submitted.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (denormalized).</summary>
    [Id(2)]
    public string? PatientName { get; set; }

    /// <summary>When the submission was created.</summary>
    [Id(3)]
    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Status: "submitted", "under-review", "accepted", "rejected", "partial".</summary>
    [Id(4)]
    public string Status { get; set; } = "submitted";

    // ─── Patient-Submitted Data ───────────────────────────────────────────────

    /// <summary>Self-reported demographic updates (address, phone, email, emergency contact).</summary>
    [Id(5)]
    public PatientSubmittedDemographics? Demographics { get; set; }

    /// <summary>Self-reported health concerns / symptoms.</summary>
    [Id(6)]
    public List<PatientSubmittedHealthConcern> HealthConcerns { get; set; } = new();

    /// <summary>Self-reported medications (OTC, supplements).</summary>
    [Id(7)]
    public List<PatientSubmittedMedication> Medications { get; set; } = new();

    /// <summary>Self-reported allergies.</summary>
    [Id(8)]
    public List<PatientSubmittedAllergy> Allergies { get; set; } = new();

    /// <summary>Smoking status and social history.</summary>
    [Id(9)]
    public PatientSubmittedSocialHistory? SocialHistory { get; set; }

    /// <summary>Family history entries.</summary>
    [Id(10)]
    public List<PatientSubmittedFamilyHistory> FamilyHistory { get; set; } = new();

    /// <summary>Advance directive information.</summary>
    [Id(11)]
    public PatientSubmittedAdvanceDirective? AdvanceDirective { get; set; }

    /// <summary>Patient health goals.</summary>
    [Id(12)]
    public List<string> HealthGoals { get; set; } = new();

    /// <summary>Free-text notes from the patient.</summary>
    [Id(13)]
    public string? PatientNotes { get; set; }

    // ─── Review ───────────────────────────────────────────────────────────────

    /// <summary>Clinician who reviewed the submission.</summary>
    [Id(14)]
    public string? ReviewedBy { get; set; }

    /// <summary>When the review was completed.</summary>
    [Id(15)]
    public DateTime? ReviewedDate { get; set; }

    /// <summary>Clinician's review notes.</summary>
    [Id(16)]
    public string? ReviewNotes { get; set; }

    /// <summary>Which sections were accepted (e.g., "Demographics", "Allergies").</summary>
    [Id(17)]
    public List<string> AcceptedSections { get; set; } = new();

    /// <summary>Which sections were rejected with reasons.</summary>
    [Id(18)]
    public List<string> RejectedSections { get; set; } = new();

    [Id(19)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Patient-submitted demographic updates.</summary>
[GenerateSerializer]
public class PatientSubmittedDemographics
{
    [Id(0)] public string? StreetAddress { get; set; }
    [Id(1)] public string? City { get; set; }
    [Id(2)] public string? State { get; set; }
    [Id(3)] public string? ZipCode { get; set; }
    [Id(4)] public string? PhoneHome { get; set; }
    [Id(5)] public string? PhoneCell { get; set; }
    [Id(6)] public string? Email { get; set; }
    [Id(7)] public string? EmergencyContactName { get; set; }
    [Id(8)] public string? EmergencyContactRelationship { get; set; }
    [Id(9)] public string? EmergencyContactPhone { get; set; }
    [Id(10)] public string? PreferredLanguage { get; set; }
}

/// <summary>Patient-reported health concern or symptom.</summary>
[GenerateSerializer]
public class PatientSubmittedHealthConcern
{
    [Id(0)] public string Description { get; set; } = string.Empty;
    [Id(1)] public string? Severity { get; set; }
    [Id(2)] public DateTime? OnsetDate { get; set; }
    [Id(3)] public bool IsOngoing { get; set; }
}

/// <summary>Patient-reported medication (OTC, supplement, etc.).</summary>
[GenerateSerializer]
public class PatientSubmittedMedication
{
    [Id(0)] public string MedicationName { get; set; } = string.Empty;
    [Id(1)] public string? Dosage { get; set; }
    [Id(2)] public string? Frequency { get; set; }
    [Id(3)] public string? Reason { get; set; }
    [Id(4)] public bool IsCurrentlyTaking { get; set; } = true;
}

/// <summary>Patient-reported allergy.</summary>
[GenerateSerializer]
public class PatientSubmittedAllergy
{
    [Id(0)] public string Allergen { get; set; } = string.Empty;
    [Id(1)] public string? ReactionDescription { get; set; }
    [Id(2)] public string? Severity { get; set; }
    [Id(3)] public string AllergenType { get; set; } = "Drug";
}

/// <summary>Patient-reported social history.</summary>
[GenerateSerializer]
public class PatientSubmittedSocialHistory
{
    /// <summary>Smoking status: "current", "former", "never".</summary>
    [Id(0)] public string? SmokingStatus { get; set; }
    [Id(1)] public string? SmokingDetails { get; set; }
    [Id(2)] public string? AlcoholUse { get; set; }
    [Id(3)] public string? ExerciseFrequency { get; set; }
    [Id(4)] public string? Occupation { get; set; }
}

/// <summary>Patient-reported family history entry.</summary>
[GenerateSerializer]
public class PatientSubmittedFamilyHistory
{
    [Id(0)] public string Relationship { get; set; } = string.Empty;
    [Id(1)] public string Condition { get; set; } = string.Empty;
    [Id(2)] public string? AgeAtOnset { get; set; }
    [Id(3)] public bool IsDeceased { get; set; }
}

/// <summary>Patient-reported advance directive info.</summary>
[GenerateSerializer]
public class PatientSubmittedAdvanceDirective
{
    [Id(0)] public bool HasLivingWill { get; set; }
    [Id(1)] public bool HasHealthcarePowerOfAttorney { get; set; }
    [Id(2)] public string? HealthcareProxyName { get; set; }
    [Id(3)] public string? HealthcareProxyPhone { get; set; }
    [Id(4)] public string? AdditionalDirectives { get; set; }
}

/// <summary>Summary entry for submission index.</summary>
[GenerateSerializer]
public class PatientSubmissionSummary
{
    [Id(0)] public string SubmissionId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string? PatientName { get; set; }
    [Id(3)] public DateTime SubmittedDate { get; set; }
    [Id(4)] public string Status { get; set; } = string.Empty;
    [Id(5)] public int SectionCount { get; set; }
}

/// <summary>
/// Index state for patient submissions.
/// Grain Key: "PATIENT-SUB-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class PatientSubmissionIndexState
{
    [Id(0)]
    public List<PatientSubmissionSummary> Submissions { get; set; } = new();
}

/// <summary>
/// System-wide submission review queue index.
/// Grain Key: "PATIENT-SUB-QUEUE"
/// </summary>
[GenerateSerializer]
public class PatientSubmissionQueueState
{
    [Id(0)]
    public List<PatientSubmissionSummary> Submissions { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════════
// §170.315(e)(2) — Secure Messaging
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// State for a secure message thread between a patient and care team.
/// §170.315(e)(2) — Secure Messaging.
///
/// Bidirectional messaging: patient can send to care team, care team can reply.
/// Each thread is tied to a patient and contains ordered messages.
///
/// Grain Key: "SECURE-MSG-THREAD:{threadId}"
/// </summary>
[GenerateSerializer]
public class SecureMessageThreadState
{
    /// <summary>Unique thread identifier.</summary>
    [Id(0)]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Patient this thread belongs to.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (denormalized).</summary>
    [Id(2)]
    public string? PatientName { get; set; }

    /// <summary>Thread subject line.</summary>
    [Id(3)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Category: "general", "medication", "appointment", "lab-results", "referral", "billing".</summary>
    [Id(4)]
    public string Category { get; set; } = "general";

    /// <summary>Status: "open", "closed", "archived".</summary>
    [Id(5)]
    public string Status { get; set; } = "open";

    /// <summary>Assigned care team member (provider).</summary>
    [Id(6)]
    public string? AssignedProviderId { get; set; }

    /// <summary>Assigned provider name.</summary>
    [Id(7)]
    public string? AssignedProviderName { get; set; }

    /// <summary>Ordered list of messages in this thread.</summary>
    [Id(8)]
    public List<SecureMessage> Messages { get; set; } = new();

    /// <summary>When the thread was created.</summary>
    [Id(9)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>When the last message was added.</summary>
    [Id(10)]
    public DateTime LastMessageDate { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the thread has unread messages for the patient.</summary>
    [Id(11)]
    public bool HasUnreadPatient { get; set; }

    /// <summary>Whether the thread has unread messages for the care team.</summary>
    [Id(12)]
    public bool HasUnreadProvider { get; set; }

    [Id(13)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>A single message within a secure thread.</summary>
[GenerateSerializer]
public class SecureMessage
{
    [Id(0)] public string MessageId { get; set; } = string.Empty;
    /// <summary>Sender type: "patient" or "provider".</summary>
    [Id(1)] public string SenderType { get; set; } = string.Empty;
    [Id(2)] public string? SenderId { get; set; }
    [Id(3)] public string? SenderName { get; set; }
    [Id(4)] public string Body { get; set; } = string.Empty;
    [Id(5)] public DateTime SentDate { get; set; } = DateTime.UtcNow;
    [Id(6)] public bool IsRead { get; set; }
}

/// <summary>Summary entry for thread listing.</summary>
[GenerateSerializer]
public class SecureMessageThreadSummary
{
    [Id(0)] public string ThreadId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string? PatientName { get; set; }
    [Id(3)] public string Subject { get; set; } = string.Empty;
    [Id(4)] public string Category { get; set; } = string.Empty;
    [Id(5)] public string Status { get; set; } = string.Empty;
    [Id(6)] public DateTime LastMessageDate { get; set; }
    [Id(7)] public int MessageCount { get; set; }
    [Id(8)] public bool HasUnreadPatient { get; set; }
    [Id(9)] public bool HasUnreadProvider { get; set; }
}

/// <summary>
/// Index state for secure message threads (per-patient).
/// Grain Key: "SECURE-MSG-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class SecureMessageIndexState
{
    [Id(0)]
    public List<SecureMessageThreadSummary> Threads { get; set; } = new();
}

/// <summary>
/// System-wide secure message queue for providers.
/// Grain Key: "SECURE-MSG-QUEUE"
/// </summary>
[GenerateSerializer]
public class SecureMessageQueueState
{
    [Id(0)]
    public List<SecureMessageThreadSummary> Threads { get; set; } = new();
}
