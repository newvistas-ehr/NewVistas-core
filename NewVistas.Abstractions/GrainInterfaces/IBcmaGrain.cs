// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// BCMA Grain Interface based on VistA BCMA MEDICATION LOG file (#53.79)
/// </summary>
public interface IBcmaGrain : IGrainWithStringKey
{
    Task<GrainStates.BcmaState> GetAdministrationAsync();

    Task RecordAdministrationAsync(
        string patientId,
        string drugName,
        string? drugId,
        string? dosage,
        string? route,
        string actionStatus,
        DateTime? scheduledDateTime,
        DateTime administrationDateTime,
        string? administeredById,
        string? administeredByName,
        string? injectionSite,
        string? prescriptionId,
        string? orderId,
        string? comments);

    Task RecordWitnessAsync(string witnessId, string witnessName);
    Task RecordPrnReasonAsync(string reason);
    Task RecordPrnEffectivenessAsync(string effectiveness);
    Task MarkNotGivenAsync(string reason);
}
