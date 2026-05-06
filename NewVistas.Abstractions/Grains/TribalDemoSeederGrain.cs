// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Implements <see cref="ITribalDemoSeederGrain"/>. Stateless orchestrator —
/// no <c>IPersistentState</c> needed; each call walks the manifest fresh.
///
/// ICN derivation: each patient gets a deterministic ICN based on their
/// position in the manifest, so re-running the seeder produces the same
/// ICN per patient (idempotent on identity). Format:
/// <c>"099" + (1000000 + index):D7 + "V" + checksum</c> where index is 0-based.
/// </summary>
public class TribalDemoSeederGrain : Grain, ITribalDemoSeederGrain
{
    private readonly ILogger<TribalDemoSeederGrain> _logger;

    public TribalDemoSeederGrain(ILogger<TribalDemoSeederGrain> logger)
    {
        _logger = logger;
    }

    public async Task<TribalDemoSeedResult> LoadAsync(
        string manifestDirectory, string seededByUserId, string seededByUserName)
    {
        if (string.IsNullOrWhiteSpace(manifestDirectory))
            throw new ArgumentException("manifestDirectory is required.", nameof(manifestDirectory));
        if (!Directory.Exists(manifestDirectory))
            throw new DirectoryNotFoundException($"Manifest directory not found: {manifestDirectory}");

        var result = new TribalDemoSeedResult();
        var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // ── Patients ────────────────────────────────────────────────────────
        string patientsPath = Path.Combine(manifestDirectory, "patients.json");
        if (File.Exists(patientsPath))
        {
            string json = await File.ReadAllTextAsync(patientsPath);
            List<PatientManifestEntry> entries =
                JsonSerializer.Deserialize<List<PatientManifestEntry>>(json, jsonOpts)
                ?? new List<PatientManifestEntry>();

            IPatientRegistrationGrain registration =
                GrainFactory.GetGrain<IPatientRegistrationGrain>("REGISTRATION");

            for (int i = 0; i < entries.Count; i++)
            {
                PatientManifestEntry e = entries[i];
                try
                {
                    string icn = DeriveDemoIcn(i);
                    var req = new RegistrationRequest
                    {
                        PatientName = e.PatientName,
                        Ssn = e.Ssn ?? string.Empty,
                        DateOfBirth = ParseDateOrNull(e.DateOfBirth),
                        Sex = e.Sex,
                        FacilityDfn = e.FacilityDfn,
                        ExternallySuppliedIcn = icn,        // idempotent identity
                        IsTribalMember = e.IsTribalMember,
                        TribalAffiliation = e.TribalAffiliation,
                        CdibNumber = e.CdibNumber,
                        ResidesInChsda = e.ResidesInChsda,
                        ChsdaResidencyDays = e.ChsdaResidencyDays,
                        IhsEligibleByCategory = e.IhsEligibleByCategory,
                    };
                    string returnedIcn = await registration.RegisterPatientAsync(req);
                    result.PatientIcns.Add(returnedIcn);
                    result.PatientsRegistered++;
                }
                catch (Exception ex)
                {
                    string msg = $"patients.json[{i}] '{e.PatientName}': {ex.GetType().Name}: {ex.Message}";
                    _logger.LogWarning(ex, "Tribal demo patient seed failed: {Msg}", msg);
                    result.Errors.Add(msg);
                }
            }
        }

        // ── CHS Referrals ───────────────────────────────────────────────────
        string referralsPath = Path.Combine(manifestDirectory, "chs-referrals.json");
        if (File.Exists(referralsPath))
        {
            string json = await File.ReadAllTextAsync(referralsPath);
            List<ChsReferralManifestEntry> entries =
                JsonSerializer.Deserialize<List<ChsReferralManifestEntry>>(json, jsonOpts)
                ?? new List<ChsReferralManifestEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                ChsReferralManifestEntry e = entries[i];
                try
                {
                    // 1-based patientIndex in the manifest → 0-based ICN derivation
                    int icnIndex = e.PatientIndex - 1;
                    if (icnIndex < 0 || icnIndex >= result.PatientIcns.Count)
                    {
                        result.Errors.Add($"chs-referrals.json[{i}]: patientIndex {e.PatientIndex} is out of range (1..{result.PatientIcns.Count}).");
                        continue;
                    }
                    string patientIcn = result.PatientIcns[icnIndex];

                    string referralId = $"EXT-REF:DEMO-{i + 1:D3}";
                    IExternalReferralGrain referral =
                        GrainFactory.GetGrain<IExternalReferralGrain>(referralId);

                    // Look up patient name from PatientGrain so the referral record
                    // matches what's on file.
                    PatientState pat = await GrainFactory.GetGrain<IPatientGrain>(patientIcn).GetPatientAsync();

                    await referral.CreateReferralAsync(
                        patientId: patientIcn, patientName: pat.Name,
                        referralType: e.ReferralType,
                        externalFacilityName: e.ExternalFacilityName, externalFacilityId: null,
                        externalProviderName: e.ExternalProviderName, externalProviderId: null,
                        purpose: e.Purpose, diagnosis: e.Diagnosis, urgency: e.Urgency,
                        referredByProviderId: seededByUserId, referredByProviderName: seededByUserName,
                        consultId: null, authorizationNumber: null,
                        appointmentDateTime: null, specialInstructions: null);
                    result.ChsReferralsCreated++;

                    await referral.RequestChsAuthorizationAsync(
                        e.EstimatedCost, e.MedicalPriorityClass,
                        e.AlternateResourcesChecked, e.AlternateResourcesNote,
                        seededByUserId, seededByUserName);

                    if (e.Approve == true && e.AuthorizedAmount.HasValue)
                    {
                        await referral.ApproveChsAuthorizationAsync(
                            e.AuthorizedAmount.Value, e.AuthorizationNumber,
                            seededByUserId, seededByUserName);
                        result.ChsReferralsApproved++;
                    }
                    else if (e.Approve == false && !string.IsNullOrEmpty(e.DenialReason))
                    {
                        await referral.DenyChsAuthorizationAsync(
                            e.DenialReason, seededByUserId, seededByUserName);
                        result.ChsReferralsDenied++;
                    }
                }
                catch (Exception ex)
                {
                    string msg = $"chs-referrals.json[{i}]: {ex.GetType().Name}: {ex.Message}";
                    _logger.LogWarning(ex, "Tribal demo CHS referral seed failed: {Msg}", msg);
                    result.Errors.Add(msg);
                }
            }
        }

