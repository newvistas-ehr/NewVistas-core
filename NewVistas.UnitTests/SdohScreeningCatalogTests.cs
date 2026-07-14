// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the curated SDOH screening crosswalk — the AHC-HRSN domain → billable Z-code →
/// referral-service-type mapping and the positive-only Evaluate. Pure logic — no cluster.
/// </summary>
[TestFixture]
public class SdohScreeningCatalogTests
{
    private static SdohScreeningResponse R(SdohDomain d, SdohResponse r) => new() { Domain = d, Response = r };

    [Test]
    public void Evaluate_OnlyPositiveDomains_ProduceFindings()
    {
        List<SdohFinding> findings = SdohScreeningCatalog.Evaluate(new[]
        {
            R(SdohDomain.FoodInsecurity, SdohResponse.Positive),
            R(SdohDomain.HousingInstability, SdohResponse.Positive),
            R(SdohDomain.Employment, SdohResponse.Negative),
            R(SdohDomain.TransportationInsecurity, SdohResponse.Unknown)
        });

        Assert.That(findings.Select(f => f.Domain),
            Is.EquivalentTo(new[] { SdohDomain.FoodInsecurity, SdohDomain.HousingInstability }));
    }

    [Test]
    public void Evaluate_NegativeAndUnknown_ProduceNothing()
    {
        List<SdohFinding> findings = SdohScreeningCatalog.Evaluate(new[]
        {
            R(SdohDomain.FoodInsecurity, SdohResponse.Negative),
            R(SdohDomain.Homelessness, SdohResponse.Unknown)
        });
        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void FoodInsecurity_MapsToBillableZCodeAndFoodReferral()
    {
        SdohFinding f = SdohScreeningCatalog.FindingFor(SdohDomain.FoodInsecurity);
        Assert.That(f.ZCode, Is.EqualTo("Z59.41"));
        Assert.That(f.ReferralServiceType, Is.EqualTo(SocialWorkReferralServiceType.Food));
    }

    [Test]
    public void EveryDomain_HasAMappedZCodeAndDisplay()
    {
        foreach (SdohDomain domain in SdohScreeningCatalog.AllDomains)
        {
            SdohFinding f = SdohScreeningCatalog.FindingFor(domain);
            Assert.That(f.ZCode, Is.Not.Empty, $"{domain} has no Z-code");
            Assert.That(f.ZCode, Does.StartWith("Z"), $"{domain} Z-code looks wrong: {f.ZCode}");
            Assert.That(f.Display, Is.Not.Empty);
        }
    }
}
