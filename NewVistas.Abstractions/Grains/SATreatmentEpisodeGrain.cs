// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class SATreatmentEpisodeGrain : Grain, ISATreatmentEpisodeGrain
{
    private readonly IPersistentState<SATreatmentEpisodeState> _state;

    public SATreatmentEpisodeGrain(
        [PersistentState("saTreatmentEpisodeState", "saEpisodeStore")]
        IPersistentState<SATreatmentEpisodeState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EpisodeId))
        {
            _state.State.EpisodeId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<SATreatmentEpisodeState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        SATreatmentModality modality,
        SubstanceType primarySubstance,
        List<SubstanceType>? secondarySubstances,
        DateTime intakeDate,
        DateTime? lastUseDate,
        DateTime? sobrietyDate,
        string? programName,
        List<string>? treatmentGoals,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.Status = SATreatmentStatus.Active;
        _state.State.Modality = modality;
        _state.State.PrimarySubstance = primarySubstance;
        _state.State.SecondarySubstances = secondarySubstances ?? new();
        _state.State.IntakeDate = intakeDate;
        _state.State.LastUseDate = lastUseDate;
        _state.State.SobrietyDate = sobrietyDate;
        _state.State.ProgramName = programName;
        _state.State.TreatmentGoals = treatmentGoals ?? new();
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddMATEntryAsync(MATEntry entry)
    {
        if (!_state.State.MATEntries.Any(e => e.EntryId == entry.EntryId))
            _state.State.MATEntries.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StopMATEntryAsync(string entryId, DateTime endDate)
    {
        MATEntry? entry = _state.State.MATEntries.FirstOrDefault(e => e.EntryId == entryId);
        if (entry != null)
        {
            entry.EndDate = endDate;
            entry.IsActive = false;
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTreatmentGoalAsync(string goal)
    {
        if (!_state.State.TreatmentGoals.Contains(goal))
            _state.State.TreatmentGoals.Add(goal);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargeAsync(DateTime dischargeDate,
        SADischargeDisposition disposition, string? notes)
    {
        _state.State.Status = SATreatmentStatus.Discharged;
        _state.State.DischargeDate = dischargeDate;
        _state.State.DischargeDisposition = disposition;
        if (notes != null) _state.State.Notes = notes;
        // Stop all active MAT entries on discharge
        foreach (MATEntry mat in _state.State.MATEntries.Where(e => e.IsActive))
        {
            mat.EndDate = dischargeDate;
            mat.IsActive = false;
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReopenAsync(string? notes)
    {
        _state.State.Status = SATreatmentStatus.Reopened;
        _state.State.DischargeDate = null;
        _state.State.DischargeDisposition = null;
        if (notes != null) _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(SATreatmentStatus status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
