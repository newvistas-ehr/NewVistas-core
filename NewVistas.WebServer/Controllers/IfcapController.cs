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
[Route("api/ifcap")]
public class IfcapController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IfcapController> _logger;

    public IfcapController(IGrainFactory grainFactory, ILogger<IfcapController> logger)
    {
        _grainFactory = grainFactory;
        _logger       = logger;
    }

    // ─── Control Points ────────────────────────────────────────────────────────

    [HttpGet("control-points")]
    public async Task<IActionResult> GetControlPoints()
    {
        try
        {
            IControlPointIndexGrain idx = _grainFactory.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            List<ControlPointIndexEntry> entries = await idx.GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching control points");
            return StatusCode(500, "Error fetching control points");
        }
    }

    [HttpGet("control-points/fiscal-year/{year:int}")]
    public async Task<IActionResult> GetControlPointsByFiscalYear(int year)
    {
        try
        {
            IControlPointIndexGrain idx = _grainFactory.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            List<ControlPointIndexEntry> entries = await idx.GetByFiscalYearAsync(year);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching control points for fiscal year {Year}", year);
            return StatusCode(500, "Error fetching control points");
        }
    }

    [HttpGet("control-points/{id}")]
    public async Task<IActionResult> GetControlPoint(string id)
    {
        try
        {
            IControlPointGrain grain = _grainFactory.GetGrain<IControlPointGrain>($"IFCAP-CP:{id}");
            ControlPointState state = await grain.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching control point {Id}", id);
            return StatusCode(500, "Error fetching control point");
        }
    }

    [HttpPost("control-points")]
    public async Task<IActionResult> CreateControlPoint([FromBody] CreateControlPointRequest req)
    {
        try
        {
            string cpId = Guid.NewGuid().ToString();
            IControlPointGrain grain = _grainFactory.GetGrain<IControlPointGrain>($"IFCAP-CP:{cpId}");
            await grain.CreateAsync(req.Name, req.FacilityId, req.ServiceId, req.FiscalYear,
                req.BudgetCode, req.AllocatedAmount, req.OfficerId, req.OfficerName);

            IControlPointIndexGrain idx = _grainFactory.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            await idx.AddOrUpdateAsync(new ControlPointIndexEntry(
                cpId, req.Name, req.FacilityId, req.FiscalYear, req.AllocatedAmount, ControlPointStatus.Active));

            return Created(cpId, cpId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating control point");
            return StatusCode(500, "Error creating control point");
        }
    }

    [HttpPost("control-points/{id}/allocate")]
    public async Task<IActionResult> AllocateFunds(string id, [FromBody] AllocateFundsRequest req)
    {
        try
        {
            IControlPointGrain grain = _grainFactory.GetGrain<IControlPointGrain>($"IFCAP-CP:{id}");
            await grain.AllocateFundsAsync(req.Amount, req.AuthorizedByUserId);
            ControlPointState updated = await grain.GetAsync();

            IControlPointIndexGrain idx = _grainFactory.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            await idx.AddOrUpdateAsync(new ControlPointIndexEntry(
                id, updated.Name, updated.FacilityId, updated.FiscalYear, updated.RemainingBalance, updated.Status));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating funds for control point {Id}", id);
            return StatusCode(500, "Error allocating funds");
        }
    }

    // ─── Purchase Requests (2237) ──────────────────────────────────────────────

    [HttpGet("control-points/{cpId}/requests")]
    public async Task<IActionResult> GetPurchaseRequests(string cpId)
    {
        try
        {
            IPurchaseRequestIndexGrain idx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");
            List<PurchaseRequestIndexEntry> entries = await idx.GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching purchase requests for CP {CpId}", cpId);
            return StatusCode(500, "Error fetching purchase requests");
        }
    }

    [HttpGet("control-points/{cpId}/requests/pending")]
    public async Task<IActionResult> GetPendingPurchaseRequests(string cpId)
    {
        try
        {
            IPurchaseRequestIndexGrain idx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");
            List<PurchaseRequestIndexEntry> entries = await idx.GetPendingAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending requests for CP {CpId}", cpId);
            return StatusCode(500, "Error fetching pending requests");
        }
    }

    [HttpPost("control-points/{cpId}/requests")]
    public async Task<IActionResult> CreatePurchaseRequest(string cpId, [FromBody] CreatePurchaseRequestRequest req)
    {
        try
        {
            string requestId = Guid.NewGuid().ToString();
            if (!Enum.TryParse<PurchaseRequestPriority>(req.Priority, true, out PurchaseRequestPriority priority))
                priority = PurchaseRequestPriority.Routine;

            List<PurchaseRequestLineItem> lineItems = req.LineItems
                .Select(l => new PurchaseRequestLineItem(
                    l.LineNumber, l.ItemDescription, l.Quantity, l.UnitOfIssue,
                    l.UnitCost, l.Quantity * l.UnitCost, l.NSN, l.FSSNumber, l.Notes))
                .ToList();

            IPurchaseRequestGrain grain = _grainFactory.GetGrain<IPurchaseRequestGrain>($"IFCAP-PR:{requestId}");
            await grain.CreateAsync(cpId, req.RequestorId, req.RequestorName, req.FiscalYear,
                priority, req.NeedByDate, req.Description, req.Justification,
                req.EstimatedCost, lineItems, req.VendorId, req.VendorName, req.Notes);

            IPurchaseRequestIndexGrain idx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");
            await idx.AddOrUpdateAsync(new PurchaseRequestIndexEntry(
                requestId, cpId, req.RequestorName, req.EstimatedCost,
                PurchaseRequestStatus.Draft, priority, DateTime.UtcNow));

            return Created(requestId, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase request for CP {CpId}", cpId);
            return StatusCode(500, "Error creating purchase request");
        }
    }

    [HttpPost("control-points/{cpId}/requests/{rid}/submit")]
    public async Task<IActionResult> SubmitPurchaseRequest(string cpId, string rid, [FromBody] SimpleUserRequest req)
    {
        try
        {
            IPurchaseRequestGrain grain = _grainFactory.GetGrain<IPurchaseRequestGrain>($"IFCAP-PR:{rid}");
            await grain.SubmitAsync(req.UserId);
            PurchaseRequestState updated = await grain.GetAsync();

            IPurchaseRequestIndexGrain idx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");
            await idx.AddOrUpdateAsync(new PurchaseRequestIndexEntry(
                rid, cpId, updated.RequestorName, updated.EstimatedCost,
                updated.Status, updated.Priority, updated.RequestDate));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting request {Rid}", rid);
            return StatusCode(500, "Error submitting purchase request");
        }
    }

    [HttpPost("control-points/{cpId}/requests/{rid}/approve")]
    public async Task<IActionResult> ApprovePurchaseRequest(string cpId, string rid, [FromBody] ApproveRequestRequest req)
    {
        try
        {
            IPurchaseRequestGrain grain = _grainFactory.GetGrain<IPurchaseRequestGrain>($"IFCAP-PR:{rid}");
            await grain.ApproveAsync(req.ApprovedByUserId, req.ApprovedByUserName);
            PurchaseRequestState pr = await grain.GetAsync();

            // Obligate funds in the control point
            IControlPointGrain cp = _grainFactory.GetGrain<IControlPointGrain>($"IFCAP-CP:{cpId}");
            await cp.ObligateFundsAsync(pr.EstimatedCost, rid);
            ControlPointState cpState = await cp.GetAsync();

            // Update indexes
            IPurchaseRequestIndexGrain prIdx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");
            await prIdx.AddOrUpdateAsync(new PurchaseRequestIndexEntry(
                rid, cpId, pr.RequestorName, pr.EstimatedCost, pr.Status, pr.Priority, pr.RequestDate));

            IControlPointIndexGrain cpIdx = _grainFactory.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            await cpIdx.AddOrUpdateAsync(new ControlPointIndexEntry(
                cpId, cpState.Name, cpState.FacilityId, cpState.FiscalYear, cpState.RemainingBalance, cpState.Status));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving request {Rid}", rid);
            return StatusCode(500, "Error approving purchase request");
        }
    }

    [HttpPost("control-points/{cpId}/requests/{rid}/reject")]
    public async Task<IActionResult> RejectPurchaseRequest(string cpId, string rid, [FromBody] RejectCancelRequest req)
    {
        try
        {
            IPurchaseRequestGrain grain = _grainFactory.GetGrain<IPurchaseRequestGrain>($"IFCAP-PR:{rid}");
            await grain.RejectAsync(req.UserId, req.Reason);
            PurchaseRequestState updated = await grain.GetAsync();

            IPurchaseRequestIndexGrain idx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");
            await idx.AddOrUpdateAsync(new PurchaseRequestIndexEntry(
                rid, cpId, updated.RequestorName, updated.EstimatedCost,
                updated.Status, updated.Priority, updated.RequestDate));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting request {Rid}", rid);
            return StatusCode(500, "Error rejecting purchase request");
        }
    }

    [HttpPost("control-points/{cpId}/requests/{rid}/cancel")]
    public async Task<IActionResult> CancelPurchaseRequest(string cpId, string rid, [FromBody] RejectCancelRequest req)
    {
        try
        {
            IPurchaseRequestGrain grain = _grainFactory.GetGrain<IPurchaseRequestGrain>($"IFCAP-PR:{rid}");
            await grain.CancelAsync(req.Reason);
            PurchaseRequestState updated = await grain.GetAsync();

            IPurchaseRequestIndexGrain idx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{cpId}");
            await idx.AddOrUpdateAsync(new PurchaseRequestIndexEntry(
                rid, cpId, updated.RequestorName, updated.EstimatedCost,
                updated.Status, updated.Priority, updated.RequestDate));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling request {Rid}", rid);
            return StatusCode(500, "Error cancelling purchase request");
        }
    }

    // ─── Purchase Orders ──────────────────────────────────────────────────────

    [HttpGet("purchase-orders")]
    public async Task<IActionResult> GetPurchaseOrders()
    {
        try
        {
            IPurchaseOrderIndexGrain idx = _grainFactory.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");
            List<PurchaseOrderIndexEntry> entries = await idx.GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching purchase orders");
            return StatusCode(500, "Error fetching purchase orders");
        }
    }

    [HttpGet("purchase-orders/open")]
    public async Task<IActionResult> GetOpenPurchaseOrders()
    {
        try
        {
            IPurchaseOrderIndexGrain idx = _grainFactory.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");
            List<PurchaseOrderIndexEntry> entries = await idx.GetOpenAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching open purchase orders");
            return StatusCode(500, "Error fetching open purchase orders");
        }
    }

    [HttpGet("purchase-orders/{id}")]
    public async Task<IActionResult> GetPurchaseOrder(string id)
    {
        try
        {
            IPurchaseOrderGrain grain = _grainFactory.GetGrain<IPurchaseOrderGrain>($"IFCAP-PO:{id}");
            PurchaseOrderState state = await grain.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching purchase order {Id}", id);
            return StatusCode(500, "Error fetching purchase order");
        }
    }

    [HttpPost("purchase-orders")]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest req)
    {
        try
        {
            string poId = Guid.NewGuid().ToString();
            List<PoLineItem> lineItems = req.LineItems
                .Select(l => new PoLineItem(
                    l.LineNumber, l.ItemDescription, l.Quantity, 0m,
                    l.UnitOfIssue, l.UnitCost, l.Quantity * l.UnitCost, l.NSN, l.FSSNumber))
                .ToList();

            IPurchaseOrderGrain poGrain = _grainFactory.GetGrain<IPurchaseOrderGrain>($"IFCAP-PO:{poId}");
            await poGrain.CreateAsync(req.RequestId, req.ControlPointId, req.VendorId, req.VendorName,
                req.VendorAddress, req.RequiredDeliveryDate, lineItems, req.IssuedByUserId, req.IssuedByUserName);
            PurchaseOrderState po = await poGrain.GetAsync();

            // Mark the purchase request as funded
            IPurchaseRequestGrain prGrain = _grainFactory.GetGrain<IPurchaseRequestGrain>($"IFCAP-PR:{req.RequestId}");
            await prGrain.FundAsync(poId);
            PurchaseRequestState pr = await prGrain.GetAsync();

            // Move funds from obligated to expended in control point
            IControlPointGrain cpGrain = _grainFactory.GetGrain<IControlPointGrain>($"IFCAP-CP:{req.ControlPointId}");
            await cpGrain.ExpenditureAsync(po.TotalAmount, poId);
            ControlPointState cpState = await cpGrain.GetAsync();

            // Update PO index
            IPurchaseOrderIndexGrain poIdx = _grainFactory.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");
            await poIdx.AddOrUpdateAsync(new PurchaseOrderIndexEntry(
                poId, req.ControlPointId, req.VendorName, po.TotalAmount, 0m,
                PurchaseOrderStatus.Open, DateTime.UtcNow, req.RequestId));

            // Update PR index
            IPurchaseRequestIndexGrain prIdx = _grainFactory.GetGrain<IPurchaseRequestIndexGrain>($"IFCAP-PR-IDX:{req.ControlPointId}");
            await prIdx.AddOrUpdateAsync(new PurchaseRequestIndexEntry(
                req.RequestId, req.ControlPointId, pr.RequestorName, pr.EstimatedCost,
                pr.Status, pr.Priority, pr.RequestDate));

            // Update CP index
            IControlPointIndexGrain cpIdx = _grainFactory.GetGrain<IControlPointIndexGrain>("IFCAP-CP-IDX");
            await cpIdx.AddOrUpdateAsync(new ControlPointIndexEntry(
                req.ControlPointId, cpState.Name, cpState.FacilityId, cpState.FiscalYear,
                cpState.RemainingBalance, cpState.Status));

            return Created(poId, poId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order");
            return StatusCode(500, "Error creating purchase order");
        }
    }

    [HttpPost("purchase-orders/{id}/cancel")]
    public async Task<IActionResult> CancelPurchaseOrder(string id, [FromBody] RejectCancelRequest req)
    {
        try
        {
            IPurchaseOrderGrain grain = _grainFactory.GetGrain<IPurchaseOrderGrain>($"IFCAP-PO:{id}");
            await grain.CancelAsync(req.Reason, req.UserId);
            PurchaseOrderState updated = await grain.GetAsync();

            IPurchaseOrderIndexGrain idx = _grainFactory.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");
            await idx.AddOrUpdateAsync(new PurchaseOrderIndexEntry(
                id, updated.ControlPointId, updated.VendorName, updated.TotalAmount,
                updated.AmountReceived, updated.Status, updated.IssuedDate, updated.RequestId));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling purchase order {Id}", id);
            return StatusCode(500, "Error cancelling purchase order");
        }
    }

    [HttpPost("purchase-orders/{id}/close")]
    public async Task<IActionResult> ClosePurchaseOrder(string id, [FromBody] ClosePoRequest req)
    {
        try
        {
            IPurchaseOrderGrain grain = _grainFactory.GetGrain<IPurchaseOrderGrain>($"IFCAP-PO:{id}");
            await grain.CloseAsync(req.UserId);
            PurchaseOrderState updated = await grain.GetAsync();

            IPurchaseOrderIndexGrain idx = _grainFactory.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");
            await idx.AddOrUpdateAsync(new PurchaseOrderIndexEntry(
                id, updated.ControlPointId, updated.VendorName, updated.TotalAmount,
                updated.AmountReceived, updated.Status, updated.IssuedDate, updated.RequestId));

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing purchase order {Id}", id);
            return StatusCode(500, "Error closing purchase order");
        }
    }

    // ─── Receiving Reports ────────────────────────────────────────────────────

    [HttpGet("purchase-orders/{poId}/receiving-reports")]
    public async Task<IActionResult> GetReceivingReports(string poId)
    {
        try
        {
            IReceivingReportIndexGrain idx = _grainFactory.GetGrain<IReceivingReportIndexGrain>($"IFCAP-RR-IDX:{poId}");
            List<ReceivingReportIndexEntry> entries = await idx.GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching receiving reports for PO {PoId}", poId);
            return StatusCode(500, "Error fetching receiving reports");
        }
    }

    [HttpPost("purchase-orders/{poId}/receiving-reports")]
    public async Task<IActionResult> CreateReceivingReport(string poId, [FromBody] CreateReceivingReportRequest req)
    {
        try
        {
            // Get PO to build RR line items
            IPurchaseOrderGrain poGrain = _grainFactory.GetGrain<IPurchaseOrderGrain>($"IFCAP-PO:{poId}");
            PurchaseOrderState po = await poGrain.GetAsync();

            string rrId = Guid.NewGuid().ToString();
            List<RrLineItem> lineItems = req.LineItems.Select(l =>
            {
                PoLineItem? poLine = po.LineItems.FirstOrDefault(pl => pl.LineNumber == l.LineNumber);
                decimal unitCost = poLine?.UnitCost ?? 0m;
                return new RrLineItem(
                    l.LineNumber,
                    poLine?.ItemDescription ?? string.Empty,
                    poLine?.Quantity ?? 0m,
                    l.QuantityReceived,
                    l.QuantityAccepted,
                    unitCost,
                    l.QuantityAccepted * unitCost,
                    l.Condition);
            }).ToList();

            // Create the receiving report grain
            IReceivingReportGrain rrGrain = _grainFactory.GetGrain<IReceivingReportGrain>($"IFCAP-RR:{rrId}");
            await rrGrain.CreateAsync(poId, po.ControlPointId, req.ReceivedByUserId, req.ReceivedByUserName, lineItems, req.Notes);
            await rrGrain.AcceptAsync(req.ReceivedByUserId);
            ReceivingReportState rr = await rrGrain.GetAsync();

            // Record receipt on the PO
            List<(int, decimal)> lineReceipts = req.LineItems
                .Select(l => (l.LineNumber, l.QuantityAccepted))
                .ToList();
            await poGrain.RecordReceiptAsync(rrId, lineReceipts);
            PurchaseOrderState updatedPo = await poGrain.GetAsync();

            // Update RR index
            IReceivingReportIndexGrain rrIdx = _grainFactory.GetGrain<IReceivingReportIndexGrain>($"IFCAP-RR-IDX:{poId}");
            decimal totalAccepted = lineItems.Sum(l => l.QuantityAccepted * l.UnitCost);
            await rrIdx.AddOrUpdateAsync(new ReceivingReportIndexEntry(
                rrId, poId, rr.ReceivedDate, rr.Status, req.ReceivedByUserName, totalAccepted));

            // Update PO index
            IPurchaseOrderIndexGrain poIdx = _grainFactory.GetGrain<IPurchaseOrderIndexGrain>("IFCAP-PO-IDX");
            await poIdx.AddOrUpdateAsync(new PurchaseOrderIndexEntry(
                poId, updatedPo.ControlPointId, updatedPo.VendorName, updatedPo.TotalAmount,
                updatedPo.AmountReceived, updatedPo.Status, updatedPo.IssuedDate, updatedPo.RequestId));

            return Created(rrId, rrId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating receiving report for PO {PoId}", poId);
            return StatusCode(500, "Error creating receiving report");
        }
    }

    // ─── Vendors ──────────────────────────────────────────────────────────────

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors([FromQuery] string? search, [FromQuery] bool activeOnly = false)
    {
        try
        {
            IIfcapVendorIndexGrain idx = _grainFactory.GetGrain<IIfcapVendorIndexGrain>("IFCAP-VENDOR-IDX");
            List<IfcapVendorIndexEntry> entries;
            if (!string.IsNullOrWhiteSpace(search))
                entries = await idx.SearchAsync(search);
            else if (activeOnly)
                entries = await idx.GetActiveAsync();
            else
                entries = await idx.GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendors");
            return StatusCode(500, "Error fetching vendors");
        }
    }

    [HttpGet("vendors/{id}")]
    public async Task<IActionResult> GetVendor(string id)
    {
        try
        {
            IIfcapVendorGrain grain = _grainFactory.GetGrain<IIfcapVendorGrain>($"IFCAP-VENDOR:{id}");
            IfcapVendorState state = await grain.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vendor {Id}", id);
            return StatusCode(500, "Error fetching vendor");
        }
    }

    [HttpPost("vendors")]
    public async Task<IActionResult> CreateVendor([FromBody] CreateVendorRequest req)
    {
        try
        {
            string vendorId = Guid.NewGuid().ToString();
            IIfcapVendorGrain grain = _grainFactory.GetGrain<IIfcapVendorGrain>($"IFCAP-VENDOR:{vendorId}");
            await grain.CreateAsync(req.Name, req.VendorNumber, req.Address, req.City, req.State,
                req.ZipCode, req.Phone, req.Fax, req.Email, req.IsSmallBusiness,
                req.IsWomanOwned, req.IsVeteranOwned, req.DUNS, req.ContactName);

            IIfcapVendorIndexGrain idx = _grainFactory.GetGrain<IIfcapVendorIndexGrain>("IFCAP-VENDOR-IDX");
            await idx.AddOrUpdateAsync(new IfcapVendorIndexEntry(
                vendorId, req.Name, req.VendorNumber, true, req.IsSmallBusiness, req.IsVeteranOwned));

            return Created(vendorId, vendorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vendor");
            return StatusCode(500, "Error creating vendor");
        }
    }

    // ─── Site Parameters ──────────────────────────────────────────────────────

    [HttpGet("site-parameters")]
    public async Task<IActionResult> GetSiteParameters()
    {
        try
        {
            IIfcapSiteParametersGrain grain = _grainFactory.GetGrain<IIfcapSiteParametersGrain>("IFCAP-SITE-PARAMS");
            IfcapSiteParametersState state = await grain.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching IFCAP site parameters");
            return StatusCode(500, "Error fetching site parameters");
        }
    }

    [HttpPut("site-parameters")]
    public async Task<IActionResult> UpdateSiteParameters([FromBody] UpdateIfcapSiteParametersRequest req)
    {
        try
        {
            IIfcapSiteParametersGrain grain = _grainFactory.GetGrain<IIfcapSiteParametersGrain>("IFCAP-SITE-PARAMS");
            await grain.UpdateAsync(req.SiteName, req.FacilityNumber, req.FiscalYear,
                req.DefaultDeliveryDays, req.IsAutoApprovalEnabled, req.AutoApprovalThreshold,
                req.PONumberPrefix, req.UpdatedByUserId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IFCAP site parameters");
            return StatusCode(500, "Error updating site parameters");
        }
    }

    // ─── Request DTOs ─────────────────────────────────────────────────────────

    public record CreateControlPointRequest(
        string Name, string FacilityId, string ServiceId,
        int FiscalYear, string BudgetCode, decimal AllocatedAmount,
        string OfficerId, string OfficerName);

    public record AllocateFundsRequest(decimal Amount, string AuthorizedByUserId);

    public record CreatePurchaseRequestRequest(
        string RequestorId, string RequestorName, int FiscalYear,
        string Priority, DateTime? NeedByDate,
        string Description, string Justification, decimal EstimatedCost,
        List<PrLineItemRequest> LineItems,
        string? VendorId, string? VendorName, string? Notes);

    public record PrLineItemRequest(
        int LineNumber, string ItemDescription, decimal Quantity,
        string UnitOfIssue, decimal UnitCost,
        string? NSN, string? FSSNumber, string? Notes);

    public record ApproveRequestRequest(string ApprovedByUserId, string ApprovedByUserName);

    public record RejectCancelRequest(string UserId, string Reason);

    public record SimpleUserRequest(string UserId);

    public record CreatePurchaseOrderRequest(
        string RequestId, string ControlPointId,
        string VendorId, string VendorName, string? VendorAddress,
        DateTime RequiredDeliveryDate,
        List<PrLineItemRequest> LineItems,
        string IssuedByUserId, string IssuedByUserName);

    public record CreateReceivingReportRequest(
        string ReceivedByUserId, string ReceivedByUserName,
        List<RrLineItemRequest> LineItems, string? Notes);

    public record RrLineItemRequest(
        int LineNumber, decimal QuantityReceived, decimal QuantityAccepted, string? Condition);

    public record ClosePoRequest(string UserId);

    public record CreateVendorRequest(
        string Name, string VendorNumber,
        string Address, string City, string State, string ZipCode,
        string? Phone, string? Fax, string? Email,
        bool IsSmallBusiness, bool IsWomanOwned, bool IsVeteranOwned,
        string? DUNS, string? ContactName);

    public record UpdateIfcapSiteParametersRequest(
        string SiteName, string FacilityNumber,
        int FiscalYear, int DefaultDeliveryDays,
        bool IsAutoApprovalEnabled, decimal AutoApprovalThreshold,
        string PONumberPrefix, string UpdatedByUserId);
}
