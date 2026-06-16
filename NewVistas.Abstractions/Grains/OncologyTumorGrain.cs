// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class OncologyTumorGrain : Grain, IOncologyTumorGrain
{
    private readonly IPersistentState<OncologyTumorState> _state;

    public OncologyTumorGrain(
        [PersistentState("oncTumorState", "oncTumorStore")] IPersistentState<OncologyTumorState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.TumorId))
        {
            _state.State.TumorId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<OncologyTumorState> GetTumorAsync() => Task.FromResult(_state.State);

    public async Task RegisterTumorAsync(
        string patientId,
        string primarySite,
        string primarySiteText,
        string histology,
        string histologyText,
        TumorLaterality laterality,
        DateTime dateOfDiagnosis,
        DiagnosisBasis diagnosisBasis,
        int sequenceNumber,
        string? oncologistId,
        string? oncologistName)
    {
        _state.State.PatientId = patientId;
        _state.State.PrimarySite = primarySite;
        _state.State.PrimarySiteText = primarySiteText;
        _state.State.Histology = histology;
        _state.State.HistologyText = histologyText;
        _state.State.Laterality = laterality;
        _state.State.DateOfDiagnosis = dateOfDiagnosis;
        _state.State.DiagnosisBasis = diagnosisBasis;
        _state.State.SequenceNumber = sequenceNumber;
        _state.State.OncologistId = oncologistId;
        _state.State.OncologistName = oncologistName;
        _state.State.Status = OncologyStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordStagingAsync(
        string? clinicalT,
        string? clinicalN,
        string? clinicalM,
        string? pathologicT,
        string? pathologicN,
        string? pathologicM,
        string? stageGroup,
        string? seerSummaryStage)
    {
        _state.State.ClinicalT = clinicalT;
        _state.State.ClinicalN = clinicalN;
        _state.State.ClinicalM = clinicalM;
        _state.State.PathologicT = pathologicT;
        _state.State.PathologicN = pathologicN;
        _state.State.PathologicM = pathologicM;
        _state.State.StageGroup = stageGroup;
        _state.State.SeerSummaryStage = seerSummaryStage;
        _state.State.StagingDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(OncologyStatus status, DateTime? statusChangeDate, string? notes)
    {
        _state.State.Status = status;
        _state.State.StatusChangeDate = statusChangeDate ?? DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
            _state.State.Comments = string.IsNullOrEmpty(_state.State.Comments)
                ? notes
                : $"{_state.State.Comments}\n{notes}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordRecurrenceAsync(DateTime recurrenceDate, string? recurrenceSite, string? notes)
    {
        _state.State.RecurrenceDate = recurrenceDate;
        _state.State.RecurrenceSite = recurrenceSite;
        _state.State.Status = OncologyStatus.Recurrence;
        _state.State.StatusChangeDate = recurrenceDate;
        if (!string.IsNullOrWhiteSpace(notes))
            _state.State.Comments = string.IsNullOrEmpty(_state.State.Comments)
                ? notes
                : $"{_state.State.Comments}\n{notes}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordLastContactAsync(DateTime dateOfLastContact, OncologyStatus status)
    {
        _state.State.DateOfLastContact = dateOfLastContact;
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTreatmentIdAsync(string treatmentId)
    {
        if (!_state.State.TreatmentIds.Contains(treatmentId))
            _state.State.TreatmentIds.Add(treatmentId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCommentAsync(string comment)
    {
        _state.State.Comments = string.IsNullOrEmpty(_state.State.Comments)
            ? comment
            : $"{_state.State.Comments}\n{comment}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
