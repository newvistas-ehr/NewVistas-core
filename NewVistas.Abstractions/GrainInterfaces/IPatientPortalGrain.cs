// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

// ═══════════════════════════════════════════════════════════════════════════════
// §170.315(e)(3) — Patient Health Information Capture
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Patient Submission Grain — stores a single patient-submitted health information packet.
/// Grain Key: "PATIENT-SUB:{submissionId}"
/// </summary>
public interface IPatientSubmissionGrain : IGrainWithStringKey
{
    Task CreateSubmissionAsync(PatientSubmissionState submission);
    Task<PatientSubmissionState> GetSubmissionAsync();

    /// <summary>Clinician marks submission as under review.</summary>
    Task MarkUnderReviewAsync(string reviewerId);

    /// <summary>
    /// Clinician completes review — accepts, rejects, or partially accepts.
    /// </summary>
    Task CompleteReviewAsync(
        string status,
        string reviewerId,
        string? reviewNotes,
        List<string> acceptedSections,
        List<string> rejectedSections);
}

/// <summary>
/// Per-patient submission index — tracks all submissions for one patient.
/// Grain Key: "PATIENT-SUB-IDX:{patientId}"
/// </summary>
public interface IPatientSubmissionIndexGrain : IGrainWithStringKey
{
    Task AddSubmissionAsync(PatientSubmissionSummary summary);
    Task UpdateStatusAsync(string submissionId, string status);
    Task<List<PatientSubmissionSummary>> GetAllSubmissionsAsync();
    Task<List<PatientSubmissionSummary>> GetSubmissionsByStatusAsync(string status);
}

/// <summary>
/// System-wide submission review queue — all pending submissions across patients.
/// Grain Key: "PATIENT-SUB-QUEUE"
/// </summary>
public interface IPatientSubmissionQueueGrain : IGrainWithStringKey
{
    Task AddSubmissionAsync(PatientSubmissionSummary summary);
    Task UpdateStatusAsync(string submissionId, string status);
    Task RemoveSubmissionAsync(string submissionId);
    Task<List<PatientSubmissionSummary>> GetPendingSubmissionsAsync();
    Task<List<PatientSubmissionSummary>> GetAllSubmissionsAsync();
}

// ═══════════════════════════════════════════════════════════════════════════════
// §170.315(e)(2) — Secure Messaging
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Secure Message Thread Grain — manages a bidirectional message thread.
/// Grain Key: "SECURE-MSG-THREAD:{threadId}"
/// </summary>
public interface ISecureMessageThreadGrain : IGrainWithStringKey
{
    Task CreateThreadAsync(
        string patientId,
        string? patientName,
        string subject,
        string category,
        string? assignedProviderId,
        string? assignedProviderName);

    /// <summary>Add a message to the thread (from patient or provider).</summary>
    Task AddMessageAsync(
        string senderType,
        string? senderId,
        string? senderName,
        string body);

    /// <summary>Mark all messages as read by the specified party ("patient" or "provider").</summary>
    Task MarkReadAsync(string readerType);

    Task CloseThreadAsync();
    Task ReopenThreadAsync();

    Task<SecureMessageThreadState> GetThreadAsync();
}

/// <summary>
/// Per-patient secure message thread index.
/// Grain Key: "SECURE-MSG-IDX:{patientId}"
/// </summary>
public interface ISecureMessageIndexGrain : IGrainWithStringKey
{
    Task AddThreadAsync(SecureMessageThreadSummary summary);
    Task UpdateThreadAsync(SecureMessageThreadSummary summary);
    Task<List<SecureMessageThreadSummary>> GetAllThreadsAsync();
    Task<List<SecureMessageThreadSummary>> GetOpenThreadsAsync();
    Task<List<SecureMessageThreadSummary>> GetUnreadByPatientAsync();
    Task<List<SecureMessageThreadSummary>> GetUnreadByProviderAsync();
}

/// <summary>
/// System-wide secure message queue for providers — threads needing attention.
/// Grain Key: "SECURE-MSG-QUEUE"
/// </summary>
public interface ISecureMessageQueueGrain : IGrainWithStringKey
{
    Task AddThreadAsync(SecureMessageThreadSummary summary);
    Task UpdateThreadAsync(SecureMessageThreadSummary summary);
    Task RemoveThreadAsync(string threadId);
    Task<List<SecureMessageThreadSummary>> GetUnreadThreadsAsync();
    Task<List<SecureMessageThreadSummary>> GetAllActiveThreadsAsync();
}
