// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.Services;

/// <summary>
/// Typed HttpClient wrapper for the NewVistas REST API.
/// Replaces direct IClusterClient grain calls with HTTP REST calls.
/// All methods mirror the grain operations that ViewModels previously called directly.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Expose the underlying HttpClient for subsystem VMs that need direct REST access
    /// to endpoints not yet covered by typed methods above.
    /// </summary>
    public HttpClient Http => _http;

    /// <summary>
    /// JSON options for deserialization (camelCase web defaults).
    /// </summary>
    public static JsonSerializerOptions Json => _json;

    // ── Auth ──────────────────────────────────────────────────────────────

    public void SetAuthToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = token != null
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;
    }

    public async Task<LoginResponse?> LoginAsync(string userName, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { UserName = userName, Password = password });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>(_json);
    }

    /// <summary>
    /// Complete MFA verification with a challenge token and TOTP code.
    /// Returns a full LoginResponse with session token on success.
    /// </summary>
    public async Task<LoginResponse?> VerifyMfaAsync(string challengeToken, string code)
    {
        var response = await _http.PostAsJsonAsync("api/auth/mfa/verify",
            new { MfaChallengeToken = challengeToken, Code = code });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>(_json);
    }

    /// <summary>
    /// End the Orleans session on the server. Called on logout/app close.
    /// </summary>
    public async Task LogoutAsync()
    {
        try { await _http.PostAsync("api/auth/logout", null); }
        catch { /* best-effort — server session will expire on timeout anyway */ }
    }

    /// <summary>
    /// Touch the server-side session to prevent timeout (called periodically by AuthService).
    /// </summary>
    public async Task TouchSessionAsync()
    {
        try { await _http.PostAsync("api/auth/touch", null); }
        catch { /* best-effort */ }
    }

    // ── Patient (PatientController) ──────────────────────────────────────

    public Task<PatientState?> GetPatientAsync(string patientId)
        => _http.GetFromJsonAsync<PatientState>($"api/patient/{Esc(patientId)}", _json);

    public Task<List<PatientIndexEntry>> SearchPatientsAsync(string query)
        => GetListAsync<PatientIndexEntry>($"api/patient/search?q={Uri.EscapeDataString(query)}");

    // ── Cover Sheet ──────────────────────────────────────────────────────

    public Task<CoverSheetState?> GetCoverSheetAsync(string patientId)
        => _http.GetFromJsonAsync<CoverSheetState>($"api/patient/{Esc(patientId)}/cover-sheet", _json);

    // ── Problems ─────────────────────────────────────────────────────────

    public Task<List<ProblemSummary>> GetActiveProblemsAsync(string patientId)
        => GetListAsync<ProblemSummary>($"api/patient/{Esc(patientId)}/problems?active=true");

    public Task<List<ProblemSummary>> GetAllProblemsAsync(string patientId)
        => GetListAsync<ProblemSummary>($"api/patient/{Esc(patientId)}/problems");

    public async Task AddProblemAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/problems", request);

    // ── Orders ───────────────────────────────────────────────────────────

    public Task<List<OrderSummary>> GetOrdersAsync(string patientId, int filter = 0)
        => GetListAsync<OrderSummary>($"api/patient/{Esc(patientId)}/orders?filter={filter}");

    public async Task PlaceOrderAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/orders", request);

    public async Task SignOrderAsync(string patientId, string orderId, string signature)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/orders/{orderId}/sign", new { ElectronicSignature = signature });

    // ── Allergies ────────────────────────────────────────────────────────

    public Task<List<AllergySummary>> GetAllergiesAsync(string patientId)
        => GetListAsync<AllergySummary>($"api/patient/{Esc(patientId)}/allergies");

    public async Task RecordAllergyAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/allergies", request);

    // ── Vitals ───────────────────────────────────────────────────────────

    public Task<List<VitalSummary>> GetVitalsAsync(string patientId)
        => GetListAsync<VitalSummary>($"api/patient/{Esc(patientId)}/vitals");

    public async Task RecordVitalsAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/vitals", request);

    // ── Notes (TIU Documents) ────────────────────────────────────────────

    public Task<List<TiuNoteSummary>> GetNotesAsync(string patientId)
        => GetListAsync<TiuNoteSummary>($"api/patient/{Esc(patientId)}/notes");

    public async Task CreateNoteAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/notes", request);

    public async Task SignNoteAsync(string patientId, string documentId, object request)
        => await _http.PostAsJsonAsync($"api/patient/{Esc(patientId)}/notes/{documentId}/sign", request);

    // ── Lab Results ──────────────────────────────────────────────────────

    public Task<List<LabResultDetail>> GetLabResultsAsync(string patientId)
        => GetListAsync<LabResultDetail>($"api/lab/{Esc(patientId)}/results");

    public Task<LabTestSummaryEntry?> GetLabSummaryAsync(string patientId)
        => _http.GetFromJsonAsync<LabTestSummaryEntry>($"api/lab/{Esc(patientId)}/summary", _json);

    public Task<LabTestState?> GetLabTestAsync(string patientId, string labTestId)
        => _http.GetFromJsonAsync<LabTestState>($"api/lab/{Esc(patientId)}/orders/{labTestId}", _json);

    public async Task OrderLabTestAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/lab/{Esc(patientId)}/orders", request);

    public Task<List<LabResultDetail>> GetAbnormalLabsAsync(string patientId)
        => GetListAsync<LabResultDetail>($"api/lab/{Esc(patientId)}/abnormal");

    // ── Consults ─────────────────────────────────────────────────────────

    public Task<List<ConsultSummary>> GetConsultsAsync(string patientId)
        => GetListAsync<ConsultSummary>($"api/patient/{Esc(patientId)}/consults");

    // ── Appointments / Scheduling ────────────────────────────────────────

    public Task<List<AppointmentEntry>> GetAppointmentsAsync(string patientId, int max = 50)
        => GetListAsync<AppointmentEntry>($"api/scheduling/{Esc(patientId)}/appointments?max={max}");

    public Task<List<VisitSummary>> GetUpcomingAppointmentsAsync(string patientId)
        => GetListAsync<VisitSummary>($"api/scheduling/{Esc(patientId)}/appointments/upcoming");

    public Task<List<ClinicEntry>> GetClinicListAsync()
        => GetListAsync<ClinicEntry>("api/scheduling/clinics");

    public async Task ScheduleAppointmentAsync(string patientId, object request)
        => await _http.PostAsJsonAsync($"api/scheduling/{Esc(patientId)}/appointments", request);

    public async Task CheckInAsync(string patientId, string appointmentId, DateTime? time)
        => await _http.PostAsJsonAsync($"api/scheduling/{Esc(patientId)}/appointments/{appointmentId}/checkin", new { CheckInTime = time });

    public async Task CancelAppointmentAsync(string patientId, string appointmentId)
        => await _http.PostAsJsonAsync($"api/scheduling/{Esc(patientId)}/appointments/{appointmentId}/cancel", new { });

    // ── Outpatient Pharmacy ──────────────────────────────────────────────

    public Task<List<PrescriptionIndexEntry>> GetPrescriptionsAsync(string patientId)
        => GetListAsync<PrescriptionIndexEntry>($"api/outpatientpharmacy/{Esc(patientId)}/prescriptions");

    public Task<PharmacyState?> GetPrescriptionAsync(string patientId, string prescriptionId)
        => _http.GetFromJsonAsync<PharmacyState>($"api/outpatientpharmacy/{Esc(patientId)}/prescriptions/{prescriptionId}", _json);

    public async Task VerifyPrescriptionAsync(string patientId, string prescriptionId, object request)
        => await _http.PostAsJsonAsync($"api/outpatientpharmacy/{Esc(patientId)}/prescriptions/{prescriptionId}/verify", request);

    public async Task DiscontinuePrescriptionAsync(string patientId, string prescriptionId, object request)
        => await _http.PostAsJsonAsync($"api/outpatientpharmacy/{Esc(patientId)}/prescriptions/{prescriptionId}/discontinue", request);

    public async Task HoldPrescriptionAsync(string patientId, string prescriptionId, object request)
        => await _http.PostAsJsonAsync($"api/outpatientpharmacy/{Esc(patientId)}/prescriptions/{prescriptionId}/hold", request);

    // ── BCMA ─────────────────────────────────────────────────────────────

    public Task<List<MarEntry>> GetPatientMARAsync(string patientId)
        => GetListAsync<MarEntry>($"api/bcma/{Esc(patientId)}/mar");

    // ── PCE (Patient Care Encounters) ────────────────────────────────────

    public Task<List<PceVisitEntry>> GetEncounterListAsync(string patientId)
        => GetListAsync<PceVisitEntry>($"api/pce/{Esc(patientId)}/visits");

    // ── ADT ──────────────────────────────────────────────────────────────

    public Task<List<AdtSummary>> GetAdtMovementsAsync(string patientId)
        => GetListAsync<AdtSummary>($"api/adt/{Esc(patientId)}/movements");

    public Task<List<WardLocationEntry>> GetWardListAsync()
        => GetListAsync<WardLocationEntry>("api/adt/wards");

    // ── Drug Formulary (NDF reference data) ──────────────────────────────

    public Task<List<VaProductIndexEntry>> SearchFormularyAsync(string term, bool formularyOnly = false, bool activeOnly = true, int max = 50)
        => GetListAsync<VaProductIndexEntry>($"api/drugformulary/search?term={Uri.EscapeDataString(term)}&formularyOnly={formularyOnly}&activeOnly={activeOnly}&max={max}");

    public Task<VaProductState?> GetProductAsync(string ien)
        => _http.GetFromJsonAsync<VaProductState>($"api/drugformulary/products/{ien}", _json);

    // ── Drug File ────────────────────────────────────────────────────────

    public Task<List<DrugIndexEntry>> SearchDrugsAsync(string term, int max = 50)
        => GetListAsync<DrugIndexEntry>($"api/drugfile/search?term={Uri.EscapeDataString(term)}&max={max}");

    public Task<DrugState?> GetDrugAsync(string ien)
        => _http.GetFromJsonAsync<DrugState>($"api/drugfile/drugs/{ien}", _json);

    public Task<List<OrderableItemIndexEntry>> GetOrderableItemsAsync(int max = 100)
        => GetListAsync<OrderableItemIndexEntry>($"api/drugfile/orderable-items?max={max}");

    // ── Drug Accountability ──────────────────────────────────────────────

    public Task<List<DrugBalanceSummary>> GetDrugAccountabilityAsync(string locationId)
        => GetListAsync<DrugBalanceSummary>($"api/drugaccountability/locations/{locationId}/drugs");

    public Task<DrugAccountabilityState?> GetDrugAccountabilityDetailAsync(string locationId, string drugId)
        => _http.GetFromJsonAsync<DrugAccountabilityState>($"api/drugaccountability/locations/{locationId}/drugs/{drugId}", _json);

    public async Task RecordTransactionAsync(string locationId, string drugId, object request)
        => await _http.PostAsJsonAsync($"api/drugaccountability/locations/{locationId}/drugs/{drugId}/transactions", request);

    // ── ICD-10 Reference ─────────────────────────────────────────────────

    public Task<List<Icd10IndexEntry>> SearchIcd10Async(string term, bool billableOnly = false, int max = 50)
        => GetListAsync<Icd10IndexEntry>($"api/icd10/search?term={Uri.EscapeDataString(term)}&billableOnly={billableOnly}&max={max}");

    public Task<Icd10State?> GetIcd10CodeAsync(string code)
        => _http.GetFromJsonAsync<Icd10State>($"api/icd10/{Uri.EscapeDataString(code)}", _json);

    // ── Pharmacy Benefits ────────────────────────────────────────────────

    public Task<PatientBenefitPlanState?> GetBenefitPlanAsync(string patientId)
        => _http.GetFromJsonAsync<PatientBenefitPlanState>($"api/pharmacybenefits/patients/{Esc(patientId)}/plan", _json);

    // ── Audit Trail ──────────────────────────────────────────────────────

    public Task<List<AuditEventSummary>> GetAuditEventsAsync(string patientId, int max = 100)
        => GetListAsync<AuditEventSummary>($"api/patient/{Esc(patientId)}/audit?max={max}");

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

    // ── Helpers ──────────────────────────────────────────────────────────

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

// ── Auth DTOs ────────────────────────────────────────────────────────────────

public record LoginResponse(
    string Token,
    string UserId,
    string UserName,
    string DisplayName,
    string? UserClass,
    bool HasElectronicSignature,
    bool MfaRequired = false,
    string? MfaChallengeToken = null);
