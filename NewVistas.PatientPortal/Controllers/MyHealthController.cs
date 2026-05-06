// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;

namespace NewVistas.PatientPortal.Controllers;

/// <summary>
/// Patient-scoped read-only health data views.
/// Patients can see their own clinical data but cannot modify it.
/// §170.315(e)(1) — View, Download, and Transmit to 3rd Party.
/// </summary>
[ApiController]
[Route("api/my/health")]
[Authorize]
public class MyHealthController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MyHealthController> _logger;

    public MyHealthController(IGrainFactory grainFactory, ILogger<MyHealthController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private string GetPatientId()
        => User.FindFirstValue("patient_id")
            ?? throw new InvalidOperationException("patient_id claim not found.");

    /// <summary>Get my demographics (from PatientGrain).</summary>
    [HttpGet("demographics")]
    public async Task<ActionResult> GetDemographics()
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await grain.GetPatientAsync();
            return Ok(new
            {
                patient.Name,
                patient.DateOfBirth,
                patient.Sex,
                Address = patient.StreetAddress1,
                patient.City,
                patient.State,
                patient.ZipCode,
                PhoneNumber = patient.PhoneNumberResidence,
                patient.MaritalStatus,
                patient.Ethnicity,
                patient.Race,
                Religion = patient.ReligiousPreference,
                patient.Email,
                patient.EmergencyContactName,
                patient.EmergencyContactPhone
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting demographics");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my recent orders (which include medication orders).</summary>
    [HttpGet("medications")]
    public async Task<ActionResult> GetMedications()
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await grain.GetPatientAsync();
            // Return recent orders — includes pharmacy orders
            return Ok(patient.RecentOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting medications");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my allergies (embedded on PatientState).</summary>
    [HttpGet("allergies")]
    public async Task<ActionResult> GetAllergies()
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await grain.GetPatientAsync();
            return Ok(patient.Allergies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting allergies");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my problems (embedded on PatientState).</summary>
    [HttpGet("problems")]
    public async Task<ActionResult> GetProblems()
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await grain.GetPatientAsync();
            return Ok(patient.Problems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting problems");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my immunizations (embedded on PatientState).</summary>
    [HttpGet("immunizations")]
    public async Task<ActionResult> GetImmunizations()
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await grain.GetPatientAsync();
            return Ok(patient.Immunizations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting immunizations");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my recent vitals (embedded cache on PatientState).</summary>
    [HttpGet("vitals")]
    public async Task<ActionResult> GetVitals()
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await grain.GetPatientAsync();
            return Ok(patient.RecentVitals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vitals");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Get my upcoming appointments.</summary>
    [HttpGet("appointments")]
    public async Task<ActionResult> GetAppointments()
    {
        try
        {
            string patientId = GetPatientId();
            var grain = _grainFactory.GetGrain<IPatientGrain>(patientId);
            var patient = await grain.GetPatientAsync();
            return Ok(patient.AppointmentIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appointments");
            return StatusCode(500, "An error occurred.");
        }
    }
}
