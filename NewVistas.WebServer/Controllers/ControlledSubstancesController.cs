// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize(Policy = "CanManageControlledSubstances")]
[ApiController]
[Route("api/[controller]")]
public class ControlledSubstancesController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ControlledSubstancesController> _logger;

    public ControlledSubstancesController(IGrainFactory grainFactory, ILogger<ControlledSubstancesController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private ICSInspectionLogGrain GetInspectionLog(string locationId) =>
        _grainFactory.GetGrain<ICSInspectionLogGrain>($"CS-INSPECT-LOG:{Uri.UnescapeDataString(locationId)}");

    private ICSDispenseLogGrain GetDispenseLog(string locationId) =>
        _grainFactory.GetGrain<ICSDispenseLogGrain>($"CS-DISPENSE-LOG:{Uri.UnescapeDataString(locationId)}");

    // ── Inspection Queries ────────────────────────────────────────────────────

    [HttpGet("{locationId}/inspections")]
    public async Task<IActionResult> GetAllInspections(string locationId)
    {
        try
        {
            List<CSInspectionSummaryEntry> result = await GetInspectionLog(locationId).GetAllInspectionsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspections for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving inspections.");
        }
    }

    [HttpGet("{locationId}/inspections/failed")]
    public async Task<IActionResult> GetFailedInspections(string locationId)
    {
        try
        {
            List<CSInspectionSummaryEntry> result = await GetInspectionLog(locationId).GetFailedInspectionsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving failed inspections for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving failed inspections.");
        }
    }

    [HttpGet("{locationId}/inspections/type/{type}")]
    public async Task<IActionResult> GetInspectionsByType(string locationId, CSInspectionType type)
    {
        try
        {
            List<CSInspectionSummaryEntry> result = await GetInspectionLog(locationId).GetInspectionsByTypeAsync(type);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspections by type for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving inspections.");
        }
    }

    [HttpGet("inspections/{inspectionId}")]
    public async Task<IActionResult> GetInspection(string inspectionId)
    {
        try
        {
            ICSInspectionGrain grain = _grainFactory.GetGrain<ICSInspectionGrain>(Uri.UnescapeDataString(inspectionId));
            CSInspectionState result = await grain.GetInspectionAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection {InspectionId}", inspectionId);
            return StatusCode(500, "An error occurred retrieving the inspection.");
        }
    }

    // ── Inspection Lifecycle ─────────────────────────────────────────────────

    [HttpPost("{locationId}/inspections")]
    public async Task<IActionResult> CreateInspection(string locationId, [FromBody] CreateCSInspectionRequest req)
    {
        try
        {
            string inspectionId = $"CS-INSPECTION:{Guid.NewGuid()}";
            ICSInspectionGrain grain = _grainFactory.GetGrain<ICSInspectionGrain>(inspectionId);
            await grain.CreateInspectionAsync(
                Uri.UnescapeDataString(locationId), req.LocationName,
                req.InspectionType, req.InspectionDateTime,
                req.InspectorId, req.InspectorName,
                req.WitnessId, req.WitnessName,
                req.SecondWitnessId, req.SecondWitnessName,
                req.Notes);

            CSInspectionState state = await grain.GetInspectionAsync();
            await GetInspectionLog(locationId).UpsertInspectionAsync(new CSInspectionSummaryEntry
            {
                InspectionId = inspectionId,
                LocationId = Uri.UnescapeDataString(locationId),
                InspectionType = req.InspectionType,
                InspectionDateTime = req.InspectionDateTime,
                InspectorName = req.InspectorName,
                OverallResult = CSInspectionResult.Passed,
                TotalDiscrepancies = 0,
                CreatedDate = state.CreatedDate,
            });

            return Created(
                $"/api/controlledsubstances/inspections/{Uri.EscapeDataString(inspectionId)}",
                new { inspectionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating CS inspection for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred creating the inspection.");
        }
    }

    [HttpPost("inspections/{inspectionId}/counts")]
    public async Task<IActionResult> AddDrugCount(string inspectionId, [FromBody] CSInspectionCount count)
    {
        try
        {
            ICSInspectionGrain grain = _grainFactory.GetGrain<ICSInspectionGrain>(Uri.UnescapeDataString(inspectionId));
            await grain.AddDrugCountAsync(count);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding drug count to inspection {InspectionId}", inspectionId);
            return StatusCode(500, "An error occurred adding the drug count.");
        }
    }

    [HttpPost("inspections/{inspectionId}/finalize")]
    public async Task<IActionResult> FinalizeInspection(string inspectionId, [FromBody] FinalizeCSInspectionRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(inspectionId);
            ICSInspectionGrain grain = _grainFactory.GetGrain<ICSInspectionGrain>(decodedId);
            await grain.FinalizeInspectionAsync(
                req.Result, req.DiscrepanciesReported,
                req.ReportedToId, req.ReportedToName,
                req.InvestigationNotes);

            CSInspectionState state = await grain.GetInspectionAsync();
            ICSInspectionLogGrain log = GetInspectionLog(Uri.EscapeDataString(state.LocationId));
            await log.UpsertInspectionAsync(new CSInspectionSummaryEntry
            {
                InspectionId = decodedId,
                LocationId = state.LocationId,
                InspectionType = state.InspectionType,
                InspectionDateTime = state.InspectionDateTime,
                InspectorName = state.InspectorName,
                OverallResult = state.OverallResult,
                TotalDiscrepancies = state.TotalDiscrepancies,
                CreatedDate = state.CreatedDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing inspection {InspectionId}", inspectionId);
            return StatusCode(500, "An error occurred finalizing the inspection.");
        }
    }

    // ── Dispense Log Queries ──────────────────────────────────────────────────

    [HttpGet("{locationId}/dispenses")]
    public async Task<IActionResult> GetAllDispenses(string locationId)
    {
        try
        {
            List<CSDispenseSummaryEntry> result = await GetDispenseLog(locationId).GetAllRecordsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CS dispenses for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving dispense records.");
        }
    }

    [HttpGet("{locationId}/dispenses/drug/{drugId}")]
    public async Task<IActionResult> GetDispensesByDrug(string locationId, string drugId)
    {
        try
        {
            List<CSDispenseSummaryEntry> result = await GetDispenseLog(locationId)
                .GetRecordsByDrugAsync(Uri.UnescapeDataString(drugId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CS dispenses by drug for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving dispense records.");
        }
    }

    [HttpGet("{locationId}/dispenses/schedule/{schedule}")]
    public async Task<IActionResult> GetDispensesBySchedule(string locationId, DEADrugSchedule schedule)
    {
        try
        {
            List<CSDispenseSummaryEntry> result = await GetDispenseLog(locationId).GetRecordsByScheduleAsync(schedule);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CS dispenses by schedule for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving dispense records.");
        }
    }

    [HttpGet("{locationId}/dispenses/daterange")]
    public async Task<IActionResult> GetDispensesByDateRange(string locationId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        try
        {
            List<CSDispenseSummaryEntry> result = await GetDispenseLog(locationId).GetRecordsByDateRangeAsync(from, to);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CS dispenses by date range for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving dispense records.");
        }
    }

    [HttpGet("dispenses/{recordId}")]
    public async Task<IActionResult> GetDispenseRecord(string recordId)
    {
        try
        {
            ICSDispenseRecordGrain grain = _grainFactory.GetGrain<ICSDispenseRecordGrain>(Uri.UnescapeDataString(recordId));
            CSDispenseRecordState result = await grain.GetRecordAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CS dispense record {RecordId}", recordId);
            return StatusCode(500, "An error occurred retrieving the dispense record.");
        }
    }

    // ── Dispense Lifecycle ────────────────────────────────────────────────────

    [HttpPost("{locationId}/dispenses")]
    public async Task<IActionResult> CreateDispenseRecord(string locationId, [FromBody] CreateCSDispenseRequest req)
    {
        try
        {
            string recordId = $"CS-DISPENSE:{Guid.NewGuid()}";
            ICSDispenseRecordGrain grain = _grainFactory.GetGrain<ICSDispenseRecordGrain>(recordId);
            await grain.CreateRecordAsync(
                Uri.UnescapeDataString(locationId), req.LocationName,
                req.PatientId, req.PatientName, req.PatientDateOfBirth,
                req.DrugId, req.DrugName, req.DEASchedule, req.NdcNumber,
                req.QuantityDispensed, req.UnitOfMeasure, req.RunningBalance,
                req.DispenseType,
                req.PrescriberId, req.PrescriberName, req.PrescriberDEANumber,
                req.DispensedById, req.DispensedByName,
                req.WitnessId, req.WitnessName,
                req.DispenseDateTime, req.PrescriptionNumber, req.OrderId, req.Notes);

            await GetDispenseLog(locationId).UpsertRecordAsync(new CSDispenseSummaryEntry
            {
                RecordId = recordId,
                LocationId = Uri.UnescapeDataString(locationId),
                PatientName = req.PatientName,
                DrugName = req.DrugName,
                DrugId = req.DrugId,
                DrugSchedule = req.DEASchedule,
                QuantityDispensed = req.QuantityDispensed,
                UnitOfMeasure = req.UnitOfMeasure,
                DispensedByName = req.DispensedByName,
                DispenseDateTime = req.DispenseDateTime,
                RunningBalance = req.RunningBalance,
            });

            return Created(
                $"/api/controlledsubstances/dispenses/{Uri.EscapeDataString(recordId)}",
                new { recordId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating CS dispense record for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred creating the dispense record.");
        }
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record CreateCSInspectionRequest(
    string LocationName,
    CSInspectionType InspectionType,
    DateTime InspectionDateTime,
    string InspectorId,
    string InspectorName,
    string WitnessId,
    string WitnessName,
    string? SecondWitnessId,
    string? SecondWitnessName,
    string? Notes);

public record FinalizeCSInspectionRequest(
    CSInspectionResult Result,
    bool DiscrepanciesReported,
    string? ReportedToId,
    string? ReportedToName,
    string? InvestigationNotes);

public record CreateCSDispenseRequest(
    string LocationName,
    string PatientId,
    string PatientName,
    DateTime? PatientDateOfBirth,
    string DrugId,
    string DrugName,
    DEADrugSchedule DEASchedule,
    string? NdcNumber,
    decimal QuantityDispensed,
    string UnitOfMeasure,
    decimal RunningBalance,
    CSDispenseType DispenseType,
    string PrescriberId,
    string PrescriberName,
    string? PrescriberDEANumber,
    string DispensedById,
    string DispensedByName,
    string? WitnessId,
    string? WitnessName,
    DateTime DispenseDateTime,
    string? PrescriptionNumber,
    string? OrderId,
    string? Notes);
