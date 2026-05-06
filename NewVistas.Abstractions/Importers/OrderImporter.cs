// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^OR(100,...) (VistA ORDER file #100) into IOrderGrain instances.
/// </summary>
public class OrderImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public OrderImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var orderGroups = records
            .Where(kvp => kvp.Key.Global == "OR" && kvp.Key.FileNumber == "100")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in orderGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("OR100", ien, "ORDER");
                IOrderGrain grain = _grainFactory.GetGrain<IOrderGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^OR(100,ien,0) = Status^PatientDFN^ProviderDFN^...
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string? statusCode = ZwrParser.Piece(zeroNode.Value, 1);
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 2);
                string? providerDfnStr = ZwrParser.Piece(zeroNode.Value, 3);

                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string patientKey = patientDfn > 0
                    ? (_ienMap.TryGetKey("DPT", patientDfn) ?? $"P{patientDfn}")
                    : "UNKNOWN";

                // 1-node: order text / orderable item
                ZwrRecord? oneNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "1");

                string orderType = oneNode != null
                    ? (ZwrParser.Piece(oneNode.Value, 1) ?? "GENERAL")
                    : "GENERAL";
                string orderableItem = oneNode != null
                    ? (ZwrParser.Piece(oneNode.Value, 2) ?? "IMPORTED ORDER")
                    : "IMPORTED ORDER";

                // Start date from 0-node piece 4
                DateTime startDate = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 4))
                    ?? DateTime.UtcNow;

                string urgency = ZwrParser.Piece(zeroNode.Value, 6) ?? "ROUTINE";

                await grain.CreateOrderAsync(
                    patientKey,
                    orderType,
                    orderableItem,
                    null,
                    providerDfnStr ?? "IMPORTED",
                    "Imported Provider",
                    startDate,
                    null, null,
                    urgency,
                    null, null, null, null);

                // Update status if indicated
                if (!string.IsNullOrEmpty(statusCode))
                    await grain.UpdateStatusAsync(statusCode);

                // Link to patient grain and register in order index
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        await patient.AddOrderIdAsync(grainKey);

                        IPatientOrderIndexGrain orderIndex =
                            _grainFactory.GetGrain<IPatientOrderIndexGrain>(patKey);
                        await orderIndex.AddOrUpdateOrderAsync(new OrderIndexEntry
                        {
                            OrderGrainKey = grainKey,
                            StartDate = startDate,
                            OrderType = orderType,
                            Status = statusCode ?? "Pending",
                            OrderText = orderableItem,
                            ProviderName = "Imported Provider",
                            IsSigned = false
                        });
                    }
                }

                result.RecordSuccess("Order");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} orders so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Order");
                _logger.LogError(ex, "Failed to import order IEN {Ien}", group.Key);
            }
        }
    }
}
