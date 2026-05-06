// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class AnesthesiaRecordGrainTests
{
    private TestCluster _cluster = default!;
    [OneTimeSetUp] public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IAnesthesiaRecordGrain GetGrain(string id) => _cluster.GrainFactory.GetGrain<IAnesthesiaRecordGrain>(id);
    private IAnesthesiaRecordIndexGrain GetIndex() => _cluster.GrainFactory.GetGrain<IAnesthesiaRecordIndexGrain>("ANES-IDX");

    private async Task<AnesthesiaRecordState> CreateTestAsync(string id, string patientId = "P-1")
    {
        return await GetGrain(id).CreateRecordAsync(
            patientId, "DOE,JOHN", "SURG-001", "Right Total Knee Arthroplasty",
            "GENERAL", "ANES-1", "Dr. Anesthesia", "ASA_II", "CLASS_II", "NPO since midnight");
    }

    [Test]
    public async Task Anesthesia_Creates()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        var result = await CreateTestAsync(id);

        Assert.That(result.RecordId, Is.EqualTo(id));
        Assert.That(result.ProcedureName, Is.EqualTo("Right Total Knee Arthroplasty"));
        Assert.That(result.AnesthesiaType, Is.EqualTo("GENERAL"));
        Assert.That(result.AsaClassification, Is.EqualTo("ASA_II"));
        Assert.That(result.AirwayClass, Is.EqualTo("CLASS_II"));
        Assert.That(result.Status, Is.EqualTo("DRAFT"));
    }

    [Test]
    public async Task Anesthesia_AddsAgent()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);

        await GetGrain(id).AddAgentAsync(new AnesthesiaAgent
        {
            AgentName = "Propofol", Category = "INDUCTION", Dose = "200", Unit = "mg",
            Route = "IV", AdministeredTime = DateTime.UtcNow
        });

        var state = await GetGrain(id).GetRecordAsync();
        Assert.That(state.Agents, Has.Count.EqualTo(1));
        Assert.That(state.Agents[0].AgentName, Is.EqualTo("Propofol"));
        Assert.That(state.Status, Is.EqualTo("IN_PROGRESS"));
    }

    [Test]
    public async Task Anesthesia_RecordsAirway()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);

        await GetGrain(id).RecordAirwayManagementAsync("ETT", "7.0", "Direct laryngoscopy, Grade I view", "Dr. Anesthesia");

        var state = await GetGrain(id).GetRecordAsync();
        Assert.That(state.AirwayDevice, Is.EqualTo("ETT"));
        Assert.That(state.AirwaySize, Is.EqualTo("7.0"));
        Assert.That(state.Events.Any(e => e.EventType == "INTUBATION"), Is.True);
    }

    [Test]
    public async Task Anesthesia_RecordsVitals()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);

        await GetGrain(id).RecordVitalsAsync(new AnesthesiaVitalEntry
        {
            Timestamp = DateTime.UtcNow, SystolicBp = 120, DiastolicBp = 70,
            HeartRate = 72, SpO2 = 99, EtCo2 = 35, Temperature = 36.5m, RespiratoryRate = 14
        });

        var state = await GetGrain(id).GetRecordAsync();
        Assert.That(state.VitalEntries, Has.Count.EqualTo(1));
        Assert.That(state.VitalEntries[0].SpO2, Is.EqualTo(99));
    }

    [Test]
    public async Task Anesthesia_RecordsInductionAndEmergence()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);
        DateTime induction = new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc);
        DateTime emergence = new DateTime(2026, 3, 20, 10, 30, 0, DateTimeKind.Utc);

        await GetGrain(id).RecordInductionAsync(induction, "Rapid sequence", "Dr. Anesthesia");
        await GetGrain(id).RecordEmergenceAsync(emergence, "Smooth emergence, following commands", "Dr. Anesthesia");

        var state = await GetGrain(id).GetRecordAsync();
        Assert.That(state.InductionTime, Is.EqualTo(induction));
        Assert.That(state.InductionMethod, Is.EqualTo("Rapid sequence"));
        Assert.That(state.EmergenceTime, Is.EqualTo(emergence));
    }

    [Test]
    public async Task Anesthesia_RecordsPacuHandoff()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);

        await GetGrain(id).RecordPacuHandoffAsync("Nurse Johnson", 9, "Alert, stable vitals");

        var state = await GetGrain(id).GetRecordAsync();
        Assert.That(state.PacuNurse, Is.EqualTo("Nurse Johnson"));
        Assert.That(state.AldretScore, Is.EqualTo(9));
        Assert.That(state.PacuHandoffNotes, Is.EqualTo("Alert, stable vitals"));
    }

    [Test]
    public async Task Anesthesia_Finalizes()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);

        await GetGrain(id).FinalizeRecordAsync("Dr. Anesthesia");

        var state = await GetGrain(id).GetRecordAsync();
        Assert.That(state.Status, Is.EqualTo("FINALIZED"));
    }

    [Test]
    public async Task Anesthesia_CannotModifyFinalized()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);
        await GetGrain(id).FinalizeRecordAsync("Dr.");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await GetGrain(id).AddAgentAsync(new AnesthesiaAgent
            { AgentName = "Late", Category = "OTHER", Dose = "1", Unit = "mg", Route = "IV", AdministeredTime = DateTime.UtcNow }));
    }

    [Test]
    public async Task Anesthesia_Addends()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        await CreateTestAsync(id);
        await GetGrain(id).FinalizeRecordAsync("Dr.");

        await GetGrain(id).AddendRecordAsync("Corrected total crystalloid to 2000mL", "Dr. Anesthesia");

        var state = await GetGrain(id).GetRecordAsync();
        Assert.That(state.Status, Is.EqualTo("ADDENDED"));
        Assert.That(state.AddendumNotes, Does.Contain("Corrected total crystalloid"));
    }

    [Test]
    public async Task AnesthesiaIndex_UpdatedOnCreate()
    {
        string id = $"ANES:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        await CreateTestAsync(id, patientId: patientId);

        var entries = await GetIndex().GetByPatientAsync(patientId);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].RecordId, Is.EqualTo(id));
        Assert.That(entries[0].AnesthesiaType, Is.EqualTo("GENERAL"));
    }
}
