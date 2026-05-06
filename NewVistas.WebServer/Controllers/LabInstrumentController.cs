// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Lab Automated Instrument Interface REST API.
/// Maps to VistA AUTO INSTRUMENT file (#62.4), LA7 MESSAGE PARAMETER (#62.48),
/// LA7 MESSAGE QUEUE (#62.49), and MUMPS routines LA7UCFG.m, LA7VIN.m, LA7SM.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/labinstruments")]
public class LabInstrumentController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<LabInstrumentController> _logger;

    public LabInstrumentController(IGrainFactory grainFactory, ILogger<LabInstrumentController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IInstrumentIndexGrain GetIndex() =>
        _grainFactory.GetGrain<IInstrumentIndexGrain>("LA-INST-INDEX");

    private IAutoInstrumentGrain GetInstrument(string id) =>
        _grainFactory.GetGrain<IAutoInstrumentGrain>(id);

    private IInstrumentMessageQueueGrain GetQueue(string instrumentId) =>
        _grainFactory.GetGrain<IInstrumentMessageQueueGrain>(instrumentId.Replace("LA-INST:", "LA-MQ:"));

    private IAutoVerifyRulesGrain GetAutoVerify(string instrumentId) =>
        _grainFactory.GetGrain<IAutoVerifyRulesGrain>(instrumentId.Replace("LA-INST:", "LA-AUTOVERIFY:"));

    // ─── Instrument CRUD ──────────────────────────────────────────────

    /// <summary>Get all registered instruments.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllInstruments()
    {
        try
        {
            List<InstrumentEntry> instruments = await GetIndex().GetAllInstrumentsAsync();
            return Ok(instruments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting instrument list");
            return StatusCode(500, "An error occurred retrieving instruments.");
        }
    }

    /// <summary>Search instruments by name, section, or manufacturer.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchInstruments([FromQuery] string term)
    {
        try
        {
            List<InstrumentEntry> results = await GetIndex().SearchInstrumentsAsync(term);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching instruments for {Term}", term);
            return StatusCode(500, "An error occurred searching instruments.");
        }
    }

    /// <summary>Get a specific instrument's full configuration.</summary>
    [HttpGet("{instrumentId}")]
    public async Task<IActionResult> GetInstrumentConfig(string instrumentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            AutoInstrumentState config = await GetInstrument(id).GetConfigAsync();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting instrument {Id}", instrumentId);
            return StatusCode(500, "An error occurred retrieving instrument configuration.");
        }
    }

    /// <summary>Create or update an instrument configuration.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrUpdateInstrument([FromBody] LaInstrumentRequest req)
    {
        try
        {
            string id = string.IsNullOrEmpty(req.InstrumentId)
                ? $"LA-INST:{Guid.NewGuid()}"
                : req.InstrumentId;

            IAutoInstrumentGrain grain = GetInstrument(id);
            await grain.UpdateConfigAsync(
                req.Name, req.InstrumentType,
                req.HL7Version, req.TcpHost, req.TcpPort, req.ConnectionTimeoutSeconds,
                req.AutoDownload, req.AutoRelease,
                req.Manufacturer, req.Model, req.SerialNumber,
                req.LabSection, req.Location,
                req.SendingApplication, req.SendingFacility);

            // Update the index
            await GetIndex().AddOrUpdateInstrumentAsync(new InstrumentEntry
            {
                InstrumentId = id,
                Name = req.Name,
                InstrumentType = req.InstrumentType,
                Status = InstrumentStatus.INACTIVE,
                LabSection = req.LabSection,
                Manufacturer = req.Manufacturer,
                Model = req.Model,
                TcpPort = req.TcpPort ?? 0
            });

            return Created("", new { InstrumentId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating instrument");
            return StatusCode(500, "An error occurred saving instrument configuration.");
        }
    }

    /// <summary>Enable an instrument for message processing.</summary>
    [HttpPost("{instrumentId}/enable")]
    public async Task<IActionResult> EnableInstrument(string instrumentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            await GetInstrument(id).EnableAsync();

            // Update index status
            AutoInstrumentState config = await GetInstrument(id).GetConfigAsync();
            await GetIndex().AddOrUpdateInstrumentAsync(new InstrumentEntry
            {
                InstrumentId = id, Name = config.Name,
                InstrumentType = config.InstrumentType, Status = InstrumentStatus.ACTIVE,
                LabSection = config.LabSection, Manufacturer = config.Manufacturer,
                Model = config.Model, TcpPort = config.TcpPort
            });

            return Ok(new { message = "Instrument enabled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling instrument {Id}", instrumentId);
            return StatusCode(500, "An error occurred enabling the instrument.");
        }
    }

    /// <summary>Disable an instrument.</summary>
    [HttpPost("{instrumentId}/disable")]
    public async Task<IActionResult> DisableInstrument(string instrumentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            await GetInstrument(id).DisableAsync();
            return Ok(new { message = "Instrument disabled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling instrument {Id}", instrumentId);
            return StatusCode(500, "An error occurred disabling the instrument.");
        }
    }

    // ─── Test Mappings ────────────────────────────────────────────────

    /// <summary>Get test mappings for an instrument.</summary>
    [HttpGet("{instrumentId}/mappings")]
    public async Task<IActionResult> GetTestMappings(string instrumentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            List<InstrumentTestMapping> mappings = await GetInstrument(id).GetTestMappingsAsync();
            return Ok(mappings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting test mappings for {Id}", instrumentId);
            return StatusCode(500, "An error occurred retrieving test mappings.");
        }
    }

    /// <summary>Add or update a test mapping.</summary>
    [HttpPost("{instrumentId}/mappings")]
    public async Task<IActionResult> AddTestMapping(string instrumentId, [FromBody] InstrumentTestMapping mapping)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            await GetInstrument(id).AddTestMappingAsync(mapping);
            return Created("", new { message = "Test mapping saved." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding test mapping for {Id}", instrumentId);
            return StatusCode(500, "An error occurred saving the test mapping.");
        }
    }

    // ─── Message Queue ────────────────────────────────────────────────

    /// <summary>Get message queue stats for an instrument.</summary>
    [HttpGet("{instrumentId}/queue")]
    public async Task<IActionResult> GetQueueStats(string instrumentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            InstrumentMessageQueueState stats = await GetQueue(id).GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue stats for {Id}", instrumentId);
            return StatusCode(500, "An error occurred retrieving queue statistics.");
        }
    }

    /// <summary>Get recent processing log for an instrument.</summary>
    [HttpGet("{instrumentId}/queue/log")]
    public async Task<IActionResult> GetQueueLog(string instrumentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            List<MessageLogEntry> log = await GetQueue(id).GetRecentLogAsync();
            return Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue log for {Id}", instrumentId);
            return StatusCode(500, "An error occurred retrieving queue log.");
        }
    }

    /// <summary>
    /// Inject a raw HL7 message into an instrument's queue (for testing).
    /// This is the manual equivalent of receiving a message over TCP/MLLP.
    /// </summary>
    [HttpPost("{instrumentId}/messages")]
    public async Task<IActionResult> InjectMessage(string instrumentId, [FromBody] LaMessageInjectRequest req)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            IInstrumentMessageQueueGrain queue = GetQueue(id);
            string messageId = await queue.EnqueueMessageAsync(req.RawHL7);
            int processed = await queue.ProcessAllAsync();
            return Ok(new { MessageId = messageId, Processed = processed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error injecting message for {Id}", instrumentId);
            return StatusCode(500, "An error occurred injecting the HL7 message.");
        }
    }

    // ─── Auto-Verify Rules ───────────────────────────────────────────

    /// <summary>Get auto-verify rules for an instrument.</summary>
    [HttpGet("{instrumentId}/autoverify")]
    public async Task<IActionResult> GetAutoVerifyRules(string instrumentId)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            AutoVerifyRulesState rules = await GetAutoVerify(id).GetRulesAsync();
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auto-verify rules for {Id}", instrumentId);
            return StatusCode(500, "An error occurred retrieving auto-verify rules.");
        }
    }

    /// <summary>Update auto-verify global rules.</summary>
    [HttpPost("{instrumentId}/autoverify")]
    public async Task<IActionResult> UpdateAutoVerifyRules(string instrumentId, [FromBody] LaAutoVerifyRequest req)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            await GetAutoVerify(id).UpdateGlobalRulesAsync(
                req.Enabled, req.MaxMessageAgeMinutes,
                req.RequireSpecimenBarcode, req.RequireDuplicateConfirmation);
            return Ok(new { message = "Auto-verify rules updated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auto-verify rules for {Id}", instrumentId);
            return StatusCode(500, "An error occurred updating auto-verify rules.");
        }
    }

    /// <summary>Add a test-specific auto-verify rule.</summary>
    [HttpPost("{instrumentId}/autoverify/tests")]
    public async Task<IActionResult> AddTestVerifyRule(string instrumentId, [FromBody] TestVerifyRule rule)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            await GetAutoVerify(id).AddTestRuleAsync(rule);
            return Created("", new { message = "Test verify rule saved." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding test verify rule for {Id}", instrumentId);
            return StatusCode(500, "An error occurred saving the test verify rule.");
        }
    }

    /// <summary>Evaluate a result against auto-verify rules (diagnostic endpoint).</summary>
    [HttpPost("{instrumentId}/autoverify/evaluate")]
    public async Task<IActionResult> EvaluateResult(string instrumentId, [FromBody] LaEvaluateRequest req)
    {
        try
        {
            string id = Uri.UnescapeDataString(instrumentId);
            AutoVerifyDecision decision = await GetAutoVerify(id).EvaluateResultAsync(
                req.LoincCode, req.ResultValue, req.PreviousValue);
            return Ok(new { Decision = decision.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating result for {Id}", instrumentId);
            return StatusCode(500, "An error occurred evaluating the result.");
        }
    }

    // ─── Demo Data ───────────────────────────────────────────────────

    /// <summary>Load demo instrument configuration with test mappings and auto-verify rules.</summary>
    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo()
    {
        try
        {
            // Ensure index is seeded
            await GetIndex().SeedDemoInstrumentsAsync();
            List<InstrumentEntry> instruments = await GetIndex().GetAllInstrumentsAsync();

            // Configure the Beckman AU5800 chemistry analyzer with test mappings
            string chemId = "LA-INST:CHEM-001";
            IAutoInstrumentGrain chem = GetInstrument(chemId);
            await chem.UpdateConfigAsync(
                "Beckman AU5800", InstrumentType.UNIVERSAL, "2.5.1",
                "127.0.0.1", 5001, 30, true, true,
                "Beckman Coulter", "AU5800", "BC-AU5800-001",
                "CHEMISTRY", "Lab Room 101", "AU5800", "MAIN_LAB");
            await chem.EnableAsync();

            // Chemistry test mappings
            await chem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "GLU", InternalLoincCode = "2345-7", TestName = "Glucose",
                Specimen = "Serum", ResultUnits = "mg/dL", NormalRangeLow = 70, NormalRangeHigh = 100,
                CriticalLow = 40, CriticalHigh = 500, AutoVerifyEnabled = true
            });
            await chem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "BUN", InternalLoincCode = "3094-0", TestName = "BUN",
                Specimen = "Serum", ResultUnits = "mg/dL", NormalRangeLow = 7, NormalRangeHigh = 20,
                CriticalLow = 2, CriticalHigh = 100, AutoVerifyEnabled = true
            });
            await chem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "CRE", InternalLoincCode = "2160-0", TestName = "Creatinine",
                Specimen = "Serum", ResultUnits = "mg/dL", NormalRangeLow = 0.7, NormalRangeHigh = 1.3,
                CriticalLow = 0.2, CriticalHigh = 10, AutoVerifyEnabled = true
            });
            await chem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "NA", InternalLoincCode = "2951-2", TestName = "Sodium",
                Specimen = "Serum", ResultUnits = "mmol/L", NormalRangeLow = 136, NormalRangeHigh = 145,
                CriticalLow = 120, CriticalHigh = 160, AutoVerifyEnabled = true
            });
            await chem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "K", InternalLoincCode = "2823-3", TestName = "Potassium",
                Specimen = "Serum", ResultUnits = "mmol/L", NormalRangeLow = 3.5, NormalRangeHigh = 5.1,
                CriticalLow = 2.5, CriticalHigh = 6.5, AutoVerifyEnabled = true
            });

            // Auto-verify rules for chemistry analyzer
            IAutoVerifyRulesGrain chemVerify = GetAutoVerify(chemId);
            await chemVerify.UpdateGlobalRulesAsync(true, 60, false, false);
            await chemVerify.AddTestRuleAsync(new TestVerifyRule
            {
                LoincCode = "2345-7", TestName = "Glucose", NormalLow = 70, NormalHigh = 100,
                CriticalLow = 40, CriticalHigh = 500, DeltaCheckPercent = 50, AutoVerifyEnabled = true
            });
            await chemVerify.AddTestRuleAsync(new TestVerifyRule
            {
                LoincCode = "2823-3", TestName = "Potassium", NormalLow = 3.5, NormalHigh = 5.1,
                CriticalLow = 2.5, CriticalHigh = 6.5, DeltaCheckPercent = 30, AutoVerifyEnabled = true
            });

            // Configure Sysmex hematology analyzer
            string hemId = "LA-INST:HEM-001";
            IAutoInstrumentGrain hem = GetInstrument(hemId);
            await hem.UpdateConfigAsync(
                "Sysmex XN-1000", InstrumentType.UNIVERSAL, "2.5.1",
                "127.0.0.1", 5002, 30, true, true,
                "Sysmex", "XN-1000", "SX-XN1000-001",
                "HEMATOLOGY", "Lab Room 102", "XN1000", "MAIN_LAB");
            await hem.EnableAsync();

            // Hematology test mappings
            await hem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "WBC", InternalLoincCode = "6690-2", TestName = "WBC",
                Specimen = "Blood", ResultUnits = "K/cmm", NormalRangeLow = 4.5, NormalRangeHigh = 11.0,
                CriticalLow = 1.0, CriticalHigh = 30.0, AutoVerifyEnabled = true
            });
            await hem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "HGB", InternalLoincCode = "718-7", TestName = "Hemoglobin",
                Specimen = "Blood", ResultUnits = "g/dL", NormalRangeLow = 12.0, NormalRangeHigh = 17.5,
                CriticalLow = 6.0, CriticalHigh = 20.0, AutoVerifyEnabled = true
            });
            await hem.AddTestMappingAsync(new InstrumentTestMapping
            {
                ExternalTestCode = "PLT", InternalLoincCode = "777-3", TestName = "Platelets",
                Specimen = "Blood", ResultUnits = "K/cmm", NormalRangeLow = 150, NormalRangeHigh = 400,
                CriticalLow = 20, CriticalHigh = 1000, AutoVerifyEnabled = true
            });

            return Ok(new
            {
                message = "Demo instrument data loaded.",
                instrumentCount = instruments.Count,
                chemistryMappings = 5,
                hematologyMappings = 3
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo instrument data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
    }

    // ─── Request DTOs ────────────────────────────────────────────────

    public record LaInstrumentRequest
    {
        public string? InstrumentId { get; set; }
        public string Name { get; set; } = "";
        public InstrumentType InstrumentType { get; set; } = InstrumentType.UNIVERSAL;
        public string? HL7Version { get; set; }
        public string? TcpHost { get; set; }
        public int? TcpPort { get; set; }
        public int? ConnectionTimeoutSeconds { get; set; }
        public bool AutoDownload { get; set; }
        public bool AutoRelease { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? LabSection { get; set; }
        public string? Location { get; set; }
        public string? SendingApplication { get; set; }
        public string? SendingFacility { get; set; }
    }

    public record LaMessageInjectRequest
    {
        public string RawHL7 { get; set; } = "";
    }

    public record LaAutoVerifyRequest
    {
        public bool Enabled { get; set; }
        public int MaxMessageAgeMinutes { get; set; } = 60;
        public bool RequireSpecimenBarcode { get; set; }
        public bool RequireDuplicateConfirmation { get; set; }
    }

    public record LaEvaluateRequest
    {
        public string LoincCode { get; set; } = "";
        public string ResultValue { get; set; } = "";
        public string? PreviousValue { get; set; }
    }
}
