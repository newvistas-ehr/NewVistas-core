// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient log of drug safety advisories the patient has received. Keyed by
/// patient id. This is the source of truth for "what warning did this patient get,
/// what did it say, who sent it, and when".
/// </summary>
public interface IPatientSafetyAdvisoryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records that the patient received an advisory with the exact message text sent.
    /// Idempotent per advisory: if a receipt already exists for <paramref name="advisoryId"/>
    /// the existing receipt id is returned and nothing is appended.
    /// </summary>
    Task<string> RecordReceiptAsync(
        string advisoryId,
        string advisoryTitle,
        string messageSent,
        string providerId,
        string providerName,
        AdvisoryChannel channel);

    /// <summary>Returns all advisory receipts for this patient (newest last).</summary>
    Task<List<PatientAdvisoryReceipt>> GetReceiptsAsync();

    /// <summary>True if the patient already has a receipt for the advisory.</summary>
    Task<bool> HasReceivedAsync(string advisoryId);

    /// <summary>Marks a previously sent advisory as acknowledged by/for the patient.</summary>
    Task AcknowledgeAsync(string advisoryId, DateTime acknowledgedDate);
}
