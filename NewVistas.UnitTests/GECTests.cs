// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

file class GECAssessmentGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("gecAssessmentStore");
    }
}

file class GECAssessmentIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("gecAssessmentIndexStore");
    }
}

file class CLCAdmissionGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("clcAdmissionStore");
    }
}

file class CLCAdmissionIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("clcAdmissionIndexStore");
    }
}

file class GECIntegrationSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("gecAssessmentStore");
        siloBuilder.AddMemoryGrainStorage("gecAssessmentIndexStore");
        siloBuilder.AddMemoryGrainStorage("clcAdmissionStore");
        siloBuilder.AddMemoryGrainStorage("clcAdmissionIndexStore");
    }
}

[TestFixture]
public class GECAssessmentGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task GECAssessmentGrain_CanCreateAssessment()
    {
        string id = $"GEC-ASSESS:{Guid.NewGuid()}";
        IGECAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(id);

        await grain.CreateAssessmentAsync("PAT-001", "Smith, John",
            GECAssessmentType.Initial, DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.SkilledNursing, "Nurse Jones", "RN");

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.AssessmentType, Is.EqualTo(GECAssessmentType.Initial));
        Assert.That(state.Status, Is.EqualTo(GECAssessmentStatus.Draft));
        Assert.That(state.LevelOfCare, Is.EqualTo(GECLevelOfCare.SkilledNursing));
        Assert.That(state.RUGCategory, Is.EqualTo(GECRUGCategory.NotAssigned));
    }

    [Test]
    public async Task GECAssessmentGrain_CanRecordADLScores()
    {
        string id = $"GEC-ASSESS:{Guid.NewGuid()}";
        IGECAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(id);
        await grain.CreateAssessmentAsync("PAT-002", "Jones, Mary",
            GECAssessmentType.Quarterly, DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-90), DateTime.UtcNow,
            GECLevelOfCare.LongTermCare, "OT Smith", "OTR");

        // All moderate assistance (2 each)
        await grain.RecordADLScoresAsync(2, 2, 2, 2, 2, 2, 2);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.ADLBedMobility, Is.EqualTo(2));
        Assert.That(state.ADLEating, Is.EqualTo(2));
        Assert.That(state.ADLTotalScore, Is.EqualTo(14));
    }

    [Test]
    public async Task GECAssessmentGrain_ADLTotalScore_Computed()
    {
        string id = $"GEC-ASSESS:{Guid.NewGuid()}";
        IGECAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(id);
        await grain.CreateAssessmentAsync("PAT-003", "Brown, Al",
            GECAssessmentType.Annual, DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-365), DateTime.UtcNow,
            GECLevelOfCare.DementiaCare, "PT Davis", "PT");

        // Total dependence on all = score 28
        await grain.RecordADLScoresAsync(4, 4, 4, 4, 4, 4, 4);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.ADLTotalScore, Is.EqualTo(28));
    }

    [Test]
    public async Task GECAssessmentGrain_CanRecordCognitiveMood()
    {
        string id = $"GEC-ASSESS:{Guid.NewGuid()}";
        IGECAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(id);
        await grain.CreateAssessmentAsync("PAT-004", "Davis, Kay",
            GECAssessmentType.Initial, DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.DementiaCare, "Psych Green", "PhD");

        await grain.RecordCognitiveMoodAsync(bimsScore: 5, phq9Score: 12);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.BIMSScore, Is.EqualTo(5));
        Assert.That(state.PHQ9Score, Is.EqualTo(12));
    }

    [Test]
    public async Task GECAssessmentGrain_CanRecordClinicalIndicators()
    {
        string id = $"GEC-ASSESS:{Guid.NewGuid()}";
        IGECAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(id);
        await grain.CreateAssessmentAsync("PAT-005", "Miller, Ed",
            GECAssessmentType.SignificantChange, DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-14), DateTime.UtcNow,
            GECLevelOfCare.SkilledNursing, "Nurse Blake", "RN");

        await grain.RecordClinicalIndicatorsAsync(
            painPresent: true, painFrequency: "Daily",
            pressureUlcerCount: 1, fallsLast30Days: 2,
            nutritionConcern: true, behaviorSymptoms: false);

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.PainPresent, Is.True);
        Assert.That(state.PressureUlcerCount, Is.EqualTo(1));
        Assert.That(state.FallsLast30Days, Is.EqualTo(2));
        Assert.That(state.NutritionConcern, Is.True);
    }

    [Test]
    public async Task GECAssessmentGrain_CanSubmitAssessment()
    {
        string id = $"GEC-ASSESS:{Guid.NewGuid()}";
        IGECAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(id);
        await grain.CreateAssessmentAsync("PAT-006", "Wilson, Rose",
            GECAssessmentType.Discharge, DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.SubAcuteRehabilitation, "Case Mgr Fox", "SW");
        await grain.SetRUGCategoryAsync(GECRUGCategory.Rehabilitation);

        await grain.SubmitAssessmentAsync("Supervisor Adams");

        GECAssessmentState state = await grain.GetAssessmentAsync();
        Assert.That(state.Status, Is.EqualTo(GECAssessmentStatus.Submitted));
        Assert.That(state.SubmittedDate, Is.Not.Null);
        Assert.That(state.RUGCategory, Is.EqualTo(GECRUGCategory.Rehabilitation));
    }
}

