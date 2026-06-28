// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.PatientPortal.Controllers;

/// <summary>
/// Patient-facing pharmacy preferences: view the outpatient pharmacies a patient can choose from
/// and set/view their preferred (default) pharmacy. Part of the EXTERNAL_PHARMACY enhancement —
/// the patient picks where their prescriptions are sent, defaulting future Rx to that pharmacy.
/// </summary>
[ApiController]
[Route("api/my/pharmacy")]
[Authorize]
public class MyPharmacyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MyPharmacyController> _logger;

    public MyPharmacyController(IGrainFactory grainFactory, ILogger<MyPharmacyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private string GetPatientId()
        => User.FindFirstValue("patient_id")
            ?? throw new InvalidOperationException("patient_id claim not found.");

    /// <summary>The outpatient pharmacies the patient can choose between (retail / mail / specialty).</summary>
    [HttpGet("options")]
    public async Task<ActionResult> GetOptions()
    {
        try
        {
            List<PharmacyDirectoryEntry> pharmacies = await _grainFactory
                .GetGrain<IPharmacyDirectoryGrain>("PHARMACY-DIRECTORY")
                .GetAllAsync(outpatientOnly: true);
            return Ok(pharmacies.Select(ToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pharmacy options");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>The patient's current preferred (default) pharmacy, or null if none chosen.</summary>
    [HttpGet("preferred")]
    public async Task<ActionResult> GetPreferred()
    {
        try
        {
            string patientId = GetPatientId();
            PharmacyDirectoryEntry? pref = await _grainFactory
                .GetGrain<IPatientWorkflowGrain>(patientId).GetPreferredPharmacyAsync();
            return Ok(pref is null ? null : ToDto(pref));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preferred pharmacy");
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Set the patient's preferred pharmacy (no-op if the id is unknown).</summary>
    [HttpPut("preferred/{pharmacyId}")]
    public async Task<ActionResult> SetPreferred(string pharmacyId)
    {
        try
        {
            string patientId = GetPatientId();
            await _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId).SetPreferredPharmacyAsync(pharmacyId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting preferred pharmacy");
            return StatusCode(500, "An error occurred.");
        }
    }

    private static PharmacyOptionDto ToDto(PharmacyDirectoryEntry p)
        => new(p.PharmacyId, p.Name, p.Kind, p.City, p.State, p.NcpdpId, p.Phone);
}

/// <summary>The subset of a pharmacy directory entry shown to a patient in the portal.</summary>
public record PharmacyOptionDto(
    string PharmacyId, string Name, string Kind, string? City, string? State, string? NcpdpId, string? Phone);
