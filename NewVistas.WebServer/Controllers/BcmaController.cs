// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// BCMA — Bar Code Medication Administration controller.
/// Exposes the Medication Administration Record (MAR) and administration history
/// for a patient. Maps to VistA PSB package, File #53.79.
///
/// Routes:
///   GET  api/bcma/{patientId}/mar                          - Full MAR
///   GET  api/bcma/{patientId}/mar/due                      - Due within 60 min
///   POST api/bcma/{patientId}/mar/{orderId}/administer     - Administer a dose
///   POST api/bcma/{patientId}/mar/{orderId}/sync           - Sync order into MAR
///   POST api/bcma/{patientId}/mar/{orderId}/deactivate     - Deactivate order on MAR
///   GET  api/bcma/{patientId}/history                      - Administration history
///   GET  api/bcma/{patientId}/history/{bcmaId}             - Single record detail
///   POST api/bcma/{patientId}/history                      - Standalone administration
///   POST api/bcma/{patientId}/history/{bcmaId}/witness     - Record witness
///   POST api/bcma/{patientId}/history/{bcmaId}/prn-reason  - Record PRN reason
///   POST api/bcma/{patientId}/history/{bcmaId}/effectiveness - PRN effectiveness
///   POST api/bcma/{patientId}/demo/load                    - Load demo data
/// </summary>
[Authorize]
[ApiController]
[Route("api/bcma/{patientId}")]
[Produces("application/json")]
public class BcmaController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<BcmaController> _log;

    public BcmaController(IGrainFactory gf, ILogger<BcmaController> log)
    {
        _gf = gf;
        _log = log;
    }

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _gf.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientMARGrain MARGrain(string patientId) =>
        _gf.GetGrain<IPatientMARGrain>($"BCMA-MAR:{patientId}");

    private IBcmaGrain BcmaGrain(string bcmaId) =>
        _gf.GetGrain<IBcmaGrain>(bcmaId);

    // ─── MAR Endpoints ───────────────────────────────────────────────────

    [HttpGet("mar")]
    public async Task<IActionResult> GetMAR(string patientId)
    {
        try
        {
            List<MarEntry> entries = await Workflow(patientId).GetPatientMARAsync();
            return Ok(entries.Select(ToMarDto));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving MAR for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving medication administration record.");
        }
    }

    [HttpGet("mar/due")]
    public async Task<IActionResult> GetDue(string patientId)
    {
        try
        {
            List<MarEntry> due = await Workflow(patientId).GetDueMedicationsAsync();
            return Ok(due.Select(ToMarDto));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving due medications for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving due medications.");
        }
    }

    [HttpPost("mar/{orderId}/administer")]
    public async Task<IActionResult> Administer(
        string patientId, string orderId, [FromBody] BcmaAdministerRequest request)
    {
        try
        {
            string bcmaId = await Workflow(patientId).AdministerMedicationAsync(
                orderId,
                request.ActionStatus,
                request.AdministrationDateTime ?? DateTime.UtcNow,
                request.AdministeredById,
                request.AdministeredByName,
                request.InjectionSite,
                request.PrnReason,
                request.Comments);

            return Created($"api/bcma/{patientId}/history/{bcmaId}", new { BcmaId = bcmaId });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error administering medication for patient {PatientId} order {OrderId}",
                patientId, orderId);
            return StatusCode(500, "Error recording medication administration.");
        }
    }

    [HttpPost("mar/{orderId}/sync")]
    public async Task<IActionResult> SyncOrder(string patientId, string orderId)
    {
        try
        {
            await Workflow(patientId).SyncOrderToMARAsync(orderId);
            return Ok(new { orderId, Message = "Order synced to MAR." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error syncing order {OrderId} to MAR for patient {PatientId}",
                orderId, patientId);
            return StatusCode(500, "Error syncing order to MAR.");
        }
    }

    [HttpPost("mar/{orderId}/deactivate")]
    public async Task<IActionResult> DeactivateOrder(string patientId, string orderId)
    {
        try
        {
            await Workflow(patientId).DeactivateOrderOnMARAsync(orderId);
            return Ok(new { orderId, Message = "Order deactivated on MAR." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error deactivating order {OrderId} on MAR for patient {PatientId}",
                orderId, patientId);
            return StatusCode(500, "Error deactivating order on MAR.");
        }
    }

    // ─── History Endpoints ────────────────────────────────────────────────

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(string patientId, [FromQuery] int max = 100)
    {
        try
        {
            List<BcmaSummary> history = await Workflow(patientId).GetMedicationAdministrationsAsync(max);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving BCMA history for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving administration history.");
        }
    }

    [HttpGet("history/{bcmaId}")]
    public async Task<IActionResult> GetDetail(string patientId, string bcmaId)
    {
        try
        {
            BcmaState detail = await BcmaGrain(bcmaId).GetAdministrationAsync();
            return Ok(ToDetailDto(detail));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving BCMA record {BcmaId}", bcmaId);
            return StatusCode(500, "Error retrieving administration record.");
        }
    }

    [HttpPost("history")]
    public async Task<IActionResult> RecordStandalone(
        string patientId, [FromBody] BcmaStandaloneRequest request)
    {
        try
        {
            string bcmaId = await Workflow(patientId).RecordMedicationAdministrationAsync(
                request.DrugName, request.DrugId, request.Dosage, request.Route,
                request.ActionStatus, request.ScheduledDateTime,
                request.AdministrationDateTime ?? DateTime.UtcNow,
                request.AdministeredById, request.AdministeredByName,
                request.InjectionSite, request.PrescriptionId, request.OrderId, request.Comments);

            return Created($"api/bcma/{patientId}/history/{bcmaId}", new { BcmaId = bcmaId });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording standalone BCMA for patient {PatientId}", patientId);
            return StatusCode(500, "Error recording medication administration.");
        }
    }

    [HttpPost("history/{bcmaId}/witness")]
    public async Task<IActionResult> RecordWitness(
        string patientId, string bcmaId, [FromBody] BcmaWitnessRequest request)
    {
        try
        {
            await BcmaGrain(bcmaId).RecordWitnessAsync(
                request.WitnessId ?? string.Empty, request.WitnessName ?? string.Empty);
            return Ok(new { bcmaId, Message = "Witness recorded." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording witness for BCMA {BcmaId}", bcmaId);
            return StatusCode(500, "Error recording witness.");
        }
    }

    [HttpPost("history/{bcmaId}/prn-reason")]
    public async Task<IActionResult> RecordPrnReason(
        string patientId, string bcmaId, [FromBody] BcmaPrnRequest request)
    {
        try
        {
            await BcmaGrain(bcmaId).RecordPrnReasonAsync(request.Reason ?? string.Empty);
            return Ok(new { bcmaId, Message = "PRN reason recorded." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording PRN reason for BCMA {BcmaId}", bcmaId);
            return StatusCode(500, "Error recording PRN reason.");
        }
    }

    [HttpPost("history/{bcmaId}/effectiveness")]
    public async Task<IActionResult> RecordEffectiveness(
        string patientId, string bcmaId, [FromBody] BcmaEffectivenessRequest request)
    {
        try
        {
            await BcmaGrain(bcmaId).RecordPrnEffectivenessAsync(request.Effectiveness ?? string.Empty);
            return Ok(new { bcmaId, Message = "PRN effectiveness recorded." });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording effectiveness for BCMA {BcmaId}", bcmaId);
            return StatusCode(500, "Error recording effectiveness.");
        }
    }

    // ─── Demo Load ────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo(string patientId)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientMARGrain mar = MARGrain(patientId);
            IPatientWorkflowGrain workflow = Workflow(patientId);

            DateTime now = DateTime.UtcNow;

            // Seed the MAR with representative inpatient orders
            MarEntry[] demoEntries =
            [
                new MarEntry
                {
                    OrderId = $"PSJ-DEMO-{Guid.NewGuid():N}",
                    DrugName = "Metoprolol Tartrate",
                    Dosage = "25mg",
                    Route = "PO",
                    Schedule = "BID",
                    OrderType = "UNIT_DOSE",
                    Priority = "ROUTINE",
                    WardId = "WARD-MED-3A",
                    RoomBed = "301-A",
                    ProviderName = "Dr. Cardiology",
                    StartDate = now.AddDays(-2),
                    StopDate = now.AddDays(5),
                    ScheduledTimes = [now.AddHours(-1), now.AddHours(11)],
                    IsActive = true
                },
                new MarEntry
                {
                    OrderId = $"PSJ-DEMO-{Guid.NewGuid():N}",
                    DrugName = "Lisinopril",
                    Dosage = "10mg",
                    Route = "PO",
                    Schedule = "DAILY",
                    OrderType = "UNIT_DOSE",
                    Priority = "ROUTINE",
                    WardId = "WARD-MED-3A",
                    RoomBed = "301-A",
                    ProviderName = "Dr. Cardiology",
                    StartDate = now.AddDays(-2),
                    StopDate = now.AddDays(5),
                    ScheduledTimes = [now.AddHours(-2)],
                    IsActive = true
                },
                new MarEntry
                {
                    OrderId = $"PSJ-DEMO-{Guid.NewGuid():N}",
                    DrugName = "Vancomycin",
                    Dosage = "1g",
                    Route = "IV",
                    Schedule = "Q12H",
                    OrderType = "IV",
                    Priority = "STAT",
                    WardId = "WARD-MED-3A",
                    RoomBed = "301-A",
                    ProviderName = "Dr. Infectious Disease",
                    StartDate = now.AddDays(-1),
                    StopDate = now.AddDays(6),
                    ScheduledTimes = [now.AddHours(-3), now.AddHours(9)],
                    IsActive = true
                },
                new MarEntry
                {
                    OrderId = $"PSJ-DEMO-{Guid.NewGuid():N}",
                    DrugName = "Morphine Sulfate",
                    Dosage = "4mg",
                    Route = "IV",
                    Schedule = "PRN Q4H",
                    OrderType = "UNIT_DOSE",
                    Priority = "ROUTINE",
                    WardId = "WARD-MED-3A",
                    RoomBed = "301-A",
                    ProviderName = "Dr. Hospitalist",
                    StartDate = now.AddDays(-1),
                    IsActive = true
                }
            ];

            foreach (MarEntry entry in demoEntries)
                await mar.AddOrUpdateEntryAsync(entry);

            // Record a sample administration for the first entry
            MarEntry first = demoEntries[0];
            string bcmaId = await workflow.RecordMedicationAdministrationAsync(
                first.DrugName, null, first.Dosage, first.Route,
                "GIVEN", first.ScheduledTimes.FirstOrDefault(),
                now.AddHours(-1), "RN-001", "Nurse Thompson",
                null, null, first.OrderId, null);

            await mar.RecordAdministrationAsync(
                first.OrderId, bcmaId, "GIVEN", now.AddHours(-1), "Nurse Thompson");

            // Record a NOT GIVEN for the morphine entry
            MarEntry morphine = demoEntries[3];
            string bcmaId2 = await workflow.RecordMedicationAdministrationAsync(
                morphine.DrugName, null, morphine.Dosage, morphine.Route,
                "NOT GIVEN", null, now.AddHours(-2), "RN-001", "Nurse Thompson",
                null, null, morphine.OrderId, "Patient refused — experiencing nausea");

            await mar.RecordAdministrationAsync(
                morphine.OrderId, bcmaId2, "NOT GIVEN", now.AddHours(-2), "Nurse Thompson");

            return Ok(new
            {
                Message = "Demo BCMA data loaded.",
                MAREntries = demoEntries.Length,
                AdministrationsRecorded = 2,
                PatientId = patientId
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error loading BCMA demo data for patient {PatientId}", patientId);
            return StatusCode(500, "Error loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // ─── DTO Projections ──────────────────────────────────────────────────

    private static BcmaMarDto ToMarDto(MarEntry e) => new()
    {
        OrderId = e.OrderId,
        DrugName = e.DrugName,
        Dosage = e.Dosage,
        Route = e.Route,
        Schedule = e.Schedule,
        OrderType = e.OrderType,
        Priority = e.Priority,
        WardId = e.WardId,
        RoomBed = e.RoomBed,
        ProviderName = e.ProviderName,
        StartDate = e.StartDate,
        StopDate = e.StopDate,
        ScheduledTimes = e.ScheduledTimes,
        LastAdminDateTime = e.LastAdminDateTime,
        LastAdminStatus = e.LastAdminStatus,
        LastAdminBy = e.LastAdminBy,
        TotalAdministrations = e.TotalAdministrations,
        BcmaAdministrationIds = e.BcmaAdministrationIds,
        IsActive = e.IsActive,
        IsDue = e.IsActive && e.ScheduledTimes.Any(t => t <= DateTime.UtcNow.AddMinutes(60))
    };

    private static BcmaDetailDto ToDetailDto(BcmaState s) => new()
    {
        BcmaId = s.AdministrationId,
        PatientId = s.PatientId,
        DrugName = s.DrugName,
        DrugId = s.DrugId,
        Dosage = s.Dosage,
        Route = s.Route,
        ActionStatus = s.ActionStatus,
        ScheduledDateTime = s.ScheduledDateTime,
        AdministrationDateTime = s.AdministrationDateTime,
        AdministeredById = s.AdministeredById,
        AdministeredByName = s.AdministeredByName,
        WitnessId = s.WitnessId,
        WitnessName = s.WitnessName,
        InjectionSite = s.InjectionSite,
        OrderId = s.OrderId,
        PrescriptionId = s.PrescriptionId,
        Comments = s.Comments,
        ReasonNotGiven = s.ReasonNotGiven,
        PrnReason = s.PrnReason,
        PrnEffectiveness = s.PrnEffectiveness,
        CreatedDate = s.CreatedDate
    };
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record BcmaAdministerRequest
{
    public string ActionStatus { get; init; } = "GIVEN";
    public DateTime? AdministrationDateTime { get; init; }
    public string? AdministeredById { get; init; }
    public string? AdministeredByName { get; init; }
    public string? InjectionSite { get; init; }
    public string? PrnReason { get; init; }
    public string? Comments { get; init; }
}

public record BcmaStandaloneRequest
{
    public string DrugName { get; init; } = string.Empty;
    public string? DrugId { get; init; }
    public string? Dosage { get; init; }
    public string? Route { get; init; }
    public string ActionStatus { get; init; } = "GIVEN";
    public DateTime? ScheduledDateTime { get; init; }
    public DateTime? AdministrationDateTime { get; init; }
    public string? AdministeredById { get; init; }
    public string? AdministeredByName { get; init; }
    public string? InjectionSite { get; init; }
    public string? PrescriptionId { get; init; }
    public string? OrderId { get; init; }
    public string? Comments { get; init; }
}

public record BcmaWitnessRequest
{
    public string? WitnessId { get; init; }
    public string? WitnessName { get; init; }
}

public record BcmaPrnRequest
{
    public string? Reason { get; init; }
}

public record BcmaEffectivenessRequest
{
    public string? Effectiveness { get; init; }
}

// ─── Response DTOs ────────────────────────────────────────────────────────────

public record BcmaMarDto
{
    public string OrderId { get; init; } = string.Empty;
    public string DrugName { get; init; } = string.Empty;
    public string? Dosage { get; init; }
    public string? Route { get; init; }
    public string? Schedule { get; init; }
    public string OrderType { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string WardId { get; init; } = string.Empty;
    public string? RoomBed { get; init; }
    public string? ProviderName { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? StopDate { get; init; }
    public List<DateTime> ScheduledTimes { get; init; } = [];
    public DateTime? LastAdminDateTime { get; init; }
    public string? LastAdminStatus { get; init; }
    public string? LastAdminBy { get; init; }
    public int TotalAdministrations { get; init; }
    public List<string> BcmaAdministrationIds { get; init; } = [];
    public bool IsActive { get; init; }
    public bool IsDue { get; init; }
}

public record BcmaDetailDto
{
    public string BcmaId { get; init; } = string.Empty;
    public string PatientId { get; init; } = string.Empty;
    public string DrugName { get; init; } = string.Empty;
    public string? DrugId { get; init; }
    public string? Dosage { get; init; }
    public string? Route { get; init; }
    public string ActionStatus { get; init; } = string.Empty;
    public DateTime? ScheduledDateTime { get; init; }
    public DateTime? AdministrationDateTime { get; init; }
    public string? AdministeredById { get; init; }
    public string? AdministeredByName { get; init; }
    public string? WitnessId { get; init; }
    public string? WitnessName { get; init; }
    public string? InjectionSite { get; init; }
    public string? OrderId { get; init; }
    public string? PrescriptionId { get; init; }
    public string? Comments { get; init; }
    public string? ReasonNotGiven { get; init; }
    public string? PrnReason { get; init; }
    public string? PrnEffectiveness { get; init; }
    public DateTime CreatedDate { get; init; }
}
