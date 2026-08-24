// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

// ─── Radiology finding extraction: grounding, parsing, the safety net ─────────
// Pure tests — no cluster. The extractor surfaces what the radiologist wrote; every
// finding must trace to a real sentence, and a fabricated finding is flagged.

[TestFixture]
public class RadiologyFindingPureTests
{
    [Test]
    public async Task Heuristic_ExtractsMaterialForaminalFinding_AndGroundsEveryQuote()
    {
        RadiologyExtractionResult result =
            await new HeuristicRadiologyFindingExtractor().ExtractAsync(RadiologyTestData.SyntheticCervicalReport);
        RadiologyFindingVerifier.Verify(RadiologyTestData.SyntheticCervicalReport, result.Findings);

        // Every extracted finding quotes a sentence actually in the report.
        Assert.That(result.Findings, Is.Not.Empty);
        Assert.That(result.Findings.All(f => f.QuoteVerified), Is.True);

        // The missed finding: moderate-to-severe LEFT neural foraminal stenosis at C5-C6.
        RadiologyFinding material = result.Findings.Single(f =>
            f.Severity == FindingSeverity.Severe && f.FindingType.Contains("foraminal"));
        Assert.That(material.Laterality, Is.EqualTo(FindingLaterality.Left));
        Assert.That(material.Level, Is.EqualTo("C5-C6"));

        // The mild central canal stenosis is also extracted but is NOT material.
        Assert.That(result.Findings.Any(f =>
            f.FindingType.Contains("Central") && f.Severity == FindingSeverity.Mild), Is.True);
    }

    [Test]
    public void Verifier_FlagsAFindingWhoseQuoteIsNotInTheReport()
    {
        List<RadiologyFinding> findings =
        [
            new() { FindingId = "RF1", SourceQuote = "At C5-C6 there is mild central canal stenosis." },
            new() { FindingId = "RF2", SourceQuote = "At C5-C6 there is a large destructive tumor." }, // invented
        ];

        int flagged = RadiologyFindingVerifier.Verify(RadiologyTestData.SyntheticCervicalReport, findings);

        Assert.That(flagged, Is.EqualTo(1));
        Assert.That(findings[0].QuoteVerified, Is.True);
        Assert.That(findings[1].QuoteVerified, Is.False);
        Assert.That(findings[1].VerificationNote, Does.Contain("not found"));
    }

    [Test]
    public void ParseJson_MapsSeverityAndLaterality_ThroughCodeFences()
    {
        const string modelText =
            "```json\n{\"findings\":[{\"findingType\":\"Neural foraminal stenosis\",\"level\":\"C5-C6\","
            + "\"laterality\":\"left\",\"severity\":\"severe\",\"severityText\":\"moderate to severe\","
            + "\"sourceQuote\":\"At C5-C6 there is moderate to severe left neural foraminal stenosis.\"}]}\n```";

        RadiologyExtractionResult result = RadiologyFindingJson.Parse(modelText, "claude");

        Assert.That(result.ProviderName, Is.EqualTo("claude"));
        RadiologyFinding f = result.Findings.Single();
        Assert.That(f.Severity, Is.EqualTo(FindingSeverity.Severe));
        Assert.That(f.Laterality, Is.EqualTo(FindingLaterality.Left));
        Assert.That(f.Level, Is.EqualTo("C5-C6"));
    }

    [Test]
    public void LiveModelHallucination_IsCaughtByVerification()
    {
        // A "model" returns a finding the report never stated.
        const string hallucinated =
            "{\"findings\":[{\"findingType\":\"Mass\",\"level\":\"C5-C6\",\"laterality\":\"Left\","
            + "\"severity\":\"Severe\",\"severityText\":\"severe\","
            + "\"sourceQuote\":\"At C5-C6 there is a 3 cm enhancing mass.\"}]}";

        RadiologyExtractionResult result = RadiologyFindingJson.Parse(hallucinated, "claude");
        int flagged = RadiologyFindingVerifier.Verify(RadiologyTestData.SyntheticCervicalReport, result.Findings);

        Assert.That(flagged, Is.EqualTo(1));
        Assert.That(result.Findings[0].QuoteVerified, Is.False);
    }

