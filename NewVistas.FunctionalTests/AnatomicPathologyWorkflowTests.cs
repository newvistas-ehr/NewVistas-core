// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Anatomic Pathology — Files #63.08 (SP), #63.09 (CY), #63.19 (AU).
/// Tests full case lifecycles through IPatientWorkflowGrain, verifying cross-grain orchestration
/// (case grain ↔ index grain) and all status transitions.
/// MUMPS routines: LRAP.m, LRAPSC.m, LRAPACC.m, LRAPAU.m
/// </summary>
[TestFixture]
public class AnatomicPathologyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private static string NewPatient() => $"PATIENT-AP-{Guid.NewGuid()}";

    // ─── Accession ────────────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_AccessionSPCase_ReturnsNonEmptyCaseId()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-10001",
            "Right kidney", "Partial nephrectomy specimen", "Excision",
            "Renal mass on CT", "r/o renal cell carcinoma",
            "PROV-001", "Dr. Ordering", "Urology OR",
            DateTime.UtcNow.AddHours(-3), DateTime.UtcNow);

        Assert.That(caseId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Workflow_AccessionSPCase_CaseIdHasCorrectPrefix()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-10002",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        Assert.That(caseId, Does.StartWith("AP-CASE:"));
    }

    [Test]
    public async Task Workflow_AccessionedCase_AppearsInPatientIndex()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-10003",
            "Colon, sigmoid", "Sigmoid colectomy", "Resection",
            "Colon adenocarcinoma on colonoscopy", null,
            "PROV-001", "Dr. Surgeon", "Surgery OR",
            null, DateTime.UtcNow);

        List<APCaseIndexEntry> cases = await wf.GetAPCasesAsync();

        Assert.That(cases, Has.Count.EqualTo(1));
        Assert.That(cases[0].CaseId, Is.EqualTo(caseId));
        Assert.That(cases[0].AccessionNumber, Is.EqualTo("SP-2024-10003"));
        Assert.That(cases[0].CaseType, Is.EqualTo(APCaseType.SurgicalPathology));
        Assert.That(cases[0].Status, Is.EqualTo(APCaseStatus.Received));
        Assert.That(cases[0].SpecimenSource, Is.EqualTo("Colon, sigmoid"));
    }

    [Test]
    public async Task Workflow_MultipleAccessions_AllAppearInIndex()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        await wf.AccessionAPCaseAsync(APCaseType.SurgicalPathology, "SP-2024-10010",
            "Skin, left cheek", null, "Excision", null, null, null, null, null, null, DateTime.UtcNow);
        await wf.AccessionAPCaseAsync(APCaseType.Cytology, "CY-2024-10010",
            "Cervix", null, "Smear", null, null, null, null, null, null, DateTime.UtcNow);
        await wf.AccessionAPCaseAsync(APCaseType.SurgicalPathology, "SP-2024-10011",
            "Prostate", null, "Biopsy", null, null, null, null, null, null, DateTime.UtcNow);

        List<APCaseIndexEntry> cases = await wf.GetAPCasesAsync();

        Assert.That(cases, Has.Count.EqualTo(3));
    }

    // ─── Get Case Detail ──────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_GetAPCase_ReturnsFullState()
    {
        string patientId = NewPatient();
        IPatientWorkflowGrain wf = Workflow(patientId);

        DateTime received = DateTime.UtcNow;
        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-10020",
            "Thyroid, right lobe", "Right thyroid lobectomy", "Excision",
            "Thyroid nodule", "Thyroid neoplasm",
            "PROV-001", "Dr. Surgeon", "OR Suite 4",
            received.AddHours(-1), received);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.CaseId, Is.EqualTo(caseId));
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.AccessionNumber, Is.EqualTo("SP-2024-10020"));
        Assert.That(state.SpecimenSource, Is.EqualTo("Thyroid, right lobe"));
        Assert.That(state.SpecimenDescription, Is.EqualTo("Right thyroid lobectomy"));
        Assert.That(state.ClinicalHistory, Is.EqualTo("Thyroid nodule"));
        Assert.That(state.ClinicalDiagnosis, Is.EqualTo("Thyroid neoplasm"));
        Assert.That(state.ReferringProviderName, Is.EqualTo("Dr. Surgeon"));
        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Received));
    }

    // ─── Filter by Type ───────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_GetAPCasesByType_ReturnsOnlyMatchingType()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        await wf.AccessionAPCaseAsync(APCaseType.SurgicalPathology, "SP-2024-10030",
            "Gallbladder", null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await wf.AccessionAPCaseAsync(APCaseType.Cytology, "CY-2024-10030",
            "Cervix", null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await wf.AccessionAPCaseAsync(APCaseType.Autopsy, "AU-2024-10030",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await wf.AccessionAPCaseAsync(APCaseType.SurgicalPathology, "SP-2024-10031",
            "Appendix", null, null, null, null, null, null, null, null, DateTime.UtcNow);

        List<APCaseIndexEntry> spCases = await wf.GetAPCasesByTypeAsync(APCaseType.SurgicalPathology);
        List<APCaseIndexEntry> cyCases = await wf.GetAPCasesByTypeAsync(APCaseType.Cytology);
        List<APCaseIndexEntry> auCases = await wf.GetAPCasesByTypeAsync(APCaseType.Autopsy);

        Assert.That(spCases, Has.Count.EqualTo(2));
        Assert.That(cyCases, Has.Count.EqualTo(1));
        Assert.That(auCases, Has.Count.EqualTo(1));

        Assert.That(spCases.All(c => c.CaseType == APCaseType.SurgicalPathology), Is.True);
        Assert.That(cyCases[0].AccessionNumber, Is.EqualTo("CY-2024-10030"));
    }

    // ─── Surgical Pathology — Full Lifecycle ──────────────────────────────────

    [Test]
    public async Task Workflow_SPCase_FullLifecycle_ReceivedToFinal()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        // 1 — Accession
        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-20001",
            "Prostate, radical prostatectomy", "Prostate gland + seminal vesicles",
            "Excision", "PSA rising, biopsy positive for carcinoma", null,
            "PROV-001", "Dr. Surgeon", "OR Suite 1",
            DateTime.UtcNow.AddHours(-6), DateTime.UtcNow.AddHours(-4));

        // 2 — Gross description
        await wf.RecordAPGrossDescriptionAsync(
            caseId,
            "Prostate gland weighing 52 grams, 4.0 x 3.8 x 3.5 cm. External surface inked. " +
            "Sections show a firm, tan-white mass in the right posterior zone, 1.8 x 1.5 cm.",
            "PATH-001", "Dr. Pathologist",
            specimenPartCount: 1, specimenWeightGrams: 52m,
            frozenSectionDiagnosis: null);

        AnatomicPathologyState afterGross = await wf.GetAPCaseAsync(caseId);
        Assert.That(afterGross.Status, Is.EqualTo(APCaseStatus.InProgress));

        // 3 — Microscopic description
        await wf.RecordAPMicroscopicDescriptionAsync(
            caseId,
            "Sections show prostatic adenocarcinoma with Gleason pattern 4+3. " +
            "Perineural invasion is present. Seminal vesicles are free of tumour. " +
            "Surgical margins are clear.");

        // 4 — Sign out
        DateTime signOut = DateTime.UtcNow;
        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "Prostatic adenocarcinoma, Gleason score 7 (4+3 = 7), pT2c N0 Mx. " +
            "Margins negative. Perineural invasion present.",
            new List<string> { "C61", "Z80.42" },
            "PATH-001", "Dr. Pathologist", signOut);

        AnatomicPathologyState finalState = await wf.GetAPCaseAsync(caseId);

        Assert.That(finalState.Status, Is.EqualTo(APCaseStatus.Final));
        Assert.That(finalState.Diagnosis, Does.Contain("Gleason score 7"));
        Assert.That(finalState.DiagnosisCodes, Contains.Item("C61"));
        Assert.That(finalState.PathologistName, Is.EqualTo("Dr. Pathologist"));
        Assert.That(finalState.DateReported, Is.Not.Null);
        Assert.That(finalState.GrossDescription, Does.Contain("52 grams"));
        Assert.That(finalState.MicroscopicDescription, Does.Contain("Gleason pattern 4+3"));
    }

    [Test]
    public async Task Workflow_SPCase_IndexUpdated_AfterSignOut()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-20010",
            "Breast, left", "Core needle biopsy x3",
            "Biopsy", "Suspicious mass on mammogram", null,
            null, null, null, null, DateTime.UtcNow);

        await wf.RecordAPGrossDescriptionAsync(
            caseId, "Three tan-white cores, each 1.2 cm in length.",
            "PATH-001", "Dr. Pathologist", 3, null, null);

        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "Invasive ductal carcinoma, grade 2.",
            new List<string> { "C50.912" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        List<APCaseIndexEntry> cases = await wf.GetAPCasesAsync();

        Assert.That(cases, Has.Count.EqualTo(1));
        Assert.That(cases[0].Status, Is.EqualTo(APCaseStatus.Final));
        Assert.That(cases[0].PrimaryDiagnosis, Does.Contain("Invasive ductal carcinoma"));
        Assert.That(cases[0].PathologistName, Is.EqualTo("Dr. Pathologist"));
        Assert.That(cases[0].DateReported, Is.Not.Null);
    }

    // ─── Preliminary → Final Transition ──────────────────────────────────────

    [Test]
    public async Task Workflow_PreliminaryToFinal_CorrectStatusTransitions()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-20020",
            "Liver, right lobe", "Core biopsy",
            "Biopsy", "Liver mass", null,
            null, null, null, null, DateTime.UtcNow);

        // Preliminary
        await wf.IssueAPPreliminaryDiagnosisAsync(
            caseId,
            "Hepatocellular carcinoma, preliminary — pending IHC.",
            "PATH-001", "Dr. Pathologist");

        AnatomicPathologyState afterPrelim = await wf.GetAPCaseAsync(caseId);
        Assert.That(afterPrelim.Status, Is.EqualTo(APCaseStatus.Preliminary));

        // Index reflects preliminary status
        List<APCaseIndexEntry> casesAfterPrelim = await wf.GetAPCasesAsync();
        Assert.That(casesAfterPrelim[0].Status, Is.EqualTo(APCaseStatus.Preliminary));

        // Final
        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "Hepatocellular carcinoma, moderately differentiated. CK7(-), CK20(-), Hepar-1(+).",
            new List<string> { "C22.0" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        AnatomicPathologyState afterFinal = await wf.GetAPCaseAsync(caseId);
        Assert.That(afterFinal.Status, Is.EqualTo(APCaseStatus.Final));
    }

    // ─── Addendum ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_SPCase_WithAddendum_StatusBecomesAddendum()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-20030",
            "Thyroid, right lobe", "Thyroid lobectomy", "Excision",
            "Thyroid nodule", null, null, null, null, null, DateTime.UtcNow);

        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "Papillary thyroid carcinoma, classic variant, 1.4 cm.",
            new List<string> { "C73" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        // Addendum after molecular testing
        await wf.AddAPAddendumAsync(
            caseId,
            "ADDENDUM: BRAF V600E mutation confirmed by PCR. " +
            "RET/PTC rearrangement: negative.",
            "PATH-001", "Dr. Pathologist");

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Addendum));
        Assert.That(state.Addendum, Does.Contain("BRAF V600E"));
        Assert.That(state.AddendumPathologistName, Is.EqualTo("Dr. Pathologist"));
        Assert.That(state.AddendumDate, Is.Not.Null);
        // Original diagnosis preserved
        Assert.That(state.Diagnosis, Does.Contain("Papillary thyroid carcinoma"));
    }

    [Test]
    public async Task Workflow_AddAddendum_IndexStatusUpdated()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-20031",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        await wf.SignOutAPDiagnosisAsync(
            caseId, "Benign tissue.",
            new List<string>(), "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        await wf.AddAPAddendumAsync(
            caseId, "ADDENDUM: Additional stains reviewed, no change to diagnosis.",
            "PATH-001", "Dr. Pathologist");

        List<APCaseIndexEntry> cases = await wf.GetAPCasesAsync();

        Assert.That(cases[0].Status, Is.EqualTo(APCaseStatus.Addendum));
    }

    // ─── Amendment ────────────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_AmendedDiagnosis_ReplacesOriginal()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-20040",
            "Lymph node, inguinal", "Excisional biopsy", "Excision",
            "Lymphadenopathy", null, null, null, null, null, DateTime.UtcNow);

        // Original sign-out (incorrect)
        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "Reactive lymphadenopathy.",
            new List<string> { "R59.1" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        // Amendment after flow cytometry returns
        await wf.AmendAPDiagnosisAsync(
            caseId,
            "AMENDED: Follicular lymphoma, grade 1-2, FL1-2.",
            new List<string> { "C82.00" },
            "Flow cytometry returned after sign-out: CD10+, CD20+, BCL2+. " +
            "Consistent with follicular lymphoma.",
            "PATH-001", "Dr. Pathologist");

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Amended));
        Assert.That(state.Diagnosis, Does.Contain("Follicular lymphoma"));
        Assert.That(state.DiagnosisCodes, Contains.Item("C82.00"));
        Assert.That(state.DiagnosisCodes, Does.Not.Contain("R59.1"));
        Assert.That(state.AmendmentReason, Does.Contain("Flow cytometry"));
    }

    [Test]
    public async Task Workflow_Amend_IndexStatusUpdated()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-20041",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await wf.SignOutAPDiagnosisAsync(
            caseId, "Reactive change.", new List<string>(),
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        await wf.AmendAPDiagnosisAsync(
            caseId, "AMENDED: Mantle cell lymphoma.",
            new List<string> { "C83.10" },
            "Cyclin D1 positive on repeat IHC.",
            "PATH-001", "Dr. Pathologist");

        List<APCaseIndexEntry> cases = await wf.GetAPCasesAsync();

        Assert.That(cases[0].Status, Is.EqualTo(APCaseStatus.Amended));
        Assert.That(cases[0].PrimaryDiagnosis, Does.Contain("Mantle cell lymphoma"));
    }

    // ─── Cytology Workflow ────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_CytologyCase_RecordsBethesdaCategory()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.Cytology, "CY-2024-20001",
            "Cervix", "ThinPrep PAP smear", "Smear",
            "Annual screening", null,
            "PROV-001", "Dr. OB/GYN", "Gyn Clinic",
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow);

        await wf.RecordAPCytologyDetailsAsync(
            caseId,
            "NILM — Negative for Intraepithelial Lesion or Malignancy",
            "Satisfactory for evaluation — transformation zone cells present");

        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "NILM. No significant findings.",
            new List<string>(),
            "PATH-001", "Dr. Cytologist", DateTime.UtcNow);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.CaseType, Is.EqualTo(APCaseType.Cytology));
        Assert.That(state.BethesdaCategory, Does.Contain("NILM"));
        Assert.That(state.SpecimenAdequacy, Does.Contain("Satisfactory"));
        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Final));
    }

    [Test]
    public async Task Workflow_CytologyCase_ASCUS_HighGrade()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.Cytology, "CY-2024-20002",
            "Cervix", "PAP smear", "Smear",
            "Abnormal bleeding", null, null, null, null,
            null, DateTime.UtcNow);

        await wf.RecordAPCytologyDetailsAsync(
            caseId,
            "HSIL — High-grade Squamous Intraepithelial Lesion",
            "Satisfactory for evaluation");

        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "HSIL (CIN2-3). Recommend colposcopy.",
            new List<string> { "N87.1" },
            "PATH-001", "Dr. Cytologist", DateTime.UtcNow);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.BethesdaCategory, Does.Contain("HSIL"));
        Assert.That(state.DiagnosisCodes, Contains.Item("N87.1"));
    }

    // ─── Autopsy Workflow ─────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_AutopsyCase_RecordsDeathFindings()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.Autopsy, "AU-2024-10001",
            null, null, null,
            "71 y/o male, found unresponsive at home. History of CAD.", null,
            null, null, null, null, DateTime.UtcNow);

        // Record gross + microscopic
        await wf.RecordAPGrossDescriptionAsync(
            caseId,
            "Well-nourished adult male. Body weight 88 kg. Heart weight 520g. " +
            "Left anterior descending artery shows 95% stenosis at mid-segment.",
            "PATH-001", "Dr. Medical Examiner",
            null, null, null);

        // Record autopsy findings
        await wf.RecordAPAutopsyFindingsAsync(
            caseId,
            causeOfDeath: "Acute myocardial infarction",
            underlyingCauseOfDeath: "Atherosclerotic coronary artery disease",
            mannerOfDeath: MannerOfDeath.Natural,
            toxicologyFindings: "No alcohol or illicit substances detected. " +
                                "Aspirin and atorvastatin at therapeutic levels.",
            bodyWeightKg: 88m,
            neuropathologyFindings: "Brain weight 1350g. No acute intracranial pathology.");

        // Sign out
        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "1. Acute myocardial infarction, anterior wall.\n" +
            "2. Atherosclerotic coronary artery disease, severe, LAD 95% stenosis.\n" +
            "3. Cardiomegaly (520g).",
            new List<string> { "I21.09", "I25.10" },
            "PATH-001", "Dr. Medical Examiner", DateTime.UtcNow);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.CaseType, Is.EqualTo(APCaseType.Autopsy));
        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Final));
        Assert.That(state.CauseOfDeath, Is.EqualTo("Acute myocardial infarction"));
        Assert.That(state.UnderlyingCauseOfDeath, Does.Contain("Atherosclerotic"));
        Assert.That(state.MannerOfDeath, Is.EqualTo(MannerOfDeath.Natural));
        Assert.That(state.ToxicologyFindings, Does.Contain("No alcohol"));
        Assert.That(state.BodyWeightKg, Is.EqualTo(88m));
        Assert.That(state.NeuropathologyFindings, Does.Contain("Brain weight 1350g"));
        Assert.That(state.DiagnosisCodes, Contains.Item("I21.09"));
    }

    [Test]
    public async Task Workflow_AutopsyCase_MannerOfDeath_Homicide()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.Autopsy, "AU-2024-10002",
            null, null, null, "GSW victim", null,
            null, null, null, null, DateTime.UtcNow);

        await wf.RecordAPAutopsyFindingsAsync(
            caseId,
            "Gunshot wound to thorax with cardiac involvement",
            "Penetrating ballistic injury",
            MannerOfDeath.Homicide,
            "No significant substances",
            null, null);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.MannerOfDeath, Is.EqualTo(MannerOfDeath.Homicide));
    }

    // ─── Gross Description — Supplemental Studies ─────────────────────────────

    [Test]
    public async Task Workflow_RecordGrossDescription_StatusBecomesInProgress()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-30001",
            "Appendix", "Appendectomy specimen", "Excision",
            "Acute appendicitis", null, null, null, null, null, DateTime.UtcNow);

        await wf.RecordAPGrossDescriptionAsync(
            caseId,
            "Appendix 8.5 cm in length, 0.9 cm diameter. Serosa is erythematous with fibrinous exudate.",
            "PATH-001", "Dr. Pathologist",
            specimenPartCount: 1, specimenWeightGrams: 12m,
            frozenSectionDiagnosis: null);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.Status, Is.EqualTo(APCaseStatus.InProgress));
        Assert.That(state.SpecimenPartCount, Is.EqualTo(1));
        Assert.That(state.SpecimenWeightGrams, Is.EqualTo(12m));
    }

    [Test]
    public async Task Workflow_FrozenSection_PreservesIntraOpDiagnosis()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-30002",
            "Breast, left", "Lumpectomy specimen", "Excision",
            "Invasive carcinoma on core biopsy", null,
            null, null, null, null, DateTime.UtcNow);

        await wf.RecordAPGrossDescriptionAsync(
            caseId,
            "Fibrofatty tissue 4.5 x 3.2 x 2.8 cm with a firm white area 2.1 cm.",
            "PATH-001", "Dr. Pathologist",
            1, 38m,
            frozenSectionDiagnosis: "Invasive carcinoma — margins appear close");

        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "Invasive ductal carcinoma, grade 2. Margins: closest 1mm inferiorly.",
            new List<string> { "C50.912" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.FrozenSectionDiagnosis, Is.EqualTo("Invasive carcinoma — margins appear close"));
        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Final));
    }

    // ─── Patient Isolation ────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_DifferentPatients_IndicesAreIsolated()
    {
        IPatientWorkflowGrain wf1 = Workflow(NewPatient());
        IPatientWorkflowGrain wf2 = Workflow(NewPatient());

        await wf1.AccessionAPCaseAsync(APCaseType.SurgicalPathology, "SP-A1",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await wf1.AccessionAPCaseAsync(APCaseType.SurgicalPathology, "SP-A2",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);
        await wf2.AccessionAPCaseAsync(APCaseType.Cytology, "CY-B1",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        List<APCaseIndexEntry> casesForP1 = await wf1.GetAPCasesAsync();
        List<APCaseIndexEntry> casesForP2 = await wf2.GetAPCasesAsync();

        Assert.That(casesForP1, Has.Count.EqualTo(2));
        Assert.That(casesForP2, Has.Count.EqualTo(1));
        Assert.That(casesForP1.All(c => c.CaseType == APCaseType.SurgicalPathology), Is.True);
        Assert.That(casesForP2[0].CaseType, Is.EqualTo(APCaseType.Cytology));
    }

    // ─── Empty Patient ─────────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_GetAPCases_ReturnsEmptyForNewPatient()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        List<APCaseIndexEntry> cases = await wf.GetAPCasesAsync();

        Assert.That(cases, Is.Empty);
    }

    [Test]
    public async Task Workflow_GetAPCasesByType_ReturnsEmptyForNewPatient()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        List<APCaseIndexEntry> spCases = await wf.GetAPCasesByTypeAsync(APCaseType.SurgicalPathology);

        Assert.That(spCases, Is.Empty);
    }

    // ─── Microscopic Only (No Gross) ──────────────────────────────────────────

    [Test]
    public async Task Workflow_RecordMicroscopic_WithoutGross_DoesNotChangeStatus()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-40001",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        // Record micro directly (some labs receive outside slides without a gross)
        await wf.RecordAPMicroscopicDescriptionAsync(
            caseId,
            "Sections of submitted material show squamous epithelium with full-thickness atypia.");

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        // Status should not auto-transition to InProgress (only gross does that)
        Assert.That(state.MicroscopicDescription, Does.Contain("squamous epithelium"));
        Assert.That(state.Status, Is.EqualTo(APCaseStatus.Received));
    }

    // ─── Cross-Case Index Ordering ────────────────────────────────────────────

    [Test]
    public async Task Workflow_Cases_OrderedMostRecentFirst()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        // Accession three cases at different times (using separate received dates)
        string caseId1 = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-50001", null, null, null,
            null, null, null, null, null, null,
            DateTime.UtcNow.AddDays(-30));  // oldest

        string caseId2 = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-50002", null, null, null,
            null, null, null, null, null, null,
            DateTime.UtcNow.AddDays(-10));  // middle

        string caseId3 = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-50003", null, null, null,
            null, null, null, null, null, null,
            DateTime.UtcNow);               // newest

        List<APCaseIndexEntry> cases = await wf.GetAPCasesAsync();

        Assert.That(cases, Has.Count.EqualTo(3));
        Assert.That(cases[0].CaseId, Is.EqualTo(caseId3));   // newest first
        Assert.That(cases[1].CaseId, Is.EqualTo(caseId2));
        Assert.That(cases[2].CaseId, Is.EqualTo(caseId1));   // oldest last
    }

    // ─── Diagnosis Codes ──────────────────────────────────────────────────────

    [Test]
    public async Task Workflow_SignOut_MultipleICD10Codes_AllStored()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-60001",
            "Colon, sigmoid", null, "Resection",
            null, null, null, null, null, null, DateTime.UtcNow);

        await wf.SignOutAPDiagnosisAsync(
            caseId,
            "Moderately differentiated adenocarcinoma of the sigmoid colon. " +
            "Incidental tubular adenoma present.",
            new List<string> { "C18.7", "D12.5", "Z80.0" },
            "PATH-001", "Dr. Pathologist", DateTime.UtcNow);

        AnatomicPathologyState state = await wf.GetAPCaseAsync(caseId);

        Assert.That(state.DiagnosisCodes, Has.Count.EqualTo(3));
        Assert.That(state.DiagnosisCodes, Contains.Item("C18.7"));
        Assert.That(state.DiagnosisCodes, Contains.Item("D12.5"));
        Assert.That(state.DiagnosisCodes, Contains.Item("Z80.0"));
    }

    [Test]
    public async Task Workflow_SignOut_EmptyCodeList_Allowed()
    {
        IPatientWorkflowGrain wf = Workflow(NewPatient());

        string caseId = await wf.AccessionAPCaseAsync(
            APCaseType.SurgicalPathology, "SP-2024-60002",
            null, null, null, null, null, null, null, null, null, DateTime.UtcNow);

        // Some cases may be signed out without ICD-10 codes (coded later)
        Assert.DoesNotThrowAsync(() =>
            wf.SignOutAPDiagnosisAsync(
                caseId, "Benign tissue.", new List<string>(),
                "PATH-001", "Dr. Pathologist", DateTime.UtcNow));
    }
}
