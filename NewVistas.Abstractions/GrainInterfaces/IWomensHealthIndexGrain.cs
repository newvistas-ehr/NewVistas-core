// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient Women's Health notification index grain.
/// Key: "WH-IDX:{patientId}"
///
/// Maintains a lightweight summary list so the UI can list all of a patient's
/// Women's Health notifications without activating every individual record grain.
/// </summary>
public interface IWomensHealthIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.WomensHealthIndexEntry>> GetAllAsync();

    Task<List<GrainStates.WomensHealthIndexEntry>> GetByTypeAsync(
        GrainStates.WomensHealthNotificationType notificationType);

    Task<List<GrainStates.WomensHealthIndexEntry>> GetFollowUpRequiredAsync();

    Task AddEntryAsync(GrainStates.WomensHealthIndexEntry entry);

    Task UpdateEntryStatusAsync(
        string notificationId,
        GrainStates.WomensHealthNotificationStatus status,
        bool? followUpRequired,
        DateTime? nextDueDate);
}
