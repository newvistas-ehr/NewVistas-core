// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// A Person-anchored household (grain key <c>HOUSEHOLD:{guid}</c>). Owns its members and fans out to
/// each member's <see cref="IPersonHouseholdIndexGrain"/> as they join and leave.
/// </summary>
public class HouseholdGrain : Grain, IHouseholdGrain
{
    private readonly IPersistentState<HouseholdState> _state;

    public HouseholdGrain(
        [PersistentState("householdState", "householdStore")]
        IPersistentState<HouseholdState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.HouseholdId))
            _state.State.HouseholdId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateAsync(string label, string byUser)
    {
        bool isNew = _state.State.CreatedDate == default;
        _state.State.Label = label;
        if (isNew)
            _state.State.CreatedDate = DateTime.UtcNow;
        Log(byUser, $"Household '{label}' created");
        await SaveAsync();
    }

    public async Task AddMemberAsync(string personId, string displayName, string relationship,
        HouseholdMemberRole role, string byUser)
    {
        if (string.IsNullOrWhiteSpace(personId))
            return;

        HouseholdMember? active = ActiveMember(personId);
        if (active is not null)
        {
            // Already active — update the descriptive fields only.
            active.DisplayName = displayName;
            active.Relationship = relationship;
            active.Role = role;
        }
        else
        {
            _state.State.Members.Add(new HouseholdMember
            {
                PersonId = personId,
                DisplayName = displayName,
                Relationship = relationship,
                Role = role,
                JoinedDate = DateTime.UtcNow
            });
            await Index(personId).AddLinkAsync(_state.State.HouseholdId, DateTime.UtcNow);
        }

        // First member, or an explicit head, becomes head of household.
        if (role == HouseholdMemberRole.HeadOfHousehold || string.IsNullOrEmpty(_state.State.HeadOfHouseholdPersonId))
            _state.State.HeadOfHouseholdPersonId = personId;

        Log(byUser, $"{displayName} ({relationship}) added");
        await SaveAsync();
    }

    public async Task RemoveMemberAsync(string personId, string byUser)
    {
        HouseholdMember? active = ActiveMember(personId);
        if (active is null)
            return;

        active.LeftDate = DateTime.UtcNow;
        await Index(personId).CloseLinkAsync(_state.State.HouseholdId, active.LeftDate.Value);

        if (_state.State.HeadOfHouseholdPersonId == personId)
            _state.State.HeadOfHouseholdPersonId = string.Empty; // head left — reassignment is a deliberate act

        Log(byUser, $"{active.DisplayName} left the household");
        await SaveAsync();
    }

    public async Task SetHeadAsync(string personId, string byUser)
    {
        if (ActiveMember(personId) is null)
            throw new InvalidOperationException("Head of household must be an active member.");
        _state.State.HeadOfHouseholdPersonId = personId;
        Log(byUser, "Head of household set");
        await SaveAsync();
    }

    public async Task SetHousingAsync(HouseholdHousingType housingType, string? street, string? city, string? state, string? zip, string byUser)
    {
        _state.State.HousingType = housingType;
        _state.State.StreetAddress = street;
        _state.State.City = city;
        _state.State.State = state;
        _state.State.ZipCode = zip;
        Log(byUser, $"Housing set to {housingType}");
        await SaveAsync();
    }

    public Task<HouseholdState> GetAsync() => Task.FromResult(_state.State);

    // ─── Internals ──────────────────────────────────────────────────────

    private HouseholdMember? ActiveMember(string personId) =>
        _state.State.Members.FirstOrDefault(m => m.PersonId == personId && m.LeftDate is null);

    private IPersonHouseholdIndexGrain Index(string personId) =>
        GrainFactory.GetGrain<IPersonHouseholdIndexGrain>($"PERSON-HOUSEHOLD-IDX:{personId}");

    private void Log(string user, string detail) =>
        _state.State.ChangeLog.Add(new HouseholdChangeLogEntry { Timestamp = DateTime.UtcNow, User = user, Detail = detail });

    private async Task SaveAsync()
    {
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>Reverse index of a Person's household memberships (grain key <c>PERSON-HOUSEHOLD-IDX:{personId}</c>).</summary>
public class PersonHouseholdIndexGrain : Grain, IPersonHouseholdIndexGrain
{
    private readonly IPersistentState<PersonHouseholdIndexState> _state;

    public PersonHouseholdIndexGrain(
        [PersistentState("personHouseholdIndexState", "personHouseholdIndexStore")]
        IPersistentState<PersonHouseholdIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PersonId))
        {
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.PersonId = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddLinkAsync(string householdId, DateTime joinedDate)
    {
        if (_state.State.Links.Any(l => l.HouseholdId == householdId && l.LeftDate is null))
            return; // already an open link
        _state.State.Links.Add(new PersonHouseholdLink { HouseholdId = householdId, JoinedDate = joinedDate });
        await _state.WriteStateAsync();
    }

    public async Task CloseLinkAsync(string householdId, DateTime leftDate)
    {
        PersonHouseholdLink? open = _state.State.Links.FirstOrDefault(l => l.HouseholdId == householdId && l.LeftDate is null);
        if (open is null)
            return;
        open.LeftDate = leftDate;
        await _state.WriteStateAsync();
    }

    public Task<string?> GetCurrentHouseholdIdAsync() =>
        Task.FromResult(_state.State.Links
            .Where(l => l.LeftDate is null)
            .OrderByDescending(l => l.JoinedDate)
            .Select(l => l.HouseholdId)
            .FirstOrDefault());

    public Task<PersonHouseholdIndexState> GetAsync() => Task.FromResult(_state.State);
}
