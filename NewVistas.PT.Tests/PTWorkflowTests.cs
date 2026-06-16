// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Tests;

[TestFixture]
public class PTWorkflowTests
{
    // ── Recording ───────────────────────────────────────────────────────────────

    [Test]
    public async Task RecordSession_Cervical_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime sessionDate = new(2026, 4, 3, 14, 0, 0, DateTimeKind.Utc);
        List<RomMeasurement> rom =
        [
            new() { Movement = Movement.Flexion, ActiveRom = 40m, PassiveRom = 45m },
            new() { Movement = Movement.Extension, ActiveRom = 38m, PassiveRom = 44m },
            new() { Movement = Movement.LateralFlexionLeft, ActiveRom = 35m, PassiveRom = 42m },
            new() { Movement = Movement.LateralFlexionRight, ActiveRom = 37m, PassiveRom = 43m },
            new() { Movement = Movement.RotationLeft, ActiveRom = 70m, PassiveRom = 78m },
            new() { Movement = Movement.RotationRight, ActiveRom = 72m, PassiveRom = 80m }
        ];
        List<StrengthMeasurement> strength =
        [
            new() { Movement = Movement.Flexion, Grade = 4m, GradeDisplay = "4" },
            new() { Movement = Movement.Extension, Grade = 4.33m, GradeDisplay = "4+" }
        ];

