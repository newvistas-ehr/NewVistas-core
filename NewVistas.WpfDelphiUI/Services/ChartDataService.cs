// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WpfDelphiUI.Services;

/// <summary>
/// All chart data for the CPRS-style tabs, read and written through <b>direct grain
/// calls</b>.
///
/// This replaces the data half of <see cref="ApiClient"/>. The division is the one the
/// architecture rests on: the WebServer answers <i>authentication</i> ("are you who you
/// say you are") and serves outsiders — patient portal, FHIR, inbound interfaces — while
/// <i>authorization</i> ("you may do A but not B") and every clinical read and write live
/// in the grains. Going through HTTP for chart data added a network hop and moved the
/// authorization decision to the wrong tier.
///
/// The DTOs are kept exactly as they were so the existing XAML bindings are untouched;
/// only the source of the data changed. Mapping lives here rather than in eleven
/// ViewModels so there is one place to look.
/// </summary>
public sealed class ChartDataService
{
    private readonly OrleansGrainService _grains;

    public ChartDataService(OrleansGrainService grains)
    {
        _grains = grains;
    }

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _grains.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Formatting helpers (DTOs are display-shaped strings) ───────────────

    private static string D(DateTime? d) => d?.ToString("MM/dd/yyyy") ?? string.Empty;
    private static string DT(DateTime? d) => d?.ToString("MM/dd/yyyy HH:mm") ?? string.Empty;
    private static string S(string? s) => s ?? string.Empty;

    // ── Patient ───────────────────────────────────────────────────────────

    public async Task<PatientDto?> GetPatientAsync(string patientId)
    {
        PatientState p = await Workflow(patientId).GetPatientAsync();
        if (string.IsNullOrEmpty(p.Name)) return null;

        string age = p.DateOfBirth is null
            ? string.Empty
            : ((int)((DateTime.UtcNow - p.DateOfBirth.Value).TotalDays / 365.2425)).ToString();

        return new PatientDto(
            PatientId: patientId,
            Name: p.Name,
            Dob: D(p.DateOfBirth),
            Ssn: S(p.SocialSecurityNumber),
            Sex: S(p.Sex),
            Age: age,
            IsVeteran: !string.IsNullOrWhiteSpace(p.Veteran) &&
                       !p.Veteran.Equals("NO", StringComparison.OrdinalIgnoreCase),
            ServiceConnectedPercent: p.ServiceConnectedPercentage?.ToString() ?? string.Empty,
            // Ward and attending live on the ADT movement, not the patient record.
            Ward: S(p.CurrentAdmission),
            RoomBed: S(p.RoomBed),
            PrimaryProvider: string.Empty,
            Attending: string.Empty,
            LastVisit: string.Empty);
    }

    public async Task<List<PatientSearchResultDto>> SearchPatientsAsync(string query)
    {
        var index = _grains.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");
        var results = await index.SearchAsync(query, 50);
        return results
            .Select(r => new PatientSearchResultDto(r.PatientId, r.Name, D(r.DateOfBirth), r.SsnLast4))
            .ToList();
    }

    // ── Cover sheet panels ────────────────────────────────────────────────

    public async Task<List<ProblemDto>> GetProblemsAsync(string patientId) =>
        (await Workflow(patientId).GetActiveProblemsAsync())
        .Select(p => new ProblemDto(p.ProblemId, S(p.DiagnosisCode), p.Diagnosis, p.Status, D(p.DateOfOnset), string.Empty))
        .ToList();

    public async Task<List<PrescriptionDto>> GetPrescriptionsAsync(string patientId) =>
        (await Workflow(patientId).GetActiveMedicationsAsync())
        .Select(r => new PrescriptionDto(
            r.PrescriptionId, r.DrugName, S(r.Sig), r.Status, D(r.FillDate),
            r.RefillsRemaining?.ToString() ?? string.Empty))
        .ToList();

    public async Task<List<AllergyDto>> GetAllergiesAsync(string patientId) =>
        (await Workflow(patientId).GetAllergiesAsync())
        .Select(a => new AllergyDto(a.AllergyId, a.Allergen, S(a.Severity), string.Join(", ", a.Reactions), a.AllergenType))
        .ToList();

