// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.BlazorWeb.Services;

namespace NewVistas.BlazorWeb_Tests;

[TestFixture]
public class SecurityKeyErrorTests
{
    [Test]
    public void Describe_PermissionDenial_NamesTheKey()
    {
        var ex = new UnauthorizedAccessException(
            "Access denied: user 89def requires any of [GMPL PROBLEM] to call IPatientWorkflowGrain.AddProblemAsync");

        string msg = SecurityKeyError.Describe(ex);

        Assert.That(msg, Does.Contain("GMPL PROBLEM"));
        Assert.That(msg, Does.Contain("permission"));
        Assert.That(msg, Does.Not.Contain("Access denied:")); // raw text not leaked
    }

    [Test]
    public void Describe_PermissionDenial_MultipleKeys_ListsThemAll()
    {
        var ex = new UnauthorizedAccessException(
            "Access denied: user x requires any of [ORES, ORELSE] to call IPatientWorkflowGrain.PlaceOrderAsync");

        string msg = SecurityKeyError.Describe(ex);

        Assert.That(msg, Does.Contain("ORES, ORELSE"));
    }

    [Test]
    public void Describe_PermissionDenial_NoKeyList_FallsBackToGenericNotice()
    {
        var ex = new UnauthorizedAccessException("Access denied.");

        string msg = SecurityKeyError.Describe(ex);

        Assert.That(msg, Does.Contain("permission"));
        Assert.That(msg, Does.Contain("administrator"));
    }

    [Test]
    public void Describe_NonPermissionError_KeepsOrdinaryErrorText()
    {
        var ex = new InvalidOperationException("Silo unreachable");

        string msg = SecurityKeyError.Describe(ex);

        Assert.That(msg, Is.EqualTo("Error: Silo unreachable"));
    }

    [Test]
    public void IsPermissionDenied_DistinguishesTypes()
    {
        Assert.That(SecurityKeyError.IsPermissionDenied(new UnauthorizedAccessException()), Is.True);
        Assert.That(SecurityKeyError.IsPermissionDenied(new Exception()), Is.False);
    }
}
