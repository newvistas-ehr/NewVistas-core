// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PatientController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PatientController> _logger;

    public PatientController(IGrainFactory grainFactory, ILogger<PatientController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpGet("{patientId}")]
    public async Task<ActionResult<PatientState>> GetPatient(string patientId)
    {
        try
        {
            var state = await GetWorkflow(patientId).GetPatientAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving patient information");
        }
    }

    [HttpGet("{patientId}/cover-sheet")]
    public async Task<ActionResult<CoverSheetState>> GetCoverSheet(string patientId)
    {
        try
        {
            var coverSheet = await GetWorkflow(patientId).GetCoverSheetAsync();
            return Ok(coverSheet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building cover sheet for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while building the cover sheet");
        }
    }

    [HttpPost("{patientId}/demographics")]
    public async Task<IActionResult> UpdateDemographics(
        string patientId, [FromBody] UpdateDemographicsRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateDemographicsAsync(
                request.Name, request.Sex, request.DateOfBirth, request.SocialSecurityNumber);
            return Ok(new { PatientId = patientId, Message = "Demographics updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating demographics for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating patient demographics");
        }
    }

    [HttpPost("{patientId}/address")]
    public async Task<IActionResult> UpdateAddress(
        string patientId, [FromBody] UpdateAddressRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateAddressAsync(
                request.StreetAddress1, request.StreetAddress2, request.StreetAddress3,
                request.City, request.State, request.ZipCode);
            return Ok(new { PatientId = patientId, Message = "Address updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating address for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating patient address");
        }
    }

    [HttpPost("{patientId}/contact-info")]
    public async Task<IActionResult> UpdateContactInfo(
        string patientId, [FromBody] UpdateContactInfoRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateContactInfoAsync(
                request.PhoneResidence, request.PhoneWork, request.Email);
            return Ok(new { PatientId = patientId, Message = "Contact info updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact info for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating patient contact info");
        }
    }

    [HttpPost("{patientId}/emergency-contact")]
    public async Task<IActionResult> UpdateEmergencyContact(
        string patientId, [FromBody] UpdateEmergencyContactRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateEmergencyContactAsync(
                request.Name, request.Relationship, request.Phone);
            return Ok(new { PatientId = patientId, Message = "Emergency contact updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating emergency contact for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating emergency contact");
        }
    }

    [HttpPost("{patientId}/veteran-info")]
    public async Task<IActionResult> UpdateVeteranInfo(
        string patientId, [FromBody] UpdateVeteranInfoRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateVeteranInfoAsync(
                request.Veteran, request.ServiceConnectedPercentage,
                request.EligibilityCode, request.PrimaryEligibilityCode);
            return Ok(new { PatientId = patientId, Message = "Veteran info updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating veteran info for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating veteran info");
        }
    }

    [HttpPost("{patientId}/military-service")]
    public async Task<IActionResult> UpdateMilitaryService(
        string patientId, [FromBody] UpdateMilitaryServiceRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateMilitaryServiceAsync(
                request.ServiceEntryDate, request.ServiceSeparationDate,
                request.ServiceBranch, request.DischargeType, request.PrisonerOfWar);
            return Ok(new { PatientId = patientId, Message = "Military service updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating military service for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating military service");
        }
    }

    [HttpPost("{patientId}/marital-status")]
    public async Task<IActionResult> UpdateMaritalStatus(
        string patientId, [FromBody] UpdateMaritalStatusRequest request)
    {
        try
        {
            await GetWorkflow(patientId).UpdateMaritalStatusAsync(request.MaritalStatus);
            return Ok(new { PatientId = patientId, Message = "Marital status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating marital status for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating marital status");
        }
    }

    [HttpGet("{patientId}/appointments")]
    public async Task<ActionResult<IEnumerable<VisitSummary>>> GetAppointments(string patientId)
    {
        try
        {
            var appointments = await GetWorkflow(patientId).GetUpcomingAppointmentsAsync();
            return Ok(appointments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appointments for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving appointments");
        }
    }

    [HttpPost("{patientId}/appointments")]
    public async Task<IActionResult> ScheduleAppointment(
        string patientId, [FromBody] ScheduleAppointmentRequest request)
    {
        try
        {
            var appointmentId = await GetWorkflow(patientId).ScheduleAppointmentAsync(
                request.ClinicId, request.ClinicName, request.AppointmentDateTime,
                request.DurationMinutes, request.ProviderId, request.ProviderName,
                request.Purpose, request.AppointmentType);

            return Created($"api/patient/{patientId}/appointments",
                new { AppointmentId = appointmentId, Message = "Appointment scheduled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling appointment for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while scheduling the appointment");
        }
    }

    [HttpGet("{patientId}/lab-tests")]
    public async Task<ActionResult<IEnumerable<LabResultSummary>>> GetLabTests(string patientId)
    {
        try
        {
            var labs = await GetWorkflow(patientId).GetLabResultsAsync();
            return Ok(labs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lab tests for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving lab tests");
        }
    }

    [HttpPost("{patientId}/lab-tests")]
    public async Task<IActionResult> OrderLabTest(
        string patientId, [FromBody] OrderLabTestRequest request)
    {
        try
        {
            var labTestId = await GetWorkflow(patientId).OrderLabTestAsync(
                request.TestId, request.TestName, request.TestCode, request.OrderId,
                request.OrderingProviderId, request.OrderingProviderName,
                request.SpecimenType, request.Category);

            return Created($"api/patient/{patientId}/lab-tests",
                new { LabTestId = labTestId, Message = "Lab test ordered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ordering lab test for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while ordering the lab test");
        }
    }

    [HttpGet("{patientId}/allergies")]
    public async Task<ActionResult<IEnumerable<AllergySummary>>> GetAllergies(string patientId)
    {
        try
        {
            var allergies = await GetWorkflow(patientId).GetAllergiesAsync();
            return Ok(allergies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allergies for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving allergies");
        }
    }

    [HttpPost("{patientId}/allergies")]
    public async Task<IActionResult> RecordAllergy(
        string patientId, [FromBody] RecordAllergyRequest request)
    {
        try
        {
            var allergyId = await GetWorkflow(patientId).RecordAllergyAsync(
                request.Allergen, request.AllergenType, request.AllergenId,
                request.ObservedHistorical, request.Reactions, request.Severity,
                request.OriginatorId, request.OriginatorName, request.Comments);

            return Created($"api/patient/{patientId}/allergies",
                new { AllergyId = allergyId, Message = "Allergy recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording allergy for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while recording the allergy");
        }
    }

    [HttpGet("{patientId}/orders")]
    public async Task<ActionResult<IEnumerable<OrderSummary>>> GetOrders(
        string patientId, [FromQuery] int filter = 2)
    {
        try
        {
            var orders = await GetWorkflow(patientId).GetOrdersByFilterAsync(filter);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    [HttpGet("{patientId}/orders/recent")]
    public async Task<ActionResult<List<OrderSummary>>> GetRecentOrders(string patientId)
    {
        try
        {
            List<OrderSummary> orders = await GetWorkflow(patientId).GetRecentOrdersAsync();
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent orders for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving recent orders");
        }
    }

    [HttpGet("{patientId}/orders/history")]
    public async Task<ActionResult<List<OrderSummary>>> GetOrderHistory(
        string patientId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int maxCount = 100)
    {
        try
        {
            List<OrderSummary> history = await GetWorkflow(patientId)
                .GetOrderHistoryAsync(from, to, maxCount);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order history for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving order history");
        }
    }

    [HttpGet("{patientId}/problems")]
    public async Task<ActionResult<IEnumerable<ProblemSummary>>> GetProblems(
        string patientId, [FromQuery] bool activeOnly = true)
    {
        try
        {
            var problems = activeOnly
                ? await GetWorkflow(patientId).GetActiveProblemsAsync()
                : await GetWorkflow(patientId).GetAllProblemsAsync();
            return Ok(problems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving problems for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving problems");
        }
    }

    [HttpGet("{patientId}/vitals")]
    public async Task<ActionResult<IEnumerable<VitalSummary>>> GetVitals(string patientId)
    {
        try
        {
            var vitals = await GetWorkflow(patientId).GetLatestVitalsAsync();
            return Ok(vitals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vitals for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving vitals");
        }
    }

    [HttpGet("{patientId}/vitals/history")]
    public async Task<ActionResult<IEnumerable<VitalSummary>>> GetVitalHistory(
        string patientId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int maxCount = 50)
    {
        try
        {
            var vitals = await GetWorkflow(patientId).GetVitalHistoryAsync(from, to, maxCount);
            return Ok(vitals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vital history for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving vital history");
        }
    }

    [HttpGet("{patientId}/vitals/history/{vitalType}")]
    public async Task<ActionResult<IEnumerable<VitalSummary>>> GetVitalHistoryByType(
        string patientId, string vitalType,
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        try
        {
            string decodedType = Uri.UnescapeDataString(vitalType);
            var vitals = await GetWorkflow(patientId).GetVitalHistoryByTypeAsync(decodedType, from, to);
            return Ok(vitals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {VitalType} history for patient {PatientId}",
                vitalType, patientId);
            return StatusCode(500, "An error occurred while retrieving vital history");
        }
    }

    [HttpPost("{patientId}/vitals")]
    public async Task<IActionResult> RecordVitals(
        string patientId, [FromBody] RecordVitalsRequest request)
    {
        try
        {
            await GetWorkflow(patientId).RecordVitalsAsync(
                request.LocationId, request.LocationName,
                request.EnteredById, request.EnteredByName,
                request.DateTimeTaken,
                request.Vitals,
                request.Qualifiers);

            return Created($"api/patient/{patientId}/vitals",
                new { Message = "Vitals recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording vitals for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while recording vitals");
        }
    }

    [HttpPost("{patientId}/problems")]
    public async Task<IActionResult> AddProblem(
        string patientId, [FromBody] AddProblemRequest request)
    {
        try
        {
            string problemId = await GetWorkflow(patientId).AddProblemAsync(
                request.Diagnosis, request.DiagnosisCode, request.Condition,
                request.Priority, request.DateOfOnset,
                request.ProviderId, request.ProviderName,
                request.ClinicId, request.ClinicName,
                request.IsServiceConnected, request.Comments);

            return Created($"api/patient/{patientId}/problems",
                new { ProblemId = problemId, Message = "Problem recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding problem for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while adding the problem");
        }
    }

    [HttpGet("{patientId}/medications")]
    public async Task<ActionResult<IEnumerable<MedicationSummary>>> GetMedications(string patientId)
    {
        try
        {
            var meds = await GetWorkflow(patientId).GetActiveMedicationsAsync();
            return Ok(meds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving medications for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving medications");
        }
    }
}

public record UpdateDemographicsRequest(
    string Name,
    string Sex,
    DateTime? DateOfBirth,
    string? SocialSecurityNumber);

public record UpdateAddressRequest(
    string? StreetAddress1,
    string? StreetAddress2,
    string? StreetAddress3,
    string? City,
    string? State,
    string? ZipCode);

public record UpdateContactInfoRequest(
    string? PhoneResidence,
    string? PhoneWork,
    string? Email);

public record UpdateEmergencyContactRequest(
    string? Name,
    string? Relationship,
    string? Phone);

public record UpdateVeteranInfoRequest(
    string Veteran,
    int? ServiceConnectedPercentage,
    string? EligibilityCode,
    string? PrimaryEligibilityCode);

public record UpdateMilitaryServiceRequest(
    DateTime? ServiceEntryDate,
    DateTime? ServiceSeparationDate,
    string? ServiceBranch,
    string? DischargeType,
    string? PrisonerOfWar);

public record UpdateMaritalStatusRequest(
    string? MaritalStatus);

public record ScheduleAppointmentRequest(
    string ClinicId,
    string ClinicName,
    DateTime AppointmentDateTime,
    int DurationMinutes,
    string? ProviderId,
    string? ProviderName,
    string? Purpose,
    string? AppointmentType,
    string? CreatedBy);

public record OrderLabTestRequest(
    string TestId,
    string TestName,
    string? TestCode,
    string? OrderId,
    string? OrderingProviderId,
    string? OrderingProviderName,
    string? SpecimenType,
    string? Category);

public record RecordAllergyRequest(
    string Allergen,
    string AllergenType,
    string? AllergenId,
    string ReactionType,
    List<string> Reactions,
    string? Severity,
    DateTime? ReactionDateTime,
    string? ObservedHistorical,
    string? OriginatorId,
    string? OriginatorName,
    string? Comments);

public record RecordVitalsRequest(
    string? LocationId,
    string? LocationName,
    string? EnteredById,
    string? EnteredByName,
    DateTime DateTimeTaken,
    Dictionary<string, string> Vitals,
    Dictionary<string, List<string>>? Qualifiers);

public record AddProblemRequest(
    string Diagnosis,
    string? DiagnosisCode,
    string? Condition,
    string? Priority,
    DateTime? DateOfOnset,
    string? ProviderId,
    string? ProviderName,
    string? ClinicId,
    string? ClinicName,
    bool IsServiceConnected,
    string? Comments);
