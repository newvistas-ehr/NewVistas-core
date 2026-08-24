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
/// The generic code-set migration sweep (ADR-006 bulk recode): the canonical scenario is
/// U07.1 shipping in 2020 while every prior COVID patient carried B34.2. The sweep must move
/// the charts and move NO statistic — Recode/Recoded are excluded from the revision numerator,
/// the denominator and the coverage ratio by construction.
/// </summary>
/// <remarks>
/// NonParallelizable — reloads the shared ICD10-INDEX singleton (LoadCodesAsync clears it),
/// which races with ClinicalCodingWorkflowTests doing the same under fixture parallelism.
/// </remarks>
[TestFixture, NonParallelizable]
public class BulkRecodeTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;

        // The sweep refuses a replacement code the loaded index does not know — a typo at
        // population scale is not recoverable. Give the index the codes this fixture uses.
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await index.LoadCodesAsync(new List<Icd10IndexEntry>
        {
            new() { Code = "B34.2", ShortDescription = "Coronavirus infection, unsp",
                    LongDescription = "Coronavirus infection, unspecified", IsBillable = true, IsActive = true },
            new() { Code = "U07.1", ShortDescription = "COVID-19",
                    LongDescription = "COVID-19", IsBillable = true, IsActive = true },
            new() { Code = "I10", ShortDescription = "Essential (primary) hypertension",
                    LongDescription = "Essential (primary) hypertension", IsBillable = true, IsActive = true },
        });
    }

    private IPatientWorkflowGrain Workflow(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    private static BulkRecodeCommand CovidDirective(string runBy = "ADMIN1") => new()
    {
        FromCode = "B34.2",
        ToCode = "U07.1",
        ToDisplay = "COVID-19",
        Narrative = "U07.1 issued; B34.2 rows remapped per coding directive.",
        RunBy = runBy,
    };

    [Test]
    public async Task Sweep_RecodesMatchingCharts_SkipsOthers_AndIsIdempotent()
    {
        string covidPid = $"RECODE-{Guid.NewGuid()}";
        string otherPid = $"RECODE-{Guid.NewGuid()}";

        string oldProblemId = await Workflow(covidPid).AddProblemAsync(
            "Coronavirus infection, unspecified", "B34.2", "A", "ACUTE",
            null, "PROV-1", "Dr. A", null, null, false, null);
        await Workflow(otherPid).AddProblemAsync(
            "Hypertension", "I10", "C", "CHRONIC", null, "PROV-1", "Dr. A", null, null, false, null);

        var sweep = _cluster.GrainFactory.GetGrain<IDxStewardshipSweepGrain>("DX-STEWARDSHIP-SWEEP");
        BulkRecodeRun run = await sweep.BulkRecodePatientsAsync(
            CovidDirective(), new List<string> { covidPid, otherPid });

        Assert.That(run.PatientsScreened, Is.EqualTo(2));
        Assert.That(run.RecodedCount, Is.EqualTo(1));
        Assert.That(run.NoMatchCount, Is.EqualTo(1));
        Assert.That(run.EpisodesClosed, Is.EqualTo(1), "AddProblemAsync opened an episode; the recode must close it");
        Assert.That(run.FailureCount, Is.EqualTo(0));

        // The chart moved: U07.1 active, B34.2 superseded off the active list.
        List<ProblemSummary> active = await Workflow(covidPid).GetActiveProblemsAsync();
        Assert.That(active.Select(p => p.DiagnosisCode), Does.Contain("U07.1"));
        Assert.That(active.Select(p => p.DiagnosisCode), Does.Not.Contain("B34.2"));

        ProblemEntry? old = await Workflow(covidPid).GetProblemWithEvidenceAsync(oldProblemId);
        Assert.That(old!.SupersededByProblemId, Is.Not.Null);
        Assert.That(old.LastRevisionReason, Is.EqualTo(RevisionReason.Recode),
            "never Correction — nobody was wrong when the code set changed");

        // The new assertion cites the old one and carries its certainty unchanged.
        ProblemEntry? renewed = await Workflow(covidPid).GetProblemWithEvidenceAsync(old.SupersededByProblemId!);
        Assert.That(renewed!.DiagnosisCode, Is.EqualTo("U07.1"));
        Assert.That(renewed.VerificationStatus, Is.EqualTo(old.VerificationStatus),
            "a code-set change is not new clinical confidence");
        EvidenceRef citation = renewed.Evidence.Single(e => e.Kind == EvidenceKind.Problem);
        Assert.That(citation.SourceId, Is.EqualTo(oldProblemId));
        Assert.That(citation.IsMachineCited, Is.True);

        // The untouched patient is untouched.
        List<ProblemSummary> otherActive = await Workflow(otherPid).GetActiveProblemsAsync();
        Assert.That(otherActive.Select(p => p.DiagnosisCode), Does.Contain("I10"));
        Assert.That(otherActive, Has.Count.EqualTo(1));

        // Re-running the same directive is a no-op, not a duplicate row.
        BulkRecodeRun rerun = await sweep.BulkRecodePatientsAsync(
            CovidDirective(), new List<string> { covidPid, otherPid });
        Assert.That(rerun.AlreadyCodedCount, Is.EqualTo(1));
        Assert.That(rerun.RecodedCount, Is.EqualTo(0));
        List<ProblemSummary> after = await Workflow(covidPid).GetActiveProblemsAsync();
        Assert.That(after.Count(p => p.DiagnosisCode == "U07.1"), Is.EqualTo(1));
    }

    [Test]
    public async Task InterruptedRun_IsRepairedByRerun_NotBlockedByIdempotencyGuard()
    {
        // The crash state a mid-sequence failure leaves behind: the replacement problem was
        // asserted but the old-code problem was never superseded — patient active under BOTH
        // codes. The documented recovery is "re-run the directive"; the guard must read this
        // as an interrupted migration to finish, never as AlreadyCoded to skip.
        string pid = $"RECODE-{Guid.NewGuid()}";
        string oldId = await Workflow(pid).AddProblemAsync(
            "Coronavirus infection, unspecified", "B34.2", "A", "ACUTE",
            null, "PROV-1", "Dr. A", null, null, false, null);
        string orphanTargetId = await Workflow(pid).AddProblemAsync(
            "COVID-19", "U07.1", "A", "ACUTE",
            null, "ADMIN1", "ADMIN1", null, null, false, null);

        var sweep = _cluster.GrainFactory.GetGrain<IDxStewardshipSweepGrain>("DX-STEWARDSHIP-SWEEP");
        BulkRecodeRun run = await sweep.BulkRecodePatientsAsync(CovidDirective(), new List<string> { pid });

        Assert.That(run.RecodedCount, Is.EqualTo(1), "the interrupted migration must complete, not report AlreadyCoded");
        Assert.That(run.AlreadyCodedCount, Is.EqualTo(0));

        List<ProblemSummary> active = await Workflow(pid).GetActiveProblemsAsync();
        Assert.That(active.Count(p => p.DiagnosisCode == "U07.1"), Is.EqualTo(1),
            "the repair must reuse the existing replacement problem, never mint a second");
        Assert.That(active.Any(p => p.ProblemId == oldId), Is.False);

        ProblemEntry? old = await Workflow(pid).GetProblemWithEvidenceAsync(oldId);
        Assert.That(old!.SupersededByProblemId, Is.EqualTo(orphanTargetId));
        Assert.That(old.LastRevisionReason, Is.EqualTo(RevisionReason.Recode));

        // And a second re-run over the now-completed patient is the plain no-op.
        BulkRecodeRun rerun = await sweep.BulkRecodePatientsAsync(CovidDirective(), new List<string> { pid });
        Assert.That(rerun.AlreadyCodedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Recode_MovesNoStatistic()
    {
        string pid = $"RECODE-{Guid.NewGuid()}";
        await Workflow(pid).AddProblemAsync(
            "Coronavirus infection, unspecified", "B34.2", "A", "ACUTE",
            null, "PROV-1", "Dr. A", null, null, false, null);

        int year = DateTime.UtcNow.Year;
        var shard = _cluster.GrainFactory.GetGrain<IDiagnosisOutcomeIndexGrain>(
            $"DX-OUTCOME:CODE:B342:{year}");
        DiagnosisOutcomeState before = await shard.GetStateAsync();

        var sweep = _cluster.GrainFactory.GetGrain<IDxStewardshipSweepGrain>("DX-STEWARDSHIP-SWEEP");
        await sweep.BulkRecodePatientsAsync(CovidDirective(), new List<string> { pid });

        DiagnosisOutcomeState after = await shard.GetStateAsync();
        Assert.That(after.RecodedCount, Is.EqualTo(before.RecodedCount + 1));
        Assert.That(after.AdjudicatedCount, Is.EqualTo(before.AdjudicatedCount),
            "a recode is not an adjudication — it must not enter the denominator");
        Assert.That(after.RevisedCount, Is.EqualTo(before.RevisedCount),
            "a recode is not a misdiagnosis — it must not enter the numerator");
    }

    [Test]
    public async Task Directive_IsValidated()
    {
        var sweep = _cluster.GrainFactory.GetGrain<IDxStewardshipSweepGrain>("DX-STEWARDSHIP-SWEEP");

        Assert.That(async () => await sweep.BulkRecodePatientsAsync(
                new BulkRecodeCommand { FromCode = "B34.2", ToCode = "B342", ToDisplay = "x", Narrative = "x", RunBy = "A" },
                new List<string>()),
            Throws.ArgumentException, "same code in different dress must be refused");

        Assert.That(async () => await sweep.BulkRecodePatientsAsync(
                new BulkRecodeCommand { FromCode = "B34.2", ToCode = "ZZ99.999", ToDisplay = "x", Narrative = "x", RunBy = "A" },
                new List<string>()),
            Throws.ArgumentException, "a replacement code unknown to the loaded index is a typo at scale");

        Assert.That(async () => await sweep.BulkRecodePatientsAsync(
                new BulkRecodeCommand { FromCode = "B34.2", ToCode = "U07.1", ToDisplay = "COVID-19", Narrative = " ", RunBy = "A" },
                new List<string>()),
            Throws.ArgumentException, "the why travels onto every chart, so it is required");
    }
}
