// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HomeCareCensusGrain : Grain, IHomeCareCensusGrain
{
    private readonly IPersistentState<HomeCareCensusState> _state;

    public HomeCareCensusGrain(
        [PersistentState("homeCareCensusState", "homeCareCensusStore")] IPersistentState<HomeCareCensusState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.SiteId))
            _state.State.SiteId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task UpsertEntryAsync(HomeCareCensusEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.EpisodeId == entry.EpisodeId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(string episodeId)
    {
        int idx = _state.State.Entries.FindIndex(e => e.EpisodeId == episodeId);
        if (idx < 0) return;
        _state.State.Entries.RemoveAt(idx);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<HomeCareCensusEntry>> GetAllAsync() =>
        Task.FromResult(_state.State.Entries.OrderBy(e => e.PatientName).ToList());

    public Task<List<HomeCareCensusEntry>> GetActiveAsync() =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.Status == HomeCareEpisodeStatus.Active || e.Status == HomeCareEpisodeStatus.OnHold)
            .OrderBy(e => e.PatientName)
            .ToList());

    public Task<List<HomeCareCensusEntry>> GetByLevelOfCareAsync(HomeCareLevelOfCare levelOfCare) =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.LevelOfCare == levelOfCare
                        && (e.Status == HomeCareEpisodeStatus.Active || e.Status == HomeCareEpisodeStatus.OnHold))
            .OrderBy(e => e.PatientName)
            .ToList());

    public Task<List<HomeCareCensusEntry>> GetByDeliveryModelAsync(HomeCareDeliveryModel deliveryModel) =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.DeliveryModel == deliveryModel
                        && (e.Status == HomeCareEpisodeStatus.Active || e.Status == HomeCareEpisodeStatus.OnHold))
            .OrderBy(e => e.PatientName)
            .ToList());

    public Task<List<HomeCareCensusEntry>> GetByProviderAsync(string providerId) =>
        Task.FromResult(_state.State.Entries
            .Where(e => e.PrimaryProviderId == providerId
                        && (e.Status == HomeCareEpisodeStatus.Active || e.Status == HomeCareEpisodeStatus.OnHold))
            .OrderBy(e => e.PatientName)
            .ToList());

    public Task<List<HomeCareCensusEntry>> GetWithUpcomingVisitsAsync(int withinDays)
    {
        DateTime now = DateTime.UtcNow;
        DateTime until = now.AddDays(withinDays);
        return Task.FromResult(_state.State.Entries
            .Where(e => e.Status == HomeCareEpisodeStatus.Active
                        && e.NextVisitDate.HasValue
                        && e.NextVisitDate.Value >= now && e.NextVisitDate.Value <= until)
            .OrderBy(e => e.NextVisitDate)
            .ToList());
    }

    public Task<List<HomeCareCensusEntry>> GetWithNoRecentVisitAsync(int daysSinceLastVisit)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-daysSinceLastVisit);
        return Task.FromResult(_state.State.Entries
            .Where(e => e.Status == HomeCareEpisodeStatus.Active
                        && (!e.LastVisitDate.HasValue || e.LastVisitDate.Value < cutoff))
            .OrderBy(e => e.LastVisitDate)
            .ToList());
    }

    public Task<HomeCareWorkloadStats> GetWorkloadStatsAsync()
    {
        DateTime now = DateTime.UtcNow;
        DateTime upcomingUntil = now.AddDays(7);
        DateTime noRecentCutoff = now.AddDays(-30);
        List<HomeCareCensusEntry> active = _state.State.Entries
            .Where(e => e.Status == HomeCareEpisodeStatus.Active || e.Status == HomeCareEpisodeStatus.OnHold)
            .ToList();

        var stats = new HomeCareWorkloadStats
        {
            ActiveEpisodes = active.Count(e => e.Status == HomeCareEpisodeStatus.Active),
            OnHoldEpisodes = active.Count(e => e.Status == HomeCareEpisodeStatus.OnHold),
            BasicCare = active.Count(e => e.LevelOfCare == HomeCareLevelOfCare.Basic),
            EnhancedCare = active.Count(e => e.LevelOfCare == HomeCareLevelOfCare.Enhanced),
            PalliativeCare = active.Count(e => e.LevelOfCare == HomeCareLevelOfCare.Palliative),
            NoRecentVisit = active.Count(e => e.Status == HomeCareEpisodeStatus.Active
                                              && (!e.LastVisitDate.HasValue || e.LastVisitDate.Value < noRecentCutoff)),
            UpcomingVisits = active.Count(e => e.NextVisitDate.HasValue
                                               && e.NextVisitDate.Value >= now && e.NextVisitDate.Value <= upcomingUntil)
        };
        return Task.FromResult(stats);
    }
}
