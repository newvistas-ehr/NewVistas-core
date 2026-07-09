// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-institution transfer-center queue (incoming + outgoing requests).
/// Grain key: "TRANSFER-CENTER:{institutionId}". Store: transferCenterStore.
/// Written by the workflow layer on every request transition — do not write directly.
/// </summary>
public interface ITransferCenterGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(TransferRequestEntry entry);

    /// <summary>Requests where THIS institution is the receiver. activeOnly = REQUESTED/ACCEPTED.</summary>
    Task<List<TransferRequestEntry>> GetIncomingAsync(bool activeOnly = true);

    /// <summary>Requests where THIS institution is the sender.</summary>
    Task<List<TransferRequestEntry>> GetOutgoingAsync(bool activeOnly = true);

    /// <summary>Incoming REQUESTED count — the nav/queue badge.</summary>
    Task<int> GetPendingIncomingCountAsync();
}
