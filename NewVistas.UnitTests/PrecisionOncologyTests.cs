// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the precision-oncology biomarker -> therapy matcher (PRECISION_ONCOLOGY).
/// Pure logic — no Orleans cluster needed.
/// </summary>
[TestFixture]
public class PrecisionOncologyTests
{
    private static TumorBiomarker Marker(string gene, BiomarkerStatus status, string result = "") =>
        new() { Gene = gene, Status = status, Result = result };

    [Test]
    public void Match_PositiveEgfr_SuggestsEgfrTki()
    {
        var matches = PrecisionOncology.Match([Marker("EGFR", BiomarkerStatus.Positive, "exon 19 deletion")]);

        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].TherapyClass, Does.Contain("EGFR"));
        Assert.That(matches[0].ExampleAgents, Does.Contain("osimertinib"));
        Assert.That(matches[0].Finding, Is.EqualTo("exon 19 deletion"));
    }

    [Test]
    public void Match_NegativeBiomarker_ProducesNoMatch()
    {
        var matches = PrecisionOncology.Match([Marker("EGFR", BiomarkerStatus.Negative)]);
        Assert.That(matches, Is.Empty);
    }

    [Test]
    public void Match_PendingOrEquivocal_ProducesNoMatch()
    {
        var matches = PrecisionOncology.Match(
        [
            Marker("ALK", BiomarkerStatus.Pending),
            Marker("BRAF", BiomarkerStatus.Equivocal)
        ]);
        Assert.That(matches, Is.Empty);
    }

    [Test]
    public void Match_PdL1Alias_NormalizesAndMatches()
    {
        // "PDL1" (no hyphen) must normalize to the "PD-L1" rule.
        var matches = PrecisionOncology.Match([Marker("PDL1", BiomarkerStatus.Positive, "TPS 60%")]);

        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].TherapyClass, Does.Contain("checkpoint inhibitor"));
    }

    [Test]
    public void Match_Erbb2Alias_MapsToHer2()
    {
        var matches = PrecisionOncology.Match([Marker("ERBB2", BiomarkerStatus.Positive, "amplified")]);

        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].TherapyClass, Does.Contain("HER2"));
    }

    [Test]
    public void Match_MultipleActionable_ReturnsAll()
    {
        var matches = PrecisionOncology.Match(
        [
            Marker("EGFR", BiomarkerStatus.Positive),
            Marker("MSI", BiomarkerStatus.Positive, "MSI-High"),
            Marker("KRAS", BiomarkerStatus.Negative)   // ignored
        ]);
        Assert.That(matches, Has.Count.EqualTo(2));
    }

    [Test]
    public void Match_NullOrEmpty_ReturnsEmpty()
    {
        Assert.That(PrecisionOncology.Match(null), Is.Empty);
        Assert.That(PrecisionOncology.Match([]), Is.Empty);
    }

    [Test]
    public void KnownMarkers_IncludeKeyActionableGenes()
    {
        Assert.That(PrecisionOncology.KnownMarkers,
            Does.Contain("EGFR").And.Contain("ALK").And.Contain("BRCA1").And.Contain("MSI"));
    }
}
