// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Generic per-patient, per-domain full-history index.
///
/// Grain Key: "{patientId}:{domain}" where domain is one of the
/// PatientHistoryDomains constants (e.g., "P123:CONSULT").
///
/// PatientState keeps only the most recent N item IDs per domain so the hot
/// patient blob stays small; this grain holds the COMPLETE ID history and is
/// only activated when full history is needed (user asks for "all", clinical
/// complete-set reads, merges). One grain type + one store serves every domain
/// that has no richer dedicated index (consults, surgery, radiology, BCMA,
/// imaging, ADT, health factors, mental health, reminders, labs).
/// </summary>
public interface IPatientHistoryIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Appends one item reference. Idempotent — an existing ItemId is updated
    /// in place (its Date refreshed if the new entry has one), not duplicated.
    /// </summary>
    Task AddEntryAsync(HistoryRef entry);

    /// <summary>
    /// Appends a batch of item references, deduplicated by ItemId. Idempotent;
    /// used by the lazy migration that flushes a legacy PatientState ID list
    /// here before that list is first trimmed.
    /// </summary>
    Task AddRangeAsync(List<HistoryRef> entries);

    /// <summary>
    /// Removes an entry by item ID (merge/retraction paths). No-op if absent.
    /// </summary>
    Task RemoveEntryAsync(string itemId);

    /// <summary>
    /// Returns ALL item IDs in append (chronological) order. Used by
    /// complete-set clinical reads (e.g., due-reminder evaluation).
    /// </summary>
    Task<List<string>> GetAllIdsAsync();

    /// <summary>
    /// Returns one page of item IDs, newest first (dated entries by Date
    /// descending, then undated migrated entries in reverse insertion order).
    /// </summary>
    Task<List<string>> GetPageAsync(int offset, int maxResults);

    /// <summary>
    /// Total number of entries.
    /// </summary>
    Task<int> GetCountAsync();
}

/// <summary>
/// Domain constants for IPatientHistoryIndexGrain keys. Kept here (not as an
/// enum) so the grain key stays a readable string: "{patientId}:{domain}".
/// </summary>
public static class PatientHistoryDomains
{
    public const string Lab = "LAB";
    public const string Consult = "CONSULT";
    public const string Surgery = "SURGERY";
    public const string Radiology = "RADIOLOGY";
    public const string Bcma = "BCMA";
    public const string Imaging = "IMAGING";
    public const string Adt = "ADT";
    public const string HealthFactor = "HEALTHFACTOR";
    public const string MentalHealth = "MENTALHEALTH";
    public const string Reminder = "REMINDER";
    public const string Pharmacy = "PHARMACY";
    public const string Tiu = "TIU";
    public const string Order = "ORDER";
    public const string Appointment = "APPOINTMENT";
}
