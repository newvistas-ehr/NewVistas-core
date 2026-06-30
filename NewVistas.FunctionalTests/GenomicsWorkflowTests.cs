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
/// Functional tests for the Hereditary Genetics &amp; Family History module: recording coded genetic
/// test reports + reportable variants, the curated hereditary-risk assessment (germline pathogenic
/// variant → syndrome finding), and structured family history feeding the referral red-flag engine.
/// End-to-end via <see cref="IPatientWorkflowGrain"/> on the shared TestCluster.
/// </summary>
[TestFixture]
public class GenomicsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>Records a genetic test report for the patient with sensible defaults; returns the report id.</summary>
    private Task<string> RecordReport(IPatientWorkflowGrain wf) =>
        wf.RecordGeneticTestReportAsync(
            "Hereditary Cancer Panel", "Genomics Lab", GeneticTestMethod.NextGenSequencing,
            "Family history of cancer", new DateTime(2026, 1, 10), new DateTime(2026, 1, 20),
            GeneticReportResult.PositivePathogenic, "DOCTOR1", "", "TEST");

    /// <summary>Adds a variant to a report with sensible defaults.</summary>
    private Task AddVariant(
        IPatientWorkflowGrain wf, string reportId, string gene,
        VariantClassification classification, VariantOrigin origin = VariantOrigin.Germline) =>
        wf.AddGeneticVariantAsync(
            reportId, gene, "c.68_69delAG", "p.Glu23ValfsTer17", "NM_007294.4",
            classification, VariantZygosity.Heterozygous, origin, "", "", "");

    // ── Reports / variants ───────────────────────────────────────────────────────────

    [Test]
    public async Task RecordReport_ReturnsReportId_AndProfileShowsIt()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string reportId = await RecordReport(wf);
        Assert.That(reportId, Is.Not.Empty);

        GenomicsState profile = await wf.GetGenomicsProfileAsync();
        Assert.That(profile.Reports, Has.Some.Matches<GeneticTestReport>(r => r.ReportId == reportId));
    }

    [Test]
    public async Task AddVariant_AddsVariantToReport()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string reportId = await RecordReport(wf);
        await AddVariant(wf, reportId, "BRCA1", VariantClassification.Pathogenic);

        GenomicsState profile = await wf.GetGenomicsProfileAsync();
        GeneticTestReport report = profile.Reports.Single(r => r.ReportId == reportId);
        Assert.That(report.Variants, Has.Some.Matches<GeneticVariant>(v => v.Gene == "BRCA1"));
    }

    [Test]
    public async Task RemoveReport_RemovesTheReport()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string reportId = await RecordReport(wf);
        Assert.That((await wf.GetGenomicsProfileAsync()).Reports.Any(r => r.ReportId == reportId), Is.True);

        await wf.RemoveGeneticReportAsync(reportId);

        GenomicsState profile = await wf.GetGenomicsProfileAsync();
        Assert.That(profile.Reports.Any(r => r.ReportId == reportId), Is.False);
    }

    // ── Hereditary findings (end-to-end curated KB) ──────────────────────────────────

    [Test]
    public async Task HereditaryFindings_GermlinePathogenicBrca1_ProducesBrca1Finding()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string reportId = await RecordReport(wf);
        await AddVariant(wf, reportId, "BRCA1", VariantClassification.Pathogenic, VariantOrigin.Germline);

        List<HereditaryFinding> findings = await wf.GetHereditaryFindingsAsync();
        Assert.That(findings, Is.Not.Empty);
        Assert.That(findings[0].Gene, Is.EqualTo("BRCA1"));
    }

    [Test]
    public async Task HereditaryFindings_GermlineVusBrca1_ProducesNoFinding()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string reportId = await RecordReport(wf);
        await AddVariant(wf, reportId, "BRCA1", VariantClassification.UncertainSignificance, VariantOrigin.Germline);

        List<HereditaryFinding> findings = await wf.GetHereditaryFindingsAsync();
        Assert.That(findings, Is.Empty);
    }

    // ── Family history ───────────────────────────────────────────────────────────────

    [Test]
    public async Task AddFamilyMember_ReturnsMemberId_AndHistoryShowsIt()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string memberId = await wf.AddFamilyMemberAsync(
            FamilyRelationship.Mother, "Jane Doe", "F", FamilyVitalStatus.Alive, 68, null, "", "");
        Assert.That(memberId, Is.Not.Empty);

        FamilyHistoryState history = await wf.GetFamilyHistoryAsync();
        FamilyMemberHistoryEntry member = history.Members.Single(m => m.MemberId == memberId);
        Assert.That(member.Relationship, Is.EqualTo(FamilyRelationship.Mother));
    }

    [Test]
    public async Task AddFamilyCondition_AddsConditionToMember()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string memberId = await wf.AddFamilyMemberAsync(
            FamilyRelationship.Mother, "Jane Doe", "F", FamilyVitalStatus.Alive, 68, null, "", "");
        await wf.AddFamilyConditionAsync(memberId, "Breast cancer", "", 44, "");

        FamilyHistoryState history = await wf.GetFamilyHistoryAsync();
        FamilyMemberHistoryEntry member = history.Members.Single(m => m.MemberId == memberId);
        Assert.That(member.Conditions, Has.Some.Matches<FamilyConditionEntry>(c => c.Condition == "Breast cancer"));
    }

    [Test]
    public async Task RemoveFamilyMember_RemovesTheMember()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string memberId = await wf.AddFamilyMemberAsync(
            FamilyRelationship.Brother, "John Doe", "M", FamilyVitalStatus.Alive, 50, null, "", "");
        Assert.That((await wf.GetFamilyHistoryAsync()).Members.Any(m => m.MemberId == memberId), Is.True);

        await wf.RemoveFamilyMemberAsync(memberId);

        FamilyHistoryState history = await wf.GetFamilyHistoryAsync();
        Assert.That(history.Members.Any(m => m.MemberId == memberId), Is.False);
    }

    [Test]
    public async Task FamilyRiskFlags_MaternalAuntOvarianCancer_FlagsOvarian()
    {
        string patientId = $"GEN-PT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string memberId = await wf.AddFamilyMemberAsync(
            FamilyRelationship.MaternalAunt, "Aunt Sue", "F", FamilyVitalStatus.Deceased, null, 60, "Ovarian cancer", "");
        await wf.AddFamilyConditionAsync(memberId, "Ovarian cancer", "", 58, "");

        List<FamilyRiskFlag> flags = await wf.GetFamilyRiskFlagsAsync();
        Assert.That(flags, Is.Not.Empty);
        Assert.That(flags, Has.Some.Matches<FamilyRiskFlag>(f => f.Pattern.Contains("Ovarian")));
    }
}
