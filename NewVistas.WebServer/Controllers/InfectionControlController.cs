// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/infectioncontrol")]
public class InfectionControlController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InfectionControlController> _logger;

    public InfectionControlController(IGrainFactory grainFactory, ILogger<InfectionControlController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IHAICaseIndexGrain CaseIndex =>
        _grainFactory.GetGrain<IHAICaseIndexGrain>("HAI-CASE-IDX");

    private IOutbreakIndexGrain OutbreakIndex =>
        _grainFactory.GetGrain<IOutbreakIndexGrain>("HAI-OUTBREAK-IDX");

    private IHAICaseGrain GetCaseGrain(string caseId) =>
        _grainFactory.GetGrain<IHAICaseGrain>($"HAI-CASE:{caseId}");

    private IOutbreakGrain GetOutbreakGrain(string outbreakId) =>
        _grainFactory.GetGrain<IOutbreakGrain>($"HAI-OUTBREAK:{outbreakId}");

    // ── HAI Cases ─────────────────────────────────────────────────────────────

    [HttpGet("cases")]
    public async Task<IActionResult> GetAllCases()
    {
        try
        {
            List<HAICaseSummary> result = await CaseIndex.GetAllCasesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all HAI cases");
            return StatusCode(500, "An error occurred retrieving HAI cases.");
        }
    }

    [HttpGet("cases/active")]
    public async Task<IActionResult> GetActiveCases()
    {
        try
        {
            List<HAICaseSummary> result = await CaseIndex.GetActiveAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active HAI cases");
            return StatusCode(500, "An error occurred retrieving active HAI cases.");
        }
    }

    [HttpGet("cases/{caseId}")]
    public async Task<IActionResult> GetCase(string caseId)
    {
        try
        {
            HAICaseState result = await GetCaseGrain(caseId).GetCaseAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HAI case {CaseId}", caseId);
            return StatusCode(500, "An error occurred retrieving the HAI case.");
        }
    }

    [HttpGet("cases/type/{haiType}")]
    public async Task<IActionResult> GetCasesByType(HAIType haiType)
    {
        try
        {
            List<HAICaseSummary> result = await CaseIndex.GetByTypeAsync(haiType);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {HAIType} cases", haiType);
            return StatusCode(500, "An error occurred retrieving cases by type.");
        }
    }

    [HttpGet("cases/location/{locationId}")]
    public async Task<IActionResult> GetCasesByLocation(string locationId)
    {
        try
        {
            List<HAICaseSummary> result = await CaseIndex.GetByLocationAsync(locationId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving HAI cases for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving cases by location.");
        }
    }

    [HttpPost("cases")]
    public async Task<IActionResult> CreateCase([FromBody] CreateHAICaseRequest req)
    {
        try
        {
            string caseId = Guid.NewGuid().ToString();
            IHAICaseGrain grain = GetCaseGrain(caseId);
            await grain.CreateCaseAsync(
                caseId, req.PatientId, req.PatientName, req.DateOfBirth,
                req.LocationId, req.LocationName, req.HAIType, req.InfectionDate,
                req.Pathogen, req.ReportedById, req.ReportedByName, req.Notes);

            HAICaseSummary summary = new()
            {
                CaseId = caseId,
                PatientId = req.PatientId,
                PatientName = req.PatientName,
                HAIType = req.HAIType,
                Status = HAICaseStatus.Suspected,
                InfectionDate = req.InfectionDate,
                LocationId = req.LocationId,
                LocationName = req.LocationName,
                Pathogen = req.Pathogen,
            };
            await CaseIndex.UpsertCaseAsync(summary);

            return Created($"/api/infectioncontrol/cases/{caseId}", new { caseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating HAI case for patient {PatientId}", req.PatientId);
            return StatusCode(500, "An error occurred creating the HAI case.");
        }
    }

    [HttpPost("cases/{caseId}/status")]
    public async Task<IActionResult> UpdateCaseStatus(string caseId, [FromBody] UpdateHAICaseStatusRequest req)
    {
        try
        {
            IHAICaseGrain grain = GetCaseGrain(caseId);
            await grain.UpdateStatusAsync(req.Status, req.ConfirmedDate);

            HAICaseState state = await grain.GetCaseAsync();
            HAICaseSummary summary = new()
            {
                CaseId = caseId,
                PatientId = state.PatientId,
                PatientName = state.PatientName,
                HAIType = state.HAIType,
                Status = state.Status,
                InfectionDate = state.InfectionDate,
                LocationId = state.LocationId,
                LocationName = state.LocationName,
                Pathogen = state.Pathogen,
                OutbreakId = state.OutbreakId,
            };
            await CaseIndex.UpsertCaseAsync(summary);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for HAI case {CaseId}", caseId);
            return StatusCode(500, "An error occurred updating the case status.");
        }
    }

    [HttpPost("cases/{caseId}/clinical")]
    public async Task<IActionResult> UpdateClinicalData(string caseId, [FromBody] UpdateHAIClinicalRequest req)
    {
        try
        {
            await GetCaseGrain(caseId).UpdateClinicalDataAsync(
                req.CultureSource, req.CultureDate, req.GramStain, req.CultureResult,
                req.DeviceType, req.DeviceInDays, req.SurgeryDate, req.SurgeryProcedure);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating clinical data for HAI case {CaseId}", caseId);
            return StatusCode(500, "An error occurred updating clinical data.");
        }
    }

    [HttpPost("cases/{caseId}/susceptibility")]
    public async Task<IActionResult> AddSusceptibilityResult(string caseId, [FromBody] AddSusceptibilityRequest req)
    {
        try
        {
            await GetCaseGrain(caseId).AddSusceptibilityResultAsync(new AntibioticSusceptibilityResult
            {
                AntibioticName = req.AntibioticName,
                Susceptibility = req.Susceptibility,
                MIC = req.MIC,
            });
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding susceptibility result for HAI case {CaseId}", caseId);
            return StatusCode(500, "An error occurred adding the susceptibility result.");
        }
    }

    // ── Outbreaks ─────────────────────────────────────────────────────────────

    [HttpGet("outbreaks")]
    public async Task<IActionResult> GetAllOutbreaks()
    {
        try
        {
            List<OutbreakSummary> result = await OutbreakIndex.GetAllOutbreaksAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all outbreaks");
            return StatusCode(500, "An error occurred retrieving outbreaks.");
        }
    }

    [HttpGet("outbreaks/active")]
    public async Task<IActionResult> GetActiveOutbreaks()
    {
        try
        {
            List<OutbreakSummary> result = await OutbreakIndex.GetActiveAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active outbreaks");
            return StatusCode(500, "An error occurred retrieving active outbreaks.");
        }
    }

    [HttpGet("outbreaks/{outbreakId}")]
    public async Task<IActionResult> GetOutbreak(string outbreakId)
    {
        try
        {
            OutbreakState result = await GetOutbreakGrain(outbreakId).GetOutbreakAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving outbreak {OutbreakId}", outbreakId);
            return StatusCode(500, "An error occurred retrieving the outbreak.");
        }
    }

    [HttpPost("outbreaks")]
    public async Task<IActionResult> CreateOutbreak([FromBody] CreateOutbreakRequest req)
    {
        try
        {
            string outbreakId = Guid.NewGuid().ToString();
            IOutbreakGrain grain = GetOutbreakGrain(outbreakId);
            await grain.CreateOutbreakAsync(
                outbreakId, req.Name, req.Description, req.HAIType,
                req.StartDate, req.LocationId, req.LocationName, req.Pathogen);

            OutbreakSummary summary = new()
            {
                OutbreakId = outbreakId,
                Name = req.Name,
                HAIType = req.HAIType,
                Status = OutbreakStatus.Active,
                StartDate = req.StartDate,
                LocationId = req.LocationId,
                LocationName = req.LocationName,
                CaseCount = 0,
            };
            await OutbreakIndex.UpsertOutbreakAsync(summary);

            return Created($"/api/infectioncontrol/outbreaks/{outbreakId}", new { outbreakId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating outbreak");
            return StatusCode(500, "An error occurred creating the outbreak.");
        }
    }

    [HttpPost("outbreaks/{outbreakId}/cases/{caseId}")]
    public async Task<IActionResult> LinkCaseToOutbreak(string outbreakId, string caseId)
    {
        try
        {
            IOutbreakGrain outbreakGrain = GetOutbreakGrain(outbreakId);
            IHAICaseGrain caseGrain = GetCaseGrain(caseId);

            await outbreakGrain.AddCaseAsync(caseId);
            await caseGrain.LinkToOutbreakAsync(outbreakId);

            // Sync indexes
            OutbreakState outbreakState = await outbreakGrain.GetOutbreakAsync();
            await OutbreakIndex.UpsertOutbreakAsync(new OutbreakSummary
            {
                OutbreakId = outbreakId,
                Name = outbreakState.Name,
                HAIType = outbreakState.HAIType,
                Status = outbreakState.Status,
                StartDate = outbreakState.StartDate,
                LocationId = outbreakState.LocationId,
                LocationName = outbreakState.LocationName,
                CaseCount = outbreakState.LinkedCaseIds.Count,
            });

            HAICaseState caseState = await caseGrain.GetCaseAsync();
            await CaseIndex.UpsertCaseAsync(new HAICaseSummary
            {
                CaseId = caseId,
                PatientId = caseState.PatientId,
                PatientName = caseState.PatientName,
                HAIType = caseState.HAIType,
                Status = caseState.Status,
                InfectionDate = caseState.InfectionDate,
                LocationId = caseState.LocationId,
                LocationName = caseState.LocationName,
                Pathogen = caseState.Pathogen,
                OutbreakId = outbreakId,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking case {CaseId} to outbreak {OutbreakId}", caseId, outbreakId);
            return StatusCode(500, "An error occurred linking the case to the outbreak.");
        }
    }

    [HttpPost("outbreaks/{outbreakId}/status")]
    public async Task<IActionResult> UpdateOutbreakStatus(string outbreakId, [FromBody] UpdateOutbreakStatusRequest req)
    {
        try
        {
            IOutbreakGrain grain = GetOutbreakGrain(outbreakId);
            await grain.UpdateStatusAsync(req.Status, req.ControlDate, req.CloseDate);

            OutbreakState state = await grain.GetOutbreakAsync();
            await OutbreakIndex.UpsertOutbreakAsync(new OutbreakSummary
            {
                OutbreakId = outbreakId,
                Name = state.Name,
                HAIType = state.HAIType,
                Status = state.Status,
                StartDate = state.StartDate,
                LocationId = state.LocationId,
                LocationName = state.LocationName,
                CaseCount = state.LinkedCaseIds.Count,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for outbreak {OutbreakId}", outbreakId);
            return StatusCode(500, "An error occurred updating the outbreak status.");
        }
    }

    // ── Antibiogram ───────────────────────────────────────────────────────────

    [HttpGet("antibiogram")]
    public async Task<IActionResult> GetAntibiogram([FromQuery] HAIType? haiType, [FromQuery] int? year)
    {
        try
        {
            List<HAICaseSummary> summaries = await CaseIndex.GetAllCasesAsync();

            if (haiType.HasValue)
                summaries = summaries.Where(c => c.HAIType == haiType.Value).ToList();
            if (year.HasValue)
                summaries = summaries.Where(c => c.InfectionDate.HasValue && c.InfectionDate.Value.Year == year.Value).ToList();

            // Load full case states in parallel to get susceptibility data
            HAICaseState[] caseStates = await Task.WhenAll(
                summaries.Select(s => GetCaseGrain(s.CaseId).GetCaseAsync()));

            // Aggregate: group by pathogen + antibiotic
            Dictionary<(string Pathogen, string Antibiotic), List<AntibioticSusceptibility>> grouped = new();
            foreach (HAICaseState cs in caseStates)
            {
                foreach (AntibioticSusceptibilityResult r in cs.SusceptibilityResults)
                {
                    var key = (cs.Pathogen, r.AntibioticName);
                    if (!grouped.ContainsKey(key))
                        grouped[key] = new();
                    grouped[key].Add(r.Susceptibility);
                }
            }

            List<AntibiogramRow> rows = grouped.Select(kvp =>
            {
                List<AntibioticSusceptibility> results = kvp.Value;
                int total = results.Count(r => r != AntibioticSusceptibility.NotTested);
                return new AntibiogramRow
                {
                    Pathogen = kvp.Key.Pathogen,
                    Antibiotic = kvp.Key.Antibiotic,
                    NTested = total,
                    PctSusceptible = total > 0 ? Math.Round(100.0 * results.Count(r => r == AntibioticSusceptibility.Susceptible) / total, 1) : 0,
                    PctIntermediate = total > 0 ? Math.Round(100.0 * results.Count(r => r == AntibioticSusceptibility.Intermediate) / total, 1) : 0,
                    PctResistant = total > 0 ? Math.Round(100.0 * results.Count(r => r == AntibioticSusceptibility.Resistant) / total, 1) : 0,
                };
            }).OrderBy(r => r.Pathogen).ThenBy(r => r.Antibiotic).ToList();

            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating antibiogram");
            return StatusCode(500, "An error occurred generating the antibiogram.");
        }
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record CreateHAICaseRequest(
    string PatientId,
    string PatientName,
    DateTime? DateOfBirth,
    string LocationId,
    string LocationName,
    HAIType HAIType,
    DateTime? InfectionDate,
    string Pathogen,
    string ReportedById,
    string ReportedByName,
    string? Notes);

public record UpdateHAICaseStatusRequest(
    HAICaseStatus Status,
    DateTime? ConfirmedDate);

public record UpdateHAIClinicalRequest(
    string CultureSource,
    DateTime? CultureDate,
    string GramStain,
    string CultureResult,
    string DeviceType,
    int? DeviceInDays,
    DateTime? SurgeryDate,
    string SurgeryProcedure);

public record AddSusceptibilityRequest(
    string AntibioticName,
    AntibioticSusceptibility Susceptibility,
    string? MIC);

public record CreateOutbreakRequest(
    string Name,
    string Description,
    HAIType HAIType,
    DateTime? StartDate,
    string LocationId,
    string LocationName,
    string Pathogen);

public record UpdateOutbreakStatusRequest(
    OutbreakStatus Status,
    DateTime? ControlDate,
    DateTime? CloseDate);

public record AntibiogramRow
{
    public string Pathogen { get; init; } = string.Empty;
    public string Antibiotic { get; init; } = string.Empty;
    public int NTested { get; init; }
    public double PctSusceptible { get; init; }
    public double PctIntermediate { get; init; }
    public double PctResistant { get; init; }
}
