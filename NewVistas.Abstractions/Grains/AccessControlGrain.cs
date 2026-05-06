// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Access Control Grain — per-user security keys and session management.
///
/// Grain Key: "ACL:{userId}"
/// Store: "accessControlStore"
///
/// This grain is the single enforcement point for authorization.
/// The AuthorizationCallFilter queries it on every grain call (with caching).
///
/// VistA heritage:
///   $$HASKEY^XUSRB — security key check
///   SETUP^XUSRB    — session initialization
///   File #200.051  — KEYS multiple
///   File #3.081    — Sign-on Log
/// </summary>
public class AccessControlGrain : Grain, IAccessControlGrain
{
    private readonly IPersistentState<AccessControlState> _state;

    public AccessControlGrain(
        [PersistentState("accessControl", "accessControlStore")]
        IPersistentState<AccessControlState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.UserId))
        {
            // Extract userId from grain key "ACL:{userId}"
            string key = this.GetPrimaryKeyString();
            _state.State.UserId = key.StartsWith("ACL:") ? key[4..] : key;
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    // ─── Security Keys ──────────────────────────────────────────────────

    public Task<bool> HasKeyAsync(string keyName) =>
        Task.FromResult(_state.State.SecurityKeys.Contains(keyName));

    public Task<bool> HasAnyKeyAsync(IEnumerable<string> keyNames) =>
        Task.FromResult(keyNames.Any(k => _state.State.SecurityKeys.Contains(k)));

    public Task<bool> HasAllKeysAsync(IEnumerable<string> keyNames) =>
        Task.FromResult(keyNames.All(k => _state.State.SecurityKeys.Contains(k)));

    public Task<IReadOnlySet<string>> GetKeysAsync() =>
        Task.FromResult<IReadOnlySet<string>>(_state.State.SecurityKeys);

    public async Task GrantKeyAsync(string keyName, string grantedByUserId, string grantedByName, string? reason = null)
    {
        if (_state.State.SecurityKeys.Add(keyName))
        {
            _state.State.KeyAuditLog.Add(new SecurityKeyAuditEntry
            {
                KeyName = keyName,
                Action = "GRANT",
                PerformedByUserId = grantedByUserId,
                PerformedByName = grantedByName,
                ActionDateTime = DateTime.UtcNow,
                Reason = reason
            });
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RevokeKeyAsync(string keyName, string revokedByUserId, string revokedByName, string? reason = null)
    {
        if (_state.State.SecurityKeys.Remove(keyName))
        {
            _state.State.KeyAuditLog.Add(new SecurityKeyAuditEntry
            {
                KeyName = keyName,
                Action = "REVOKE",
                PerformedByUserId = revokedByUserId,
                PerformedByName = revokedByName,
                ActionDateTime = DateTime.UtcNow,
                Reason = reason
            });
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task SetKeysAsync(List<string> keys, string setByUserId, string setByName, string? reason = null)
    {
        HashSet<string> newKeys = new(keys);
        HashSet<string> currentKeys = _state.State.SecurityKeys;
        DateTime now = DateTime.UtcNow;

        // Record revocations for keys being removed
        foreach (string removed in currentKeys.Except(newKeys))
        {
            _state.State.KeyAuditLog.Add(new SecurityKeyAuditEntry
            {
                KeyName = removed,
                Action = "REVOKE",
                PerformedByUserId = setByUserId,
                PerformedByName = setByName,
                ActionDateTime = now,
                Reason = reason ?? "Bulk key reassignment"
            });
        }

        // Record grants for new keys being added
        foreach (string added in newKeys.Except(currentKeys))
        {
            _state.State.KeyAuditLog.Add(new SecurityKeyAuditEntry
            {
                KeyName = added,
                Action = "GRANT",
                PerformedByUserId = setByUserId,
                PerformedByName = setByName,
                ActionDateTime = now,
                Reason = reason ?? "Bulk key reassignment"
            });
        }

        _state.State.SecurityKeys = newKeys;
        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();
    }

    public Task<List<SecurityKeyAuditEntry>> GetKeyAuditLogAsync() =>
        Task.FromResult(_state.State.KeyAuditLog);

    // ─── Session Management ─────────────────────────────────────────────

    public async Task StartSessionAsync(string? divisionId, string? divisionName, string? clientDevice, string? clientIpAddress)
    {
        DateTime now = DateTime.UtcNow;
        _state.State.HasActiveSession = true;
        _state.State.SessionStartTime = now;
        _state.State.LastActivityTime = now;
        _state.State.SessionDivisionId = divisionId;
        _state.State.SessionDivisionName = divisionName;
        _state.State.ClientDevice = clientDevice;
        _state.State.ClientIpAddress = clientIpAddress;
        _state.State.LastModifiedDate = now;
        await _state.WriteStateAsync();
    }

    public async Task EndSessionAsync()
    {
        _state.State.HasActiveSession = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task TouchSessionAsync()
    {
        if (_state.State.HasActiveSession)
        {
            _state.State.LastActivityTime = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> IsSessionActiveAsync()
    {
        if (!_state.State.HasActiveSession)
            return Task.FromResult(false);

        if (_state.State.LastActivityTime == null)
            return Task.FromResult(false);

        bool expired = DateTime.UtcNow - _state.State.LastActivityTime.Value >
                        TimeSpan.FromMinutes(_state.State.SessionTimeoutMinutes);

        return Task.FromResult(!expired);
    }

    public Task<AccessControlState> GetAccessControlStateAsync() =>
        Task.FromResult(_state.State);

    public async Task SetSessionTimeoutAsync(int timeoutMinutes)
    {
        _state.State.SessionTimeoutMinutes = timeoutMinutes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
