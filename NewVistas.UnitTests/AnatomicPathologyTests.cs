// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Anatomic Pathology grain layer.
/// Covers IAnatomicPathologyCaseGrain and IAnatomicPathologyCaseIndexGrain
/// in isolation using in-memory Orleans TestCluster.
/// VistA Files #63.08 (SP), #63.09 (CY), #63.19 (AU).
/// </summary>
[TestFixture]
public class AnatomicPathologyCaseGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IAnatomicPathologyCaseGrain NewCase() =>
        _cluster.GrainFactory.GetGrain<IAnatomicPathologyCaseGrain>($"AP-CASE:{Guid.NewGuid()}");

    // ─── Accession ────────────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_CanAccessionSurgicalPathologyCase()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        DateTime received = DateTime.UtcNow;

        await grain.AccessionCaseAsync(
            "PAT-001", APCaseType.SurgicalPathology, "SP-2024-00001",
            "Right lung, lower lobe", "Wedge biopsy", "Biopsy",
            "Cough and nodule on CT", "r/o adenocarcinoma",
            "PROV-001", "Dr. Ordering",
            "Pulmonary Clinic", DateTime.UtcNow.AddHours(-2), received);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.CaseType, Is.EqualTo(APCaseType.SurgicalPathology));
        Assert.That(state.AccessionNumber, Is.EqualTo("SP-2024-00001"));
        Assert.That(state.SpecimenSource, Is.EqualTo("Right lung, lower lobe"));
        Assert.That(state.SpecimenDescription, Is.EqualTo("Wedge biopsy"));
        Assert.That(state.SpecimenType, Is.EqualTo("Biopsy"));
        Assert.That(state.ClinicalHistory, Is.EqualTo("Cough and nodule on CT"));
        Assert.That(state.ClinicalDiagnosis, Is.EqualTo("r/o adenocarcinoma"));
        Assert.That(state.ReferringProviderName, Is.EqualTo("Dr. Ordering"));
        Assert.That(state.CollectionLocation, Is.EqualTo("Pulmonary Clinic"));
        Assert.That(state.DateReceived, Is.EqualTo(received).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task CaseGrain_DefaultStatus_IsReceived()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();

        await grain.AccessionCaseAsync(
            "PAT-002", APCaseType.SurgicalPathology, "SP-2024-00002",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Received));
    }

    [Test]
    public async Task CaseGrain_CaseId_MatchesGrainKey()
    {
        string key = $"AP-CASE:{Guid.NewGuid()}";
        IAnatomicPathologyCaseGrain grain =
            _cluster.GrainFactory.GetGrain<IAnatomicPathologyCaseGrain>(key);

        await grain.AccessionCaseAsync(
            "PAT-003", APCaseType.Cytology, "CY-2024-00001",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.CaseId, Is.EqualTo(key));
    }

    // ─── Gross Description ────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_RecordGrossDescription_TransitionsToInProgress()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-010", APCaseType.SurgicalPathology, "SP-2024-00010",
            "Colon, sigmoid", null, "Resection",
            null, null, null, null, null, null, DateTime.UtcNow);

        await grain.RecordGrossDescriptionAsync(
            "Received is a segment of colon measuring 15 cm in length and 4 cm in diameter. " +
            "Cut surface reveals a 3.5 cm ulcerating mass.",
            "PATH-001", "Dr. Pathologist",
            specimenPartCount: 1, specimenWeightGrams: 85m,
            frozenSectionDiagnosis: null);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.InProgress));
        Assert.That(state.GrossDescription, Does.Contain("3.5 cm ulcerating mass"));
        Assert.That(state.GrossPathologistName, Is.EqualTo("Dr. Pathologist"));
        Assert.That(state.SpecimenPartCount, Is.EqualTo(1));
        Assert.That(state.SpecimenWeightGrams, Is.EqualTo(85m));
        Assert.That(state.GrossExamDateTime, Is.Not.Null);
    }

    [Test]
    public async Task CaseGrain_RecordGrossDescription_WithFrozenSection()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-011", APCaseType.SurgicalPathology, "SP-2024-00011",
            "Breast, left", null, "Excision",
            null, null, null, null, null, null, DateTime.UtcNow);

        await grain.RecordGrossDescriptionAsync(
            "Fibrofatty tissue with firm white area.",
            "PATH-001", "Dr. Pathologist",
            specimenPartCount: 1, specimenWeightGrams: 42m,
            frozenSectionDiagnosis: "Invasive ductal carcinoma");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.FrozenSectionDiagnosis, Is.EqualTo("Invasive ductal carcinoma"));
    }

    // ─── Microscopic Description ──────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_RecordMicroscopicDescription_StoresText()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-020", APCaseType.SurgicalPathology, "SP-2024-00020",
            "Skin, right arm", null, null,
            null, null, null, null, null, null, DateTime.UtcNow);
        await grain.RecordGrossDescriptionAsync(
            "Ellipse of skin 1.5 x 0.8 cm.", null, null, null, null, null);

        await grain.RecordMicroscopicDescriptionAsync(
            "Sections show atypical melanocytic proliferation with pagetoid spread. " +
            "No lymphovascular invasion identified.");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.MicroscopicDescription, Does.Contain("melanocytic proliferation"));
    }

    // ─── Preliminary Diagnosis ────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_IssuePreliminaryDiagnosis_TransitionsToPreliminary()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-030", APCaseType.SurgicalPathology, "SP-2024-00030",
            "Liver, right lobe", null, "Biopsy",
            null, null, null, null, null, null, DateTime.UtcNow);

        await grain.IssuePreliminaryDiagnosisAsync(
            "Hepatocellular carcinoma, preliminary — pending IHC.",
            "PATH-001", "Dr. Pathologist");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Preliminary));
        Assert.That(state.Diagnosis, Does.Contain("Hepatocellular carcinoma"));
        Assert.That(state.PathologistName, Is.EqualTo("Dr. Pathologist"));
    }

    // ─── Sign-Out / Final Diagnosis ───────────────────────────────────────────

    [Test]
    public async Task CaseGrain_SignOutDiagnosis_TransitionsToFinal()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-040", APCaseType.SurgicalPathology, "SP-2024-00040",
            "Prostate, radical prostatectomy specimen", null, "Excision",
            null, null, null, null, null, null, DateTime.UtcNow);
        await grain.RecordGrossDescriptionAsync(
            "Prostate gland 45g.", "PATH-001", "Dr. Pathologist",
            null, 45m, null);

        DateTime signOut = DateTime.UtcNow;
        await grain.SignOutDiagnosisAsync(
            "Prostatic adenocarcinoma, Gleason score 7 (3+4), pT2c, margins negative.",
            new List<string> { "C61", "Z80.42" },
            "PATH-001", "Dr. Pathologist", signOut);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Final));
        Assert.That(state.Diagnosis, Does.Contain("Gleason score 7"));
        Assert.That(state.DiagnosisCodes, Has.Count.EqualTo(2));
        Assert.That(state.DiagnosisCodes, Contains.Item("C61"));
        Assert.That(state.PathologistName, Is.EqualTo("Dr. Pathologist"));
        Assert.That(state.SignOutDateTime, Is.Not.Null);
        Assert.That(state.DateReported, Is.Not.Null);
    }

    [Test]
    public async Task CaseGrain_SignOut_SetsDateReported()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-041", APCaseType.SurgicalPathology, "SP-2024-00041",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        DateTime signOut = DateTime.UtcNow;
        await grain.SignOutDiagnosisAsync(
            "Benign tissue.", new List<string>(),
            "PATH-001", "Dr. Pathologist", signOut);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.DateReported, Is.EqualTo(signOut).Within(TimeSpan.FromSeconds(1)));
    }

    // ─── Addendum ─────────────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_AddAddendum_TransitionsToAddendum()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-050", APCaseType.SurgicalPathology, "SP-2024-00050",
            "Thyroid, left lobe", null, null,
            null, null, null, null, null, null, DateTime.UtcNow);
        await grain.SignOutDiagnosisAsync(
            "Papillary thyroid carcinoma.", new List<string> { "C73" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        await grain.AddAddendumAsync(
            "ADDENDUM: BRAF V600E mutation detected by molecular testing.",
            "PATH-001", "Dr. Pathologist");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Addendum));
        Assert.That(state.Addendum, Does.Contain("BRAF V600E"));
        Assert.That(state.AddendumDate, Is.Not.Null);
        Assert.That(state.AddendumPathologistName, Is.EqualTo("Dr. Pathologist"));
    }

    // ─── Amendment ────────────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_AmendDiagnosis_TransitionsToAmended()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-060", APCaseType.SurgicalPathology, "SP-2024-00060",
            "Lymph node, axillary", null, "Excision",
            null, null, null, null, null, null, DateTime.UtcNow);
        await grain.SignOutDiagnosisAsync(
            "Reactive lymphadenopathy.", new List<string> { "R59.0" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        await grain.AmendDiagnosisAsync(
            "Diffuse large B-cell lymphoma.",
            new List<string> { "C83.30" },
            "IHC results returned after sign-out confirm B-cell lineage.",
            "PATH-001", "Dr. Pathologist");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Amended));
        Assert.That(state.Diagnosis, Is.EqualTo("Diffuse large B-cell lymphoma."));
        Assert.That(state.DiagnosisCodes, Contains.Item("C83.30"));
        Assert.That(state.AmendmentReason, Does.Contain("IHC results"));
    }

    [Test]
    public async Task CaseGrain_Amend_ReplacesDiagnosisCodes()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-061", APCaseType.SurgicalPathology, "SP-2024-00061",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await grain.SignOutDiagnosisAsync(
            "Reactive change.", new List<string> { "R59.0" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        await grain.AmendDiagnosisAsync(
            "Follicular lymphoma.",
            new List<string> { "C82.00" },
            "Corrected after flow cytometry.",
            "PATH-001", "Dr. Pathologist");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.DiagnosisCodes, Has.Count.EqualTo(1));
        Assert.That(state.DiagnosisCodes, Contains.Item("C82.00"));
        Assert.That(state.DiagnosisCodes, Does.Not.Contain("R59.0"));
    }

    // ─── Supplemental Studies ─────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_AddSpecialStain_AppearsInList()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-070", APCaseType.SurgicalPathology, "SP-2024-00070",
            "Lung", null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.AddSpecialStainAsync("GMS");
        await grain.AddSpecialStainAsync("PAS");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.SpecialStains, Has.Count.EqualTo(2));
        Assert.That(state.SpecialStains, Contains.Item("GMS"));
        Assert.That(state.SpecialStains, Contains.Item("PAS"));
    }

    [Test]
    public async Task CaseGrain_AddSpecialStain_NoDuplicates()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-071", APCaseType.SurgicalPathology, "SP-2024-00071",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.AddSpecialStainAsync("AFB");
        await grain.AddSpecialStainAsync("AFB");
        await grain.AddSpecialStainAsync("AFB");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.SpecialStains, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CaseGrain_AddIHCResult_AppearsInList()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-072", APCaseType.SurgicalPathology, "SP-2024-00072",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.AddImmunohistochemistryResultAsync("CD20: Positive (diffuse)");
        await grain.AddImmunohistochemistryResultAsync("CD3: Negative");
        await grain.AddImmunohistochemistryResultAsync("Ki-67: 80%");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.ImmunohistochemistryResults, Has.Count.EqualTo(3));
        Assert.That(state.ImmunohistochemistryResults, Contains.Item("CD20: Positive (diffuse)"));
    }

    // ─── Cytology-Specific ────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_RecordCytologyDetails_StoresBethesdaAndAdequacy()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-080", APCaseType.Cytology, "CY-2024-00001",
            "Cervix", "PAP smear", "Smear",
            null, null, null, null, null, null, DateTime.UtcNow);

        await grain.RecordCytologyDetailsAsync(
            "NILM — Negative for Intraepithelial Lesion or Malignancy",
            "Satisfactory for evaluation");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.BethesdaCategory, Is.EqualTo("NILM — Negative for Intraepithelial Lesion or Malignancy"));
        Assert.That(state.SpecimenAdequacy, Is.EqualTo("Satisfactory for evaluation"));
    }

    [Test]
    public async Task CaseGrain_RecordCytologyDetails_NullValuesAllowed()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-081", APCaseType.Cytology, "CY-2024-00002",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.RecordCytologyDetailsAsync(null, null);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.BethesdaCategory, Is.Null);
        Assert.That(state.SpecimenAdequacy, Is.Null);
    }

    // ─── Autopsy-Specific ─────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_RecordAutopsyFindings_StoresAllFields()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-090", APCaseType.Autopsy, "AU-2024-00001",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.RecordAutopsyFindingsAsync(
            causeOfDeath: "Acute myocardial infarction",
            underlyingCauseOfDeath: "Coronary artery disease",
            mannerOfDeath: MannerOfDeath.Natural,
            toxicologyFindings: "No significant toxicological substances detected.",
            bodyWeightKg: 82.5m,
            neuropathologyFindings: "Brain weight 1380g. No acute lesions.");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.CauseOfDeath, Is.EqualTo("Acute myocardial infarction"));
        Assert.That(state.UnderlyingCauseOfDeath, Is.EqualTo("Coronary artery disease"));
        Assert.That(state.MannerOfDeath, Is.EqualTo(MannerOfDeath.Natural));
        Assert.That(state.ToxicologyFindings, Does.Contain("No significant"));
        Assert.That(state.BodyWeightKg, Is.EqualTo(82.5m));
        Assert.That(state.NeuropathologyFindings, Does.Contain("Brain weight 1380g"));
    }

    [Test]
    public async Task CaseGrain_RecordAutopsyFindings_HomicideManner()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-091", APCaseType.Autopsy, "AU-2024-00002",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.RecordAutopsyFindingsAsync(
            "Gunshot wound to thorax", "Gunshot wound",
            MannerOfDeath.Homicide, null, null, null);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.MannerOfDeath, Is.EqualTo(MannerOfDeath.Homicide));
    }

    // ─── Comments ─────────────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_AddComments_StoresText()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-100", APCaseType.SurgicalPathology, "SP-2024-00100",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.AddCommentsAsync("Slide reviewed with attending pathologist.");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Comments, Is.EqualTo("Slide reviewed with attending pathologist."));
    }

    // ─── Cancellation ─────────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_CancelCase_TransitionsToCancelled()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-110", APCaseType.SurgicalPathology, "SP-2024-00110",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await grain.CancelCaseAsync("Duplicate accession — see SP-2024-00109.");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Cancelled));
        Assert.That(state.Comments, Does.Contain("CANCELLED"));
        Assert.That(state.Comments, Does.Contain("Duplicate accession"));
    }

    [Test]
    public async Task CaseGrain_Cancel_AppendsToExistingComments()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        await grain.AccessionCaseAsync(
            "PAT-111", APCaseType.SurgicalPathology, "SP-2024-00111",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await grain.AddCommentsAsync("Specimen submitted in formalin.");

        await grain.CancelCaseAsync("Unacceptable specimen — improperly fixed.");

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.Comments, Does.Contain("Specimen submitted in formalin."));
        Assert.That(state.Comments, Does.Contain("CANCELLED"));
    }

    // ─── LastModifiedDate ─────────────────────────────────────────────────────

    [Test]
    public async Task CaseGrain_LastModifiedDate_UpdatedOnWrite()
    {
        IAnatomicPathologyCaseGrain grain = NewCase();
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        await grain.AccessionCaseAsync(
            "PAT-120", APCaseType.Cytology, "CY-2024-00020",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        AnatomicPathologyState state = await grain.GetCaseAsync();

        Assert.That(state.LastModifiedDate, Is.GreaterThanOrEqualTo(before));
        Assert.That(state.CreatedDate, Is.GreaterThanOrEqualTo(before));
    }
}

