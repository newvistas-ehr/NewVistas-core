// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Vitals (File #120.5), Allergies (File #120.8), and Problem List (File #9000011) grains.
/// </summary>
[TestFixture]
public class VitalGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IVitalGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IVitalGrain>($"VITAL-{Guid.NewGuid()}");

    [Test]
    public async Task VitalGrain_RecordVital_PersistsAllFields()
    {
        IVitalGrain grain = NewGrain();
        DateTime taken = DateTime.UtcNow;

        await grain.RecordVitalAsync(
            "PATIENT-001", "BLOOD PRESSURE", "120/80", "mmHg",
            taken, "LOC-001", "Primary Care Clinic",
            "NURSE-001", "Smith, Jane",
            new List<string> { "SITTING", "RIGHT ARM" },
            "Patient relaxed during measurement");

        VitalState state = await grain.GetVitalAsync();

        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.VitalType, Is.EqualTo("BLOOD PRESSURE"));
        Assert.That(state.Value, Is.EqualTo("120/80"));
        Assert.That(state.Units, Is.EqualTo("mmHg"));
        Assert.That(state.DateTimeTaken, Is.EqualTo(taken));
        Assert.That(state.EnteredByName, Is.EqualTo("Smith, Jane"));
        Assert.That(state.Qualifiers, Does.Contain("SITTING"));
        Assert.That(state.Qualifiers, Does.Contain("RIGHT ARM"));
        Assert.That(state.Comments, Does.Contain("relaxed"));
        Assert.That(state.IsEnteredInError, Is.False);
    }

    [Test]
    public async Task VitalGrain_RecordTemperature_PersistsCorrectly()
    {
        IVitalGrain grain = NewGrain();

        await grain.RecordVitalAsync(
            "PATIENT-002", "TEMPERATURE", "98.6", "F",
            DateTime.UtcNow, null, null, null, null,
            null, null);

        VitalState state = await grain.GetVitalAsync();
        Assert.That(state.VitalType, Is.EqualTo("TEMPERATURE"));
        Assert.That(state.Value, Is.EqualTo("98.6"));
        Assert.That(state.Units, Is.EqualTo("F"));
    }

    [Test]
    public async Task VitalGrain_MarkAbnormal_SetsAbnormalFlag()
    {
        IVitalGrain grain = NewGrain();
        await grain.RecordVitalAsync(
            "PATIENT-003", "PULSE", "118", "per min",
            DateTime.UtcNow, null, null, null, null, null, null);

        await grain.MarkAbnormalAsync("H");

        VitalState state = await grain.GetVitalAsync();
        Assert.That(state.AbnormalFlag, Is.EqualTo("H"));
    }

    [Test]
    public async Task VitalGrain_MarkEnteredInError_SetsErrorFlag()
    {
        IVitalGrain grain = NewGrain();
        await grain.RecordVitalAsync(
            "PATIENT-004", "WEIGHT", "320", "lbs",
            DateTime.UtcNow, null, null, null, null, null, null);

        await grain.MarkEnteredInErrorAsync("Patient weighed with clothes — error");

        VitalState state = await grain.GetVitalAsync();
        Assert.That(state.IsEnteredInError, Is.True);
        Assert.That(state.EnteredInErrorReason, Does.Contain("clothes"));
    }

    [Test]
    public async Task VitalGrain_ValidateRange_NormalTemperature_NoFlags()
    {
        IVitalGrain grain = NewGrain();
        await grain.RecordVitalAsync(
            "PATIENT-005", "TEMPERATURE", "98.6", "F",
            DateTime.UtcNow, null, null, null, null, null, null);

        await grain.ValidateRangeAsync();

        VitalState state = await grain.GetVitalAsync();
        Assert.That(state.IsOutOfRange, Is.False);
    }

    [Test]
    public async Task VitalGrain_ValidateRange_LowO2Sat_FlagsAbnormal()
    {
        IVitalGrain grain = NewGrain();
        // Grain's PULSE OXIMETRY abnLow threshold is 50 — use 45 to trigger flag
        await grain.RecordVitalAsync(
            "PATIENT-006", "PULSE OXIMETRY", "45", "%",
            DateTime.UtcNow, null, null, null, null, null, null);

        await grain.ValidateRangeAsync();

        VitalState state = await grain.GetVitalAsync();
        Assert.That(state.IsAbnormalLow, Is.True);
    }

    [Test]
    public async Task VitalGrain_RecordWithNoQualifiers_EmptyList()
    {
        IVitalGrain grain = NewGrain();
        await grain.RecordVitalAsync(
            "PATIENT-007", "RESPIRATION", "16", "per min",
            DateTime.UtcNow, null, null, null, null, null, null);

        VitalState state = await grain.GetVitalAsync();
        Assert.That(state.Qualifiers, Is.Not.Null);
        Assert.That(state.Qualifiers, Is.Empty);
    }
}

