// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// CMOP Controller — Consolidated Mail Outpatient Pharmacy.
/// Manages mail-order prescription suspense queue, transmissions, and tracking.
/// Maps to VistA PSX package.
/// </summary>
[ApiController]
[Route("api/cmop")]
[Authorize]
public class CmopController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CmopController> _logger;

    public CmopController(IGrainFactory grainFactory, ILogger<CmopController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── Suspense Queue ──────────────────────────────────────────────────────

    [HttpGet("suspense/{siteId}")]
    public async Task<ActionResult> GetSuspenseQueue(string siteId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");
            List<CmopSuspenseEntry> queue = await grain.GetQueuedPrescriptionsAsync();
            return Ok(queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CMOP suspense queue for site {SiteId}", siteId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("suspense/{siteId}/count")]
    public async Task<ActionResult> GetSuspenseCount(string siteId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");
            int count = await grain.GetQueueCountAsync();
            return Ok(new { Count = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CMOP suspense count for site {SiteId}", siteId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("suspense/{siteId}/add")]
    public async Task<ActionResult> AddToSuspense(string siteId, [FromBody] CmopAddToSuspenseRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");
            await grain.AddToSuspenseAsync(new CmopSuspenseEntry
            {
                PrescriptionId = request.PrescriptionId,
                PatientId = request.PatientId,
                PatientName = request.PatientName,
                DrugName = request.DrugName,
                RxNumber = request.RxNumber,
                Quantity = request.Quantity,
                DaysSupply = request.DaysSupply,
                FillType = request.FillType ?? "ORIGINAL",
                QueuedDate = DateTime.UtcNow,
                Priority = request.Priority ?? "ROUTINE",
                MailingAddress = request.MailingAddress
            });
            return Ok(new { Message = "Added to CMOP suspense." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to CMOP suspense for site {SiteId}", siteId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("suspense/{siteId}/remove")]
    public async Task<ActionResult> RemoveFromSuspense(string siteId, [FromBody] CmopRemoveFromSuspenseRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");
            await grain.RemoveFromSuspenseAsync(request.PrescriptionId);
            return Ok(new { Message = "Removed from suspense." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from CMOP suspense for site {SiteId}", siteId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("suspense/{siteId}/transmit")]
    public async Task<ActionResult> TransmitQueue(string siteId, [FromBody] CmopTransmitRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");
            string txId = await grain.TransmitQueueAsync(request.CmopFacilityId, request.CmopFacilityName);
            _logger.LogInformation("CMOP transmission {TxId} created for site {SiteId}", txId, siteId);
            return Created("", new { TransmissionId = txId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transmitting CMOP queue for site {SiteId}", siteId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("suspense/{siteId}/auto-transmit")]
    public async Task<ActionResult> SetAutoTransmit(string siteId, [FromBody] CmopAutoTransmitRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");
            await grain.SetAutoTransmitAsync(request.Enabled, request.Hour);
            return Ok(new { Message = "Auto-transmit updated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting auto-transmit for site {SiteId}", siteId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Transmissions ───────────────────────────────────────────────────────

    [HttpGet("transmissions/{siteId}")]
    public async Task<ActionResult> GetTransmissions(string siteId, [FromQuery] string? status = null)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopTransmissionIndexGrain>($"CMOP-TX-INDEX:{siteId}");
            List<CmopTransmissionSummary> list = status != null
                ? await grain.GetTransmissionsByStatusAsync(status)
                : await grain.GetTransmissionsAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CMOP transmissions for site {SiteId}", siteId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("transmissions/{siteId}/{transmissionId}")]
    public async Task<ActionResult> GetTransmission(string siteId, string transmissionId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{transmissionId}");
            CmopTransmissionState tx = await grain.GetTransmissionAsync();
            return Ok(tx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CMOP transmission {TxId}", transmissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("transmissions/{siteId}/{transmissionId}/acknowledge")]
    public async Task<ActionResult> AcknowledgeReceipt(string siteId, string transmissionId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{transmissionId}");
            await grain.AcknowledgeReceiptAsync();
            await SyncIndexAsync(siteId, transmissionId, "RECEIVED");
            return Ok(new { Message = "Receipt acknowledged." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging CMOP transmission {TxId}", transmissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("transmissions/{siteId}/{transmissionId}/dispensed")]
    public async Task<ActionResult> RecordDispensed(string siteId, string transmissionId, [FromBody] CmopDispensedRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{transmissionId}");
            await grain.RecordDispensedAsync(request.DispensedCount, request.RejectedCount, request.ErrorMessage);
            await SyncIndexAsync(siteId, transmissionId, "DISPENSED", request.DispensedCount, request.RejectedCount);
            return Ok(new { Message = "Dispensing recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording dispensed for CMOP transmission {TxId}", transmissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("transmissions/{siteId}/{transmissionId}/shipped")]
    public async Task<ActionResult> RecordShipped(string siteId, string transmissionId, [FromBody] CmopShippedRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{transmissionId}");
            await grain.RecordShippedAsync(request.TrackingNumber, request.Carrier);
            await SyncIndexAsync(siteId, transmissionId, "SHIPPED", trackingNumber: request.TrackingNumber);
            return Ok(new { Message = "Shipment recorded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording shipment for CMOP transmission {TxId}", transmissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("transmissions/{siteId}/{transmissionId}/complete")]
    public async Task<ActionResult> CompleteTransmission(string siteId, string transmissionId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{transmissionId}");
            await grain.CompleteAsync();
            await SyncIndexAsync(siteId, transmissionId, "COMPLETED");
            return Ok(new { Message = "Transmission completed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing CMOP transmission {TxId}", transmissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("transmissions/{siteId}/{transmissionId}/cancel")]
    public async Task<ActionResult> CancelTransmission(string siteId, string transmissionId, [FromBody] CmopCancelRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{transmissionId}");
            await grain.CancelAsync(request.Reason);
            await SyncIndexAsync(siteId, transmissionId, "CANCELLED");
            return Ok(new { Message = "Transmission cancelled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling CMOP transmission {TxId}", transmissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ────────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    [AllowAnonymous]
    public async Task<ActionResult> LoadDemo([FromQuery] string siteId = "SITE-500")
    {
        try
        {
            var suspense = _grainFactory.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{siteId}");

            var demoRxs = new[]
            {
                ("RX-CMOP-001", "PATIENT-001", "JONES,MICHAEL R", "LISINOPRIL 10MG TAB", "RX1001", 90, 90, "REFILL", "ROUTINE"),
                ("RX-CMOP-002", "PATIENT-001", "JONES,MICHAEL R", "METFORMIN 500MG TAB", "RX1002", 180, 90, "REFILL", "ROUTINE"),
                ("RX-CMOP-003", "PATIENT-002", "SMITH,SARAH L", "AMLODIPINE 5MG TAB", "RX2001", 90, 90, "ORIGINAL", "ROUTINE"),
                ("RX-CMOP-004", "PATIENT-003", "WILLIAMS,JAMES T", "OMEPRAZOLE 20MG CAP", "RX3001", 90, 90, "REFILL", "ROUTINE"),
                ("RX-CMOP-005", "PATIENT-003", "WILLIAMS,JAMES T", "ATORVASTATIN 40MG TAB", "RX3002", 90, 90, "REFILL", "URGENT"),
            };

            foreach (var (rxId, patId, name, drug, rxNum, qty, days, fill, priority) in demoRxs)
            {
                await suspense.AddToSuspenseAsync(new CmopSuspenseEntry
                {
                    PrescriptionId = rxId,
                    PatientId = patId,
                    PatientName = name,
                    DrugName = drug,
                    RxNumber = rxNum,
                    Quantity = qty,
                    DaysSupply = days,
                    FillType = fill,
                    QueuedDate = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(10, 600)),
                    Priority = priority,
                    MailingAddress = "123 MAIN ST, ANYTOWN, ST 12345"
                });
            }

            return Ok(new { Message = $"Loaded {demoRxs.Length} prescriptions to CMOP suspense queue." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading CMOP demo data");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task SyncIndexAsync(string siteId, string transmissionId, string status,
        int? dispensedCount = null, int? rejectedCount = null, string? trackingNumber = null)
    {
        var indexGrain = _grainFactory.GetGrain<ICmopTransmissionIndexGrain>($"CMOP-TX-INDEX:{siteId}");
        List<CmopTransmissionSummary> all = await indexGrain.GetTransmissionsAsync();
        var existing = all.Find(t => t.TransmissionId == transmissionId);
        if (existing != null)
        {
            existing.Status = status;
            if (dispensedCount.HasValue) existing.DispensedCount = dispensedCount.Value;
            if (rejectedCount.HasValue) existing.RejectedCount = rejectedCount.Value;
            if (trackingNumber != null) existing.TrackingNumber = trackingNumber;
            await indexGrain.AddOrUpdateAsync(existing);
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record CmopAddToSuspenseRequest
{
    public required string PrescriptionId { get; init; }
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required string DrugName { get; init; }
    public string? RxNumber { get; init; }
    public int? Quantity { get; init; }
    public int? DaysSupply { get; init; }
    public string? FillType { get; init; }
    public string? Priority { get; init; }
    public string? MailingAddress { get; init; }
}

public record CmopRemoveFromSuspenseRequest { public required string PrescriptionId { get; init; } }
public record CmopTransmitRequest { public required string CmopFacilityId { get; init; } public required string CmopFacilityName { get; init; } }
public record CmopAutoTransmitRequest { public required bool Enabled { get; init; } public int? Hour { get; init; } }
public record CmopDispensedRequest { public required int DispensedCount { get; init; } public int RejectedCount { get; init; } public string? ErrorMessage { get; init; } }
public record CmopShippedRequest { public required string TrackingNumber { get; init; } public required string Carrier { get; init; } }
public record CmopCancelRequest { public required string Reason { get; init; } }
