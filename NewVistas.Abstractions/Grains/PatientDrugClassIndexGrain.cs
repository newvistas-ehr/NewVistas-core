// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient maintainer of the class→patient reverse index. Keyed by patient id.
/// Recomputes the patient's active VA drug-class set from the PSO prescription index
/// and applies the diff to the class cohort shards.
/// </summary>
public class PatientDrugClassIndexGrain : Grain, IPatientDrugClassIndexGrain
{
    private readonly IPersistentState<PatientDrugClassIndexState> _state;

    public PatientDrugClassIndexGrain(
        [PersistentState("patientDrugClassIndexState", "patientDrugClassIndexStore")]
        IPersistentState<PatientDrugClassIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            _state.State.PatientId = this.GetPrimaryKeyString();

        return base.OnActivateAsync(cancellationToken);
    }

    private IDrugClassCohortIndexGrain Cohort(string classCode) =>
        GrainFactory.GetGrain<IDrugClassCohortIndexGrain>(classCode);

    public Task<List<string>> GetActiveClassCodesAsync() =>
        Task.FromResult(_state.State.ActiveClassCodes.OrderBy(c => c).ToList());

    public async Task RefreshAsync()
    {
        string patientId = this.GetPrimaryKeyString();

        // Resolve the patient's current active/hold prescriptions and their drugs.
        IPatientPrescriptionIndexGrain pso =
            GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");
        List<PrescriptionIndexEntry> entries = await pso.GetAllAsync();

        List<string> drugIds = entries
            .Where(e => e.Status is "ACTIVE" or "HOLD")
            .Select(e => e.DrugId)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d!)
            .Distinct()
            .ToList();

        // Union of every active drug's class set (primary + secondary), upper-cased.
        HashSet<string> current = new();
        if (drugIds.Count > 0)
        {
            DrugClassInfo[] infos = await Task.WhenAll(
                drugIds.Select(id => GrainFactory.GetGrain<IDrugGrain>(id).GetDrugClassAsync()));

            foreach (DrugClassInfo info in infos)
                foreach (string code in info.AllClassCodes)
                    if (!string.IsNullOrWhiteSpace(code))
                        current.Add(code.ToUpperInvariant());
        }

        // Diff against the previously stored set and propagate to the cohort shards.
        HashSet<string> previous = _state.State.ActiveClassCodes;
        List<string> added = current.Where(c => !previous.Contains(c)).ToList();
        List<string> removed = previous.Where(c => !current.Contains(c)).ToList();

        if (added.Count == 0 && removed.Count == 0)
            return;

        foreach (string code in added)
            await Cohort(code).AddPatientAsync(patientId);
        foreach (string code in removed)
            await Cohort(code).RemovePatientAsync(patientId);

        _state.State.ActiveClassCodes = current;
        await _state.WriteStateAsync();
    }
}
