// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class MedProcedureGrain : Grain, IMedProcedureGrain
{
    private readonly IPersistentState<MedProcedureState> _state;

    public MedProcedureGrain(
        [PersistentState("medProcedureState", "medProcedureStore")] IPersistentState<MedProcedureState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProcedureId))
        {
            _state.State.ProcedureId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<MedProcedureState> GetProcedureAsync() => Task.FromResult(_state.State);

    public async Task OrderProcedureAsync(
        string patientId,
        MedProcedureCategory category,
        string procedureCode,
        string procedureDescription,
        DateTime orderedDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? indication)
    {
        _state.State.PatientId = patientId;
        _state.State.Category = category;
        _state.State.ProcedureCode = procedureCode;
        _state.State.ProcedureDescription = procedureDescription;
        _state.State.OrderedDate = orderedDate;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Indication = indication;
        _state.State.Status = MedProcedureStatus.Ordered;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ScheduleProcedureAsync(DateTime scheduledDate)
    {
        _state.State.ScheduledDate = scheduledDate;
        _state.State.Status = MedProcedureStatus.Scheduled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task BeginProcedureAsync(DateTime performedDate)
    {
        _state.State.PerformedDate = performedDate;
        _state.State.Status = MedProcedureStatus.InProgress;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteProcedureAsync(
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes)
    {
        _state.State.PerformedDate = performedDate;
        _state.State.Findings = findings;
        _state.State.Impression = impression;
        _state.State.Notes = notes;
        _state.State.Status = MedProcedureStatus.Completed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelProcedureAsync(string? reason)
    {
        _state.State.Status = MedProcedureStatus.Cancelled;
        _state.State.CancellationReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── ECG ──────────────────────────────────────────────────────────────────

    public async Task RecordEcgResultsAsync(
        int? rate,
        CardiacRhythm? rhythm,
        int? prIntervalMs,
        int? qrsDurationMs,
        int? qtcMs,
        int? axisDegrees,
        string? interpretation,
        bool? isNormal)
    {
        _state.State.EcgRate = rate;
        _state.State.EcgRhythm = rhythm;
        _state.State.EcgPrIntervalMs = prIntervalMs;
        _state.State.EcgQrsDurationMs = qrsDurationMs;
        _state.State.EcgQtcMs = qtcMs;
        _state.State.EcgAxisDegrees = axisDegrees;
        _state.State.EcgInterpretation = interpretation;
        _state.State.EcgIsNormal = isNormal;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── Cardiology ───────────────────────────────────────────────────────────

    public async Task RecordEchoResultsAsync(
        decimal? lvEjectionFraction,
        string? lvDiastolicFunction,
        string? valvularFindings)
    {
        _state.State.LvEjectionFraction = lvEjectionFraction;
        _state.State.LvDiastolicFunction = lvDiastolicFunction;
        _state.State.ValvularFindings = valvularFindings;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordStressTestResultsAsync(
        decimal? peakMets,
        decimal? targetHeartRatePct,
        bool? inducibleIschemia)
    {
        _state.State.PeakMets = peakMets;
        _state.State.TargetHeartRatePct = targetHeartRatePct;
        _state.State.InducibleIschemia = inducibleIschemia;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordHolterResultsAsync(
        int? durationHours,
        int? arrhythmiaEvents,
        CardiacRhythm? dominantRhythm)
    {
        _state.State.HolterDurationHours = durationHours;
        _state.State.HolterArrhythmiaEvents = arrhythmiaEvents;
        _state.State.EcgRhythm = dominantRhythm;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordCathResultsAsync(
        string? accessSite,
        string? coronaryFindings,
        string? intervention)
    {
        _state.State.CathAccessSite = accessSite;
        _state.State.CoronaryFindings = coronaryFindings;
        _state.State.CathIntervention = intervention;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── Pulmonary Function ────────────────────────────────────────────────────

    public async Task RecordPftResultsAsync(
        decimal? fev1,
        decimal? fev1PctPredicted,
        decimal? fvc,
        decimal? fvcPctPredicted,
        decimal? fev1FvcRatio,
        decimal? dlco,
        decimal? dlcoPctPredicted,
        decimal? tlc,
        decimal? rv,
        bool? obstructive,
        bool? restrictive,
        bool? bronchodilatorResponse)
    {
        _state.State.PftFev1 = fev1;
        _state.State.PftFev1PctPredicted = fev1PctPredicted;
        _state.State.PftFvc = fvc;
        _state.State.PftFvcPctPredicted = fvcPctPredicted;
        _state.State.PftFev1FvcRatio = fev1FvcRatio;
        _state.State.PftDlco = dlco;
        _state.State.PftDlcoPctPredicted = dlcoPctPredicted;
        _state.State.PftTlc = tlc;
        _state.State.PftRv = rv;
        _state.State.PftObstructive = obstructive;
        _state.State.PftRestrictive = restrictive;
        _state.State.PftBronchodilatorResponse = bronchodilatorResponse;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAbgResultsAsync(
        decimal? ph,
        decimal? pao2,
        decimal? paco2,
        decimal? hco3,
        decimal? sao2)
    {
        _state.State.AbgPh = ph;
        _state.State.AbgPao2 = pao2;
        _state.State.AbgPaco2 = paco2;
        _state.State.AbgHco3 = hco3;
        _state.State.AbgSao2 = sao2;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GI/Endoscopy ─────────────────────────────────────────────────────────

    public async Task RecordEndoscopyResultsAsync(
        EndoscopyType endoscopyType,
        BowelPrepQuality? bowelPrepQuality,
        bool? cecumReached,
        int? scopeAdvancedCm,
        bool? biopsyTaken,
        List<string>? biopsySites,
        int? polypCount,
        List<string>? polypDescriptions,
        List<string>? endoscopicInterventions)
    {
        _state.State.EndoscopyType = endoscopyType;
        _state.State.BowelPrepQuality = bowelPrepQuality;
        _state.State.CecumReached = cecumReached;
        _state.State.ScopeAdvancedCm = scopeAdvancedCm;
        _state.State.BiopsyTaken = biopsyTaken;
        _state.State.BiopsySites = biopsySites ?? new();
        _state.State.PolypCount = polypCount;
        _state.State.PolypDescriptions = polypDescriptions ?? new();
        _state.State.EndoscopicInterventions = endoscopicInterventions ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