        // ── GPRA Report ─────────────────────────────────────────────────────
        string gpraPath = Path.Combine(manifestDirectory, "gpra-report.json");
        if (File.Exists(gpraPath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(gpraPath);
                GpraReportManifestEntry e =
                    JsonSerializer.Deserialize<GpraReportManifestEntry>(json, jsonOpts)
                    ?? throw new InvalidOperationException("gpra-report.json deserialized to null.");

                IGpraReportGrain report =
                    GrainFactory.GetGrain<IGpraReportGrain>($"GPRA-REPORT:{e.ReportId}");

                await report.CreateAsync(
                    fiscalYear: e.FiscalYear,
                    reportingPeriod: ParseEnum<GpraReportingPeriod>(e.ReportingPeriod),
                    currentPeriodStart: ParseDate(e.CurrentPeriodStart),
                    currentPeriodEnd: ParseDate(e.CurrentPeriodEnd),
                    baselinePeriodStart: ParseDate(e.BaselinePeriodStart),
                    baselinePeriodEnd: ParseDate(e.BaselinePeriodEnd),
                    facilityId: e.FacilityId, facilityName: e.FacilityName,
                    communityTaxonomy: e.CommunityTaxonomy,
                    activeUserPopulation: e.ActiveUserPopulation,
                    generatedById: e.GeneratedById, generatedByName: e.GeneratedByName);

                foreach (GpraIndicatorManifestEntry ind in e.Indicators)
                {
                    await report.AddIndicatorResultAsync(new GpraIndicatorResult
                    {
                        MeasureId = ind.MeasureId,
                        Title = ind.Title,
                        Category = ParseEnum<GpraClinicalCategory>(ind.Category),
                        CurrentDenominator = ind.CurrentDenominator,
                        CurrentNumerator = ind.CurrentNumerator,
                        CurrentPerformanceRate = ind.CurrentPerformanceRate,
                        BaselineDenominator = ind.BaselineDenominator,
                        BaselineNumerator = ind.BaselineNumerator,
                        BaselinePerformanceRate = ind.BaselinePerformanceRate,
                        PercentagePointChange = ind.PercentagePointChange,
                        IsImproved = ind.IsImproved,
                        TargetRate = ind.TargetRate,
                        TargetMet = ind.TargetMet,
                    });
                }

                await report.CompleteAsync();
                result.GpraReportsCreated = 1;
            }
            catch (Exception ex)
            {
                string msg = $"gpra-report.json: {ex.GetType().Name}: {ex.Message}";
                _logger.LogWarning(ex, "Tribal demo GPRA report seed failed: {Msg}", msg);
                result.Errors.Add(msg);
            }
        }