[TestFixture]
public class GECAssessmentIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task GECAssessmentIndexGrain_CanUpsertAndRetrieve()
    {
        IGECAssessmentIndexGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentIndexGrain>("GEC-ASSESS-IDX:PAT-IDX-T1");

        GECAssessmentIndexEntry entry = new()
        {
            AssessmentId = "GEC-ASSESS:A1",
            PatientId = "PAT-IDX-T1",
            PatientName = "Test Patient",
            AssessmentType = GECAssessmentType.Initial,
            AssessmentDate = DateTime.UtcNow,
            Status = GECAssessmentStatus.Completed,
            RUGCategory = GECRUGCategory.Rehabilitation,
            ADLTotalScore = 10,
            LevelOfCare = GECLevelOfCare.SkilledNursing
        };
        await grain.UpsertAssessmentAsync(entry);

        List<GECAssessmentIndexEntry> all = await grain.GetAllAssessmentsAsync();
        Assert.That(all.Any(a => a.AssessmentId == "GEC-ASSESS:A1"), Is.True);
    }

    [Test]
    public async Task GECAssessmentIndexGrain_FilterByType()
    {
        IGECAssessmentIndexGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentIndexGrain>("GEC-ASSESS-IDX:PAT-IDX-T2");

        await grain.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "GEC-ASSESS:B1", PatientId = "PAT-IDX-T2", PatientName = "A",
            AssessmentType = GECAssessmentType.Annual,
            AssessmentDate = DateTime.UtcNow, Status = GECAssessmentStatus.Submitted
        });
        await grain.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "GEC-ASSESS:B2", PatientId = "PAT-IDX-T2", PatientName = "A",
            AssessmentType = GECAssessmentType.Quarterly,
            AssessmentDate = DateTime.UtcNow.AddDays(-90), Status = GECAssessmentStatus.Submitted
        });

        List<GECAssessmentIndexEntry> annuals = await grain.GetAssessmentsByTypeAsync(GECAssessmentType.Annual);
        Assert.That(annuals.Any(a => a.AssessmentId == "GEC-ASSESS:B1"), Is.True);
        Assert.That(annuals.Any(a => a.AssessmentId == "GEC-ASSESS:B2"), Is.False);
    }

    [Test]
    public async Task GECAssessmentIndexGrain_GetLatestAssessment()
    {
        IGECAssessmentIndexGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentIndexGrain>("GEC-ASSESS-IDX:PAT-IDX-T3");

        DateTime older = DateTime.UtcNow.AddDays(-90);
        DateTime newer = DateTime.UtcNow;
        await grain.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "GEC-ASSESS:C-OLD", PatientId = "PAT-IDX-T3", PatientName = "B",
            AssessmentType = GECAssessmentType.Quarterly, AssessmentDate = older, Status = GECAssessmentStatus.Submitted
        });
        await grain.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "GEC-ASSESS:C-NEW", PatientId = "PAT-IDX-T3", PatientName = "B",
            AssessmentType = GECAssessmentType.Annual, AssessmentDate = newer, Status = GECAssessmentStatus.Submitted
        });

        GECAssessmentIndexEntry? latest = await grain.GetLatestAssessmentAsync();
        Assert.That(latest, Is.Not.Null);
        Assert.That(latest!.AssessmentId, Is.EqualTo("GEC-ASSESS:C-NEW"));
    }

    [Test]
    public async Task GECAssessmentIndexGrain_UpsertUpdatesExisting()
    {
        IGECAssessmentIndexGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentIndexGrain>("GEC-ASSESS-IDX:PAT-IDX-T4");

        await grain.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "GEC-ASSESS:D1", PatientId = "PAT-IDX-T4", PatientName = "C",
            AssessmentType = GECAssessmentType.Initial, AssessmentDate = DateTime.UtcNow,
            Status = GECAssessmentStatus.Draft, RUGCategory = GECRUGCategory.NotAssigned
        });
        await grain.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "GEC-ASSESS:D1", PatientId = "PAT-IDX-T4", PatientName = "C",
            AssessmentType = GECAssessmentType.Initial, AssessmentDate = DateTime.UtcNow,
            Status = GECAssessmentStatus.Submitted, RUGCategory = GECRUGCategory.Rehabilitation
        });

        List<GECAssessmentIndexEntry> all = await grain.GetAllAssessmentsAsync();
        List<GECAssessmentIndexEntry> mine = all.Where(a => a.AssessmentId == "GEC-ASSESS:D1").ToList();
        Assert.That(mine, Has.Count.EqualTo(1));
        Assert.That(mine[0].Status, Is.EqualTo(GECAssessmentStatus.Submitted));
    }
}

