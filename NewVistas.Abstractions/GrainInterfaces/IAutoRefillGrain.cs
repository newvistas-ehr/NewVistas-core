// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for automated prescription refill scheduling.
/// Enabled per site via ISiteParametersGrain.Features containing "AUTO_REFILL".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// VistA's outpatient pharmacy (File #52) tracks refills remaining and days supply
/// but has no automated refill scheduling — pharmacists must manually process each
/// refill. This grain adds automatic refill scheduling based on days supply,
/// refills remaining, and patient opt-in, generating refill requests before the
/// patient runs out of medication.
///
/// Keyed by enrollment ID (e.g., "RX-AUTOREFILL:{guid}").
/// </summary>
public interface IAutoRefillGrain : IGrainWithStringKey
{
    Task<AutoRefillState> GetEnrollmentAsync();

    Task<AutoRefillState> EnrollAsync(
        string patientId,
        string patientName,
        string prescriptionId,
        string drugName,
        string drugClass,
        int daysSupply,
        int refillsRemaining,
        DateTime lastFillDate,
        string pharmacyId,
        string pharmacyName,
        string enrolledByProviderId,
        string enrolledByProviderName);

    Task UpdateDaysSupplyAsync(int daysSupply);
    Task UpdateRefillsRemainingAsync(int refillsRemaining);
    Task RecordFillAsync(DateTime fillDate, int newRefillsRemaining);
    Task GenerateRefillRequestAsync(string generatedByName);
    Task MarkRefillDispensedAsync(DateTime dispensedDate, string dispensedByName);
    Task SuspendAsync(string reason, string suspendedByName);
    Task ResumeAsync(string resumedByName);
    Task DisenrollAsync(string reason, string disenrolledByName);
    Task ExpireAsync();
}

/// <summary>
/// System-level index grain for auto-refill enrollments.
/// Singleton keyed by "RX-AUTOREFILL-IDX".
/// </summary>
public interface IAutoRefillIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(AutoRefillIndexEntry entry);
    Task RemoveAsync(string enrollmentId);
    Task<List<AutoRefillIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
    Task<List<AutoRefillIndexEntry>> GetByStatusAsync(string status, int maxResults = 50);
    Task<List<AutoRefillIndexEntry>> GetByPharmacyAsync(string pharmacyId, int maxResults = 50);
    Task<List<AutoRefillIndexEntry>> GetDueForRefillAsync(DateTime asOfDate, int maxResults = 50);
    Task<List<AutoRefillIndexEntry>> SearchAsync(string? patientId, string? status, string? pharmacyId, int maxResults = 50);
    Task<int> GetCountAsync();
}