    [Test]
    public async Task RealReport_WhenProvided_ExtractsGroundedFindings()
    {
        if (string.IsNullOrWhiteSpace(RadiologyTestData.RealReport))
            Assert.Ignore("Paste a real report into RadiologyTestData.RealReport to exercise this.");

        RadiologyExtractionResult result =
            await new HeuristicRadiologyFindingExtractor().ExtractAsync(RadiologyTestData.RealReport);
        RadiologyFindingVerifier.Verify(RadiologyTestData.RealReport, result.Findings);

        Assert.That(result.Findings.All(f => f.QuoteVerified), Is.True);
    }
}

// ─── End-to-end: extraction grain + the acknowledge/reject forcing function ───

[TestFixture]
public class RadiologyFindingExtractionGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IRadiologyFindingExtractionGrain Report(string reportId) =>
        _cluster.GrainFactory.GetGrain<IRadiologyFindingExtractionGrain>(reportId);

    [Test]
    public async Task Extract_GroundsFindings_AndFlagsMaterialOnesForAcknowledgment()
    {
        string reportId = $"RAD-{Guid.NewGuid()}";
        RadiologyExtractionState state = await Report(reportId).ExtractAsync(
            RadiologyTestData.SyntheticCervicalReport, $"PATIENT-{Guid.NewGuid()}", "RAD-1");

        Assert.That(state.ModelProvider, Is.EqualTo("offline-heuristic"));
        Assert.That(state.Findings.All(f => f.QuoteVerified), Is.True);

        // Exactly the moderate-to-severe foraminal finding is flagged as requiring a decision.
        List<RadiologyFinding> material = state.Findings.Where(f => f.RequiresAcknowledgment).ToList();
        Assert.That(material, Has.Count.EqualTo(1));
        Assert.That(material[0].Severity, Is.EqualTo(FindingSeverity.Severe));
        Assert.That(material[0].FindingType, Does.Contain("foraminal"));
    }

    [Test]
    public async Task Acknowledge_RecordsClinicianDisposition()
    {
        string reportId = $"RAD-{Guid.NewGuid()}";
        RadiologyExtractionState state = await Report(reportId).ExtractAsync(
            RadiologyTestData.SyntheticCervicalReport, "PATIENT-1", "RAD-1");
        string materialId = state.Findings.First(f => f.RequiresAcknowledgment).FindingId;

        await Report(reportId).AcknowledgeAsync(materialId, "DOCTOR1");

        RadiologyFinding f = (await Report(reportId).GetAsync()).Findings.Single(x => x.FindingId == materialId);
        Assert.That(f.Acknowledgment, Is.EqualTo(FindingAcknowledgment.Acknowledged));
        Assert.That(f.DispositionedBy, Is.EqualTo("DOCTOR1"));
        Assert.That(f.PatientVisible, Is.False);
    }

    [Test]
    public async Task Reject_RequiresAReason_AndIsRecordedPatientVisible()
    {
        string reportId = $"RAD-{Guid.NewGuid()}";
        RadiologyExtractionState state = await Report(reportId).ExtractAsync(
            RadiologyTestData.SyntheticCervicalReport, "PATIENT-1", "RAD-1");
        string materialId = state.Findings.First(f => f.RequiresAcknowledgment).FindingId;

        // A rejection without a reason is refused — you cannot silently dismiss a finding.
        Assert.That(async () => await Report(reportId).RejectAsync(materialId, "DOCTOR1", "  "),
            Throws.ArgumentException);

        await Report(reportId).RejectAsync(materialId, "DOCTOR1", "Believed to be a positioning artifact.");

        RadiologyFinding f = (await Report(reportId).GetAsync()).Findings.Single(x => x.FindingId == materialId);
        Assert.That(f.Acknowledgment, Is.EqualTo(FindingAcknowledgment.Rejected));
        Assert.That(f.RejectionReason, Is.EqualTo("Believed to be a positioning artifact."));
        Assert.That(f.PatientVisible, Is.True);   // recorded and visible to the patient
    }

    [Test]
    public async Task ReExtract_RejectionSurvives_AndFindingCountDoesNotInflate()
    {
        string reportId = $"RAD-{Guid.NewGuid()}";
        RadiologyExtractionState first = await Report(reportId).ExtractAsync(
            RadiologyTestData.SyntheticCervicalReport, "PATIENT-1", "RAD-1");
        RadiologyFinding material = first.Findings.First(f => f.RequiresAcknowledgment);

        await Report(reportId).RejectAsync(material.FindingId, "DOCTOR1", "Positioning artifact.");

        // Re-running extraction over the SAME report must not erase the recorded rejection.
        RadiologyExtractionState second = await Report(reportId).ExtractAsync(
            RadiologyTestData.SyntheticCervicalReport, "PATIENT-1", "RAD-2");

        Assert.That(second.Findings, Has.Count.EqualTo(first.Findings.Count),
            "re-extraction of the same report must not inflate the finding count");

        RadiologyFinding survived = second.Findings.Single(f =>
            f.FindingType.Equals(material.FindingType, StringComparison.OrdinalIgnoreCase)
            && f.Level.Equals(material.Level, StringComparison.OrdinalIgnoreCase)
            && f.Laterality == material.Laterality);
        Assert.That(survived.Acknowledgment, Is.EqualTo(FindingAcknowledgment.Rejected));
        Assert.That(survived.RejectionReason, Is.EqualTo("Positioning artifact."));
        Assert.That(survived.DispositionedBy, Is.EqualTo("DOCTOR1"));
        Assert.That(survived.PatientVisible, Is.True);
    }

    [Test]
    public async Task ReExtract_AcknowledgmentSurvives()
    {
        string reportId = $"RAD-{Guid.NewGuid()}";
        RadiologyExtractionState first = await Report(reportId).ExtractAsync(
            RadiologyTestData.SyntheticCervicalReport, "PATIENT-1", "RAD-1");
        RadiologyFinding material = first.Findings.First(f => f.RequiresAcknowledgment);

        await Report(reportId).AcknowledgeAsync(material.FindingId, "DOCTOR1");

        RadiologyExtractionState second = await Report(reportId).ExtractAsync(
            RadiologyTestData.SyntheticCervicalReport, "PATIENT-1", "RAD-2");

        Assert.That(second.Findings, Has.Count.EqualTo(first.Findings.Count));

        RadiologyFinding survived = second.Findings.Single(f =>
            f.FindingType.Equals(material.FindingType, StringComparison.OrdinalIgnoreCase)
            && f.Level.Equals(material.Level, StringComparison.OrdinalIgnoreCase)
            && f.Laterality == material.Laterality);
        Assert.That(survived.Acknowledgment, Is.EqualTo(FindingAcknowledgment.Acknowledged));
        Assert.That(survived.DispositionedBy, Is.EqualTo("DOCTOR1"));
    }
}

