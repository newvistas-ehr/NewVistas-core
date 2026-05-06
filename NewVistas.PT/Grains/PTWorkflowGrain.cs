// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Concurrency;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Grains;

/// <summary>
/// Orchestrates PT measurement workflows for a patient.
/// Stateless coordinator — no persistent state, delegates to session and index grains.
/// Reentrant to allow concurrent fan-out reads via Task.WhenAll.
/// </summary>
[Reentrant]
public class PTWorkflowGrain : Grain, IPTWorkflowGrain
{
    private string PatientId => this.GetPrimaryKeyString();

    public async Task<string> RecordBodyGroupSessionAsync(
        BodyGroup bodyGroup,
        DateTime sessionDate,
        string? therapistId,
        string? therapistName,
        string? locationId,
        string? locationName,
        Laterality side,
        List<RomMeasurement> romMeasurements,
        List<StrengthMeasurement> strengthMeasurements,
        string? notes)
    {
        string sessionKey = MakeSessionKey(bodyGroup, side, sessionDate);

        // Create and populate the session grain
        IPTSessionGrain sessionGrain = GrainFactory.GetGrain<IPTSessionGrain>(sessionKey);
        await sessionGrain.RecordSessionAsync(
            PatientId, bodyGroup, sessionDate,
            therapistId, therapistName,
            locationId, locationName,
            side, romMeasurements, strengthMeasurements, notes);

        // Update the body group index
        IPTSessionIndexGrain indexGrain = GetIndexGrain(bodyGroup);
        await indexGrain.AddSessionKeyAsync(sessionKey, sessionDate, bodyGroup, side);

        return sessionKey;
    }

