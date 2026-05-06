// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class AutoRefillGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IAutoRefillGrain GetGrain(string id) => _cluster.GrainFactory.GetGrain<IAutoRefillGrain>(id);
    private IAutoRefillIndexGrain GetIndex() => _cluster.GrainFactory.GetGrain<IAutoRefillIndexGrain>("RX-AUTOREFILL-IDX");

    private async Task<AutoRefillState> EnrollTestAsync(string id,
        string patientId = "PATIENT-1", int daysSupply = 30, int refillsRemaining = 3)
    {
        DateTime lastFill = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        return await GetGrain(id).EnrollAsync(
            patientId, "DOE,JOHN", "RX-001", "Metoprolol 25mg", "CV100",
            daysSupply, refillsRemaining, lastFill,
            "PHARM-1", "Main Pharmacy", "PROV-1", "Dr. Jones");
    }

    [Test]
    public async Task AutoRefill_Enrolls()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        var result = await EnrollTestAsync(id);

        Assert.That(result.EnrollmentId, Is.EqualTo(id));
        Assert.That(result.DrugName, Is.EqualTo("Metoprolol 25mg"));
        Assert.That(result.DaysSupply, Is.EqualTo(30));
        Assert.That(result.RefillsRemaining, Is.EqualTo(3));
        Assert.That(result.Status, Is.EqualTo("ACTIVE"));
        Assert.That(result.LeadTimeDays, Is.EqualTo(7));
        // NextRefillDate = 2026-03-01 + 30 - 7 = 2026-03-24
        Assert.That(result.NextRefillDate, Is.EqualTo(new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(result.RefillHistory, Has.Count.EqualTo(1));
        Assert.That(result.RefillHistory[0].EventType, Is.EqualTo("ENROLLED"));
    }

    [Test]
    public async Task AutoRefill_NoRefillsStatus()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        var result = await EnrollTestAsync(id, refillsRemaining: 0);

        Assert.That(result.Status, Is.EqualTo("NO_REFILLS"));
    }

    [Test]
    public async Task AutoRefill_RecordsFill()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        await EnrollTestAsync(id);
        var grain = GetGrain(id);

        DateTime newFill = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc);
        await grain.RecordFillAsync(newFill, 2);

        var state = await grain.GetEnrollmentAsync();
        Assert.That(state.LastFillDate, Is.EqualTo(newFill));
        Assert.That(state.RefillsRemaining, Is.EqualTo(2));
        // NextRefillDate = 2026-03-25 + 30 - 7 = 2026-04-17
        Assert.That(state.NextRefillDate, Is.EqualTo(new DateTime(2026, 4, 17, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(state.RefillHistory, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AutoRefill_GeneratesRefillRequest()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        await EnrollTestAsync(id);
        var grain = GetGrain(id);

        await grain.GenerateRefillRequestAsync("Auto-Refill System");

        var state = await grain.GetEnrollmentAsync();
        Assert.That(state.Status, Is.EqualTo("REFILL_PENDING"));
        Assert.That(state.TotalRefillsGenerated, Is.EqualTo(1));
    }

    [Test]
    public async Task AutoRefill_CannotRefillWithNoRefills()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        await EnrollTestAsync(id, refillsRemaining: 0);
        var grain = GetGrain(id);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await grain.GenerateRefillRequestAsync("System"));
    }

    [Test]
    public async Task AutoRefill_Suspends()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        await EnrollTestAsync(id);
        var grain = GetGrain(id);

        await grain.SuspendAsync("Patient traveling", "Pharmacist Smith");

        var state = await grain.GetEnrollmentAsync();
        Assert.That(state.Status, Is.EqualTo("SUSPENDED"));
        Assert.That(state.SuspendReason, Is.EqualTo("Patient traveling"));
    }

    [Test]
    public async Task AutoRefill_Resumes()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        await EnrollTestAsync(id);
        var grain = GetGrain(id);
        await grain.SuspendAsync("Temp", "Pharmacist");

        await grain.ResumeAsync("Pharmacist");

        var state = await grain.GetEnrollmentAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.SuspendReason, Is.Null);
    }

    [Test]
    public async Task AutoRefill_Disenrolls()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        await EnrollTestAsync(id);
        var grain = GetGrain(id);

        await grain.DisenrollAsync("Medication discontinued", "Dr. Jones");

        var state = await grain.GetEnrollmentAsync();
        Assert.That(state.Status, Is.EqualTo("DISENROLLED"));
    }

    [Test]
    public async Task AutoRefillIndex_UpdatedOnEnroll()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        await EnrollTestAsync(id, patientId: patientId);

        var entries = await GetIndex().GetByPatientAsync(patientId);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].EnrollmentId, Is.EqualTo(id));
        Assert.That(entries[0].Status, Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task AutoRefillIndex_DueForRefill()
    {
        string id = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        // LastFill 2026-03-01, 30-day, lead 7 → NextRefill 2026-03-24
        await EnrollTestAsync(id, patientId: patientId);

        // As of 2026-03-25, this should be due
        var due = await GetIndex().GetDueForRefillAsync(new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(due.Any(e => e.EnrollmentId == id), Is.True);

        // As of 2026-03-20, not yet due
        var notDue = await GetIndex().GetDueForRefillAsync(new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(notDue.Any(e => e.EnrollmentId == id), Is.False);
    }
}
