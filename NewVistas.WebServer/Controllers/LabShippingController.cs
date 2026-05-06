// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/lab-shipping")]
public class LabShippingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<LabShippingController> _logger;

    public LabShippingController(IGrainFactory grainFactory,
        ILogger<LabShippingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private IShippingManifestGrain GetManifestGrain(string manifestId) =>
        _grainFactory.GetGrain<IShippingManifestGrain>($"LA-SHIP:{manifestId}");

    private IShippingConfigGrain GetConfigGrain(string configId) =>
        _grainFactory.GetGrain<IShippingConfigGrain>($"LA-SHIPCFG:{configId}");

    // ─── Manifest GET endpoints ────────────────────────────────────────────────

    [HttpGet("manifests/{manifestId}")]
    public async Task<IActionResult> GetManifest(string manifestId)
    {
        try
        {
            ShippingManifestState state = await GetManifestGrain(manifestId).GetManifestAsync();
            return Ok(new ShipManifestDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manifest {ManifestId}", manifestId);
            return StatusCode(500, "An error occurred retrieving the manifest.");
        }
    }

    // ─── Manifest POST endpoints ───────────────────────────────────────────────

    [HttpPost("manifests")]
    public async Task<IActionResult> CreateManifest([FromBody] CreateManifestRequest req)
    {
        try
        {
            string manifestId = Guid.NewGuid().ToString();
            IShippingManifestGrain grain = GetManifestGrain(manifestId);
            await grain.CreateManifestAsync(
                req.ShippingConfigId, req.DestinationLab, req.ShippingMethod,
                req.ShippingConditions, req.ContainerType, req.CreatedBy);
            return Created($"api/lab-shipping/manifests/{manifestId}", new { manifestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manifest for destination {DestinationLab}", req.DestinationLab);
            return StatusCode(500, "An error occurred creating the manifest.");
        }
    }

    [HttpPost("manifests/{manifestId}/specimens")]
    public async Task<IActionResult> AddSpecimen(string manifestId, [FromBody] AddSpecimenRequest req)
    {
        try
        {
            ManifestSpecimen specimen = new()
            {
                SpecimenId = req.SpecimenId,
                PatientId = req.PatientId,
                TestName = req.TestName,
                LoincCode = req.LoincCode,
                SpecimenType = req.SpecimenType,
                CollectionDate = req.CollectionDate,
                IsRemoved = false
            };
            await GetManifestGrain(manifestId).AddSpecimenAsync(specimen);
            return Created($"api/lab-shipping/manifests/{manifestId}/specimens/{req.SpecimenId}",
                new { manifestId, specimenId = req.SpecimenId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding specimen to manifest {ManifestId}", manifestId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("manifests/{manifestId}/specimens/{specimenId}/remove")]
    public async Task<IActionResult> RemoveSpecimen(string manifestId, string specimenId)
    {
        try
        {
            await GetManifestGrain(manifestId).RemoveSpecimenAsync(specimenId);
            return Ok(new { message = "Specimen removed." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing specimen {SpecimenId} from manifest {ManifestId}", specimenId, manifestId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("manifests/{manifestId}/ship")]
    public async Task<IActionResult> ShipManifest(string manifestId, [FromBody] ShipManifestRequest req)
    {
        try
        {
            await GetManifestGrain(manifestId).ShipManifestAsync(req.TrackingNumber);
            return Ok(new { message = "Manifest shipped." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error shipping manifest {ManifestId}", manifestId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("manifests/{manifestId}/received")]
    public async Task<IActionResult> MarkReceived(string manifestId)
    {
        try
        {
            await GetManifestGrain(manifestId).MarkReceivedAsync();
            return Ok(new { message = "Manifest marked as received." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking manifest {ManifestId} as received", manifestId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("manifests/{manifestId}/cancel")]
    public async Task<IActionResult> CancelManifest(string manifestId)
    {
        try
        {
            await GetManifestGrain(manifestId).CancelManifestAsync();
            return Ok(new { message = "Manifest cancelled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling manifest {ManifestId}", manifestId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Config GET endpoints ──────────────────────────────────────────────────

    [HttpGet("configs/{configId}")]
    public async Task<IActionResult> GetConfig(string configId)
    {
        try
        {
            ShippingConfigState state = await GetConfigGrain(configId).GetConfigAsync();
            return Ok(new ShipConfigDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping config {ConfigId}", configId);
            return StatusCode(500, "An error occurred retrieving the shipping configuration.");
        }
    }

    // ─── Config POST endpoints ─────────────────────────────────────────────────

    [HttpPost("configs/{configId}")]
    public async Task<IActionResult> UpdateConfig(string configId, [FromBody] UpdateShipConfigRequest req)
    {
        try
        {
            await GetConfigGrain(configId).UpdateConfigAsync(
                req.LabName, req.Address, req.Phone, req.DefaultShippingMethod,
                req.DefaultConditions, req.DefaultContainerType, req.IsActive, req.AcceptedTests);
            return Ok(new { message = "Shipping configuration updated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shipping config {ConfigId}", configId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ──────────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo()
    {
        try
        {
            // Create reference lab configurations
            string questId = "QUEST-001";
            await GetConfigGrain(questId).UpdateConfigAsync(
                "Quest Diagnostics", "500 Plaza Dr, Secaucus NJ 07094", "800-222-0446",
                "FedEx Priority Overnight", "Ambient", "Biohazard Bag", true,
                new List<string> { "2951-2", "2160-0", "2345-7", "17861-6", "14979-9" });

            string labCorpId = "LABCORP-001";
            await GetConfigGrain(labCorpId).UpdateConfigAsync(
                "LabCorp", "531 S Spring St, Burlington NC 27215", "800-877-7530",
                "FedEx Priority Overnight", "Refrigerated", "Insulated Box", true,
                new List<string> { "14979-9", "5778-6", "2093-3", "2085-9", "2571-8" });

            // Create a manifest with specimens
            string manifest1Id = "DEMO-MAN-001";
            IShippingManifestGrain m1 = GetManifestGrain(manifest1Id);
            await m1.CreateManifestAsync(questId, "Quest Diagnostics", "FedEx Priority Overnight", "Ambient", "Biohazard Bag", "TECH-LAB-1");
            await m1.AddSpecimenAsync(new ManifestSpecimen
            {
                SpecimenId = "SPEC-001", PatientId = "P-001", TestName = "TSH",
                LoincCode = "14979-9", SpecimenType = "Serum", CollectionDate = DateTime.UtcNow.AddHours(-2)
            });
            await m1.AddSpecimenAsync(new ManifestSpecimen
            {
                SpecimenId = "SPEC-002", PatientId = "P-002", TestName = "BMP",
                LoincCode = "2160-0", SpecimenType = "Serum", CollectionDate = DateTime.UtcNow.AddHours(-1)
            });

            // Create a shipped manifest
            string manifest2Id = "DEMO-MAN-002";
            IShippingManifestGrain m2 = GetManifestGrain(manifest2Id);
            await m2.CreateManifestAsync(labCorpId, "LabCorp", "FedEx Priority Overnight", "Refrigerated", "Insulated Box", "TECH-LAB-2");
            await m2.AddSpecimenAsync(new ManifestSpecimen
            {
                SpecimenId = "SPEC-003", PatientId = "P-003", TestName = "Lipid Panel",
                LoincCode = "2093-3", SpecimenType = "Serum", CollectionDate = DateTime.UtcNow.AddDays(-1)
            });
            await m2.ShipManifestAsync("FDX-987654321");

            return Ok(new
            {
                message = "Demo lab shipping data loaded",
                configs = new[] { questId, labCorpId },
                manifests = new[] { manifest1Id, manifest2Id }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo lab shipping data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ShipManifestDto(
    string ManifestId,
    string ShippingConfigId,
    string DestinationLab,
    string Status,
    List<ShipSpecimenDto> Specimens,
    string? TrackingNumber,
    string? ShippingMethod,
    string? ShippingConditions,
    string? ContainerType,
    DateTime CreatedDate,
    DateTime? ShippedDate,
    DateTime? ReceivedDate,
    string? CreatedBy)
{
    public ShipManifestDto(ShippingManifestState s)
        : this(s.ManifestId, s.ShippingConfigId, s.DestinationLab, s.Status.ToString(),
               s.Specimens.Select(sp => new ShipSpecimenDto(sp)).ToList(),
               s.TrackingNumber, s.ShippingMethod, s.ShippingConditions, s.ContainerType,
               s.CreatedDate, s.ShippedDate, s.ReceivedDate, s.CreatedBy) { }
}

public record ShipSpecimenDto(
    string SpecimenId,
    string PatientId,
    string TestName,
    string? LoincCode,
    string? SpecimenType,
    DateTime? CollectionDate,
    bool IsRemoved)
{
    public ShipSpecimenDto(ManifestSpecimen s)
        : this(s.SpecimenId, s.PatientId, s.TestName, s.LoincCode,
               s.SpecimenType, s.CollectionDate, s.IsRemoved) { }
}

public record ShipConfigDto(
    string ConfigId,
    string LabName,
    string? Address,
    string? Phone,
    string? DefaultShippingMethod,
    string? DefaultConditions,
    string? DefaultContainerType,
    bool IsActive,
    List<string> AcceptedTests)
{
    public ShipConfigDto(ShippingConfigState s)
        : this(s.ConfigId, s.LabName, s.Address, s.Phone, s.DefaultShippingMethod,
               s.DefaultConditions, s.DefaultContainerType, s.IsActive, s.AcceptedTests) { }
}

public record CreateManifestRequest(
    string ShippingConfigId,
    string DestinationLab,
    string? ShippingMethod,
    string? ShippingConditions,
    string? ContainerType,
    string? CreatedBy);

public record AddSpecimenRequest(
    string SpecimenId,
    string PatientId,
    string TestName,
    string? LoincCode,
    string? SpecimenType,
    DateTime? CollectionDate);

public record ShipManifestRequest(
    string? TrackingNumber);

public record UpdateShipConfigRequest(
    string LabName,
    string? Address,
    string? Phone,
    string? DefaultShippingMethod,
    string? DefaultConditions,
    string? DefaultContainerType,
    bool IsActive,
    List<string> AcceptedTests);
