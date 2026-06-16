// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ═══════════════════════════════════════════════════════════════════════════
// OncologyTumorGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class OncologyTumorGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IOncologyTumorGrain NewTumor() =>
        _cluster.GrainFactory.GetGrain<IOncologyTumorGrain>($"ONC-TUMOR:{Guid.NewGuid()}");

    // ── Registration ─────────────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_CanRegisterTumor()
    {
        IOncologyTumorGrain grain = NewTumor();
        DateTime dxDate = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        await grain.RegisterTumorAsync(
            "PAT-001", "C34.1", "Upper lobe of lung", "8140/3", "Adenocarcinoma, NOS",
            TumorLaterality.Right, dxDate, DiagnosisBasis.HistologyOfPrimary,
            1, "ONCO-001", "Dr. Smith");

        OncologyTumorState state = await grain.GetTumorAsync();

        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PrimarySite, Is.EqualTo("C34.1"));
        Assert.That(state.PrimarySiteText, Is.EqualTo("Upper lobe of lung"));
        Assert.That(state.Histology, Is.EqualTo("8140/3"));
        Assert.That(state.HistologyText, Is.EqualTo("Adenocarcinoma, NOS"));
        Assert.That(state.Laterality, Is.EqualTo(TumorLaterality.Right));
        Assert.That(state.DateOfDiagnosis, Is.EqualTo(dxDate));
        Assert.That(state.DiagnosisBasis, Is.EqualTo(DiagnosisBasis.HistologyOfPrimary));
        Assert.That(state.SequenceNumber, Is.EqualTo(1));
        Assert.That(state.OncologistId, Is.EqualTo("ONCO-001"));
        Assert.That(state.OncologistName, Is.EqualTo("Dr. Smith"));
    }

    [Test]
    public async Task TumorGrain_DefaultStatus_IsActive()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-002", "C50.1", "Central breast", "8500/3", "Infiltrating duct carcinoma",
            TumorLaterality.Left, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary,
            1, null, null);

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.Active));
    }

    [Test]
    public async Task TumorGrain_TumorId_MatchesGrainKey()
    {
        string key = $"ONC-TUMOR:{Guid.NewGuid()}";
        IOncologyTumorGrain grain = _cluster.GrainFactory.GetGrain<IOncologyTumorGrain>(key);
        await grain.RegisterTumorAsync(
            "PAT-003", "C61", "Prostate gland", "8140/3", "Adenocarcinoma, NOS",
            TumorLaterality.NotApplicable, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary,
            1, null, null);

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.TumorId, Is.EqualTo(key));
    }

    // ── Staging ──────────────────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_RecordStaging_StoresAllTNMFields()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-004", "C34.1", "Lung", "8140/3", "Adenocarcinoma",
            TumorLaterality.Right, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        await grain.RecordStagingAsync("T2a", "N1", "M0", "T2a", "N0", "M0", "IIA", "3");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.ClinicalT, Is.EqualTo("T2a"));
        Assert.That(state.ClinicalN, Is.EqualTo("N1"));
        Assert.That(state.ClinicalM, Is.EqualTo("M0"));
        Assert.That(state.PathologicT, Is.EqualTo("T2a"));
        Assert.That(state.PathologicN, Is.EqualTo("N0"));
        Assert.That(state.PathologicM, Is.EqualTo("M0"));
        Assert.That(state.StageGroup, Is.EqualTo("IIA"));
        Assert.That(state.SeerSummaryStage, Is.EqualTo("3"));
    }

    [Test]
    public async Task TumorGrain_RecordStaging_SetsStagingDate()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-005", "C61", "Prostate", "8140/3", "Adenocarcinoma",
            TumorLaterality.NotApplicable, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        await grain.RecordStagingAsync("T3", "N0", "M0", null, null, null, "III", "4");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.StagingDate, Is.Not.Null);
        Assert.That(state.StagingDate!.Value, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task TumorGrain_RecordStaging_AllowsNullFields()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-006", "C50.9", "Breast", "8500/3", "IDC",
            TumorLaterality.Right, DateTime.UtcNow, DiagnosisBasis.ClinicalOnly, 1, null, null);

        await grain.RecordStagingAsync(null, null, null, "T1c", "N0", "M0", "I", "1");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.ClinicalT, Is.Null);
        Assert.That(state.PathologicT, Is.EqualTo("T1c"));
        Assert.That(state.StageGroup, Is.EqualTo("I"));
    }

    // ── Status Updates ───────────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_UpdateStatus_TransitionsToInRemission()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-007", "C34.1", "Lung", "8140/3", "Adenocarcinoma",
            TumorLaterality.Left, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        DateTime changeDate = DateTime.UtcNow;
        await grain.UpdateStatusAsync(OncologyStatus.InRemission, changeDate, "Post-surgical remission");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.InRemission));
        Assert.That(state.StatusChangeDate, Is.EqualTo(changeDate).Within(TimeSpan.FromSeconds(1)));
        Assert.That(state.Comments, Does.Contain("Post-surgical remission"));
    }

    [Test]
    public async Task TumorGrain_UpdateStatus_NullDateDefaultsToNow()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-008", "C61", "Prostate", "8140/3", "Adenocarcinoma",
            TumorLaterality.NotApplicable, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        await grain.UpdateStatusAsync(OncologyStatus.Deceased, null, null);

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.Deceased));
        Assert.That(state.StatusChangeDate, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task TumorGrain_UpdateStatus_AppendsNoteToExistingComments()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-009", "C50.1", "Breast", "8500/3", "IDC",
            TumorLaterality.Right, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);
        await grain.AddCommentAsync("Initial note");
        await grain.UpdateStatusAsync(OncologyStatus.InRemission, null, "Second note");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Comments, Does.Contain("Initial note"));
        Assert.That(state.Comments, Does.Contain("Second note"));
    }

    // ── Recurrence ───────────────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_RecordRecurrence_SetsStatusAndDate()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-010", "C34.1", "Lung", "8140/3", "Adenocarcinoma",
            TumorLaterality.Right, DateTime.UtcNow.AddYears(-2), DiagnosisBasis.HistologyOfPrimary, 1, null, null);
        await grain.UpdateStatusAsync(OncologyStatus.InRemission, null, null);

        DateTime recDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await grain.RecordRecurrenceAsync(recDate, "Liver", "Hepatic metastasis");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.Recurrence));
        Assert.That(state.RecurrenceDate, Is.EqualTo(recDate));
        Assert.That(state.RecurrenceSite, Is.EqualTo("Liver"));
        Assert.That(state.StatusChangeDate, Is.EqualTo(recDate));
        Assert.That(state.Comments, Does.Contain("Hepatic metastasis"));
    }

    [Test]
    public async Task TumorGrain_RecordRecurrence_NullSiteIsAllowed()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-011", "C61", "Prostate", "8140/3", "Adenocarcinoma",
            TumorLaterality.NotApplicable, DateTime.UtcNow.AddYears(-1), DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        await grain.RecordRecurrenceAsync(DateTime.UtcNow, null, null);

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.Recurrence));
        Assert.That(state.RecurrenceSite, Is.Null);
    }

    // ── Last Contact ─────────────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_RecordLastContact_StoresDateAndStatus()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-012", "C50.9", "Breast", "8500/3", "IDC",
            TumorLaterality.Left, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        DateTime contactDate = DateTime.UtcNow;
        await grain.RecordLastContactAsync(contactDate, OncologyStatus.InRemission);

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.DateOfLastContact, Is.EqualTo(contactDate).Within(TimeSpan.FromSeconds(1)));
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.InRemission));
    }

    // ── Treatment ID Tracking ────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_AddTreatmentId_AppearsInList()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-013", "C34.1", "Lung", "8140/3", "Adenocarcinoma",
            TumorLaterality.Right, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        await grain.AddTreatmentIdAsync("ONC-TX:tx1");
        await grain.AddTreatmentIdAsync("ONC-TX:tx2");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.TreatmentIds, Has.Count.EqualTo(2));
        Assert.That(state.TreatmentIds, Contains.Item("ONC-TX:tx1"));
        Assert.That(state.TreatmentIds, Contains.Item("ONC-TX:tx2"));
    }

    [Test]
    public async Task TumorGrain_AddTreatmentId_NoDuplicates()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-014", "C61", "Prostate", "8140/3", "Adenocarcinoma",
            TumorLaterality.NotApplicable, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        await grain.AddTreatmentIdAsync("ONC-TX:same");
        await grain.AddTreatmentIdAsync("ONC-TX:same");
        await grain.AddTreatmentIdAsync("ONC-TX:same");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.TreatmentIds, Has.Count.EqualTo(1));
    }

    // ── Comments ─────────────────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_AddComment_StoresText()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-015", "C50.1", "Breast", "8500/3", "IDC",
            TumorLaterality.Right, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        await grain.AddCommentAsync("Patient enrolled in clinical trial.");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Comments, Is.EqualTo("Patient enrolled in clinical trial."));
    }

    [Test]
    public async Task TumorGrain_AddComment_AppendsToExisting()
    {
        IOncologyTumorGrain grain = NewTumor();
        await grain.RegisterTumorAsync(
            "PAT-016", "C34.1", "Lung", "8140/3", "Adenocarcinoma",
            TumorLaterality.Left, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        await grain.AddCommentAsync("First comment.");
        await grain.AddCommentAsync("Second comment.");

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.Comments, Does.Contain("First comment."));
        Assert.That(state.Comments, Does.Contain("Second comment."));
    }

    // ── LastModifiedDate ─────────────────────────────────────────────────

    [Test]
    public async Task TumorGrain_LastModifiedDate_UpdatedOnEveryWrite()
    {
        IOncologyTumorGrain grain = NewTumor();
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        await grain.RegisterTumorAsync(
            "PAT-017", "C61", "Prostate", "8140/3", "Adenocarcinoma",
            TumorLaterality.NotApplicable, DateTime.UtcNow, DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        OncologyTumorState state = await grain.GetTumorAsync();
        Assert.That(state.LastModifiedDate, Is.GreaterThanOrEqualTo(before));
        Assert.That(state.CreatedDate, Is.GreaterThanOrEqualTo(before));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// OncologyTumorIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class OncologyTumorIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IOncologyTumorIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IOncologyTumorIndexGrain>($"ONC-TUMOR-IDX:{Guid.NewGuid()}");

    private static OncologyTumorIndexEntry MakeEntry(
        string tumorId,
        OncologyStatus status = OncologyStatus.Active,
        DateTime? dxDate = null) => new()
        {
            TumorId = tumorId,
            PrimarySite = "C34.1",
            PrimarySiteText = "Upper lobe of lung",
            Histology = "8140/3",
            HistologyText = "Adenocarcinoma",
            DateOfDiagnosis = dxDate ?? DateTime.UtcNow,
            Status = status,
            StageGroup = "IIA",
            SequenceNumber = 1,
            OncologistName = "Dr. Test"
        };

    [Test]
    public async Task IndexGrain_EmptyIndex_ReturnsEmptyList()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        List<OncologyTumorIndexEntry> all = await index.GetAllTumorsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task IndexGrain_UpsertTumor_AppearsInGetAll()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        string tumorId = $"ONC-TUMOR:{Guid.NewGuid()}";
        await index.UpsertTumorAsync(MakeEntry(tumorId));

        List<OncologyTumorIndexEntry> all = await index.GetAllTumorsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TumorId, Is.EqualTo(tumorId));
    }

    [Test]
    public async Task IndexGrain_UpsertTumor_UpdatesExistingEntry()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        string tumorId = $"ONC-TUMOR:{Guid.NewGuid()}";
        await index.UpsertTumorAsync(MakeEntry(tumorId, OncologyStatus.Active));
        await index.UpsertTumorAsync(MakeEntry(tumorId, OncologyStatus.InRemission));

        List<OncologyTumorIndexEntry> all = await index.GetAllTumorsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(OncologyStatus.InRemission));
    }

    [Test]
    public async Task IndexGrain_GetActiveTumors_FiltersCorrectly()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.Active));
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.InRemission));
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.Recurrence));
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.Deceased));

        List<OncologyTumorIndexEntry> active = await index.GetActiveTumorsAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(t => t.Status == OncologyStatus.Active || t.Status == OncologyStatus.Recurrence), Is.True);
    }

    [Test]
    public async Task IndexGrain_RemoveTumor_RemovesFromIndex()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        string tumorId = $"ONC-TUMOR:{Guid.NewGuid()}";
        await index.UpsertTumorAsync(MakeEntry(tumorId));
        await index.RemoveTumorAsync(tumorId);

        List<OncologyTumorIndexEntry> all = await index.GetAllTumorsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task IndexGrain_RemoveNonExistentTumor_IsIdempotent()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        string tumorId = $"ONC-TUMOR:{Guid.NewGuid()}";
        await index.UpsertTumorAsync(MakeEntry(tumorId));

        Assert.DoesNotThrowAsync(() => index.RemoveTumorAsync($"ONC-TUMOR:{Guid.NewGuid()}"));

        List<OncologyTumorIndexEntry> all = await index.GetAllTumorsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IndexGrain_GetAllTumors_OrderedByDiagnosisDateDescending()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        DateTime older = DateTime.UtcNow.AddYears(-3);
        DateTime newer = DateTime.UtcNow.AddYears(-1);
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.InRemission, older));
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.Active, newer));

        List<OncologyTumorIndexEntry> all = await index.GetAllTumorsAsync();
        Assert.That(all[0].DateOfDiagnosis, Is.EqualTo(newer).Within(TimeSpan.FromSeconds(1)));
        Assert.That(all[1].DateOfDiagnosis, Is.EqualTo(older).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task IndexGrain_GetActiveTumors_EmptyWhenNoneActive()
    {
        IOncologyTumorIndexGrain index = NewIndex();
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.Deceased));
        await index.UpsertTumorAsync(MakeEntry($"ONC-TUMOR:{Guid.NewGuid()}", OncologyStatus.LostToFollowUp));

        List<OncologyTumorIndexEntry> active = await index.GetActiveTumorsAsync();
        Assert.That(active, Is.Empty);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// OncologyTreatmentGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class OncologyTreatmentGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IOncologyTreatmentGrain NewTreatment() =>
        _cluster.GrainFactory.GetGrain<IOncologyTreatmentGrain>($"ONC-TX:{Guid.NewGuid()}");

    // ── Create ────────────────────────────────────────────────────────────

    [Test]
    public async Task TreatmentGrain_CanCreateTreatment()
    {
        IOncologyTreatmentGrain grain = NewTreatment();

        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t1", "PAT-001", OncologyTreatmentType.Chemotherapy,
            "FOLFOX", "85 mg/m² q14d", "PROV-001", "Dr. Jones",
            "VA Medical Center", "6 cycles planned");

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.TumorId, Is.EqualTo("ONC-TUMOR:t1"));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.TreatmentType, Is.EqualTo(OncologyTreatmentType.Chemotherapy));
        Assert.That(state.AgentName, Is.EqualTo("FOLFOX"));
        Assert.That(state.DoseDescription, Is.EqualTo("85 mg/m² q14d"));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Jones"));
        Assert.That(state.FacilityName, Is.EqualTo("VA Medical Center"));
    }

    [Test]
    public async Task TreatmentGrain_DefaultStatus_IsPlanned()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t2", "PAT-002", OncologyTreatmentType.Radiation,
            "IMRT", null, null, null, null, null);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Planned));
    }

    [Test]
    public async Task TreatmentGrain_TreatmentId_MatchesGrainKey()
    {
        string key = $"ONC-TX:{Guid.NewGuid()}";
        IOncologyTreatmentGrain grain = _cluster.GrainFactory.GetGrain<IOncologyTreatmentGrain>(key);
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t3", "PAT-003", OncologyTreatmentType.Immunotherapy,
            "Pembrolizumab", null, null, null, null, null);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.TreatmentId, Is.EqualTo(key));
    }

    // ── Start ─────────────────────────────────────────────────────────────

    [Test]
    public async Task TreatmentGrain_StartTreatment_TransitionsToActive()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t4", "PAT-004", OncologyTreatmentType.Chemotherapy,
            "FOLFOX", null, null, null, null, null);

        DateTime startDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await grain.StartTreatmentAsync(startDate);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Active));
        Assert.That(state.StartDate, Is.EqualTo(startDate));
    }

    // ── Complete ──────────────────────────────────────────────────────────

    [Test]
    public async Task TreatmentGrain_CompleteTreatment_TransitionsToCompleted()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t5", "PAT-005", OncologyTreatmentType.Chemotherapy,
            "FOLFOX", null, null, null, null, null);
        await grain.StartTreatmentAsync(DateTime.UtcNow.AddMonths(-3));

        DateTime endDate = DateTime.UtcNow;
        await grain.CompleteTreatmentAsync(endDate, TreatmentResponseAssessment.CompleteResponse, "Patient tolerated well");

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Completed));
        Assert.That(state.EndDate, Is.EqualTo(endDate).Within(TimeSpan.FromSeconds(1)));
        Assert.That(state.ResponseAssessment, Is.EqualTo(TreatmentResponseAssessment.CompleteResponse));
        Assert.That(state.Notes, Does.Contain("Patient tolerated well"));
    }

    [Test]
    public async Task TreatmentGrain_CompleteTreatment_SetsResponseAssessmentDate()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t6", "PAT-006", OncologyTreatmentType.Radiation,
            "Stereotactic", null, null, null, null, null);
        await grain.StartTreatmentAsync(DateTime.UtcNow.AddMonths(-2));

        DateTime endDate = DateTime.UtcNow;
        await grain.CompleteTreatmentAsync(endDate, TreatmentResponseAssessment.PartialResponse, null);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.ResponseAssessmentDate, Is.EqualTo(endDate).Within(TimeSpan.FromSeconds(1)));
    }

    // ── Discontinue ───────────────────────────────────────────────────────

    [Test]
    public async Task TreatmentGrain_DiscontinueTreatment_TransitionsToDiscontinued()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t7", "PAT-007", OncologyTreatmentType.Chemotherapy,
            "Cisplatin", null, null, null, null, null);
        await grain.StartTreatmentAsync(DateTime.UtcNow.AddMonths(-1));

        await grain.DiscontinueTreatmentAsync(DateTime.UtcNow, "Grade 3 nephrotoxicity", "Switched to carboplatin");

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Discontinued));
        Assert.That(state.DiscontinuationReason, Is.EqualTo("Grade 3 nephrotoxicity"));
        Assert.That(state.Notes, Does.Contain("Switched to carboplatin"));
    }

    // ── Response Assessment ───────────────────────────────────────────────

    [Test]
    public async Task TreatmentGrain_RecordResponse_UpdatesAssessment()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t8", "PAT-008", OncologyTreatmentType.Immunotherapy,
            "Nivolumab", null, null, null, null, null);
        await grain.StartTreatmentAsync(DateTime.UtcNow.AddMonths(-4));

        DateTime assessDate = DateTime.UtcNow;
        await grain.RecordResponseAsync(TreatmentResponseAssessment.PartialResponse, assessDate, "Scan shows 30% reduction");

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.ResponseAssessment, Is.EqualTo(TreatmentResponseAssessment.PartialResponse));
        Assert.That(state.ResponseAssessmentDate, Is.EqualTo(assessDate).Within(TimeSpan.FromSeconds(1)));
        Assert.That(state.Notes, Does.Contain("30% reduction"));
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Active));  // still active
    }

    [Test]
    public async Task TreatmentGrain_RecordResponse_DefaultIsNotAssessed()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t9", "PAT-009", OncologyTreatmentType.TargetedTherapy,
            "Osimertinib", null, null, null, null, null);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.ResponseAssessment, Is.EqualTo(TreatmentResponseAssessment.NotAssessed));
    }

    // ── Cycles ────────────────────────────────────────────────────────────

    [Test]
    public async Task TreatmentGrain_UpdateCycles_StoresCycleCount()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t10", "PAT-010", OncologyTreatmentType.Chemotherapy,
            "AC-T", null, null, null, null, null);
        await grain.StartTreatmentAsync(DateTime.UtcNow.AddMonths(-3));

        await grain.UpdateCyclesAsync(4);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.CyclesCompleted, Is.EqualTo(4));
    }

    [Test]
    public async Task TreatmentGrain_UpdateCycles_CanIncrementMultipleTimes()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t11", "PAT-011", OncologyTreatmentType.Chemotherapy,
            "FOLFOX", null, null, null, null, null);
        await grain.StartTreatmentAsync(DateTime.UtcNow.AddMonths(-6));

        await grain.UpdateCyclesAsync(3);
        await grain.UpdateCyclesAsync(6);
        await grain.UpdateCyclesAsync(12);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.CyclesCompleted, Is.EqualTo(12));
    }

    // ── LastModifiedDate ─────────────────────────────────────────────────

    [Test]
    public async Task TreatmentGrain_LastModifiedDate_UpdatedOnWrite()
    {
        IOncologyTreatmentGrain grain = NewTreatment();
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        await grain.CreateTreatmentAsync(
            "ONC-TUMOR:t12", "PAT-012", OncologyTreatmentType.HormoneTherapy,
            "Tamoxifen", null, null, null, null, null);

        OncologyTreatmentState state = await grain.GetTreatmentAsync();
        Assert.That(state.LastModifiedDate, Is.GreaterThanOrEqualTo(before));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// OncologyTreatmentIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class OncologyTreatmentIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IOncologyTreatmentIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IOncologyTreatmentIndexGrain>($"ONC-TX-IDX:{Guid.NewGuid()}");

    private static OncologyTreatmentIndexEntry MakeEntry(
        string treatmentId,
        string tumorId,
        OncologyTreatmentStatus status = OncologyTreatmentStatus.Planned,
        DateTime? startDate = null) => new()
        {
            TreatmentId = treatmentId,
            TumorId = tumorId,
            TreatmentType = OncologyTreatmentType.Chemotherapy,
            AgentName = "FOLFOX",
            StartDate = startDate,
            EndDate = null,
            Status = status,
            ResponseAssessment = TreatmentResponseAssessment.NotAssessed,
            ProviderName = "Dr. Test"
        };

    [Test]
    public async Task TreatmentIndexGrain_EmptyIndex_ReturnsEmptyList()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        List<OncologyTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task TreatmentIndexGrain_UpsertTreatment_AppearsInGetAll()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        string txId = $"ONC-TX:{Guid.NewGuid()}";
        await index.UpsertTreatmentAsync(MakeEntry(txId, "ONC-TUMOR:t1"));

        List<OncologyTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TreatmentId, Is.EqualTo(txId));
    }

    [Test]
    public async Task TreatmentIndexGrain_UpsertTreatment_UpdatesExistingEntry()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        string txId = $"ONC-TX:{Guid.NewGuid()}";
        await index.UpsertTreatmentAsync(MakeEntry(txId, "ONC-TUMOR:t2", OncologyTreatmentStatus.Planned));
        await index.UpsertTreatmentAsync(MakeEntry(txId, "ONC-TUMOR:t2", OncologyTreatmentStatus.Active));

        List<OncologyTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(OncologyTreatmentStatus.Active));
    }

    [Test]
    public async Task TreatmentIndexGrain_GetByTumor_FiltersCorrectly()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        string tumorA = $"ONC-TUMOR:{Guid.NewGuid()}";
        string tumorB = $"ONC-TUMOR:{Guid.NewGuid()}";
        await index.UpsertTreatmentAsync(MakeEntry($"ONC-TX:{Guid.NewGuid()}", tumorA));
        await index.UpsertTreatmentAsync(MakeEntry($"ONC-TX:{Guid.NewGuid()}", tumorA));
        await index.UpsertTreatmentAsync(MakeEntry($"ONC-TX:{Guid.NewGuid()}", tumorB));

        List<OncologyTreatmentIndexEntry> forA = await index.GetTreatmentsByTumorAsync(tumorA);
        Assert.That(forA, Has.Count.EqualTo(2));
        Assert.That(forA.All(t => t.TumorId == tumorA), Is.True);
    }

    [Test]
    public async Task TreatmentIndexGrain_GetByTumor_EmptyForUnknownTumor()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        await index.UpsertTreatmentAsync(MakeEntry($"ONC-TX:{Guid.NewGuid()}", "ONC-TUMOR:known"));

        List<OncologyTreatmentIndexEntry> result = await index.GetTreatmentsByTumorAsync("ONC-TUMOR:unknown");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task TreatmentIndexGrain_RemoveTreatment_RemovesFromIndex()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        string txId = $"ONC-TX:{Guid.NewGuid()}";
        await index.UpsertTreatmentAsync(MakeEntry(txId, "ONC-TUMOR:t3"));
        await index.RemoveTreatmentAsync(txId);

        List<OncologyTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task TreatmentIndexGrain_RemoveNonExistentTreatment_IsIdempotent()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        string txId = $"ONC-TX:{Guid.NewGuid()}";
        await index.UpsertTreatmentAsync(MakeEntry(txId, "ONC-TUMOR:t4"));

        Assert.DoesNotThrowAsync(() => index.RemoveTreatmentAsync($"ONC-TX:{Guid.NewGuid()}"));

        List<OncologyTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TreatmentIndexGrain_GetAllTreatments_OrderedByStartDateDescending()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        DateTime older = DateTime.UtcNow.AddMonths(-6);
        DateTime newer = DateTime.UtcNow.AddMonths(-1);
        await index.UpsertTreatmentAsync(MakeEntry($"ONC-TX:{Guid.NewGuid()}", "ONC-TUMOR:t5", OncologyTreatmentStatus.Completed, older));
        await index.UpsertTreatmentAsync(MakeEntry($"ONC-TX:{Guid.NewGuid()}", "ONC-TUMOR:t5", OncologyTreatmentStatus.Active, newer));

        List<OncologyTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all[0].StartDate, Is.EqualTo(newer).Within(TimeSpan.FromSeconds(1)));
        Assert.That(all[1].StartDate, Is.EqualTo(older).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task TreatmentIndexGrain_MultipleTumors_AllReturnedInGetAll()
    {
        IOncologyTreatmentIndexGrain index = NewIndex();
        for (int i = 0; i < 5; i++)
            await index.UpsertTreatmentAsync(MakeEntry($"ONC-TX:{Guid.NewGuid()}", $"ONC-TUMOR:t{i}"));

        List<OncologyTreatmentIndexEntry> all = await index.GetAllTreatmentsAsync();
        Assert.That(all, Has.Count.EqualTo(5));
    }
}
