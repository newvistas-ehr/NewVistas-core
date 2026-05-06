// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class AnesthesiaWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private ISiteParametersGrain GetSiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private async Task<string> CreatePatientAsync(string name, string sex, DateTime dob, string ssn)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId).UpdateDemographicsAsync(name, sex, dob, ssn);
        await _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX").AddOrUpdateAsync(new PatientIndexEntry
        { PatientId = patientId, Name = name, DateOfBirth = dob, Sex = sex, SsnLast4 = ssn.Length >= 4 ? ssn[^4..] : string.Empty, IsActive = true });
        return patientId;
    }

    [Test, Order(1)]
    public async Task WorkflowAnesthesia_FailsWhenDisabled()
    {
        await GetSiteParams().DisableFeatureAsync("ANESTHESIA_TRACKING");
        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await GetWorkflow(patientId).CreateAnesthesiaRecordAsync(
                "SURG-1", "Appendectomy", "GENERAL", "ANES-1", "Dr. Anesthesia", "ASA_I", null, null));
    }

    [Test, Order(2)]
    public async Task WorkflowAnesthesia_CreatesRecordWhenEnabled()
    {
        await GetSiteParams().EnableFeatureAsync("ANESTHESIA_TRACKING");
        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");

        var record = await GetWorkflow(patientId).CreateAnesthesiaRecordAsync(
            "SURG-1", "Right Total Knee", "SPINAL", "ANES-1", "Dr. Anesthesia", "ASA_II", "CLASS_I", "NPO verified");

        Assert.That(record, Is.Not.Null);
        Assert.That(record.PatientId, Is.EqualTo(patientId));
        Assert.That(record.Status, Is.EqualTo("DRAFT"));
        Assert.That(record.AnesthesiaType, Is.EqualTo("SPINAL"));
        Assert.That(record.AsaClassification, Is.EqualTo("ASA_II"));
    }

    [Test, Order(3)]
    public async Task WorkflowAnesthesia_ListsRecords()
    {
        await GetSiteParams().EnableFeatureAsync("ANESTHESIA_TRACKING");
        string patientId = await CreatePatientAsync("JONES,MARY", "F", new DateTime(1980, 3, 10), "777-88-9999");
        var workflow = GetWorkflow(patientId);

        await workflow.CreateAnesthesiaRecordAsync("SURG-A", "Cholecystectomy", "GENERAL", "ANES-1", "Dr. A", "ASA_I", null, null);
        await workflow.CreateAnesthesiaRecordAsync("SURG-B", "Carpal Tunnel Release", "MAC", "ANES-2", "Dr. B", "ASA_II", null, null);

        var records = await workflow.GetAnesthesiaRecordsAsync();
        Assert.That(records, Has.Count.EqualTo(2));
    }

    [Test, Order(4)]
    public async Task WorkflowAnesthesia_AddAgentAndFinalize()
    {
        await GetSiteParams().EnableFeatureAsync("ANESTHESIA_TRACKING");
        string patientId = await CreatePatientAsync("BROWN,ROBERT", "M", new DateTime(1955, 12, 1), "222-33-4444");
        var workflow = GetWorkflow(patientId);

        var record = await workflow.CreateAnesthesiaRecordAsync(
            "SURG-C", "Hip Replacement", "GENERAL", "ANES-1", "Dr. Anesthesia", "ASA_III", "CLASS_II", null);

        await workflow.AddAnesthesiaAgentAsync(record.RecordId, new AnesthesiaAgent
        {
            AgentName = "Propofol", Category = "INDUCTION", Dose = "200", Unit = "mg",
            Route = "IV", AdministeredTime = DateTime.UtcNow
        });

        await workflow.AddAnesthesiaAgentAsync(record.RecordId, new AnesthesiaAgent
        {
            AgentName = "Sevoflurane", Category = "MAINTENANCE", Dose = "2", Unit = "%",
            Route = "INHALATION", AdministeredTime = DateTime.UtcNow
        });

        await workflow.FinalizeAnesthesiaRecordAsync(record.RecordId, "Dr. Anesthesia");

        var finalized = await workflow.GetAnesthesiaRecordAsync(record.RecordId);
        Assert.That(finalized.Status, Is.EqualTo("FINALIZED"));
        Assert.That(finalized.Agents, Has.Count.EqualTo(2));
        Assert.That(finalized.Agents[0].AgentName, Is.EqualTo("Propofol"));
        Assert.That(finalized.Agents[1].AgentName, Is.EqualTo("Sevoflurane"));
    }
}