[TestFixture]
public class EmbeddedAllergyTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientGrain NewPatient() =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>($"PATIENT-{Guid.NewGuid()}");

    private static AllergyEntry MakeEntry(string allergyId, string allergen, string type = "DRUG",
        List<string>? reactions = null, string? severity = null, string? observedHistorical = null,
        string? originatorName = null, string? comments = null) => new()
    {
        AllergyId = allergyId,
        Allergen = allergen,
        AllergenType = type,
        ReactionType = "ALLERGY",
        Reactions = reactions ?? new List<string>(),
        Severity = severity,
        ObservedHistorical = observedHistorical,
        OriginatorName = originatorName,
        Comments = comments,
        OriginationDateTime = DateTime.UtcNow,
        CreatedDate = DateTime.UtcNow,
        LastModifiedDate = DateTime.UtcNow
    };

    [Test]
    public async Task AddAllergy_PersistsAllFields()
    {
        IPatientGrain patient = NewPatient();

        AllergyEntry entry = MakeEntry("A-001", "PENICILLIN", "DRUG",
            new List<string> { "HIVES", "ANAPHYLAXIS" }, "SEVERE", "HISTORICAL",
            "Dr. Adams", "Well-documented allergy");

        await patient.AddAllergyAsync(entry);

        List<AllergyEntry> allergies = await patient.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(1));

        AllergyEntry stored = allergies[0];
        Assert.That(stored.Allergen, Is.EqualTo("PENICILLIN"));
        Assert.That(stored.AllergenType, Is.EqualTo("DRUG"));
        Assert.That(stored.Reactions, Has.Count.EqualTo(2));
        Assert.That(stored.Reactions, Does.Contain("HIVES"));
        Assert.That(stored.Reactions, Does.Contain("ANAPHYLAXIS"));
        Assert.That(stored.Severity, Is.EqualTo("SEVERE"));
        Assert.That(stored.ObservedHistorical, Is.EqualTo("HISTORICAL"));
        Assert.That(stored.OriginatorName, Is.EqualTo("Dr. Adams"));
        Assert.That(stored.IsVerified, Is.False);
        Assert.That(stored.IsEnteredInError, Is.False);
    }

    [Test]
    public async Task GetAllergy_ById_ReturnsSingleEntry()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddAllergyAsync(MakeEntry("A-010", "SULFA", "DRUG",
            new List<string> { "RASH" }, "MODERATE", "OBSERVED"));

        AllergyEntry? found = await patient.GetAllergyAsync("A-010");
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Allergen, Is.EqualTo("SULFA"));
    }

    [Test]
    public async Task GetAllergy_UnknownId_ReturnsNull()
    {
        IPatientGrain patient = NewPatient();
        AllergyEntry? found = await patient.GetAllergyAsync("NONEXISTENT");
        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task UpdateAllergy_ChangesSeverityAndComments()
    {
        IPatientGrain patient = NewPatient();
        AllergyEntry entry = MakeEntry("A-020", "IBUPROFEN", "DRUG",
            new List<string> { "GI UPSET" }, "MILD");
        await patient.AddAllergyAsync(entry);

        entry.Severity = "MODERATE";
        entry.Comments = "Patient now reports more severe reactions";
        entry.LastModifiedDate = DateTime.UtcNow;
        await patient.UpdateAllergyAsync(entry);

        AllergyEntry? updated = await patient.GetAllergyAsync("A-020");
        Assert.That(updated!.Severity, Is.EqualTo("MODERATE"));
        Assert.That(updated.Comments, Does.Contain("more severe"));
    }

    [Test]
    public async Task RemoveAllergy_RemovesById()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddAllergyAsync(MakeEntry("A-030", "ASPIRIN"));
        await patient.AddAllergyAsync(MakeEntry("A-031", "CODEINE"));

        await patient.RemoveAllergyAsync("A-030");

        List<AllergyEntry> remaining = await patient.GetAllergiesAsync();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].AllergyId, Is.EqualTo("A-031"));
    }

    [Test]
    public async Task AddAllergy_DuplicateId_IsIgnored()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddAllergyAsync(MakeEntry("A-040", "PENICILLIN"));
        await patient.AddAllergyAsync(MakeEntry("A-040", "PENICILLIN"));

        List<AllergyEntry> allergies = await patient.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AddMultipleAllergies_AllRetrieved()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddAllergyAsync(MakeEntry("A-050", "PENICILLIN", "DRUG"));
        await patient.AddAllergyAsync(MakeEntry("A-051", "PEANUTS", "FOOD"));
        await patient.AddAllergyAsync(MakeEntry("A-052", "LATEX", "OTHER"));

        List<AllergyEntry> allergies = await patient.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task NoAllergies_ReturnsEmptyList()
    {
        IPatientGrain patient = NewPatient();
        List<AllergyEntry> allergies = await patient.GetAllergiesAsync();
        Assert.That(allergies, Is.Empty);
    }

    [Test]
    public async Task RemoveAllergy_NonexistentId_DoesNotThrow()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddAllergyAsync(MakeEntry("A-060", "MORPHINE"));

        Assert.DoesNotThrowAsync(() => patient.RemoveAllergyAsync("NONEXISTENT"));

        List<AllergyEntry> allergies = await patient.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UpdateAllergy_NonexistentId_DoesNotThrow()
    {
        IPatientGrain patient = NewPatient();
        AllergyEntry entry = MakeEntry("NONEXISTENT", "WHATEVER");
        Assert.DoesNotThrowAsync(() => patient.UpdateAllergyAsync(entry));
    }

    [Test]
    public async Task AllergiesAreIndependentAcrossPatients()
    {
        IPatientGrain patient1 = NewPatient();
        IPatientGrain patient2 = NewPatient();

        await patient1.AddAllergyAsync(MakeEntry("A-070", "PENICILLIN"));

        List<AllergyEntry> p2Allergies = await patient2.GetAllergiesAsync();
        Assert.That(p2Allergies, Is.Empty);
    }
}

