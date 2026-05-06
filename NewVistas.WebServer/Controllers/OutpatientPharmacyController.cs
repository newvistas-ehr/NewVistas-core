// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Outpatient Pharmacy (PSO) REST API — prescription fill/refill cycle,
/// label generation, counseling flags, pharmacist verification.
/// </summary>
[Authorize(Roles = "Pharmacist,Provider")]
[ApiController]
[Route("api/outpatientpharmacy")]
public class OutpatientPharmacyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<OutpatientPharmacyController> _logger;

    public OutpatientPharmacyController(IGrainFactory grainFactory, ILogger<OutpatientPharmacyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // -------------------------------------------------------------------
    // List endpoints
    // -------------------------------------------------------------------

    [HttpGet("{patientId}/prescriptions")]
    public async Task<IActionResult> GetPrescriptions(string patientId)
    {
        try
        {
            List<PrescriptionIndexEntry> entries = await GetIndexGrain(patientId).GetAllAsync();
            return Ok(entries.Select(ToSummaryDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting prescriptions for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve prescriptions.");
        }
    }

    [HttpGet("{patientId}/prescriptions/active")]
    public async Task<IActionResult> GetActivePrescriptions(string patientId)
    {
        try
        {
            List<PrescriptionIndexEntry> entries = await GetIndexGrain(patientId).GetActiveAsync();
            return Ok(entries.Select(ToSummaryDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active prescriptions for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to retrieve active prescriptions.");
        }
    }

    [HttpGet("{patientId}/prescriptions/{rxId}")]
    public async Task<IActionResult> GetPrescription(string patientId, string rxId)
    {
        try
        {
            PharmacyState state = await GetRxGrain(rxId).GetPrescriptionAsync();
            return Ok(ToDetailDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting prescription {RxId}", rxId);
            return StatusCode(500, "Failed to retrieve prescription.");
        }
    }

    // -------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------

    [HttpPost("{patientId}/prescriptions")]
    public async Task<IActionResult> CreatePrescription(string patientId, [FromBody] CreatePrescriptionRequest request)
    {
        try
        {
            string rxId = $"RX-{Guid.NewGuid():N}";
            IPharmacyGrain rxGrain = GetRxGrain(rxId);

            await rxGrain.CreatePrescriptionAsync(
                patientId,
                request.DrugName,
                request.DrugId,
                request.Dosage,
                request.Route,
                request.Schedule,
                request.Sig,
                request.DaysSupply,
                request.Quantity,
                request.Refills,
                request.ProviderId,
                request.ProviderName,
                request.PharmacyId,
                request.PharmacyName,
                request.OrderId,
                request.Comments);

            if (!string.IsNullOrWhiteSpace(request.Priority))
                await rxGrain.UpdatePriorityAsync(request.Priority);

            await SyncIndex(patientId, rxId);
            return Created($"api/outpatientpharmacy/{patientId}/prescriptions/{rxId}", new { PrescriptionId = rxId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating prescription for patient {PatientId}", patientId);
            return StatusCode(500, "Failed to create prescription.");
        }
    }

    // -------------------------------------------------------------------
    // Lifecycle actions
    // -------------------------------------------------------------------

    [HttpPost("{patientId}/prescriptions/{rxId}/fill")]
    public async Task<IActionResult> Fill(string patientId, string rxId, [FromBody] FillRequest request)
    {
        try
        {
            await GetWorkflow(patientId).FillPrescriptionWorkflowAsync(rxId, request.FillDate ?? DateTime.UtcNow);
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filling prescription {RxId}", rxId);
            return StatusCode(500, "Failed to fill prescription.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/refill")]
    public async Task<IActionResult> Refill(string patientId, string rxId, [FromBody] FillRequest request)
    {
        try
        {
            await GetWorkflow(patientId).RefillPrescriptionWorkflowAsync(rxId, request.FillDate ?? DateTime.UtcNow);
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refilling prescription {RxId}", rxId);
            return StatusCode(500, "Failed to refill prescription.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/discontinue")]
    public async Task<IActionResult> Discontinue(string patientId, string rxId, [FromBody] DiscontinueRequest request)
    {
        try
        {
            await GetWorkflow(patientId).DiscontinuePrescriptionWorkflowAsync(rxId, request.Reason ?? string.Empty);
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discontinuing prescription {RxId}", rxId);
            return StatusCode(500, "Failed to discontinue prescription.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/hold")]
    public async Task<IActionResult> Hold(string patientId, string rxId, [FromBody] HoldRequest request)
    {
        try
        {
            await GetWorkflow(patientId).HoldPrescriptionWorkflowAsync(rxId, request.Reason ?? string.Empty);
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing prescription {RxId} on hold", rxId);
            return StatusCode(500, "Failed to place prescription on hold.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/resume")]
    public async Task<IActionResult> Resume(string patientId, string rxId)
    {
        try
        {
            await GetWorkflow(patientId).ResumePrescriptionWorkflowAsync(rxId);
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming prescription {RxId}", rxId);
            return StatusCode(500, "Failed to resume prescription.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/verify")]
    public async Task<IActionResult> Verify(string patientId, string rxId, [FromBody] VerifyRequest request)
    {
        try
        {
            await GetWorkflow(patientId).VerifyPrescriptionWorkflowAsync(rxId, request.PharmacistId ?? "UNKNOWN");
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying prescription {RxId}", rxId);
            return StatusCode(500, "Failed to verify prescription.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/printlabel")]
    public async Task<IActionResult> PrintLabel(string patientId, string rxId, [FromBody] PrintLabelRequest request)
    {
        try
        {
            await GetWorkflow(patientId).PrintLabelWorkflowAsync(rxId, request.RxNumber);
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing label for prescription {RxId}", rxId);
            return StatusCode(500, "Failed to print label.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/counseling")]
    public async Task<IActionResult> SetCounseling(string patientId, string rxId, [FromBody] CounselingRequest request)
    {
        try
        {
            await GetRxGrain(rxId).SetCounselingFlagAsync(request.Required);
            await SyncIndex(patientId, rxId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting counseling flag for prescription {RxId}", rxId);
            return StatusCode(500, "Failed to set counseling flag.");
        }
    }

    [HttpGet("{patientId}/prescriptions/{rxId}/pa-status")]
    public async Task<IActionResult> GetPriorAuthStatus(string patientId, string rxId)
    {
        try
        {
            PriorAuthCoverageResult result = await GetWorkflow(patientId).CheckPriorAuthStatusAsync(rxId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking PA status for {RxId}", rxId);
            return StatusCode(500, "Failed to check prior authorization status.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/dispense")]
    public async Task<IActionResult> RecordDispense(string patientId, string rxId, [FromBody] RecordDispenseRequest request)
    {
        try
        {
            await GetWorkflow(patientId).RecordDispenseWorkflowAsync(
                rxId, request.NdcDispensed, request.LotNumber, request.PharmacistId);
            await SyncIndex(patientId, rxId);
            return Ok(new { Message = "Dispense details recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording dispense for {RxId}", rxId);
            return StatusCode(500, "Failed to record dispense details.");
        }
    }

    [HttpPost("{patientId}/prescriptions/{rxId}/counseling/complete")]
    public async Task<IActionResult> RecordCounseling(string patientId, string rxId, [FromBody] RecordCounselingRequest request)
    {
        try
        {
            await GetWorkflow(patientId).RecordCounselingWorkflowAsync(
                rxId, request.PharmacistId, request.Notes);
            await SyncIndex(patientId, rxId);
            return Ok(new { Message = "Counseling recorded." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording counseling for {RxId}", rxId);
            return StatusCode(500, "Failed to record counseling.");
        }
    }

    [HttpGet("{patientId}/prescriptions/{rxId}/label")]
    public async Task<IActionResult> GenerateLabel(string patientId, string rxId)
    {
        try
        {
            PrescriptionLabelContent label = await GetWorkflow(patientId)
                .GenerateLabelContentWorkflowAsync(rxId);
            return Ok(label);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating label for {RxId}", rxId);
            return StatusCode(500, "Failed to generate label.");
        }
    }

    [HttpGet("{patientId}/prescriptions/{rxId}/refill-eligibility")]
    public async Task<IActionResult> GetRefillEligibility(string patientId, string rxId,
        [FromQuery] DateTime? proposedDate = null)
    {
        try
        {
            RefillEligibilityResult result = await GetWorkflow(patientId)
                .GetRefillEligibilityAsync(rxId, proposedDate ?? DateTime.UtcNow);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking refill eligibility for {RxId}", rxId);
            return StatusCode(500, "Failed to check refill eligibility.");
        }
    }

    [HttpGet("{patientId}/prescriptions/{rxId}/refillhistory")]
    public async Task<IActionResult> GetRefillHistory(string patientId, string rxId)
    {
        try
        {
            List<RefillRecord> history = await GetRxGrain(rxId).GetRefillHistoryAsync();
            return Ok(history.Select(r => new RefillHistoryEntryDto(
                r.FillNumber, r.FillDate, r.Quantity, r.DaysSupply,
                r.RxNumber, r.PharmacistId, r.NdcDispensed, r.RecordedDate)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting refill history for prescription {RxId}", rxId);
            return StatusCode(500, "Failed to retrieve refill history.");
        }
    }

    // -------------------------------------------------------------------
    // Demo data
    // -------------------------------------------------------------------

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemoData([FromQuery] string patientId = "P-DEMO-001")
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientPrescriptionIndexGrain indexGrain = GetIndexGrain(patientId);
            await indexGrain.ClearAsync();

            var demos = new[]
            {
                new
                {
                    DrugName = "LISINOPRIL 10MG TAB",
                    DrugId = "50-LISINOPRIL",
                    Dosage = "10MG",
                    Route = "ORAL",
                    Schedule = "DAILY",
                    Sig = "TAKE ONE TABLET BY MOUTH DAILY FOR BLOOD PRESSURE",
                    DaysSupply = 90,
                    Quantity = 90,
                    Refills = 5,
                    Priority = "ROUTINE",
                    ProviderId = "PROV-001",
                    ProviderName = "DR. JANE SMITH"
                },
                new
                {
                    DrugName = "METFORMIN 500MG TAB",
                    DrugId = "50-METFORMIN",
                    Dosage = "500MG",
                    Route = "ORAL",
                    Schedule = "BID",
                    Sig = "TAKE ONE TABLET BY MOUTH TWICE DAILY WITH MEALS",
                    DaysSupply = 30,
                    Quantity = 60,
                    Refills = 11,
                    Priority = "ROUTINE",
                    ProviderId = "PROV-001",
                    ProviderName = "DR. JANE SMITH"
                },
                new
                {
                    DrugName = "ATORVASTATIN 40MG TAB",
                    DrugId = "50-ATORVASTATIN",
                    Dosage = "40MG",
                    Route = "ORAL",
                    Schedule = "QHS",
                    Sig = "TAKE ONE TABLET BY MOUTH AT BEDTIME",
                    DaysSupply = 90,
                    Quantity = 90,
                    Refills = 3,
                    Priority = "ROUTINE",
                    ProviderId = "PROV-002",
                    ProviderName = "DR. MARK JONES"
                }
            };

            List<string> created = new();

            foreach (var demo in demos)
            {
                string rxId = $"RX-DEMO-{Guid.NewGuid():N}";
                IPharmacyGrain rxGrain = GetRxGrain(rxId);

                await rxGrain.CreatePrescriptionAsync(
                    patientId, demo.DrugName, demo.DrugId,
                    demo.Dosage, demo.Route, demo.Schedule, demo.Sig,
                    demo.DaysSupply, demo.Quantity, demo.Refills,
                    demo.ProviderId, demo.ProviderName,
                    "PHARM-001", "MAIN PHARMACY",
                    null, null);

                await rxGrain.UpdatePriorityAsync(demo.Priority);
                await rxGrain.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-10));
                await rxGrain.VerifyAsync("RPH-001");
                await rxGrain.PrintLabelAsync($"RX{DateTime.UtcNow:yyyyMMdd}{created.Count + 1:000}");
                await SyncIndex(patientId, rxId);
                created.Add(rxId);
            }

            return Created("api/outpatientpharmacy/demo/load",
                new { PatientId = patientId, Created = created.Count, PrescriptionIds = created });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo pharmacy data");
            return StatusCode(500, "Failed to load demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPharmacyGrain GetRxGrain(string rxId) =>
        _grainFactory.GetGrain<IPharmacyGrain>(rxId);

    private IPatientPrescriptionIndexGrain GetIndexGrain(string patientId) =>
        _grainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");

    private async Task SyncIndex(string patientId, string rxId)
    {
        PharmacyState state = await GetRxGrain(rxId).GetPrescriptionAsync();
        PrescriptionIndexEntry entry = new()
        {
            PrescriptionId = rxId,
            DrugName = state.DrugName,
            DrugId = state.DrugId,
            Status = state.Status,
            IssueDate = state.IssueDate,
            ExpirationDate = state.ExpirationDate,
            Refills = state.Refills,
            RefillsRemaining = state.RefillsRemaining,
            Priority = state.Priority,
            IsVerified = state.IsVerified,
            CounselingRequired = state.CounselingRequired,
            ProviderId = state.ProviderId,
            ProviderName = state.ProviderName,
            Dosage = state.Dosage
        };
        await GetIndexGrain(patientId).AddOrUpdateEntryAsync(entry);
    }

    private static PrescriptionSummaryDto ToSummaryDto(PrescriptionIndexEntry e) =>
        new(e.PrescriptionId, e.DrugName, e.DrugId, e.Dosage, e.Status,
            e.IssueDate, e.ExpirationDate, e.Refills, e.RefillsRemaining,
            e.Priority, e.IsVerified, e.CounselingRequired, e.ProviderName);

    private static PrescriptionDetailDto ToDetailDto(PharmacyState s) =>
        new(s.PrescriptionId, s.PatientId, s.DrugName, s.DrugId, s.Dosage,
            s.Route, s.Schedule, s.Sig, s.Status, s.IssueDate, s.FillDate,
            s.ExpirationDate, s.LastDispenseDate, s.DaysSupply, s.Quantity,
            s.Refills, s.RefillsRemaining, s.ProviderId, s.ProviderName,
            s.PharmacyId, s.PharmacyName, s.OrderId, s.Comments,
            s.RxNumber, s.Priority, s.IsVerified, s.VerifiedBy, s.VerifiedDate,
            s.CounselingRequired, s.IsLabelPrinted, s.LabelPrintedDate,
            s.NdcDispensed, s.LotNumber, s.DiscontinueReason, s.HoldReason,
            s.HoldDate, s.CreatedDate, s.LastModifiedDate);
}

// -------------------------------------------------------------------
// DTOs
// -------------------------------------------------------------------

public record PrescriptionSummaryDto(
    string PrescriptionId,
    string DrugName,
    string? DrugId,
    string? Dosage,
    string Status,
    DateTime? IssueDate,
    DateTime? ExpirationDate,
    int? Refills,
    int? RefillsRemaining,
    string Priority,
    bool IsVerified,
    bool CounselingRequired,
    string? ProviderName);

public record PrescriptionDetailDto(
    string PrescriptionId,
    string PatientId,
    string DrugName,
    string? DrugId,
    string? Dosage,
    string? Route,
    string? Schedule,
    string? Sig,
    string Status,
    DateTime? IssueDate,
    DateTime? FillDate,
    DateTime? ExpirationDate,
    DateTime? LastDispenseDate,
    int? DaysSupply,
    int? Quantity,
    int? Refills,
    int? RefillsRemaining,
    string? ProviderId,
    string? ProviderName,
    string? PharmacyId,
    string? PharmacyName,
    string? OrderId,
    string? Comments,
    string? RxNumber,
    string Priority,
    bool IsVerified,
    string? VerifiedBy,
    DateTime? VerifiedDate,
    bool CounselingRequired,
    bool IsLabelPrinted,
    DateTime? LabelPrintedDate,
    string? NdcDispensed,
    string? LotNumber,
    string? DiscontinueReason,
    string? HoldReason,
    DateTime? HoldDate,
    DateTime CreatedDate,
    DateTime LastModifiedDate);

public record CreatePrescriptionRequest(
    string DrugName,
    string? DrugId,
    string? Dosage,
    string? Route,
    string? Schedule,
    string? Sig,
    int? DaysSupply,
    int? Quantity,
    int? Refills,
    string? ProviderId,
    string? ProviderName,
    string? PharmacyId,
    string? PharmacyName,
    string? OrderId,
    string? Comments,
    string? Priority);

public record FillRequest(DateTime? FillDate);
public record DiscontinueRequest(string? Reason);
public record HoldRequest(string? Reason);
public record VerifyRequest(string? PharmacistId);
public record PrintLabelRequest(string? RxNumber);
public record CounselingRequest(bool Required);

public record RecordDispenseRequest(string? NdcDispensed, string? LotNumber, string? PharmacistId);
public record RecordCounselingRequest(string PharmacistId, string? Notes);

public record RefillHistoryEntryDto(
    int FillNumber,
    DateTime FillDate,
    int? Quantity,
    int? DaysSupply,
    string? RxNumber,
    string? PharmacistId,
    string? NdcDispensed,
    DateTime RecordedDate);
