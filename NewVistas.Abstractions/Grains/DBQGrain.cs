// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class DBQGrain : Grain, IDBQGrain
{
    private readonly IPersistentState<DBQState> _state;

    public DBQGrain(
        [PersistentState("cpDbqState", "cpDbqStore")] IPersistentState<DBQState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.DbqId))
        {
            _state.State.DbqId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateDBQAsync(
        string examId,
        string patientId,
        string patientName,
        DBQType dbqType,
        string dbqFormNumber,
        string dbqTitle,
        string claimNumber,
        string conditionClaimed,
        string diagnosisCode,
        string diagnosisDescription)
    {
        _state.State.ExamId = examId;
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.DbqType = dbqType;
        _state.State.DbqFormNumber = dbqFormNumber;
        _state.State.DbqTitle = dbqTitle;
        _state.State.ClaimNumber = claimNumber;
        _state.State.ConditionClaimed = conditionClaimed;
        _state.State.DiagnosisCode = diagnosisCode;
        _state.State.DiagnosisDescription = diagnosisDescription;
        _state.State.Status = DBQStatus.Draft;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateSectionsAsync(
        string historySection,
        string symptomsSection,
        string functionalImpactSection,
        string rangeOfMotionSection,
        string mentalStatusSection,
        string diagnosticTestsSection)
    {
        _state.State.HistorySection = historySection;
        _state.State.SymptomsSection = symptomsSection;
        _state.State.FunctionalImpactSection = functionalImpactSection;
        _state.State.RangeOfMotionSection = rangeOfMotionSection;
        _state.State.MentalStatusSection = mentalStatusSection;
        _state.State.DiagnosticTestsSection = diagnosticTestsSection;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordOpinionAsync(
        bool nexusOpinion,
        string nexusStatement,
        string opinionsSection,
        ServiceConnectionType serviceConnectionType,
        bool residualsPermanent,
        bool expectedImprovement)
    {
        _state.State.NexusOpinion = nexusOpinion;
        _state.State.NexusStatement = nexusStatement;
        _state.State.OpinionsSection = opinionsSection;
        _state.State.ServiceConnectionType = serviceConnectionType;
        _state.State.ResidualsPermanent = residualsPermanent;
        _state.State.ExpectedImprovement = expectedImprovement;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetProposedRatingAsync(int proposedRating)
    {
        _state.State.ProposedRating = proposedRating;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteDBQAsync()
    {
        _state.State.Status = DBQStatus.Completed;
        _state.State.CompletedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SignDBQAsync(string signedBy, DateTime signedDate)
    {
        _state.State.SignedBy = signedBy;
        _state.State.SignedDate = signedDate;
        _state.State.Status = DBQStatus.Signed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<DBQState> GetDBQAsync() => Task.FromResult(_state.State);
}
