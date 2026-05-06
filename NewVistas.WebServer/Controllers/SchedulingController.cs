// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Scheduling controller — SD package (VistA File #44 / #44.4).
/// Manages clinic definitions and patient outpatient appointments.
/// MUMPS references: SDAM2.m, SDAMEVT.m, SDCO.m, SDCOU.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/scheduling")]
public class SchedulingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SchedulingController> _logger;

    public SchedulingController(IGrainFactory grainFactory, ILogger<SchedulingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IClinicIndexGrain GetClinicIndex() =>
        _grainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");

    // ─── Patient Appointments ─────────────────────────────────────────────

    /// <summary>GET api/scheduling/{patientId}/appointments — list all appointments</summary>
    [HttpGet("{patientId}/appointments")]
    public async Task<IActionResult> GetAppointments(string patientId, [FromQuery] int max = 50)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            List<AppointmentEntry> entries = await GetWorkflow(pid).GetAllAppointmentsAsync(max);
            return Ok(entries.Select(e => new SdAppointmentDto(e)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appointments for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving appointments.");
        }
    }

    /// <summary>GET api/scheduling/{patientId}/appointments/{appointmentId} — get single appointment</summary>
    [HttpGet("{patientId}/appointments/{appointmentId}")]
    public async Task<IActionResult> GetAppointment(string patientId, string appointmentId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string aid = Uri.UnescapeDataString(appointmentId);
            AppointmentState state = await GetWorkflow(pid).GetAppointmentAsync(aid);
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appointment {AppointmentId} for patient {PatientId}", appointmentId, patientId);
            return StatusCode(500, "An error occurred retrieving the appointment.");
        }
    }

    /// <summary>POST api/scheduling/{patientId}/appointments — schedule new appointment</summary>
    [HttpPost("{patientId}/appointments")]
    public async Task<IActionResult> ScheduleAppointment(string patientId, [FromBody] SdScheduleRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string appointmentId = await GetWorkflow(pid).ScheduleAppointmentAsync(
                req.ClinicId, req.ClinicName, req.AppointmentDateTime,
                req.DurationMinutes, req.ProviderId, req.ProviderName,
                req.Purpose, req.AppointmentType, req.AllowDoubleBook);
            return Created($"api/scheduling/{patientId}/appointments/{appointmentId}", new { appointmentId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("booked") || ex.Message.Contains("conflict"))
        {
            _logger.LogWarning("Scheduling conflict for patient {PatientId}: {Message}", patientId, ex.Message);
            return Conflict(new { error = ex.Message, allowDoubleBookToOverride = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling appointment for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred scheduling the appointment.");
        }
    }

    /// <summary>PUT api/scheduling/{patientId}/appointments/{appointmentId}/checkin</summary>
    [HttpPut("{patientId}/appointments/{appointmentId}/checkin")]
    public async Task<IActionResult> CheckIn(string patientId, string appointmentId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string aid = Uri.UnescapeDataString(appointmentId);
            await GetWorkflow(pid).CheckInAsync(aid, DateTime.UtcNow);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking in appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred during check-in.");
        }
    }

    /// <summary>PUT api/scheduling/{patientId}/appointments/{appointmentId}/checkout</summary>
    [HttpPut("{patientId}/appointments/{appointmentId}/checkout")]
    public async Task<IActionResult> CheckOut(string patientId, string appointmentId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string aid = Uri.UnescapeDataString(appointmentId);
            await GetWorkflow(pid).CheckOutAsync(aid, DateTime.UtcNow);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred during check-out.");
        }
    }

    /// <summary>PUT api/scheduling/{patientId}/appointments/{appointmentId}/cancel</summary>
    [HttpPut("{patientId}/appointments/{appointmentId}/cancel")]
    public async Task<IActionResult> CancelAppointment(string patientId, string appointmentId, [FromBody] SdCancelRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string aid = Uri.UnescapeDataString(appointmentId);
            await GetWorkflow(pid).CancelAppointmentAsync(aid);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred cancelling the appointment.");
        }
    }

    /// <summary>PUT api/scheduling/{patientId}/appointments/{appointmentId}/noshow</summary>
    [HttpPut("{patientId}/appointments/{appointmentId}/noshow")]
    public async Task<IActionResult> NoShow(string patientId, string appointmentId)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string aid = Uri.UnescapeDataString(appointmentId);
            await GetWorkflow(pid).NoShowAppointmentAsync(aid);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking no-show for appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred marking the no-show.");
        }
    }

    /// <summary>PUT api/scheduling/{patientId}/appointments/{appointmentId}/reschedule</summary>
    [HttpPut("{patientId}/appointments/{appointmentId}/reschedule")]
    public async Task<IActionResult> Reschedule(string patientId, string appointmentId, [FromBody] SdRescheduleRequest req)
    {
        try
        {
            string pid = Uri.UnescapeDataString(patientId);
            string aid = Uri.UnescapeDataString(appointmentId);
            await GetWorkflow(pid).RescheduleAppointmentAsync(aid, req.NewDateTime, req.Reason, null);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred rescheduling the appointment.");
        }
    }

    // ─── Clinic Directory ─────────────────────────────────────────────────

    /// <summary>GET api/scheduling/clinics — list all clinics</summary>
    [HttpGet("clinics")]
    public async Task<IActionResult> GetClinics([FromQuery] string? search = null)
    {
        try
        {
            List<ClinicEntry> clinics = string.IsNullOrWhiteSpace(search)
                ? await GetClinicIndex().GetAllClinicsAsync()
                : await GetClinicIndex().SearchClinicsAsync(search);
            return Ok(clinics.Select(c => new SdClinicDto(c)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clinics");
            return StatusCode(500, "An error occurred retrieving clinics.");
        }
    }

    // ─── Patient Eligibility (DG eligibility — DGENELA.m) ─────────────────

    /// <summary>GET api/scheduling/{patientId}/eligibility — check scheduling eligibility</summary>
    [HttpGet("{patientId}/eligibility")]
    public async Task<IActionResult> CheckEligibility(string patientId)
    {
        try
        {
            PatientEligibilityResult result = await GetWorkflow(patientId)
                .CheckPatientEligibilityForSchedulingAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking eligibility for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred checking patient eligibility.");
        }
    }

    // ─── Appointment Letters (SD appointment letters) ─────────────────────

    /// <summary>GET api/scheduling/{patientId}/appointments/{appointmentId}/letter?type= — generate letter</summary>
    [HttpGet("{patientId}/appointments/{appointmentId}/letter")]
    public async Task<IActionResult> GenerateAppointmentLetter(
        string patientId, string appointmentId, [FromQuery] string type = "CONFIRMATION")
    {
        try
        {
            AppointmentLetterContent letter = await GetWorkflow(patientId)
                .GenerateAppointmentLetterAsync(appointmentId, type);
            return Ok(letter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating letter for appointment {AppointmentId}", appointmentId);
            return StatusCode(500, "An error occurred generating the appointment letter.");
        }
    }

    // ─── Reminder Batch Processing (SD reminder processing) ─────────────

    /// <summary>GET api/scheduling/{patientId}/reminders/pending?daysAhead= — appointments needing reminders</summary>
    [HttpGet("{patientId}/reminders/pending")]
    public async Task<IActionResult> GetPendingReminders(string patientId, [FromQuery] int daysAhead = 7)
    {
        try
        {
            List<AppointmentEntry> pending = await GetWorkflow(patientId)
                .GetAppointmentsNeedingRemindersAsync(daysAhead);
            return Ok(pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending reminders for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving pending reminders.");
        }
    }

    /// <summary>POST api/scheduling/{patientId}/reminders/process?daysAhead= — process reminder batch</summary>
    [HttpPost("{patientId}/reminders/process")]
    public async Task<IActionResult> ProcessReminderBatch(string patientId, [FromQuery] int daysAhead = 7)
    {
        try
        {
            ReminderBatchResult result = await GetWorkflow(patientId)
                .ProcessReminderBatchAsync(daysAhead);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reminders for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred processing reminders.");
        }
    }

    // ─── Available Slots & Capacity (SDBUILD.m availability grid) ────────

    /// <summary>GET api/scheduling/clinics/{clinicId}/slots?date=&amp;patientId= — available slots for a date</summary>
    [HttpGet("clinics/{clinicId}/slots")]
    public async Task<IActionResult> GetAvailableSlots(
        string clinicId, [FromQuery] DateTime? date = null, [FromQuery] string patientId = "SYSTEM")
    {
        try
        {
            List<AvailableSlot> slots = await GetWorkflow(patientId)
                .GetAvailableSlotsAsync(clinicId, date ?? DateTime.UtcNow.Date.AddDays(1));
            return Ok(slots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available slots for clinic {ClinicId}", clinicId);
            return StatusCode(500, "An error occurred retrieving available slots.");
        }
    }

    /// <summary>GET api/scheduling/clinics/{clinicId}/capacity?date=&amp;patientId= — daily capacity</summary>
    [HttpGet("clinics/{clinicId}/capacity")]
    public async Task<IActionResult> GetClinicDailyCapacity(
        string clinicId, [FromQuery] DateTime? date = null, [FromQuery] string patientId = "SYSTEM")
    {
        try
        {
            ClinicDailyCapacity capacity = await GetWorkflow(patientId)
                .GetClinicDailyCapacityAsync(clinicId, date ?? DateTime.UtcNow.Date.AddDays(1));
            return Ok(capacity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capacity for clinic {ClinicId}", clinicId);
            return StatusCode(500, "An error occurred retrieving clinic capacity.");
        }
    }

    // ─── Demo Data ────────────────────────────────────────────────────────

    /// <summary>POST api/scheduling/demo/load?patientId= — load demo appointments</summary>
    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo([FromQuery] string patientId = "PATIENT-DEMO-001")
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            string pid = Uri.UnescapeDataString(patientId);

            // Seed clinic index
            await GetClinicIndex().SeedDemoClinicsAsync();

            // Schedule 4 demo appointments across different clinics
            IPatientWorkflowGrain workflow = GetWorkflow(pid);

            DateTime now = DateTime.UtcNow;

            string a1 = await workflow.ScheduleAppointmentAsync(
                "SD-CLINIC-001", "PRIMARY CARE",
                now.AddDays(7).Date.AddHours(9),
                30, "PROV-001", "Dr. Reynolds",
                "Annual wellness visit", "REGULAR");

            string a2 = await workflow.ScheduleAppointmentAsync(
                "SD-CLINIC-003", "CARDIOLOGY",
                now.AddDays(14).Date.AddHours(10).AddMinutes(30),
                45, "PROV-002", "Dr. Patel",
                "Follow-up after stress test", "FOLLOW-UP");

            // Past appointment — already checked in/out
            string a3 = await workflow.ScheduleAppointmentAsync(
                "SD-CLINIC-001", "PRIMARY CARE",
                now.AddDays(-30).Date.AddHours(14),
                30, "PROV-001", "Dr. Reynolds",
                "Blood pressure check", "REGULAR");
            await workflow.CheckInAsync(a3, now.AddDays(-30).Date.AddHours(13).AddMinutes(55));
            await workflow.CheckOutAsync(a3, now.AddDays(-30).Date.AddHours(14).AddMinutes(35));

            // Cancelled appointment
            string a4 = await workflow.ScheduleAppointmentAsync(
                "SD-CLINIC-002", "MENTAL HEALTH",
                now.AddDays(-7).Date.AddHours(11),
                60, "PROV-003", "Dr. Williams",
                "Initial evaluation", "REGULAR");
            await workflow.CancelAppointmentAsync(a4);

            return Ok(new { patientId = pid, appointmentsCreated = 4, clinicsSeeded = 6 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo scheduling data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // ─── Request / Response DTOs ─────────────────────────────────────────

    public record SdScheduleRequest(
        string ClinicId,
        string ClinicName,
        DateTime AppointmentDateTime,
        int DurationMinutes,
        string? ProviderId,
        string? ProviderName,
        string? Purpose,
        string? AppointmentType,
        bool AllowDoubleBook = false);

    public record SdCancelRequest(string? Reason);

    public record SdRescheduleRequest(DateTime NewDateTime, string? Reason);

    public record SdAppointmentDto(
        string AppointmentId,
        string ClinicId,
        string ClinicName,
        DateTime AppointmentDateTime,
        int DurationMinutes,
        string Status,
        string? ProviderName,
        string? Purpose,
        string? AppointmentType)
    {
        public SdAppointmentDto(AppointmentEntry e) : this(
            e.AppointmentId, e.ClinicId, e.ClinicName,
            e.AppointmentDateTime, e.DurationMinutes, e.Status,
            e.ProviderName, e.Purpose, e.AppointmentType) { }
    }

    public record SdClinicDto(
        string ClinicId,
        string Name,
        string? Division,
        string? StopCode,
        int AppointmentLength,
        string Status)
    {
        public SdClinicDto(ClinicEntry c) : this(
            c.ClinicId, c.Name, c.Division, c.StopCode,
            c.AppointmentLength, c.Status) { }
    }
}
