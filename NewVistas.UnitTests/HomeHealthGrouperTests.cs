// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Phase-2 Medicare skilled home-health classifiers — the deterministic PDGM
/// grouper (<see cref="HomeHealthGrouper"/>) and the OASIS pre-submission scrubber
/// (<see cref="OasisScrubber"/>). Pure logic — no Orleans cluster needed.
/// </summary>
[TestFixture]
public class HomeHealthGrouperTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>A grouping input with sane defaults; tweak fields per test.</summary>
    private static HomeHealthGroupingInput Input(
        HomeCareAdmissionSource source = HomeCareAdmissionSource.Community,
        bool early = true,
        string primaryDx = "",
        List<string>? secondary = null,
        Dictionary<string, string>? oasis = null,
        int visits = 10) => new()
        {
            AdmissionSource = source,
            IsEarlyPeriod = early,
            PrimaryDiagnosisCode = primaryDx,
            SecondaryDiagnoses = secondary ?? new(),
            OasisItems = oasis ?? new(),
            VisitCount = visits
        };

    /// <summary>OASIS functional items all set to the same response value.</summary>
    private static Dictionary<string, string> FunctionalItems(string value) =>
        OasisItems.FunctionalItems.ToDictionary(item => item, _ => value);

    // ── Timing + admission source → HIPPS first character ─────────────────────────

    [Test]
    public void Group_InstitutionalEarly_HippsStartsWith3()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(
            Input(source: HomeCareAdmissionSource.AcuteHospital, early: true));

        Assert.That(result.CaseMixGroup, Does.StartWith("3"));
        Assert.That(result.AdmissionSource, Is.EqualTo("Institutional"));
        Assert.That(result.Timing, Is.EqualTo("Early"));
    }

    [Test]
    public void Group_CommunityLate_HippsStartsWith2()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(
            Input(source: HomeCareAdmissionSource.Community, early: false));

        Assert.That(result.CaseMixGroup, Does.StartWith("2"));
        Assert.That(result.AdmissionSource, Is.EqualTo("Community"));
        Assert.That(result.Timing, Is.EqualTo("Late"));
    }

    // ── Clinical grouping from the principal diagnosis ────────────────────────────

    [Test]
    public void Group_CardiacPrimaryDx_IsCardiacClinicalGroup()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(Input(primaryDx: "I50.9"));
        Assert.That(result.ClinicalGrouping, Does.Contain("Cardiac"));
    }

    [Test]
    public void Group_WoundPrimaryDx_IsWoundClinicalGroup()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(Input(primaryDx: "L89.90"));
        Assert.That(result.ClinicalGrouping, Does.Contain("Wound"));
    }

    [Test]
    public void Group_MusculoskeletalRehabDx_IsMusculoskeletalClinicalGroup()
    {
        PdgmGroupingResult joint = HomeHealthGrouper.Group(Input(primaryDx: "M17.0"));
        Assert.That(joint.ClinicalGrouping, Does.Contain("Musculoskeletal"));

        PdgmGroupingResult aftercare = HomeHealthGrouper.Group(Input(primaryDx: "Z47.1"));
        Assert.That(aftercare.ClinicalGrouping, Does.Contain("Musculoskeletal"));
    }

    [Test]
    public void Group_UnknownOrEmptyPrimaryDx_IsMmtaOther()
    {
        PdgmGroupingResult unknown = HomeHealthGrouper.Group(Input(primaryDx: "Q99.9"));
        Assert.That(unknown.ClinicalGrouping, Is.EqualTo("MMTA - Other"));

        PdgmGroupingResult empty = HomeHealthGrouper.Group(Input(primaryDx: ""));
        Assert.That(empty.ClinicalGrouping, Is.EqualTo("MMTA - Other"));
    }

    // ── Functional level from OASIS items (HIPPS 3rd character) ────────────────────

    [Test]
    public void Group_LowFunctionalScores_IsLowLevel_AndHippsThirdCharA()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(Input(oasis: FunctionalItems("0")));

        Assert.That(result.FunctionalLevel, Is.EqualTo("Low"));
        Assert.That(result.CaseMixGroup[2], Is.EqualTo('A'));
    }

    [Test]
    public void Group_HighFunctionalScores_IsHighLevel_AndHippsThirdCharC()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(Input(oasis: FunctionalItems("3")));

        Assert.That(result.FunctionalLevel, Is.EqualTo("High"));
        Assert.That(result.CaseMixGroup[2], Is.EqualTo('C'));
    }

    // ── Comorbidity adjustment from secondary diagnoses ───────────────────────────

    [Test]
    public void Group_NoRelevantSecondaryDx_ComorbidityNone()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(
            Input(secondary: new List<string>()));

        Assert.That(result.ComorbidityAdjustment, Is.EqualTo("None"));
    }

    [Test]
    public void Group_TwoRelevantSecondaryDx_ComorbidityHigh()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(
            Input(secondary: new List<string> { "E11.9", "I10" }));

        Assert.That(result.ComorbidityAdjustment, Is.EqualTo("High"));
    }

    // ── LUPA determination from the visit count ───────────────────────────────────

    [Test]
    public void Group_VisitCountBelowThreshold_IsLupa()
    {
        // Low functional level → LUPA threshold 3; a single visit is below it.
        PdgmGroupingResult result = HomeHealthGrouper.Group(
            Input(oasis: FunctionalItems("0"), visits: 1));

        Assert.That(result.FunctionalLevel, Is.EqualTo("Low"));
        Assert.That(result.IsLupa, Is.True);
    }

    [Test]
    public void Group_VisitCountWellAboveThreshold_IsNotLupa()
    {
        PdgmGroupingResult result = HomeHealthGrouper.Group(
            Input(oasis: FunctionalItems("0"), visits: 20));

        Assert.That(result.IsLupa, Is.False);
    }

    // ── OASIS scrubber ────────────────────────────────────────────────────────────

    /// <summary>A complete, valid SOC OASIS data set (functional items + primary dx, version set).</summary>
    private static OasisDataSet CompleteSocOasis()
    {
        var items = FunctionalItems("2");
        items[OasisItems.PrimaryDiagnosis] = "I50.9";
        return new OasisDataSet { Version = "OASIS-E2", Items = items };
    }

    [Test]
    public void Scrub_SocMissingFunctionalItems_IsNotClean_WithIssues()
    {
        var data = new OasisDataSet
        {
            Version = "OASIS-E2",
            Items = new Dictionary<string, string> { [OasisItems.PrimaryDiagnosis] = "I50.9" }
        };

        OasisScrubResult result = OasisScrubber.Scrub(data, HomeCareAssessmentType.OasisStartOfCare);

        Assert.That(result.IsClean, Is.False);
        Assert.That(result.Issues, Is.Not.Empty);
        Assert.That(result.Issues, Has.Some.Contains("M1830"));
    }

    [Test]
    public void Scrub_CompleteSocOasis_IsClean_NoIssues()
    {
        OasisScrubResult result = OasisScrubber.Scrub(CompleteSocOasis(), HomeCareAssessmentType.OasisStartOfCare);

        Assert.That(result.IsClean, Is.True);
        Assert.That(result.Issues, Is.Empty);
    }

    [Test]
    public void Scrub_OutOfRangeFunctionalValue_ProducesIssue()
    {
        OasisDataSet data = CompleteSocOasis();
        data.Items[OasisItems.Bathing] = "9"; // valid scale is 0-6

        OasisScrubResult result = OasisScrubber.Scrub(data, HomeCareAssessmentType.OasisStartOfCare);

        Assert.That(result.IsClean, Is.False);
        Assert.That(result.Issues, Has.Some.Contains(OasisItems.Bathing).And.Some.Contains("out-of-range"));
    }
}
