// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/consult-services")]
public class ConsultServiceController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ConsultServiceController> _logger;

    public ConsultServiceController(IGrainFactory grainFactory,
        ILogger<ConsultServiceController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private IConsultServiceDirectoryGrain GetDirectory() =>
        _grainFactory.GetGrain<IConsultServiceDirectoryGrain>("CONSULT-SVC-DIR");

    // ─── GET endpoints ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAllServices()
    {
        try
        {
            List<ConsultServiceEntry> services = await GetDirectory().GetAllServicesAsync();
            return Ok(services.Select(s => new ConsultServiceDto(s)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all consult services");
            return StatusCode(500, "An error occurred retrieving consult services.");
        }
    }

    [HttpGet("{serviceId}")]
    public async Task<IActionResult> GetService(string serviceId)
    {
        try
        {
            ConsultServiceEntry? service = await GetDirectory().GetServiceAsync(serviceId);
            if (service is null)
                return NotFound(new { message = $"Service {serviceId} not found." });
            return Ok(new ConsultServiceDto(service));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting consult service {ServiceId}", serviceId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchServices([FromQuery] string q, [FromQuery] int maxResults = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { message = "Query parameter 'q' is required." });

            List<ConsultServiceEntry> results = await GetDirectory().SearchServicesAsync(q, maxResults);
            return Ok(results.Select(s => new ConsultServiceDto(s)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching consult services with query {Query}", q);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── POST endpoints ────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> AddService([FromBody] AddConsultServiceRequest req)
    {
        try
        {
            ConsultServiceEntry entry = new()
            {
                ServiceId = req.ServiceId,
                ServiceName = req.ServiceName,
                Specialty = req.Specialty,
                DefaultProviderId = req.DefaultProviderId,
                DefaultProviderName = req.DefaultProviderName,
                LocationId = req.LocationId,
                LocationName = req.LocationName,
                IsActive = req.IsActive,
                AcceptsInterfacility = req.AcceptsInterfacility
            };
            await GetDirectory().AddServiceAsync(entry);
            return Created($"api/consult-services/{req.ServiceId}", new { serviceId = req.ServiceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding consult service {ServiceId}", req.ServiceId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpDelete("{serviceId}")]
    public async Task<IActionResult> RemoveService(string serviceId)
    {
        try
        {
            await GetDirectory().RemoveServiceAsync(serviceId);
            return Ok(new { message = $"Service {serviceId} removed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing consult service {ServiceId}", serviceId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("demo/seed")]
    public async Task<IActionResult> SeedDemo()
    {
        try
        {
            await GetDirectory().SeedDemoDataAsync();
            return Ok(new { message = "Demo consult services seeded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding demo consult service data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ConsultServiceDto(
    string ServiceId,
    string ServiceName,
    string? Specialty,
    string? DefaultProviderId,
    string? DefaultProviderName,
    string? LocationId,
    string? LocationName,
    bool IsActive,
    bool AcceptsInterfacility)
{
    public ConsultServiceDto(ConsultServiceEntry e)
        : this(e.ServiceId, e.ServiceName, e.Specialty, e.DefaultProviderId, e.DefaultProviderName,
               e.LocationId, e.LocationName, e.IsActive, e.AcceptsInterfacility) { }
}

public record AddConsultServiceRequest(
    string ServiceId,
    string ServiceName,
    string? Specialty,
    string? DefaultProviderId,
    string? DefaultProviderName,
    string? LocationId,
    string? LocationName,
    bool IsActive,
    bool AcceptsInterfacility);