    public async Task<List<PTSessionState>> GetLatestSessionsAsync(BodyGroup bodyGroup, int count = 2)
    {
        IPTSessionIndexGrain indexGrain = GetIndexGrain(bodyGroup);
        List<PTSessionIndexEntry> entries = await indexGrain.GetLastNSessionsAsync(count);

        if (entries.Count == 0)
            return new List<PTSessionState>();

        // Fan-out to load full session states concurrently
        Task<PTSessionState>[] tasks = entries
            .Select(e => GrainFactory.GetGrain<IPTSessionGrain>(e.SessionGrainKey).GetSessionAsync())
            .ToArray();

        PTSessionState[] results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public async Task<List<PTSessionState>> GetSessionHistoryAsync(
        BodyGroup bodyGroup, DateTime? from, DateTime? to, int maxCount = 50)
    {
        IPTSessionIndexGrain indexGrain = GetIndexGrain(bodyGroup);

        List<PTSessionIndexEntry> entries;
        if (from.HasValue && to.HasValue)
            entries = await indexGrain.GetSessionsByDateRangeAsync(from.Value, to.Value);
        else
            entries = await indexGrain.GetAllSessionsAsync();

        entries = entries.Take(maxCount).ToList();

        if (entries.Count == 0)
            return new List<PTSessionState>();

        Task<PTSessionState>[] tasks = entries
            .Select(e => GrainFactory.GetGrain<IPTSessionGrain>(e.SessionGrainKey).GetSessionAsync())
            .ToArray();

        PTSessionState[] results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public async Task<List<BodyGroup>> GetBodyGroupsWithDataAsync()
    {
        BodyGroup[] allGroups = Enum.GetValues<BodyGroup>();

        Task<int>[] countTasks = allGroups
            .Select(bg => GetIndexGrain(bg).GetCountAsync())
            .ToArray();

        int[] counts = await Task.WhenAll(countTasks);

        List<BodyGroup> result = new();
        for (int i = 0; i < allGroups.Length; i++)
        {
            if (counts[i] > 0)
                result.Add(allGroups[i]);
        }

        return result;
    }

    public Task<List<Movement>> GetStandardMovementsAsync(BodyGroup bodyGroup)
        => Task.FromResult(BodyGroupDefinitions.GetMovements(bodyGroup).ToList());

    // ── PT Goals ──────────────────────────────────────────────────────

    public Task<string> AddGoalAsync(BodyGroup bodyGroup, PTGoal goal)
        => GetGoalGrain(bodyGroup).AddGoalAsync(goal);

    public Task UpdateGoalAsync(BodyGroup bodyGroup, string goalId, GoalStatus? status, decimal? currentValue, string? notes)
        => GetGoalGrain(bodyGroup).UpdateGoalAsync(goalId, status, currentValue, notes);

    public Task AddGoalProgressAsync(BodyGroup bodyGroup, string goalId, decimal value, string? notes)
        => GetGoalGrain(bodyGroup).AddProgressEntryAsync(goalId, value, notes);

    public async Task<List<PTGoal>> GetGoalsForBodyGroupAsync(BodyGroup bodyGroup)
    {
        PTGoalState state = await GetGoalGrain(bodyGroup).GetGoalsAsync();
        return state.Goals;
    }

    public async Task<List<PTGoal>> GetAllActiveGoalsAsync()
    {
        BodyGroup[] allGroups = Enum.GetValues<BodyGroup>();

        Task<List<PTGoal>>[] tasks = allGroups
            .Select(bg => GetGoalGrain(bg).GetActiveGoalsAsync())
            .ToArray();

        List<PTGoal>[] results = await Task.WhenAll(tasks);
        return results.SelectMany(g => g).ToList();
    }

    // ── Clinic Exercises ──────────────────────────────────────────────

    public Task AddClinicExerciseAsync(string sessionKey, ClinicExerciseLog exercise)
        => GrainFactory.GetGrain<IPTSessionGrain>(sessionKey).AddExerciseLogAsync(exercise);

    // ── Home Exercise Program ─────────────────────────────────────────

    public Task<string> AddHepPrescriptionAsync(HepPrescription prescription)
        => GetHepGrain().AddPrescriptionAsync(prescription);

    public Task UpdateHepPrescriptionStatusAsync(string prescriptionId, HepStatus status)
        => GetHepGrain().UpdatePrescriptionStatusAsync(prescriptionId, status);

    public Task<string> LogHepCompletionAsync(HepCompletionLog log)
        => GetHepGrain().LogCompletionAsync(log);

    public Task<List<HepPrescription>> GetActiveHepPrescriptionsAsync()
        => GetHepGrain().GetActivePrescriptionsAsync();

    public Task<List<HepCompletionLog>> GetHepCompletionLogsAsync(string? prescriptionId, DateTime? from, DateTime? to)
        => GetHepGrain().GetCompletionLogsAsync(prescriptionId, from, to);

    // ── PT Referrals ─────────────────────────────────────────────────

    public async Task<string> CreateReferralAsync(
        string patientName,
        string? referringProviderName,
        string? referringProviderId,
        string? referringProviderSpecialty,
        string? referringFacilityName,
        string? diagnosis,
        string? diagnosisCode,
        List<BodyGroup>? bodyGroups,
        string? reasonForReferral,
        string? precautions,
        int authorizedVisits,
        DateTime? authorizationExpirationDate,
        DateTime referralDate,
        DateTime? receivedDate,
        string? notes)
    {
        string referralKey = $"PTREF:{PatientId}:{Guid.NewGuid()}";
        IPTReferralGrain grain = GetReferralGrain(referralKey);
        await grain.CreateReferralAsync(
            PatientId, patientName,
            referringProviderName, referringProviderId,
            referringProviderSpecialty, referringFacilityName,
            diagnosis, diagnosisCode, bodyGroups,
            reasonForReferral, precautions,
            authorizedVisits, authorizationExpirationDate,
            referralDate, receivedDate, notes);
        return referralKey;
    }

    public async Task<List<PTReferralState>> GetAllReferralsAsync()
    {
        IPTReferralIndexGrain index = GetReferralIndexGrain();
        List<PTReferralIndexEntry> entries = await index.GetAllReferralsAsync();
        if (entries.Count == 0) return new List<PTReferralState>();

        Task<PTReferralState>[] tasks = entries
            .Select(e => GetReferralGrain(e.ReferralGrainKey).GetReferralAsync())
            .ToArray();
        PTReferralState[] results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public async Task<List<PTReferralState>> GetActiveReferralsAsync()
    {
        IPTReferralIndexGrain index = GetReferralIndexGrain();
        List<PTReferralIndexEntry> entries = await index.GetActiveReferralsAsync();
        if (entries.Count == 0) return new List<PTReferralState>();

        Task<PTReferralState>[] tasks = entries
            .Select(e => GetReferralGrain(e.ReferralGrainKey).GetReferralAsync())
            .ToArray();
        PTReferralState[] results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public Task<PTReferralState> GetReferralAsync(string referralGrainKey)
        => GetReferralGrain(referralGrainKey).GetReferralAsync();

    public Task UpdateReferralStatusAsync(string referralGrainKey, PTReferralStatus status, string? notes)
        => GetReferralGrain(referralGrainKey).UpdateStatusAsync(status, notes);

    public Task UpdateReferralAuthorizationAsync(string referralGrainKey, int authorizedVisits, DateTime? expirationDate)
        => GetReferralGrain(referralGrainKey).UpdateAuthorizationAsync(authorizedVisits, expirationDate);

    public async Task<string> RecordBodyGroupSessionAsync(
        BodyGroup bodyGroup,
        DateTime sessionDate,
        string? therapistId,
        string? therapistName,
        string? locationId,
        string? locationName,
        Laterality side,
        List<RomMeasurement> romMeasurements,
        List<StrengthMeasurement> strengthMeasurements,
        string? notes,
        string? referralGrainKey)
    {
        // Delegate to existing method for the core recording
        string sessionKey = await RecordBodyGroupSessionAsync(
            bodyGroup, sessionDate, therapistId, therapistName,
            locationId, locationName, side,
            romMeasurements, strengthMeasurements, notes);

        // If a referral is specified, link the session and increment visit count
        if (!string.IsNullOrEmpty(referralGrainKey))
        {
            IPTSessionGrain sessionGrain = GrainFactory.GetGrain<IPTSessionGrain>(sessionKey);
            await sessionGrain.SetReferralAsync(referralGrainKey);

            IPTReferralGrain referralGrain = GetReferralGrain(referralGrainKey);
            await referralGrain.IncrementVisitCountAsync();
        }

        return sessionKey;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private string MakeSessionKey(BodyGroup bodyGroup, Laterality side, DateTime sessionDate)
        => $"PTSESSION:{PatientId}:{bodyGroup}:{side}:{sessionDate:yyyyMMddHHmmss}";

    private IPTSessionIndexGrain GetIndexGrain(BodyGroup bodyGroup)
        => GrainFactory.GetGrain<IPTSessionIndexGrain>($"PTINDEX:{PatientId}:{bodyGroup}");

    private IPTGoalGrain GetGoalGrain(BodyGroup bodyGroup)
        => GrainFactory.GetGrain<IPTGoalGrain>($"PTGOAL:{PatientId}:{bodyGroup}");

    private IPTHomeExerciseProgramGrain GetHepGrain()
        => GrainFactory.GetGrain<IPTHomeExerciseProgramGrain>($"PTHEP:{PatientId}");

    private IPTReferralGrain GetReferralGrain(string referralGrainKey)
        => GrainFactory.GetGrain<IPTReferralGrain>(referralGrainKey);

    private IPTReferralIndexGrain GetReferralIndexGrain()
        => GrainFactory.GetGrain<IPTReferralIndexGrain>($"PTREF-IDX:{PatientId}");
}
