// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for DS4P (Data Segmentation for Privacy) workflows.
/// §170.315(b)(7) — Security tags — summary of care — send.
/// §170.315(b)(8) — Security tags — summary of care — receive.
///
/// Tests end-to-end workflows through the PatientWorkflowGrain:
/// generating DS4P-tagged C-CDAs, analyzing received documents,
/// and round-trip verification.
/// </summary>
[TestFixture]
public class Ds4pWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> SetupPatient(string patientId)
    {
        IPatientWorkflowGrain w = Workflow(patientId);
        await w.UpdateDemographicsAsync("Ds4pTest, Patient", "M", new DateTime(1980, 5, 15), null);
        return patientId;
    }

    // ─── Generate → Analyze round-trip ───────────────────────────────────

    [Test]
    public async Task Ds4pWorkflow_GenerateAndAnalyzeSubstanceAbuse()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        // Generate DS4P-tagged C-CDA with ETH (substance abuse) tag
        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("CCD", ["ETH"]);
        Assert.That(ccda, Is.Not.Empty);
        Assert.That(ccda, Does.Contain("2.16.840.1.113883.3.3251.1.1")); // DS4P template

        // Analyze the generated C-CDA
        string msgId = $"MSG-{Guid.NewGuid()}";
        Ds4pAnalysisResult result = await Workflow(patientId).AnalyzeDs4pCcdaAsync(msgId, ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        Assert.That(result.HasDs4pTemplateId, Is.True);
        Assert.That(result.DocumentConfidentialityCode, Is.EqualTo("R"));
    }

    [Test]
    public async Task Ds4pWorkflow_GenerateAndAnalyzeMentalHealth()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("CCD", ["PSY"]);
        string msgId = $"MSG-{Guid.NewGuid()}";
        Ds4pAnalysisResult result = await Workflow(patientId).AnalyzeDs4pCcdaAsync(msgId, ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        Assert.That(result.SectionTags.Count, Is.GreaterThan(0));
        Assert.That(result.ObligationPolicies, Contains.Item("NOREDISCLOSURE"));
    }

    [Test]
    public async Task Ds4pWorkflow_GenerateAndAnalyzeHiv()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("CCD", ["HIV"]);
        string msgId = $"MSG-{Guid.NewGuid()}";
        Ds4pAnalysisResult result = await Workflow(patientId).AnalyzeDs4pCcdaAsync(msgId, ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        // HIV triggers Results section tagging
        Assert.That(result.SectionTags.Any(s => s.SectionCode == "30954-2"), Is.True);
    }

    [Test]
    public async Task Ds4pWorkflow_GenerateAndAnalyzeMultipleCategories()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("CCD", ["ETH", "PSY", "HIV"]);
        string msgId = $"MSG-{Guid.NewGuid()}";
        Ds4pAnalysisResult result = await Workflow(patientId).AnalyzeDs4pCcdaAsync(msgId, ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        // Should have multiple tagged sections
        Assert.That(result.SectionTags.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task Ds4pWorkflow_ReferralDocument()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("Referral", ["SDV"]);

        Assert.That(ccda, Does.Contain("Referral Summary"));
        Assert.That(ccda, Does.Contain("code=\"R\""));
    }

    [Test]
    public async Task Ds4pWorkflow_DischargeDocument()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("Discharge", ["GDIS"]);

        Assert.That(ccda, Does.Contain("Discharge Summary"));
        Assert.That(ccda, Does.Contain("2.16.840.1.113883.3.3251.1.1"));
    }

    // ─── Persistence ─────────────────────────────────────────────────────

    [Test]
    public async Task Ds4pWorkflow_AnalysisPersistedAndRetrievable()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("CCD", ["ETH"]);
        string msgId = $"MSG-{Guid.NewGuid()}";
        await Workflow(patientId).AnalyzeDs4pCcdaAsync(msgId, ccda);

        // Retrieve the stored analysis
        Ds4pAnalysisResult retrieved = await Workflow(patientId).GetDs4pAnalysisAsync(msgId);

        Assert.That(retrieved.HasDs4pTags, Is.True);
        Assert.That(retrieved.DocumentConfidentialityCode, Is.EqualTo("R"));
    }

    // ─── Obligation and Refrain Policies ─────────────────────────────────

    [Test]
    public async Task Ds4pWorkflow_ObligationAndRefrainPoliciesPresent()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("CCD", ["ETH"]);
        string msgId = $"MSG-{Guid.NewGuid()}";
        Ds4pAnalysisResult result = await Workflow(patientId).AnalyzeDs4pCcdaAsync(msgId, ccda);

        Assert.That(result.ObligationPolicies, Contains.Item("NOREDISCLOSURE"));
        Assert.That(result.RefrainPolicies, Contains.Item("NORDSCLCD"));
    }

    // ─── Patient Isolation ───────────────────────────────────────────────

    [Test]
    public async Task Ds4pWorkflow_DifferentPatientsIsolated()
    {
        string patient1 = await SetupPatient($"PATIENT-{Guid.NewGuid()}");
        string patient2 = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda1 = await Workflow(patient1).GenerateDs4pCcdaAsync("CCD", ["ETH"]);
        string ccda2 = await Workflow(patient2).GenerateDs4pCcdaAsync("CCD", ["HIV"]);

        // Both should generate valid DS4P-tagged C-CDAs
        Assert.That(ccda1, Does.Contain("2.16.840.1.113883.3.3251.1.1"));
        Assert.That(ccda2, Does.Contain("2.16.840.1.113883.3.3251.1.1"));
        // But they should be different documents
        Assert.That(ccda1, Is.Not.EqualTo(ccda2));
    }

    // ─── Genetic / GINA ──────────────────────────────────────────────────

    [Test]
    public async Task Ds4pWorkflow_GeneticInformationTagging()
    {
        string patientId = await SetupPatient($"PATIENT-{Guid.NewGuid()}");

        string ccda = await Workflow(patientId).GenerateDs4pCcdaAsync("CCD", ["GDIS"]);
        string msgId = $"MSG-{Guid.NewGuid()}";
        Ds4pAnalysisResult result = await Workflow(patientId).AnalyzeDs4pCcdaAsync(msgId, ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        // Genetic triggers Results section tagging
        Assert.That(result.SectionTags.Any(s => s.SectionCode == "30954-2"), Is.True);
    }
}
