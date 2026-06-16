// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Master Patient Index (MPI) Controller — cross-facility patient correlation.
/// Maps to VistA MPI package (File #985, MPIF001.m, RGADTP.m).
///
/// Manages ICN-based patient identity correlation across treating facilities,
/// deterministic/probabilistic patient matching, and treating facility lists.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mpi")]
[Produces("application/json")]
public class MpiController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<MpiController> _logger;

    public MpiController(IGrainFactory grainFactory, ILogger<MpiController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IMpiSearchGrain GetSearchGrain() =>
        _grainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");

    private IMpiCorrelationGrain GetCorrelationGrain(string icn) =>
        _grainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{icn}");

    private IMpiMatchGrain GetMatchGrain() =>
        _grainFactory.GetGrain<IMpiMatchGrain>("MPI-MATCHER");

    // ─── Search ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Search the MPI by name, SSN, or ICN.
    /// Mirrors MPIF001.m LOOKUP.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int maxResults = 25)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<MpiSearchResult>());

            List<MpiSearchResult> results = await GetSearchGrain().SearchAsync(q, maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching MPI for {Query}", q);
            return StatusCode(500, "Error searching MPI.");
        }
    }

    /// <summary>
    /// Lookup a patient by ICN.
    /// Mirrors MPIF001.m GETICN.
    /// </summary>
    [HttpGet("icn/{icn}")]
    public async Task<IActionResult> LookupByIcn(string icn)
    {
        try
        {
            MpiSearchResult? result = await GetSearchGrain().LookupByIcnAsync(icn);
            if (result == null)
                return NotFound(new { message = $"ICN {icn} not found" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up ICN {Icn}", icn);
            return StatusCode(500, "Error looking up ICN.");
        }
    }

    /// <summary>
    /// Lookup patients by SSN.
    /// </summary>
    [HttpGet("ssn/{ssn}")]
    public async Task<IActionResult> LookupBySsn(string ssn)
    {
        try
        {
            List<MpiSearchResult> results = await GetSearchGrain().LookupBySsnAsync(ssn);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up SSN");
            return StatusCode(500, "Error looking up SSN.");
        }
    }

    // ─── Correlation ──────────────────────────────────────────────────────────

    /// <summary>
    /// Get a patient's full MPI correlation record.
    /// </summary>
    [HttpGet("correlation/{icn}")]
    public async Task<IActionResult> GetCorrelation(string icn)
    {
        try
        {
            MpiCorrelationState state = await GetCorrelationGrain(icn).GetCorrelationAsync();
            if (string.IsNullOrEmpty(state.Icn))
                return NotFound(new { message = $"No correlation found for ICN {icn}" });

            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting MPI correlation for {Icn}", icn);
            return StatusCode(500, "Error retrieving correlation.");
        }
    }

    /// <summary>
    /// Create or update a patient's MPI correlation.
    /// Mirrors MPIF001.m SETCOR.
    /// </summary>
    [HttpPost("correlation")]
    public async Task<IActionResult> SetCorrelation([FromBody] MpiSetCorrelationRequest req)
    {
        try
        {
            IMpiCorrelationGrain grain = GetCorrelationGrain(req.Icn);
            await grain.SetCorrelationAsync(req.Icn, req.PatientName, req.Ssn, req.DateOfBirth, req.Sex);

            // Update the search index
            await GetSearchGrain().AddOrUpdatePatientAsync(new MpiSearchEntry
            {
                Icn = req.Icn,
                PatientName = req.PatientName,
                Ssn = req.Ssn,
                DateOfBirth = req.DateOfBirth,
                Sex = req.Sex,
                FacilityCount = 0,
                IsDeceased = false
            });

            return Created($"/api/mpi/correlation/{Uri.EscapeDataString(req.Icn)}", new { icn = req.Icn });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting MPI correlation for {Icn}", req.Icn);
            return StatusCode(500, "Error setting correlation.");
        }
    }

    /// <summary>
    /// Add a local facility correlation to a patient's MPI record.
    /// Mirrors MPIF001.m SETCOR local facility section.
    /// </summary>
    [HttpPost("correlation/{icn}/facility")]
    public async Task<IActionResult> AddFacilityCorrelation(string icn, [FromBody] MpiAddFacilityRequest req)
    {
        try
        {
            await GetCorrelationGrain(icn).AddLocalCorrelationAsync(
                req.FacilityId, req.FacilityName, req.LocalDfn, req.CorrelationDate);

            return Ok(new { icn, facilityId = req.FacilityId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding facility correlation for {Icn}", icn);
            return StatusCode(500, "Error adding facility correlation.");
        }
    }

    /// <summary>
    /// Get a patient's treating facility list.
    /// Mirrors MPIF001.m GETCOR.
    /// </summary>
    [HttpGet("correlation/{icn}/facilities")]
    public async Task<IActionResult> GetTreatingFacilities(string icn)
    {
        try
        {
            List<MpiLocalCorrelation> facilities = await GetCorrelationGrain(icn).GetTreatingFacilitiesAsync();
            return Ok(facilities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting treating facilities for {Icn}", icn);
            return StatusCode(500, "Error retrieving treating facilities.");
        }
    }

    // ─── Matching ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Find a match for a patient using probabilistic matching.
    /// Used during registration to detect potential duplicate patients.
    /// </summary>
    [HttpPost("match")]
    public async Task<IActionResult> FindMatch([FromBody] MpiMatchRequest req)
    {
        try
        {
            MpiMatchResult result = await GetMatchGrain().FindMatchAsync(
                req.PatientName, req.Ssn, req.DateOfBirth, req.Sex);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing MPI match");
            return StatusCode(500, "Error performing patient match.");
        }
    }

    /// <summary>
    /// Find all match candidates above a given threshold.
    /// </summary>
    [HttpPost("match/candidates")]
    public async Task<IActionResult> FindCandidates([FromBody] MpiCandidateRequest req)
    {
        try
        {
            List<MpiMatchCandidate> candidates = await GetMatchGrain().FindCandidatesAsync(
                req.PatientName, req.Ssn, req.DateOfBirth, req.Sex, req.MinimumScore);

            return Ok(candidates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding MPI match candidates");
            return StatusCode(500, "Error finding match candidates.");
        }
    }

    // ─── Status & Demo ────────────────────────────────────────────────────────

    /// <summary>
    /// Get MPI index status.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            MpiIndexStatus status = await GetSearchGrain().GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting MPI status");
            return StatusCode(500, "Error retrieving MPI status.");
        }
    }

    /// <summary>
    /// Seed demo MPI data — 10 patients with ICNs and multi-facility correlations.
    /// </summary>
    [HttpPost("demo/load")]
    public async Task<IActionResult> SeedDemoData()
    {
        try
        {
            await GetSearchGrain().SeedDemoDataAsync();
            MpiIndexStatus status = await GetSearchGrain().GetStatusAsync();
            return Ok(new { message = "MPI demo data loaded", totalPatients = status.TotalPatients });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding MPI demo data");
            return StatusCode(500, "Error seeding demo data.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record MpiSetCorrelationRequest(
    string Icn,
    string PatientName,
    string Ssn,
    DateTime? DateOfBirth,
    string? Sex);

public record MpiAddFacilityRequest(
    string FacilityId,
    string FacilityName,
    string LocalDfn,
    DateTime CorrelationDate);

public record MpiMatchRequest(
    string PatientName,
    string Ssn,
    DateTime? DateOfBirth,
    string? Sex);

public record MpiCandidateRequest(
    string PatientName,
    string? Ssn,
    DateTime? DateOfBirth,
    string? Sex,
    double MinimumScore);
