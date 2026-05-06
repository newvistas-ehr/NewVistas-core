// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab specimen accession grain — LRACE.m accessioning workflow.
/// Grain key: "LAB-ACCESSION:{accessionNumber}"
/// </summary>
public interface ILabAccessionGrain : IGrainWithStringKey
{
    Task<LabAccessionState> GetAsync();

    Task<string> CreateAsync(
        string patientId, List<string> labTestIds, string specimenType,
        string? collectionTube, DateTime collectionDateTime,
        string? labSection, string? accessionedByUserId, string? accessionedByUserName, string? notes);

    Task MarkInProcessAsync();

    Task MarkCompletedAsync();

    Task RejectAsync(SpecimenRejectReason reason, string? rejectNotes,
        string rejectedByUserId, bool orderRecollect);
}
