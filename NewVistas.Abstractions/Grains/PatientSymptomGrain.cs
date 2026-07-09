// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient coded symptom record (grain key <c>SYMPTOMS:{patientId}</c>). Append-only history
/// with a latest-per-code projection; fans out to the <see cref="ISymptomCohortIndexGrain"/>
/// reverse shards on every presence change so denominators stay honest.
/// </summary>
public class PatientSymptomGrain : Grain, IPatientSymptomGrain
{
    private readonly IPersistentState<PatientSymptomState> _state;

    public PatientSymptomGrain(
        [PersistentState("patientSymptomState", "patientSymptomStore")]
        IPersistentState<PatientSymptomState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            // Key is "SYMPTOMS:{patientId}" — strip the prefix to recover the patient id.
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.PatientId = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<int> RecordObservationsAsync(List<SymptomObservation> observations)
    {
        if (observations is null || observations.Count == 0)
            return 0;

        string patientId = _state.State.PatientId;
        int accepted = 0;

        foreach (SymptomObservation raw in observations)
        {
            if (raw is null || !SymptomCatalog.Contains(raw.Code))
                continue; // closed vocabulary — silently drop unknown codes

            SymptomObservation obs = raw with
            {
                Display = SymptomCatalog.DisplayFor(raw.Code),
                RecordedDate = DateTime.UtcNow
            };

            bool wasPresent = _state.State.Latest.TryGetValue(obs.Code, out SymptomObservation? prev)
                              && prev!.Presence == SymptomPresence.Present;

            _state.State.History.Add(obs);
            _state.State.Latest[obs.Code] = obs;
            accepted++;

            // Reverse-index maintenance: every answer marks "assessed"; Present toggles the present set.
            ISymptomCohortIndexGrain shard =
                GrainFactory.GetGrain<ISymptomCohortIndexGrain>($"SYMPTOM-COHORT:{obs.Code}");
            bool isPresent = obs.Presence == SymptomPresence.Present;
            if (isPresent && !wasPresent)
                await shard.RecordPresenceAsync(patientId, true);
            else if (!isPresent && wasPresent)
                await shard.RecordPresenceAsync(patientId, false);
            else
                await shard.MarkAssessedAsync(patientId);
        }

        if (accepted > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
        return accepted;
    }

    public Task<PatientSymptomState> GetAsync() => Task.FromResult(_state.State);

    public Task<List<SymptomObservation>> GetLatestAsync() =>
        Task.FromResult(_state.State.Latest.Values
            .OrderBy(o => o.Display, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<SymptomObservation?> GetLatestForCodeAsync(string code) =>
        Task.FromResult(_state.State.Latest.TryGetValue(code, out SymptomObservation? o) ? o : null);
}
