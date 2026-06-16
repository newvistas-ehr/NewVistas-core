// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class CLCAdmissionIndexState
{
    [Id(0)] public List<CLCAdmissionIndexEntry> Admissions { get; set; } = new();
}

public class CLCAdmissionIndexGrain : Grain, ICLCAdmissionIndexGrain
{
    private readonly IPersistentState<CLCAdmissionIndexState> _state;

    public CLCAdmissionIndexGrain(
        [PersistentState("clcAdmissionIndexState", "clcAdmissionIndexStore")] IPersistentState<CLCAdmissionIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertAdmissionAsync(CLCAdmissionIndexEntry entry)
    {
        CLCAdmissionIndexEntry? existing = _state.State.Admissions.Find(a => a.AdmissionId == entry.AdmissionId);
        if (existing is not null)
            _state.State.Admissions.Remove(existing);
        _state.State.Admissions.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<CLCAdmissionIndexEntry>> GetAllAdmissionsAsync()
    {
        List<CLCAdmissionIndexEntry> result = _state.State.Admissions
            .OrderByDescending(a => a.AdmitDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CLCAdmissionIndexEntry>> GetActiveCensusAsync()
    {
        List<CLCAdmissionIndexEntry> result = _state.State.Admissions
            .Where(a => a.Status is CLCAdmissionStatus.Active or CLCAdmissionStatus.OnLeave)
            .OrderBy(a => a.Ward)
            .ThenBy(a => a.BedRoom)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CLCAdmissionIndexEntry>> GetAdmissionsByLevelOfCareAsync(GECLevelOfCare levelOfCare)
    {
        List<CLCAdmissionIndexEntry> result = _state.State.Admissions
            .Where(a => a.LevelOfCare == levelOfCare && a.Status == CLCAdmissionStatus.Active)
            .OrderBy(a => a.PatientName)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CLCAdmissionIndexEntry>> GetAdmissionsByWardAsync(string ward)
    {
        List<CLCAdmissionIndexEntry> result = _state.State.Admissions
            .Where(a => a.Ward == ward && a.Status is CLCAdmissionStatus.Active or CLCAdmissionStatus.OnLeave)
            .OrderBy(a => a.BedRoom)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CLCAdmissionIndexEntry>> GetAnticipatedDischargesAsync(int withinDays)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(withinDays);
        List<CLCAdmissionIndexEntry> result = _state.State.Admissions
            .Where(a => a.Status == CLCAdmissionStatus.Active
                && a.AnticipatedDischargeDate.HasValue
                && a.AnticipatedDischargeDate.Value <= cutoff)
            .OrderBy(a => a.AnticipatedDischargeDate)
            .ToList();
        return Task.FromResult(result);
    }
}
