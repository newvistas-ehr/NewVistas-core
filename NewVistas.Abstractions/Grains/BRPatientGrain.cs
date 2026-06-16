// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Blind Rehabilitation Patient Grain — grain key: "BR-PATIENT:{patientId}"
/// </summary>
public class BRPatientGrain : Grain, IBRPatientGrain
{
    private readonly IPersistentState<BRPatientState> _state;

    public BRPatientGrain(
        [PersistentState("brPatientState", "brPatientStore")]
        IPersistentState<BRPatientState> state)
    {
        _state = state;
    }

    public Task<BRPatientState> GetAsync() => Task.FromResult(_state.State);

    public async Task InitializeAsync(string patientId)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = patientId;
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordVisualAcuityAsync(
        string rightEyeDistance,
        string leftEyeDistance,
        string bestCorrectedRight,
        string bestCorrectedLeft,
        VisualField visualFieldRight,
        VisualField visualFieldLeft,
        string? contrastSensitivity,
        DateTime examDate,
        string examinerId,
        string examinerName,
        string? notes)
    {
        _state.State.RightEyeDistance = rightEyeDistance;
        _state.State.LeftEyeDistance = leftEyeDistance;
        _state.State.BestCorrectedRight = bestCorrectedRight;
        _state.State.BestCorrectedLeft = bestCorrectedLeft;
        _state.State.VisualFieldRight = visualFieldRight;
        _state.State.VisualFieldLeft = visualFieldLeft;
        _state.State.ContrastSensitivity = contrastSensitivity;
        _state.State.LastExamDate = examDate;
        _state.State.ExaminerId = examinerId;
        _state.State.ExaminerName = examinerName;
        if (notes is not null) _state.State.AcuityNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateDiagnosisAsync(
        string primaryDiagnosis,
        string? secondaryDiagnosis,
        BROnsetType onsetType,
        DateTime? onsetDate,
        bool serviceConnected,
        int? serviceConnectedPercentage,
        string? icd10Code,
        string? notes)
    {
        _state.State.PrimaryDiagnosis = primaryDiagnosis;
        _state.State.SecondaryDiagnosis = secondaryDiagnosis;
        _state.State.OnsetType = onsetType;
        _state.State.OnsetDate = onsetDate;
        _state.State.ServiceConnected = serviceConnected;
        _state.State.ServiceConnectedPercentage = serviceConnectedPercentage;
        _state.State.Icd10Code = icd10Code;
        if (notes is not null) _state.State.DiagnosisNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddDeviceAsync(BRDeviceEntry device)
    {
        _state.State.Devices.Add(device);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTrainingGoalAsync(string goal, BRTrainingArea area)
    {
        _state.State.TrainingGoals.Add(new BRTrainingGoalEntry
        {
            Goal = goal,
            Area = area,
            RecordedDate = DateTime.UtcNow
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateEligibilityAsync(BREligibilityStatus eligibility, string? reason)
    {
        _state.State.EligibilityStatus = eligibility;
        _state.State.EligibilityReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
