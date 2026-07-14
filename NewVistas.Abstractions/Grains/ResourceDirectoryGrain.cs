// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>The community-resource directory (grain key <c>RESOURCE-DIRECTORY</c>).</summary>
public class ResourceDirectoryGrain : Grain, IResourceDirectoryGrain
{
    private readonly IPersistentState<ResourceDirectoryState> _state;

    public ResourceDirectoryGrain(
        [PersistentState("resourceDirectoryState", "resourceDirectoryStore")]
        IPersistentState<ResourceDirectoryState> state)
    {
        _state = state;
    }

    public async Task<string> AddOrUpdateAsync(CommunityResource resource)
    {
        string id = string.IsNullOrWhiteSpace(resource.ResourceId) ? Guid.NewGuid().ToString() : resource.ResourceId;
        CommunityResource entry = resource with { ResourceId = id };
        _state.State.Resources.RemoveAll(r => r.ResourceId == id);
        _state.State.Resources.Add(entry);
        await _state.WriteStateAsync();
        return id;
    }

    public async Task RemoveAsync(string resourceId)
    {
        if (_state.State.Resources.RemoveAll(r => r.ResourceId == resourceId) > 0)
            await _state.WriteStateAsync();
    }

    public Task<CommunityResource?> GetAsync(string resourceId) =>
        Task.FromResult(_state.State.Resources.FirstOrDefault(r => r.ResourceId == resourceId));

    public Task<List<CommunityResource>> GetAllAsync() =>
        Task.FromResult(_state.State.Resources.Where(r => r.IsActive)
            .OrderBy(r => r.ServiceType).ThenBy(r => r.Name).ToList());

    public Task<List<CommunityResource>> SearchAsync(SocialWorkReferralServiceType? serviceType, string? text)
    {
        IEnumerable<CommunityResource> q = _state.State.Resources.Where(r => r.IsActive);
        if (serviceType is { } st)
            q = q.Where(r => r.ServiceType == st);
        if (!string.IsNullOrWhiteSpace(text))
        {
            string t = text.Trim().ToLowerInvariant();
            q = q.Where(r =>
                (r.Name?.ToLowerInvariant().Contains(t) ?? false)
                || (r.Description?.ToLowerInvariant().Contains(t) ?? false)
                || (r.ServiceArea?.ToLowerInvariant().Contains(t) ?? false)
                || (r.City?.ToLowerInvariant().Contains(t) ?? false));
        }
        return Task.FromResult(q.OrderBy(r => r.ServiceType).ThenBy(r => r.Name).ToList());
    }
}
