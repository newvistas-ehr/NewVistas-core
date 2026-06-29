// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Claims;

namespace NewVistas.BlazorWeb.Services;

/// <summary>
/// Resolves the in-app "Help" link to the right page of the user manual (served from
/// <c>wwwroot/manual</c>) using two signals: the signed-in user's <b>role</b> picks the manual
/// section (doctor / nurse / pharmacist / admin), and the <b>current page</b> deep-links the
/// matching topic, falling back to the section home when there's no specific topic.
/// </summary>
public static class ManualHelp
{
    /// <summary>Base path of the manual (static files under wwwroot/manual).</summary>
    public const string Root = "/manual";

    // JWT role claims may arrive as the full ClaimTypes.Role URI or the short "role"/"roles".
    private static readonly string[] RoleClaimTypes = { ClaimTypes.Role, "role", "roles" };

    /// <summary>All role values on a principal, across the claim-type spellings a JWT may use.</summary>
    public static IEnumerable<string> RolesFromPrincipal(ClaimsPrincipal user)
        => RoleClaimTypes.SelectMany(t => user.FindAll(t).Select(c => c.Value));

    /// <summary>
    /// The manual section for a user's roles. A user with several roles is mapped by priority:
    /// administrator → pharmacist → provider(doctor) → nurse. A nurse-practitioner (Provider +
    /// Nurse) lands on the doctor manual since they prescribe. Defaults to doctor.
    /// </summary>
    public static string SectionForRoles(IEnumerable<string> roles)
    {
        var set = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        if (set.Contains("Administrator") || set.Contains("RegistrationClerk")) return "admin";
        if (set.Contains("Pharmacist")) return "pharmacist";
        if (set.Contains("Provider") || set.Contains("Surgeon") || set.Contains("Radiologist") || set.Contains("MentalHealth") || set.Contains("Oncologist")) return "doctor";
        if (set.Contains("Nurse")) return "nurse";
        return "doctor";
    }

    // Per-section map of app-route first-segment → manual topic file. Unmapped routes fall back
    // to the section's index page.
    private static readonly Dictionary<string, Dictionary<string, string>> Topics = new()
    {
        ["doctor"] = new()
        {
            [""] = "getting-started.html",
            ["patient-lookup"] = "getting-started.html",
            ["cover-sheet"] = "cover-sheet.html",
            ["problems"] = "problem-list.html",
            ["orders"] = "orders.html",
            ["medications"] = "prescribing.html",
            ["epcs"] = "prescribing.html",
            ["notes"] = "notes.html",
            ["consults"] = "consults.html",
            ["labs"] = "labs.html",
            ["allergies"] = "allergies.html",
            ["immunizations"] = "immunizations.html",
            ["oncology"] = "oncology.html",
            ["radiation-therapy"] = "radiation-therapy.html",
            ["cancerregistry"] = "cancer-registry.html",
            ["home-care"] = "home-care.html",
        },
        ["nurse"] = new()
        {
            [""] = "getting-started.html",
            ["vitals"] = "vital-signs.html",
            ["pain-assessment"] = "pain-assessment.html",
            ["bcma"] = "bcma.html",
            ["nursing"] = "nursing-assessment.html",
            ["nursing-careplan"] = "care-plan.html",
            ["nursing-triage"] = "triage.html",
            ["nursing-tasks"] = "task-worklist.html",
            ["shift-handoff"] = "shift-handoff.html",
        },
        ["pharmacist"] = new()
        {
            [""] = "getting-started.html",
            ["pharmacy"] = "pharmacy-hub.html",
            ["outpatientpharmacy"] = "verify-fill.html",
            ["inpatientpharmacy"] = "inpatient-meds.html",
            ["drugaccountability"] = "drug-accountability.html",
            ["pharmacybenefits"] = "benefits-pa.html",
            ["drug-utilization-review"] = "drug-utilization-review.html",
            ["interaction-blocking"] = "interaction-screening.html",
            ["epcs"] = "epcs.html",
            ["pharmacy-pos"] = "pos-claims.html",
        },
        ["admin"] = new()
        {
            [""] = "getting-started.html",
            ["adt"] = "adt.html",
            ["registration"] = "registration.html",
            ["patient-merge"] = "patient-merge.html",
            ["means-test"] = "means-test.html",
            ["service-connected"] = "sc-conditions.html",
            ["prosthetics"] = "prosthetics.html",
            ["security-keys"] = "security-keys.html",
            ["site-parameters"] = "site-parameters.html",
        },
    };

    /// <summary>
    /// The manual URL for a section + the current app route (a base-relative path like
    /// "orders" or "medications?x=1"). Picks the matching topic or the section index.
    /// </summary>
    public static string UrlForRoute(string section, string? relativeRoute)
    {
        if (!Topics.TryGetValue(section, out Dictionary<string, string>? map))
        {
            section = "doctor";
            map = Topics["doctor"];
        }

        string seg = FirstSegment(relativeRoute);
        string file = map.TryGetValue(seg, out string? f) ? f : "index.html";
        return $"{Root}/{section}/{file}";
    }

    private static string FirstSegment(string? relativeRoute)
    {
        string s = (relativeRoute ?? string.Empty).Trim('/');
        int q = s.IndexOf('?');
        if (q >= 0) s = s[..q];
        int slash = s.IndexOf('/');
        if (slash >= 0) s = s[..slash];
        return s.ToLowerInvariant();
    }
}
