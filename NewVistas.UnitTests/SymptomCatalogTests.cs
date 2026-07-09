// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the curated coded-symptom catalog — the closed surveillance vocabulary and the
/// survey question-set resolver. Pure logic — no Orleans cluster (the class under test is static).
/// </summary>
[TestFixture]
public class SymptomCatalogTests
{
    private const string AnosmiaCode = "44169009";     // loss of smell — core screen, Sensory
    private const string TinnitusCode = "60862001";    // ringing in ears — NOT core screen

    [Test]
    public void Catalog_HasNoDuplicateCodes()
    {
        int distinct = SymptomCatalog.All.Select(e => e.Code).Distinct().Count();
        Assert.That(SymptomCatalog.All, Has.Count.EqualTo(distinct));
    }

    [Test]
    public void CoreScreen_IsNonEmptySubsetOfAll_AllFlaggedCore()
    {
        Assert.That(SymptomCatalog.CoreScreen, Is.Not.Empty);
        Assert.That(SymptomCatalog.CoreScreen.All(e => e.IsCoreScreen), Is.True);
        Assert.That(SymptomCatalog.CoreScreen.Count, Is.LessThan(SymptomCatalog.All.Count));
    }

    [Test]
    public void Anosmia_IsCoreSensorySymptom()
    {
        SymptomCatalogEntry? e = SymptomCatalog.TryGet(AnosmiaCode);
        Assert.That(e, Is.Not.Null);
        Assert.That(e!.Category, Is.EqualTo(SymptomCategory.Sensory));
        Assert.That(e.IsCoreScreen, Is.True);
    }

    [Test]
    public void Contains_KnownTrue_UnknownFalse()
    {
        Assert.That(SymptomCatalog.Contains(AnosmiaCode), Is.True);
        Assert.That(SymptomCatalog.Contains("NOT-A-CODE"), Is.False);
        Assert.That(SymptomCatalog.Contains(null), Is.False);
    }

    [Test]
    public void BuildSurveyQuestionSet_DefaultsToCoreScreen()
    {
        List<SymptomCatalogEntry> set = SymptomCatalog.BuildSurveyQuestionSet();
        Assert.That(set.Select(e => e.Code), Is.EquivalentTo(SymptomCatalog.CoreScreen.Select(e => e.Code)));
    }

    [Test]
    public void BuildSurveyQuestionSet_UnionsExtraCatalogCodes()
    {
        // Tinnitus is not part of the core screen; an active proto that lists it should surface it.
        Assert.That(SymptomCatalog.CoreScreen.Any(e => e.Code == TinnitusCode), Is.False);

        List<SymptomCatalogEntry> set = SymptomCatalog.BuildSurveyQuestionSet(new[] { TinnitusCode });

        Assert.That(set.Select(e => e.Code), Does.Contain(TinnitusCode));
        Assert.That(set, Has.Count.EqualTo(SymptomCatalog.CoreScreen.Count + 1));
    }

    [Test]
    public void BuildSurveyQuestionSet_IgnoresUnknownCodes()
    {
        List<SymptomCatalogEntry> set = SymptomCatalog.BuildSurveyQuestionSet(new[] { "NOT-A-CODE", "" });
        Assert.That(set, Has.Count.EqualTo(SymptomCatalog.CoreScreen.Count));
    }

    [Test]
    public void BackgroundPrevalence_KnownPositive_UnknownZero()
    {
        Assert.That(SymptomCatalog.BackgroundPrevalenceFor(AnosmiaCode), Is.GreaterThan(0.0));
        Assert.That(SymptomCatalog.BackgroundPrevalenceFor("NOT-A-CODE"), Is.EqualTo(0.0));
    }
}
