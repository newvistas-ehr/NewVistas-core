// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class MailGroupGrain : Grain, IMailGroupGrain
{
    private readonly IPersistentState<MailGroupState> _state;

    public MailGroupGrain(
        [PersistentState("mailGroupState", "mailGroupStore")]
        IPersistentState<MailGroupState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.GroupName))
        {
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<MailGroupState> GetGroupAsync() => Task.FromResult(_state.State);

    public async Task CreateGroupAsync(
        string groupName,
        string description,
        string organizerId,
        string organizerName,
        string membershipType)
    {
        _state.State.GroupName = groupName;
        _state.State.Description = description;
        _state.State.OrganizerId = organizerId;
        _state.State.OrganizerName = organizerName;
        _state.State.MembershipType = membershipType;
        _state.State.IsActive = true;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Add organizer as first member
        _state.State.Members.Add(new MailGroupMember
        {
            UserId = organizerId,
            UserName = organizerName,
            Role = "ORGANIZER",
            JoinedDate = DateTime.UtcNow
        });

        await _state.WriteStateAsync();
    }

    public async Task AddMemberAsync(string userId, string userName, string role)
    {
        if (!_state.State.Members.Any(m => m.UserId == userId))
        {
            _state.State.Members.Add(new MailGroupMember
            {
                UserId = userId,
                UserName = userName,
                Role = role,
                JoinedDate = DateTime.UtcNow
            });
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveMemberAsync(string userId)
    {
        MailGroupMember? member = _state.State.Members.FirstOrDefault(m => m.UserId == userId);
        if (member != null)
        {
            _state.State.Members.Remove(member);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateMemberRoleAsync(string userId, string role)
    {
        MailGroupMember? member = _state.State.Members.FirstOrDefault(m => m.UserId == userId);
        if (member != null)
        {
            member.Role = role;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<MailGroupMember>> GetMembersAsync() => Task.FromResult(_state.State.Members);
}
