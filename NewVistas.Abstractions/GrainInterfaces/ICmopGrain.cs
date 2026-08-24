// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// CMOP Transmission Grain — manages a single batch transmission to a
/// Consolidated Mail Outpatient Pharmacy facility.
/// Key: "CMOP-TX:{transmissionId}"
/// Maps to VistA PSX package / File #550.2.
/// </summary>
public interface ICmopTransmissionGrain : IGrainWithStringKey
{
    Task<CmopTransmissionState> GetTransmissionAsync();

    Task CreateTransmissionAsync(
        string cmopFacilityId,
        string cmopFacilityName,
        string originatingSiteId,
        List<CmopPrescriptionEntry> prescriptions);

    Task TransmitAsync();
    Task AcknowledgeReceiptAsync();
    Task RecordDispensedAsync(int dispensedCount, int rejectedCount, string? errorMessage);
    Task RecordShippedAsync(string trackingNumber, string carrier);
    Task CompleteAsync();
    Task CancelAsync(string reason);
    Task RejectItemAsync(string prescriptionId, string reason);
}

/// <summary>
/// CMOP Suspense Queue Grain — manages prescriptions queued for mail-order
/// fulfillment at a specific site.
/// Key: "CMOP-SUSPENSE:{siteId}"
/// Maps to VistA File #52.5 (Rx Suspense).
/// </summary>
public interface ICmopSuspenseGrain : IGrainWithStringKey
{
    /// <summary>
    /// Seeds a small demo data set through this grain. Owned here rather than in a
    /// controller so Blazor, WPF and the REST API all invoke one implementation — an
    /// internal UI must never call the WebServer to populate its own screen.
    /// </summary>
    Task SeedDemoDataAsync();

    Task<CmopSuspenseState> GetSuspenseAsync();
    Task<List<CmopSuspenseEntry>> GetQueuedPrescriptionsAsync();
    Task<int> GetQueueCountAsync();

    Task AddToSuspenseAsync(CmopSuspenseEntry entry);
    Task RemoveFromSuspenseAsync(string prescriptionId);
    Task ClearQueueAsync();

    Task SetAutoTransmitAsync(bool enabled, int? hour);

    /// <summary>
    /// Pulls all queued prescriptions, creates a transmission, clears the queue.
    /// Returns the transmission ID.
    /// </summary>
    Task<string> TransmitQueueAsync(string cmopFacilityId, string cmopFacilityName);
}

/// <summary>
/// CMOP Transmission Index Grain — tracks all transmissions for a site.
/// Key: "CMOP-TX-INDEX:{siteId}"
/// </summary>
public interface ICmopTransmissionIndexGrain : IGrainWithStringKey
{
    Task<List<CmopTransmissionSummary>> GetTransmissionsAsync();
    Task<List<CmopTransmissionSummary>> GetTransmissionsByStatusAsync(string status);
    Task AddOrUpdateAsync(CmopTransmissionSummary summary);
    Task RemoveAsync(string transmissionId);
}
