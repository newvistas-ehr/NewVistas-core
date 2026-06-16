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
/// Functional tests for the Contract Health Services (CHS / PRC)
/// authorization workflow. Covers the request → approve / deny lifecycle on
/// <see cref="IExternalReferralGrain"/> and the eligibility-gated workflow
/// path on <see cref="IPatientWorkflowGrain"/> (a CHS approval requires the
/// patient to hold the <c>IHS CHS</c> eligibility code, normally stamped by
/// <c>IhsTribalEligibilityPolicy</c> at registration).
///
/// Uses <see cref="SharedCluster"/> — site features (PATIENT_MERGE,
/// EXTERNAL_REFERRAL_TRACKING) need to be enabled per-test on the
/// site-parameters grain since the shared cluster doesn't pre-enable them.
/// </summary>
[TestFixture]
public class ChsAuthorizationTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
        ISiteParametersGrain siteParams =
            _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.EnableFeatureAsync("EXTERNAL_REFERRAL_TRACKING");
    }

    private IExternalReferralGrain Referral(string id) =>
        _cluster.GrainFactory.GetGrain<IExternalReferralGrain>(id);

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>Creates a fresh referral grain and seeds it with a generic external referral.</summary>
    private async Task<string> CreateReferralAsync(string patientId, string patientName)
    {
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        await Referral(referralId).CreateReferralAsync(
            patientId: patientId, patientName: patientName, referralType: "SPECIALTY",
            externalFacilityName: "Tulsa Cardiology Associates", externalFacilityId: null,
            externalProviderName: "Dr. Mendoza", externalProviderId: null,
            purpose: "Cardiology consult for new-onset chest pain", diagnosis: "R07.9",
            urgency: "ROUTINE",
            referredByProviderId: "DOCTOR1", referredByProviderName: "SMITH,JOHN",
            consultId: null, authorizationNumber: null,
            appointmentDateTime: null, specialInstructions: null);
        return referralId;
    }

    [Test]
    public async Task RequestChsAuthorization_MarksReferralPending()
    {
        string referralId = await CreateReferralAsync("PAT-CHS-1", "TRIBAL,PATIENT-1");

        await Referral(referralId).RequestChsAuthorizationAsync(
            estimatedCost: 1500m,
            medicalPriorityClass: "II",
            alternateResourcesChecked: true,
            alternateResourcesNote: "No alternate coverage on file",
            requestedByProviderId: "DOCTOR1",
            requestedByProviderName: "SMITH,JOHN");

        ExternalReferralState state = await Referral(referralId).GetReferralAsync();
        Assert.That(state.IsChsReferral, Is.True);
        Assert.That(state.Status, Is.EqualTo("PENDING_CHS_AUTH"));
        Assert.That(state.EstimatedCost, Is.EqualTo(1500m));
        Assert.That(state.MedicalPriorityClass, Is.EqualTo("II"));
        Assert.That(state.AlternateResourcesChecked, Is.True);
    }

    [Test]
    public async Task ApproveChsAuthorization_OnPendingRequest_AuthorizesAndStampsAmount()
    {
        string referralId = await CreateReferralAsync("PAT-CHS-2", "TRIBAL,PATIENT-2");
        await Referral(referralId).RequestChsAuthorizationAsync(
            estimatedCost: 2000m, medicalPriorityClass: "I",
            alternateResourcesChecked: true, alternateResourcesNote: null,
            requestedByProviderId: "DOCTOR1", requestedByProviderName: "SMITH,JOHN");

        await Referral(referralId).ApproveChsAuthorizationAsync(
            authorizedAmount: 2200m,
            authorizationNumber: "CHS-2026-00045",
            approvedById: "CHSCOORD1",
            approvedByName: "COORDINATOR,CHS");

        ExternalReferralState state = await Referral(referralId).GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo("AUTHORIZED"));
        Assert.That(state.AuthorizedAmount, Is.EqualTo(2200m));
        Assert.That(state.AuthorizationNumber, Is.EqualTo("CHS-2026-00045"));
        Assert.That(state.ChsAuthorizedById, Is.EqualTo("CHSCOORD1"));
        Assert.That(state.ChsAuthorizationDate, Is.Not.Null);
    }

    [Test]
    public async Task DenyChsAuthorization_OnPendingRequest_TransitionsToDenied()
    {
        string referralId = await CreateReferralAsync("PAT-CHS-3", "TRIBAL,PATIENT-3");
        await Referral(referralId).RequestChsAuthorizationAsync(
            estimatedCost: 5000m, medicalPriorityClass: "IV",
            alternateResourcesChecked: false, alternateResourcesNote: "Patient declined to disclose",
            requestedByProviderId: "DOCTOR1", requestedByProviderName: "SMITH,JOHN");

        await Referral(referralId).DenyChsAuthorizationAsync(
            denialReason: "Priority IV deferred for FY2026; alternate resources not verified.",
            deniedById: "CHSCOORD1",
            deniedByName: "COORDINATOR,CHS");

        ExternalReferralState state = await Referral(referralId).GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo("DENIED"));
        Assert.That(state.StatusReason, Does.Contain("Priority IV deferred"));
        Assert.That(state.RequiresFollowUp, Is.False);
        Assert.That(state.AuthorizedAmount, Is.Null);
    }

    [Test]
    public void ApproveChsAuthorization_WithoutPriorRequest_Throws()
    {
        // Create the referral but never call RequestChsAuthorizationAsync.
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        Assert.That(async () =>
        {
            await Referral(referralId).CreateReferralAsync(
                "PAT-CHS-4", "TRIBAL,PATIENT-4", "SPECIALTY",
                "External Clinic", null, null, null, "Eval", null, "ROUTINE",
                "DOC", "DOC", null, null, null, null);
            await Referral(referralId).ApproveChsAuthorizationAsync(
                100m, null, "CHS1", "CHS");
        }, Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task ApproveChsAuthorization_OnAlreadyAuthorizedReferral_Throws()
    {
        string referralId = await CreateReferralAsync("PAT-CHS-5", "TRIBAL,PATIENT-5");
        await Referral(referralId).RequestChsAuthorizationAsync(
            1000m, "II", true, null, "DOC", "DOC");
        await Referral(referralId).ApproveChsAuthorizationAsync(1000m, null, "CHS1", "CHS");

        // Second approval attempt — referral is already AUTHORIZED, not PENDING_CHS_AUTH.
        Assert.That(
            async () => await Referral(referralId).ApproveChsAuthorizationAsync(2000m, null, "CHS1", "CHS"),
            Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public void RequestChsAuthorization_NegativeEstimatedCost_Throws()
    {
        Assert.That(async () =>
        {
            string referralId = await CreateReferralAsync("PAT-CHS-6", "TRIBAL,PATIENT-6");
            await Referral(referralId).RequestChsAuthorizationAsync(
                estimatedCost: -1m, medicalPriorityClass: "II",
                alternateResourcesChecked: true, alternateResourcesNote: null,
                requestedByProviderId: "DOC", requestedByProviderName: "DOC");
        }, Throws.InstanceOf<ArgumentException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task ApproveChsAuthorization_ViaWorkflow_RequiresIhsChsEligibility()
    {
        // Patient has eligibility "IHS DIRECT" (direct-care only), NOT "IHS CHS".
        // The workflow guard must reject the approval even though the auth filter
        // would otherwise let it through.
        string patientId = $"PAT-DIRECT-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId)
            .UpdateDemographicsAsync("DIRECTCARE,ONLY", "M", new DateTime(1965, 1, 1), "111223333");
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId)
            .UpdateVeteranInfoAsync("N", null, "IHS DIRECT", "IHS DIRECT");

        string referralId = await CreateReferralAsync(patientId, "DIRECTCARE,ONLY");
        await Referral(referralId).RequestChsAuthorizationAsync(
            500m, "II", true, null, "DOC", "DOC");

        Assert.That(
            async () => await Workflow(patientId).ApproveChsAuthorizationAsync(
                referralId, 500m, null, "CHSCOORD", "COORD"),
            Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>(),
            "A direct-care-only patient must not be approvable for CHS funding.");
    }

    [Test]
    public async Task ApproveChsAuthorization_ViaWorkflow_AllowedWhenPatientIsChsEligible()
    {
        string patientId = $"PAT-CHS-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId)
            .UpdateDemographicsAsync("CHS,ELIGIBLE", "F", new DateTime(1970, 1, 1), "222334444");
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId)
            .UpdateVeteranInfoAsync("N", null, "IHS CHS", "IHS CHS");

        string referralId = await CreateReferralAsync(patientId, "CHS,ELIGIBLE");
        await Referral(referralId).RequestChsAuthorizationAsync(
            1500m, "I", true, null, "DOC", "DOC");

        await Workflow(patientId).ApproveChsAuthorizationAsync(
            referralId, 1500m, "CHS-2026-00099", "CHSCOORD", "COORD");

        ExternalReferralState state = await Referral(referralId).GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo("AUTHORIZED"));
        Assert.That(state.AuthorizedAmount, Is.EqualTo(1500m));
    }

    [Test]
    public async Task ChsApproval_PropagatesToReferralIndex()
    {
        string referralId = await CreateReferralAsync("PAT-CHS-IDX", "TRIBAL,IDX");
        await Referral(referralId).RequestChsAuthorizationAsync(
            800m, "III", true, null, "DOC", "DOC");
        await Referral(referralId).ApproveChsAuthorizationAsync(800m, null, "CHS1", "CHS");

        IExternalReferralIndexGrain idx =
            _cluster.GrainFactory.GetGrain<IExternalReferralIndexGrain>("EXT-REF-IDX");
        List<ExternalReferralIndexEntry> entries = await idx.GetByPatientAsync("PAT-CHS-IDX");

        ExternalReferralIndexEntry? hit = entries.FirstOrDefault(e => e.ReferralId == referralId);
        Assert.That(hit, Is.Not.Null);
        Assert.That(hit!.IsChsReferral, Is.True);
        Assert.That(hit.MedicalPriorityClass, Is.EqualTo("III"));
        Assert.That(hit.AuthorizedAmount, Is.EqualTo(800m));
        Assert.That(hit.Status, Is.EqualTo("AUTHORIZED"));
    }
}
