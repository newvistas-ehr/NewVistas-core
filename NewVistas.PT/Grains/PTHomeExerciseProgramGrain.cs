// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Grains;

/// <summary>
/// Manages the home exercise program for a patient.
/// Key format: "PTHEP:{patientId}"
/// </summary>
public class PTHomeExerciseProgramGrain : Grain, IPTHomeExerciseProgramGrain
{
    private readonly IPersistentState<PTHomeExerciseProgramState> _state;

    public PTHomeExerciseProgramGrain(
        [PersistentState("ptHepState", "physTherapyHepStore")]
        IPersistentState<PTHomeExerciseProgramState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            string key = this.GetPrimaryKeyString();
            string[] parts = key.Split(':');
            // Key format: PTHEP:{patientId}
            _state.State.PatientId = parts.Length > 1 ? parts[1] : key;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PTHomeExerciseProgramState> GetProgramAsync() => Task.FromResult(_state.State);

    public async Task<string> AddPrescriptionAsync(HepPrescription prescription)
    {
        prescription.PrescriptionId = Guid.NewGuid().ToString();
        prescription.LastModifiedDate = DateTime.UtcNow;
        if (prescription.PrescribedDate == default)
            prescription.PrescribedDate = DateTime.UtcNow;

        _state.State.Prescriptions.Add(prescription);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return prescription.PrescriptionId;
    }

    public async Task UpdatePrescriptionStatusAsync(string prescriptionId, HepStatus status)
    {
        HepPrescription? prescription = _state.State.Prescriptions
            .FirstOrDefault(p => p.PrescriptionId == prescriptionId);
        if (prescription == null) return;

        prescription.Status = status;
        prescription.LastModifiedDate = DateTime.UtcNow;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<string> LogCompletionAsync(HepCompletionLog log)
    {
        log.LogId = Guid.NewGuid().ToString();
        if (log.CompletedDate == default)
            log.CompletedDate = DateTime.UtcNow;

        _state.State.CompletionLogs.Add(log);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return log.LogId;
    }

    public Task<List<HepPrescription>> GetActivePrescriptionsAsync()
        => Task.FromResult(_state.State.Prescriptions
            .Where(p => p.Status == HepStatus.Active).ToList());

    public Task<List<HepCompletionLog>> GetCompletionLogsAsync(
        string? prescriptionId, DateTime? from, DateTime? to)
    {
        IEnumerable<HepCompletionLog> logs = _state.State.CompletionLogs;

        if (!string.IsNullOrEmpty(prescriptionId))
            logs = logs.Where(l => l.PrescriptionId == prescriptionId);
        if (from.HasValue)
            logs = logs.Where(l => l.CompletedDate >= from.Value);
        if (to.HasValue)
            logs = logs.Where(l => l.CompletedDate <= to.Value);

        return Task.FromResult(logs.OrderByDescending(l => l.CompletedDate).ToList());
    }
}
