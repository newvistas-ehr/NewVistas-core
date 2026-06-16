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
/// REST API for Fee Basis community care — authorizations, invoices, vendors, site parameters,
/// and income thresholds (VistA Files #162, #162.1, #162.5, #162.6, #408.15).
/// </summary>
[Authorize]
[ApiController]
[Route("api/feebasis")]
[Produces("application/json")]
public class FeeBasisController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<FeeBasisController> _logger;

    public FeeBasisController(
        IGrainFactory grainFactory,
        ILogger<FeeBasisController> logger)
    {
        _grainFactory = grainFactory;
        _logger       = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Fee Patient (File #162 patient summary) ───────────────────────────────

    [HttpGet("patients/{patientId}/fee-patient")]
    public async Task<IActionResult> GetFeePatient(string patientId)
    {
        try { return Ok(await W(patientId).GetFeePatientAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fee patient for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving fee patient.");
        }
    }

    // ─── Authorizations (File #162.6) ─────────────────────────────────────────

    [HttpGet("patients/{patientId}/authorizations")]
    public async Task<IActionResult> GetAuthorizations(string patientId)
    {
        try { return Ok(await W(patientId).GetFeeAuthorizationsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorizations for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving authorizations.");
        }
    }

    [HttpGet("patients/{patientId}/authorizations/{authId}")]
    public async Task<IActionResult> GetAuthorization(string patientId, string authId)
    {
        try { return Ok(await W(patientId).GetFeeAuthorizationAsync(authId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorization {AuthId}", authId);
            return StatusCode(500, "Error retrieving authorization.");
        }
    }

    [HttpPost("patients/{patientId}/authorizations")]
    public async Task<IActionResult> CreateAuthorization(
        string patientId,
        [FromBody] CreateAuthorizationRequest req)
    {
        try
        {
            string authId = await W(patientId).CreateFeeAuthorizationAsync(
                req.VendorId,
                req.VendorName,
                req.ServiceType,
                req.AuthorizationDate,
                req.EffectiveDate,
                req.ExpirationDate,
                req.AuthorizedAmount,
                req.AuthorizedByUserId,
                req.AuthorizedByUserName,
                req.ServiceDescription,
                req.MaxVisits,
                req.DiagnosisCode,
                req.AuthorizationNumber,
                req.Notes);
            return Created($"api/feebasis/patients/{patientId}/authorizations/{authId}", new { authId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating authorization for {PatientId}", patientId);
            return StatusCode(500, "Error creating authorization.");
        }
    }

    [HttpPost("authorizations/{authId}/suspend")]
    public async Task<IActionResult> SuspendAuthorization(string authId, [FromBody] SuspendCancelRequest req)
    {
        try
        {
            IFeeAuthorizationGrain auth = _grainFactory.GetGrain<IFeeAuthorizationGrain>($"FEE-AUTH:{authId}");
            await auth.SuspendAsync(req.Reason, req.UserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending authorization {AuthId}", authId);
            return StatusCode(500, "Error suspending authorization.");
        }
    }

    [HttpPost("authorizations/{authId}/cancel")]
    public async Task<IActionResult> CancelAuthorization(string authId, [FromBody] SuspendCancelRequest req)
    {
        try
        {
            IFeeAuthorizationGrain auth = _grainFactory.GetGrain<IFeeAuthorizationGrain>($"FEE-AUTH:{authId}");
            await auth.CancelAsync(req.Reason, req.UserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling authorization {AuthId}", authId);
            return StatusCode(500, "Error cancelling authorization.");
        }
    }

    // ─── Invoices (File #162.1) ────────────────────────────────────────────────

    [HttpGet("patients/{patientId}/invoices")]
    public async Task<IActionResult> GetInvoices(string patientId)
    {
        try { return Ok(await W(patientId).GetFeeInvoicesAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoices for {PatientId}", patientId);
            return StatusCode(500, "Error retrieving invoices.");
        }
    }

    [HttpPost("authorizations/{authId}/invoices")]
    public async Task<IActionResult> SubmitInvoice(string authId, [FromBody] SubmitInvoiceRequest req)
    {
        try
        {
            string invoiceId = $"FEE-INVOICE:{Guid.NewGuid()}";
            IFeeInvoiceGrain invoice = _grainFactory.GetGrain<IFeeInvoiceGrain>(invoiceId);
            await invoice.SubmitAsync(
                $"FEE-AUTH:{authId}",
                req.PatientId,
                req.VendorId,
                req.VendorName,
                req.InvoiceNumber,
                req.ServiceDate,
                req.ServiceType,
                req.BilledAmount,
                req.DiagnosisCode,
                req.ProcedureCodes ?? new List<string>(),
                req.ServiceDateEnd,
                req.Notes);

            // Update patient invoice index
            IFeeInvoiceIndexGrain idx = _grainFactory.GetGrain<IFeeInvoiceIndexGrain>($"FEE-INVOICE-IDX:{req.PatientId}");
            await idx.AddOrUpdateAsync(new FeeInvoiceIndexEntry
            {
                InvoiceId       = invoiceId,
                AuthorizationId = $"FEE-AUTH:{authId}",
                VendorName      = req.VendorName,
                ServiceType     = req.ServiceType,
                Status          = FeeInvoiceStatus.Received.ToString(),
                BilledAmount    = req.BilledAmount,
                PaidAmount      = null,
                ServiceDate     = req.ServiceDate,
            });

            return Created($"api/feebasis/invoices/{invoiceId}", new { invoiceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting invoice for authorization {AuthId}", authId);
            return StatusCode(500, "Error submitting invoice.");
        }
    }

    [HttpPost("invoices/{invoiceId}/approve")]
    public async Task<IActionResult> ApproveInvoice(string invoiceId, [FromBody] ApproveInvoiceRequest req)
    {
        try
        {
            IFeeInvoiceGrain invoice = _grainFactory.GetGrain<IFeeInvoiceGrain>($"FEE-INVOICE:{invoiceId}");
            await invoice.ApproveAsync(req.ApprovedAmount, req.ReviewedByUserId, req.ReviewerName);

            // Update index entry status
            FeeInvoiceState state = await invoice.GetAsync();
            IFeeInvoiceIndexGrain idx = _grainFactory.GetGrain<IFeeInvoiceIndexGrain>($"FEE-INVOICE-IDX:{state.PatientId}");
            await idx.AddOrUpdateAsync(new FeeInvoiceIndexEntry
            {
                InvoiceId       = state.InvoiceId,
                AuthorizationId = state.AuthorizationId,
                VendorName      = state.VendorName,
                ServiceType     = state.ServiceType,
                Status          = state.Status.ToString(),
                BilledAmount    = state.BilledAmount,
                PaidAmount      = state.PaidAmount,
                ServiceDate     = state.ServiceDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving invoice {InvoiceId}", invoiceId);
            return StatusCode(500, "Error approving invoice.");
        }
    }

    [HttpPost("invoices/{invoiceId}/reject")]
    public async Task<IActionResult> RejectInvoice(string invoiceId, [FromBody] RejectInvoiceRequest req)
    {
        try
        {
            IFeeInvoiceGrain invoice = _grainFactory.GetGrain<IFeeInvoiceGrain>($"FEE-INVOICE:{invoiceId}");
            await invoice.RejectAsync(req.Reason, req.ReviewedByUserId, req.ReviewerName);

            FeeInvoiceState state = await invoice.GetAsync();
            IFeeInvoiceIndexGrain idx = _grainFactory.GetGrain<IFeeInvoiceIndexGrain>($"FEE-INVOICE-IDX:{state.PatientId}");
            await idx.AddOrUpdateAsync(new FeeInvoiceIndexEntry
            {
                InvoiceId       = state.InvoiceId,
                AuthorizationId = state.AuthorizationId,
                VendorName      = state.VendorName,
                ServiceType     = state.ServiceType,
                Status          = state.Status.ToString(),
                BilledAmount    = state.BilledAmount,
                PaidAmount      = state.PaidAmount,
                ServiceDate     = state.ServiceDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting invoice {InvoiceId}", invoiceId);
            return StatusCode(500, "Error rejecting invoice.");
        }
    }

    [HttpPost("invoices/{invoiceId}/pay")]
    public async Task<IActionResult> PayInvoice(string invoiceId, [FromBody] PayInvoiceRequest req)
    {
        try
        {
            IFeeInvoiceGrain invoice = _grainFactory.GetGrain<IFeeInvoiceGrain>($"FEE-INVOICE:{invoiceId}");
            await invoice.PayAsync(req.PaidAmount, req.PaymentMethod, req.CheckNumber, req.PaymentDate);

            FeeInvoiceState state = await invoice.GetAsync();
            IFeeInvoiceIndexGrain idx = _grainFactory.GetGrain<IFeeInvoiceIndexGrain>($"FEE-INVOICE-IDX:{state.PatientId}");
            await idx.AddOrUpdateAsync(new FeeInvoiceIndexEntry
            {
                InvoiceId       = state.InvoiceId,
                AuthorizationId = state.AuthorizationId,
                VendorName      = state.VendorName,
                ServiceType     = state.ServiceType,
                Status          = state.Status.ToString(),
                BilledAmount    = state.BilledAmount,
                PaidAmount      = state.PaidAmount,
                ServiceDate     = state.ServiceDate,
            });

            // Also update authorization index entry
            FeeAuthorizationState authState = await _grainFactory
                .GetGrain<IFeeAuthorizationGrain>(state.AuthorizationId)
                .GetAsync();
            IFeeAuthorizationIndexGrain authIdx = _grainFactory.GetGrain<IFeeAuthorizationIndexGrain>(
                $"FEE-AUTH-IDX:{state.PatientId}");
            await authIdx.AddOrUpdateAsync(new FeeAuthorizationIndexEntry
            {
                AuthorizationId   = authState.AuthorizationId,
                PatientId         = authState.PatientId,
                VendorName        = authState.VendorName,
                ServiceType       = authState.ServiceType.ToString(),
                Status            = authState.Status.ToString(),
                AuthorizedAmount  = authState.AuthorizedAmount,
                SpentAmount       = authState.SpentAmount,
                AuthorizationDate = authState.AuthorizationDate,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error paying invoice {InvoiceId}", invoiceId);
            return StatusCode(500, "Error paying invoice.");
        }
    }

    // ─── Vendors (File #162.5) ─────────────────────────────────────────────────

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors([FromQuery] bool activeOnly = false)
    {
        try
        {
            IFeeVendorIndexGrain idx = _grainFactory.GetGrain<IFeeVendorIndexGrain>("FEE-VENDOR-IDX");
            List<FeeVendorIndexEntry> vendors = activeOnly
                ? await idx.GetActiveAsync()
                : await idx.GetAllAsync();
            return Ok(vendors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vendors");
            return StatusCode(500, "Error retrieving vendors.");
        }
    }

    [HttpGet("vendors/{vendorId}")]
    public async Task<IActionResult> GetVendor(string vendorId)
    {
        try
        {
            IFeeVendorGrain vendor = _grainFactory.GetGrain<IFeeVendorGrain>($"FEE-VENDOR:{vendorId}");
            return Ok(await vendor.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vendor {VendorId}", vendorId);
            return StatusCode(500, "Error retrieving vendor.");
        }
    }

    [HttpPost("vendors")]
    public async Task<IActionResult> CreateVendor([FromBody] CreateVendorRequest req)
    {
        try
        {
            string vendorId = Guid.NewGuid().ToString();
            IFeeVendorGrain vendor = _grainFactory.GetGrain<IFeeVendorGrain>($"FEE-VENDOR:{vendorId}");
            await vendor.CreateAsync(
                req.VendorName,
                req.VendorType,
                req.SpecialtyCode,
                req.SpecialtyName,
                req.NPI,
                req.TaxId,
                req.Address,
                req.Phone,
                req.Fax,
                req.ContractNumber,
                req.ContractStartDate,
                req.ContractEndDate,
                req.Notes);

            IFeeVendorIndexGrain idx = _grainFactory.GetGrain<IFeeVendorIndexGrain>("FEE-VENDOR-IDX");
            await idx.AddOrUpdateAsync(new FeeVendorIndexEntry
            {
                VendorId      = $"FEE-VENDOR:{vendorId}",
                VendorName    = req.VendorName,
                VendorType    = req.VendorType,
                SpecialtyName = req.SpecialtyName,
                IsActive      = true,
            });

            return Created($"api/feebasis/vendors/{vendorId}", new { vendorId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor");
            return StatusCode(500, "Error creating vendor.");
        }
    }

    // ─── Site Parameters ───────────────────────────────────────────────────────

    [HttpGet("site-parameters")]
    public async Task<IActionResult> GetSiteParameters()
    {
        try
        {
            IFeeSiteParametersGrain grain = _grainFactory.GetGrain<IFeeSiteParametersGrain>("FEE-SITE-PARAMS");
            return Ok(await grain.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fee site parameters");
            return StatusCode(500, "Error retrieving site parameters.");
        }
    }

    [HttpPut("site-parameters")]
    public async Task<IActionResult> UpdateSiteParameters([FromBody] UpdateSiteParametersRequest req)
    {
        try
        {
            IFeeSiteParametersGrain grain = _grainFactory.GetGrain<IFeeSiteParametersGrain>("FEE-SITE-PARAMS");
            await grain.UpdateAsync(
                req.SiteName,
                req.IsFeeBasisEnabled,
                req.FiscalYear,
                req.AnnualBudget,
                req.MaxAuthorizationDays,
                req.RequiresPreAuthorization,
                req.AutoApprovalLimit,
                req.DefaultPaymentMethod,
                req.UpdatedByUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fee site parameters");
            return StatusCode(500, "Error updating site parameters.");
        }
    }

    // ─── Fee Basis Batch Payments (File #162.7) ────────────────────────────────

    [HttpPost("batch-payments")]
    public async Task<IActionResult> CreateBatchPayment([FromBody] CreateFeeBatchPaymentRequest req)
    {
        try
        {
            string batchId = $"FEE-BATCH:{Guid.NewGuid()}";
            IFeeBatchPaymentGrain batch = _grainFactory.GetGrain<IFeeBatchPaymentGrain>(batchId);
            await batch.CreateAsync(req.VendorId, req.VendorName, req.BatchDate, req.PaymentMethod, req.CheckNumber, req.Notes);

            IFeeBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IFeeBatchPaymentIndexGrain>("FEE-BATCH-IDX");
            await idx.AddOrUpdateAsync(new FeeBatchPaymentIndexEntry
            {
                BatchId        = batchId,
                VendorName     = req.VendorName,
                BatchDate      = req.BatchDate,
                PaymentMethod  = req.PaymentMethod,
                TotalAmount    = 0m,
                InvoiceCount   = 0,
                IsPosted       = false,
                PostedDate     = null,
            });

            _logger.LogInformation("Fee basis batch payment {BatchId} created", batchId);
            return Created($"api/feebasis/batch-payments/{Uri.EscapeDataString(batchId)}", new { batchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fee basis batch payment");
            return StatusCode(500, "Error creating batch payment.");
        }
    }

    [HttpPost("batch-payments/{batchId}/invoices")]
    public async Task<IActionResult> AddInvoiceToBatch(string batchId, [FromBody] AddInvoiceToBatchRequest req)
    {
        try
        {
            IFeeBatchPaymentGrain batch = _grainFactory.GetGrain<IFeeBatchPaymentGrain>(batchId);
            await batch.AddInvoiceAsync(req.InvoiceId, req.AuthorizationId, req.PatientId, req.PatientName, req.VendorId, req.VendorName, req.PaidAmount);

            FeeBatchPaymentState state = await batch.GetAsync();
            IFeeBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IFeeBatchPaymentIndexGrain>("FEE-BATCH-IDX");
            await idx.AddOrUpdateAsync(new FeeBatchPaymentIndexEntry
            {
                BatchId       = batchId,
                VendorName    = state.VendorName,
                BatchDate     = state.BatchDate,
                PaymentMethod = state.PaymentMethod,
                TotalAmount   = state.TotalAmount,
                InvoiceCount  = state.InvoiceEntries.Count,
                IsPosted      = state.IsPosted,
                PostedDate    = state.PostedDate,
            });

            return Ok(new { batchId, invoiceId = req.InvoiceId, totalAmount = state.TotalAmount, invoiceCount = state.InvoiceEntries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding invoice {InvoiceId} to batch {BatchId}", req.InvoiceId, batchId);
            return StatusCode(500, "Error adding invoice to batch.");
        }
    }

    [HttpPost("batch-payments/{batchId}/post")]
    public async Task<IActionResult> PostBatchPayment(string batchId, [FromBody] PostFeeBatchPaymentRequest req)
    {
        try
        {
            IFeeBatchPaymentGrain batch = _grainFactory.GetGrain<IFeeBatchPaymentGrain>(batchId);
            await batch.PostAsync(req.PostedByUserId, req.PostedByUserName);

            FeeBatchPaymentState state = await batch.GetAsync();
            IFeeBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IFeeBatchPaymentIndexGrain>("FEE-BATCH-IDX");
            await idx.AddOrUpdateAsync(new FeeBatchPaymentIndexEntry
            {
                BatchId       = batchId,
                VendorName    = state.VendorName,
                BatchDate     = state.BatchDate,
                PaymentMethod = state.PaymentMethod,
                TotalAmount   = state.TotalAmount,
                InvoiceCount  = state.InvoiceEntries.Count,
                IsPosted      = state.IsPosted,
                PostedDate    = state.PostedDate,
            });

            _logger.LogInformation("Fee basis batch {BatchId} posted by {UserId} — {Count} invoices, total {Total:C}", batchId, req.PostedByUserId, state.InvoiceEntries.Count, state.TotalAmount);
            return Ok(new { batchId, postedDate = state.PostedDate, invoiceCount = state.InvoiceEntries.Count, totalAmount = state.TotalAmount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting fee basis batch {BatchId}", batchId);
            return StatusCode(500, "Error posting batch payment.");
        }
    }

    [HttpGet("batch-payments")]
    public async Task<IActionResult> GetAllBatchPayments()
    {
        try
        {
            IFeeBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IFeeBatchPaymentIndexGrain>("FEE-BATCH-IDX");
            return Ok(await idx.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fee basis batch payments");
            return StatusCode(500, "Error retrieving batch payments.");
        }
    }

    [HttpGet("batch-payments/unposted")]
    public async Task<IActionResult> GetUnpostedBatchPayments()
    {
        try
        {
            IFeeBatchPaymentIndexGrain idx = _grainFactory.GetGrain<IFeeBatchPaymentIndexGrain>("FEE-BATCH-IDX");
            return Ok(await idx.GetUnpostedAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unposted fee basis batch payments");
            return StatusCode(500, "Error retrieving unposted batches.");
        }
    }

    [HttpGet("batch-payments/{batchId}")]
    public async Task<IActionResult> GetBatchPayment(string batchId)
    {
        try
        {
            IFeeBatchPaymentGrain batch = _grainFactory.GetGrain<IFeeBatchPaymentGrain>(batchId);
            return Ok(await batch.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fee basis batch {BatchId}", batchId);
            return StatusCode(500, "Error retrieving batch payment.");
        }
    }

    [HttpGet("batch-payments/{batchId}/invoices")]
    public async Task<IActionResult> GetBatchInvoices(string batchId)
    {
        try
        {
            IFeeBatchPaymentGrain batch = _grainFactory.GetGrain<IFeeBatchPaymentGrain>(batchId);
            FeeBatchPaymentState state = await batch.GetAsync();
            return Ok(state.InvoiceEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoices for fee basis batch {BatchId}", batchId);
            return StatusCode(500, "Error retrieving batch invoices.");
        }
    }

    // ─── Income Thresholds (File #408.15) ─────────────────────────────────────

    [HttpGet("income-thresholds/{year:int}")]
    public async Task<IActionResult> GetIncomeThresholds(int year)
    {
        try
        {
            IIncomeThresholdGrain grain = _grainFactory.GetGrain<IIncomeThresholdGrain>("INCOME-THRESHOLD-IDX");
            return Ok(await grain.GetByYearAsync(year));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting income thresholds for year {Year}", year);
            return StatusCode(500, "Error retrieving income thresholds.");
        }
    }

    [HttpPost("income-thresholds/seed/{year:int}")]
    public async Task<IActionResult> SeedIncomeThresholds(int year)
    {
        try
        {
            IIncomeThresholdGrain grain = _grainFactory.GetGrain<IIncomeThresholdGrain>("INCOME-THRESHOLD-IDX");
            await grain.SeedDefaultsAsync(year);
            return Ok(new { message = $"Income thresholds seeded for FY {year}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding income thresholds for year {Year}", year);
            return StatusCode(500, "Error seeding income thresholds.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record CreateAuthorizationRequest(
    string VendorId,
    string VendorName,
    string ServiceType,
    DateTime AuthorizationDate,
    DateTime EffectiveDate,
    DateTime? ExpirationDate,
    decimal AuthorizedAmount,
    string AuthorizedByUserId,
    string AuthorizedByUserName,
    string ServiceDescription,
    int? MaxVisits,
    string? DiagnosisCode,
    string? AuthorizationNumber,
    string? Notes);

public record SuspendCancelRequest(string Reason, string UserId);

public record SubmitInvoiceRequest(
    string PatientId,
    string VendorId,
    string VendorName,
    string InvoiceNumber,
    DateTime ServiceDate,
    string ServiceType,
    decimal BilledAmount,
    string? DiagnosisCode,
    List<string>? ProcedureCodes,
    DateTime? ServiceDateEnd,
    string? Notes);

public record ApproveInvoiceRequest(
    decimal ApprovedAmount,
    string ReviewedByUserId,
    string ReviewerName);

public record RejectInvoiceRequest(
    string Reason,
    string ReviewedByUserId,
    string ReviewerName);

public record PayInvoiceRequest(
    decimal PaidAmount,
    string PaymentMethod,
    string? CheckNumber,
    DateTime PaymentDate);

public record CreateVendorRequest(
    string VendorName,
    string VendorType,
    string? SpecialtyCode,
    string? SpecialtyName,
    string? NPI,
    string? TaxId,
    string? Address,
    string? Phone,
    string? Fax,
    string? ContractNumber,
    DateTime? ContractStartDate,
    DateTime? ContractEndDate,
    string? Notes);

public record UpdateSiteParametersRequest(
    string SiteName,
    bool IsFeeBasisEnabled,
    int FiscalYear,
    decimal? AnnualBudget,
    int MaxAuthorizationDays,
    bool RequiresPreAuthorization,
    decimal? AutoApprovalLimit,
    string DefaultPaymentMethod,
    string UpdatedByUserId);

public record CreateFeeBatchPaymentRequest(
    string? VendorId,
    string? VendorName,
    DateTime BatchDate,
    string PaymentMethod,
    string? CheckNumber,
    string? Notes);

public record AddInvoiceToBatchRequest(
    string InvoiceId,
    string AuthorizationId,
    string PatientId,
    string PatientName,
    string VendorId,
    string VendorName,
    decimal PaidAmount);

public record PostFeeBatchPaymentRequest(
    string PostedByUserId,
    string PostedByUserName);
