// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Lab EDI Controller — electronic lab order/result interchange with reference labs.
/// Extends the existing Lab Instrument infrastructure (LA Package) with external
/// reference lab ordering (ORM) and result receipt (ORU).
///
/// Manages outbound electronic orders to reference labs (Quest, LabCorp, ARUP),
/// inbound result receipt and review, and reference lab configuration.
/// </summary>
[Authorize]
[ApiController]
[Route("api/labedi")]
[Produces("application/json")]
public class LabEdiController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<LabEdiController> _logger;

    public LabEdiController(IGrainFactory grainFactory, ILogger<LabEdiController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private ILabEdiIndexGrain GetIndexGrain() =>
        _grainFactory.GetGrain<ILabEdiIndexGrain>("LAB-EDI-INDEX");

    // ─── Reference Lab Configuration ──────────────────────────────────────────

    /// <summary>Get all configured reference labs.</summary>
    [HttpGet("labs")]
    public async Task<IActionResult> GetReferenceLabs()
    {
        try
        {
            List<LabEdiLabSummary> labs = await GetIndexGrain().GetReferenceLabsAsync();
            return Ok(labs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reference labs");
            return StatusCode(500, "Error retrieving reference labs.");
        }
    }

    /// <summary>Get a specific reference lab's configuration.</summary>
    [HttpGet("labs/{referenceLabId}")]
    public async Task<IActionResult> GetLabConfig(string referenceLabId)
    {
        try
        {
            ILabEdiConfigGrain grain = _grainFactory.GetGrain<ILabEdiConfigGrain>($"LAB-EDI-CFG:{referenceLabId}");
            LabEdiConfigState state = await grain.GetConfigAsync();
            if (string.IsNullOrEmpty(state.LabName))
                return NotFound(new { message = $"Reference lab {referenceLabId} not found" });

            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lab config for {LabId}", referenceLabId);
            return StatusCode(500, "Error retrieving lab configuration.");
        }
    }

    /// <summary>Configure a reference lab connection.</summary>
    [HttpPost("labs")]
    public async Task<IActionResult> ConfigureLab([FromBody] LabEdiConfigRequest req)
    {
        try
        {
            ILabEdiConfigGrain grain = _grainFactory.GetGrain<ILabEdiConfigGrain>($"LAB-EDI-CFG:{req.ReferenceLabId}");
            await grain.ConfigureAsync(
                req.LabName, req.CliaNumber, req.ConnectionType,
                req.Host, req.Port, req.FtpPath, req.Hl7Version,
                req.SendingFacilityId, req.SendingFacilityName,
                req.AutoAcknowledge, req.AutoFileResults);

            await GetIndexGrain().AddOrUpdateLabAsync(new LabEdiLabSummary
            {
                ReferenceLabId = req.ReferenceLabId,
                LabName = req.LabName,
                CliaNumber = req.CliaNumber,
                ConnectionType = req.ConnectionType,
                IsActive = true,
                TotalOrders = 0,
                TotalResults = 0
            });

            return Created($"/api/labedi/labs/{Uri.EscapeDataString(req.ReferenceLabId)}", new { referenceLabId = req.ReferenceLabId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring reference lab {LabId}", req.ReferenceLabId);
            return StatusCode(500, "Error configuring reference lab.");
        }
    }

    // ─── Orders ───────────────────────────────────────────────────────────────

    /// <summary>Create a new electronic lab order for a reference lab.</summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] LabEdiCreateOrderRequest req)
    {
        try
        {
            string orderId = $"LAB-EDI-ORD:{Guid.NewGuid()}";
            ILabEdiOrderGrain grain = _grainFactory.GetGrain<ILabEdiOrderGrain>(orderId);

            List<LabEdiTestRequest> tests = req.TestsRequested.Select(t => new LabEdiTestRequest
            {
                TestCode = t.TestCode,
                TestName = t.TestName,
                LoincCode = t.LoincCode,
                SpecimenRequirements = t.SpecimenRequirements
            }).ToList();

            await grain.CreateOrderAsync(
                req.PatientId, req.PatientName, req.ReferenceLab, req.ReferenceLabId,
                req.OrderingProviderId, req.OrderingProviderName, tests,
                req.ClinicalHistory, req.SpecimenType, req.SpecimenId,
                req.CollectionDate, req.Priority);

            // Update index
            await GetIndexGrain().AddOrUpdateOrderAsync(new LabEdiOrderSummary
            {
                OrderId = orderId,
                PatientId = req.PatientId,
                PatientName = req.PatientName,
                ReferenceLab = req.ReferenceLab,
                Status = LabEdiOrderStatus.Draft,
                TestCount = tests.Count,
                CreatedDate = DateTime.UtcNow
            });

            // Enqueue for transmission
            ILabEdiQueueGrain queue = _grainFactory.GetGrain<ILabEdiQueueGrain>($"LAB-EDI-Q:{req.ReferenceLabId}");
            await queue.EnqueueOrderAsync(orderId, req.PatientId, req.PatientName, DateTime.UtcNow);

            return Created($"/api/labedi/orders/{Uri.EscapeDataString(orderId)}", new { orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lab EDI order for patient {PatientId}", req.PatientId);
            return StatusCode(500, "Error creating lab order.");
        }
    }

    /// <summary>Get a specific order.</summary>
    [HttpGet("orders/{orderId}")]
    public async Task<IActionResult> GetOrder(string orderId)
    {
        try
        {
            ILabEdiOrderGrain grain = _grainFactory.GetGrain<ILabEdiOrderGrain>(orderId);
            LabEdiOrderState state = await grain.GetOrderAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lab EDI order {OrderId}", orderId);
            return StatusCode(500, "Error retrieving order.");
        }
    }

    /// <summary>Get all orders for a patient.</summary>
    [HttpGet("patients/{patientId}/orders")]
    public async Task<IActionResult> GetPatientOrders(string patientId, [FromQuery] int maxResults = 25)
    {
        try
        {
            List<LabEdiOrderSummary> orders = await GetIndexGrain().GetOrdersByPatientAsync(patientId, maxResults);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lab EDI orders for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving patient orders.");
        }
    }

    /// <summary>Mark an order as transmitted (HL7 ORM sent).</summary>
    [HttpPost("orders/{orderId}/transmit")]
    public async Task<IActionResult> TransmitOrder(string orderId, [FromBody] LabEdiTransmitRequest req)
    {
        try
        {
            ILabEdiOrderGrain grain = _grainFactory.GetGrain<ILabEdiOrderGrain>(orderId);
            await grain.MarkTransmittedAsync(req.Hl7MessageId, req.TransmittedDate);
            return Ok(new { orderId, status = "Transmitted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transmitting lab EDI order {OrderId}", orderId);
            return StatusCode(500, "Error transmitting order.");
        }
    }

    /// <summary>Cancel an order.</summary>
    [HttpPost("orders/{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(string orderId, [FromBody] LabEdiCancelRequest req)
    {
        try
        {
            ILabEdiOrderGrain grain = _grainFactory.GetGrain<ILabEdiOrderGrain>(orderId);
            await grain.CancelOrderAsync(req.Reason, req.CancelledBy);
            return Ok(new { orderId, status = "Cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling lab EDI order {OrderId}", orderId);
            return StatusCode(500, "Error cancelling order.");
        }
    }

    // ─── Results ──────────────────────────────────────────────────────────────

    /// <summary>Record an inbound result from a reference lab (HL7 ORU).</summary>
    [HttpPost("results")]
    public async Task<IActionResult> RecordResult([FromBody] LabEdiRecordResultRequest req)
    {
        try
        {
            string resultId = $"LAB-EDI-RES:{Guid.NewGuid()}";
            ILabEdiResultGrain grain = _grainFactory.GetGrain<ILabEdiResultGrain>(resultId);

            List<LabEdiResultItem> results = req.Results.Select(r => new LabEdiResultItem
            {
                TestCode = r.TestCode,
                TestName = r.TestName,
                LoincCode = r.LoincCode,
                Value = r.Value,
                Units = r.Units,
                ReferenceRange = r.ReferenceRange,
                AbnormalFlag = r.AbnormalFlag,
                ResultStatus = r.ResultStatus
            }).ToList();

            await grain.RecordResultAsync(
                req.OrderId, req.PatientId, req.ReferenceLab, req.ReferenceLabId,
                req.Hl7MessageId, results, req.ResultDate, req.PerformingLabClia);

            // Record in queue
            ILabEdiQueueGrain queue = _grainFactory.GetGrain<ILabEdiQueueGrain>($"LAB-EDI-Q:{req.ReferenceLabId}");
            await queue.RecordInboundResultAsync(resultId, req.OrderId, req.PatientId, DateTime.UtcNow);

            return Created($"/api/labedi/results/{Uri.EscapeDataString(resultId)}", new { resultId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording lab EDI result");
            return StatusCode(500, "Error recording result.");
        }
    }

    /// <summary>Get a specific result.</summary>
    [HttpGet("results/{resultId}")]
    public async Task<IActionResult> GetResult(string resultId)
    {
        try
        {
            ILabEdiResultGrain grain = _grainFactory.GetGrain<ILabEdiResultGrain>(resultId);
            LabEdiResultState state = await grain.GetResultAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lab EDI result {ResultId}", resultId);
            return StatusCode(500, "Error retrieving result.");
        }
    }

    /// <summary>Review a result (pathologist/lab tech review step).</summary>
    [HttpPost("results/{resultId}/review")]
    public async Task<IActionResult> ReviewResult(string resultId, [FromBody] LabEdiReviewRequest req)
    {
        try
        {
            ILabEdiResultGrain grain = _grainFactory.GetGrain<ILabEdiResultGrain>(resultId);
            await grain.ReviewResultAsync(req.ReviewedBy, req.ReviewedByName, req.ReviewedDate, req.Comments);
            return Ok(new { resultId, status = "Reviewed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing lab EDI result {ResultId}", resultId);
            return StatusCode(500, "Error reviewing result.");
        }
    }

    /// <summary>Accept a result (files into patient lab record).</summary>
    [HttpPost("results/{resultId}/accept")]
    public async Task<IActionResult> AcceptResult(string resultId, [FromBody] LabEdiAcceptRequest req)
    {
        try
        {
            ILabEdiResultGrain grain = _grainFactory.GetGrain<ILabEdiResultGrain>(resultId);
            await grain.AcceptResultAsync(req.AcceptedBy);
            return Ok(new { resultId, status = "Accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting lab EDI result {ResultId}", resultId);
            return StatusCode(500, "Error accepting result.");
        }
    }

    // ─── Queue & Dashboard ────────────────────────────────────────────────────

    /// <summary>Get pending orders for a reference lab.</summary>
    [HttpGet("labs/{referenceLabId}/queue")]
    public async Task<IActionResult> GetPendingOrders(string referenceLabId)
    {
        try
        {
            ILabEdiQueueGrain queue = _grainFactory.GetGrain<ILabEdiQueueGrain>($"LAB-EDI-Q:{referenceLabId}");
            List<LabEdiQueueEntry> entries = await queue.GetPendingOrdersAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending orders for lab {LabId}", referenceLabId);
            return StatusCode(500, "Error retrieving pending orders.");
        }
    }

    /// <summary>Get queue status for a reference lab.</summary>
    [HttpGet("labs/{referenceLabId}/queue/status")]
    public async Task<IActionResult> GetQueueStatus(string referenceLabId)
    {
        try
        {
            ILabEdiQueueGrain queue = _grainFactory.GetGrain<ILabEdiQueueGrain>($"LAB-EDI-Q:{referenceLabId}");
            LabEdiQueueStatus status = await queue.GetQueueStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue status for lab {LabId}", referenceLabId);
            return StatusCode(500, "Error retrieving queue status.");
        }
    }

    /// <summary>Get aggregate EDI dashboard.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            LabEdiDashboard dashboard = await GetIndexGrain().GetDashboardAsync();
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lab EDI dashboard");
            return StatusCode(500, "Error retrieving dashboard.");
        }
    }

    /// <summary>Seed demo reference lab configurations.</summary>
    [HttpPost("demo/load")]
    public async Task<IActionResult> SeedDemoData()
    {
        try
        {
            await GetIndexGrain().SeedDemoDataAsync();
            return Ok(new { message = "Lab EDI demo data loaded" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding lab EDI demo data");
            return StatusCode(500, "Error seeding demo data.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record LabEdiConfigRequest(
    string ReferenceLabId,
    string LabName,
    string CliaNumber,
    string ConnectionType,
    string? Host,
    int? Port,
    string? FtpPath,
    string? Hl7Version,
    string SendingFacilityId,
    string SendingFacilityName,
    bool AutoAcknowledge,
    bool AutoFileResults);

public record LabEdiCreateOrderRequest(
    string PatientId,
    string PatientName,
    string ReferenceLab,
    string ReferenceLabId,
    string OrderingProviderId,
    string OrderingProviderName,
    List<LabEdiTestRequestDto> TestsRequested,
    string? ClinicalHistory,
    string? SpecimenType,
    string? SpecimenId,
    DateTime? CollectionDate,
    string? Priority);

public record LabEdiTestRequestDto(
    string TestCode,
    string TestName,
    string? LoincCode,
    string? SpecimenRequirements);

public record LabEdiTransmitRequest(
    string Hl7MessageId,
    DateTime TransmittedDate);

public record LabEdiCancelRequest(
    string Reason,
    string CancelledBy);

public record LabEdiRecordResultRequest(
    string OrderId,
    string PatientId,
    string ReferenceLab,
    string ReferenceLabId,
    string Hl7MessageId,
    List<LabEdiResultItemDto> Results,
    DateTime ResultDate,
    string? PerformingLabClia);

public record LabEdiResultItemDto(
    string TestCode,
    string TestName,
    string? LoincCode,
    string Value,
    string? Units,
    string? ReferenceRange,
    string? AbnormalFlag,
    string ResultStatus);

public record LabEdiReviewRequest(
    string ReviewedBy,
    string ReviewedByName,
    DateTime ReviewedDate,
    string? Comments);

public record LabEdiAcceptRequest(string AcceptedBy);
