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
[Route("api/ambulatory-copay")]
public class AmbulatoryCopayController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AmbulatoryCopayController> _logger;

    public AmbulatoryCopayController(IGrainFactory grainFactory,
        ILogger<AmbulatoryCopayController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private IAmbulatoryCopaySheetGrain GetSheetGrain(string sheetId) =>
        _grainFactory.GetGrain<IAmbulatoryCopaySheetGrain>($"IB-SHEET:{sheetId}");

    // ─── GET endpoints ─────────────────────────────────────────────────────────

    [HttpGet("{sheetId}")]
    public async Task<IActionResult> GetSheet(string sheetId)
    {
        try
        {
            AmbulatoryCopaySheetState state = await GetSheetGrain(sheetId).GetAsync();
            return Ok(new CopaySheetDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting copay sheet {SheetId}", sheetId);
            return StatusCode(500, "An error occurred retrieving the copay sheet.");
        }
    }

    // ─── POST endpoints ────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateSheet([FromBody] CreateCopaySheetRequest req)
    {
        try
        {
            string sheetId = Guid.NewGuid().ToString();
            IAmbulatoryCopaySheetGrain grain = GetSheetGrain(sheetId);
            string createdId = await grain.CreateAsync(
                req.PatientId, req.EncounterId, req.VisitDate, req.ClinicId, req.ClinicName);
            return Created($"api/ambulatory-copay/{createdId}", new { sheetId = createdId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating copay sheet for patient {PatientId}", req.PatientId);
            return StatusCode(500, "An error occurred creating the copay sheet.");
        }
    }

    [HttpPost("{sheetId}/check-item")]
    public async Task<IActionResult> CheckItem(string sheetId, [FromBody] CheckItemRequest req)
    {
        try
        {
            await GetSheetGrain(sheetId).CheckItemAsync(
                req.ItemCode, req.ItemDescription, req.CheckedByUserId, req.CheckedByUserName);
            return Ok(new { message = "Item checked." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking item {ItemCode} on sheet {SheetId}", req.ItemCode, sheetId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{sheetId}/complete")]
    public async Task<IActionResult> CompleteSheet(string sheetId, [FromBody] CompleteSheetRequest req)
    {
        try
        {
            await GetSheetGrain(sheetId).CompleteAsync(req.CompletedByUserId, req.CompletedByUserName);
            return Ok(new { message = "Sheet completed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing copay sheet {SheetId}", sheetId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{sheetId}/link-billing")]
    public async Task<IActionResult> LinkBillingAction(string sheetId, [FromBody] LinkBillingRequest req)
    {
        try
        {
            await GetSheetGrain(sheetId).LinkBillingActionAsync(req.BillingActionId);
            return Ok(new { message = "Billing action linked." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking billing action to sheet {SheetId}", sheetId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo()
    {
        try
        {
            // Sheet 1: completed encounter
            string sheet1Id = "DEMO-SHEET-001";
            IAmbulatoryCopaySheetGrain sheet1 = GetSheetGrain(sheet1Id);
            await sheet1.CreateAsync("P-001", "ENC-001", DateTime.UtcNow.AddDays(-3), "CL-PRIMARY", "Primary Care Clinic");
            await sheet1.CheckItemAsync("99213", "Office visit, established patient, level 3", "USR-CLERK-1", "Mary Johnson");
            await sheet1.CheckItemAsync("85025", "CBC with differential", "USR-CLERK-1", "Mary Johnson");
            await sheet1.CheckItemAsync("80053", "Comprehensive metabolic panel", "USR-CLERK-1", "Mary Johnson");
            await sheet1.CompleteAsync("USR-CLERK-1", "Mary Johnson");

            // Sheet 2: in-progress encounter
            string sheet2Id = "DEMO-SHEET-002";
            IAmbulatoryCopaySheetGrain sheet2 = GetSheetGrain(sheet2Id);
            await sheet2.CreateAsync("P-002", "ENC-002", DateTime.UtcNow, "CL-CARDIO", "Cardiology Clinic");
            await sheet2.CheckItemAsync("99214", "Office visit, established patient, level 4", "USR-CLERK-2", "Robert Davis");

            // Sheet 3: empty sheet for new patient
            string sheet3Id = "DEMO-SHEET-003";
            IAmbulatoryCopaySheetGrain sheet3 = GetSheetGrain(sheet3Id);
            await sheet3.CreateAsync("P-003", null, DateTime.UtcNow, "CL-SURG", "General Surgery Clinic");

            return Ok(new
            {
                message = "Demo copay sheets loaded",
                sheetIds = new[] { sheet1Id, sheet2Id, sheet3Id }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo copay sheet data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record CopaySheetDto(
    string SheetId,
    string PatientId,
    string? EncounterId,
    DateTime VisitDate,
    string? ClinicId,
    string? ClinicName,
    bool IsComplete,
    string? CompletedByUserId,
    string? CompletedByUserName,
    DateTime? CompletedDateTime,
    int TotalBillableItems,
    List<CopayChecklistItemDto> ChecklistItems,
    List<string> BillingActionIds,
    DateTime CreatedDate)
{
    public CopaySheetDto(AmbulatoryCopaySheetState s)
        : this(s.SheetId, s.PatientId, s.EncounterId, s.VisitDate, s.ClinicId, s.ClinicName,
               s.IsComplete, s.CompletedByUserId, s.CompletedByUserName, s.CompletedDateTime,
               s.TotalBillableItems,
               s.ChecklistItems.Select(i => new CopayChecklistItemDto(i)).ToList(),
               s.BillingActionIds, s.CreatedDate) { }
}

public record CopayChecklistItemDto(
    string ItemCode,
    string ItemDescription,
    bool IsChecked,
    string? CheckedByUserId,
    string? CheckedByUserName,
    DateTime? CheckedDateTime)
{
    public CopayChecklistItemDto(CopayChecklistItem i)
        : this(i.ItemCode, i.ItemDescription, i.IsChecked, i.CheckedByUserId,
               i.CheckedByUserName, i.CheckedDateTime) { }
}

public record CreateCopaySheetRequest(
    string PatientId,
    string? EncounterId,
    DateTime VisitDate,
    string? ClinicId,
    string? ClinicName);

public record CheckItemRequest(
    string ItemCode,
    string ItemDescription,
    string CheckedByUserId,
    string CheckedByUserName);

public record CompleteSheetRequest(
    string CompletedByUserId,
    string CompletedByUserName);

public record LinkBillingRequest(
    string BillingActionId);
