// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Radiology exam tracking — detailed tech workflow status.
/// Grain key: "RAD-EXAM:{radiologyId}"
/// </summary>
public interface IRadExamTrackingGrain : IGrainWithStringKey
{
    Task<RadExamTrackingState> GetAsync();
    Task InitializeAsync(string radiologyId, string patientId);
    Task ScheduleAsync(DateTime scheduledDateTime, string? room);
    Task AssignProtocolAsync(string protocolId, string protocolName, string? protocolParameters);
    Task MarkPatientPreppedAsync(string? prepNotes);
    Task StartExamAsync();
    Task CompleteExamAsync(int? imageCount, string? techNotes);
    Task SendImagesToPacsAsync();
    Task LinkImageAsync(string imageId);
    Task CancelExamAsync();
}
