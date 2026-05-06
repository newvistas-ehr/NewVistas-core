// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Index Grain — singleton cross-reference for patient search.
/// Grain key: "PATIENT-INDEX"
///
/// Search heuristic mirrors ORWPT LOOKUP (ORWPT.m):
///   4-digit numeric     → SSN last-4 exact match (^DPT "BS5" x-ref)
///   1–8 digit numeric   → DFN exact match (^DPT direct IEN)
///   Otherwise           → case-insensitive Name prefix (^DPT "B" x-ref)
/// </summary>
public class PatientIndexGrain : Grain, IPatientIndexGrain
{
    private readonly IPersistentState<PatientIndexState> _state;

    public PatientIndexGrain(
        [PersistentState("patientIndexState", "patientIndexStore")]
        IPersistentState<PatientIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PatientIndexEntry entry)
    {
        _state.State.Patients[entry.PatientId] = entry;
        await _state.WriteStateAsync();
    }

    public Task<PatientIndexEntry?> GetByPatientIdAsync(string patientId)
    {
        _state.State.Patients.TryGetValue(patientId, out PatientIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<PatientIndexEntry?> GetByDfnAsync(string dfn)
    {
        PatientIndexEntry? entry = _state.State.Patients.Values
            .FirstOrDefault(e => e.Dfn == dfn);
        return Task.FromResult(entry);
    }

    public Task<PatientIndexEntry?> GetByIcnAsync(string icn)
    {
        PatientIndexEntry? entry = _state.State.Patients.Values
            .FirstOrDefault(e => e.Icn == icn);
        return Task.FromResult(entry);
    }

    public Task<List<PatientIndexEntry>> SearchAsync(string searchTerm, int maxResults = 25)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Task.FromResult(new List<PatientIndexEntry>());

        string term = searchTerm.Trim();

        IEnumerable<PatientIndexEntry> results;

        if (term.Length == 4 && term.All(char.IsDigit))
        {
            // 4-digit all-numeric: SSN last-4 exact match
            results = _state.State.Patients.Values
                .Where(e => e.SsnLast4 == term);
        }
        else if (term.Length >= 1 && term.Length <= 8 && term.All(char.IsDigit))
        {
            // 1–8 digit numeric (not 4): DFN exact match
            results = _state.State.Patients.Values
                .Where(e => e.Dfn == term);
        }
        else
        {
            // Name prefix search — mirrors VistA "B" cross-reference on ^DPT
            // Supports "LAST" or "LAST,FIRST" prefix, case-insensitive
            results = _state.State.Patients.Values
                .Where(e => e.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(results
            .OrderBy(e => e.Name)
            .Take(maxResults)
            .ToList());
    }

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Patients.Count);

    public Task<List<string>> GetAllPatientIdsAsync(int? maxResults = null)
    {
        IEnumerable<string> ids = _state.State.Patients.Keys;
        if (maxResults.HasValue) ids = ids.Take(maxResults.Value);
        return Task.FromResult(ids.ToList());
    }
}
