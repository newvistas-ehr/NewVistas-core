// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for DS4P (Data Segmentation for Privacy) grains.
/// §170.315(b)(7) — Security tags — summary of care — send.
/// §170.315(b)(8) — Security tags — summary of care — receive.
/// </summary>
[TestFixture]
public class Ds4pGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── DS4P Processor (Receive — §170.315(b)(8)) ───────────────────────

    [Test]
    public async Task Ds4pProcessor_CanDetectDs4pTemplateId()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("R", includeDs4pTemplate: true, sensitivityCodes: ["ETH"]);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        Assert.That(result.HasDs4pTemplateId, Is.True);
        Assert.That(result.DocumentConfidentialityCode, Is.EqualTo("R"));
    }

    [Test]
    public async Task Ds4pProcessor_CanDetectRestrictedConfidentiality()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("R", includeDs4pTemplate: false, sensitivityCodes: []);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        Assert.That(result.DocumentConfidentialityCode, Is.EqualTo("R"));
    }

    [Test]
    public async Task Ds4pProcessor_NormalDocumentHasNoTags()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("N", includeDs4pTemplate: false, sensitivityCodes: []);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.HasDs4pTags, Is.False);
        Assert.That(result.DocumentConfidentialityCode, Is.EqualTo("N"));
    }

    [Test]
    public async Task Ds4pProcessor_CanDetectSectionLevelTags()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("R", includeDs4pTemplate: true, sensitivityCodes: ["ETH", "PSY"],
            includeSectionTags: true);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        Assert.That(result.SectionTags.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task Ds4pProcessor_CanDetectObligationPolicies()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("R", includeDs4pTemplate: true, sensitivityCodes: ["HIV"],
            includeSectionTags: true, includeObligations: true);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.ObligationPolicies, Has.Count.GreaterThan(0));
        Assert.That(result.ObligationPolicies, Contains.Item("NOREDISCLOSURE"));
    }

    [Test]
    public async Task Ds4pProcessor_CanDetectRefrainPolicies()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("R", includeDs4pTemplate: true, sensitivityCodes: ["ETH"],
            includeSectionTags: true, includeRefrains: true);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.RefrainPolicies, Has.Count.GreaterThan(0));
        Assert.That(result.RefrainPolicies, Contains.Item("NORDSCLCD"));
    }

    [Test]
    public async Task Ds4pProcessor_PersistsAndRetrievesResult()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("R", includeDs4pTemplate: true, sensitivityCodes: ["PSY"]);
        await proc.AnalyzeCcdaAsync(ccda);

        Ds4pAnalysisResult retrieved = await proc.GetAnalysisAsync();

        Assert.That(retrieved.HasDs4pTags, Is.True);
        Assert.That(retrieved.DocumentConfidentialityCode, Is.EqualTo("R"));
    }

    [Test]
    public async Task Ds4pProcessor_HandlesInvalidXml()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync("not valid xml <>");

        Assert.That(result.HasDs4pTags, Is.False);
    }

    [Test]
    public async Task Ds4pProcessor_VeryRestrictedConfidentiality()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("V", includeDs4pTemplate: true, sensitivityCodes: ["HIV"]);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        Assert.That(result.DocumentConfidentialityCode, Is.EqualTo("V"));
    }

    [Test]
    public async Task Ds4pProcessor_MultipleSensitivityCodes()
    {
        string messageId = $"MSG-{Guid.NewGuid()}";
        IDs4pProcessorGrain proc = _cluster.GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");

        string ccda = BuildDs4pCcdaXml("R", includeDs4pTemplate: true,
            sensitivityCodes: ["ETH", "PSY", "HIV"], includeSectionTags: true);
        Ds4pAnalysisResult result = await proc.AnalyzeCcdaAsync(ccda);

        Assert.That(result.HasDs4pTags, Is.True);
        Assert.That(result.SectionTags.Count, Is.GreaterThan(0));

        // At least one section should have multiple sensitivity codes
        Ds4pSectionTag? taggedSection = result.SectionTags.FirstOrDefault(s => s.SensitivityCodes.Count > 0);
        Assert.That(taggedSection, Is.Not.Null);
    }

    // ─── DS4P Generator (Send — §170.315(b)(7)) ─────────────────────────

    [Test]
    public async Task Ds4pGenerator_ProducesDs4pTaggedCcda()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("Test, Patient", "M", new DateTime(1990, 1, 15), null);

        string ccda = await w.GenerateDs4pCcdaAsync("CCD", ["ETH"]);

        Assert.That(ccda, Does.Contain("2.16.840.1.113883.3.3251.1.1")); // DS4P template
        Assert.That(ccda, Does.Contain("code=\"R\"")); // Restricted confidentiality
        Assert.That(ccda, Does.Contain("Restricted")); // Display name
    }

    [Test]
    public async Task Ds4pGenerator_IncludesSensitivityCategoryCodes()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("Test, Patient", "F", new DateTime(1985, 6, 20), null);

        string ccda = await w.GenerateDs4pCcdaAsync("CCD", ["PSY", "HIV"]);

        Assert.That(ccda, Does.Contain("PSY")); // Mental Health
        Assert.That(ccda, Does.Contain("HIV")); // HIV/AIDS
    }

    [Test]
    public async Task Ds4pGenerator_IncludesObligationPolicy()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("Test, Patient", "M", new DateTime(1970, 3, 10), null);

        string ccda = await w.GenerateDs4pCcdaAsync("CCD", ["ETH"]);

        Assert.That(ccda, Does.Contain("NOREDISCLOSURE"));
    }

    [Test]
    public async Task Ds4pGenerator_IncludesRefrainPolicy()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("Test, Patient", "F", new DateTime(1995, 11, 22), null);

        string ccda = await w.GenerateDs4pCcdaAsync("Referral", ["SDV"]);

        Assert.That(ccda, Does.Contain("NORDSCLCD"));
    }

    [Test]
    public async Task Ds4pGenerator_SectionLevelConfidentialityOnMeds()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("Test, Patient", "M", new DateTime(1988, 7, 4), null);

        // ETH triggers section-level tags on Medications
        string ccda = await w.GenerateDs4pCcdaAsync("CCD", ["ETH"]);

        // Should have section-level security observations
        Assert.That(ccda, Does.Contain("2.16.840.1.113883.3.3251.1.4")); // Security Observation template
        Assert.That(ccda, Does.Contain("SECCLASSOBS")); // Classification observation
        Assert.That(ccda, Does.Contain("SECCATOBS")); // Category observation
    }

    [Test]
    public async Task Ds4pGenerator_RoundTripGenerateAndAnalyze()
    {
        // Generate a DS4P-tagged C-CDA, then analyze it — should detect all tags
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("RoundTrip, Test", "M", new DateTime(1980, 1, 1), null);

        string ccda = await w.GenerateDs4pCcdaAsync("CCD", ["ETH", "PSY"]);

        string messageId = $"MSG-{Guid.NewGuid()}";
        Ds4pAnalysisResult analysis = await w.AnalyzeDs4pCcdaAsync(messageId, ccda);

        Assert.That(analysis.HasDs4pTags, Is.True);
        Assert.That(analysis.HasDs4pTemplateId, Is.True);
        Assert.That(analysis.DocumentConfidentialityCode, Is.EqualTo("R"));
        Assert.That(analysis.SectionTags.Count, Is.GreaterThan(0));
        Assert.That(analysis.ObligationPolicies, Contains.Item("NOREDISCLOSURE"));
        Assert.That(analysis.RefrainPolicies, Contains.Item("NORDSCLCD"));
    }

    [Test]
    public async Task Ds4pGenerator_DischargeSummaryType()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await w.UpdateDemographicsAsync("Discharge, Test", "F", new DateTime(1975, 5, 15), null);

        string ccda = await w.GenerateDs4pCcdaAsync("Discharge", ["ETH"]);

        Assert.That(ccda, Does.Contain("Discharge Summary"));
        Assert.That(ccda, Does.Contain("2.16.840.1.113883.3.3251.1.1")); // DS4P template
    }

    // ─── Helper: Build DS4P-tagged C-CDA XML for testing ─────────────────

    private static string BuildDs4pCcdaXml(string confidentialityCode,
        bool includeDs4pTemplate, List<string> sensitivityCodes,
        bool includeSectionTags = false, bool includeObligations = false,
        bool includeRefrains = false)
    {
        string ns = "urn:hl7-org:v3";
        string ds4pTemplate = includeDs4pTemplate
            ? $"<templateId xmlns=\"{ns}\" root=\"2.16.840.1.113883.3.3251.1.1\"/>" : string.Empty;

        string sectionEntries = string.Empty;
        if (includeSectionTags)
        {
            string catObs = string.Join("\n", sensitivityCodes.Select(c =>
                $@"<component xmlns=""{ns}""><observation classCode=""OBS"" moodCode=""EVN"">
                    <templateId root=""2.16.840.1.113883.3.3251.1.4""/>
                    <code code=""SECCATOBS"" codeSystem=""2.16.840.1.113883.1.11.20457"" displayName=""Security Category"" codeSystemName=""ObservationValue""/>
                    <value xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""CE"" code=""{c}"" codeSystem=""2.16.840.1.113883.1.11.20428""/>
                </observation></component>"));

            string obligObs = includeObligations
                ? $@"<component xmlns=""{ns}""><observation classCode=""OBS"" moodCode=""EVN"">
                    <templateId root=""2.16.840.1.113883.3.3251.1.4""/>
                    <code code=""SECCONOBS"" codeSystem=""2.16.840.1.113883.1.11.20457"" displayName=""Security Control"" codeSystemName=""ObservationValue""/>
                    <value xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""CE"" code=""NOREDISCLOSURE"" codeSystem=""2.16.840.1.113883.1.11.20445""/>
                </observation></component>"
                : string.Empty;

            string refrainObs = includeRefrains
                ? $@"<component xmlns=""{ns}""><observation classCode=""OBS"" moodCode=""EVN"">
                    <templateId root=""2.16.840.1.113883.3.3251.1.4""/>
                    <code code=""SECCONOBS"" codeSystem=""2.16.840.1.113883.1.11.20457"" displayName=""Security Control"" codeSystemName=""ObservationValue""/>
                    <value xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xsi:type=""CE"" code=""NORDSCLCD"" codeSystem=""2.16.840.1.113883.1.11.20446""/>
                </observation></component>"
                : string.Empty;

            sectionEntries = $@"<entry xmlns=""{ns}"" typeCode=""COMP"">
                <organizer classCode=""CLUSTER"" moodCode=""EVN"">
                    <templateId root=""2.16.840.1.113883.3.3251.1.4""/>
                    <statusCode code=""completed""/>
                    {catObs}
                    {obligObs}
                    {refrainObs}
                </organizer>
            </entry>";
        }

        string sectionConfCode = includeSectionTags
            ? $@"<confidentialityCode xmlns=""{ns}"" code=""R"" codeSystem=""2.16.840.1.113883.5.25""/>"
            : string.Empty;

        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ClinicalDocument xmlns=""{ns}"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
    {ds4pTemplate}
    <confidentialityCode code=""{confidentialityCode}"" codeSystem=""2.16.840.1.113883.5.25""/>
    <component>
        <structuredBody>
            <component>
                <section>
                    <code code=""10160-0"" codeSystem=""2.16.840.1.113883.6.1""/>
                    <title>Medications</title>
                    {sectionConfCode}
                    <text><paragraph>Test medication data</paragraph></text>
                    {sectionEntries}
                </section>
            </component>
        </structuredBody>
    </component>
</ClinicalDocument>";
    }
}
