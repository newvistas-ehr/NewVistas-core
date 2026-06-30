// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Neonatal / newborn-nursery orchestration (NEONATAL_CARE). A newborn is registered from the
/// mother's delivery and gets its own chart (birth data + classification, exam, newborn screening,
/// interval measurements, nursery level, discharge), linked back to her pregnancy. Access matches
/// the OB module it extends — writes open, reads open. Facility nursery census is the singleton
/// <see cref="INewbornNurseryGrain"/>.
/// </summary>
public partial class PatientWorkflowGrain
{
    private INewbornGrain Newborn(string newbornId) => GrainFactory.GetGrain<INewbornGrain>(newbornId);
    private INewbornNurseryGrain Nursery() => GrainFactory.GetGrain<INewbornNurseryGrain>("NEONATE-NURSERY:DEFAULT");

    private static readonly NewbornScreeningType[] UniversalScreens =
    {
        NewbornScreeningType.MetabolicBloodSpot,
        NewbornScreeningType.CriticalCongenitalHeartDisease,
        NewbornScreeningType.Hearing
    };

    /// <summary>Rebuilds the nursery census entry for a newborn (with the live pending-screen count).</summary>
    private async Task RefreshNewbornNurseryAsync(string newbornId)
    {
        NewbornState n = await Newborn(newbornId).GetAsync();
        int pending = UniversalScreens.Count(t =>
        {
            NewbornScreeningEntry? s = n.Screenings.FirstOrDefault(x => x.ScreeningType == t);
            return s is null || s.Result == NewbornScreeningResult.Pending;
        });
        await Nursery().UpsertEntryAsync(new NewbornNurseryEntry
        {
            NewbornId = n.NewbornId,
            NewbornName = n.Name,
            MotherPatientId = n.MotherPatientId,
            Sex = n.Sex,
            BirthDateTime = n.BirthDateTime,
            GestationalAgeWeeks = n.GestationalAgeWeeks,
            BirthWeightGrams = n.BirthWeightGrams,
            NurseryLevel = n.NurseryLevel,
            Status = n.Status,
            AttendingProviderName = n.AttendingProviderName,
            PendingScreenCount = pending
        });
    }

    // ─── Writes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a newborn delivered from one of this patient's (the mother's) pregnancies and
    /// links it back to that pregnancy. Returns the new newborn id.
    /// </summary>
    public async Task<string> RegisterNewbornFromDeliveryAsync(
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
        string newbornId = $"NEONATE:{Guid.NewGuid()}";
        await Newborn(newbornId).RegisterAsync(
            PatientId, pregnancyId, name, sex, birthDateTime, gestationalAgeWeeks, gestationalAgeDays,
            deliveryMethod, birthWeightGrams, lengthCm, headCircumferenceCm,
            apgar1Min, apgar5Min, apgar10Min, multipleBirthOrder, multipleBirthTotal,
            attendingProviderId, attendingProviderName, birthLocationName);
        await Pregnancy(pregnancyId).AddNewbornIdAsync(newbornId);
        await RefreshNewbornNurseryAsync(newbornId);
        return newbornId;
    }

    public async Task RecordNewbornExamAsync(string newbornId, NewbornExam exam)
    {
        await Newborn(newbornId).RecordExamAsync(exam);
    }

    public async Task RecordNewbornScreeningAsync(
        string newbornId, NewbornScreeningType type, NewbornScreeningResult result, string valueText,
        DateTime? performedDate, string performedBy, string notes)
    {
        await Newborn(newbornId).RecordScreeningAsync(new NewbornScreeningEntry
        {
            ScreeningType = type,
            Result = result,
            ValueText = valueText,
            PerformedDate = performedDate,
            PerformedBy = performedBy,
            Notes = notes
        });
        await RefreshNewbornNurseryAsync(newbornId);
    }

    public async Task AddNewbornMeasurementAsync(
        string newbornId, DateTime measuredAt, int? weightGrams, NewbornFeedingType feedingType,
        decimal? bilirubinMgDl, string feedingNotes, string notes)
    {
        await Newborn(newbornId).AddMeasurementAsync(new NewbornMeasurement
        {
            MeasuredAt = measuredAt,
            WeightGrams = weightGrams,
            FeedingType = feedingType,
            BilirubinMgDl = bilirubinMgDl,
            FeedingNotes = feedingNotes,
            Notes = notes
        });
    }

    public async Task SetNewbornNurseryLevelAsync(string newbornId, NurseryLevelOfCare level, string reason)
    {
        await Newborn(newbornId).SetNurseryLevelAsync(level, reason);
        await RefreshNewbornNurseryAsync(newbornId);
    }

    public async Task TransferNewbornAsync(string newbornId, string toLocation, string reason)
    {
        await Newborn(newbornId).TransferAsync(toLocation, reason);
        await RefreshNewbornNurseryAsync(newbornId);
    }

    public async Task DischargeNewbornAsync(
        string newbornId, DateTime dischargeDateTime, int? dischargeWeightGrams, NewbornFeedingType dischargeFeeding,
        string disposition, string followUpPlan, bool carSeatTestPassed)
    {
        await Newborn(newbornId).DischargeAsync(dischargeDateTime, dischargeWeightGrams, dischargeFeeding, disposition, followUpPlan, carSeatTestPassed);
        await RefreshNewbornNurseryAsync(newbornId);
    }

    // ─── Reads (open) ───────────────────────────────────────────────────────

    public Task<NewbornState> GetNewbornAsync(string newbornId) => Newborn(newbornId).GetAsync();

    public async Task<List<NewbornState>> GetNewbornsForPregnancyAsync(string pregnancyId)
    {
        PregnancyState preg = await Pregnancy(pregnancyId).GetAsync();
        var result = new List<NewbornState>();
        foreach (string id in preg.NewbornIds)
            result.Add(await Newborn(id).GetAsync());
        return result;
    }

    /// <summary>All newborns delivered from this patient's pregnancies (newest first).</summary>
    public async Task<List<NewbornState>> GetNewbornsForMotherAsync()
    {
        var result = new List<NewbornState>();
        List<PregnancyIndexEntry> pregnancies = await GetPregnanciesAsync();
        foreach (PregnancyIndexEntry p in pregnancies)
        {
            PregnancyState preg = await Pregnancy(p.PregnancyId).GetAsync();
            foreach (string id in preg.NewbornIds)
                result.Add(await Newborn(id).GetAsync());
        }
        return result.OrderByDescending(n => n.BirthDateTime).ToList();
    }
}