/// <summary>
/// Unit tests for the AP case index grain in isolation.
/// </summary>
[TestFixture]
public class AnatomicPathologyCaseIndexGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IAnatomicPathologyCaseIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IAnatomicPathologyCaseIndexGrain>(
            $"AP-CASE-IDX:{Guid.NewGuid()}");

    private static APCaseIndexEntry MakeEntry(
        string caseId,
        APCaseType type = APCaseType.SurgicalPathology,
        APCaseStatus status = APCaseStatus.Received,
        DateTime? dateReceived = null,
        string? diagnosis = null) => new()
    {
        CaseId          = caseId,
        AccessionNumber = $"SP-{Guid.NewGuid():N}",
        CaseType        = type,
        Status          = status,
        DateReceived    = dateReceived ?? DateTime.UtcNow,
        PrimaryDiagnosis = diagnosis,
        PathologistName  = null
    };

    [Test]
    public async Task IndexGrain_EmptyIndex_ReturnsEmptyList()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();

        List<APCaseIndexEntry> all = await index.GetAllCasesAsync();

        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task IndexGrain_UpsertCase_AppearsInGetAll()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();
        string caseId = $"AP-CASE:{Guid.NewGuid()}";

        await index.UpsertCaseAsync(MakeEntry(caseId, APCaseType.SurgicalPathology));

        List<APCaseIndexEntry> all = await index.GetAllCasesAsync();

        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].CaseId, Is.EqualTo(caseId));
    }

    [Test]
    public async Task IndexGrain_UpsertCase_UpdatesExistingEntry()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();
        string caseId = $"AP-CASE:{Guid.NewGuid()}";

        await index.UpsertCaseAsync(MakeEntry(caseId, status: APCaseStatus.Received));
        await index.UpsertCaseAsync(MakeEntry(caseId, status: APCaseStatus.Final, diagnosis: "Benign."));

        List<APCaseIndexEntry> all = await index.GetAllCasesAsync();

        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(APCaseStatus.Final));
        Assert.That(all[0].PrimaryDiagnosis, Is.EqualTo("Benign."));
    }

    [Test]
    public async Task IndexGrain_GetCasesByType_FiltersByCaseType()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();

        await index.UpsertCaseAsync(MakeEntry($"AP-CASE:{Guid.NewGuid()}", APCaseType.SurgicalPathology));
        await index.UpsertCaseAsync(MakeEntry($"AP-CASE:{Guid.NewGuid()}", APCaseType.SurgicalPathology));
        await index.UpsertCaseAsync(MakeEntry($"AP-CASE:{Guid.NewGuid()}", APCaseType.Cytology));
        await index.UpsertCaseAsync(MakeEntry($"AP-CASE:{Guid.NewGuid()}", APCaseType.Autopsy));

        List<APCaseIndexEntry> spCases = await index.GetCasesByTypeAsync(APCaseType.SurgicalPathology);
        List<APCaseIndexEntry> cyCases = await index.GetCasesByTypeAsync(APCaseType.Cytology);
        List<APCaseIndexEntry> auCases = await index.GetCasesByTypeAsync(APCaseType.Autopsy);

        Assert.That(spCases, Has.Count.EqualTo(2));
        Assert.That(cyCases, Has.Count.EqualTo(1));
        Assert.That(auCases, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IndexGrain_GetCasesByType_EmptyForUnusedType()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();
        await index.UpsertCaseAsync(MakeEntry($"AP-CASE:{Guid.NewGuid()}", APCaseType.SurgicalPathology));

        List<APCaseIndexEntry> auCases = await index.GetCasesByTypeAsync(APCaseType.Autopsy);

        Assert.That(auCases, Is.Empty);
    }

    [Test]
    public async Task IndexGrain_RemoveCase_RemovesFromIndex()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();
        string caseId1 = $"AP-CASE:{Guid.NewGuid()}";
        string caseId2 = $"AP-CASE:{Guid.NewGuid()}";

        await index.UpsertCaseAsync(MakeEntry(caseId1));
        await index.UpsertCaseAsync(MakeEntry(caseId2));

        await index.RemoveCaseAsync(caseId1);

        List<APCaseIndexEntry> all = await index.GetAllCasesAsync();

        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].CaseId, Is.EqualTo(caseId2));
    }

    [Test]
    public async Task IndexGrain_RemoveNonExistentCase_IsIdempotent()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();
        string caseId = $"AP-CASE:{Guid.NewGuid()}";
        await index.UpsertCaseAsync(MakeEntry(caseId));

        // Remove a case that doesn't exist — should not throw
        Assert.DoesNotThrowAsync(() =>
            index.RemoveCaseAsync($"AP-CASE:{Guid.NewGuid()}"));

        List<APCaseIndexEntry> all = await index.GetAllCasesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IndexGrain_GetAllCases_OrderedByDateReceivedDescending()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();
        DateTime oldest  = DateTime.UtcNow.AddDays(-30);
        DateTime middle  = DateTime.UtcNow.AddDays(-15);
        DateTime newest  = DateTime.UtcNow;

        string oldId = $"AP-CASE:{Guid.NewGuid()}";
        string midId = $"AP-CASE:{Guid.NewGuid()}";
        string newId = $"AP-CASE:{Guid.NewGuid()}";

        // Add out of order
        await index.UpsertCaseAsync(MakeEntry(midId, dateReceived: middle));
        await index.UpsertCaseAsync(MakeEntry(oldId, dateReceived: oldest));
        await index.UpsertCaseAsync(MakeEntry(newId, dateReceived: newest));

        List<APCaseIndexEntry> all = await index.GetAllCasesAsync();

        Assert.That(all[0].CaseId, Is.EqualTo(newId));
        Assert.That(all[1].CaseId, Is.EqualTo(midId));
        Assert.That(all[2].CaseId, Is.EqualTo(oldId));
    }

    [Test]
    public async Task IndexGrain_MultipleCasesOfSameType_AllReturned()
    {
        IAnatomicPathologyCaseIndexGrain index = NewIndex();

        for (int i = 0; i < 5; i++)
        {
            await index.UpsertCaseAsync(
                MakeEntry($"AP-CASE:{Guid.NewGuid()}", APCaseType.Cytology));
        }

        List<APCaseIndexEntry> cyCases = await index.GetCasesByTypeAsync(APCaseType.Cytology);

        Assert.That(cyCases, Has.Count.EqualTo(5));
    }
}