[TestFixture]
public class EmbeddedProblemTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientGrain NewPatient() =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>($"PATIENT-{Guid.NewGuid()}");

    private static ProblemEntry MakeEntry(string problemId, string diagnosis,
        string? diagnosisCode = null, string? condition = null, string? priority = null,
        DateTime? dateOfOnset = null, string? recordingProviderId = null,
        string? recordingProviderName = null, string? responsibleProviderId = null,
        string? responsibleProviderName = null, string? clinicId = null,
        string? clinicName = null, bool isServiceConnected = false,
        string? comments = null) => new()
    {
        ProblemId = problemId,
        Diagnosis = diagnosis,
        DiagnosisCode = diagnosisCode,
        Status = "ACTIVE",
        Condition = condition,
        Priority = priority,
        DateOfOnset = dateOfOnset,
        RecordingProviderId = recordingProviderId,
        RecordingProviderName = recordingProviderName,
        ResponsibleProviderId = responsibleProviderId,
        ResponsibleProviderName = responsibleProviderName,
        ClinicId = clinicId,
        ClinicName = clinicName,
        IsServiceConnected = isServiceConnected,
        Comments = comments,
        CreatedDate = DateTime.UtcNow,
        LastModifiedDate = DateTime.UtcNow
    };

    [Test]
    public async Task AddProblem_PersistsAllFields()
    {
        IPatientGrain patient = NewPatient();
        DateTime onset = new DateTime(2020, 3, 15);

        ProblemEntry entry = MakeEntry("P-001", "Type 2 Diabetes Mellitus", "E11.9",
            "ACTIVE", "CHRONIC", onset,
            "PROV-001", "Dr. Adams", "PROV-001", "Dr. Adams",
            "CLINIC-001", "Primary Care", true, "Well-controlled on metformin");

        await patient.AddProblemAsync(entry);

        List<ProblemEntry> problems = await patient.GetProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(1));

        ProblemEntry stored = problems[0];
        Assert.That(stored.Diagnosis, Is.EqualTo("Type 2 Diabetes Mellitus"));
        Assert.That(stored.DiagnosisCode, Is.EqualTo("E11.9"));
        Assert.That(stored.Condition, Is.EqualTo("ACTIVE"));
        Assert.That(stored.Priority, Is.EqualTo("CHRONIC"));
        Assert.That(stored.DateOfOnset, Is.EqualTo(onset));
        Assert.That(stored.RecordingProviderName, Is.EqualTo("Dr. Adams"));
        Assert.That(stored.IsServiceConnected, Is.True);
        Assert.That(stored.Comments, Does.Contain("metformin"));
    }

    [Test]
    public async Task GetProblem_ById_ReturnsSingleEntry()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddProblemAsync(MakeEntry("P-010", "Hypertension", "I10",
            "ACTIVE", "CHRONIC"));

        ProblemEntry? found = await patient.GetProblemAsync("P-010");
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Diagnosis, Is.EqualTo("Hypertension"));
    }

    [Test]
    public async Task GetProblem_UnknownId_ReturnsNull()
    {
        IPatientGrain patient = NewPatient();
        ProblemEntry? found = await patient.GetProblemAsync("NONEXISTENT");
        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task UpdateProblem_ChangesConditionAndComments()
    {
        IPatientGrain patient = NewPatient();
        ProblemEntry entry = MakeEntry("P-020", "Hypothyroidism", "E03.9",
            "ACTIVE", "CHRONIC");
        await patient.AddProblemAsync(entry);

        entry.Condition = "STABLE";
        entry.Comments = "Thyroid levels normalized on levothyroxine";
        entry.LastModifiedDate = DateTime.UtcNow;
        await patient.UpdateProblemAsync(entry);

        ProblemEntry? updated = await patient.GetProblemAsync("P-020");
        Assert.That(updated!.Condition, Is.EqualTo("STABLE"));
        Assert.That(updated.Comments, Does.Contain("levothyroxine"));
    }

    [Test]
    public async Task RemoveProblem_RemovesById()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddProblemAsync(MakeEntry("P-030", "PTSD", "F43.10"));
        await patient.AddProblemAsync(MakeEntry("P-031", "Tinnitus", "H93.19"));

        await patient.RemoveProblemAsync("P-030");

        List<ProblemEntry> remaining = await patient.GetProblemsAsync();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].ProblemId, Is.EqualTo("P-031"));
    }

    [Test]
    public async Task AddProblem_DuplicateId_IsIgnored()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddProblemAsync(MakeEntry("P-040", "Hypertension", "I10"));
        await patient.AddProblemAsync(MakeEntry("P-040", "Hypertension", "I10"));

        List<ProblemEntry> problems = await patient.GetProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ServiceConnectedProblem_FlagSet()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddProblemAsync(MakeEntry("P-050", "PTSD", "F43.10",
            "ACTIVE", "CHRONIC", new DateTime(2005, 6, 1),
            "PROV-002", "Dr. Baker", "PROV-002", "Dr. Baker",
            "CLINIC-002", "Mental Health", true, "Combat-related"));

        ProblemEntry? stored = await patient.GetProblemAsync("P-050");
        Assert.That(stored!.IsServiceConnected, Is.True);
    }

    [Test]
    public async Task NullOptionalFields_DoesNotThrow()
    {
        IPatientGrain patient = NewPatient();
        ProblemEntry entry = MakeEntry("P-060", "Obesity", "E66.9", "ACTIVE");

        Assert.DoesNotThrowAsync(() => patient.AddProblemAsync(entry));

        ProblemEntry? stored = await patient.GetProblemAsync("P-060");
        Assert.That(stored!.Diagnosis, Is.EqualTo("Obesity"));
        Assert.That(stored.Priority, Is.Null.Or.Empty);
    }

    [Test]
    public async Task AddMultipleProblems_AllRetrieved()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddProblemAsync(MakeEntry("P-070", "Hypertension", "I10"));
        await patient.AddProblemAsync(MakeEntry("P-071", "Diabetes", "E11.9"));
        await patient.AddProblemAsync(MakeEntry("P-072", "PTSD", "F43.10"));

        List<ProblemEntry> problems = await patient.GetProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task NoProblems_ReturnsEmptyList()
    {
        IPatientGrain patient = NewPatient();
        List<ProblemEntry> problems = await patient.GetProblemsAsync();
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public async Task RemoveProblem_NonexistentId_DoesNotThrow()
    {
        IPatientGrain patient = NewPatient();
        await patient.AddProblemAsync(MakeEntry("P-080", "Tinnitus"));

        Assert.DoesNotThrowAsync(() => patient.RemoveProblemAsync("NONEXISTENT"));

        List<ProblemEntry> problems = await patient.GetProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ProblemsAreIndependentAcrossPatients()
    {
        IPatientGrain patient1 = NewPatient();
        IPatientGrain patient2 = NewPatient();

        await patient1.AddProblemAsync(MakeEntry("P-090", "Hypertension", "I10"));

        List<ProblemEntry> p2Problems = await patient2.GetProblemsAsync();
        Assert.That(p2Problems, Is.Empty);
    }
}

[TestFixture]
public class SiteParametersGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ISiteParametersGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>($"SITE-{Guid.NewGuid()}");

    [Test]
    public async Task SiteParams_DefaultVitalsDisplayCount_Is10()
    {
        ISiteParametersGrain grain = NewGrain();

        int count = await grain.GetVitalsDisplayCountAsync();

        Assert.That(count, Is.EqualTo(10));
    }

    [Test]
    public async Task SiteParams_SetVitalsDisplayCount_PersistsValue()
    {
        ISiteParametersGrain grain = NewGrain();

        await grain.SetVitalsDisplayCountAsync(15);

        int count = await grain.GetVitalsDisplayCountAsync();
        Assert.That(count, Is.EqualTo(15));
    }

    [Test]
    public async Task SiteParams_SetParameter_PersistsKeyValue()
    {
        ISiteParametersGrain grain = NewGrain();

        await grain.SetParameterAsync("ORWCV VITALS", "20");

        string? value = await grain.GetParameterAsync("ORWCV VITALS");
        Assert.That(value, Is.EqualTo("20"));
    }

    [Test]
    public async Task SiteParams_GetParameter_UnknownKey_ReturnsNull()
    {
        ISiteParametersGrain grain = NewGrain();

        string? value = await grain.GetParameterAsync("NONEXISTENT_PARAM");

        Assert.That(value, Is.Null);
    }

    [Test]
    public async Task SiteParams_MultipleParameters_AllPersisted()
    {
        ISiteParametersGrain grain = NewGrain();

        await grain.SetParameterAsync("PARAM_A", "ValueA");
        await grain.SetParameterAsync("PARAM_B", "ValueB");
        await grain.SetParameterAsync("PARAM_C", "ValueC");

        string? a = await grain.GetParameterAsync("PARAM_A");
        string? b = await grain.GetParameterAsync("PARAM_B");
        string? c = await grain.GetParameterAsync("PARAM_C");

        Assert.That(a, Is.EqualTo("ValueA"));
        Assert.That(b, Is.EqualTo("ValueB"));
        Assert.That(c, Is.EqualTo("ValueC"));
    }

    [Test]
    public async Task SiteParams_GetParameters_ReturnsFullState()
    {
        ISiteParametersGrain grain = _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:TESTFACILITY");

        await grain.SetParameterAsync("INIT_PARAM", "InitValue");

        SiteParametersState state = await grain.GetParametersAsync();

        Assert.That(state, Is.Not.Null);
        Assert.That(state.SiteId, Is.EqualTo("SITE:TESTFACILITY"));
        Assert.That(state.Parameters, Does.ContainKey("INIT_PARAM"));
    }

    [Test]
    public async Task SiteParams_OverwriteParameter_UpdatesValue()
    {
        ISiteParametersGrain grain = NewGrain();

        await grain.SetParameterAsync("OVERWRITE_KEY", "OriginalValue");
        await grain.SetParameterAsync("OVERWRITE_KEY", "UpdatedValue");

        string? value = await grain.GetParameterAsync("OVERWRITE_KEY");
        Assert.That(value, Is.EqualTo("UpdatedValue"));
    }
}

