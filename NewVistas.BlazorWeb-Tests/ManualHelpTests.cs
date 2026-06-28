// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.BlazorWeb.Services;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class ManualHelpTests
{
    [Test]
    public void SectionForRoles_MapsByPriority()
    {
        Assert.That(ManualHelp.SectionForRoles(new[] { "Provider", "OrderEntry" }), Is.EqualTo("doctor"));
        Assert.That(ManualHelp.SectionForRoles(new[] { "Nurse", "OrderEntry" }), Is.EqualTo("nurse"));
        Assert.That(ManualHelp.SectionForRoles(new[] { "Pharmacist" }), Is.EqualTo("pharmacist"));
        Assert.That(ManualHelp.SectionForRoles(new[] { "Administrator" }), Is.EqualTo("admin"));
        // A nurse-practitioner (Provider + Nurse) lands on the doctor manual — they prescribe.
        Assert.That(ManualHelp.SectionForRoles(new[] { "Provider", "Nurse" }), Is.EqualTo("doctor"));
        // Unknown / no roles default to doctor.
        Assert.That(ManualHelp.SectionForRoles(Array.Empty<string>()), Is.EqualTo("doctor"));
    }

    [TestCase("doctor", "medications", "/manual/doctor/prescribing.html")]
    [TestCase("doctor", "orders?type=Pharmacy", "/manual/doctor/orders.html")]
    [TestCase("doctor", "labs", "/manual/doctor/labs.html")]
    [TestCase("doctor", "allergies", "/manual/doctor/allergies.html")]
    [TestCase("doctor", "consults", "/manual/doctor/consults.html")]
    [TestCase("doctor", "immunizations", "/manual/doctor/immunizations.html")]
    [TestCase("doctor", "cover-sheet", "/manual/doctor/cover-sheet.html")]
    [TestCase("doctor", "", "/manual/doctor/getting-started.html")]
    [TestCase("doctor", "some-unmapped-page", "/manual/doctor/index.html")]
    [TestCase("nurse", "vitals", "/manual/nurse/vital-signs.html")]
    [TestCase("nurse", "bcma", "/manual/nurse/bcma.html")]
    [TestCase("pharmacist", "drug-utilization-review", "/manual/pharmacist/drug-utilization-review.html")]
    [TestCase("pharmacist", "pharmacy-pos", "/manual/pharmacist/pos-claims.html")]
    [TestCase("admin", "security-keys", "/manual/admin/security-keys.html")]
    [TestCase("nurse", "pain-assessment", "/manual/nurse/pain-assessment.html")]
    [TestCase("nurse", "nursing-careplan", "/manual/nurse/care-plan.html")]
    [TestCase("pharmacist", "pharmacy", "/manual/pharmacist/pharmacy-hub.html")]
    [TestCase("pharmacist", "inpatientpharmacy", "/manual/pharmacist/inpatient-meds.html")]
    [TestCase("admin", "registration", "/manual/admin/registration.html")]
    [TestCase("admin", "service-connected", "/manual/admin/sc-conditions.html")]
    [TestCase("bogus", "xyz-unmapped", "/manual/doctor/index.html")] // unknown section → doctor + fallback
    public void UrlForRoute_DeepLinksTopicOrFallsBack(string section, string route, string expected)
        => Assert.That(ManualHelp.UrlForRoute(section, route), Is.EqualTo(expected));
}