[TestFixture]
public class CLCAdmissionGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task CLCAdmissionGrain_CanAdmitPatient()
    {
        string id = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(id);

        await grain.AdmitPatientAsync("PAT-CLC-001", "Adams, Frank", new DateTime(1945, 3, 15),
            DateTime.UtcNow, CLCAdmitSource.AcuteHospital,
            GECLevelOfCare.SkilledNursing,
            "CLC Ward 3B", "Bed 12",
            "Dr. Thomas", "CVA with residual deficits",
            "City Hospital", DateTime.UtcNow.AddDays(30), "Post-stroke rehab");

        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-CLC-001"));
        Assert.That(state.Status, Is.EqualTo(CLCAdmissionStatus.Active));
        Assert.That(state.LevelOfCare, Is.EqualTo(GECLevelOfCare.SkilledNursing));
        Assert.That(state.Ward, Is.EqualTo("CLC Ward 3B"));
        Assert.That(state.BedRoom, Is.EqualTo("Bed 12"));
        Assert.That(state.AdmitSource, Is.EqualTo(CLCAdmitSource.AcuteHospital));
    }

    [Test]
    public async Task CLCAdmissionGrain_CanUpdateLevelOfCare()
    {
        string id = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(id);
        await grain.AdmitPatientAsync("PAT-CLC-002", "Baker, Sue", null,
            DateTime.UtcNow, CLCAdmitSource.Community,
            GECLevelOfCare.SubAcuteRehabilitation,
            "CLC Ward 2A", "Bed 5", "Dr. Patel",
            "Hip fracture s/p ORIF", string.Empty, null, "");

        await grain.UpdateLevelOfCareAsync(GECLevelOfCare.LongTermCare);

        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.LevelOfCare, Is.EqualTo(GECLevelOfCare.LongTermCare));
    }

    [Test]
    public async Task CLCAdmissionGrain_CanMarkOnLeaveAndReturn()
    {
        string id = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(id);
        await grain.AdmitPatientAsync("PAT-CLC-003", "Carter, Jim", null,
            DateTime.UtcNow, CLCAdmitSource.Community,
            GECLevelOfCare.LongTermCare,
            "CLC Ward 1", "Bed 8", "Dr. Evans",
            "COPD, CHF", string.Empty, null, "");

        await grain.MarkOnLeaveAsync();
        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.Status, Is.EqualTo(CLCAdmissionStatus.OnLeave));

        await grain.ReturnFromLeaveAsync();
        state = await grain.GetAdmissionAsync();
        Assert.That(state.Status, Is.EqualTo(CLCAdmissionStatus.Active));
    }

    [Test]
    public async Task CLCAdmissionGrain_CanDischargePatient()
    {
        string id = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(id);
        await grain.AdmitPatientAsync("PAT-CLC-004", "Davis, Ann", null,
            DateTime.UtcNow, CLCAdmitSource.AcuteHospital,
            GECLevelOfCare.SubAcuteRehabilitation,
            "CLC Ward 2B", "Bed 3", "Dr. Kim",
            "TKR", string.Empty, DateTime.UtcNow.AddDays(21), "");

        await grain.DischargePatientAsync(CLCDischargeDestination.Home, "Patient met all rehab goals");

        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.Status, Is.EqualTo(CLCAdmissionStatus.Discharged));
        Assert.That(state.DischargeDestination, Is.EqualTo(CLCDischargeDestination.Home));
        Assert.That(state.ActualDischargeDate, Is.Not.Null);
        Assert.That(state.DischargeNotes, Does.Contain("rehab goals"));
    }

    [Test]
    public async Task CLCAdmissionGrain_CanUpdateBedAssignment()
    {
        string id = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(id);
        await grain.AdmitPatientAsync("PAT-CLC-005", "Evans, Mike", null,
            DateTime.UtcNow, CLCAdmitSource.Community,
            GECLevelOfCare.DementiaCare,
            "CLC Ward 4", "Bed 1", "Dr. Long",
            "Alzheimers Disease", string.Empty, null, "");

        await grain.UpdateBedAssignmentAsync("CLC Ward 4 Dementia Unit", "Room 102A");

        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.Ward, Is.EqualTo("CLC Ward 4 Dementia Unit"));
        Assert.That(state.BedRoom, Is.EqualTo("Room 102A"));
    }

    [Test]
    public async Task CLCAdmissionGrain_CanMarkDeceased()
    {
        string id = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(id);
        await grain.AdmitPatientAsync("PAT-CLC-006", "Ford, Bea", null,
            DateTime.UtcNow, CLCAdmitSource.AcuteHospital,
            GECLevelOfCare.Hospice,
            "Hospice Unit", "Room 201", "Dr. Marsh",
            "End-stage lung cancer", string.Empty, null, "Comfort care");

        await grain.MarkDeceasedAsync("Patient passed peacefully with family present");

        CLCAdmissionState state = await grain.GetAdmissionAsync();
        Assert.That(state.Status, Is.EqualTo(CLCAdmissionStatus.Deceased));
        Assert.That(state.DischargeDestination, Is.EqualTo(CLCDischargeDestination.Deceased));
    }
}

