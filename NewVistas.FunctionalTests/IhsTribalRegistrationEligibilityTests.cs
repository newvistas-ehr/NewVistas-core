// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Eligibility;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;
using NewVistas.Abstractions.Security;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for the IHS / tribal registration flow:
/// registration with <see cref="IhsTribalEligibilityPolicy"/> wired into the
/// silo. Verifies that tribal hints on the <see cref="RegistrationRequest"/>
/// produce the expected eligibility code and enrollment record per
/// 38 CFR Part 136 (IHS Beneficiary Eligibility) tiers.
///
/// Uses a dedicated <see cref="TestCluster"/> (not <see cref="SharedCluster"/>)
/// so the IHS policy can be wired in without affecting other tests.
/// </summary>
[TestFixture, NonParallelizable]
public class IhsTribalRegistrationEligibilityTests
{
    public const string TestClusterPrefix = "910";
    public const string TestLocalClusterId = "TRIBAL-TEST-CLUSTER";

    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<IhsPolicySiloConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
    }

    private IPatientRegistrationGrain Registration() =>
        _cluster.GrainFactory.GetGrain<IPatientRegistrationGrain>("REGISTRATION");

    private static RegistrationRequest TribalRequest(
        bool isMember = true,
        string tribe = "Cherokee Nation",
        bool? residesInChsda = null,
        int? chsdaResidencyDays = null,
        string? eligibleByCategory = null)
    {
        string suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        return new RegistrationRequest
        {
            PatientName = $"TRIBAL,PATIENT-{suffix}",
            Ssn = "111223333",
            DateOfBirth = new DateTime(1965, 6, 12),
            Sex = "F",
            FacilityDfn = $"DFN-{suffix}",
            IsTribalMember = isMember,
            TribalAffiliation = tribe,
            ResidesInChsda = residesInChsda,
            ChsdaResidencyDays = chsdaResidencyDays,
            IhsEligibleByCategory = eligibleByCategory,
        };
    }

    [Test]
    public async Task Register_TribalMemberOnly_AssignsDirectCare()
    {
        // Tribal membership alone qualifies for direct care; no CHSDA residency.
        string icn = await Registration().RegisterPatientAsync(TribalRequest(isMember: true));

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Verified));
        Assert.That(enrollment.PriorityGroup, Is.EqualTo(IhsTribalEligibilityPolicy.DirectCarePriorityGroup));
        Assert.That(enrollment.CopayExempt, Is.True);
        Assert.That(enrollment.CopayExemptionReason, Is.EqualTo("IHS_BENEFICIARY"));
    }

    [Test]
    public async Task Register_TribalMemberWith180DayChsdaResidency_AssignsChs()
    {
        RegistrationRequest req = TribalRequest(
            isMember: true,
            residesInChsda: true,
            chsdaResidencyDays: 180);

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.PriorityGroup, Is.EqualTo(IhsTribalEligibilityPolicy.ChsPriorityGroup));
    }

    [Test]
    public async Task Register_TribalMemberWith179DayChsdaResidency_FallsBackToDirectCare()
    {
        // Just below the 180-day CHS residency threshold per 25 CFR § 136.23.
        RegistrationRequest req = TribalRequest(
            isMember: true,
            residesInChsda: true,
            chsdaResidencyDays: 179);

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.PriorityGroup, Is.EqualTo(IhsTribalEligibilityPolicy.DirectCarePriorityGroup),
            "Below the 180-day CHSDA residency floor, direct-care only.");
    }

    [Test]
    public async Task Register_TribalMemberOutsideChsda_DirectCareOnly()
    {
        RegistrationRequest req = TribalRequest(
            isMember: true,
            residesInChsda: false,
            chsdaResidencyDays: 365);  // residency days irrelevant if not in CHSDA

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.PriorityGroup, Is.EqualTo(IhsTribalEligibilityPolicy.DirectCarePriorityGroup));
    }

    [Test]
    public async Task Register_NonIndianEligibleByCategory_AssignsDirectCare()
    {
        // E.g., a non-Indian woman pregnant by an eligible Indian
        // (eligible during pregnancy + 6 weeks postpartum per 25 CFR § 136.12).
        RegistrationRequest req = TribalRequest(
            isMember: false,
            tribe: "",
            eligibleByCategory: "PREGNANT-NON-INDIAN");

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Verified));
        Assert.That(enrollment.PriorityGroup, Is.EqualTo(IhsTribalEligibilityPolicy.DirectCarePriorityGroup));
    }

    [Test]
    public async Task Register_NoTribalHints_LeavesEnrollmentUntouched()
    {
        // Self-pay or private-insurance walk-in at a tribal facility — neither
        // tribal member nor category-eligible. Patient still registered, but
        // no enrollment record.
        RegistrationRequest req = TribalRequest(isMember: false, tribe: "");
        req.IhsEligibleByCategory = null;

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Unverified));
        Assert.That(enrollment.PriorityGroup, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Register_TribalMember_StampsIhsEligibilityCodeOnPatientState()
    {
        string icn = await Registration().RegisterPatientAsync(TribalRequest());

        PatientState state = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn).GetPatientAsync();

        Assert.That(state.PrimaryEligibilityCode,
            Is.EqualTo(IhsTribalEligibilityPolicy.DirectCareCode)
              .Or.EqualTo(IhsTribalEligibilityPolicy.ChsEligibleCode));
        Assert.That(state.Veteran, Is.EqualTo("N"),
            "IHS patients are not veterans (unless a separate VA policy also runs).");
    }

    [Test]
    public async Task Register_TribalMember_RecordsTribeInPrioritySubgroup()
    {
        const string tribe = "Cherokee Nation";
        string icn = await Registration().RegisterPatientAsync(
            TribalRequest(isMember: true, tribe: tribe));

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.PrioritySubgroup, Is.EqualTo(tribe),
            "Tribal affiliation should be persisted on the enrollment record.");
    }

    [Test]
    public async Task Register_TribalMember_IcnStartsWithTribalPrefix()
    {
        // The tribal cluster boots with prefix 910 (per ClusterPrefixAllocations.md
        // 9xx block reserved for non-VA / IHS deployments). Every ICN the
        // registration grain issues from this silo should begin with that prefix.
        // This is the integration link between the tribal-profile cluster identity
        // and the downstream key-by-ICN grains (workflow, ADT, BCMA, beds, IV
        // admixture, etc.) — those grains are key-agnostic by design, so as long
        // as the ICN is well-formed, the inpatient flow validated under
        // SharedCluster (see InpatientStayEndToEndTests) composes transparently.
        string icn = await Registration().RegisterPatientAsync(TribalRequest(isMember: true));

        Assert.That(icn, Does.StartWith(TestClusterPrefix),
            $"Tribal-cluster ICN must begin with the cluster prefix '{TestClusterPrefix}'.");

        // ICN format pinned by ADR-001: {3-digit prefix}{7-digit seq}V{6-digit checksum}.
        Assert.That(icn, Does.Match(@"^\d{3}\d{7}V\d{6}$"),
            "ICN must match the ADR-001 canonical format.");
    }

    // ── Configuration plumbing ───────────────────────────────────────────────

    private sealed class IhsPolicySiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryGrainStorage("icnIssuerStore");
            siloBuilder.AddMemoryGrainStorage("mpiCorrelationStore");
            siloBuilder.AddMemoryGrainStorage("mpiSearchStore");
            siloBuilder.AddMemoryGrainStorage("patientStore");
            // Diagnosis provenance & revision statistics (ADR-006) — AddProblemAsync opens
            // a diagnostic episode, so any silo exercising the problem list needs these.
            siloBuilder.AddMemoryGrainStorage("dxEpisodeStore");
            siloBuilder.AddMemoryGrainStorage("dxOutcomeStore");
            siloBuilder.AddMemoryGrainStorage("patientHistoryIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");
            siloBuilder.AddMemoryGrainStorage("patientEnrollmentStore");
            siloBuilder.AddMemoryGrainStorage("autoEligibilityDeterminationStore");
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");

            siloBuilder.Services.AddSingleton<IClusterIdentity>(
                new StaticClusterIdentity(TestLocalClusterId, TestClusterPrefix));
            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, NullClinicalEventReplicationSink>();
            siloBuilder.Services.AddSingleton<IDrugInteractionCacheService, DrugInteractionCacheService>();

            // The thing under test: IHS-aligned eligibility policy.
            siloBuilder.Services.AddSingleton<IRegistrationEligibilityPolicy, IhsTribalEligibilityPolicy>();
            siloBuilder.Services.AddSingleton<IMpiFederationAnnouncer, NoOpMpiFederationAnnouncer>();
            siloBuilder.Services.AddSingleton<IMpiInboundHandler, DefaultMpiInboundHandler>();
        }
    }
}
