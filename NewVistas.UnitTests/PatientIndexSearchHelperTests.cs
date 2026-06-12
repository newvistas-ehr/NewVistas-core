// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.UnitTests;

/// <summary>
/// Tests for the shared ORWPT LOOKUP search heuristic. This helper is the
/// single source of truth used by BOTH PatientIndexGrain (singleton) and the
/// PatientSearchGrain StatelessWorker readers, so its behavior is the contract
/// that keeps those two implementations from drifting. Pure function — no cluster.
/// </summary>
[TestFixture]
public class PatientIndexSearchHelperTests
{
    private static readonly List<PatientIndexEntry> Roster =
    [
        new PatientIndexEntry { PatientId = "P1", Name = "SMITH,JOHN A",  Sex = "M", SsnLast4 = "1234", Dfn = "1001" },
        new PatientIndexEntry { PatientId = "P2", Name = "SMITH,JANE B",  Sex = "F", SsnLast4 = "5678", Dfn = "1002" },
        new PatientIndexEntry { PatientId = "P3", Name = "SMITHE,ADAM",   Sex = "M", SsnLast4 = "1234", Dfn = "1003" },
        new PatientIndexEntry { PatientId = "P4", Name = "JONES,ROBERT",  Sex = "M", SsnLast4 = "9999", Dfn = "42"   },
    ];

    [Test]
    public void EmptyTerm_ReturnsEmpty()
        => Assert.That(PatientIndexSearchHelper.Search(Roster, "", 25), Is.Empty);

    [Test]
    public void WhitespaceTerm_ReturnsEmpty()
        => Assert.That(PatientIndexSearchHelper.Search(Roster, "   ", 25), Is.Empty);

    [Test]
    public void FourDigitTerm_MatchesSsnLast4Exact()
    {
        List<PatientIndexEntry> results = PatientIndexSearchHelper.Search(Roster, "1234", 25);

        // Two patients share SSN last-4 1234; the DFN "1234" branch must NOT fire.
        Assert.That(results.Select(e => e.PatientId), Is.EquivalentTo(new[] { "P1", "P3" }));
    }

    [Test]
    public void ShortNumericTerm_MatchesDfnExact()
    {
        List<PatientIndexEntry> results = PatientIndexSearchHelper.Search(Roster, "42", 25);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].PatientId, Is.EqualTo("P4"));
    }

    [Test]
    public void NamePrefix_IsCaseInsensitive()
    {
        List<PatientIndexEntry> results = PatientIndexSearchHelper.Search(Roster, "smith", 25);

        // "SMITH,JOHN", "SMITH,JANE", and "SMITHE,ADAM" all start with SMITH.
        Assert.That(results.Select(e => e.PatientId), Is.EquivalentTo(new[] { "P1", "P2", "P3" }));
    }

    [Test]
    public void NamePrefix_SupportsLastCommaFirst()
    {
        List<PatientIndexEntry> results = PatientIndexSearchHelper.Search(Roster, "SMITH,JA", 25);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].PatientId, Is.EqualTo("P2"));
    }

    [Test]
    public void Results_OrderedByNameAscending()
    {
        List<PatientIndexEntry> results = PatientIndexSearchHelper.Search(Roster, "SMITH", 25);

        Assert.That(results.Select(e => e.Name),
            Is.EqualTo(new[] { "SMITH,JANE B", "SMITH,JOHN A", "SMITHE,ADAM" }));
    }

    [Test]
    public void MaxResults_CapsResults()
    {
        List<PatientIndexEntry> results = PatientIndexSearchHelper.Search(Roster, "SMITH", 2);

        Assert.That(results, Has.Count.EqualTo(2));
        // The cap is applied after the name ordering, so the alphabetically
        // first two survive.
        Assert.That(results.Select(e => e.Name), Is.EqualTo(new[] { "SMITH,JANE B", "SMITH,JOHN A" }));
    }

    [Test]
    public void TermIsTrimmed()
    {
        List<PatientIndexEntry> results = PatientIndexSearchHelper.Search(Roster, "  JONES  ", 25);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].PatientId, Is.EqualTo("P4"));
    }

    [Test]
    public void NoMatch_ReturnsEmpty()
        => Assert.That(PatientIndexSearchHelper.Search(Roster, "NONEXISTENT", 25), Is.Empty);
}
