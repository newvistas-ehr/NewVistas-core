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
[Route("api/ib-site-config")]
public class IBSiteConfigController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IBSiteConfigController> _logger;

    public IBSiteConfigController(IGrainFactory grainFactory,
        ILogger<IBSiteConfigController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private IIBSiteParametersGrain GetSiteParams() =>
        _grainFactory.GetGrain<IIBSiteParametersGrain>("IB-SITE-PARAMS");

    // ─── GET endpoints ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetSiteParameters()
    {
        try
        {
            IBSiteParametersState state = await GetSiteParams().GetAsync();
            return Ok(new IBSiteParamsDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IB site parameters");
            return StatusCode(500, "An error occurred retrieving site parameters.");
        }
    }

    // ─── POST endpoints ────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> UpdateSiteParameters([FromBody] UpdateIBSiteParamsRequest req)
    {
        try
        {
            await GetSiteParams().UpdateAsync(
                req.SiteName,
                req.BillingMailingAddress,
                req.BillingEmail,
                req.OutpatientCopayThreshold,
                req.InpatientPerDiemRate,
                req.NhcuPerDiemRate,
                req.PharmacyCopayTier1Amount,
                req.PharmacyCopayTier2Amount,
                req.PharmacyCopayTier3Amount,
                req.MedicareDeductibleAmount,
                req.CopayCapAmount,
                req.IsTricareSiteEnabled,
                req.UpdatedByUserId);
            return Ok(new { message = "Site parameters updated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IB site parameters");
            return StatusCode(500, "An error occurred updating site parameters.");
        }
    }

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo()
    {
        try
        {
            await GetSiteParams().UpdateAsync(
                siteName: "James J. Peters VA Medical Center",
                billingMailingAddress: "130 West Kingsbridge Road, Bronx NY 10468",
                billingEmail: "billing@va.gov",
                outpatientCopayThreshold: 50.00m,
                inpatientPerDiemRate: 1880.00m,
                nhcuPerDiemRate: 97.00m,
                pharmacyCopayTier1Amount: 5.00m,
                pharmacyCopayTier2Amount: 8.00m,
                pharmacyCopayTier3Amount: 11.00m,
                medicareDeductibleAmount: 1632.00m,
                copayCapAmount: 700.00m,
                isTricareSiteEnabled: true,
                updatedByUserId: "ADMIN-DEMO");

            return Ok(new { message = "Demo IB site parameters loaded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo IB site parameters");
            return StatusCode(500, "An error occurred loading demo data.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record IBSiteParamsDto(
    string SiteId,
    string SiteName,
    string BillingMailingAddress,
    string? BillingEmail,
    decimal? OutpatientCopayThreshold,
    decimal? InpatientPerDiemRate,
    decimal? NhcuPerDiemRate,
    decimal PharmacyCopayTier1Amount,
    decimal PharmacyCopayTier2Amount,
    decimal PharmacyCopayTier3Amount,
    decimal? MedicareDeductibleAmount,
    decimal? CopayCapAmount,
    List<string> BillingTypes,
    bool IsTricareSiteEnabled,
    DateTime? LastUpdatedDate,
    string? LastUpdatedByUserId)
{
    public IBSiteParamsDto(IBSiteParametersState s)
        : this(s.SiteId, s.SiteName, s.BillingMailingAddress, s.BillingEmail,
               s.OutpatientCopayThreshold, s.InpatientPerDiemRate, s.NhcuPerDiemRate,
               s.PharmacyCopayTier1Amount, s.PharmacyCopayTier2Amount, s.PharmacyCopayTier3Amount,
               s.MedicareDeductibleAmount, s.CopayCapAmount, s.BillingTypes,
               s.IsTricareSiteEnabled, s.LastUpdatedDate, s.LastUpdatedByUserId) { }
}

public record UpdateIBSiteParamsRequest(
    string SiteName,
    string BillingMailingAddress,
    string? BillingEmail,
    decimal? OutpatientCopayThreshold,
    decimal? InpatientPerDiemRate,
    decimal? NhcuPerDiemRate,
    decimal PharmacyCopayTier1Amount,
    decimal PharmacyCopayTier2Amount,
    decimal PharmacyCopayTier3Amount,
    decimal? MedicareDeductibleAmount,
    decimal? CopayCapAmount,
    bool IsTricareSiteEnabled,
    string UpdatedByUserId);
