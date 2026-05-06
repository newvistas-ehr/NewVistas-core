// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Record Tracking: paper chart location, check-out/in, requests.
/// VistA File #190 (RECORD TRACKING). RTOUT.m, RTIN.m, RTREQ.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RecordTrackingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<RecordTrackingController> _logger;

    public RecordTrackingController(IGrainFactory grainFactory, ILogger<RecordTrackingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IChartGrain Chart(string patientId)
        => _grainFactory.GetGrain<IChartGrain>($"RT-CHART:{Uri.UnescapeDataString(patientId)}");

    private IChartIndexGrain ChartIndex()
        => _grainFactory.GetGrain<IChartIndexGrain>("RT-CHART-IDX");

    private IChartRequestGrain ChartRequest(string requestId)
        => _grainFactory.GetGrain<IChartRequestGrain>(Uri.UnescapeDataString(requestId));

    private IChartRequestIndexGrain RequestIndex()
        => _grainFactory.GetGrain<IChartRequestIndexGrain>("RT-REQUEST-IDX");

    private static ChartIndexEntry BuildChartIndex(ChartState s) => new()
    {
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        ChartNumber = s.ChartNumber,
        CurrentLocation = s.CurrentLocation,
        CurrentLocationType = s.CurrentLocationType,
        IsCheckedOut = s.IsCheckedOut,
        IsOnRequest = s.IsOnRequest,
        IsLost = s.IsLost,
        CheckOutDate = s.CheckOutDate,
        CurrentBorrowerName = s.CurrentBorrowerName,
        ExpectedReturnDate = s.ExpectedReturnDate,
        VolumeCount = s.Volumes.Count
    };

    private static ChartRequestIndexEntry BuildRequestIndex(ChartRequestState s) => new()
    {
        RequestId = s.RequestId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        RequestedByName = s.RequestedByName,
        RequestDate = s.RequestDate,
        NeededBy = s.NeededBy,
        Priority = s.Priority,
        Status = s.Status,
        RequestedForLocation = s.RequestedForLocation,
        RequestType = s.RequestType
    };

    // ── Charts ────────────────────────────────────────────────────────────────

    [HttpPost("charts")]
    public async Task<IActionResult> InitializeChart([FromBody] InitializeChartDto dto)
    {
        try
        {
            string patientId = dto.PatientId.Trim();
            await Chart(patientId).InitializeChartAsync(patientId, dto.PatientName, dto.ChartNumber, dto.HomeLocation);
            ChartState state = await Chart(patientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(state));
            return Created($"/api/recordtracking/charts/{Uri.EscapeDataString(patientId)}", new { patientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing chart for patient {PatientId}", dto.PatientId);
            return StatusCode(500, "Error initializing chart.");
        }
    }

    [HttpGet("charts/{patientId}")]
    public async Task<IActionResult> GetChart(string patientId)
    {
        try
        {
            return Ok(await Chart(patientId).GetChartAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chart for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving chart.");
        }
    }

    [HttpGet("charts")]
    public async Task<IActionResult> GetAllCharts()
    {
        try
        {
            return Ok(await ChartIndex().GetAllChartsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all charts");
            return StatusCode(500, "Error retrieving charts.");
        }
    }

    [HttpGet("charts/checkedout")]
    public async Task<IActionResult> GetCheckedOutCharts()
    {
        try
        {
            return Ok(await ChartIndex().GetCheckedOutChartsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checked-out charts");
            return StatusCode(500, "Error retrieving checked-out charts.");
        }
    }

    [HttpGet("charts/onrequest")]
    public async Task<IActionResult> GetChartsOnRequest()
    {
        try
        {
            return Ok(await ChartIndex().GetChartsOnRequestAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving charts on request");
            return StatusCode(500, "Error retrieving charts on request.");
        }
    }

    [HttpGet("charts/lost")]
    public async Task<IActionResult> GetLostCharts()
    {
        try
        {
            return Ok(await ChartIndex().GetLostChartsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lost charts");
            return StatusCode(500, "Error retrieving lost charts.");
        }
    }

    [HttpGet("charts/overdue")]
    public async Task<IActionResult> GetOverdueCharts()
    {
        try
        {
            return Ok(await ChartIndex().GetOverdueChartsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue charts");
            return StatusCode(500, "Error retrieving overdue charts.");
        }
    }

    [HttpPost("charts/{patientId}/checkout")]
    public async Task<IActionResult> CheckOutChart(string patientId, [FromBody] CheckOutChartDto dto)
    {
        try
        {
            await Chart(patientId).CheckOutChartAsync(dto.BorrowerId, dto.BorrowerName, dto.Location,
                dto.LocationType, dto.ExpectedReturnDate, dto.HandledBy);
            ChartState state = await Chart(patientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out chart for patient {PatientId}", patientId);
            return StatusCode(500, "Error checking out chart.");
        }
    }

    [HttpPost("charts/{patientId}/checkin")]
    public async Task<IActionResult> CheckInChart(string patientId, [FromBody] CheckInChartDto dto)
    {
        try
        {
            await Chart(patientId).CheckInChartAsync(dto.HandledBy);
            ChartState state = await Chart(patientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking in chart for patient {PatientId}", patientId);
            return StatusCode(500, "Error checking in chart.");
        }
    }

    [HttpPost("charts/{patientId}/transfer")]
    public async Task<IActionResult> TransferChart(string patientId, [FromBody] TransferChartDto dto)
    {
        try
        {
            await Chart(patientId).TransferChartAsync(dto.NewLocation, dto.NewLocationType,
                dto.NewBorrowerId, dto.NewBorrowerName, dto.HandledBy);
            ChartState state = await Chart(patientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring chart for patient {PatientId}", patientId);
            return StatusCode(500, "Error transferring chart.");
        }
    }

    [HttpPost("charts/{patientId}/lost")]
    public async Task<IActionResult> MarkChartLost(string patientId, [FromBody] MarkChartLostDto dto)
    {
        try
        {
            await Chart(patientId).MarkChartLostAsync(dto.Notes, dto.HandledBy);
            ChartState state = await Chart(patientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking chart lost for patient {PatientId}", patientId);
            return StatusCode(500, "Error marking chart lost.");
        }
    }

    [HttpPost("charts/{patientId}/found")]
    public async Task<IActionResult> MarkChartFound(string patientId, [FromBody] MarkChartFoundDto dto)
    {
        try
        {
            await Chart(patientId).MarkChartFoundAsync(dto.Location, dto.LocationType, dto.HandledBy);
            ChartState state = await Chart(patientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking chart found for patient {PatientId}", patientId);
            return StatusCode(500, "Error marking chart found.");
        }
    }

    [HttpPost("charts/{patientId}/volumes")]
    public async Task<IActionResult> AddVolume(string patientId, [FromBody] AddVolumeDto dto)
    {
        try
        {
            await Chart(patientId).AddVolumeAsync(dto.VolumeNumber, dto.DateRange);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding volume for patient {PatientId}", patientId);
            return StatusCode(500, "Error adding volume.");
        }
    }

    // ── Chart Requests ────────────────────────────────────────────────────────

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateChartRequestDto dto)
    {
        try
        {
            string requestId = $"RT-REQUEST:{Guid.NewGuid()}";
            await ChartRequest(requestId).CreateRequestAsync(
                dto.PatientId, dto.PatientName,
                dto.RequestedById, dto.RequestedByName,
                dto.NeededBy, dto.Priority,
                dto.RequestedForLocation, dto.RequestType, dto.Notes);
            ChartRequestState state = await ChartRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            // Mark chart as on request
            await Chart(dto.PatientId).SetRequestFlagAsync(true);
            ChartState chartState = await Chart(dto.PatientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(chartState));
            return Created($"/api/recordtracking/requests/{Uri.EscapeDataString(requestId)}", new { requestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chart request for patient {PatientId}", dto.PatientId);
            return StatusCode(500, "Error creating chart request.");
        }
    }

    [HttpGet("requests/{requestId}")]
    public async Task<IActionResult> GetRequest(string requestId)
    {
        try
        {
            return Ok(await ChartRequest(requestId).GetRequestAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving request {RequestId}", requestId);
            return StatusCode(500, "Error retrieving request.");
        }
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetAllRequests()
    {
        try
        {
            return Ok(await RequestIndex().GetAllRequestsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all requests");
            return StatusCode(500, "Error retrieving requests.");
        }
    }

    [HttpGet("requests/pending")]
    public async Task<IActionResult> GetPendingRequests()
    {
        try
        {
            return Ok(await RequestIndex().GetPendingRequestsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending requests");
            return StatusCode(500, "Error retrieving pending requests.");
        }
    }

    [HttpGet("requests/urgent")]
    public async Task<IActionResult> GetUrgentRequests()
    {
        try
        {
            return Ok(await RequestIndex().GetUrgentRequestsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving urgent requests");
            return StatusCode(500, "Error retrieving urgent requests.");
        }
    }

    [HttpGet("requests/patient/{patientId}")]
    public async Task<IActionResult> GetRequestsByPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await RequestIndex().GetRequestsByPatientAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requests for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving requests.");
        }
    }

    [HttpPost("requests/{requestId}/fulfill")]
    public async Task<IActionResult> FulfillRequest(string requestId, [FromBody] FulfillChartRequestDto dto)
    {
        try
        {
            await ChartRequest(requestId).FulfillRequestAsync(dto.FulfilledBy);
            ChartRequestState state = await ChartRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fulfilling request {RequestId}", requestId);
            return StatusCode(500, "Error fulfilling request.");
        }
    }

    [HttpPost("requests/{requestId}/deliver")]
    public async Task<IActionResult> DeliverChart(string requestId, [FromBody] DeliverChartDto dto)
    {
        try
        {
            await ChartRequest(requestId).MarkDeliveredAsync(dto.HandledBy);
            ChartRequestState state = await ChartRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            // Clear the request flag on the chart
            await Chart(state.PatientId).SetRequestFlagAsync(false);
            ChartState chartState = await Chart(state.PatientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(chartState));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delivering chart for request {RequestId}", requestId);
            return StatusCode(500, "Error delivering chart.");
        }
    }

    [HttpPost("requests/{requestId}/cancel")]
    public async Task<IActionResult> CancelRequest(string requestId, [FromBody] CancelChartRequestDto dto)
    {
        try
        {
            await ChartRequest(requestId).CancelRequestAsync(dto.CancellationReason);
            ChartRequestState state = await ChartRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            // Clear the request flag
            await Chart(state.PatientId).SetRequestFlagAsync(false);
            ChartState chartState = await Chart(state.PatientId).GetChartAsync();
            await ChartIndex().UpsertChartAsync(BuildChartIndex(chartState));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling request {RequestId}", requestId);
            return StatusCode(500, "Error cancelling request.");
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            List<ChartIndexEntry> charts = await ChartIndex().GetAllChartsAsync();
            List<ChartRequestIndexEntry> requests = await RequestIndex().GetAllRequestsAsync();
            DateTime now = DateTime.UtcNow;

            var dashboard = new
            {
                TotalCharts = charts.Count,
                CheckedOut = charts.Count(c => c.IsCheckedOut),
                Overdue = charts.Count(c => c.IsCheckedOut && c.ExpectedReturnDate.HasValue && c.ExpectedReturnDate.Value < now),
                OnRequest = charts.Count(c => c.IsOnRequest),
                Lost = charts.Count(c => c.IsLost),
                PendingRequests = requests.Count(r => r.Status is ChartRequestStatus.Pending or ChartRequestStatus.Pulled or ChartRequestStatus.InTransit),
                StatRequests = requests.Count(r => r.Priority == ChartRequestPriority.STAT
                    && r.Status is ChartRequestStatus.Pending or ChartRequestStatus.Pulled or ChartRequestStatus.InTransit)
            };
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating record tracking dashboard");
            return StatusCode(500, "Error generating dashboard.");
        }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record InitializeChartDto(string PatientId, string PatientName, string ChartNumber, string HomeLocation);

public record CheckOutChartDto(
    string BorrowerId,
    string BorrowerName,
    string Location,
    ChartLocationType LocationType,
    DateTime? ExpectedReturnDate,
    string HandledBy);

public record CheckInChartDto(string HandledBy);

public record TransferChartDto(
    string NewLocation,
    ChartLocationType NewLocationType,
    string NewBorrowerId,
    string NewBorrowerName,
    string HandledBy);

public record MarkChartLostDto(string Notes, string HandledBy);

public record MarkChartFoundDto(string Location, ChartLocationType LocationType, string HandledBy);

public record AddVolumeDto(int VolumeNumber, string DateRange);

public record CreateChartRequestDto(
    string PatientId,
    string PatientName,
    string RequestedById,
    string RequestedByName,
    DateTime NeededBy,
    ChartRequestPriority Priority,
    string RequestedForLocation,
    ChartRequestType RequestType,
    string Notes);

public record FulfillChartRequestDto(string FulfilledBy);

public record DeliverChartDto(string HandledBy);

public record CancelChartRequestDto(string CancellationReason);
