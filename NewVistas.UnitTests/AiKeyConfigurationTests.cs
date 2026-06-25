// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;
using NewVistas.AI;

namespace NewVistas.UnitTests;

// The API key is the user's own secret. A downloader supplies theirs via the
// ANTHROPIC_API_KEY env var (or ClinicalNarrative:ApiKey); if they turn live AI on without
// one, the app must NOT crash — it must serve the offline grounded output and tell them how
// to configure a key. These tests lock that contract in.
[TestFixture]
public class AiKeyConfigurationTests
{
    [Test]
    public void ResolveApiKey_PrefersTheConfiguredKey()
    {
        var options = new ClinicalNarrativeOptions { ApiKey = "sk-ant-configured" };
        Assert.That(options.ResolveApiKey(), Is.EqualTo("sk-ant-configured"));
    }

    [Test]
    public void ResolveApiKey_FallsBackToEnvironmentVariable_WhenConfigIsEmpty()
    {
        string? original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-from-env");
            var options = new ClinicalNarrativeOptions { ApiKey = null };
            Assert.That(options.ResolveApiKey(), Is.EqualTo("sk-ant-from-env"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }

    [Test]
    public void ApiKeyHelpText_TellsTheUserHowToInputAndGetAKey()
    {
        // The notice must teach both halves: where to get a key, and how to supply it.
        Assert.That(ClinicalNarrativeOptions.ApiKeyHelpText, Does.Contain("console.anthropic.com"));
        Assert.That(ClinicalNarrativeOptions.ApiKeyHelpText, Does.Contain("ANTHROPIC_API_KEY"));
    }

    [Test]
    public async Task MisconfiguredNarrative_ServesGroundedSummary_WithSetupNotice_AndNeverCallsTheModel()
    {
        var service = new MisconfiguredClinicalNarrativeService();
        var context = new ClinicalSummaryContext
        {
            PatientId = "P1",
            Purpose = "office visit",
            Facts =
            [
                new ClinicalFact { FactId = "F1", Category = ClinicalFactCategory.Problem, Text = "Low back pain" },
                new ClinicalFact { FactId = "F2", Category = ClinicalFactCategory.Medication, Text = "Lyrica 150mg" },
            ],
        };

        NarrativeResult result = await service.ComposeAsync(context);

        Assert.That(service.IsLiveModel, Is.False);               // no model was called
        Assert.That(result.Narrative, Is.Not.Empty);              // still a grounded summary
        Assert.That(result.Claims, Is.Not.Empty);
        Assert.That(result.ConfigurationNotice, Is.Not.Null.And.Contain("ANTHROPIC_API_KEY"));
    }

    [Test]
    public async Task MisconfiguredRadiology_ServesGroundedFindings_WithSetupNotice()
    {
        var extractor = new MisconfiguredRadiologyFindingExtractor();
        const string report = "At C5-C6 there is moderate to severe left neural foraminal stenosis.";

        RadiologyExtractionResult result = await extractor.ExtractAsync(report);

        Assert.That(extractor.IsLiveModel, Is.False);
        Assert.That(result.Findings, Is.Not.Empty);               // heuristic still grounded the finding
        Assert.That(result.ConfigurationNotice, Is.Not.Null.And.Contain("console.anthropic.com"));
    }

    [Test]
    public void AddClinicalNarrativeAi_WhenDisabled_RegistersNothing()
    {
        var services = new ServiceCollection();
        services.AddClinicalNarrativeAi(new ClinicalNarrativeOptions { Enabled = false });

        // Disabled is a no-op: the host's offline template default remains the active service.
        Assert.That(services.Any(d => d.ServiceType == typeof(IClinicalNarrativeService)), Is.False);
    }
}
