// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class AccessControlWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IAccessControlGrain NewAclGrain() =>
        _cluster.GrainFactory.GetGrain<IAccessControlGrain>($"ACL:{Guid.NewGuid()}");

    // ─── Security Key Tests ─────────────────────────────────────────────

    [Test]
    public async Task AccessControl_DefaultState_NoKeysNoSession()
    {
        IAccessControlGrain grain = NewAclGrain();

        AccessControlState state = await grain.GetAccessControlStateAsync();

        Assert.That(state.SecurityKeys, Has.Count.EqualTo(0));
        Assert.That(state.HasActiveSession, Is.False);
        Assert.That(state.UserId, Is.Not.Empty);
    }

    [Test]
    public async Task AccessControl_GrantKey_AddsKeyAndAudit()
    {
        IAccessControlGrain grain = NewAclGrain();

        await grain.GrantKeyAsync(SecurityKeys.ORES, "ADMIN-1", "SMITH,ADMIN", "Initial provisioning");

        IReadOnlySet<string> keys = await grain.GetKeysAsync();
        Assert.That(keys, Contains.Item(SecurityKeys.ORES));
        Assert.That(keys, Has.Count.EqualTo(1));

        List<SecurityKeyAuditEntry> audit = await grain.GetKeyAuditLogAsync();
        Assert.That(audit, Has.Count.EqualTo(1));
        Assert.That(audit[0].Action, Is.EqualTo("GRANT"));
        Assert.That(audit[0].KeyName, Is.EqualTo(SecurityKeys.ORES));
        Assert.That(audit[0].PerformedByUserId, Is.EqualTo("ADMIN-1"));
    }

    [Test]
    public async Task AccessControl_GrantKey_Idempotent()
    {
        IAccessControlGrain grain = NewAclGrain();

        await grain.GrantKeyAsync(SecurityKeys.PROVIDER, "ADMIN-1", "SMITH,ADMIN");
        await grain.GrantKeyAsync(SecurityKeys.PROVIDER, "ADMIN-1", "SMITH,ADMIN");

        IReadOnlySet<string> keys = await grain.GetKeysAsync();
        Assert.That(keys, Has.Count.EqualTo(1));

        List<SecurityKeyAuditEntry> audit = await grain.GetKeyAuditLogAsync();
        Assert.That(audit, Has.Count.EqualTo(1)); // second grant was no-op
    }

    [Test]
    public async Task AccessControl_RevokeKey_RemovesKeyAndAudits()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.GrantKeyAsync(SecurityKeys.LRLAB, "ADMIN-1", "SMITH,ADMIN");

        await grain.RevokeKeyAsync(SecurityKeys.LRLAB, "ADMIN-1", "SMITH,ADMIN", "No longer assigned to lab");

        IReadOnlySet<string> keys = await grain.GetKeysAsync();
        Assert.That(keys, Has.Count.EqualTo(0));

        List<SecurityKeyAuditEntry> audit = await grain.GetKeyAuditLogAsync();
        Assert.That(audit, Has.Count.EqualTo(2)); // grant + revoke
        Assert.That(audit[1].Action, Is.EqualTo("REVOKE"));
    }

    [Test]
    public async Task AccessControl_RevokeKey_Idempotent()
    {
        IAccessControlGrain grain = NewAclGrain();

        await grain.RevokeKeyAsync(SecurityKeys.XUPROG, "ADMIN-1", "SMITH,ADMIN");

        List<SecurityKeyAuditEntry> audit = await grain.GetKeyAuditLogAsync();
        Assert.That(audit, Has.Count.EqualTo(0)); // nothing to revoke
    }

    [Test]
    public async Task AccessControl_HasKey_ReturnsTrueWhenHeld()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.GrantKeyAsync(SecurityKeys.ORES, "ADMIN-1", "SMITH,ADMIN");

        bool hasOres = await grain.HasKeyAsync(SecurityKeys.ORES);
        bool hasProvider = await grain.HasKeyAsync(SecurityKeys.PROVIDER);

        Assert.That(hasOres, Is.True);
        Assert.That(hasProvider, Is.False);
    }

    [Test]
    public async Task AccessControl_HasAnyKey_OrLogic()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.GrantKeyAsync(SecurityKeys.ORELSE, "ADMIN-1", "SMITH,ADMIN");

        bool hasAny = await grain.HasAnyKeyAsync(new[] { SecurityKeys.ORES, SecurityKeys.ORELSE });
        bool hasNone = await grain.HasAnyKeyAsync(new[] { SecurityKeys.XUPROG, SecurityKeys.LRLAB });

        Assert.That(hasAny, Is.True);
        Assert.That(hasNone, Is.False);
    }

    [Test]
    public async Task AccessControl_HasAllKeys_AndLogic()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.GrantKeyAsync(SecurityKeys.ORES, "ADMIN-1", "SMITH,ADMIN");
        await grain.GrantKeyAsync(SecurityKeys.PROVIDER, "ADMIN-1", "SMITH,ADMIN");

        bool hasAll = await grain.HasAllKeysAsync(new[] { SecurityKeys.ORES, SecurityKeys.PROVIDER });
        bool missingOne = await grain.HasAllKeysAsync(new[] { SecurityKeys.ORES, SecurityKeys.LRLAB });

        Assert.That(hasAll, Is.True);
        Assert.That(missingOne, Is.False);
    }

    [Test]
    public async Task AccessControl_SetKeys_BulkReplace()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.GrantKeyAsync(SecurityKeys.ORES, "ADMIN-1", "SMITH,ADMIN");
        await grain.GrantKeyAsync(SecurityKeys.PROVIDER, "ADMIN-1", "SMITH,ADMIN");

        // Replace with a different set
        await grain.SetKeysAsync(
            new List<string> { SecurityKeys.LRLAB, SecurityKeys.LRVERIFY },
            "ADMIN-1", "SMITH,ADMIN", "Role change to lab tech");

        IReadOnlySet<string> keys = await grain.GetKeysAsync();
        Assert.That(keys, Has.Count.EqualTo(2));
        Assert.That(keys, Contains.Item(SecurityKeys.LRLAB));
        Assert.That(keys, Contains.Item(SecurityKeys.LRVERIFY));
        Assert.That(keys, Does.Not.Contain(SecurityKeys.ORES));

        // Audit: 2 initial grants + 2 revokes (ORES, PROVIDER) + 2 grants (LRLAB, LRVERIFY)
        List<SecurityKeyAuditEntry> audit = await grain.GetKeyAuditLogAsync();
        Assert.That(audit, Has.Count.EqualTo(6));
    }

    // ─── Session Management Tests ───────────────────────────────────────

    [Test]
    public async Task AccessControl_StartSession_ActivatesSession()
    {
        IAccessControlGrain grain = NewAclGrain();

        await grain.StartSessionAsync("DIV-500", "BOSTON VAMC", "WORKSTATION-1", "10.0.0.50");

        AccessControlState state = await grain.GetAccessControlStateAsync();
        Assert.That(state.HasActiveSession, Is.True);
        Assert.That(state.SessionDivisionId, Is.EqualTo("DIV-500"));
        Assert.That(state.SessionDivisionName, Is.EqualTo("BOSTON VAMC"));
        Assert.That(state.ClientDevice, Is.EqualTo("WORKSTATION-1"));
        Assert.That(state.ClientIpAddress, Is.EqualTo("10.0.0.50"));
        Assert.That(state.SessionStartTime, Is.Not.Null);
        Assert.That(state.LastActivityTime, Is.Not.Null);
    }

    [Test]
    public async Task AccessControl_EndSession_DeactivatesSession()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.StartSessionAsync(null, null, null, null);

        await grain.EndSessionAsync();

        bool isActive = await grain.IsSessionActiveAsync();
        Assert.That(isActive, Is.False);
    }

    [Test]
    public async Task AccessControl_IsSessionActive_ChecksTimeout()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.StartSessionAsync(null, null, null, null);

        bool isActive = await grain.IsSessionActiveAsync();
        Assert.That(isActive, Is.True);
    }

    [Test]
    public async Task AccessControl_TouchSession_UpdatesLastActivity()
    {
        IAccessControlGrain grain = NewAclGrain();
        await grain.StartSessionAsync(null, null, null, null);
        AccessControlState before = await grain.GetAccessControlStateAsync();
        DateTime firstActivity = before.LastActivityTime ?? DateTime.MinValue;

        // Small delay to ensure time difference
        await Task.Delay(50);
        await grain.TouchSessionAsync();

        AccessControlState after = await grain.GetAccessControlStateAsync();
        Assert.That(after.LastActivityTime, Is.GreaterThanOrEqualTo(firstActivity));
    }

    [Test]
    public async Task AccessControl_SetSessionTimeout_Configurable()
    {
        IAccessControlGrain grain = NewAclGrain();

        await grain.SetSessionTimeoutAsync(30);

        AccessControlState state = await grain.GetAccessControlStateAsync();
        Assert.That(state.SessionTimeoutMinutes, Is.EqualTo(30));
    }

    [Test]
    public async Task AccessControl_NoSession_IsSessionActiveFalse()
    {
        IAccessControlGrain grain = NewAclGrain();

        bool isActive = await grain.IsSessionActiveAsync();

        Assert.That(isActive, Is.False);
    }

    // ─── Combined Key + Session Test ────────────────────────────────────

    [Test]
    public async Task AccessControl_FullProviderSetup_KeysAndSession()
    {
        IAccessControlGrain grain = NewAclGrain();

        // Provision a provider with typical keys
        await grain.SetKeysAsync(
            new List<string> { SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.TIU_SIGN, SecurityKeys.GMRA_ALLERGY },
            "ADMIN-1", "SMITH,ADMIN", "New provider onboarding");

        // Start session
        await grain.StartSessionAsync("DIV-500", "BOSTON VAMC", "CPRS-WS-42", "10.0.0.42");

        // Verify full state
        AccessControlState state = await grain.GetAccessControlStateAsync();
        Assert.That(state.SecurityKeys, Has.Count.EqualTo(4));
        Assert.That(state.HasActiveSession, Is.True);
        Assert.That(await grain.HasKeyAsync(SecurityKeys.ORES), Is.True);
        Assert.That(await grain.HasKeyAsync(SecurityKeys.PSO_PHARMACY), Is.False);
        Assert.That(await grain.IsSessionActiveAsync(), Is.True);
    }
}