        string key = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Cervical, sessionDate, "THER-1", "Dr. Smith",
            "LOC-1", "PT Clinic", Laterality.Bilateral,
            rom, strength, "Initial evaluation");

        // Verify via direct grain read
        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(key);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.BodyGroup, Is.EqualTo(BodyGroup.Cervical));
        Assert.That(state.SessionDate, Is.EqualTo(sessionDate));
        Assert.That(state.TherapistId, Is.EqualTo("THER-1"));
        Assert.That(state.TherapistName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.LocationId, Is.EqualTo("LOC-1"));
        Assert.That(state.LocationName, Is.EqualTo("PT Clinic"));
        Assert.That(state.Side, Is.EqualTo(Laterality.Bilateral));
        Assert.That(state.RomMeasurements, Has.Count.EqualTo(6));
        Assert.That(state.StrengthMeasurements, Has.Count.EqualTo(2));
        Assert.That(state.Notes, Is.EqualTo("Initial evaluation"));
    }

    [Test]
    public async Task RecordSession_Shoulder_AllEightMovements()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        List<Movement> shoulderMovements = BodyGroupDefinitions.GetMovements(BodyGroup.Shoulder).ToList();
        List<RomMeasurement> rom = shoulderMovements
            .Select(m => new RomMeasurement { Movement = m, ActiveRom = 90m, PassiveRom = 100m })
            .ToList();

        string key = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder, DateTime.UtcNow, null, null, null, null,
            Laterality.Right, rom, new List<StrengthMeasurement>(), null);

        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(key);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.RomMeasurements, Has.Count.EqualTo(8));
        Assert.That(state.BodyGroup, Is.EqualTo(BodyGroup.Shoulder));
    }

    [Test]
    public async Task RecordSession_ReturnsSessionKey()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        string key = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Knee, DateTime.UtcNow, null, null, null, null,
            Laterality.Left, new List<RomMeasurement>(), new List<StrengthMeasurement>(), null);

        Assert.That(key, Does.StartWith($"PTSESSION:{patientId}:Knee:Left:"));
    }

    // ── History & "Last 2" ──────────────────────────────────────────────────────

    [Test]
    public async Task GetLatestSessions_TwoSessions_ReturnsBothDescending()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime date1 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime date2 = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Cervical, date1, null, null, null, null,
            Laterality.Bilateral, new(), new(), "Session 1");
        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Cervical, date2, null, null, null, null,
            Laterality.Bilateral, new(), new(), "Session 2");

        List<PTSessionState> sessions = await workflow.GetLatestSessionsAsync(BodyGroup.Cervical, 2);

        Assert.That(sessions, Has.Count.EqualTo(2));
        Assert.That(sessions[0].SessionDate, Is.EqualTo(date2));
        Assert.That(sessions[1].SessionDate, Is.EqualTo(date1));
    }

    [Test]
    public async Task GetLatestSessions_Default_ReturnsTwo()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime date1 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime date2 = new(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime date3 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Hip, date1, null, null, null, null, Laterality.Left, new(), new(), null);
        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Hip, date2, null, null, null, null, Laterality.Left, new(), new(), null);
        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Hip, date3, null, null, null, null, Laterality.Left, new(), new(), null);

        // Default count = 2
        List<PTSessionState> sessions = await workflow.GetLatestSessionsAsync(BodyGroup.Hip);

        Assert.That(sessions, Has.Count.EqualTo(2));
        Assert.That(sessions[0].SessionDate, Is.EqualTo(date3));
        Assert.That(sessions[1].SessionDate, Is.EqualTo(date2));
    }

    [Test]
    public async Task GetLatestSessions_NoData_ReturnsEmptyList()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        List<PTSessionState> sessions = await workflow.GetLatestSessionsAsync(BodyGroup.Ankle);

        Assert.That(sessions, Is.Empty);
    }

    [Test]
    public async Task GetSessionHistory_DateRangeFilter()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime jan = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        DateTime mar = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        DateTime may = new(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);

        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Elbow, jan, null, null, null, null, Laterality.Right, new(), new(), null);
        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Elbow, mar, null, null, null, null, Laterality.Right, new(), new(), null);
        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Elbow, may, null, null, null, null, Laterality.Right, new(), new(), null);

        // Only Feb-Apr range
        DateTime from = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc);
        List<PTSessionState> sessions = await workflow.GetSessionHistoryAsync(BodyGroup.Elbow, from, to);

        Assert.That(sessions, Has.Count.EqualTo(1));
        Assert.That(sessions[0].SessionDate, Is.EqualTo(mar));
    }

    // ── Body Group Discovery ────────────────────────────────────────────────────

    [Test]
    public async Task GetBodyGroupsWithData_Multiple_ReturnsAll()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Cervical, DateTime.UtcNow, null, null, null, null,
            Laterality.Bilateral, new(), new(), null);
        await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder, DateTime.UtcNow, null, null, null, null,
            Laterality.Right, new(), new(), null);

        List<BodyGroup> groups = await workflow.GetBodyGroupsWithDataAsync();

        Assert.That(groups, Does.Contain(BodyGroup.Cervical));
        Assert.That(groups, Does.Contain(BodyGroup.Shoulder));
    }

    [Test]
    public async Task GetBodyGroupsWithData_NoData_ReturnsEmpty()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        List<BodyGroup> groups = await workflow.GetBodyGroupsWithDataAsync();

        Assert.That(groups, Is.Empty);
    }

    // ── Measurement Details ─────────────────────────────────────────────────────

    [Test]
    public async Task RomMeasurement_ActiveAndPassive_BothPersisted()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        List<RomMeasurement> rom =
        [
            new() { Movement = Movement.Flexion, ActiveRom = 120m, PassiveRom = 135m }
        ];

        string key = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Knee, DateTime.UtcNow, null, null, null, null,
            Laterality.Left, rom, new(), null);

        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(key);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.RomMeasurements[0].ActiveRom, Is.EqualTo(120m));
        Assert.That(state.RomMeasurements[0].PassiveRom, Is.EqualTo(135m));
    }

    [Test]
    public async Task StrengthMeasurement_PlusMinusGrades_CorrectDecimal()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        List<StrengthMeasurement> strength =
        [
            new() { Movement = Movement.Flexion, Grade = 3.33m, GradeDisplay = "3+" },
            new() { Movement = Movement.Extension, Grade = 3.67m, GradeDisplay = "4-" },
            new() { Movement = Movement.Abduction, Grade = 5m, GradeDisplay = "5" }
        ];

        string key = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Hip, DateTime.UtcNow, null, null, null, null,
            Laterality.Right, new(), strength, null);

        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(key);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.StrengthMeasurements[0].Grade, Is.EqualTo(3.33m));
        Assert.That(state.StrengthMeasurements[0].GradeDisplay, Is.EqualTo("3+"));
        Assert.That(state.StrengthMeasurements[1].Grade, Is.EqualTo(3.67m));
        Assert.That(state.StrengthMeasurements[1].GradeDisplay, Is.EqualTo("4-"));
        Assert.That(state.StrengthMeasurements[2].Grade, Is.EqualTo(5m));
        Assert.That(state.StrengthMeasurements[2].GradeDisplay, Is.EqualTo("5"));
    }

    [Test]
    public async Task RecordSession_WithPainOnMotion_Persists()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        List<RomMeasurement> rom =
        [
            new()
            {
                Movement = Movement.Flexion,
                ActiveRom = 140m,
                PassiveRom = 160m,
                PainOnMotion = "sharp pain at end range"
            }
        ];

        string key = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder, DateTime.UtcNow, null, null, null, null,
            Laterality.Left, rom, new(), null);

        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(key);
        PTSessionState state = await sessionGrain.GetSessionAsync();

        Assert.That(state.RomMeasurements[0].PainOnMotion, Is.EqualTo("sharp pain at end range"));
    }

    // ── Edge Cases ──────────────────────────────────────────────────────────────

    [Test]
    public async Task RecordSession_LeftAndRight_SeparateSessions()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime sessionDate = new(2026, 4, 3, 14, 0, 0, DateTimeKind.Utc);

        string leftKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder, sessionDate, null, null, null, null,
            Laterality.Left, new(), new(), "Left shoulder");
        string rightKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder, sessionDate, null, null, null, null,
            Laterality.Right, new(), new(), "Right shoulder");

        Assert.That(leftKey, Is.Not.EqualTo(rightKey));
        Assert.That(leftKey, Does.Contain("Left"));
        Assert.That(rightKey, Does.Contain("Right"));

        // Both should appear in the index
        List<PTSessionState> sessions = await workflow.GetLatestSessionsAsync(BodyGroup.Shoulder, 10);
        Assert.That(sessions, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetStandardMovements_AllBodyGroups_NonEmpty()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        foreach (BodyGroup bg in Enum.GetValues<BodyGroup>())
        {
            List<Movement> movements = await workflow.GetStandardMovementsAsync(bg);
            Assert.That(movements, Is.Not.Empty, $"Body group {bg} has no movements defined");
        }
    }

    [Test]
    public void BodyGroupDefinitions_AllGroupsCovered()
    {
        IReadOnlyList<BodyGroup> covered = BodyGroupDefinitions.GetAllBodyGroups();

        foreach (BodyGroup bg in Enum.GetValues<BodyGroup>())
        {
            Assert.That(covered, Does.Contain(bg), $"Body group {bg} is missing from definitions");
        }
    }

    // ── Static Definitions ──────────────────────────────────────────────────────

    [Test]
    public void CervicalHas6Movements()
    {
        IReadOnlyList<Movement> movements = BodyGroupDefinitions.GetMovements(BodyGroup.Cervical);
        Assert.That(movements, Has.Count.EqualTo(6));
    }

    [Test]
    public void ShoulderHas8Movements()
    {
        IReadOnlyList<Movement> movements = BodyGroupDefinitions.GetMovements(BodyGroup.Shoulder);
        Assert.That(movements, Has.Count.EqualTo(8));
    }

    [Test]
    public void NoMovementDuplicatesInAnyGroup()
    {
        foreach (BodyGroup bg in Enum.GetValues<BodyGroup>())
        {
            IReadOnlyList<Movement> movements = BodyGroupDefinitions.GetMovements(bg);
            int distinctCount = movements.Distinct().Count();
            Assert.That(distinctCount, Is.EqualTo(movements.Count),
                $"Body group {bg} has duplicate movements");
        }
    }

    // ── Incremental Build ───────────────────────────────────────────────────────

    [Test]
    public async Task AddRomMeasurement_AfterRecord_Appends()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        List<RomMeasurement> rom =
        [
            new() { Movement = Movement.Flexion, ActiveRom = 40m, PassiveRom = 45m }
        ];

        string key = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Cervical, DateTime.UtcNow, null, null, null, null,
            Laterality.Bilateral, rom, new(), null);

        // Now add another measurement directly to the session grain
        IPTSessionGrain sessionGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(key);
        await sessionGrain.AddRomMeasurementAsync(
            new RomMeasurement { Movement = Movement.Extension, ActiveRom = 38m, PassiveRom = 44m });

        PTSessionState state = await sessionGrain.GetSessionAsync();
        Assert.That(state.RomMeasurements, Has.Count.EqualTo(2));
    }

    // ── Wizard Batch Recording ────────────────────────────────────────────────

    [Test]
    public async Task WizardBatch_MultipleBodyGroupsSameTimestamp_AllPersisted()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime sessionDate = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);

        // Simulate wizard saving Spine + Right Upper Extremity in one batch
        string cervicalKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Cervical, sessionDate, null, "Dr. Smith", null, "PT Clinic",
            Laterality.Bilateral,
            [new() { Movement = Movement.Flexion, ActiveRom = 35m, PassiveRom = 40m }],
            [new() { Movement = Movement.Flexion, Grade = 4m, GradeDisplay = "4" }],
            "Initial evaluation");

        string thoracicKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.ThoracicSpine, sessionDate, null, "Dr. Smith", null, "PT Clinic",
            Laterality.Bilateral,
            [new() { Movement = Movement.Flexion, ActiveRom = 25m }],
            new(), "Initial evaluation");

        string shoulderKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder, sessionDate, null, "Dr. Smith", null, "PT Clinic",
            Laterality.Right,
            [new() { Movement = Movement.Flexion, ActiveRom = 160m, PassiveRom = 175m }],
            new(), "Initial evaluation");

        string elbowKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Elbow, sessionDate, null, "Dr. Smith", null, "PT Clinic",
            Laterality.Right,
            [new() { Movement = Movement.Flexion, ActiveRom = 140m }],
            new(), "Initial evaluation");

        // All keys should be unique
        string[] keys = [cervicalKey, thoracicKey, shoulderKey, elbowKey];
        Assert.That(keys.Distinct().Count(), Is.EqualTo(4));

        // All body groups should appear in GetBodyGroupsWithDataAsync
        List<BodyGroup> groups = await workflow.GetBodyGroupsWithDataAsync();
        Assert.That(groups, Does.Contain(BodyGroup.Cervical));
        Assert.That(groups, Does.Contain(BodyGroup.ThoracicSpine));
        Assert.That(groups, Does.Contain(BodyGroup.Shoulder));
        Assert.That(groups, Does.Contain(BodyGroup.Elbow));

        // Each session should retain its own data
        IPTSessionGrain cervicalGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(cervicalKey);
        PTSessionState cervicalState = await cervicalGrain.GetSessionAsync();
        Assert.That(cervicalState.BodyGroup, Is.EqualTo(BodyGroup.Cervical));
        Assert.That(cervicalState.Side, Is.EqualTo(Laterality.Bilateral));
        Assert.That(cervicalState.RomMeasurements, Has.Count.EqualTo(1));
        Assert.That(cervicalState.StrengthMeasurements, Has.Count.EqualTo(1));

        IPTSessionGrain shoulderGrain = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTSessionGrain>(shoulderKey);
        PTSessionState shoulderState = await shoulderGrain.GetSessionAsync();
        Assert.That(shoulderState.BodyGroup, Is.EqualTo(BodyGroup.Shoulder));
        Assert.That(shoulderState.Side, Is.EqualTo(Laterality.Right));
    }

    [Test]
    public async Task WizardBatch_BothSidesUpperExtremity_EightSessions()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime sessionDate = new(2026, 4, 5, 15, 0, 0, DateTimeKind.Utc);
        BodyGroup[] upperGroups = [BodyGroup.Shoulder, BodyGroup.Elbow, BodyGroup.Wrist, BodyGroup.Hand];
        Laterality[] sides = [Laterality.Right, Laterality.Left];

        // Record 8 sessions: 4 body groups x 2 sides
        var sessionKeys = new List<string>();
        foreach (BodyGroup bg in upperGroups)
        {
            foreach (Laterality side in sides)
            {
                IReadOnlyList<Movement> movements = BodyGroupDefinitions.GetMovements(bg);
                List<RomMeasurement> rom = [new() { Movement = movements[0], ActiveRom = 90m }];

                string key = await workflow.RecordBodyGroupSessionAsync(
                    bg, sessionDate, null, "Dr. Smith", null, "PT Clinic",
                    side, rom, new(), null);
                sessionKeys.Add(key);
            }
        }

        Assert.That(sessionKeys.Distinct().Count(), Is.EqualTo(8));

        // Each body group should have 2 sessions (R + L)
        foreach (BodyGroup bg in upperGroups)
        {
            List<PTSessionState> sessions = await workflow.GetLatestSessionsAsync(bg, 10);
            Assert.That(sessions, Has.Count.EqualTo(2), $"{bg} should have 2 sessions (R + L)");
        }
    }

    [Test]
    public async Task WizardBatch_SharedTherapistAndNotes_PersistedOnEachSession()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPTWorkflowGrain workflow = SharedCluster.Instance.GrainFactory
            .GetGrain<IPTWorkflowGrain>(patientId);

        DateTime sessionDate = new(2026, 4, 5, 16, 0, 0, DateTimeKind.Utc);

        string key1 = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Hip, sessionDate, null, "Jane PT", null, "Rehab Center",
            Laterality.Right,
            [new() { Movement = Movement.Flexion, ActiveRom = 110m }],
            new(), "Global wizard notes");

        string key2 = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Knee, sessionDate, null, "Jane PT", null, "Rehab Center",
            Laterality.Right,
            [new() { Movement = Movement.Flexion, ActiveRom = 125m }],
            new(), "Knee-specific override");

        // Both share therapist and location
        IPTSessionGrain grain1 = SharedCluster.Instance.GrainFactory.GetGrain<IPTSessionGrain>(key1);
        IPTSessionGrain grain2 = SharedCluster.Instance.GrainFactory.GetGrain<IPTSessionGrain>(key2);
        PTSessionState state1 = await grain1.GetSessionAsync();
        PTSessionState state2 = await grain2.GetSessionAsync();

        Assert.That(state1.TherapistName, Is.EqualTo("Jane PT"));
        Assert.That(state2.TherapistName, Is.EqualTo("Jane PT"));
        Assert.That(state1.LocationName, Is.EqualTo("Rehab Center"));
        Assert.That(state2.LocationName, Is.EqualTo("Rehab Center"));

        // Notes differ (page-level override vs global)
        Assert.That(state1.Notes, Is.EqualTo("Global wizard notes"));
        Assert.That(state2.Notes, Is.EqualTo("Knee-specific override"));
    }

    // ── MMT Grade Parsing ───────────────────────────────────────────────────────

    [Test]
    public void ParseMmtGrade_ValidGrades()
    {
        var result3Plus = BodyGroupDefinitions.ParseMmtGrade("3+");
        Assert.That(result3Plus, Is.Not.Null);
        Assert.That(result3Plus!.Value.grade, Is.EqualTo(3.33m));
        Assert.That(result3Plus!.Value.display, Is.EqualTo("3+"));

        var result4Minus = BodyGroupDefinitions.ParseMmtGrade("4-");
        Assert.That(result4Minus, Is.Not.Null);
        Assert.That(result4Minus!.Value.grade, Is.EqualTo(3.67m));
        Assert.That(result4Minus!.Value.display, Is.EqualTo("4-"));

        var result5 = BodyGroupDefinitions.ParseMmtGrade("5");
        Assert.That(result5, Is.Not.Null);
        Assert.That(result5!.Value.grade, Is.EqualTo(5m));

        var result0 = BodyGroupDefinitions.ParseMmtGrade("0");
        Assert.That(result0, Is.Not.Null);
        Assert.That(result0!.Value.grade, Is.EqualTo(0m));
    }

    [Test]
    public void ParseMmtGrade_InvalidGrades_ReturnsNull()
    {
        Assert.That(BodyGroupDefinitions.ParseMmtGrade("6"), Is.Null);
        Assert.That(BodyGroupDefinitions.ParseMmtGrade("-1"), Is.Null);
        Assert.That(BodyGroupDefinitions.ParseMmtGrade("abc"), Is.Null);
        Assert.That(BodyGroupDefinitions.ParseMmtGrade("0+"), Is.Null);
    }
}
