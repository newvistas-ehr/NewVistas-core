// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace NewVistas.WpfDelphiUI.Services;

/// <summary>
/// Typed HttpClient wrapper for the NewVistas REST API.
/// All methods are thin façades over the HTTP endpoints in NewVistas.WebServer.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Set (or clear) the JWT bearer token on all outgoing requests.
    /// </summary>
    public void SetAuthToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = token != null
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    public async Task<LoginResponseDto?> LoginAsync(string userName, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { UserName = userName, Password = password });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponseDto>(_json);
    }

    /// <summary>
    /// End the Orleans session on the server. Called on logout/app close.
    /// </summary>
    public async Task LogoutAsync()
    {
        try { await _http.PostAsync("api/auth/logout", null); }
        catch { /* best-effort — server session will expire on timeout anyway */ }
    }

    // ── Patient ───────────────────────────────────────────────────────────

    public Task<PatientDto?> GetPatientAsync(string patientId)
        => _http.GetFromJsonAsync<PatientDto>($"api/patient/{Esc(patientId)}", _json);

    public Task<List<PatientSearchResultDto>> SearchPatientsAsync(string query)
        => GetListAsync<PatientSearchResultDto>($"api/patient/search?q={Uri.EscapeDataString(query)}");

    // ── Problems ──────────────────────────────────────────────────────────

    public Task<List<ProblemDto>> GetProblemsAsync(string patientId)
        => GetListAsync<ProblemDto>($"api/patient/{Esc(patientId)}/problems");

    // ── Medications ───────────────────────────────────────────────────────

    public Task<List<PrescriptionDto>> GetPrescriptionsAsync(string patientId)
        => GetListAsync<PrescriptionDto>($"api/outpatientpharmacy/{Esc(patientId)}/prescriptions");

    // ── Allergies ─────────────────────────────────────────────────────────

    public Task<List<AllergyDto>> GetAllergiesAsync(string patientId)
        => GetListAsync<AllergyDto>($"api/patient/{Esc(patientId)}/allergies");

    // ── Lab Results ───────────────────────────────────────────────────────

    public Task<List<LabResultDto>> GetLabResultsAsync(string patientId)
        => GetListAsync<LabResultDto>($"api/lab/{Esc(patientId)}/results");

    public Task<List<LabResultDto>> GetAbnormalLabsAsync(string patientId)
        => GetListAsync<LabResultDto>($"api/lab/{Esc(patientId)}/abnormal");

    // ── Vital Signs ───────────────────────────────────────────────────────

    public Task<List<VitalSummaryDto>> GetVitalsAsync(string patientId)
        => GetListAsync<VitalSummaryDto>($"api/patient/{Esc(patientId)}/vitals");

    public Task<List<VitalSummaryDto>> GetVitalHistoryAsync(string patientId, DateTime from, DateTime to)
        => GetListAsync<VitalSummaryDto>($"api/patient/{Esc(patientId)}/vitals/history?from={from:O}&to={to:O}&maxCount=100");

    // ── Clinical Notes ────────────────────────────────────────────────────

    public Task<List<NoteDto>> GetNotesAsync(string patientId)
        => GetListAsync<NoteDto>($"api/patient/{Esc(patientId)}/notes");

    public Task<List<NoteDto>> GetNoteHistoryAsync(string patientId, DateTime from, DateTime to)
        => GetListAsync<NoteDto>($"api/patient/{Esc(patientId)}/notes/history?from={from:O}&to={to:O}&maxCount=100");

    // ── Orders ────────────────────────────────────────────────────────────

    public Task<List<OrderDto>> GetOrdersAsync(string patientId)
        => GetListAsync<OrderDto>($"api/patient/{Esc(patientId)}/orders");

    public Task<List<OrderDto>> GetOrdersWithFilterAsync(string patientId, int filter)
        => GetListAsync<OrderDto>($"api/patient/{Esc(patientId)}/orders?filter={filter}");

    public Task<List<OrderDto>> GetOrderHistoryAsync(string patientId, DateTime from, DateTime to)
        => GetListAsync<OrderDto>($"api/patient/{Esc(patientId)}/orders/history?from={from:O}&to={to:O}&maxCount=100");

    // ── Appointments ──────────────────────────────────────────────────────

    public Task<List<AppointmentDto>> GetAppointmentsAsync(string patientId)
        => GetListAsync<AppointmentDto>($"api/scheduling/{Esc(patientId)}/appointments");

    // ── Clinical Reminders ────────────────────────────────────────────────

    public Task<List<ReminderDto>> GetRemindersAsync(string patientId)
        => GetListAsync<ReminderDto>($"api/patient/{Esc(patientId)}/reminders");

    // ── Radiology ─────────────────────────────────────────────────────────

    public Task<List<RadiologyReportDto>> GetRadiologyReportsAsync(string patientId)
        => GetListAsync<RadiologyReportDto>($"api/patient/{Esc(patientId)}/radiology");

    // ── Consults ──────────────────────────────────────────────────────────

    public Task<List<ConsultDto>> GetConsultsAsync(string patientId)
        => GetListAsync<ConsultDto>($"api/patient/{Esc(patientId)}/consults");

    // ── Surgery ───────────────────────────────────────────────────────────

    public Task<List<SurgeryDto>> GetSurgeriesAsync(string patientId)
        => GetListAsync<SurgeryDto>($"api/patient/{Esc(patientId)}/surgery");

    // ── Problems (write) ───────────────────────────────────────────────────

    public async Task AddProblemAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/problems", request);

    // ── Allergies (write) ──────────────────────────────────────────────────

    public async Task RecordAllergyAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/allergies", request);

    // ── Vitals (write) ─────────────────────────────────────────────────────

    public async Task RecordVitalsAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/vitals", request);

    // ── Notes (write) ──────────────────────────────────────────────────────

    public async Task CreateNoteAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/notes", request);

    public async Task SignNoteAsync(string patientId, string documentId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/notes/{documentId}/sign", request);

    // ── Orders (write) ─────────────────────────────────────────────────────

    public async Task PlaceOrderAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/orders", request);

    public async Task SignOrderAsync(string patientId, string orderId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/orders/{orderId}/sign", request);

    public async Task DiscontinueOrderAsync(string patientId, string orderId)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/orders/{orderId}/discontinue", new { });

    // ── Labs (write) ───────────────────────────────────────────────────────

    public async Task OrderLabTestAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/lab/{Esc(patientId)}/orders", request);

    // ── Consults (write) ───────────────────────────────────────────────────

    public async Task RequestConsultAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/consults", request);

    // ── Surgery (write) ────────────────────────────────────────────────────

    public async Task ScheduleSurgeryAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/surgery", request);

    // ── Diabetes Registry (read-only) ─────────────────────────────────────
    // Workflow grain enforces CanManageDiabetesRegistry on writes; reads
    // are open to any authenticated clinician — the chart panel is read-only.

    public Task<DiabetesRegistrySnapshotDto?> GetDiabetesSnapshotAsync(string patientId)
        => _http.GetFromJsonAsync<DiabetesRegistrySnapshotDto>($"api/diabetesregistry/{Esc(patientId)}/snapshot", _json);

    public Task<DiabetesPreVisitPlanDto?> GetDiabetesPreVisitPlanAsync(string patientId, DateTime? visitDate = null)
    {
        string url = $"api/diabetesregistry/{Esc(patientId)}/previsit-plan";
        if (visitDate.HasValue) url += $"?visitDate={visitDate.Value:O}";
        return _http.GetFromJsonAsync<DiabetesPreVisitPlanDto>(url, _json);
    }

    // ── External Referrals + CHS Authorization ────────────────────────────

    public Task<List<ExternalReferralDto>> GetExternalReferralsAsync(string patientId)
        => GetListAsync<ExternalReferralDto>($"api/externalreferral/{Esc(patientId)}/referrals");

    public Task<HttpResponseMessage> RequestChsAuthorizationAsync(string patientId, string referralId, object request)
        => _http.PostAsJsonAsync($"api/externalreferral/{Esc(patientId)}/referrals/{Esc(referralId)}/chs/request", request);

    public Task<HttpResponseMessage> ApproveChsAuthorizationAsync(string patientId, string referralId, object request)
        => _http.PostAsJsonAsync($"api/externalreferral/{Esc(patientId)}/referrals/{Esc(referralId)}/chs/approve", request);

    public Task<HttpResponseMessage> DenyChsAuthorizationAsync(string patientId, string referralId, object request)
        => _http.PostAsJsonAsync($"api/externalreferral/{Esc(patientId)}/referrals/{Esc(referralId)}/chs/deny", request);

    // ── GPRA Reporting (facility-wide, not per-patient) ───────────────────

    public Task<List<GpraReportIndexEntryDto>> GetGpraReportsAsync()
        => GetListAsync<GpraReportIndexEntryDto>("api/gpra/reports");

    public Task<List<GpraReportIndexEntryDto>> GetGpraReportsByFiscalYearAsync(int fiscalYear)
        => GetListAsync<GpraReportIndexEntryDto>($"api/gpra/reports/fy/{fiscalYear}");

    public Task<GpraReportDto?> GetGpraReportAsync(string reportId)
        => _http.GetFromJsonAsync<GpraReportDto>($"api/gpra/reports/{Esc(reportId)}", _json);

    // ── Security Key Fetch ──────────────────────────────────────────────

    public async Task<List<string>?> GetSecurityKeysAsync(string userId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<AclKeysResponse>(
                $"api/accesscontrol/users/{Esc(userId)}/keys", _json);
            return response?.Keys;
        }
        catch
        {
            return null;
        }
    }

    private sealed class AclKeysResponse
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> Keys { get; set; } = [];
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string Esc(string id) => Uri.EscapeDataString(id.Trim());

    private async Task<List<T>> GetListAsync<T>(string url)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<T>>(url, _json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

// ── DTOs (mirrors REST API response shapes) ───────────────────────────────────

public record PatientDto(
    string PatientId,
    string Name,
    string Dob,
    string Ssn,
    string Sex,
    string Age,
    bool IsVeteran,
    string ServiceConnectedPercent,
    string Ward,
    string RoomBed,
    string PrimaryProvider,
    string Attending,
    string LastVisit);

public record PatientSearchResultDto(
    string PatientId,
    string Name,
    string Dob,
    string Ssn);

public record ProblemDto(
    string ProblemId,
    string IcdCode,
    string Description,
    string Status,
    string OnsetDate,
    string Provider);

public record PrescriptionDto(
    string PrescriptionId,
    string DrugName,
    string Sig,
    string Status,
    string IssueDate,
    string Refills);

public record AllergyDto(
    string AllergyId,
    string Allergen,
    string Severity,
    string Reactions,
    string Type);

public record LabResultDto(
    string LabTestId,
    string TestName,
    string Value,
    string Units,
    string ReferenceRange,
    string AbnormalFlag,
    string CollectionDate,
    string Status);

public record VitalSummaryDto(
    string VitalType,
    string Value,
    string Units,
    string TakenDate,
    bool IsAbnormal);

public record NoteDto(
    string DocumentId,
    string Title,
    string Author,
    string SignedDate,
    string Status,
    string DocumentType);

public record OrderDto(
    string OrderId,
    string OrderText,
    string Status,
    string OrderedBy,
    string OrderDate,
    string Service);

public record AppointmentDto(
    string AppointmentId,
    string Clinic,
    string AppointmentDate,
    string Status,
    string Provider);

public record ReminderDto(
    string ReminderId,
    string Name,
    string DueDate,
    string Priority);

public record RadiologyReportDto(
    string ReportId,
    string Procedure,
    string ReportDate,
    string Status,
    string Impression);

public record ConsultDto(
    string ConsultId,
    string Service,
    string RequestDate,
    string Status,
    string Urgency);

public record SurgeryDto(
    string SurgeryId,
    string Procedure,
    string SurgeryDate,
    string Surgeon,
    string Status);

public record LoginResponseDto(
    string Token,
    string UserId,
    string UserName,
    string DisplayName,
    string? UserClass,
    bool HasElectronicSignature);

// ── Diabetes Registry DTOs (mirror NewVistas.Abstractions.GrainStates) ─────────
// Status values are JSON ints (matches the [GenerateSerializer] enum default).

public sealed class DiabetesRegistrySnapshotDto
{
    public string Icn { get; set; } = string.Empty;
    public bool IsEnrolled { get; set; }
    public string? DiabetesType { get; set; }
    public decimal? LastHbA1cValue { get; set; }
    public DateTime? LastHbA1cDate { get; set; }
    /// <summary>0=NoData, 1=Good, 2=AtTarget, 3=Poor.</summary>
    public int HbA1cControl { get; set; }
    /// <summary>0=NoData, 1=UpToDate, 2=Due, 3=Overdue (each).</summary>
    public int FootExamStatus { get; set; }
    public int EyeExamStatus { get; set; }
    public int AcrStatus { get; set; }
    /// <summary>0=NoData, 1=Normal, 2=Reduced, 3=Severe.</summary>
    public int KidneyFunction { get; set; }
    public decimal? LastEgfrValue { get; set; }
    public DateTime? LastEgfrDate { get; set; }
    public decimal? LastAcrValue { get; set; }
    public DateTime? LastAcrDate { get; set; }
    public DateTime? LastFootExamDate { get; set; }
    public DateTime? LastEyeExamDate { get; set; }
}

public sealed class DiabetesPreVisitPlanDto
{
    public string Icn { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public List<string> ItemsDue { get; set; } = [];
    public List<string> ItemsOverdue { get; set; } = [];
    public List<string> ItemsUpToDate { get; set; } = [];
    public DiabetesRegistrySnapshotDto Snapshot { get; set; } = new();
}

// ── External Referral / CHS DTOs ─────────────────────────────────────────────
// Mirrors ExternalReferralState; only the fields the chart UI needs.

public sealed class ExternalReferralDto
{
    public string ReferralId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string ReferralType { get; set; } = string.Empty;
    public string ExternalFacilityName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public string Urgency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusReason { get; set; }
    public string ReferredByProviderName { get; set; } = string.Empty;
    public DateTime ReferralDate { get; set; }
    public DateTime? AppointmentDateTime { get; set; }

    // CHS fields
    public bool IsChsReferral { get; set; }
    public string? MedicalPriorityClass { get; set; }
    public decimal? AuthorizedAmount { get; set; }
    public bool AlternateResourcesChecked { get; set; }
    public string? AlternateResourcesNote { get; set; }
    public DateTime? ChsAuthorizationDate { get; set; }
    public string? ChsAuthorizedByName { get; set; }
}

// ── GPRA Reporting DTOs ──────────────────────────────────────────────────────
// Mirrors GpraReportIndexEntry / GpraReportState. Status/period/category are
// serialised as ints (Orleans default).

public sealed class GpraReportIndexEntryDto
{
    public string ReportId { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    /// <summary>0=FullFiscalYear, 1=Q1, 2=Q2, 3=Q3, 4=Q4.</summary>
    public int ReportingPeriod { get; set; }
    /// <summary>0=Draft, 1=Evaluating, 2=Completed, 3=Error.</summary>
    public int Status { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public int ActiveUserPopulation { get; set; }
    public int IndicatorCount { get; set; }
    public DateTime CreatedDate { get; set; }
}

public sealed class GpraReportDto
{
    public string ReportId { get; set; } = string.Empty;
    public int Status { get; set; }
    public int FiscalYear { get; set; }
    public int ReportingPeriod { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public int ActiveUserPopulation { get; set; }
    public List<GpraIndicatorResultDto> Indicators { get; set; } = [];
    public DateTime CreatedDate { get; set; }
}

public sealed class GpraIndicatorResultDto
{
    public string MeasureId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>0=Diabetes, 1=CV, 2=WomensHealth, 3=Imm, 4=BH, 5=Prev, 6=Asthma, 7=Child, 8=Oral, 9=OBGYN.</summary>
    public int Category { get; set; }
    public int CurrentDenominator { get; set; }
    public int CurrentNumerator { get; set; }
    public decimal CurrentPerformanceRate { get; set; }
    public int BaselineDenominator { get; set; }
    public int BaselineNumerator { get; set; }
    public decimal BaselinePerformanceRate { get; set; }
    public decimal PercentagePointChange { get; set; }
    public bool IsImproved { get; set; }
    public decimal? TargetRate { get; set; }
    public bool TargetMet { get; set; }
}
