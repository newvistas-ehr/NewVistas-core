// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Care Team Grain — manages the list of providers assigned to a patient's care team.
///
/// Derived from VistA PCMM (Primary Care Management Module), File #404.43 (Patient-Team Assignment).
/// Key: "CARE-TEAM:{patientId}"
/// </summary>
public class CareTeamGrain : Grain, ICareTeamGrain
{
    private readonly IPersistentState<CareTeamState> _state;

    public CareTeamGrain(
        [PersistentState("careTeam", "careTeamStore")] IPersistentState<CareTeamState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddMemberAsync(string providerId, string providerName, string role,
        string? specialty, string assignmentSource, DateTime? expirationDate)
    {
        CareTeamMember? existing = _state.State.Members
            .FirstOrDefault(m => m.ProviderId == providerId);

        if (existing != null)
        {
            // Reactivate if inactive, or update if active
            existing.ProviderName = providerName;
            existing.Role = role;
            existing.Specialty = specialty;
            existing.AssignmentSource = assignmentSource;
            existing.IsActive = true;
            existing.DeactivationDate = null;

            // Extend expiration if new date is later
            if (expirationDate.HasValue &&
                (!existing.ExpirationDate.HasValue || expirationDate.Value > existing.ExpirationDate.Value))
            {
                existing.ExpirationDate = expirationDate;
            }
        }
        else
        {
            _state.State.Members.Add(new CareTeamMember
            {
                ProviderId = providerId,
                ProviderName = providerName,
                Role = role,
                Specialty = specialty,
                AssignmentDate = DateTime.UtcNow,
                ExpirationDate = expirationDate,
                IsActive = true,
                AssignmentSource = assignmentSource
            });
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveMemberAsync(string providerId)
    {
        CareTeamMember? member = _state.State.Members
            .FirstOrDefault(m => m.ProviderId == providerId && m.IsActive);

        if (member != null)
        {
            member.IsActive = false;
            member.DeactivationDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<CareTeamMember>> GetMembersAsync()
        => Task.FromResult(_state.State.Members);

    public Task<List<CareTeamMember>> GetActiveMembersAsync()
        => Task.FromResult(_state.State.Members
            .Where(m => m.IsActive && (m.ExpirationDate == null || m.ExpirationDate > DateTime.UtcNow))
            .ToList());

    public async Task SetPcpAsync(string providerId, string providerName, string? specialty)
    {
        // Deactivate current PCP if different
        CareTeamMember? currentPcp = _state.State.Members
            .FirstOrDefault(m => m.Role == "PCP" && m.IsActive);

        if (currentPcp != null && currentPcp.ProviderId != providerId)
        {
            currentPcp.IsActive = false;
            currentPcp.DeactivationDate = DateTime.UtcNow;
        }

        // Add or update PCP
        CareTeamMember? existing = _state.State.Members
            .FirstOrDefault(m => m.ProviderId == providerId);

        if (existing != null)
        {
            existing.Role = "PCP";
            existing.ProviderName = providerName;
            existing.Specialty = specialty;
            existing.IsActive = true;
            existing.DeactivationDate = null;
            existing.ExpirationDate = null; // PCP has no expiration
        }
        else
        {
            _state.State.Members.Add(new CareTeamMember
            {
                ProviderId = providerId,
                ProviderName = providerName,
                Role = "PCP",
                Specialty = specialty,
                AssignmentDate = DateTime.UtcNow,
                ExpirationDate = null,
                IsActive = true,
                AssignmentSource = "MANUAL"
            });
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<CareTeamMember?> GetPcpAsync()
        => Task.FromResult(_state.State.Members
            .FirstOrDefault(m => m.Role == "PCP" && m.IsActive));

    public Task<bool> HasMemberAsync(string providerId)
        => Task.FromResult(_state.State.Members
            .Any(m => m.ProviderId == providerId));

    public Task<bool> HasActiveMemberAsync(string providerId)
        => Task.FromResult(_state.State.Members
            .Any(m => m.ProviderId == providerId && m.IsActive &&
                      (m.ExpirationDate == null || m.ExpirationDate > DateTime.UtcNow)));

    public Task<List<CareTeamMember>> GetMembersByRoleAsync(string role)
        => Task.FromResult(_state.State.Members
            .Where(m => m.Role == role && m.IsActive &&
                        (m.ExpirationDate == null || m.ExpirationDate > DateTime.UtcNow))
            .ToList());

    public async Task UpdateMemberLastSeenAsync(string providerId, DateTime lastSeenDate)
    {
        CareTeamMember? member = _state.State.Members
            .FirstOrDefault(m => m.ProviderId == providerId);

        if (member != null)
        {
            member.LastSeenDate = lastSeenDate;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }
}
