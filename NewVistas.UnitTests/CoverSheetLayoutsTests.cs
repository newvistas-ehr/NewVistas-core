// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using NewVistas.Abstractions.Clinical;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the specialty cover-sheet layout registry + the per-patient-then-viewer resolution.
/// </summary>
[TestFixture]
public class CoverSheetLayoutsTests
{
    [Test]
    public void Resolve_KnownIds_ReturnEachLayout()
    {
        Assert.That(CoverSheetLayouts.Resolve("general").Id, Is.EqualTo(CoverSheetLayouts.General));
        Assert.That(CoverSheetLayouts.Resolve("oncology").Id, Is.EqualTo(CoverSheetLayouts.Oncology));
        Assert.That(CoverSheetLayouts.Resolve("procedural").Id, Is.EqualTo(CoverSheetLayouts.Procedural));
    }

    [Test]
    public void Resolve_UnknownOrNull_FallsBackToGeneral()
    {
        Assert.That(CoverSheetLayouts.Resolve("nope").Id, Is.EqualTo(CoverSheetLayouts.General));
        Assert.That(CoverSheetLayouts.Resolve(null).Id, Is.EqualTo(CoverSheetLayouts.General));
    }

    [Test]
    public void All_ThreeLayouts_EachWithNameAndSections()
    {
        Assert.That(CoverSheetLayouts.All, Has.Count.EqualTo(3));
        foreach (CoverSheetLayout l in CoverSheetLayouts.All)
        {
            Assert.That(l.Name, Is.Not.Empty);
            Assert.That(l.Sections, Is.Not.Empty);
        }
    }

    [Test]
    public void MapViewerRole_Surgery_Procedural()
    {
        Assert.That(CoverSheetLayouts.MapViewerRole("SURGERY"), Is.EqualTo(CoverSheetLayouts.Procedural));
        Assert.That(CoverSheetLayouts.MapViewerRole("Orthopedic Surgery"), Is.EqualTo(CoverSheetLayouts.Procedural));
    }

    [Test]
    public void MapViewerRole_Oncology_Oncology()
    {
        Assert.That(CoverSheetLayouts.MapViewerRole("ONCOLOGY"), Is.EqualTo(CoverSheetLayouts.Oncology));
        Assert.That(CoverSheetLayouts.MapViewerRole("Hematology/Oncology"), Is.EqualTo(CoverSheetLayouts.Oncology));
    }

    [Test]
    public void MapViewerRole_PrimaryCare_General()
    {
        Assert.That(CoverSheetLayouts.MapViewerRole("MEDICINE"), Is.EqualTo(CoverSheetLayouts.General));
        Assert.That(CoverSheetLayouts.MapViewerRole("Family Practice"), Is.EqualTo(CoverSheetLayouts.General));
    }

    [Test]
    public void MapViewerRole_UnmappedOrEmpty_Null()
    {
        Assert.That(CoverSheetLayouts.MapViewerRole("Cardiology"), Is.Null);
        Assert.That(CoverSheetLayouts.MapViewerRole(null), Is.Null);
        Assert.That(CoverSheetLayouts.MapViewerRole(""), Is.Null);
    }

    [Test]
    public void ResolveDefault_PatientContextOnly_NoViewer()
    {
        Assert.That(CoverSheetLayouts.ResolveDefault(false, false, null).LayoutId, Is.EqualTo(CoverSheetLayouts.General));
        Assert.That(CoverSheetLayouts.ResolveDefault(true, false, null).LayoutId, Is.EqualTo(CoverSheetLayouts.Oncology));
        Assert.That(CoverSheetLayouts.ResolveDefault(false, true, null).LayoutId, Is.EqualTo(CoverSheetLayouts.Procedural));
    }

    [Test]
    public void ResolveDefault_CancerAndSurgery_PatientLoudestIsOncology()
    {
        // Patient-context tiebreak: serious cancer outranks an elective surgery.
        Assert.That(CoverSheetLayouts.ResolveDefault(true, true, null).LayoutId, Is.EqualTo(CoverSheetLayouts.Oncology));
    }

    [Test]
    public void ResolveDefault_SurgeonViewer_CancerAndSurgery_PicksProceduralLens()
    {
        (string layoutId, string reason) = CoverSheetLayouts.ResolveDefault(true, true, "SURGERY");
        Assert.That(layoutId, Is.EqualTo(CoverSheetLayouts.Procedural));
        Assert.That(reason, Does.Contain("viewer"));
    }

    [Test]
    public void ResolveDefault_SurgeonViewer_NoSurgery_FallsBackToPatientLoudest()
    {
        // Surgeon viewer, but the patient has no surgery → procedural not relevant → patient-loudest (oncology).
        Assert.That(CoverSheetLayouts.ResolveDefault(true, false, "SURGERY").LayoutId, Is.EqualTo(CoverSheetLayouts.Oncology));
    }

    [Test]
    public void ResolveDefault_OncologistViewer_MatchesPatientLoudest()
    {
        Assert.That(CoverSheetLayouts.ResolveDefault(true, true, "ONCOLOGY").LayoutId, Is.EqualTo(CoverSheetLayouts.Oncology));
    }

    [Test]
    public void ResolveDefault_UnmappedViewer_UsesPatientLoudest()
    {
        // A cardiologist (unmapped) viewing a surgical patient → patient-loudest (procedural).
        Assert.That(CoverSheetLayouts.ResolveDefault(false, true, "Cardiology").LayoutId, Is.EqualTo(CoverSheetLayouts.Procedural));
    }

    [Test]
    public void ResolveDefault_GeneralistViewer_NeverBuriesASpecialtyConcern()
    {
        // A PCP/generalist lens does NOT demote a specialty concern (true patient-then-viewer):
        Assert.That(CoverSheetLayouts.ResolveDefault(true, false, "MEDICINE").LayoutId, Is.EqualTo(CoverSheetLayouts.Oncology));
        Assert.That(CoverSheetLayouts.ResolveDefault(false, true, "Family Practice").LayoutId, Is.EqualTo(CoverSheetLayouts.Procedural));
    }

    [Test]
    public void ResolveDefault_GeneralistViewer_NoSpecialtyConcern_IsGeneral()
    {
        Assert.That(CoverSheetLayouts.ResolveDefault(false, false, "MEDICINE").LayoutId, Is.EqualTo(CoverSheetLayouts.General));
    }
}
