// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for the coded symptom capture layer: <see cref="IPatientSymptomGrain"/>
/// append-only history + latest-per-code projection, and the <see cref="ISymptomCohortIndexGrain"/>
/// reverse shards (Present ⊆ Assessed). Symptom codes are a shared closed vocabulary, so
/// cross-cluster assertions use per-patient membership (not global counts); the pure shard-mechanics
/// test uses a unique fake code so it can assert exact counts.
/// </summary>
[TestFixture]
public class SymptomCaptureTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    // Catalog codes used below.
    private const string Anosmia = "44169009";
    private const string Fever = "386661006";
    private const string Cough = "49727002";

    private IPatientSymptomGrain Symptoms(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientSymptomGrain>($"SYMPTOMS:{patientId}");

    private ISymptomCohortIndexGrain Shard(string code) =>
        _cluster.GrainFactory.GetGrain<ISymptomCohortIndexGrain>($"SYMPTOM-COHORT:{code}");

    private static SymptomObservation Obs(string code, SymptomPresence presence) => new()
    {
        Code = code,
        Presence = presence,
        Source = SymptomSource.Survey,
        RecordedBy = "TESTER"
    };

    [Test]
    public async Task Record_Present_UpdatesLatestAndShards()
    {
        string patient = $"PSX-{Guid.NewGuid()}";

        int accepted = await Symptoms(patient).RecordObservationsAsync(new() { Obs(Anosmia, SymptomPresence.Present) });

        Assert.That(accepted, Is.EqualTo(1));
        SymptomObservation? latest = await Symptoms(patient).GetLatestForCodeAsync(Anosmia);
        Assert.That(latest, Is.Not.Null);
        Assert.That(latest!.Presence, Is.EqualTo(SymptomPresence.Present));
        Assert.That(latest.Display, Is.EqualTo("Loss of smell")); // denormalized from the catalog
        Assert.That(await Shard(Anosmia).ContainsPresentAsync(patient), Is.True);
        Assert.That(await Shard(Anosmia).GetAssessedAsync(), Does.Contain(patient));
    }

    [Test]
    public async Task Record_Absent_MarksAssessedNotPresent()
    {
        string patient = $"PSX-{Guid.NewGuid()}";

        await Symptoms(patient).RecordObservationsAsync(new() { Obs(Fever, SymptomPresence.Absent) });

        Assert.That(await Shard(Fever).ContainsPresentAsync(patient), Is.False);
        Assert.That(await Shard(Fever).GetAssessedAsync(), Does.Contain(patient));
    }

    [Test]
    public async Task Record_PresenceToggle_RemovesFromPresentKeepsAssessed()
    {
        string patient = $"PSX-{Guid.NewGuid()}";

        await Symptoms(patient).RecordObservationsAsync(new() { Obs(Cough, SymptomPresence.Present) });
        Assert.That(await Shard(Cough).ContainsPresentAsync(patient), Is.True);

        // A later assessment says the cough resolved — Present set drops it, Assessed keeps it.
        await Symptoms(patient).RecordObservationsAsync(new() { Obs(Cough, SymptomPresence.Absent) });

        Assert.That(await Shard(Cough).ContainsPresentAsync(patient), Is.False);
        Assert.That(await Shard(Cough).GetAssessedAsync(), Does.Contain(patient));

        SymptomObservation? latest = await Symptoms(patient).GetLatestForCodeAsync(Cough);
        Assert.That(latest!.Presence, Is.EqualTo(SymptomPresence.Absent));

        // History is append-only — both answers are retained (onset/progression signal).
        PatientSymptomState state = await Symptoms(patient).GetAsync();
        Assert.That(state.History.Count(h => h.Code == Cough), Is.EqualTo(2));
    }

    [Test]
    public async Task Record_UnknownVocabularyCode_IsDropped()
    {
        string patient = $"PSX-{Guid.NewGuid()}";

        int accepted = await Symptoms(patient).RecordObservationsAsync(new() { Obs("NOT-A-CODE", SymptomPresence.Present) });

        Assert.That(accepted, Is.EqualTo(0));
        Assert.That(await Symptoms(patient).GetLatestAsync(), Is.Empty);
    }

    [Test]
    public async Task Shard_PresentIsSubsetOfAssessed_ExactCounts()
    {
        // A unique fake code so the shard is isolated and counts are exact.
        string code = $"FAKE-{Guid.NewGuid():N}";
        string p1 = $"PSX-{Guid.NewGuid()}";
        string p2 = $"PSX-{Guid.NewGuid()}";
        string p3 = $"PSX-{Guid.NewGuid()}";

        await Shard(code).RecordPresenceAsync(p1, true);   // present + assessed
        await Shard(code).MarkAssessedAsync(p2);           // assessed only
        await Shard(code).RecordPresenceAsync(p3, false);  // assessed only (asked, denied)

        Assert.That(await Shard(code).GetPresentCountAsync(), Is.EqualTo(1));
        Assert.That(await Shard(code).GetAssessedCountAsync(), Is.EqualTo(3));
    }
}