    public async Task<List<LabResultDto>> GetLabResultsAsync(string patientId) =>
        (await Workflow(patientId).GetLabResultsAsync())
        .Select(MapLab)
        .ToList();

    public async Task<List<LabResultDto>> GetAbnormalLabsAsync(string patientId) =>
        (await Workflow(patientId).GetLabResultsAsync())
        .Where(l => !string.IsNullOrWhiteSpace(l.Flag))
        .Select(MapLab)
        .ToList();

    private static LabResultDto MapLab(LabResultSummary l) =>
        new(l.LabTestId, l.TestName, S(l.ResultValue), S(l.Units), string.Empty, S(l.Flag), D(l.CollectionDate), l.Status);

    public async Task<List<VitalSummaryDto>> GetVitalsAsync(string patientId) =>
        (await Workflow(patientId).GetLatestVitalsAsync())
        .Select(MapVital)
        .ToList();

    public async Task<List<VitalSummaryDto>> GetVitalHistoryAsync(string patientId, DateTime from, DateTime to) =>
        (await Workflow(patientId).GetLatestVitalsAsync())
        .Where(v => v.DateTimeTaken >= from && v.DateTimeTaken <= to)
        .Select(MapVital)
        .ToList();

    private static VitalSummaryDto MapVital(VitalSummary v) =>
        new(v.VitalType, v.Value, S(v.Units), DT(v.DateTimeTaken), !string.IsNullOrWhiteSpace(v.AbnormalFlag));

    public async Task<List<NoteDto>> GetNotesAsync(string patientId) =>
        (await Workflow(patientId).GetNotesAsync(null, 50))
        .Select(MapNote)
        .ToList();

    public async Task<List<NoteDto>> GetNoteHistoryAsync(string patientId, DateTime from, DateTime to) =>
        (await Workflow(patientId).GetNotesAsync(null, 200))
        .Where(n => n.ReferenceDate >= from && n.ReferenceDate <= to)
        .Select(MapNote)
        .ToList();

    private static NoteDto MapNote(TiuNoteSummary n) =>
        new(n.DocumentId, S(n.Subject), S(n.AuthorName), D(n.ReferenceDate), n.Status, n.DocumentType);

    public async Task<List<OrderDto>> GetOrdersAsync(string patientId) =>
        (await Workflow(patientId).GetRecentOrdersAsync()).Select(MapOrder).ToList();

    public async Task<List<OrderDto>> GetOrdersWithFilterAsync(string patientId, int filter) =>
        (await Workflow(patientId).GetOrdersByFilterAsync(filter)).Select(MapOrder).ToList();

    public async Task<List<OrderDto>> GetOrderHistoryAsync(string patientId, DateTime from, DateTime to) =>
        (await Workflow(patientId).GetOrderHistoryAsync(from, to, 200)).Select(MapOrder).ToList();

    private static OrderDto MapOrder(OrderSummary o) =>
        new(o.OrderId, o.OrderText, o.Status, S(o.ProviderName), D(o.StartDate), o.OrderType);

    public async Task<List<AppointmentDto>> GetAppointmentsAsync(string patientId) =>
        (await Workflow(patientId).GetAllAppointmentsAsync(50))
        .Select(a => new AppointmentDto(a.AppointmentId, a.ClinicName, DT(a.AppointmentDateTime), a.Status, S(a.ProviderName)))
        .ToList();

    public async Task<List<ReminderDto>> GetRemindersAsync(string patientId) =>
        (await Workflow(patientId).GetRemindersAsync())
        .Select(r => new ReminderDto(r.ReminderId, r.ReminderName, D(r.DueDate), r.Status))
        .ToList();

    public async Task<List<RadiologyReportDto>> GetRadiologyReportsAsync(string patientId) =>
        (await Workflow(patientId).GetRadiologyStudiesAsync(50))
        .Select(r => new RadiologyReportDto(r.RadiologyId, r.ProcedureName, D(r.ExamDateTime), r.Status, S(r.ImagingType)))
        .ToList();