        return result;
    }

    /// <summary>
    /// Compute a deterministic demo ICN from the patient's 0-based position
    /// in the manifest. Format: "099{1000000+index:D7}V{checksum:D6}" — the
    /// 099 prefix is the test-only allocation per ClusterPrefixAllocations.md.
    /// </summary>
    private static string DeriveDemoIcn(int zeroBasedIndex)
    {
        long sequence = 1_000_000L + zeroBasedIndex;
        string prefixAndSeq = "099" + sequence.ToString("D7", CultureInfo.InvariantCulture);
        string checksum = IcnChecksumCalculator.Compute(prefixAndSeq);
        return $"{prefixAndSeq}V{checksum}";
    }

    private static DateTime? ParseDateOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : DateTime.Parse(s, CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture);

    private static T ParseEnum<T>(string s) where T : struct =>
        Enum.TryParse<T>(s, ignoreCase: true, out T v) ? v
        : throw new ArgumentException($"'{s}' is not a valid {typeof(T).Name}.");

    // ── Manifest record types ─────────────────────────────────────────────
    // System.Text.Json deserialization targets; field names match the JSON.

    internal sealed class PatientManifestEntry
    {
        public string PatientName { get; set; } = string.Empty;
        public string? Ssn { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Sex { get; set; }
        public string FacilityDfn { get; set; } = string.Empty;
        public bool? IsTribalMember { get; set; }
        public string? TribalAffiliation { get; set; }
        public string? CdibNumber { get; set; }
        public bool? ResidesInChsda { get; set; }
        public int? ChsdaResidencyDays { get; set; }
        public string? IhsEligibleByCategory { get; set; }
    }

    internal sealed class ChsReferralManifestEntry
    {
        public int PatientIndex { get; set; }
        public string ReferralType { get; set; } = "SPECIALTY";
        public string ExternalFacilityName { get; set; } = string.Empty;
        public string? ExternalProviderName { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string? Diagnosis { get; set; }
        public string Urgency { get; set; } = "ROUTINE";
        public decimal EstimatedCost { get; set; }
        public string MedicalPriorityClass { get; set; } = "III";
        public bool AlternateResourcesChecked { get; set; }
        public string? AlternateResourcesNote { get; set; }
        public bool? Approve { get; set; }
        public decimal? AuthorizedAmount { get; set; }
        public string? AuthorizationNumber { get; set; }
        public string? DenialReason { get; set; }
    }

    internal sealed class GpraReportManifestEntry
    {
        public string ReportId { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public string ReportingPeriod { get; set; } = "FullFiscalYear";
        public string CurrentPeriodStart { get; set; } = string.Empty;
        public string CurrentPeriodEnd { get; set; } = string.Empty;
        public string BaselinePeriodStart { get; set; } = string.Empty;
        public string BaselinePeriodEnd { get; set; } = string.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string? CommunityTaxonomy { get; set; }
        public int ActiveUserPopulation { get; set; }
        public string? GeneratedById { get; set; }
        public string? GeneratedByName { get; set; }
        public List<GpraIndicatorManifestEntry> Indicators { get; set; } = new();
    }

    internal sealed class GpraIndicatorManifestEntry
    {
        public string MeasureId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "Diabetes";
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
}
