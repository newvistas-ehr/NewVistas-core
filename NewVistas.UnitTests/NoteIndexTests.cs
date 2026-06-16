// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for PatientNoteIndexGrain — the per-patient note index
/// that eliminates N+1 fan-out for filtered note queries.
/// </summary>
[TestFixture]
public class PatientNoteIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientNoteIndexGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IPatientNoteIndexGrain>($"PATIENT-{Guid.NewGuid()}");

    [Test]
    public async Task NoteIndex_AddEntry_AppearsInAllEntries()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
        {
            DocumentGrainKey = "TIU-001",
            ReferenceDate = now,
            DocumentType = "PROGRESS NOTE",
            Status = "UNSIGNED",
            Subject = "Annual Visit",
            AuthorName = "Dr. Adams"
        });

        List<NoteIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].DocumentGrainKey, Is.EqualTo("TIU-001"));
        Assert.That(entries[0].DocumentType, Is.EqualTo("PROGRESS NOTE"));
    }

    [Test]
    public async Task NoteIndex_MultipleEntries_SortedByDateDescending()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime oldest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime middle = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime newest = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-OLDEST", ReferenceDate = oldest, DocumentType = "PROGRESS NOTE", Status = "COMPLETED", Subject = "Old" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-NEWEST", ReferenceDate = newest, DocumentType = "DISCHARGE SUMMARY", Status = "UNSIGNED", Subject = "New" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-MIDDLE", ReferenceDate = middle, DocumentType = "CONSULT NOTE", Status = "COMPLETED", Subject = "Mid" });

        List<NoteIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries[0].DocumentGrainKey, Is.EqualTo("TIU-NEWEST"));
        Assert.That(entries[1].DocumentGrainKey, Is.EqualTo("TIU-MIDDLE"));
        Assert.That(entries[2].DocumentGrainKey, Is.EqualTo("TIU-OLDEST"));
    }

    [Test]
    public async Task NoteIndex_UpdateExistingEntry_ReplacesOldEntry()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-UPD", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "UNSIGNED", Subject = "Draft" });

        // Update same note — status changed to COMPLETED
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-UPD", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "COMPLETED", Subject = "Draft" });

        List<NoteIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Status, Is.EqualTo("COMPLETED"));
    }

    [Test]
    public async Task NoteIndex_RemoveEntry_RemovesFromIndex()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-KEEP", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-REMOVE", ReferenceDate = now.AddMinutes(-5), DocumentType = "PROGRESS NOTE", Status = "UNSIGNED" });

        await grain.RemoveNoteAsync("TIU-REMOVE");

        List<NoteIndexEntry> entries = await grain.GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].DocumentGrainKey, Is.EqualTo("TIU-KEEP"));
    }

    [Test]
    public async Task NoteIndex_GetEntries_ExcludesAddendaAndRetracted()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-NORMAL", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-ADDENDUM", ReferenceDate = now, DocumentType = "ADDENDUM", Status = "COMPLETED", IsAddendum = true });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-RETRACTED", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "RETRACTED" });

        List<NoteIndexEntry> topLevel = await grain.GetEntriesAsync(null, 50);
        Assert.That(topLevel, Has.Count.EqualTo(1));
        Assert.That(topLevel[0].DocumentGrainKey, Is.EqualTo("TIU-NORMAL"));
    }

    [Test]
    public async Task NoteIndex_GetEntries_FilterByDocumentType()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-PROG", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-DISCH", ReferenceDate = now, DocumentType = "DISCHARGE SUMMARY", Status = "COMPLETED" });

        List<NoteIndexEntry> progNotes = await grain.GetEntriesAsync("PROGRESS NOTE", 50);
        Assert.That(progNotes, Has.Count.EqualTo(1));
        Assert.That(progNotes[0].DocumentGrainKey, Is.EqualTo("TIU-PROG"));
    }

    [Test]
    public async Task NoteIndex_GetEntriesByStatus_FiltersCorrectly()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-UNSIGNED", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "UNSIGNED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-COMPLETED", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-UNCOSIGNED", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "UNCOSIGNED" });

        List<NoteIndexEntry> unsigned = await grain.GetEntriesByStatusAsync("UNSIGNED");
        Assert.That(unsigned, Has.Count.EqualTo(1));
        Assert.That(unsigned[0].DocumentGrainKey, Is.EqualTo("TIU-UNSIGNED"));
    }

    [Test]
    public async Task NoteIndex_DateRange_FiltersCorrectly()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime jan = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime feb = new DateTime(2026, 2, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime mar = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc);

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-JAN", ReferenceDate = jan, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-FEB", ReferenceDate = feb, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-MAR", ReferenceDate = mar, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });

        List<NoteIndexEntry> range = await grain.GetEntriesByDateRangeAsync(
            new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
        Assert.That(range, Has.Count.EqualTo(1));
        Assert.That(range[0].DocumentGrainKey, Is.EqualTo("TIU-FEB"));
    }

    [Test]
    public async Task NoteIndex_Count_ReturnsCorrectCount()
    {
        IPatientNoteIndexGrain grain = NewGrain();
        DateTime now = DateTime.UtcNow;

        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-C1", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "COMPLETED" });
        await grain.AddOrUpdateNoteAsync(new NoteIndexEntry
            { DocumentGrainKey = "TIU-C2", ReferenceDate = now, DocumentType = "PROGRESS NOTE", Status = "UNSIGNED" });

        int count = await grain.GetCountAsync();
        Assert.That(count, Is.EqualTo(2));
    }
}
