// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for the class→patient reverse index. Exercises the live path:
/// prescription lifecycle (PharmacyGrain) → PSO index → PatientDrugClassIndexGrain
/// refresh → DrugClassCohortIndexGrain shards → advisory cohort resolution.
///
/// Each test uses unique VA drug class codes so the shared-cluster cohort shards
/// stay isolated and assertions can be exact.
/// </summary>
[TestFixture]
public class DrugClassCohortIndexTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private static string NewClassCode() => $"TC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private IDrugClassCohortIndexGrain Cohort(string classCode) =>
        _cluster.GrainFactory.GetGrain<IDrugClassCohortIndexGrain>(classCode);

    private async Task<string> CreateDrugAsync(string primaryClass, params string[] secondaryClasses)
    {
        string ien = $"DRUG-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IDrugGrain>(ien).SaveDrugAsync(new DrugState
        {
            LocalName = ien,
            PrimaryDrugClassCode = primaryClass,
            SecondaryDrugClassCodes = secondaryClasses.ToList(),
        });
        return ien;
    }

    private async Task<IPharmacyGrain> CreateActiveRxAsync(string patientId, string drugName, string drugIen)
    {
        IPharmacyGrain rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>($"RX-{Guid.NewGuid()}");
        await rx.CreatePrescriptionAsync(
            patientId, drugName, drugIen, "1 tab", "ORAL", "QD", "Take daily",
            30, 30, 3, null, null, null, null, null, null);
        return rx;
    }

    [Test]
    public async Task ActivePrescription_PlacesPatientInPrimaryAndSecondaryClassCohorts()
    {
        string primary = NewClassCode();
        string secondary = NewClassCode();
        string drugIen = await CreateDrugAsync(primary, secondary);
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        await CreateActiveRxAsync(patientId, "MULTICLASS TAB", drugIen);

        // Patient is matched against EVERY class the drug belongs to, not just primary.
        Assert.That(await Cohort(primary).ContainsAsync(patientId), Is.True);
        Assert.That(await Cohort(secondary).ContainsAsync(patientId), Is.True);

        List<string> classes = await _cluster.GrainFactory
            .GetGrain<IPatientDrugClassIndexGrain>(patientId).GetActiveClassCodesAsync();
        Assert.That(classes, Does.Contain(primary));
        Assert.That(classes, Does.Contain(secondary));
    }

    [Test]
    public async Task Discontinue_RemovesPatientFromCohort_WhenNoOtherActiveMedInClass()
    {
        string cls = NewClassCode();
        string drugIen = await CreateDrugAsync(cls);
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        IPharmacyGrain rx = await CreateActiveRxAsync(patientId, "SOLO TAB", drugIen);
        Assert.That(await Cohort(cls).ContainsAsync(patientId), Is.True);

        await rx.DiscontinueAsync("therapy complete");
        Assert.That(await Cohort(cls).ContainsAsync(patientId), Is.False);
    }

    [Test]
    public async Task Discontinue_KeepsPatientInCohort_WhenAnotherActiveMedSharesClass()
    {
        string cls = NewClassCode();
        string drugA = await CreateDrugAsync(cls);
        string drugB = await CreateDrugAsync(cls);
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        IPharmacyGrain rxA = await CreateActiveRxAsync(patientId, "DRUG A", drugA);
        await CreateActiveRxAsync(patientId, "DRUG B", drugB);
        Assert.That(await Cohort(cls).ContainsAsync(patientId), Is.True);

        // Discontinuing one med must not drop the patient — they're still on another
        // active med in the same class.
        await rxA.DiscontinueAsync("switched");
        Assert.That(await Cohort(cls).ContainsAsync(patientId), Is.True);
    }

    [Test]
    public async Task Advisory_GetAffectedPatients_UnionThenPanelIntersection_ExcludesReached()
    {
        string cls = NewClassCode();
        string drugIen = await CreateDrugAsync(cls);

        string panelPatient = $"PATIENT-{Guid.NewGuid()}";
        string otherPatient = $"PATIENT-{Guid.NewGuid()}";
        await CreateActiveRxAsync(panelPatient, "PPI", drugIen);
        await CreateActiveRxAsync(otherPatient, "PPI", drugIen);

        // Provider panel contains only the first patient.
        string providerId = $"PROV-{Guid.NewGuid()}";
        await _cluster.GrainFactory
            .GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{providerId}")
            .AddOrUpdatePatientAsync(new ProviderPatientEntry
            {
                PatientId = panelPatient,
                PatientName = "DOE,JOHN",
                IsActive = true,
            });

        string advisoryId = $"DSA-{Guid.NewGuid()}";
        IDrugSafetyAdvisoryGrain advisory =
            _cluster.GrainFactory.GetGrain<IDrugSafetyAdvisoryGrain>(advisoryId);
        await advisory.SaveAsync(new DrugSafetyAdvisoryState
        {
            AdvisoryId = advisoryId,
            Title = "Class advisory",
            TargetDrugClassCodes = [cls],
            DefaultMessage = "Please review your medication.",
        });
        await advisory.ActivateAsync();

        // Whole-class cohort includes both patients.
        List<string> all = await advisory.GetAffectedPatientsAsync(null);
        Assert.That(all, Does.Contain(panelPatient));
        Assert.That(all, Does.Contain(otherPatient));

        // Provider-scoped cohort = just this provider's patient.
        List<string> mine = await advisory.GetAffectedPatientsAsync(providerId);
        Assert.That(mine, Is.EqualTo(new List<string> { panelPatient }));

        // After dispatch, the patient drops out of the "still needs warning" list.
        await advisory.DispatchAsync(
            "edited message", [panelPatient], providerId, "Dr. Smith", AdvisoryChannel.PatientPortal);
        List<string> remaining = await advisory.GetAffectedPatientsAsync(providerId);
        Assert.That(remaining, Is.Empty);
    }
}