[TestFixture]
public class PatientVitalIndexGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientVitalIndexGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IPatientVitalIndexGrain>($"PATIENT-{Guid.NewGuid()}");

    [Test]
    public async Task VitalIndex_AddKey_AppearsInAllKeys()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime taken = DateTime.UtcNow;

        await grain.AddVitalKeyAsync("VITAL:P001:20260310120000:PULSE", taken, "PULSE");

        List<VitalIndexEntry> keys = await grain.GetAllKeysAsync();
        Assert.That(keys, Has.Count.EqualTo(1));
        Assert.That(keys[0].VitalGrainKey, Is.EqualTo("VITAL:P001:20260310120000:PULSE"));
        Assert.That(keys[0].VitalType, Is.EqualTo("PULSE"));
    }

    [Test]
    public async Task VitalIndex_MultipleKeys_SortedByDateDescending()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime oldest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime middle = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime newest = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

        await grain.AddVitalKeyAsync("VITAL:KEY:OLDEST", oldest, "TEMPERATURE");
        await grain.AddVitalKeyAsync("VITAL:KEY:NEWEST", newest, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:KEY:MIDDLE", middle, "BLOOD PRESSURE");

        List<VitalIndexEntry> keys = await grain.GetAllKeysAsync();
        Assert.That(keys, Has.Count.EqualTo(3));
        Assert.That(keys[0].VitalGrainKey, Is.EqualTo("VITAL:KEY:NEWEST"));
        Assert.That(keys[1].VitalGrainKey, Is.EqualTo("VITAL:KEY:MIDDLE"));
        Assert.That(keys[2].VitalGrainKey, Is.EqualTo("VITAL:KEY:OLDEST"));
    }

    [Test]
    public async Task VitalIndex_DuplicateKey_IsIgnored()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime taken = DateTime.UtcNow;

        await grain.AddVitalKeyAsync("VITAL:DUP:KEY", taken, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:DUP:KEY", taken, "PULSE");

        List<VitalIndexEntry> keys = await grain.GetAllKeysAsync();
        Assert.That(keys, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task VitalIndex_RemoveKey_RemovesFromIndex()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime taken = DateTime.UtcNow;

        await grain.AddVitalKeyAsync("VITAL:KEEP", taken, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:REMOVE", taken.AddMinutes(-5), "TEMPERATURE");

        await grain.RemoveVitalKeyAsync("VITAL:REMOVE");

        List<VitalIndexEntry> keys = await grain.GetAllKeysAsync();
        Assert.That(keys, Has.Count.EqualTo(1));
        Assert.That(keys[0].VitalGrainKey, Is.EqualTo("VITAL:KEEP"));
    }

    [Test]
    public async Task VitalIndex_GetByDateRange_FiltersCorrectly()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime jan = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        DateTime feb = new DateTime(2026, 2, 15, 10, 0, 0, DateTimeKind.Utc);
        DateTime mar = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        await grain.AddVitalKeyAsync("VITAL:JAN", jan, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:FEB", feb, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:MAR", mar, "PULSE");

        DateTime rangeFrom = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime rangeTo = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        List<VitalIndexEntry> keys = await grain.GetKeysByDateRangeAsync(rangeFrom, rangeTo);
        Assert.That(keys, Has.Count.EqualTo(1));
        Assert.That(keys[0].VitalGrainKey, Is.EqualTo("VITAL:FEB"));
    }

    [Test]
    public async Task VitalIndex_GetKeysBeforeDate_ReturnsLimited()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime d1 = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime d2 = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime d3 = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        DateTime d4 = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        await grain.AddVitalKeyAsync("VITAL:D1", d1, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:D2", d2, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:D3", d3, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:D4", d4, "PULSE");

        DateTime before = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        List<VitalIndexEntry> keys = await grain.GetKeysBeforeDateAsync(before, 2);

        Assert.That(keys, Has.Count.EqualTo(2));
        // Should return the 2 most recent entries before the cutoff (d3 then d2)
        Assert.That(keys[0].VitalGrainKey, Is.EqualTo("VITAL:D3"));
        Assert.That(keys[1].VitalGrainKey, Is.EqualTo("VITAL:D2"));
    }

    [Test]
    public async Task VitalIndex_GetKeysByTypeAndDateRange_FiltersCorrectly()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime d1 = new DateTime(2026, 2, 10, 8, 0, 0, DateTimeKind.Utc);
        DateTime d2 = new DateTime(2026, 2, 15, 8, 0, 0, DateTimeKind.Utc);
        DateTime d3 = new DateTime(2026, 2, 20, 8, 0, 0, DateTimeKind.Utc);

        await grain.AddVitalKeyAsync("VITAL:PULSE1", d1, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:TEMP1", d2, "TEMPERATURE");
        await grain.AddVitalKeyAsync("VITAL:PULSE2", d3, "PULSE");

        DateTime from = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        List<VitalIndexEntry> keys = await grain.GetKeysByTypeAndDateRangeAsync("PULSE", from, to);
        Assert.That(keys, Has.Count.EqualTo(2));
        Assert.That(keys[0].VitalGrainKey, Is.EqualTo("VITAL:PULSE2"));
        Assert.That(keys[1].VitalGrainKey, Is.EqualTo("VITAL:PULSE1"));
    }

    [Test]
    public async Task VitalIndex_GetCount_ReturnsCorrectCount()
    {
        IPatientVitalIndexGrain grain = NewGrain();
        DateTime taken = DateTime.UtcNow;

        await grain.AddVitalKeyAsync("VITAL:C1", taken, "PULSE");
        await grain.AddVitalKeyAsync("VITAL:C2", taken.AddMinutes(-1), "TEMPERATURE");
        await grain.AddVitalKeyAsync("VITAL:C3", taken.AddMinutes(-2), "BLOOD PRESSURE");

        int count = await grain.GetCountAsync();
        Assert.That(count, Is.EqualTo(3));
    }
}

