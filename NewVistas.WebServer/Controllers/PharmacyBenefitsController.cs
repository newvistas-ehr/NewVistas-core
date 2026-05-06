// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/pharmacybenefits")]
public class PharmacyBenefitsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PharmacyBenefitsController> _logger;

    public PharmacyBenefitsController(IGrainFactory grainFactory,
        ILogger<PharmacyBenefitsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private IPatientBenefitPlanGrain GetPlanGrain(string patientId) =>
        _grainFactory.GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");

    private IFormularyIndexGrain GetFormularyGrain(string planId) =>
        _grainFactory.GetGrain<IFormularyIndexGrain>($"PBM-FORMULARY:{planId}");

    private IPriorAuthorizationGrain GetPaGrain(string paId) =>
        _grainFactory.GetGrain<IPriorAuthorizationGrain>($"PA:{paId}");

    private IPriorAuthIndexGrain GetPaIndexGrain(string patientId) =>
        _grainFactory.GetGrain<IPriorAuthIndexGrain>($"PA-INDEX:{patientId}");

    private async Task SyncPaIndex(string patientId, PriorAuthorizationState pa)
    {
        await GetPaIndexGrain(patientId).AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = pa.PaId,
            DrugName = pa.DrugName,
            Status = pa.Status,
            RequestDate = pa.RequestDate,
            ExpirationDate = pa.ExpirationDate,
            ProviderName = pa.ProviderName,
            DrugId = pa.DrugId
        });
    }

    // ─── Patient Benefit Plan ──────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/plan")]
    public async Task<IActionResult> GetPlan(string patientId)
    {
        try
        {
            PatientBenefitPlanState plan = await GetPlanGrain(patientId).GetPlanAsync();
            return Ok(new BenefitPlanDto(plan));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting benefit plan for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("patients/{patientId}/plan")]
    public async Task<IActionResult> SetPlan(string patientId, [FromBody] PbmSetPlanRequest req)
    {
        try
        {
            await GetPlanGrain(patientId).SetPlanAsync(req.PlanId, req.PlanName, req.InsuranceName,
                req.GroupNumber, req.MemberId, req.EffectiveDate, req.TerminationDate,
                req.CopayTier1, req.CopayTier2, req.CopayTier3, req.DaySupplyLimit, req.AnnualDeductible);
            return Created($"api/pharmacybenefits/patients/{patientId}/plan", new { patientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting benefit plan for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("patients/{patientId}/plan/deactivate")]
    public async Task<IActionResult> DeactivatePlan(string patientId)
    {
        try
        {
            await GetPlanGrain(patientId).DeactivateAsync();
            return Ok(new { patientId, status = "DEACTIVATED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating plan for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Coverage Check ────────────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/plan/coverage/{drugId}")]
    public async Task<IActionResult> CheckCoverage(string patientId, string drugId)
    {
        try
        {
            PatientBenefitPlanState plan = await GetPlanGrain(patientId).GetPlanAsync();
            if (!plan.IsActive || string.IsNullOrEmpty(plan.PlanId))
                return Ok(new CoverageCheckDto(drugId, string.Empty, "NOT_COVERED", 0, false, null, null, null));

            FormularyEntry? entry = await GetFormularyGrain(plan.PlanId).GetDrugAsync(drugId);
            if (entry is null)
                return Ok(new CoverageCheckDto(drugId, string.Empty, "NOT_COVERED", 0, false, null, null, null));

            decimal copay = entry.Tier switch
            {
                1 => entry.CopayOverride ?? plan.CopayTier1,
                2 => entry.CopayOverride ?? plan.CopayTier2,
                3 => entry.CopayOverride ?? plan.CopayTier3,
                _ => 0
            };

            return Ok(new CoverageCheckDto(drugId, entry.DrugName, entry.CoverageStatus, entry.Tier,
                entry.RequiresPriorAuth, copay, entry.QuantityLimit, entry.DaySupplyLimit));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking coverage for drug {DrugId}", drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Prior Authorizations ──────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/priorauths")]
    public async Task<IActionResult> GetAllPas(string patientId)
    {
        try
        {
            List<PriorAuthIndexEntry> entries = await GetPaIndexGrain(patientId).GetAllAsync();
            return Ok(entries.Select(e => new PriorAuthSummaryDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PAs for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("patients/{patientId}/priorauths/pending")]
    public async Task<IActionResult> GetPendingPas(string patientId)
    {
        try
        {
            List<PriorAuthIndexEntry> entries = await GetPaIndexGrain(patientId).GetPendingAsync();
            return Ok(entries.Select(e => new PriorAuthSummaryDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending PAs for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("patients/{patientId}/priorauths")]
    public async Task<IActionResult> SubmitPa(string patientId, [FromBody] PbmSubmitPaRequest req)
    {
        try
        {
            string paId = Guid.NewGuid().ToString();
            IPriorAuthorizationGrain paGrain = GetPaGrain(paId);
            await paGrain.SubmitRequestAsync(patientId, req.DrugId, req.DrugName, req.PlanId,
                req.ProviderId, req.ProviderName, req.DiagnosisCodes, req.ClinicalJustification);

            PriorAuthorizationState pa = await paGrain.GetAsync();
            await SyncPaIndex(patientId, pa);

            return Created($"api/pharmacybenefits/patients/{patientId}/priorauths", new { paId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting PA for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("patients/{patientId}/priorauths/{paId}/approve")]
    public async Task<IActionResult> ApprovePa(string patientId, string paId,
        [FromBody] PbmApprovePaRequest req)
    {
        try
        {
            IPriorAuthorizationGrain paGrain = GetPaGrain(paId);
            await paGrain.ApproveAsync(req.ReviewerId, req.ReviewerName, req.Notes, req.ExpirationDate);
            PriorAuthorizationState pa = await paGrain.GetAsync();
            await SyncPaIndex(patientId, pa);
            return Ok(new { paId, status = "APPROVED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving PA {PaId}", paId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("patients/{patientId}/priorauths/{paId}/deny")]
    public async Task<IActionResult> DenyPa(string patientId, string paId,
        [FromBody] PbmDenyPaRequest req)
    {
        try
        {
            IPriorAuthorizationGrain paGrain = GetPaGrain(paId);
            await paGrain.DenyAsync(req.ReviewerId, req.ReviewerName, req.Reason);
            PriorAuthorizationState pa = await paGrain.GetAsync();
            await SyncPaIndex(patientId, pa);
            return Ok(new { paId, status = "DENIED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying PA {PaId}", paId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Formulary ─────────────────────────────────────────────────────────────

    [HttpGet("formulary/{planId}")]
    public async Task<IActionResult> GetFormulary(string planId)
    {
        try
        {
            List<FormularyEntry> entries = await GetFormularyGrain(planId).GetAllAsync();
            return Ok(entries.Select(e => new FormularyEntryDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting formulary for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("formulary/{planId}/tier/{tier}")]
    public async Task<IActionResult> GetFormularyByTier(string planId, int tier)
    {
        try
        {
            List<FormularyEntry> entries = await GetFormularyGrain(planId).GetByTierAsync(tier);
            return Ok(entries.Select(e => new FormularyEntryDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tier {Tier} formulary for plan {PlanId}", tier, planId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("formulary/{planId}/priorauth")]
    public async Task<IActionResult> GetFormularyRequiringPa(string planId)
    {
        try
        {
            List<FormularyEntry> entries = await GetFormularyGrain(planId).GetRequiringPriorAuthAsync();
            return Ok(entries.Select(e => new FormularyEntryDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PA-required formulary entries for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("formulary/{planId}/entries")]
    public async Task<IActionResult> AddFormularyEntry(string planId,
        [FromBody] PbmAddFormularyEntryRequest req)
    {
        try
        {
            await GetFormularyGrain(planId).AddOrUpdateEntryAsync(new FormularyEntry
            {
                DrugId = req.DrugId,
                DrugName = req.DrugName,
                Tier = req.Tier,
                CoverageStatus = req.CoverageStatus,
                RequiresPriorAuth = req.RequiresPriorAuth,
                CopayOverride = req.CopayOverride,
                QuantityLimit = req.QuantityLimit,
                DaySupplyLimit = req.DaySupplyLimit,
                StepTherapyRequired = req.StepTherapyRequired,
                StepTherapyNotes = req.StepTherapyNotes,
                Notes = req.Notes
            });
            return Created($"api/pharmacybenefits/formulary/{planId}/entries", new { planId, req.DrugId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding formulary entry for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ──────────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo([FromQuery] string patientId = "P-DEMO-001")
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            string planId = "TRICARE-STD";

            // Set patient benefit plan
            await GetPlanGrain(patientId).SetPlanAsync(
                planId, "TRICARE Standard", "Defense Health Agency",
                groupNumber: "GRP-VA-001", memberId: $"MBR-{patientId}",
                effectiveDate: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                terminationDate: new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                copayTier1: 0m, copayTier2: 11m, copayTier3: 22m,
                daySupplyLimit: 90, annualDeductible: 150m);

            // Seed 10 formulary entries
            var formularyEntries = new[]
            {
                new { DrugId = "50-METFORMIN",   Name = "Metformin 500mg",      Tier = 1, Status = "COVERED",      Pa = false, Copay = (decimal?)null, Qty = (int?)null },
                new { DrugId = "50-LISINOPRIL",  Name = "Lisinopril 10mg",      Tier = 1, Status = "COVERED",      Pa = false, Copay = (decimal?)null, Qty = (int?)null },
                new { DrugId = "50-ASPIRIN",     Name = "Aspirin 81mg",         Tier = 1, Status = "COVERED",      Pa = false, Copay = (decimal?)null, Qty = (int?)180 },
                new { DrugId = "50-ATORVASTATIN",Name = "Atorvastatin 20mg",    Tier = 1, Status = "COVERED",      Pa = false, Copay = (decimal?)null, Qty = (int?)null },
                new { DrugId = "50-AMLODIPINE",  Name = "Amlodipine 5mg",       Tier = 2, Status = "COVERED",      Pa = false, Copay = (decimal?)null, Qty = (int?)null },
                new { DrugId = "50-SERTRALINE",  Name = "Sertraline 50mg",      Tier = 2, Status = "COVERED",      Pa = false, Copay = (decimal?)null, Qty = (int?)null },
                new { DrugId = "50-WARFARIN",    Name = "Warfarin 5mg",         Tier = 2, Status = "COVERED",      Pa = false, Copay = (decimal?)null, Qty = (int?)null },
                new { DrugId = "50-DULOXETINE",  Name = "Duloxetine 30mg",      Tier = 3, Status = "COVERED",      Pa = false, Copay = (decimal?)22m,  Qty = (int?)null },
                new { DrugId = "50-ADALIMUMAB",  Name = "Adalimumab (Humira)",  Tier = 3, Status = "REQUIRES_PA",  Pa = true,  Copay = (decimal?)null, Qty = (int?)2   },
                new { DrugId = "50-ECULIZUMAB",  Name = "Eculizumab (Soliris)", Tier = 3, Status = "REQUIRES_PA",  Pa = true,  Copay = (decimal?)null, Qty = (int?)null },
            };

            IFormularyIndexGrain formularyGrain = GetFormularyGrain(planId);
            foreach (var e in formularyEntries)
            {
                await formularyGrain.AddOrUpdateEntryAsync(new FormularyEntry
                {
                    DrugId = e.DrugId,
                    DrugName = e.Name,
                    Tier = e.Tier,
                    CoverageStatus = e.Status,
                    RequiresPriorAuth = e.Pa,
                    CopayOverride = e.Copay,
                    QuantityLimit = e.Qty
                });
            }

            // Submit one PENDING PA for Adalimumab
            string paId = Guid.NewGuid().ToString();
            IPriorAuthorizationGrain paGrain = GetPaGrain(paId);
            await paGrain.SubmitRequestAsync(patientId, "50-ADALIMUMAB", "Adalimumab (Humira)",
                planId, "PROV-007", "Dr. Sarah Lee",
                new List<string> { "M05.79", "M06.9" },
                "Patient has failed first-line DMARDs including methotrexate. RA symptoms uncontrolled.");
            PriorAuthorizationState pa = await paGrain.GetAsync();
            await SyncPaIndex(patientId, pa);

            return Ok(new
            {
                message = "Demo PBM data loaded",
                patientId,
                planId,
                formularyDrugs = formularyEntries.Length,
                priorAuthsPending = 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading PBM demo data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record BenefitPlanDto(
    string PatientId,
    string PlanId,
    string PlanName,
    string InsuranceName,
    string? GroupNumber,
    string? MemberId,
    DateTime? EffectiveDate,
    DateTime? TerminationDate,
    bool IsActive,
    decimal CopayTier1,
    decimal CopayTier2,
    decimal CopayTier3,
    int DaySupplyLimit,
    decimal AnnualDeductible,
    bool DeductibleMet)
{
    public BenefitPlanDto(PatientBenefitPlanState s)
        : this(s.PatientId, s.PlanId, s.PlanName, s.InsuranceName, s.GroupNumber, s.MemberId,
               s.EffectiveDate, s.TerminationDate, s.IsActive, s.CopayTier1, s.CopayTier2,
               s.CopayTier3, s.DaySupplyLimit, s.AnnualDeductible, s.DeductibleMet) { }
}

public record PbmSetPlanRequest(
    string PlanId,
    string PlanName,
    string InsuranceName,
    string? GroupNumber,
    string? MemberId,
    DateTime? EffectiveDate,
    DateTime? TerminationDate,
    decimal CopayTier1,
    decimal CopayTier2,
    decimal CopayTier3,
    int DaySupplyLimit,
    decimal AnnualDeductible);

public record FormularyEntryDto(
    string DrugId,
    string DrugName,
    int Tier,
    string CoverageStatus,
    bool RequiresPriorAuth,
    decimal? CopayOverride,
    int? QuantityLimit,
    int? DaySupplyLimit,
    bool StepTherapyRequired,
    string? Notes)
{
    public FormularyEntryDto(FormularyEntry e)
        : this(e.DrugId, e.DrugName, e.Tier, e.CoverageStatus, e.RequiresPriorAuth,
               e.CopayOverride, e.QuantityLimit, e.DaySupplyLimit, e.StepTherapyRequired, e.Notes) { }
}

public record PbmAddFormularyEntryRequest(
    string DrugId,
    string DrugName,
    int Tier,
    string CoverageStatus,
    bool RequiresPriorAuth,
    decimal? CopayOverride,
    int? QuantityLimit,
    int? DaySupplyLimit,
    bool StepTherapyRequired,
    string? StepTherapyNotes,
    string? Notes);

public record CoverageCheckDto(
    string DrugId,
    string DrugName,
    string CoverageStatus,
    int Tier,
    bool RequiresPriorAuth,
    decimal? ApplicableCopay,
    int? QuantityLimit,
    int? DaySupplyLimit);

public record PriorAuthSummaryDto(
    string PaId,
    string DrugName,
    string Status,
    DateTime RequestDate,
    DateTime? ExpirationDate,
    string? ProviderName,
    string? DrugId)
{
    public PriorAuthSummaryDto(PriorAuthIndexEntry e)
        : this(e.PaId, e.DrugName, e.Status, e.RequestDate, e.ExpirationDate, e.ProviderName, e.DrugId) { }
}

public record PbmSubmitPaRequest(
    string? DrugId,
    string DrugName,
    string? PlanId,
    string? ProviderId,
    string? ProviderName,
    List<string> DiagnosisCodes,
    string? ClinicalJustification);

public record PbmApprovePaRequest(
    string ReviewerId,
    string ReviewerName,
    string? Notes,
    DateTime? ExpirationDate);

public record PbmDenyPaRequest(
    string ReviewerId,
    string ReviewerName,
    string Reason);