    public async Task<List<ConsultDto>> GetConsultsAsync(string patientId) =>
        (await Workflow(patientId).GetConsultsAsync(null, 50))
        .Select(c => new ConsultDto(c.ConsultId, c.ToService, D(c.RequestDateTime), c.Status, c.Urgency))
        .ToList();

    public async Task<List<ExternalReferralDto>> GetExternalReferralsAsync(string patientId) =>
        (await Workflow(patientId).GetExternalReferralsAsync())
        .Select(r => new ExternalReferralDto
        {
            ReferralId = r.ReferralId,
            PatientId = r.PatientId,
            PatientName = r.PatientName,
            ReferralType = r.ReferralType,
            ExternalFacilityName = r.ExternalFacilityName,
            Status = r.Status,
            Urgency = r.Urgency,
        })
        .ToList();

    public async Task<DiabetesRegistrySnapshotDto?> GetDiabetesSnapshotAsync(string patientId)
    {
        DiabetesRegistrySnapshot s = await Workflow(patientId).GetDiabetesRegistrySnapshotAsync();
        if (!s.IsEnrolled) return null;
        return new DiabetesRegistrySnapshotDto
        {
            IsEnrolled = s.IsEnrolled,
            DiabetesType = S(s.DiabetesType),
            LastHbA1cValue = s.LastHbA1cValue,
            LastHbA1cDate = s.LastHbA1cDate,
            HbA1cControl = (int)s.HbA1cControl,
            FootExamStatus = (int)s.FootExamStatus,
            EyeExamStatus = (int)s.EyeExamStatus,
            AcrStatus = (int)s.AcrStatus,
            KidneyFunction = (int)s.KidneyFunction,
        };
    }

    // ── Writes ────────────────────────────────────────────────────────────
    // Named parameters rather than the anonymous JSON bodies the REST calls used, so a
    // wrong field name is now a compile error instead of a silently ignored property.

    public Task AddProblemAsync(string patientId, string description, string? icdCode, DateTime? onsetDate) =>
        Workflow(patientId).AddProblemAsync(
            description, icdCode, null, null, onsetDate,
            Actor(patientId).Id, Actor(patientId).Name, null, null, false, null);

    public Task RecordVitalsAsync(string patientId, Dictionary<string, string> vitals, DateTime takenAt) =>
        Workflow(patientId).RecordVitalsAsync(
            null, null, Actor(patientId).Id, Actor(patientId).Name, takenAt, vitals, null);

    public Task CreateNoteAsync(string patientId, string documentType, string title, string noteText, string? authorName) =>
        Workflow(patientId).CreateNoteAsync(
            documentType, null, noteText, title,
            Actor(patientId).Id, authorName ?? Actor(patientId).Name,
            null, null, null, null, null, DateTime.UtcNow);

    public Task SignNoteAsync(string patientId, string documentId, string signatureCode) =>
        Workflow(patientId).SignNoteAsync(documentId, signatureCode);

    public Task PlaceOrderAsync(string patientId, string orderType, string orderText, string urgency) =>
        Workflow(patientId).PlaceOrderAsync(
            orderType, orderText, null,
            Actor(patientId).Id, Actor(patientId).Name,
            null, null, urgency, null, null);

    public Task SignOrderAsync(string patientId, string orderId, string electronicSignature) =>
        Workflow(patientId).SignOrderAsync(orderId, electronicSignature);

    public Task DiscontinueOrderAsync(string patientId, string orderId, string reason = "Discontinued via CPRS UI") =>
        Workflow(patientId).DiscontinueOrderAsync(orderId, reason);

    public Task OrderLabTestAsync(string patientId, string testName, string? loincCode, string priority) =>
        Workflow(patientId).OrderLabTestAsync(
            Guid.NewGuid().ToString(), testName, loincCode, null,
            Actor(patientId).Id, Actor(patientId).Name, null, priority);

    public Task RequestConsultAsync(string patientId, string toService, string reason, string urgency) =>
        Workflow(patientId).RequestConsultAsync(
            toService, null, null, null, urgency,
            Actor(patientId).Id, Actor(patientId).Name,
            null, null, reason, null, null, null, null);

