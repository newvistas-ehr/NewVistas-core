// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

[Authorize(Roles = "Pharmacist,Provider")]
[ApiController]
[Route("api/drugaccountability")]
public class DrugAccountabilityController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DrugAccountabilityController> _logger;

    public DrugAccountabilityController(IGrainFactory grainFactory,
        ILogger<DrugAccountabilityController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── private helpers ───────────────────────────────────────────────────────

    private IDrugAccountabilityGrain GetDaGrain(string locationId, string drugId) =>
        _grainFactory.GetGrain<IDrugAccountabilityGrain>($"DA:{locationId}:{drugId}");

    private IDrugAccountabilityLocationGrain GetLocGrain(string locationId) =>
        _grainFactory.GetGrain<IDrugAccountabilityLocationGrain>($"DA-LOC:{locationId}");

    private async Task SyncLocationIndex(string locationId, string drugId)
    {
        IDrugAccountabilityGrain daGrain = GetDaGrain(locationId, drugId);
        DrugAccountabilityState state = await daGrain.GetStateAsync();
        IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locationId);
        await locGrain.AddOrUpdateDrugAsync(new DrugBalanceSummary
        {
            DrugId = state.DrugId,
            DrugName = state.DrugName,
            CurrentBalance = state.CurrentBalance,
            UnitOfMeasure = state.UnitOfMeasure,
            IsControlled = state.IsControlled,
            ReorderPoint = state.ReorderPoint,
            Ndc = state.Ndc
        });
    }

    // ─── GET endpoints ─────────────────────────────────────────────────────────

    [HttpGet("locations/{locationId}/drugs")]
    public async Task<IActionResult> GetAllDrugs(string locationId)
    {
        try
        {
            List<DrugBalanceSummary> drugs = await GetLocGrain(locationId).GetAllDrugsAsync();
            return Ok(drugs.Select(d => new DaBalanceSummaryDto(d)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting drugs for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred retrieving drugs.");
        }
    }

    [HttpGet("locations/{locationId}/drugs/low-stock")]
    public async Task<IActionResult> GetLowStockDrugs(string locationId)
    {
        try
        {
            List<DrugBalanceSummary> drugs = await GetLocGrain(locationId).GetLowStockDrugsAsync();
            return Ok(drugs.Select(d => new DaBalanceSummaryDto(d)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low-stock drugs for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("locations/{locationId}/drugs/controlled")]
    public async Task<IActionResult> GetControlledSubstances(string locationId)
    {
        try
        {
            List<DrugBalanceSummary> drugs = await GetLocGrain(locationId).GetControlledSubstancesAsync();
            return Ok(drugs.Select(d => new DaBalanceSummaryDto(d)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting controlled substances for location {LocationId}", locationId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("locations/{locationId}/drugs/{drugId}")]
    public async Task<IActionResult> GetDrugDetail(string locationId, string drugId)
    {
        try
        {
            DrugAccountabilityState state = await GetDaGrain(locationId, drugId).GetStateAsync();
            return Ok(new DaDrugDetailDto(state));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting drug {DrugId} at location {LocationId}", drugId, locationId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("locations/{locationId}/drugs/{drugId}/history")]
    public async Task<IActionResult> GetTransactionHistory(string locationId, string drugId)
    {
        try
        {
            List<DrugAccountabilityTransaction> txns = await GetDaGrain(locationId, drugId).GetTransactionHistoryAsync();
            return Ok(txns.OrderByDescending(t => t.TransactionDate).Select(t => new TransactionHistoryDto(t)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction history for drug {DrugId} at location {LocationId}", drugId, locationId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── POST endpoints ────────────────────────────────────────────────────────

    [HttpPost("locations/{locationId}/drugs/{drugId}/initialise")]
    public async Task<IActionResult> InitialiseDrug(string locationId, string drugId,
        [FromBody] DaInitialiseRequest req)
    {
        try
        {
            IDrugAccountabilityGrain daGrain = GetDaGrain(locationId, drugId);
            await daGrain.InitialiseAsync(locationId, req.LocationName, drugId, req.DrugName,
                req.Ndc, req.IsControlled, req.IsVaultControlled, req.UnitOfMeasure, req.ReorderPoint);

            IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locationId);
            await locGrain.InitialiseLocationAsync(locationId, req.LocationName, req.LocationType);
            await SyncLocationIndex(locationId, drugId);

            return Created($"api/drugaccountability/locations/{locationId}/drugs/{drugId}", new { locationId, drugId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initialising drug {DrugId} at location {LocationId}", drugId, locationId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("locations/{locationId}/drugs/{drugId}/receive")]
    public async Task<IActionResult> ReceiveStock(string locationId, string drugId,
        [FromBody] DaReceiveRequest req)
    {
        try
        {
            await GetDaGrain(locationId, drugId).ReceiveStockAsync(req.Quantity, req.LotNumber,
                req.Ndc, req.UserId, req.UserName, req.Notes);
            await SyncLocationIndex(locationId, drugId);
            return Ok(new { balance = await GetDaGrain(locationId, drugId).GetBalanceAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving stock for drug {DrugId}", drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("locations/{locationId}/drugs/{drugId}/dispense")]
    public async Task<IActionResult> DispenseToPatient(string locationId, string drugId,
        [FromBody] DaDispenseRequest req)
    {
        try
        {
            await GetDaGrain(locationId, drugId).DispenseToPatientAsync(req.Quantity,
                req.PatientId, req.PrescriptionId, req.UserId, req.UserName);
            await SyncLocationIndex(locationId, drugId);
            return Ok(new { balance = await GetDaGrain(locationId, drugId).GetBalanceAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispensing drug {DrugId}", drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("locations/{locationId}/drugs/{drugId}/return")]
    public async Task<IActionResult> RecordReturn(string locationId, string drugId,
        [FromBody] DaReturnRequest req)
    {
        try
        {
            await GetDaGrain(locationId, drugId).RecordReturnAsync(req.Quantity,
                req.PatientId, req.PrescriptionId, req.UserId, req.UserName, req.Notes);
            await SyncLocationIndex(locationId, drugId);
            return Ok(new { balance = await GetDaGrain(locationId, drugId).GetBalanceAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording return for drug {DrugId}", drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("locations/{locationId}/drugs/{drugId}/waste")]
    public async Task<IActionResult> RecordWaste(string locationId, string drugId,
        [FromBody] DaWasteRequest req)
    {
        try
        {
            await GetDaGrain(locationId, drugId).RecordWasteAsync(req.Quantity,
                req.WitnessId, req.WitnessName, req.UserId, req.UserName, req.Reason);
            await SyncLocationIndex(locationId, drugId);
            return Ok(new { balance = await GetDaGrain(locationId, drugId).GetBalanceAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording waste for drug {DrugId}", drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("locations/{locationId}/drugs/{drugId}/transfer")]
    public async Task<IActionResult> TransferTo(string locationId, string drugId,
        [FromBody] DaTransferRequest req)
    {
        try
        {
            await GetDaGrain(locationId, drugId).TransferToAsync(req.Quantity,
                req.DestinationLocationId, req.UserId, req.UserName, req.Notes);
            await SyncLocationIndex(locationId, drugId);
            return Ok(new { balance = await GetDaGrain(locationId, drugId).GetBalanceAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording transfer for drug {DrugId}", drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("locations/{locationId}/drugs/{drugId}/inventory")]
    public async Task<IActionResult> RecordInventoryCount(string locationId, string drugId,
        [FromBody] DaInventoryCountRequest req)
    {
        try
        {
            await GetDaGrain(locationId, drugId).RecordInventoryCountAsync(req.CountedQuantity,
                req.UserId, req.UserName, req.Notes);
            await SyncLocationIndex(locationId, drugId);
            return Ok(new { balance = await GetDaGrain(locationId, drugId).GetBalanceAsync() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording inventory count for drug {DrugId}", drugId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemo()
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            // ── Location 1: Controlled Vault ──────────────────────────────────
            string vaultId = "VAULT-001";
            string vaultName = "Pharmacy Vault (Controlled)";

            await InitDrug(vaultId, vaultName, "VAULT", "50-MORPHINE", "Morphine Sulfate 15mg Tabs",
                "00406-8771-01", isControlled: true, isVaultControlled: true, "TABLET", 50);
            await InitDrug(vaultId, vaultName, "VAULT", "50-OXYCODONE", "Oxycodone HCl 5mg Tabs",
                "00603-4992-21", isControlled: true, isVaultControlled: true, "TABLET", 100);
            await InitDrug(vaultId, vaultName, "VAULT", "50-LORAZEPAM", "Lorazepam 1mg Tabs",
                "00781-1077-01", isControlled: true, isVaultControlled: false, "TABLET", 50);

            // Receive initial stock
            await GetDaGrain(vaultId, "50-MORPHINE").ReceiveStockAsync(200, "LOT-MOR-001", null, "PHARM-1", "Jane Smith RPh", "Initial stock");
            await GetDaGrain(vaultId, "50-OXYCODONE").ReceiveStockAsync(500, "LOT-OXY-001", null, "PHARM-1", "Jane Smith RPh", "Initial stock");
            await GetDaGrain(vaultId, "50-LORAZEPAM").ReceiveStockAsync(150, "LOT-LOR-001", null, "PHARM-1", "Jane Smith RPh", "Initial stock");

            // Dispense one to patient
            await GetDaGrain(vaultId, "50-MORPHINE").DispenseToPatientAsync(30, "P-001", "RX-5001", "PHARM-1", "Jane Smith RPh");

            // Waste 2 tablets (controlled — witness required)
            await GetDaGrain(vaultId, "50-MORPHINE").RecordWasteAsync(2, "NURSE-7", "Alice Johnson RN", "PHARM-1", "Jane Smith RPh", "Broken tablets");

            await SyncLocationIndex(vaultId, "50-MORPHINE");
            await SyncLocationIndex(vaultId, "50-OXYCODONE");
            await SyncLocationIndex(vaultId, "50-LORAZEPAM");

            // ── Location 2: Dispensing Window ────────────────────────────────
            string dispId = "DISP-001";
            string dispName = "Main Dispensing Window";

            await InitDrug(dispId, dispName, "DISPENSING", "50-METFORMIN", "Metformin 500mg Tabs",
                "00093-1048-01", isControlled: false, isVaultControlled: false, "TABLET", 200);
            await InitDrug(dispId, dispName, "DISPENSING", "50-LISINOPRIL", "Lisinopril 10mg Tabs",
                "00116-2010-01", isControlled: false, isVaultControlled: false, "TABLET", 100);
            await InitDrug(dispId, dispName, "DISPENSING", "50-WARFARIN", "Warfarin Sodium 5mg Tabs",
                "00056-0173-75", isControlled: false, isVaultControlled: false, "TABLET", 50);

            await GetDaGrain(dispId, "50-METFORMIN").ReceiveStockAsync(1000, "LOT-MET-001", null, "PHARM-2", "Bob Jones RPh", "Monthly restock");
            await GetDaGrain(dispId, "50-LISINOPRIL").ReceiveStockAsync(500, "LOT-LIS-001", null, "PHARM-2", "Bob Jones RPh", "Monthly restock");
            await GetDaGrain(dispId, "50-WARFARIN").ReceiveStockAsync(200, "LOT-WAR-001", null, "PHARM-2", "Bob Jones RPh", "Monthly restock");

            await GetDaGrain(dispId, "50-METFORMIN").DispenseToPatientAsync(60, "P-002", "RX-6001", "PHARM-2", "Bob Jones RPh");
            await GetDaGrain(dispId, "50-WARFARIN").DispenseToPatientAsync(30, "P-003", "RX-6002", "PHARM-2", "Bob Jones RPh");

            await SyncLocationIndex(dispId, "50-METFORMIN");
            await SyncLocationIndex(dispId, "50-LISINOPRIL");
            await SyncLocationIndex(dispId, "50-WARFARIN");

            // ── Location 3: Ward Stock ────────────────────────────────────────
            string wardId = "WARD-3E";
            string wardName = "3 East Nursing Station";

            await InitDrug(wardId, wardName, "WARD_STOCK", "50-ASPIRIN", "Aspirin 81mg Tabs",
                "00280-0105-01", isControlled: false, isVaultControlled: false, "TABLET", 100);

            await GetDaGrain(wardId, "50-ASPIRIN").ReceiveStockAsync(300, "LOT-ASP-001", null, "PHARM-2", "Bob Jones RPh", "Ward par stock");
            await GetDaGrain(wardId, "50-ASPIRIN").DispenseToPatientAsync(2, "P-004", null, "NURSE-1", "Carol White RN");

            await SyncLocationIndex(wardId, "50-ASPIRIN");

            return Ok(new
            {
                message = "Demo data loaded",
                locations = new[] { vaultId, dispId, wardId },
                drugsLoaded = 8
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo drug accountability data");
            return StatusCode(500, "An error occurred loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // ─── demo helper ──────────────────────────────────────────────────────────

    private async Task InitDrug(string locationId, string locationName, string locationType,
        string drugId, string drugName, string? ndc, bool isControlled, bool isVaultControlled,
        string unitOfMeasure, decimal reorderPoint)
    {
        IDrugAccountabilityGrain daGrain = GetDaGrain(locationId, drugId);
        await daGrain.InitialiseAsync(locationId, locationName, drugId, drugName, ndc,
            isControlled, isVaultControlled, unitOfMeasure, reorderPoint);

        IDrugAccountabilityLocationGrain locGrain = GetLocGrain(locationId);
        await locGrain.InitialiseLocationAsync(locationId, locationName, locationType);
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record DaBalanceSummaryDto(
    string DrugId,
    string DrugName,
    decimal CurrentBalance,
    string UnitOfMeasure,
    bool IsControlled,
    decimal? ReorderPoint,
    string? Ndc,
    bool IsLowStock)
{
    public DaBalanceSummaryDto(DrugBalanceSummary d)
        : this(d.DrugId, d.DrugName, d.CurrentBalance, d.UnitOfMeasure, d.IsControlled,
               d.ReorderPoint, d.Ndc,
               d.ReorderPoint.HasValue && d.CurrentBalance <= d.ReorderPoint.Value) { }
}

public record DaDrugDetailDto(
    string DrugId,
    string DrugName,
    string LocationId,
    string LocationName,
    string? Ndc,
    bool IsControlled,
    bool IsVaultControlled,
    decimal CurrentBalance,
    string UnitOfMeasure,
    decimal? ReorderPoint,
    DateTime? LastInventoryDate,
    int TransactionCount)
{
    public DaDrugDetailDto(DrugAccountabilityState s)
        : this(s.DrugId, s.DrugName, s.LocationId, s.LocationName, s.Ndc, s.IsControlled,
               s.IsVaultControlled, s.CurrentBalance, s.UnitOfMeasure, s.ReorderPoint,
               s.LastInventoryDate, s.Transactions.Count) { }
}

public record TransactionHistoryDto(
    string TransactionId,
    string TransactionType,
    decimal Quantity,
    decimal BalanceBefore,
    decimal BalanceAfter,
    DateTime TransactionDate,
    string? LotNumber,
    string? UserId,
    string? UserName,
    string? PatientId,
    string? PrescriptionId,
    string? DestinationLocationId,
    string? WitnessId,
    string? WitnessName,
    string? Notes)
{
    public TransactionHistoryDto(DrugAccountabilityTransaction t)
        : this(t.TransactionId, t.TransactionType, t.Quantity, t.BalanceBefore, t.BalanceAfter,
               t.TransactionDate, t.LotNumber, t.UserId, t.UserName, t.PatientId,
               t.PrescriptionId, t.DestinationLocationId, t.WitnessId, t.WitnessName, t.Notes) { }
}

public record DaInitialiseRequest(
    string LocationName,
    string LocationType,
    string DrugName,
    string? Ndc,
    bool IsControlled,
    bool IsVaultControlled,
    string UnitOfMeasure,
    decimal? ReorderPoint);

public record DaReceiveRequest(
    decimal Quantity,
    string? LotNumber,
    string? Ndc,
    string? UserId,
    string? UserName,
    string? Notes);

public record DaDispenseRequest(
    decimal Quantity,
    string? PatientId,
    string? PrescriptionId,
    string? UserId,
    string? UserName);

public record DaReturnRequest(
    decimal Quantity,
    string? PatientId,
    string? PrescriptionId,
    string? UserId,
    string? UserName,
    string? Notes);

public record DaWasteRequest(
    decimal Quantity,
    string? WitnessId,
    string? WitnessName,
    string? UserId,
    string? UserName,
    string? Reason);

public record DaTransferRequest(
    decimal Quantity,
    string DestinationLocationId,
    string? UserId,
    string? UserName,
    string? Notes);

public record DaInventoryCountRequest(
    decimal CountedQuantity,
    string? UserId,
    string? UserName,
    string? Notes);
