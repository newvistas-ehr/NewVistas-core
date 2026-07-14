// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// R4 (Whole-Person Social Care roadmap) — veteran psychosocial enrichment: combat/era/exposure/VSO
/// context recorded on the patient aggregate, plus the era-suggestion helper driven by service dates.
/// </summary>
[TestFixture]
public class VeteranPsychosocialTests
{
    private TestCluster _cluster = null!;
    private const string By = "SW1";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [Test]
    public async Task RecordAndReadBack_CombatEraExposureVso()
    {
        string patient = $"VET-{Guid.NewGuid()}";

        // Empty before first record.
        Assert.That(await Wf(patient).GetVeteranPsychosocialAsync(), Is.Null);

        var profile = new VeteranPsychosocialProfile
        {
            CombatVeteran = true,
            PurpleHeart = true,
            HomelessOrAtRisk = true,
            ServiceEras = { MilitaryServiceEra.PostGulfWar_OefOif },
            Exposures = { MilitaryEnvironmentalExposure.BurnPitAirborneHazards, MilitaryEnvironmentalExposure.GulfWarSwAsiaConditions },
            Vso = new VsoContact { OrganizationName = "DAV", RepresentativeName = "J. Rep", Phone = "555-0100", PowerOfAttorneyOnFile = true },
            Notes = "Post-deployment reintegration support.",
        };
        await Wf(patient).UpdateVeteranPsychosocialAsync(profile, By);

        VeteranPsychosocialProfile? read = await Wf(patient).GetVeteranPsychosocialAsync();
        Assert.That(read, Is.Not.Null);
        Assert.That(read!.CombatVeteran, Is.True);
        Assert.That(read.PurpleHeart, Is.True);
        Assert.That(read.HomelessOrAtRisk, Is.True);
        Assert.That(read.ServiceEras, Does.Contain(MilitaryServiceEra.PostGulfWar_OefOif));
        Assert.That(read.Exposures, Has.Count.EqualTo(2));
        Assert.That(read.Vso!.OrganizationName, Is.EqualTo("DAV"));
        Assert.That(read.Vso.PowerOfAttorneyOnFile, Is.True);
        Assert.That(read.LastUpdatedBy, Is.EqualTo(By));
        Assert.That(read.LastUpdatedDate, Is.Not.Null);
    }

    [Test]
    public void EraHelper_MapsEntryYearToEra()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VeteranEraHelper.SuggestEraFromEntryDate(new DateTime(2005, 1, 1)), Is.EqualTo(MilitaryServiceEra.PostGulfWar_OefOif));
            Assert.That(VeteranEraHelper.SuggestEraFromEntryDate(new DateTime(1992, 1, 1)), Is.EqualTo(MilitaryServiceEra.PersianGulfWar));
            Assert.That(VeteranEraHelper.SuggestEraFromEntryDate(new DateTime(1968, 1, 1)), Is.EqualTo(MilitaryServiceEra.VietnamEra));
            Assert.That(VeteranEraHelper.SuggestEraFromEntryDate(null), Is.Null);
        });
    }
}
