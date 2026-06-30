// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the pharmacogenomics (PGx) module (PHARMACOGENOMICS): recording coded gene
/// results on a patient, the curated drug-gene matcher exposed via the workflow grain, and — the key
/// test — the DUR engine reading the PGx profile so a drug-gene contraindication FAILS a Drug
/// Utilization Review at prescribing time. End-to-end via <see cref="IPatientWorkflowGrain"/> on the
/// shared TestCluster.
/// </summary>
[TestFixture]
public class PharmacogenomicsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>Records a single coded PGx result for the patient with sensible defaults.</summary>
    private Task<string> RecordResult(
        IPatientWorkflowGrain wf, string gene, string diplotype, PgxPhenotype phenotype)
        => wf.RecordPharmacogenomicResultAsync(
            gene, diplotype, phenotype, null, new DateTime(2026, 1, 15),
            "Genomics Lab", "Targeted genotyping", "", "TEST");

    /// <summary>Runs a DUR for a free-text drug, passing nulls/false for the optional drug params.</summary>
    private Task<string> PerformDur(IPatientWorkflowGrain wf, string drugName)
        => wf.PerformDurAsync(
            prescriptionId: $"RX-{Guid.NewGuid()}",
            drugName: drugName,
            drugId: null,
            drugClass: null,
            dosage: null,
            route: null,
            schedule: null,
            daysSupply: null,
            quantity: null,
            maxDaysSupply: null,
            maxQuantity: null,
            isControlledSubstance: false,
            deaSchedule: null,
            performedBy: "TEST");

    // ── Recording / profile ────────────────────────────────────────────────────────

    [Test]
    public async Task RecordResult_ThenGetProfile_GeneAppearsWithPhenotype()
    {
        string patientId = $"PGX-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await RecordResult(wf, "CYP2C19", "*2/*2", PgxPhenotype.PoorMetabolizer);

        PharmacogenomicsState profile = await wf.GetPharmacogenomicProfileAsync();
        PgxResultEntry entry = profile.Results.Single(r => r.Gene == "CYP2C19");
        Assert.That(entry.Phenotype, Is.EqualTo(PgxPhenotype.PoorMetabolizer));
        Assert.That(entry.Diplotype, Is.EqualTo("*2/*2"));
    }

    [Test]
    public async Task RecordResult_SameGeneTwice_UpsertsToSingleEntry()
    {
        string patientId = $"PGX-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await RecordResult(wf, "CYP2C19", "*1/*2", PgxPhenotype.IntermediateMetabolizer);
        await RecordResult(wf, "CYP2C19", "*2/*2", PgxPhenotype.PoorMetabolizer);

        PharmacogenomicsState profile = await wf.GetPharmacogenomicProfileAsync();
        Assert.That(profile.Results.Where(r => r.Gene == "CYP2C19").ToList(), Has.Count.EqualTo(1));

        PgxResultEntry entry = profile.Results.Single(r => r.Gene == "CYP2C19");
        Assert.That(entry.Phenotype, Is.EqualTo(PgxPhenotype.PoorMetabolizer));
        Assert.That(entry.Diplotype, Is.EqualTo("*2/*2"));
    }

    [Test]
    public async Task RemoveResult_RemovesTheGene()
    {
        string patientId = $"PGX-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await RecordResult(wf, "DPYD", "*1/*2A", PgxPhenotype.IntermediateMetabolizer);
        Assert.That((await wf.GetPharmacogenomicProfileAsync()).Results.Any(r => r.Gene == "DPYD"), Is.True);

        await wf.RemovePharmacogenomicResultAsync("DPYD");

        PharmacogenomicsState profile = await wf.GetPharmacogenomicProfileAsync();
        Assert.That(profile.Results.Any(r => r.Gene == "DPYD"), Is.False);
    }

    // ── Recommendations / drug check ─────────────────────────────────────────────────

    [Test]
    public async Task GetRecommendations_AfterCyp2c19PoorMetabolizer_ContainsClopidogrelRec()
    {
        string patientId = $"PGX-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await RecordResult(wf, "CYP2C19", "*2/*2", PgxPhenotype.PoorMetabolizer);

        List<PgxRecommendation> recs = await wf.GetPharmacogenomicRecommendationsAsync();
        Assert.That(recs, Has.Some.Matches<PgxRecommendation>(r => r.Drug == "clopidogrel"));
    }

    [Test]
    public async Task CheckDrug_Clopidogrel_AfterCyp2c19PoorMetabolizer_IsAvoid()
    {
        string patientId = $"PGX-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await RecordResult(wf, "CYP2C19", "*2/*2", PgxPhenotype.PoorMetabolizer);

        List<PgxRecommendation> recs = await wf.CheckDrugPharmacogenomicsAsync("clopidogrel");
        Assert.That(recs, Is.Not.Empty);
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.Avoid));
    }

    // ── DUR drug-gene integration ────────────────────────────────────────────────────

    [Test]
    public async Task Dur_Clopidogrel_WithCyp2c19PoorMetabolizer_PharmacogenomicCheckFails()
    {
        string patientId = $"PGX-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await RecordResult(wf, "CYP2C19", "*2/*2", PgxPhenotype.PoorMetabolizer);

        string assessmentId = await PerformDur(wf, "clopidogrel");
        DurAssessmentState assessment = await wf.GetDurAssessmentAsync(assessmentId);

        DurCheckResult pgxCheck = assessment.Checks.Single(c => c.CheckType == DurCheckType.Pharmacogenomic);
        Assert.That(pgxCheck.Outcome, Is.EqualTo(DurOutcome.Fail));
    }

    [Test]
    public async Task Dur_Clopidogrel_NoPgxResults_PharmacogenomicCheckIsNotApplicable()
    {
        string patientId = $"PGX-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string assessmentId = await PerformDur(wf, "clopidogrel");
        DurAssessmentState assessment = await wf.GetDurAssessmentAsync(assessmentId);

        DurCheckResult pgxCheck = assessment.Checks.Single(c => c.CheckType == DurCheckType.Pharmacogenomic);
        Assert.That(pgxCheck.Outcome, Is.EqualTo(DurOutcome.NotApplicable));
    }
}
