// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Grain implementation for a single Anatomic Pathology case (SP, CY, or AU).
/// VistA LAB DATA file (#63) subfiles #63.08, #63.09, #63.19.
/// Grain key pattern: "AP-CASE:{caseId}"
/// </summary>
public class AnatomicPathologyCaseGrain : Grain, IAnatomicPathologyCaseGrain
{
    private readonly IPersistentState<AnatomicPathologyState> _state;

    public AnatomicPathologyCaseGrain(
        [PersistentState("apCaseState", "apCaseStore")] IPersistentState<AnatomicPathologyState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.CaseId))
        {
            _state.State.CaseId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<AnatomicPathologyState> GetCaseAsync() =>
        Task.FromResult(_state.State);

    public async Task AccessionCaseAsync(
        string patientId,
        APCaseType caseType,
        string accessionNumber,
        string? specimenSource,
        string? specimenDescription,
        string? specimenType,
        string? clinicalHistory,
        string? clinicalDiagnosis,
        string? referringProviderId,
        string? referringProviderName,
        string? collectionLocation,
        DateTime? dateCollected,
        DateTime dateReceived)
    {
        _state.State.PatientId = patientId;
        _state.State.CaseType = caseType;
        _state.State.AccessionNumber = accessionNumber;
        _state.State.SpecimenSource = specimenSource;
        _state.State.SpecimenDescription = specimenDescription;
        _state.State.SpecimenType = specimenType;
        _state.State.ClinicalHistory = clinicalHistory;
        _state.State.ClinicalDiagnosis = clinicalDiagnosis;
        _state.State.ReferringProviderId = referringProviderId;
        _state.State.ReferringProviderName = referringProviderName;
        _state.State.CollectionLocation = collectionLocation;
        _state.State.DateCollected = dateCollected;
        _state.State.DateReceived = dateReceived;
        _state.State.DateAccessioned = DateTime.UtcNow;
        _state.State.Status = APCaseStatus.Received;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordGrossDescriptionAsync(
        string grossDescription,
        string? pathologistId,
        string? pathologistName,
        int? specimenPartCount,
        decimal? specimenWeightGrams,
        string? frozenSectionDiagnosis)
    {
        _state.State.GrossDescription = grossDescription;
        _state.State.GrossPathologistId = pathologistId;
        _state.State.GrossPathologistName = pathologistName;
        _state.State.GrossExamDateTime = DateTime.UtcNow;
        _state.State.SpecimenPartCount = specimenPartCount;
        _state.State.SpecimenWeightGrams = specimenWeightGrams;
        _state.State.FrozenSectionDiagnosis = frozenSectionDiagnosis;
        _state.State.Status = APCaseStatus.InProgress;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordMicroscopicDescriptionAsync(string microscopicDescription)
    {
        _state.State.MicroscopicDescription = microscopicDescription;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SignOutDiagnosisAsync(
        string diagnosis,
        List<string> diagnosisCodes,
        string pathologistId,
        string pathologistName,
        DateTime signOutDateTime)
    {
        _state.State.Diagnosis = diagnosis;
        _state.State.DiagnosisCodes = diagnosisCodes;
        _state.State.PathologistId = pathologistId;
        _state.State.PathologistName = pathologistName;
        _state.State.SignOutDateTime = signOutDateTime;
        _state.State.DateReported = signOutDateTime;
        _state.State.Status = APCaseStatus.Final;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task IssuePreliminaryDiagnosisAsync(
        string preliminaryDiagnosis,
        string pathologistId,
        string pathologistName)
    {
        _state.State.Diagnosis = preliminaryDiagnosis;
        _state.State.PathologistId = pathologistId;
        _state.State.PathologistName = pathologistName;
        _state.State.Status = APCaseStatus.Preliminary;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAddendumAsync(
        string addendumText,
        string pathologistId,
        string pathologistName)
    {
        _state.State.Addendum = addendumText;
        _state.State.AddendumDate = DateTime.UtcNow;
        _state.State.AddendumPathologistId = pathologistId;
        _state.State.AddendumPathologistName = pathologistName;
        _state.State.Status = APCaseStatus.Addendum;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AmendDiagnosisAsync(
        string correctedDiagnosis,
        List<string> correctedCodes,
        string amendmentReason,
        string pathologistId,
        string pathologistName)
    {
        _state.State.Diagnosis = correctedDiagnosis;
        _state.State.DiagnosisCodes = correctedCodes;
        _state.State.AmendmentReason = amendmentReason;
        _state.State.PathologistId = pathologistId;
        _state.State.PathologistName = pathologistName;
        _state.State.Status = APCaseStatus.Amended;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddSpecialStainAsync(string stainName)
    {
        if (!_state.State.SpecialStains.Contains(stainName))
            _state.State.SpecialStains.Add(stainName);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddImmunohistochemistryResultAsync(string ihcResult)
    {
        _state.State.ImmunohistochemistryResults.Add(ihcResult);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordCytologyDetailsAsync(string? bethesdaCategory, string? specimenAdequacy)
    {
        _state.State.BethesdaCategory = bethesdaCategory;
        _state.State.SpecimenAdequacy = specimenAdequacy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAutopsyFindingsAsync(
        string? causeOfDeath,
        string? underlyingCauseOfDeath,
        MannerOfDeath? mannerOfDeath,
        string? toxicologyFindings,
        decimal? bodyWeightKg,
        string? neuropathologyFindings)
    {
        _state.State.CauseOfDeath = causeOfDeath;
        _state.State.UnderlyingCauseOfDeath = underlyingCauseOfDeath;
        _state.State.MannerOfDeath = mannerOfDeath;
        _state.State.ToxicologyFindings = toxicologyFindings;
        _state.State.BodyWeightKg = bodyWeightKg;
        _state.State.NeuropathologyFindings = neuropathologyFindings;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCommentsAsync(string comments)
    {
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelCaseAsync(string reason)
    {
        _state.State.Status = APCaseStatus.Cancelled;
        _state.State.Comments = string.IsNullOrEmpty(_state.State.Comments)
            ? $"CANCELLED: {reason}"
            : $"{_state.State.Comments}\nCANCELLED: {reason}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