// ─── The workflow façade: what the Radiology page actually calls ─────────────

[TestFixture]
public class RadiologyFindingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    [Test]
    public async Task Facade_ExtractsFromTheFiledReport_AndDispositionsThroughTheWorkflow()
    {
        string pid = $"RADPAT-{Guid.NewGuid()}";
        var wf = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

        string radiologyId = await wf.OrderRadiologyStudyAsync(
            "MRI CERVICAL SPINE W/O CONTRAST", null, "72141", "MRI",
            "PROV-1", "Dr. A", "ROUTINE", null, "Radiculopathy", null, null, null);

        // No filed report yet — extraction must refuse, not run on nothing.
        Assert.That(async () => await wf.ExtractRadiologyFindingsAsync(radiologyId, "DOCTOR1"),
            Throws.InvalidOperationException);
        Assert.That(await wf.GetRadiologyFindingsAsync(radiologyId), Is.Null,
            "no extraction has been run, so the read must say so rather than return an empty shell");

        await wf.CompleteRadiologyAsync(radiologyId,
            RadiologyTestData.SyntheticCervicalReport, "Multilevel degenerative change.",
            "RAD-1", "Dr. R");

        RadiologyExtractionState state = await wf.ExtractRadiologyFindingsAsync(radiologyId, "DOCTOR1");
        Assert.That(state.PatientId, Is.EqualTo(pid));
        Assert.That(state.Findings, Is.Not.Empty);
        Assert.That(state.Findings.All(f => f.QuoteVerified), Is.True);

        string materialId = state.Findings.First(f => f.RequiresAcknowledgment).FindingId;
        await wf.AcknowledgeRadiologyFindingAsync(radiologyId, materialId, "DOCTOR1");

        RadiologyExtractionState? readBack = await wf.GetRadiologyFindingsAsync(radiologyId);
        Assert.That(readBack, Is.Not.Null);
        Assert.That(readBack!.Findings.Single(f => f.FindingId == materialId).Acknowledgment,
            Is.EqualTo(FindingAcknowledgment.Acknowledged));
    }
}
