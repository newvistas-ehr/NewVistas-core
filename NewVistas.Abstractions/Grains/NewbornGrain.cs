// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class NewbornGrain : Grain, INewbornGrain
{
    private readonly IPersistentState<NewbornState> _state;

    public NewbornGrain(
        [PersistentState("newbornState", "newbornStore")] IPersistentState<NewbornState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.NewbornId))
        {
            _state.State.NewbornId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RegisterAsync(
        string motherPatientId,
        string pregnancyId,
        string name,
        NewbornSex sex,
        DateTime birthDateTime,
        int gestationalAgeWeeks,
        int gestationalAgeDays,
        DeliveryMethod deliveryMethod,
        int? birthWeightGrams,
        decimal? lengthCm,
        decimal? headCircumferenceCm,
        int? apgar1Min,
        int? apgar5Min,
        int? apgar10Min,
        int multipleBirthOrder,
        int multipleBirthTotal,
        string attendingProviderId,
        string attendingProviderName,
        string birthLocationName)
    {
        _state.State.MotherPatientId = motherPatientId;
        _state.State.PregnancyId = pregnancyId;
        _state.State.Name = name;
        _state.State.Sex = sex;
        _state.State.BirthDateTime = birthDateTime;
        _state.State.GestationalAgeWeeks = gestationalAgeWeeks;
        _state.State.GestationalAgeDays = gestationalAgeDays;
        _state.State.DeliveryMethod = deliveryMethod;
        _state.State.BirthWeightGrams = birthWeightGrams;
        _state.State.LengthCm = lengthCm;
        _state.State.HeadCircumferenceCm = headCircumferenceCm;
        _state.State.Apgar1Min = apgar1Min;
        _state.State.Apgar5Min = apgar5Min;
        _state.State.Apgar10Min = apgar10Min;
        _state.State.MultipleBirthOrder = multipleBirthOrder < 1 ? 1 : multipleBirthOrder;
        _state.State.MultipleBirthTotal = multipleBirthTotal < 1 ? 1 : multipleBirthTotal;
        _state.State.AttendingProviderId = attendingProviderId;
        _state.State.AttendingProviderName = attendingProviderName;
        _state.State.BirthLocationName = birthLocationName;
        _state.State.Status = NewbornStatus.Admitted;

        _state.State.GestationalAgeClassification = NeonatalClassifier.ClassifyGestationalAge(gestationalAgeWeeks);
        _state.State.BirthWeightCategory = NeonatalClassifier.ClassifyBirthWeight(birthWeightGrams);
        _state.State.SizeForGestationalAge = NeonatalClassifier.ClassifySizeForGestationalAge(gestationalAgeWeeks, birthWeightGrams);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordExamAsync(NewbornExam exam)
    {
        _state.State.Exam = exam ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordScreeningAsync(NewbornScreeningEntry screening)
    {
        // Upsert by screening type — one current result per screen.
        _state.State.Screenings.RemoveAll(s => s.ScreeningType == screening.ScreeningType);
        _state.State.Screenings.Add(screening);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddMeasurementAsync(NewbornMeasurement measurement)
    {
        _state.State.Measurements.Add(measurement);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetNurseryLevelAsync(NurseryLevelOfCare level, string reason)
    {
        _state.State.NurseryLevel = level;
        _state.State.NurseryLevelReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task TransferAsync(string toLocation, string reason)
    {
        _state.State.Status = NewbornStatus.Transferred;
        _state.State.TransferLocation = toLocation;
        _state.State.NurseryLevelReason = string.IsNullOrEmpty(reason) ? _state.State.NurseryLevelReason : reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargeAsync(
        DateTime dischargeDateTime,
        int? dischargeWeightGrams,
        NewbornFeedingType dischargeFeeding,
        string disposition,
        string followUpPlan,
        bool carSeatTestPassed)
    {
        _state.State.Status = NewbornStatus.Discharged;
        _state.State.DischargeDateTime = dischargeDateTime;
        _state.State.DischargeWeightGrams = dischargeWeightGrams;
        _state.State.DischargeFeeding = dischargeFeeding;
        _state.State.DischargeDisposition = disposition;
        _state.State.FollowUpPlan = followUpPlan;
        _state.State.CarSeatTestPassed = carSeatTestPassed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<NewbornState> GetAsync() => Task.FromResult(_state.State);
}
