// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// MPI Correlation Grain — stores a single patient's national identity and
/// all local facility correlations in the Master Patient Index.
///
/// Grain Key: "MPI:{icn}" (e.g., "MPI:1000000001V123456")
///
/// Maps to VistA PATIENT CORRELATION file (#985). Each activation represents
/// one nationally-identified patient and their correlations to local DFNs
/// at treating facilities.
///
/// Related MUMPS routines:
///   GETICN^MPIF001  — retrieve ICN
///   SETCOR^MPIF001  — set/update correlation
///   GETCOR^MPIF001  — get all correlations
/// </summary>
public class MpiCorrelationGrain : Grain, IMpiCorrelationGrain
{
    private readonly IPersistentState<MpiCorrelationState> _state;

    public MpiCorrelationGrain(
        [PersistentState("mpiCorrelationState", "mpiCorrelationStore")]
        IPersistentState<MpiCorrelationState> state)
    {
        _state = state;
    }

    public Task<MpiCorrelationState> GetCorrelationAsync() =>
        Task.FromResult(_state.State);

    public async Task SetCorrelationAsync(string icn, string patientName, string ssn, DateTime? dateOfBirth, string? sex)
    {
        _state.State.Icn = icn;
        _state.State.PatientName = patientName;
        _state.State.Ssn = ssn;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.Sex = sex;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task AddLocalCorrelationAsync(string facilityId, string facilityName, string localDfn, DateTime correlationDate)
    {
        // Check for existing correlation at this facility
        MpiLocalCorrelation? existing = _state.State.LocalCorrelations
            .FirstOrDefault(c => c.FacilityId == facilityId);

        if (existing != null)
        {
            // Update existing correlation
            existing.FacilityName = facilityName;
            existing.LocalDfn = localDfn;
            existing.CorrelationDate = correlationDate;
        }
        else
        {
            _state.State.LocalCorrelations.Add(new MpiLocalCorrelation
            {
                FacilityId = facilityId,
                FacilityName = facilityName,
                LocalDfn = localDfn,
                CorrelationDate = correlationDate
            });
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveLocalCorrelationAsync(string facilityId)
    {
        int removed = _state.State.LocalCorrelations.RemoveAll(c => c.FacilityId == facilityId);

        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<MpiLocalCorrelation>> GetTreatingFacilitiesAsync() =>
        Task.FromResult(_state.State.LocalCorrelations);

    public async Task UpdateLastSeenAsync(string facilityId, DateTime lastSeenDate)
    {
        MpiLocalCorrelation? correlation = _state.State.LocalCorrelations
            .FirstOrDefault(c => c.FacilityId == facilityId);

        if (correlation != null)
        {
            correlation.LastSeenDate = lastSeenDate;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task MarkDeceasedAsync(DateTime dateOfDeath)
    {
        _state.State.IsDeceased = true;
        _state.State.DateOfDeath = dateOfDeath;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkAsMergedAsync(string survivingIcn)
    {
        if (string.IsNullOrWhiteSpace(survivingIcn))
            throw new ArgumentException("Surviving ICN cannot be empty.", nameof(survivingIcn));
        if (survivingIcn == _state.State.Icn)
            throw new ArgumentException("An ICN cannot be merged into itself.", nameof(survivingIcn));

        // Idempotent: re-applying with the same target is a no-op.
        if (_state.State.MergedIntoIcn == survivingIcn)
            return;
        // Defensive: refuse to overwrite an existing merge target with a different one
        // (would silently re-route the alias chain). Caller must reissue if intentional.
        if (!string.IsNullOrEmpty(_state.State.MergedIntoIcn))
            throw new InvalidOperationException(
                $"ICN {_state.State.Icn} is already merged into {_state.State.MergedIntoIcn}; refusing to remerge into {survivingIcn}.");

        _state.State.MergedIntoIcn = survivingIcn;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }
}