    public Task ScheduleSurgeryAsync(string patientId, string procedure, DateTime dateOfOperation, string? surgeonName) =>
        Workflow(patientId).ScheduleSurgeryAsync(
            procedure, null, dateOfOperation,
            Actor(patientId).Id, surgeonName ?? Actor(patientId).Name,
            null, null, null, null, null, null);

    // ── CHS (Contract Health Services) authorization ──────────────────────
    // Key-gated in the grain (CanAuthorizeChs) — exactly the kind of decision that must
    // stay on the grain side of the authentication/authorization line.

    public Task RequestChsAuthorizationAsync(
        string patientId, string referralId, decimal estimatedCost, string priorityClass,
        bool alternateResourcesChecked, string? alternateResourcesNote) =>
        Workflow(patientId).RequestChsAuthorizationAsync(
            referralId, estimatedCost, priorityClass, alternateResourcesChecked,
            alternateResourcesNote, Actor(patientId).Id, Actor(patientId).Name);

    public Task ApproveChsAuthorizationAsync(
        string patientId, string referralId, decimal authorizedAmount, string? authorizationNumber) =>
        Workflow(patientId).ApproveChsAuthorizationAsync(
            referralId, authorizedAmount, authorizationNumber, Actor(patientId).Id, Actor(patientId).Name);

    public Task DenyChsAuthorizationAsync(string patientId, string referralId, string denialReason) =>
        Workflow(patientId).DenyChsAuthorizationAsync(
            referralId, denialReason, Actor(patientId).Id, Actor(patientId).Name);

    // ── Diabetes registry + GPRA ──────────────────────────────────────────

    public async Task<DiabetesPreVisitPlanDto?> GetDiabetesPreVisitPlanAsync(string patientId, DateTime? visitDate = null)
    {
        DiabetesPreVisitPlan plan = await Workflow(patientId).GetDiabetesPreVisitPlanAsync(visitDate ?? DateTime.UtcNow);
        return new DiabetesPreVisitPlanDto
        {
            ItemsDue = plan.ItemsDue,
            ItemsOverdue = plan.ItemsOverdue,
            ItemsUpToDate = plan.ItemsUpToDate,
        };
    }

    public async Task<List<GpraReportIndexEntryDto>> GetGpraReportsAsync() =>
        (await _grains.GetGrain<IGpraReportIndexGrain>("GPRA-REPORT-INDEX").GetAllAsync())
        .Select(MapGpraIndex).ToList();

    public async Task<List<GpraReportIndexEntryDto>> GetGpraReportsByFiscalYearAsync(int fiscalYear) =>
        (await _grains.GetGrain<IGpraReportIndexGrain>("GPRA-REPORT-INDEX").GetByFiscalYearAsync(fiscalYear))
        .Select(MapGpraIndex).ToList();

    private static GpraReportIndexEntryDto MapGpraIndex(GpraReportIndexEntry e) => new()
    {
        ReportId = e.ReportId,
        FiscalYear = e.FiscalYear,
        Status = (int)e.Status,
    };

    public async Task<GpraReportDto?> GetGpraReportAsync(string reportId)
    {
        GpraReportState r = await _grains.GetGrain<IGpraReportGrain>(reportId).GetAsync();
        if (string.IsNullOrEmpty(r.ReportId)) return null;
        return new GpraReportDto
        {
            ReportId = r.ReportId,
            FiscalYear = r.FiscalYear,
            Status = (int)r.Status,
        };
    }

    /// <summary>The signed-in clinician, for attributing writes.</summary>
    private (string Id, string Name) Actor(string _) =>
        (_grains.CurrentUserId ?? "CPRS", _grains.CurrentUserName ?? "CPRS User");

    public async Task<List<SurgeryDto>> GetSurgeriesAsync(string patientId) =>
        (await Workflow(patientId).GetSurgeriesAsync(50))
        .Select(s => new SurgeryDto(s.SurgeryId, s.PrincipalProcedure, D(s.DateOfOperation), S(s.SurgeonName), s.Status))
        .ToList();
}
