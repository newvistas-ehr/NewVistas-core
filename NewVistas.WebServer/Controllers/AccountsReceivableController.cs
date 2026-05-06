// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Accounts Receivable — debtor records, AR accounts, transactions,
/// batch payments, and site parameters (VistA Files #340, #342, #344, #430, #433).
/// </summary>
[Authorize]
[ApiController]
[Route("api/accountsreceivable")]
[Produces("application/json")]
public class AccountsReceivableController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AccountsReceivableController> _logger;

    public AccountsReceivableController(
        IGrainFactory grainFactory,
        ILogger<AccountsReceivableController> logger)
    {
        _grainFactory = grainFactory;
        _logger       = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── AR Debtor (File #340) ─────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/debtor")]
    public async Task<IActionResult> GetARDebtor(string patientId)
    {
        try { return Ok(await W(patientId).GetARDebtorAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AR debtor for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving AR debtor.");
        }
    }

    // ─── AR Accounts (File #430) ───────────────────────────────────────────────

    [HttpGet("patients/{patientId}/accounts")]
    public async Task<IActionResult> GetARAccounts(string patientId)
    {
        try { return Ok(await W(patientId).GetARAccountsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AR accounts for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving AR accounts.");
        }
    }

    [HttpGet("patients/{patientId}/accounts/active")]
    public async Task<IActionResult> GetActiveARAccounts(string patientId)
    {
        try { return Ok(await W(patientId).GetActiveARAccountsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active AR accounts for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving active AR accounts.");
        }
    }

    [HttpGet("patients/{patientId}/accounts/{accountId}")]
    public async Task<IActionResult> GetARAccount(string patientId, string accountId)
    {
        try { return Ok(await W(patientId).GetARAccountAsync(accountId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AR account {AccountId} for {PatientId}", accountId, patientId);
            return StatusCode(500, "Error retrieving AR account.");
        }
    }

    [HttpPost("patients/{patientId}/accounts")]
    public async Task<IActionResult> CreateARAccount(
        string patientId,
        [FromBody] CreateARAccountRequest req)
    {
        try
        {
            string accountId = await W(patientId).CreateARAccountAsync(
                req.BillingActionId, req.ARCategory, req.OriginalAmount, req.DueDate);
            return Created($"api/accountsreceivable/patients/{patientId}/accounts/{accountId}",
                new { ARAccountId = accountId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating AR account for {PatientId}", patientId);
            return StatusCode(500, "Error creating AR account.");
        }
    }

    [HttpPost("patients/{patientId}/accounts/{accountId}/payments")]
    public async Task<IActionResult> PostARPayment(
        string patientId,
        string accountId,
        [FromBody] PostARPaymentRequest req)
    {
        try
        {
            string txnId = await W(patientId).PostARPaymentAsync(
                accountId, req.Amount, req.PaymentMethod,
                req.AppliedByUserId, req.AppliedByUserName,
                req.ReceiptNumber, req.CheckNumber, req.Notes);
            return Ok(new { TransactionId = txnId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting payment to AR account {AccountId} for {PatientId}", accountId, patientId);
            return StatusCode(500, "Error posting payment.");
        }
    }

    [HttpPost("patients/{patientId}/accounts/{accountId}/adjustments")]
    public async Task<IActionResult> PostARAdjustment(
        string patientId,
        string accountId,
        [FromBody] PostARAdjustmentRequest req)
    {
        try
        {
            string txnId = await W(patientId).PostARAdjustmentAsync(
                accountId, req.Amount, req.AdjustmentType,
                req.AppliedByUserId, req.AppliedByUserName, req.Notes);
            return Ok(new { TransactionId = txnId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting adjustment to AR account {AccountId} for {PatientId}", accountId, patientId);
            return StatusCode(500, "Error posting adjustment.");
        }
    }

    [HttpPost("patients/{patientId}/accounts/{accountId}/waive")]
    public async Task<IActionResult> WaiveARAccount(
        string patientId,
        string accountId,
        [FromBody] WaiveARAccountRequest req)
    {
        try
        {
            string txnId = await W(patientId).WaiveARAccountAsync(
                accountId, req.WaivedAmount,
                req.WaivedByUserId, req.WaivedByUserName, req.Reason);
            return Ok(new { TransactionId = txnId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiving AR account {AccountId} for {PatientId}", accountId, patientId);
            return StatusCode(500, "Error waiving AR account.");
        }
    }

    // ─── Batch Payments (File #344) ───────────────────────────────────────────

    [HttpGet("batch-payments")]
    public async Task<IActionResult> GetAllBatchPayments()
    {
        try
        {
            IARBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IARBatchPaymentIndexGrain>("AR-BATCH-IDX");
            return Ok(await idx.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all batch payments");
            return StatusCode(500, "Error retrieving batch payments.");
        }
    }

    [HttpGet("batch-payments/unposted")]
    public async Task<IActionResult> GetUnpostedBatchPayments()
    {
        try
        {
            IARBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IARBatchPaymentIndexGrain>("AR-BATCH-IDX");
            return Ok(await idx.GetUnpostedAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unposted batch payments");
            return StatusCode(500, "Error retrieving unposted batch payments.");
        }
    }

    [HttpGet("batch-payments/{batchId}")]
    public async Task<IActionResult> GetBatchPayment(string batchId)
    {
        try
        {
            IARBatchPaymentGrain batch = _grainFactory.GetGrain<IARBatchPaymentGrain>($"AR-BATCH:{batchId}");
            return Ok(await batch.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch payment {BatchId}", batchId);
            return StatusCode(500, "Error retrieving batch payment.");
        }
    }

    [HttpPost("batch-payments")]
    public async Task<IActionResult> CreateBatchPayment([FromBody] CreateBatchPaymentRequest req)
    {
        try
        {
            string batchId = Guid.NewGuid().ToString();
            IARBatchPaymentGrain batch = _grainFactory.GetGrain<IARBatchPaymentGrain>($"AR-BATCH:{batchId}");
            await batch.CreateAsync(req.FacilityId, req.FacilityName, req.BatchDate,
                req.PaymentMethod, req.CheckNumber, req.Notes);

            IARBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IARBatchPaymentIndexGrain>("AR-BATCH-IDX");
            await idx.AddOrUpdateAsync(new ARBatchPaymentIndexEntry
            {
                BatchId       = batchId,
                BatchDate     = req.BatchDate,
                FacilityName  = req.FacilityName,
                PaymentMethod = req.PaymentMethod,
                TotalAmount   = 0m,
                IsPosted      = false,
                PaymentCount  = 0,
            });

            return Created($"api/accountsreceivable/batch-payments/{batchId}", new { BatchId = batchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch payment");
            return StatusCode(500, "Error creating batch payment.");
        }
    }

    [HttpPost("batch-payments/{batchId}/payments")]
    public async Task<IActionResult> AddPaymentToBatch(
        string batchId,
        [FromBody] AddBatchPaymentLineRequest req)
    {
        try
        {
            IARBatchPaymentGrain batch = _grainFactory.GetGrain<IARBatchPaymentGrain>($"AR-BATCH:{batchId}");
            await batch.AddPaymentAsync(
                req.ARAccountId, req.PatientId, req.PatientName,
                req.Amount, req.ReceiptNumber);

            ARBatchPaymentState state = await batch.GetAsync();
            IARBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IARBatchPaymentIndexGrain>("AR-BATCH-IDX");
            await idx.AddOrUpdateAsync(new ARBatchPaymentIndexEntry
            {
                BatchId       = batchId,
                BatchDate     = state.BatchDate,
                FacilityName  = state.FacilityName,
                PaymentMethod = state.PaymentMethod,
                TotalAmount   = state.TotalAmount,
                IsPosted      = state.IsPosted,
                PaymentCount  = state.Payments.Count,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding payment to batch {BatchId}", batchId);
            return StatusCode(500, "Error adding payment to batch.");
        }
    }

    [HttpPost("batch-payments/{batchId}/post")]
    public async Task<IActionResult> PostBatchPayment(
        string batchId,
        [FromBody] PostBatchRequest req)
    {
        try
        {
            IARBatchPaymentGrain batch = _grainFactory.GetGrain<IARBatchPaymentGrain>($"AR-BATCH:{batchId}");
            await batch.PostAsync(req.PostedByUserId, req.PostedByUserName);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting batch {BatchId}", batchId);
            return StatusCode(500, "Error posting batch payment.");
        }
    }

    // ─── Site Parameters (File #342) ──────────────────────────────────────────

    [HttpGet("site-parameters")]
    public async Task<IActionResult> GetSiteParameters()
    {
        try
        {
            IARSiteParametersGrain site = _grainFactory.GetGrain<IARSiteParametersGrain>("AR-SITE-PARAMS");
            return Ok(await site.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AR site parameters");
            return StatusCode(500, "Error retrieving AR site parameters.");
        }
    }

    [HttpPut("site-parameters")]
    public async Task<IActionResult> UpdateSiteParameters([FromBody] UpdateARSiteParametersRequest req)
    {
        try
        {
            IARSiteParametersGrain site = _grainFactory.GetGrain<IARSiteParametersGrain>("AR-SITE-PARAMS");
            await site.UpdateAsync(
                req.SiteName, req.ARFacilityNumber,
                req.InterestRate, req.AdminCost, req.PenaltyRate,
                req.MinimumPaymentAmount, req.MaxPaymentPlanMonths,
                req.IsAutoInterestEnabled, req.IsPenaltyEnabled,
                req.StatementFrequencyDays, req.CollectionThreshold,
                req.IsFmsEnabled, req.IsTreasuryOffsetEnabled,
                req.UpdatedByUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating AR site parameters");
            return StatusCode(500, "Error updating AR site parameters.");
        }
    }

    // ─── Interest & Charges ────────────────────────────────────────────────────

    [HttpPost("patients/{patientId}/accounts/{accountId}/accrue-interest")]
    public async Task<IActionResult> AccrueInterest(
        string patientId,
        string accountId,
        [FromBody] AccrueChargeRequest req)
    {
        try
        {
            await W(patientId).AccrueARInterestAsync(accountId, req.Amount, req.AppliedByUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accruing interest on account {AccountId}", accountId);
            return StatusCode(500, "Error accruing interest.");
        }
    }

    [HttpPost("patients/{patientId}/accounts/{accountId}/accrue-penalty")]
    public async Task<IActionResult> AccruePenalty(
        string patientId,
        string accountId,
        [FromBody] AccrueChargeRequest req)
    {
        try
        {
            await W(patientId).AccrueARPenaltyAsync(accountId, req.Amount, req.AppliedByUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accruing penalty on account {AccountId}", accountId);
            return StatusCode(500, "Error accruing penalty.");
        }
    }

    [HttpPost("patients/{patientId}/accounts/{accountId}/accrue-admin-cost")]
    public async Task<IActionResult> AccrueAdminCost(
        string patientId,
        string accountId,
        [FromBody] AccrueChargeRequest req)
    {
        try
        {
            await W(patientId).AccrueARAdminCostAsync(accountId, req.Amount, req.AppliedByUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accruing admin cost on account {AccountId}", accountId);
            return StatusCode(500, "Error accruing admin cost.");
        }
    }

    // ─── Treasury Offset Program (TOP) ────────────────────────────────────────

    [HttpPost("patients/{patientId}/accounts/{accountId}/refer-to-top")]
    public async Task<IActionResult> ReferToTop(
        string patientId,
        string accountId,
        [FromBody] ReferToTopRequest req)
    {
        try
        {
            IARAccountGrain acct = _grainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{accountId}");
            ARAccountState state = await acct.GetAsync();

            string referralId = Guid.NewGuid().ToString();
            await acct.ReferToTopAsync(referralId, req.ReferredAmount, req.ReferredByUserId, req.ReferredByUserName);

            ITopReferralGrain referral = _grainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{referralId}");
            await referral.CreateAsync(
                accountId, patientId, req.PatientName,
                req.ReferredAmount, state.CurrentBalance,
                req.ReferredByUserId, req.ReferredByUserName, req.Notes);

            ITopReferralIndexGrain idx = _grainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-IDX");
            await idx.AddOrUpdateAsync(new TopReferralIndexEntry(
                referralId, accountId, patientId, req.PatientName,
                req.ReferredAmount, TopReferralStatus.Pending, DateTime.UtcNow, 0m));

            return Created($"api/accountsreceivable/top-referrals/{referralId}", new { ReferralId = referralId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error referring account {AccountId} to TOP", accountId);
            return StatusCode(500, "Error referring account to TOP.");
        }
    }

    [HttpGet("top-referrals")]
    public async Task<IActionResult> GetAllTopReferrals()
    {
        try
        {
            ITopReferralIndexGrain idx = _grainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-IDX");
            return Ok(await idx.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TOP referrals");
            return StatusCode(500, "Error retrieving TOP referrals.");
        }
    }

    [HttpGet("top-referrals/pending")]
    public async Task<IActionResult> GetPendingTopReferrals()
    {
        try
        {
            ITopReferralIndexGrain idx = _grainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-IDX");
            return Ok(await idx.GetPendingAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending TOP referrals");
            return StatusCode(500, "Error retrieving pending TOP referrals.");
        }
    }

    [HttpGet("top-referrals/{referralId}")]
    public async Task<IActionResult> GetTopReferral(string referralId)
    {
        try
        {
            ITopReferralGrain referral = _grainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{referralId}");
            return Ok(await referral.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TOP referral {ReferralId}", referralId);
            return StatusCode(500, "Error retrieving TOP referral.");
        }
    }

    [HttpPost("top-referrals/{referralId}/offset")]
    public async Task<IActionResult> RecordTopOffset(
        string referralId,
        [FromBody] RecordTopOffsetRequest req)
    {
        try
        {
            ITopReferralGrain referral = _grainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{referralId}");
            await referral.RecordOffsetAsync(req.OffsetAmount, req.OffsetDate);
            TopReferralState refState = await referral.GetAsync();

            IARAccountGrain acct = _grainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{refState.ARAccountId}");
            await acct.RecordTopOffsetAsync(req.OffsetAmount, req.ProcessedByUserId, req.ProcessedByUserName);

            ITopReferralIndexGrain idx = _grainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-IDX");
            await idx.AddOrUpdateAsync(new TopReferralIndexEntry(
                referralId, refState.ARAccountId, refState.PatientId, refState.PatientName,
                refState.ReferredAmount, refState.Status, refState.ReferralDate, refState.OffsetAmount));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording TOP offset for referral {ReferralId}", referralId);
            return StatusCode(500, "Error recording TOP offset.");
        }
    }

    [HttpPost("top-referrals/{referralId}/withdraw")]
    public async Task<IActionResult> WithdrawTopReferral(
        string referralId,
        [FromBody] WithdrawTopRequest req)
    {
        try
        {
            ITopReferralGrain referral = _grainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{referralId}");
            await referral.WithdrawAsync(req.Reason);
            TopReferralState refState = await referral.GetAsync();

            IARAccountGrain acct = _grainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{refState.ARAccountId}");
            await acct.WithdrawTopReferralAsync();

            ITopReferralIndexGrain idx = _grainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-IDX");
            await idx.AddOrUpdateAsync(new TopReferralIndexEntry(
                referralId, refState.ARAccountId, refState.PatientId, refState.PatientName,
                refState.ReferredAmount, refState.Status, refState.ReferralDate, refState.OffsetAmount));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing TOP referral {ReferralId}", referralId);
            return StatusCode(500, "Error withdrawing TOP referral.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record CreateARAccountRequest(
    string? BillingActionId,
    string ARCategory,
    decimal OriginalAmount,
    DateTime? DueDate);

public record PostARPaymentRequest(
    decimal Amount,
    string PaymentMethod,
    string AppliedByUserId,
    string AppliedByUserName,
    string? ReceiptNumber,
    string? CheckNumber,
    string? Notes);

public record PostARAdjustmentRequest(
    decimal Amount,
    string AdjustmentType,
    string AppliedByUserId,
    string AppliedByUserName,
    string? Notes);

public record WaiveARAccountRequest(
    decimal WaivedAmount,
    string WaivedByUserId,
    string WaivedByUserName,
    string Reason);

public record CreateBatchPaymentRequest(
    string? FacilityId,
    string? FacilityName,
    DateTime BatchDate,
    string PaymentMethod,
    string? CheckNumber,
    string? Notes);

public record AddBatchPaymentLineRequest(
    string ARAccountId,
    string PatientId,
    string PatientName,
    decimal Amount,
    string? ReceiptNumber);

public record PostBatchRequest(
    string PostedByUserId,
    string PostedByUserName);

public record UpdateARSiteParametersRequest(
    string SiteName,
    string ARFacilityNumber,
    decimal InterestRate,
    decimal AdminCost,
    decimal PenaltyRate,
    decimal MinimumPaymentAmount,
    int MaxPaymentPlanMonths,
    bool IsAutoInterestEnabled,
    bool IsPenaltyEnabled,
    int StatementFrequencyDays,
    decimal CollectionThreshold,
    bool IsFmsEnabled,
    bool IsTreasuryOffsetEnabled,
    string UpdatedByUserId);

public record AccrueChargeRequest(
    decimal Amount,
    string AppliedByUserId);

public record ReferToTopRequest(
    decimal ReferredAmount,
    string PatientName,
    string ReferredByUserId,
    string ReferredByUserName,
    string? Notes);

public record RecordTopOffsetRequest(
    decimal OffsetAmount,
    DateTime OffsetDate,
    string ProcessedByUserId,
    string ProcessedByUserName);

public record WithdrawTopRequest(string Reason);
