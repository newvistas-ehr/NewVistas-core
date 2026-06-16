// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Personal Insurance Policies — VistA File #355.7.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class PersonalPolicyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── 1 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AddPersonalPolicy_ReturnsNonEmptyId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string policyId = await wf.AddPersonalPolicyAsync(
            null, "Aetna PPO", "MBR-001", null, null,
            null, null, null, true, null, null, null);

        Assert.That(policyId, Is.Not.Null.And.Not.Empty);
    }

    // ── 2 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AddPersonalPolicy_StoresGroupPlanName()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string policyId = await wf.AddPersonalPolicyAsync(
            null, "Blue Cross Blue Shield", "MBR-002", null, null,
            null, null, null, true, null, null, null);

        PersonalPolicyState state = await wf.GetPersonalPolicyAsync(policyId);
        Assert.That(state.GroupPlanName, Is.EqualTo("Blue Cross Blue Shield"));
    }

    // ── 3 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AddPersonalPolicy_StoresSubscriberInfo()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string policyId = await wf.AddPersonalPolicyAsync(
            null, "Cigna HMO", "SUB-12345", "John Doe", "SELF",
            null, null, null, true, null, null, null);

        PersonalPolicyState state = await wf.GetPersonalPolicyAsync(policyId);
        Assert.That(state.SubscriberId, Is.EqualTo("SUB-12345"));
        Assert.That(state.SubscriberName, Is.EqualTo("John Doe"));
    }

    // ── 4 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetPersonalPolicy_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime effectiveDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string policyId = await wf.AddPersonalPolicyAsync(
            "PLAN-100", "United Healthcare", "UHC-99999", "Jane Smith", "SPOUSE",
            effectiveDate, new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            "FAMILY", true, 25.00m, "PHARM-001", "Employer-sponsored family plan");

        PersonalPolicyState state = await wf.GetPersonalPolicyAsync(policyId);
        Assert.That(state.CoverageType, Is.EqualTo("FAMILY"));
        Assert.That(state.IsPrimary, Is.True);
        Assert.That(state.CopayAmount, Is.EqualTo(25.00m));
        Assert.That(state.EffectiveDate, Is.EqualTo(effectiveDate));
    }

    // ── 5 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetPersonalPolicies_ReturnsIndexList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddPersonalPolicyAsync(
            null, "Aetna PPO", "MBR-A1", null, null,
            null, null, null, true, null, null, null);
        await wf.AddPersonalPolicyAsync(
            null, "Delta Dental", "MBR-A2", null, null,
            null, null, null, false, null, null, null);

        List<PersonalPolicyIndexEntry> policies = await wf.GetPersonalPoliciesAsync();
        Assert.That(policies, Has.Count.GreaterThanOrEqualTo(2));
    }

    // ── 6 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task DeactivatePersonalPolicy_SetsInactive()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string policyId = await wf.AddPersonalPolicyAsync(
            null, "Humana Gold", "MBR-D1", null, null,
            null, null, null, true, null, null, null);

        await wf.DeactivatePersonalPolicyAsync(policyId);

        PersonalPolicyState state = await wf.GetPersonalPolicyAsync(policyId);
        Assert.That(state.IsActive, Is.False);
    }

    // ── 7 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task MultiplePolicies_PrimaryAndSecondary()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddPersonalPolicyAsync(
            null, "Blue Cross Primary", "MBR-P1", null, null,
            null, null, null, true, null, null, null);
        await wf.AddPersonalPolicyAsync(
            null, "MetLife Secondary", "MBR-S1", null, null,
            null, null, null, false, null, null, null);

        List<PersonalPolicyIndexEntry> policies = await wf.GetPersonalPoliciesAsync();
        Assert.That(policies, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(policies.Any(p => p.IsPrimary), Is.True);
        Assert.That(policies.Any(p => !p.IsPrimary), Is.True);
    }

    // ── 8 ────────────────────────────────────────────────────────────────────

    [Test]
    public async Task FullPolicyLifecycle_AddGetDeactivate()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Add
        string policyId = await wf.AddPersonalPolicyAsync(
            "PLAN-LC", "Kaiser Permanente", "KP-77777", "Alice Veteran", "SELF",
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), null,
            "INDIVIDUAL", true, 15.00m, null, "Annual enrollment");

        Assert.That(policyId, Is.Not.Null.And.Not.Empty);

        // Get
        PersonalPolicyState state = await wf.GetPersonalPolicyAsync(policyId);
        Assert.That(state.GroupPlanName, Is.EqualTo("Kaiser Permanente"));
        Assert.That(state.SubscriberId, Is.EqualTo("KP-77777"));
        Assert.That(state.IsActive, Is.True);

        // Deactivate
        await wf.DeactivatePersonalPolicyAsync(policyId);

        PersonalPolicyState deactivated = await wf.GetPersonalPolicyAsync(policyId);
        Assert.That(deactivated.IsActive, Is.False);

        // Verify index reflects deactivation
        List<PersonalPolicyIndexEntry> policies = await wf.GetPersonalPoliciesAsync();
        PersonalPolicyIndexEntry? entry = policies.FirstOrDefault(p => p.PolicyId == policyId);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.IsActive, Is.False);
    }
}
