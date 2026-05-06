// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Home Telehealth API — remote patient monitoring enrollment, readings, alerts, and devices.
///
/// Patient-centric endpoints (enrollment, readings, alerts) route through IPatientWorkflowGrain.
/// Device inventory endpoints (create, list, calibrate, retire) call device grains directly.
///
/// VistA Files: #720 (HT Patient), #720.5 (Measurements), #720.7 (Devices), #720.9 (Alerts).
/// MUMPS routines: HTPATIEN.m, HTMONREC.m, HTMEASUR.m, HTALERT.m
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class HomeTelehealthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HomeTelehealthController> _logger;

    public HomeTelehealthController(IGrainFactory grainFactory, ILogger<HomeTelehealthController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IHomeTelehealthDeviceGrain GetDeviceGrain(string deviceId)
        => _grainFactory.GetGrain<IHomeTelehealthDeviceGrain>($"HT-DEVICE:{deviceId}");

    private IHomeTelehealthDeviceIndexGrain GetDeviceIndex()
        => _grainFactory.GetGrain<IHomeTelehealthDeviceIndexGrain>("HT-DEVICE-IDX");

    // ─── Enrollment ──────────────────────────────────────────────────────────

    /// <summary>Returns the patient's Home Telehealth enrollment record.</summary>
    [HttpGet("api/patient/{patientId}/home-telehealth")]
    [ProducesResponseType(typeof(HomeTelehealthPatientState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeTelehealthPatientState>> GetEnrollment(string patientId)
    {
        try
        {
            HomeTelehealthPatientState state = await GetWorkflow(patientId).GetHtPatientAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HT enrollment for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the enrollment record");
        }
    }

    /// <summary>Enrolls a patient in the Home Telehealth program.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/enroll")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Enroll(string patientId, [FromBody] EnrollRequest request)
    {
        try
        {
            await GetWorkflow(patientId).EnrollInHomeTelehealthAsync(
                request.CareCoordinatorId,
                request.CareCoordinatorName,
                request.PrimaryCareProviderId,
                request.PrimaryCareProviderName,
                request.Protocol,
                request.Notes);
            return Created($"/api/patient/{patientId}/home-telehealth", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling patient {PatientId} in HT", patientId);
            return StatusCode(500, "An error occurred while enrolling the patient");
        }
    }

    /// <summary>Disenrolls a patient from the Home Telehealth program.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/disenroll")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disenroll(string patientId, [FromBody] DisenrollRequest request)
    {
        try
        {
            await GetWorkflow(patientId).DisenrollFromHomeTelehealthAsync(request.Reason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disenrolling patient {PatientId} from HT", patientId);
            return StatusCode(500, "An error occurred while disenrolling the patient");
        }
    }

    // ─── Alert Thresholds ─────────────────────────────────────────────────────

    /// <summary>Sets alert thresholds for a patient.</summary>
    [HttpPut("api/patient/{patientId}/home-telehealth/thresholds")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetThresholds(string patientId, [FromBody] SetThresholdsRequest request)
    {
        try
        {
            await GetWorkflow(patientId).SetHtAlertThresholdsAsync(request.Thresholds);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting HT thresholds for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while setting alert thresholds");
        }
    }

    // ─── Device Assignment ────────────────────────────────────────────────────

    /// <summary>Assigns a device to a patient.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/devices")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AssignDevice(string patientId, [FromBody] AssignDeviceRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AssignHtDeviceAsync(
                request.DeviceId, request.DeviceName, request.DeviceType);
            return Created($"/api/patient/{patientId}/home-telehealth", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning device {DeviceId} to patient {PatientId}",
                request.DeviceId, patientId);
            return StatusCode(500, "An error occurred while assigning the device");
        }
    }

    /// <summary>Returns a device from a patient back to the inventory.</summary>
    [HttpDelete("api/patient/{patientId}/home-telehealth/devices/{deviceId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReturnDevice(string patientId, string deviceId)
    {
        try
        {
            await GetWorkflow(patientId).ReturnHtDeviceAsync(deviceId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning device {DeviceId} from patient {PatientId}",
                deviceId, patientId);
            return StatusCode(500, "An error occurred while returning the device");
        }
    }

    // ─── Readings ─────────────────────────────────────────────────────────────

    /// <summary>Records a physiological measurement for a patient.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/readings")]
    [ProducesResponseType(typeof(RecordReadingResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecordReadingResponse>> RecordReading(
        string patientId, [FromBody] RecordReadingRequest request)
    {
        try
        {
            string readingId = await GetWorkflow(patientId).RecordHtReadingAsync(
                request.MeasurementType,
                request.Value1,
                request.Value2,
                request.Unit,
                request.ReadingDateTime ?? DateTime.UtcNow,
                request.Source,
                request.DeviceId,
                request.Notes);
            return Created($"/api/patient/{patientId}/home-telehealth/readings",
                new RecordReadingResponse { ReadingId = readingId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording HT reading for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while recording the reading");
        }
    }

    /// <summary>Returns readings for a patient, with optional filters.</summary>
    [HttpGet("api/patient/{patientId}/home-telehealth/readings")]
    [ProducesResponseType(typeof(List<HtReadingIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HtReadingIndexEntry>>> GetReadings(
        string patientId,
        [FromQuery] HtMeasurementType? measurementType = null,
        [FromQuery] int? days = null,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            List<HtReadingIndexEntry> readings = await GetWorkflow(patientId)
                .GetHtReadingsAsync(measurementType, days, maxResults);
            return Ok(readings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HT readings for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving readings");
        }
    }

    /// <summary>Marks a reading as reviewed by a clinician.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/readings/{readingId}/review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReviewReading(
        string patientId, string readingId, [FromBody] ReviewReadingRequest request)
    {
        try
        {
            await GetWorkflow(patientId).ReviewHtReadingAsync(readingId, request.ReviewedById, request.ReviewedByName);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing HT reading {ReadingId} for patient {PatientId}",
                readingId, patientId);
            return StatusCode(500, "An error occurred while marking the reading as reviewed");
        }
    }

    // ─── Alerts ───────────────────────────────────────────────────────────────

    /// <summary>Returns alerts for a patient, optionally filtered by status.</summary>
    [HttpGet("api/patient/{patientId}/home-telehealth/alerts")]
    [ProducesResponseType(typeof(List<HtAlertIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HtAlertIndexEntry>>> GetAlerts(
        string patientId,
        [FromQuery] HtAlertStatus? status = null)
    {
        try
        {
            List<HtAlertIndexEntry> alerts = await GetWorkflow(patientId).GetHtAlertsAsync(status);
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HT alerts for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving alerts");
        }
    }

    /// <summary>Acknowledges an alert.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/alerts/{alertId}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AcknowledgeAlert(
        string patientId, string alertId, [FromBody] AlertActionRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AcknowledgeHtAlertAsync(
                alertId, request.ClinicianId, request.ClinicianName, request.ClinicalResponse);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging HT alert {AlertId} for patient {PatientId}",
                alertId, patientId);
            return StatusCode(500, "An error occurred while acknowledging the alert");
        }
    }

    /// <summary>Resolves an alert.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/alerts/{alertId}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResolveAlert(
        string patientId, string alertId, [FromBody] AlertActionRequest request)
    {
        try
        {
            await GetWorkflow(patientId).ResolveHtAlertAsync(
                alertId, request.ClinicianId, request.ClinicianName, request.ClinicalResponse);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving HT alert {AlertId} for patient {PatientId}",
                alertId, patientId);
            return StatusCode(500, "An error occurred while resolving the alert");
        }
    }

    /// <summary>Dismisses an alert as non-actionable.</summary>
    [HttpPost("api/patient/{patientId}/home-telehealth/alerts/{alertId}/dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DismissAlert(
        string patientId, string alertId, [FromBody] AlertActionRequest request)
    {
        try
        {
            await GetWorkflow(patientId).DismissHtAlertAsync(
                alertId, request.ClinicianId, request.ClinicianName, request.ClinicalResponse);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing HT alert {AlertId} for patient {PatientId}",
                alertId, patientId);
            return StatusCode(500, "An error occurred while dismissing the alert");
        }
    }

    // ─── Device Inventory (system-level) ─────────────────────────────────────

    /// <summary>Creates a new device in the inventory.</summary>
    [HttpPost("api/home-telehealth/devices")]
    [ProducesResponseType(typeof(CreateDeviceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateDeviceResponse>> CreateDevice([FromBody] CreateDeviceRequest request)
    {
        try
        {
            string deviceId = $"HT-DEV-{Guid.NewGuid()}";
            IHomeTelehealthDeviceGrain grain = GetDeviceGrain(deviceId);
            await grain.CreateAsync(deviceId, request.DeviceName, request.DeviceType,
                request.Manufacturer, request.Model, request.SerialNumber, request.Notes);

            await GetDeviceIndex().AddAsync(new HtDeviceIndexEntry
            {
                DeviceId = deviceId,
                DeviceName = request.DeviceName,
                DeviceType = request.DeviceType,
                SerialNumber = request.SerialNumber,
                Status = HtDeviceStatus.Available,
                AssignedPatientId = null
            });

            return Created($"/api/home-telehealth/devices/{deviceId}",
                new CreateDeviceResponse { DeviceId = deviceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating HT device");
            return StatusCode(500, "An error occurred while creating the device");
        }
    }

    /// <summary>Returns all devices, optionally filtered by type and/or status.</summary>
    [HttpGet("api/home-telehealth/devices")]
    [ProducesResponseType(typeof(List<HtDeviceIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<HtDeviceIndexEntry>>> GetDevices(
        [FromQuery] HtDeviceType? deviceType = null,
        [FromQuery] HtDeviceStatus? status = null)
    {
        try
        {
            List<HtDeviceIndexEntry> devices = await GetDeviceIndex().GetAsync(deviceType, status);
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HT device inventory");
            return StatusCode(500, "An error occurred while retrieving the device inventory");
        }
    }

    /// <summary>Returns a specific device record.</summary>
    [HttpGet("api/home-telehealth/devices/{deviceId}")]
    [ProducesResponseType(typeof(HomeTelehealthDeviceState), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeTelehealthDeviceState>> GetDevice(string deviceId)
    {
        try
        {
            HomeTelehealthDeviceState state = await GetDeviceGrain(deviceId).GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HT device {DeviceId}", deviceId);
            return StatusCode(500, "An error occurred while retrieving the device");
        }
    }

    /// <summary>Records a calibration event for a device.</summary>
    [HttpPost("api/home-telehealth/devices/{deviceId}/calibrate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CalibrateDevice(string deviceId, [FromBody] CalibrateDeviceRequest request)
    {
        try
        {
            await GetDeviceGrain(deviceId).RecordCalibrationAsync(request.CalibrationDate, request.NextDueDate);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording calibration for device {DeviceId}", deviceId);
            return StatusCode(500, "An error occurred while recording the calibration");
        }
    }

    /// <summary>Sends a device to maintenance.</summary>
    [HttpPost("api/home-telehealth/devices/{deviceId}/maintenance")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendToMaintenance(string deviceId, [FromBody] DeviceNoteRequest request)
    {
        try
        {
            await GetDeviceGrain(deviceId).SendToMaintenanceAsync(request.Notes);
            await GetDeviceIndex().UpdateStatusAsync(deviceId, HtDeviceStatus.InMaintenance, null);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending device {DeviceId} to maintenance", deviceId);
            return StatusCode(500, "An error occurred while updating the device status");
        }
    }

    /// <summary>Retires a device from service.</summary>
    [HttpPost("api/home-telehealth/devices/{deviceId}/retire")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RetireDevice(string deviceId, [FromBody] DeviceNoteRequest request)
    {
        try
        {
            await GetDeviceGrain(deviceId).RetireAsync(request.Notes);
            await GetDeviceIndex().UpdateStatusAsync(deviceId, HtDeviceStatus.Retired, null);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retiring device {DeviceId}", deviceId);
            return StatusCode(500, "An error occurred while retiring the device");
        }
    }
}

// ─── Request / Response DTOs ─────────────────────────────────────────────────

public record EnrollRequest
{
    public string? CareCoordinatorId { get; init; }
    public string? CareCoordinatorName { get; init; }
    public string? PrimaryCareProviderId { get; init; }
    public string? PrimaryCareProviderName { get; init; }
    public HtCareProtocol Protocol { get; init; }
    public string? Notes { get; init; }
}

public record DisenrollRequest
{
    public string? Reason { get; init; }
}

public record SetThresholdsRequest
{
    public List<HtAlertThreshold> Thresholds { get; init; } = new();
}

public record AssignDeviceRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public HtDeviceType DeviceType { get; init; }
}

public record RecordReadingRequest
{
    public HtMeasurementType MeasurementType { get; init; }
    public decimal? Value1 { get; init; }
    public decimal? Value2 { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTime? ReadingDateTime { get; init; }
    public HtReadingSource Source { get; init; }
    public string? DeviceId { get; init; }
    public string? Notes { get; init; }
}

public record RecordReadingResponse
{
    public string ReadingId { get; init; } = string.Empty;
}

public record ReviewReadingRequest
{
    public string ReviewedById { get; init; } = string.Empty;
    public string ReviewedByName { get; init; } = string.Empty;
}

public record AlertActionRequest
{
    public string ClinicianId { get; init; } = string.Empty;
    public string ClinicianName { get; init; } = string.Empty;
    public string? ClinicalResponse { get; init; }
}

public record CreateDeviceRequest
{
    public string DeviceName { get; init; } = string.Empty;
    public HtDeviceType DeviceType { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public string? Notes { get; init; }
}

public record CreateDeviceResponse
{
    public string DeviceId { get; init; } = string.Empty;
}

public record CalibrateDeviceRequest
{
    public DateTime CalibrationDate { get; init; }
    public DateTime NextDueDate { get; init; }
}

public record DeviceNoteRequest
{
    public string? Notes { get; init; }
}
