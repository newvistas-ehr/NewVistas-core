// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
