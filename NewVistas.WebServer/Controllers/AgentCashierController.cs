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
/// REST API for Agent Cashier — cashier receipts, daily sessions, reconciliation,
/// and turn-in to fiscal (VistA File #36 AGENT CASHIER).
/// </summary>
[Authorize]
[ApiController]
[Route("api/agentcashier")]
[Produces("application/json")]
public class AgentCashierController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AgentCashierController> _logger;

    public AgentCashierController(
        IGrainFactory grainFactory,
        ILogger<AgentCashierController> logger)
    {
        _grainFactory = grainFactory;
        _logger       = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Receipts ──────────────────────────────────────────────────────────────

    [HttpPost("receipts")]
    public async Task<IActionResult> IssueReceipt([FromBody] IssueReceiptRequest req)
    {
        try
        {
            string receiptId = $"CASHIER-RECEIPT:{Guid.NewGuid()}";
            CashierPaymentMethod method = Enum.TryParse<CashierPaymentMethod>(req.PaymentMethod, out CashierPaymentMethod m)
                ? m
                : CashierPaymentMethod.Cash;

            ICashierReceiptGrain receipt = _grainFactory.GetGrain<ICashierReceiptGrain>(receiptId);
            await receipt.IssueAsync(
                req.ReceiptNumber,
                req.PatientId,
                req.PatientName,
                req.ARAccountId,
                req.Amount,
                method,
                req.CashierId,
                req.CashierName,
                req.SessionId,
                req.CheckNumber,
                req.Notes);

            // Update per-patient receipt index
            ICashierReceiptIndexGrain idx = _grainFactory.GetGrain<ICashierReceiptIndexGrain>(
                $"CASHIER-RECEIPT-IDX:{req.PatientId}");
            await idx.AddOrUpdateAsync(new CashierReceiptIndexEntry
            {
                ReceiptId     = receiptId,
                ReceiptNumber = req.ReceiptNumber,
                PatientId     = req.PatientId,
                ARAccountId   = req.ARAccountId,
                Amount        = req.Amount,
                PaymentMethod = req.PaymentMethod,
                Status        = CashierReceiptStatus.Issued.ToString(),
                ReceiptDate   = DateTime.UtcNow,
            });

            // Update session index running total
            CashierSessionState sessionState = await _grainFactory
                .GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{req.SessionId}")
                .GetAsync();
            ICashierSessionIndexGrain sessionIdx = _grainFactory.GetGrain<ICashierSessionIndexGrain>("CASHIER-SESSION-IDX");
            await sessionIdx.AddOrUpdateAsync(new CashierSessionIndexEntry
            {
                SessionId      = sessionState.SessionId,
                StationId      = sessionState.StationId,
                CashierId      = sessionState.CashierId,
                CashierName    = sessionState.CashierName,
                SessionDate    = sessionState.SessionDate,
                TotalCollected = sessionState.TotalCollected,
                Status         = sessionState.Status.ToString(),
                TurnedInDate   = sessionState.TurnedInDate,
            });

            return Created($"api/agentcashier/receipts/{receiptId}", new { receiptId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing cashier receipt");
            return StatusCode(500, "Error issuing receipt.");
        }
    }

    [HttpPost("receipts/{receiptId}/void")]
    public async Task<IActionResult> VoidReceipt(string receiptId, [FromBody] VoidReceiptRequest req)
    {
        try
        {
            ICashierReceiptGrain receipt = _grainFactory.GetGrain<ICashierReceiptGrain>($"CASHIER-RECEIPT:{receiptId}");
            await receipt.VoidAsync(req.Reason, req.VoidedByUserId);

            // Update patient index entry
            CashierReceiptState state = await receipt.GetAsync();
            ICashierReceiptIndexGrain idx = _grainFactory.GetGrain<ICashierReceiptIndexGrain>(
                $"CASHIER-RECEIPT-IDX:{state.PatientId}");
            await idx.AddOrUpdateAsync(new CashierReceiptIndexEntry
            {
                ReceiptId     = state.ReceiptId,
                ReceiptNumber = state.ReceiptNumber,
                PatientId     = state.PatientId,
                ARAccountId   = state.ARAccountId,
                Amount        = state.Amount,
                PaymentMethod = state.PaymentMethod.ToString(),
                Status        = state.Status.ToString(),
                ReceiptDate   = state.ReceiptDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voiding receipt {ReceiptId}", receiptId);
            return StatusCode(500, "Error voiding receipt.");
        }
    }

    [HttpGet("receipts/{receiptId}")]
    public async Task<IActionResult> GetReceipt(string receiptId)
    {
        try
        {
            ICashierReceiptGrain receipt = _grainFactory.GetGrain<ICashierReceiptGrain>($"CASHIER-RECEIPT:{receiptId}");
            return Ok(await receipt.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting receipt {ReceiptId}", receiptId);
            return StatusCode(500, "Error retrieving receipt.");
        }
    }

    [HttpGet("patients/{patientId}/receipts")]
    public async Task<IActionResult> GetPatientReceipts(string patientId)
    {
        try { return Ok(await W(patientId).GetCashierReceiptsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting receipts for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving receipts.");
        }
    }

    // ─── Sessions ──────────────────────────────────────────────────────────────

    [HttpPost("sessions")]
    public async Task<IActionResult> OpenSession([FromBody] OpenSessionRequest req)
    {
        try
        {
            string sessionId = Guid.NewGuid().ToString();
            ICashierSessionGrain session = _grainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{sessionId}");
            await session.OpenAsync(
                req.StationId,
                req.StationName,
                req.CashierId,
                req.CashierName,
                req.SessionDate,
                req.OpeningBalance);

            ICashierSessionIndexGrain idx = _grainFactory.GetGrain<ICashierSessionIndexGrain>("CASHIER-SESSION-IDX");
            await idx.AddOrUpdateAsync(new CashierSessionIndexEntry
            {
                SessionId      = $"CASHIER-SESSION:{sessionId}",
                StationId      = req.StationId,
                CashierId      = req.CashierId,
                CashierName    = req.CashierName,
                SessionDate    = req.SessionDate,
                TotalCollected = 0m,
                Status         = CashierSessionStatus.Open.ToString(),
                TurnedInDate   = null,
            });

            return Created($"api/agentcashier/sessions/{sessionId}", new { sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening cashier session");
            return StatusCode(500, "Error opening session.");
        }
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        try
        {
            ICashierSessionGrain session = _grainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{sessionId}");
            return Ok(await session.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session {SessionId}", sessionId);
            return StatusCode(500, "Error retrieving session.");
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetAllSessions()
    {
        try
        {
            ICashierSessionIndexGrain idx = _grainFactory.GetGrain<ICashierSessionIndexGrain>("CASHIER-SESSION-IDX");
            return Ok(await idx.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sessions");
            return StatusCode(500, "Error retrieving sessions.");
        }
    }

    [HttpGet("sessions/open")]
    public async Task<IActionResult> GetOpenSessions()
    {
        try
        {
            ICashierSessionIndexGrain idx = _grainFactory.GetGrain<ICashierSessionIndexGrain>("CASHIER-SESSION-IDX");
            return Ok(await idx.GetOpenSessionsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting open sessions");
            return StatusCode(500, "Error retrieving open sessions.");
        }
    }

    [HttpPost("sessions/{sessionId}/close")]
    public async Task<IActionResult> CloseSession(string sessionId, [FromBody] CloseSessionRequest req)
    {
        try
        {
            ICashierSessionGrain session = _grainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{sessionId}");
            await session.CloseAsync(req.ActualBalance, req.Notes);

            CashierSessionState state = await session.GetAsync();
            ICashierSessionIndexGrain idx = _grainFactory.GetGrain<ICashierSessionIndexGrain>("CASHIER-SESSION-IDX");
            await idx.AddOrUpdateAsync(new CashierSessionIndexEntry
            {
                SessionId      = state.SessionId,
                StationId      = state.StationId,
                CashierId      = state.CashierId,
                CashierName    = state.CashierName,
                SessionDate    = state.SessionDate,
                TotalCollected = state.TotalCollected,
                Status         = state.Status.ToString(),
                TurnedInDate   = state.TurnedInDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing session {SessionId}", sessionId);
            return StatusCode(500, "Error closing session.");
        }
    }

    [HttpPost("sessions/{sessionId}/turn-in")]
    public async Task<IActionResult> TurnInSession(string sessionId, [FromBody] TurnInRequest req)
    {
        try
        {
            ICashierSessionGrain session = _grainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{sessionId}");
            await session.TurnInAsync(req.TurnedInAmount, req.TurnedInToUserId, req.TurnedInReceiptNumber);

            CashierSessionState state = await session.GetAsync();
            ICashierSessionIndexGrain idx = _grainFactory.GetGrain<ICashierSessionIndexGrain>("CASHIER-SESSION-IDX");
            await idx.AddOrUpdateAsync(new CashierSessionIndexEntry
            {
                SessionId      = state.SessionId,
                StationId      = state.StationId,
                CashierId      = state.CashierId,
                CashierName    = state.CashierName,
                SessionDate    = state.SessionDate,
                TotalCollected = state.TotalCollected,
                Status         = state.Status.ToString(),
                TurnedInDate   = state.TurnedInDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning in session {SessionId}", sessionId);
            return StatusCode(500, "Error processing turn-in.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record IssueReceiptRequest(
    string ReceiptNumber,
    string PatientId,
    string PatientName,
    string ARAccountId,
    decimal Amount,
    string PaymentMethod,
    string CashierId,
    string CashierName,
    string SessionId,
    string? CheckNumber,
    string? Notes);

public record VoidReceiptRequest(string Reason, string VoidedByUserId);

public record OpenSessionRequest(
    string StationId,
    string StationName,
    string CashierId,
    string CashierName,
    DateTime SessionDate,
    decimal OpeningBalance);

public record CloseSessionRequest(decimal ActualBalance, string? Notes);

public record TurnInRequest(
    decimal TurnedInAmount,
    string TurnedInToUserId,
    string? TurnedInReceiptNumber);