[TestFixture]
public class CLCAdmissionIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task CLCAdmissionIndexGrain_CanUpsertAndRetrieve()
    {
        ICLCAdmissionIndexGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX-T1");

        await grain.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "CLC-1", PatientId = "P1", PatientName = "Alpha",
            AdmitDate = DateTime.UtcNow, LevelOfCare = GECLevelOfCare.SkilledNursing,
            Status = CLCAdmissionStatus.Active, Ward = "Ward A", BedRoom = "Bed 1"
        });

        List<CLCAdmissionIndexEntry> all = await grain.GetAllAdmissionsAsync();
        Assert.That(all.Any(a => a.AdmissionId == "CLC-1"), Is.True);
    }

    [Test]
    public async Task CLCAdmissionIndexGrain_FilterActiveCensus()
    {
        ICLCAdmissionIndexGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX-T2");

        await grain.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "CLC-A1", PatientId = "P1", PatientName = "Active",
            AdmitDate = DateTime.UtcNow, Status = CLCAdmissionStatus.Active, Ward = "W1"
        });
        await grain.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "CLC-A2", PatientId = "P2", PatientName = "Discharged",
            AdmitDate = DateTime.UtcNow.AddDays(-30), Status = CLCAdmissionStatus.Discharged, Ward = "W1"
        });

        List<CLCAdmissionIndexEntry> census = await grain.GetActiveCensusAsync();
        Assert.That(census.Any(a => a.AdmissionId == "CLC-A1"), Is.True);
        Assert.That(census.Any(a => a.AdmissionId == "CLC-A2"), Is.False);
    }

    [Test]
    public async Task CLCAdmissionIndexGrain_FilterByLevelOfCare()
    {
        ICLCAdmissionIndexGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX-T3");

        await grain.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "CLC-L1", PatientId = "P1", PatientName = "HospicePat",
            AdmitDate = DateTime.UtcNow, Status = CLCAdmissionStatus.Active,
            LevelOfCare = GECLevelOfCare.Hospice, Ward = "Hospice"
        });
        await grain.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "CLC-L2", PatientId = "P2", PatientName = "SkilledPat",
            AdmitDate = DateTime.UtcNow, Status = CLCAdmissionStatus.Active,
            LevelOfCare = GECLevelOfCare.SkilledNursing, Ward = "Skilled"
        });

        List<CLCAdmissionIndexEntry> hospice = await grain.GetAdmissionsByLevelOfCareAsync(GECLevelOfCare.Hospice);
        Assert.That(hospice.Any(a => a.AdmissionId == "CLC-L1"), Is.True);
        Assert.That(hospice.Any(a => a.AdmissionId == "CLC-L2"), Is.False);
    }

    [Test]
    public async Task CLCAdmissionIndexGrain_FilterAnticipatedDischarges()
    {
        ICLCAdmissionIndexGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX-T4");

        await grain.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "CLC-D1", PatientId = "P1", PatientName = "Soon",
            AdmitDate = DateTime.UtcNow.AddDays(-14), Status = CLCAdmissionStatus.Active,
            AnticipatedDischargeDate = DateTime.UtcNow.AddDays(3)
        });
        await grain.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "CLC-D2", PatientId = "P2", PatientName = "Later",
            AdmitDate = DateTime.UtcNow.AddDays(-7), Status = CLCAdmissionStatus.Active,
            AnticipatedDischargeDate = DateTime.UtcNow.AddDays(30)
        });

        List<CLCAdmissionIndexEntry> upcoming = await grain.GetAnticipatedDischargesAsync(7);
        Assert.That(upcoming.Any(a => a.AdmissionId == "CLC-D1"), Is.True);
        Assert.That(upcoming.Any(a => a.AdmissionId == "CLC-D2"), Is.False);
    }
}

