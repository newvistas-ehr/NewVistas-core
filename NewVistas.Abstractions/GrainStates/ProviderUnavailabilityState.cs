// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Provider Unavailability event state — tracks a provider's sudden unavailability
/// and the batch processing of affected appointments.
///
/// Key pattern: "PROV-UNAVAIL:{guid}"
/// VistA File #44.5 (Non-Count Clinic / Clinic Cancel).
/// </summary>
[GenerateSerializer]
public class ProviderUnavailabilityState
{
    /// <summary>Unique event ID.</summary>
    [Id(0)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>Provider IEN.</summary>
    [Id(1)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Provider name.</summary>
    [Id(2)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Start of unavailability period.</summary>
    [Id(3)]
    public DateTime UnavailableFrom { get; set; }

    /// <summary>End of unavailability period.</summary>
    [Id(4)]
    public DateTime UnavailableTo { get; set; }

    /// <summary>Reason: ILLNESS, INJURY, EMERGENCY, PERSONAL, OTHER.</summary>
    [Id(5)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Additional notes.</summary>
    [Id(6)]
    public string? Notes { get; set; }

    /// <summary>Status: Pending, Processing, Completed, Cancelled.</summary>
    [Id(7)]
    public string Status { get; set; } = "Pending";

    /// <summary>Who initiated this event.</summary>
    [Id(8)]
    public string InitiatedByUserId { get; set; } = string.Empty;

    /// <summary>Name of who initiated this event.</summary>
    [Id(9)]
    public string InitiatedByUserName { get; set; } = string.Empty;

    /// <summary>Date event created.</summary>
    [Id(10)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date event last modified.</summary>
    [Id(11)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>List of affected appointment records.</summary>
    [Id(12)]
    public List<AffectedAppointmentRecord> AffectedAppointments { get; set; } = new();

    /// <summary>Total number of affected appointments.</summary>
    [Id(13)]
    public int TotalAffected { get; set; }

    /// <summary>Number of appointments cancelled.</summary>
    [Id(14)]
    public int CancelledCount { get; set; }

    /// <summary>Number of appointments reassigned.</summary>
    [Id(15)]
    public int ReassignedCount { get; set; }

    /// <summary>Number of notifications generated.</summary>
    [Id(16)]
    public int NotificationsSent { get; set; }

    /// <summary>Number of waitlist offers generated.</summary>
    [Id(17)]
    public int WaitlistOffersGenerated { get; set; }

    /// <summary>Replacement provider ID (for reassignment).</summary>
    [Id(18)]
    public string? ReplacementProviderId { get; set; }

    /// <summary>Replacement provider name (for reassignment).</summary>
    [Id(19)]
    public string? ReplacementProviderName { get; set; }
}

/// <summary>
/// Record of an appointment affected by a provider unavailability event.
/// </summary>
[GenerateSerializer]
public class AffectedAppointmentRecord
{
    /// <summary>Appointment IEN.</summary>
    [Id(0)]
    public string AppointmentId { get; set; } = string.Empty;

    /// <summary>Patient IEN.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Clinic ID.</summary>
    [Id(3)]
    public string ClinicId { get; set; } = string.Empty;

    /// <summary>Clinic name.</summary>
    [Id(4)]
    public string ClinicName { get; set; } = string.Empty;

    /// <summary>Appointment date/time.</summary>
    [Id(5)]
    public DateTime AppointmentDateTime { get; set; }

    /// <summary>Action taken: CANCELLED or REASSIGNED.</summary>
    [Id(6)]
    public string ActionTaken { get; set; } = string.Empty;

    /// <summary>Whether a notification was generated for this appointment.</summary>
    [Id(7)]
    public bool NotificationGenerated { get; set; }

    /// <summary>Whether a waitlist offer was generated for this slot.</summary>
    [Id(8)]
    public bool WaitlistOfferGenerated { get; set; }
}

/// <summary>
/// Result of a batch unavailability operation.
/// </summary>
[GenerateSerializer]
public class ProviderUnavailabilityResult
{
    /// <summary>Event ID.</summary>
    [Id(0)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>Total affected appointments.</summary>
    [Id(1)]
    public int TotalAffected { get; set; }

    /// <summary>Number successfully processed.</summary>
    [Id(2)]
    public int Processed { get; set; }

    /// <summary>Number that failed.</summary>
    [Id(3)]
    public int Failed { get; set; }

    /// <summary>Error messages for failed operations.</summary>
    [Id(4)]
    public List<string> Errors { get; set; } = new();
}
