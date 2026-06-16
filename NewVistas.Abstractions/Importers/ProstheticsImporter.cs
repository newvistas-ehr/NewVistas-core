// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^RMPR (VistA PROSTHETICS file #669.1) into embedded ProstheticsItem entries
/// via the PatientWorkflowGrain.
/// </summary>
public class ProstheticsImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public ProstheticsImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var prostheticGroups = records
            .Where(kvp => kvp.Key.Global == "RMPR" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in prostheticGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^Item^HCPCSCode^Category^DateIssued(FM)^Qty^Cost^ProviderDFN;VA(200,^SC
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 1);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                string item = ZwrParser.Piece(zeroNode.Value, 2) ?? "UNKNOWN ITEM";
                string? hcpcsCode = ZwrParser.Piece(zeroNode.Value, 3);
                string? category = ZwrParser.Piece(zeroNode.Value, 4);
                DateTime dateIssued = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 5))
                    ?? DateTime.UtcNow;

                int qty = 1;
                string? qtyStr = ZwrParser.Piece(zeroNode.Value, 6);
                if (!string.IsNullOrEmpty(qtyStr) && int.TryParse(qtyStr, out int parsedQty))
                    qty = parsedQty;

                decimal? cost = null;
                string? costStr = ZwrParser.Piece(zeroNode.Value, 7);
                if (!string.IsNullOrEmpty(costStr) && decimal.TryParse(costStr, out decimal parsedCost))
                    cost = parsedCost;

                // Provider reference
                string? providerDfnStr = ZwrParser.Piece(zeroNode.Value, 8);
                string? providerId = null;
                string? providerName = null;
                if (providerDfnStr != null)
                {
                    long.TryParse(providerDfnStr.Split(';')[0], out long providerDfn);
                    if (providerDfn > 0)
                    {
                        providerId = _ienMap.TryGetKey("VA200", providerDfn) ?? $"STAFF-{providerDfn}";
                        providerName = await ResolveProviderNameAsync(providerId, providerDfn);
                    }
                }

                // Service connected flag
                string? scStr = ZwrParser.Piece(zeroNode.Value, 9);
                bool isServiceConnected = scStr == "1";

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.IssueProstheticAsync(
                    item, hcpcsCode, category,
                    dateIssued, qty, cost,
                    providerId, providerName,
                    null, null,         // locationId, locationName
                    isServiceConnected,
                    null);              // comments

                result.RecordSuccess("Prosthetics");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} prosthetic items so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Prosthetics");
                _logger.LogError(ex, "Failed to import prosthetic item IEN {Ien}", group.Key);
            }
        }
    }

    private async Task<string> ResolveProviderNameAsync(string providerKey, long providerDfn)
    {
        try
        {
            INewPersonGrain person = _grainFactory.GetGrain<INewPersonGrain>(providerKey);
            string name = await person.GetDisplayNameAsync();
            return string.IsNullOrEmpty(name) ? $"Provider {providerDfn}" : name;
        }
        catch
        {
            return $"Provider {providerDfn}";
        }
    }
}
