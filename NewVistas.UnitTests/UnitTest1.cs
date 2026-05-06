// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class PatientGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task PatientGrain_CanUpdateDemographics()
    {
        // Arrange
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        // Act
        await patient.UpdateDemographicsAsync(
            "John Doe",
            "M",
            new DateTime(1980, 1, 15),
            "123-45-6789");

        var state = await patient.GetPatientAsync();

        // Assert
        Assert.That(state.Name, Is.EqualTo("John Doe"));
        Assert.That(state.Sex, Is.EqualTo("M"));
        Assert.That(state.DateOfBirth, Is.EqualTo(new DateTime(1980, 1, 15)));
        Assert.That(state.SocialSecurityNumber, Is.EqualTo("123-45-6789"));
    }

    [Test]
    public async Task PatientGrain_CanAddAndRetrieveAppointmentIds()
    {
        // Arrange
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        var appointmentId1 = $"APPT-{Guid.NewGuid()}";
        var appointmentId2 = $"APPT-{Guid.NewGuid()}";

        // Act
        await patient.AddAppointmentIdAsync(appointmentId1);
        await patient.AddAppointmentIdAsync(appointmentId2);
        var appointmentIds = await patient.GetAppointmentIdsAsync();

        // Assert
        Assert.That(appointmentIds, Has.Count.EqualTo(2));
        Assert.That(appointmentIds, Contains.Item(appointmentId1));
        Assert.That(appointmentIds, Contains.Item(appointmentId2));
    }

    [Test]
    public async Task PatientGrain_CanCalculateAge()
    {
        // Arrange
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        var birthDate = DateTime.Today.AddYears(-30);

        await patient.UpdateDemographicsAsync("Jane Doe", "F", birthDate, null);

        // Act
        var age = await patient.GetAgeAsync();

        // Assert
        Assert.That(age, Is.EqualTo(30));
    }

    [Test]
    public async Task PatientGrain_IsVeteran_ReturnsTrue_WhenVeteranFlagIsY()
    {
        // Arrange
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        await patient.UpdateVeteranInfoAsync("Y", 50, "ELIGIBLE", "PRIMARY");

        // Act
        var isVeteran = await patient.IsVeteranAsync();

        // Assert
        Assert.That(isVeteran, Is.True);
    }
}

[TestFixture]
public class AppointmentGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task AppointmentGrain_CanScheduleAppointment()
    {
        // Arrange
        var appointmentId = $"APPT-{Guid.NewGuid()}";
        var appointment = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        var appointmentDateTime = DateTime.UtcNow.AddDays(7);

        // Act
        await appointment.ScheduleAppointmentAsync(
            patientId: "PATIENT-001",
            clinicId: "CLINIC-001",
            clinicName: "Cardiology Clinic",
            appointmentDateTime: appointmentDateTime,
            durationMinutes: 30,
            providerId: "PROVIDER-123",
            providerName: "Dr. Smith",
            purpose: "Annual Checkup",
            appointmentType: "ROUTINE",
            createdBy: "USER-001");

        var state = await appointment.GetAppointmentAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.ClinicName, Is.EqualTo("Cardiology Clinic"));
        Assert.That(state.Status, Is.EqualTo("Scheduled"));
    }

    [Test]
    public async Task AppointmentGrain_CanCheckIn()
    {
        // Arrange
        var appointmentId = $"APPT-{Guid.NewGuid()}";
        var appointment = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        
        await appointment.ScheduleAppointmentAsync(
            "PATIENT-001", "CLINIC-001", "Clinic", 
            DateTime.UtcNow.AddHours(1), 30, null, null, null, null, null);

        // Act
        await appointment.CheckInAsync(DateTime.UtcNow);
        var state = await appointment.GetAppointmentAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("Checked In"));
        Assert.That(state.CheckInDateTime, Is.Not.Null);
    }
}

[TestFixture]
public class LabTestGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task LabTestGrain_CanOrderLabTest()
    {
        // Arrange
        var labTestId = $"LAB-{Guid.NewGuid()}";
        var labTest = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labTestId);

        // Act
        await labTest.OrderLabTestAsync(
            patientId: "PATIENT-001",
            testId: "60-1",
            testName: "WBC",
            testCode: "WBC",
            orderId: null,
            orderingProviderId: "PROVIDER-123",
            orderingProviderName: "Dr. Smith",
            specimenType: "Blood",
            category: "HEMATOLOGY");

        var state = await labTest.GetLabTestAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.TestName, Is.EqualTo("WBC"));
        Assert.That(state.Status, Is.EqualTo("Ordered"));
    }

    [Test]
    public async Task LabTestGrain_CanRecordResult()
    {
        // Arrange
        var labTestId = $"LAB-{Guid.NewGuid()}";
        var labTest = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        
        await labTest.OrderLabTestAsync(
            "PATIENT-001", "60-1", "WBC", "WBC", null,
            "PROVIDER-123", "Dr. Smith", "Blood", "HEMATOLOGY");

        // Act
        await labTest.RecordResultAsync(
            DateTime.UtcNow,
            "7.5",
            "K/cmm",
            "3.4",
            "8.3",
            "Normal");

        var state = await labTest.GetLabTestAsync();

        // Assert
        Assert.That(state.ResultValue, Is.EqualTo("7.5"));
        Assert.That(state.AbnormalFlag, Is.EqualTo("Normal"));
    }
}

[TestFixture]
public class EmbeddedAllergyBasicTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task EmbeddedAllergy_CanRecordAllergy()
    {
        // Arrange
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var entry = new AllergyEntry
        {
            AllergyId = $"ALLERGY-{Guid.NewGuid()}",
            Allergen = "Penicillin",
            AllergenType = "Drug",
            ReactionType = "ALLERGY",
            Reactions = new List<string> { "Rash", "Itching" },
            Severity = "Moderate",
            ReactionDateTime = DateTime.UtcNow,
            ObservedHistorical = "O",
            OriginatorId = "USER-001",
            OriginatorName = "Nurse Jane",
            Comments = "Patient reported reaction during previous visit"
        };

        // Act
        await patient.AddAllergyAsync(entry);
        List<AllergyEntry> allergies = await patient.GetAllergiesAsync();

        // Assert
        Assert.That(allergies, Has.Count.EqualTo(1));
        Assert.That(allergies[0].Allergen, Is.EqualTo("Penicillin"));
        Assert.That(allergies[0].Reactions, Has.Count.EqualTo(2));
        Assert.That(allergies[0].Severity, Is.EqualTo("Moderate"));
    }

    [Test]
    public async Task EmbeddedAllergy_CanUpdateAllergy()
    {
        // Arrange
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var entry = new AllergyEntry
        {
            AllergyId = $"ALLERGY-{Guid.NewGuid()}",
            Allergen = "Latex",
            AllergenType = "Other",
            ReactionType = "ALLERGY",
            Reactions = new List<string> { "Contact Dermatitis" },
            Severity = "Mild"
        };

        await patient.AddAllergyAsync(entry);

        // Act — update via the entry
        entry.IsVerified = true;
        entry.VerifiedById = "PROVIDER-123";
        entry.VerifiedByName = "Dr. Smith";
        entry.VerifiedDateTime = DateTime.UtcNow;
        entry.LastModifiedDate = DateTime.UtcNow;
        await patient.UpdateAllergyAsync(entry);

        AllergyEntry? updated = await patient.GetAllergyAsync(entry.AllergyId);

        // Assert
        Assert.That(updated!.IsVerified, Is.True);
        Assert.That(updated.VerifiedByName, Is.EqualTo("Dr. Smith"));
    }
}
