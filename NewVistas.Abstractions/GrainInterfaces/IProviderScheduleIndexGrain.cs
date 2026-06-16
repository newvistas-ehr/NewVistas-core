// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Provider Schedule Index Grain — a provider's appointment schedule.
/// Provides "Today's Schedule" and "Upcoming" views from the provider's perspective.
///
/// Key pattern: "PROV-SCHED:{providerId}"
/// </summary>
public interface IProviderScheduleIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Add or update a schedule entry. Upserts by AppointmentId.
    /// </summary>
    Task AddOrUpdateAsync(ProviderScheduleEntry entry);

    /// <summary>
    /// Remove a schedule entry by appointment ID.
    /// </summary>
    Task RemoveAsync(string appointmentId);

    /// <summary>
    /// Get all appointments for a specific date, ordered by time.
    /// </summary>
    Task<List<ProviderScheduleEntry>> GetByDateAsync(DateTime date);

    /// <summary>
    /// Get today's appointments, ordered by time.
    /// </summary>
    Task<List<ProviderScheduleEntry>> GetTodayAsync();

    /// <summary>
    /// Get upcoming appointments within the next N days (default 7), ordered by date/time.
    /// </summary>
    Task<List<ProviderScheduleEntry>> GetUpcomingAsync(int days = 7);

    /// <summary>
    /// Get all schedule entries up to a maximum count, ordered by date descending.
    /// </summary>
    Task<List<ProviderScheduleEntry>> GetAllAsync(int max = 100);

    /// <summary>
    /// Update the status of an appointment by ID.
    /// </summary>
    Task UpdateStatusAsync(string appointmentId, string status);
}
