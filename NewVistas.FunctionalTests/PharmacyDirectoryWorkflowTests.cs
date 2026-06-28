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
/// Multiple-outpatient-pharmacies enhancement: the pharmacy directory, a patient's preferred
/// (default) pharmacy, and structured prescriptions that record which pharmacy fills them.
/// </summary>
[TestFixture]
public class PharmacyDirectoryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPharmacyDirectoryGrain Dir() => _cluster.GrainFactory.GetGrain<IPharmacyDirectoryGrain>("PHARMACY-DIRECTORY");
    private IPatientWorkflowGrain Wf(string id) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(id);

    [Test]
    public async Task Directory_AutoSeeds_AndOutpatientListExcludesHospitalPharmacy()
    {
        List<PharmacyDirectoryEntry> cvs = await Dir().SearchAsync("CVS", outpatientOnly: true);
        Assert.That(cvs.Any(p => p.Name.Contains("CVS")), Is.True);

        List<PharmacyDirectoryEntry> outpatient = await Dir().GetAllAsync(outpatientOnly: true);
        Assert.That(outpatient.Any(p => p.Kind == PharmacyKinds.Inpatient), Is.False,
            "the outpatient (patient-choice) list excludes the hospital pharmacy");
        Assert.That(outpatient.Any(p => p.Kind == PharmacyKinds.Mail), Is.True, "mail-order is an outpatient option");

        List<PharmacyDirectoryEntry> withInpatient = await Dir().GetAllAsync(outpatientOnly: false);
        Assert.That(withInpatient.Any(p => p.Kind == PharmacyKinds.Inpatient), Is.True,
            "the hospital pharmacy exists in the directory");
    }

    [Test]
    public async Task PreferredPharmacy_RoundTrips_AndIgnoresUnknown()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        Assert.That(await Wf(pid).GetPreferredPharmacyAsync(), Is.Null);

        await Wf(pid).SetPreferredPharmacyAsync("PHARM-CVS-4501");
        PharmacyDirectoryEntry? pref = await Wf(pid).GetPreferredPharmacyAsync();
        Assert.That(pref, Is.Not.Null);
        Assert.That(pref!.PharmacyId, Is.EqualTo("PHARM-CVS-4501"));

        // An unknown pharmacy id is a no-op (keeps the existing preference).
        await Wf(pid).SetPreferredPharmacyAsync("PHARM-DOES-NOT-EXIST");
        Assert.That((await Wf(pid).GetPreferredPharmacyAsync())!.PharmacyId, Is.EqualTo("PHARM-CVS-4501"));
    }

    [Test]
    public async Task PlacePrescription_AppearsInActiveMeds_AndRecordsChosenPharmacy()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        string rxId = await Wf(pid).PlacePrescriptionAsync(
            "Lisinopril 10 mg tablet", null, "10 mg", "PO", "DAILY", "Take 1 tablet by mouth daily",
            30, 30, 3, "PROV-001", "Dr. Smith", "PHARM-WAG-2210", "WALGREENS #2210", null);

        Assert.That(rxId, Does.StartWith("RX-"));

        List<MedicationSummary> meds = await Wf(pid).GetActiveMedicationsAsync();
        Assert.That(meds.Any(m => m.DrugName == "Lisinopril 10 mg tablet"), Is.True,
            "a placed prescription shows in the active-med list");

        // The chosen pharmacy is recorded on the prescription itself.
        PharmacyState rx = await _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId).GetPrescriptionAsync();
        Assert.That(rx.PharmacyId, Is.EqualTo("PHARM-WAG-2210"));
        Assert.That(rx.PharmacyName, Is.EqualTo("WALGREENS #2210"));
    }

    [Test]
    public async Task PlacePrescription_ErxCapablePharmacy_RecordsOfflineTransmission()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        // Walgreens accepts e-Rx and has an NCPDP id → the (offline) transmitter records the NewRx.
        string rxId = await Wf(pid).PlacePrescriptionAsync(
            "Atorvastatin 20 mg tablet", null, "20 mg", "PO", "QHS", "Take 1 tablet at bedtime",
            30, 30, 5, "PROV-001", "Dr. Smith", "PHARM-WAG-2210", "WALGREENS #2210", null);

        PharmacyState rx = await _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId).GetPrescriptionAsync();
        Assert.That(rx.ErxStatus, Is.EqualTo("NOT_TRANSMITTED"),
            "the offline transmitter records the attempt but does not actually send");
        Assert.That(rx.ErxDetail, Does.Contain("NCPDP SCRIPT"));
        Assert.That(rx.ErxDetail, Does.Contain("WALGREENS"));
    }

    [Test]
    public async Task PlacePrescription_HospitalPharmacy_IsNotEPrescribed()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        // The hospital pharmacy has no NCPDP id / is not e-Rx capable → no transmission attempt.
        string rxId = await Wf(pid).PlacePrescriptionAsync(
            "Acetaminophen 500 mg tablet", null, "500 mg", "PO", "Q6H PRN", "Take 1-2 tablets every 6 hours as needed",
            10, 40, 0, "PROV-001", "Dr. Smith", "PHARM-HOSPITAL", "NEWVISTAS HOSPITAL PHARMACY", null);

        PharmacyState rx = await _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId).GetPrescriptionAsync();
        Assert.That(rx.ErxStatus, Is.Null, "the hospital pharmacy (no NCPDP, not e-Rx) is not transmitted");
    }
}