[TestFixture]
public class GECIntegrationTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task GEC_FullAssessmentWorkflow()
    {
        string patientId = $"PAT-GEC-{Guid.NewGuid()}";
        string assessmentId = $"GEC-ASSESS:{Guid.NewGuid()}";
        IGECAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IGECAssessmentGrain>(assessmentId);
        IGECAssessmentIndexGrain index = _cluster.GrainFactory.GetGrain<IGECAssessmentIndexGrain>($"GEC-ASSESS-IDX:{patientId}");

        await grain.CreateAssessmentAsync(patientId, "Reed, Pat",
            GECAssessmentType.Initial, DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            GECLevelOfCare.SkilledNursing, "RN Wright", "RN");
        await grain.RecordADLScoresAsync(3, 3, 2, 2, 1, 2, 2);
        await grain.RecordCognitiveMoodAsync(bimsScore: 9, phq9Score: 8);
        await grain.RecordClinicalIndicatorsAsync(true, "Daily", 0, 1, false, false);
        await grain.SetRUGCategoryAsync(GECRUGCategory.ClinicallyCComplex);
        await grain.SubmitAssessmentAsync("Supervisor Lee");

        GECAssessmentState state = await grain.GetAssessmentAsync();
        await index.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = state.AssessmentId,
            PatientId = state.PatientId,
            PatientName = state.PatientName,
            AssessmentType = state.AssessmentType,
            AssessmentDate = state.AssessmentDate,
            Status = state.Status,
            RUGCategory = state.RUGCategory,
            ADLTotalScore = state.ADLTotalScore,
            LevelOfCare = state.LevelOfCare
        });

        Assert.That(state.Status, Is.EqualTo(GECAssessmentStatus.Submitted));
        Assert.That(state.ADLTotalScore, Is.EqualTo(15));
        Assert.That(state.RUGCategory, Is.EqualTo(GECRUGCategory.ClinicallyCComplex));

        List<GECAssessmentIndexEntry> assessments = await index.GetAllAssessmentsAsync();
        Assert.That(assessments.Any(a => a.AssessmentId == state.AssessmentId), Is.True);
    }

    [Test]
    public async Task GEC_CLCAdmitAndDischarge_UpdatesCensus()
    {
        string admissionId = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain admission = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(admissionId);
        ICLCAdmissionIndexGrain index = _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX-INT-1");

        await admission.AdmitPatientAsync(
            $"PAT-CLC-INT-{Guid.NewGuid()}", "Stone, Lee", null,
            DateTime.UtcNow, CLCAdmitSource.AcuteHospital,
            GECLevelOfCare.SkilledNursing,
            "Ward 2B", "Bed 6", "Dr. Crane",
            "Pneumonia with deconditioning", string.Empty, DateTime.UtcNow.AddDays(21), "");
        CLCAdmissionState state = await admission.GetAdmissionAsync();
        await index.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = state.AdmissionId, PatientId = state.PatientId, PatientName = state.PatientName,
            AdmitDate = state.AdmitDate, LevelOfCare = state.LevelOfCare, Status = state.Status,
            Ward = state.Ward, BedRoom = state.BedRoom
        });

        List<CLCAdmissionIndexEntry> census = await index.GetActiveCensusAsync();
        Assert.That(census.Any(a => a.AdmissionId == state.AdmissionId), Is.True);

        await admission.DischargePatientAsync(CLCDischargeDestination.Home, "Returned home with home health");
        state = await admission.GetAdmissionAsync();
        await index.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = state.AdmissionId, PatientId = state.PatientId, PatientName = state.PatientName,
            AdmitDate = state.AdmitDate, LevelOfCare = state.LevelOfCare, Status = state.Status,
            Ward = state.Ward, BedRoom = state.BedRoom
        });

        census = await index.GetActiveCensusAsync();
        Assert.That(census.Any(a => a.AdmissionId == state.AdmissionId), Is.False);
    }

    [Test]
    public async Task GEC_MultipleAssessments_LatestReturned()
    {
        string patientId = $"PAT-MULTI-{Guid.NewGuid()}";
        IGECAssessmentIndexGrain index = _cluster.GrainFactory.GetGrain<IGECAssessmentIndexGrain>($"GEC-ASSESS-IDX:{patientId}");

        // Initial assessment 6 months ago
        await index.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "OLD-ASSESS", PatientId = patientId, PatientName = "Cole, Ben",
            AssessmentType = GECAssessmentType.Initial,
            AssessmentDate = DateTime.UtcNow.AddMonths(-6),
            Status = GECAssessmentStatus.Submitted
        });
        // Quarterly assessment today
        await index.UpsertAssessmentAsync(new GECAssessmentIndexEntry
        {
            AssessmentId = "NEW-ASSESS", PatientId = patientId, PatientName = "Cole, Ben",
            AssessmentType = GECAssessmentType.Quarterly,
            AssessmentDate = DateTime.UtcNow,
            Status = GECAssessmentStatus.Submitted
        });

        GECAssessmentIndexEntry? latest = await index.GetLatestAssessmentAsync();
        Assert.That(latest, Is.Not.Null);
        Assert.That(latest!.AssessmentId, Is.EqualTo("NEW-ASSESS"));
    }

    [Test]
    public async Task GEC_WardCensusFiltering_WorksCorrectly()
    {
        ICLCAdmissionIndexGrain index = _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX-WARD");

        await index.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "WARD-A1", PatientId = "P1", PatientName = "Alpha",
            AdmitDate = DateTime.UtcNow, Status = CLCAdmissionStatus.Active,
            Ward = "CLC Ward 3B", BedRoom = "Bed 1"
        });
        await index.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = "WARD-B1", PatientId = "P2", PatientName = "Beta",
            AdmitDate = DateTime.UtcNow, Status = CLCAdmissionStatus.Active,
            Ward = "CLC Ward 2A", BedRoom = "Bed 5"
        });

        List<CLCAdmissionIndexEntry> ward3b = await index.GetAdmissionsByWardAsync("CLC Ward 3B");
        Assert.That(ward3b.Any(a => a.AdmissionId == "WARD-A1"), Is.True);
        Assert.That(ward3b.Any(a => a.AdmissionId == "WARD-B1"), Is.False);
    }

    [Test]
    public async Task GEC_HospiceAdmission_TrackedByLevelOfCare()
    {
        ICLCAdmissionIndexGrain index = _cluster.GrainFactory.GetGrain<ICLCAdmissionIndexGrain>("CLC-ADMIT-IDX-HOSPICE");

        string admissionId = $"CLC-ADMIT:{Guid.NewGuid()}";
        ICLCAdmissionGrain grain = _cluster.GrainFactory.GetGrain<ICLCAdmissionGrain>(admissionId);
        await grain.AdmitPatientAsync(
            $"PAT-H-{Guid.NewGuid()}", "Marsh, Faye", null,
            DateTime.UtcNow, CLCAdmitSource.AcuteHospital,
            GECLevelOfCare.Hospice,
            "Hospice Unit", "Room 105", "Dr. Palliative",
            "End-stage heart failure", string.Empty, null, "Comfort measures only");
        CLCAdmissionState state = await grain.GetAdmissionAsync();
        await index.UpsertAdmissionAsync(new CLCAdmissionIndexEntry
        {
            AdmissionId = state.AdmissionId, PatientId = state.PatientId, PatientName = state.PatientName,
            AdmitDate = state.AdmitDate, LevelOfCare = state.LevelOfCare, Status = state.Status,
            Ward = state.Ward, BedRoom = state.BedRoom
        });

        List<CLCAdmissionIndexEntry> hospice = await index.GetAdmissionsByLevelOfCareAsync(GECLevelOfCare.Hospice);
        Assert.That(hospice.Any(a => a.AdmissionId == state.AdmissionId), Is.True);
    }
}