[TestFixture]
public class RecentVitalsCacheTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientGrain NewPatient() =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>($"PATIENT-{Guid.NewGuid()}");

    private static VitalSummary MakeSummary(string vitalId, string vitalType,
        string value, DateTime dateTimeTaken, string? units = null,
        string? abnormalFlag = null) => new()
    {
        VitalId = vitalId,
        VitalType = vitalType,
        Value = value,
        Units = units,
        DateTimeTaken = dateTimeTaken,
        AbnormalFlag = abnormalFlag
    };

    [Test]
    public async Task AddRecentVital_AppearsInCache()
    {
        IPatientGrain patient = NewPatient();
        DateTime taken = DateTime.UtcNow;

        VitalSummary summary = MakeSummary("V-001", "PULSE", "72", taken, "per min");
        await patient.AddRecentVitalAsync(summary, 10);

        List<VitalSummary> vitals = await patient.GetRecentVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(1));
        Assert.That(vitals[0].VitalId, Is.EqualTo("V-001"));
        Assert.That(vitals[0].VitalType, Is.EqualTo("PULSE"));
        Assert.That(vitals[0].Value, Is.EqualTo("72"));
    }

    [Test]
    public async Task AddRecentVital_TrimToMaxCount()
    {
        IPatientGrain patient = NewPatient();
        DateTime baseTime = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            VitalSummary summary = MakeSummary($"V-{i:D3}", "PULSE", $"{70 + i}",
                baseTime.AddMinutes(i), "per min");
            await patient.AddRecentVitalAsync(summary, 3);
        }

        List<VitalSummary> vitals = await patient.GetRecentVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(3));
        // Should keep the 3 most recent (V-004, V-003, V-002)
        Assert.That(vitals[0].VitalId, Is.EqualTo("V-004"));
        Assert.That(vitals[1].VitalId, Is.EqualTo("V-003"));
        Assert.That(vitals[2].VitalId, Is.EqualTo("V-002"));
    }

    [Test]
    public async Task GetRecentVitals_Empty_ReturnsEmptyList()
    {
        IPatientGrain patient = NewPatient();

        List<VitalSummary> vitals = await patient.GetRecentVitalsAsync();
        Assert.That(vitals, Is.Empty);
    }

    [Test]
    public async Task SetRecentVitals_ReplacesEntireCache()
    {
        IPatientGrain patient = NewPatient();
        DateTime taken = DateTime.UtcNow;

        // Add an initial vital
        await patient.AddRecentVitalAsync(
            MakeSummary("V-OLD", "PULSE", "72", taken, "per min"), 10);

        // Replace with a new list
        List<VitalSummary> newVitals = new()
        {
            MakeSummary("V-NEW-1", "TEMPERATURE", "98.6", taken, "F"),
            MakeSummary("V-NEW-2", "BLOOD PRESSURE", "120/80", taken.AddMinutes(-1), "mmHg")
        };
        await patient.SetRecentVitalsAsync(newVitals);

        List<VitalSummary> vitals = await patient.GetRecentVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(2));
        Assert.That(vitals[0].VitalId, Is.EqualTo("V-NEW-1"));
        Assert.That(vitals[1].VitalId, Is.EqualTo("V-NEW-2"));
    }

    [Test]
    public async Task RecentVitals_MostRecentFirst()
    {
        IPatientGrain patient = NewPatient();
        DateTime baseTime = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);

        // Add in chronological order — each Insert(0) pushes to front
        await patient.AddRecentVitalAsync(
            MakeSummary("V-OLD", "TEMPERATURE", "98.6", baseTime), 10);
        await patient.AddRecentVitalAsync(
            MakeSummary("V-MID", "PULSE", "72", baseTime.AddHours(1)), 10);
        await patient.AddRecentVitalAsync(
            MakeSummary("V-NEW", "BLOOD PRESSURE", "120/80", baseTime.AddHours(2)), 10);

        List<VitalSummary> vitals = await patient.GetRecentVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(3));
        // Most recently added is at position 0 (Insert(0) semantics)
        Assert.That(vitals[0].VitalId, Is.EqualTo("V-NEW"));
        Assert.That(vitals[1].VitalId, Is.EqualTo("V-MID"));
        Assert.That(vitals[2].VitalId, Is.EqualTo("V-OLD"));
    }
}
