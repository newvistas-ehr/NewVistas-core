// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain for a single nursing shift handoff/report.
/// Grain key: "NURS-HANDOFF:{guid}"
/// </summary>
public interface INursingShiftHandoffGrain : IGrainWithStringKey
{
    Task<NursingShiftHandoffState> GetAsync();

    Task<string> CreateAsync(
        string patientId, NursingShift shift, DateTime shiftDate,
        string outgoingNurseId, string outgoingNurseName,
        string? locationId, string? locationName, string? bedNumber,
        SbarPatientSummary sbar, HandoffClinicalSnapshot? clinicalSnapshot,
        List<string>? safetyConcerns, string? notes);

    Task CompleteAsync();

    Task AcknowledgeAsync(string incomingNurseId, string incomingNurseName);
}
