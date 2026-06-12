// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/druginteractions")]
public class DrugInteractionDataController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DrugInteractionDataController> _logger;

    public DrugInteractionDataController(IGrainFactory grainFactory,
        ILogger<DrugInteractionDataController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private IDrugInteractionDatasetGrain GetDataset() =>
        _grainFactory.GetGrain<IDrugInteractionDatasetGrain>("DI-DATASET");

    private IDrugInteractionCheckerGrain GetChecker() =>
        _grainFactory.GetGrain<IDrugInteractionCheckerGrain>("CHECKER");

    // ─── GET endpoints ─────────────────────────────────────────────────────────

    [HttpGet("dataset/status")]
    public async Task<IActionResult> GetDatasetStatus()
    {
        try
        {
            DrugInteractionStatus status = await GetDataset().GetStatusAsync();
            return Ok(new DiStatusDto(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting drug interaction dataset status");
            return StatusCode(500, "An error occurred retrieving dataset status.");
        }
    }

    [HttpGet("dataset/all")]
    public async Task<IActionResult> GetAllInteractions()
    {
        try
        {
            List<DrugInteractionPair> pairs = await GetDataset().GetAllInteractionsAsync();
            return Ok(pairs.Select(p => new DiPairDto(p)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all drug interactions");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("dataset/lookup")]
    public async Task<IActionResult> LookupInteraction([FromQuery] string ien1, [FromQuery] string ien2)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ien1) || string.IsNullOrWhiteSpace(ien2))
                return BadRequest(new { message = "Both ien1 and ien2 query parameters are required." });

            DrugInteractionPair? pair = await GetDataset().GetInteractionAsync(ien1, ien2);
            if (pair is null)
                return NotFound(new { message = "No interaction found between the specified ingredients." });
            return Ok(new DiPairDto(pair));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up interaction between {Ien1} and {Ien2}", ien1, ien2);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("cache/ready")]
    public async Task<IActionResult> IsCacheReady()
    {
        try
        {
            bool ready = await GetChecker().IsCacheReadyAsync();
            return Ok(new { isReady = ready });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking interaction cache readiness");
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── POST endpoints ────────────────────────────────────────────────────────

    [HttpPost("dataset/load")]
    public async Task<IActionResult> LoadDataset([FromBody] DiLoadRequest req)
    {
        try
        {
            await GetDataset().LoadInteractionsAsync(req.Pairs);
            DrugInteractionStatus status = await GetDataset().GetStatusAsync();
            return Created("api/druginteractions/dataset/status", new DiStatusDto(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading drug interaction dataset ({PairCount} pairs)", req.Pairs.Count);
            return StatusCode(500, "An error occurred loading the dataset.");
        }
    }

    [HttpPost("dataset/clear")]
    public async Task<IActionResult> ClearDataset()
    {
        try
        {
            await GetDataset().ClearAsync();
            return Ok(new { message = "Drug interaction dataset cleared." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing drug interaction dataset");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("check")]
    public async Task<IActionResult> CheckInteractions([FromBody] DiCheckRequest req)
    {
        try
        {
            DrugInteractionCheckResponse response = await GetChecker().CheckInteractionsAsync(req.Ingredients);

            if (response.Status == DrugInteractionCheckStatus.DataUnavailable)
            {
                return StatusCode(503, new
                {
                    status = "DataUnavailable",
                    message = "Drug interaction dataset is not loaded. Interactions cannot be verified."
                });
            }

            return Ok(response.Results.Select(r => new DiResultDto(r)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking drug interactions for {IngredientCount} ingredients", req.Ingredients.Count);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo()
    {
        try
        {
            List<DrugInteractionPair> demoPairs = new()
            {
                new DrugInteractionPair
                {
                    IngredientIen1 = "1836", IngredientName1 = "WARFARIN SODIUM",
                    IngredientIen2 = "60", IngredientName2 = "ASPIRIN",
                    Severity = InteractionSeverity.Significant,
                    Description = "Concurrent use increases risk of bleeding.",
                    ClinicalEffects = "Increased anticoagulant effect and risk of hemorrhage.",
                    Mechanism = "Aspirin inhibits platelet aggregation and may displace warfarin from protein binding.",
                    Management = "Monitor INR closely. Consider alternative analgesic."
                },
                new DrugInteractionPair
                {
                    IngredientIen1 = "1836", IngredientName1 = "WARFARIN SODIUM",
                    IngredientIen2 = "3603", IngredientName2 = "METRONIDAZOLE",
                    Severity = InteractionSeverity.Significant,
                    Description = "Metronidazole inhibits warfarin metabolism.",
                    ClinicalEffects = "Increased anticoagulant effect.",
                    Mechanism = "CYP2C9 inhibition by metronidazole.",
                    Management = "Monitor INR. Reduce warfarin dose if needed."
                },
                new DrugInteractionPair
                {
                    IngredientIen1 = "1464", IngredientName1 = "SIMVASTATIN",
                    IngredientIen2 = "3241", IngredientName2 = "ERYTHROMYCIN",
                    Severity = InteractionSeverity.Contraindicated,
                    Description = "Erythromycin significantly increases simvastatin levels.",
                    ClinicalEffects = "Risk of rhabdomyolysis.",
                    Mechanism = "CYP3A4 inhibition by erythromycin.",
                    Management = "Avoid combination. Use alternative statin or antibiotic."
                },
                new DrugInteractionPair
                {
                    IngredientIen1 = "2466", IngredientName1 = "LISINOPRIL",
                    IngredientIen2 = "4073", IngredientName2 = "POTASSIUM CHLORIDE",
                    Severity = InteractionSeverity.Moderate,
                    Description = "ACE inhibitors may increase potassium retention.",
                    ClinicalEffects = "Risk of hyperkalemia.",
                    Mechanism = "ACE inhibitors reduce aldosterone secretion, decreasing renal potassium excretion.",
                    Management = "Monitor serum potassium levels. Adjust potassium supplementation."
                },
                new DrugInteractionPair
                {
                    IngredientIen1 = "2780", IngredientName1 = "FLUOXETINE",
                    IngredientIen2 = "3850", IngredientName2 = "TRAMADOL",
                    Severity = InteractionSeverity.Significant,
                    Description = "Risk of serotonin syndrome and seizures.",
                    ClinicalEffects = "Serotonin syndrome, lowered seizure threshold.",
                    Mechanism = "Both agents increase serotonergic activity.",
                    Management = "Avoid if possible. Monitor for signs of serotonin syndrome."
                }
            };

            await GetDataset().LoadInteractionsAsync(demoPairs);
            DrugInteractionStatus status = await GetDataset().GetStatusAsync();

            return Ok(new
            {
                message = "Demo drug interaction dataset loaded",
                pairsLoaded = demoPairs.Count,
                status = new DiStatusDto(status)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo drug interaction data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record DiStatusDto(
    bool IsLoaded,
    DateTime? LastLoadedDate,
    int TotalPairs,
    bool IsCachePopulated)
{
    public DiStatusDto(DrugInteractionStatus s)
        : this(s.IsLoaded, s.LastLoadedDate, s.TotalPairs, s.IsCachePopulated) { }
}

public record DiPairDto(
    string IngredientIen1,
    string IngredientName1,
    string IngredientIen2,
    string IngredientName2,
    string Severity,
    string Description,
    string? ClinicalEffects,
    string? Mechanism,
    string? Management)
{
    public DiPairDto(DrugInteractionPair p)
        : this(p.IngredientIen1, p.IngredientName1, p.IngredientIen2, p.IngredientName2,
               p.Severity.ToString(), p.Description, p.ClinicalEffects, p.Mechanism, p.Management) { }
}

public record DiResultDto(
    DrugIngredient Drug1,
    DrugIngredient Drug2,
    DiPairDto Interaction)
{
    public DiResultDto(DrugInteractionResult r)
        : this(r.Drug1, r.Drug2, new DiPairDto(r.Interaction)) { }
}

public record DiLoadRequest(
    List<DrugInteractionPair> Pairs);

public record DiCheckRequest(
    List<DrugIngredient> Ingredients);
