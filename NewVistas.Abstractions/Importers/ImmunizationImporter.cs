// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^AUPNVIMM (VistA V IMMUNIZATION file #9000010.11) into embedded ImmunizationEntry
/// via the PatientWorkflowGrain.
/// </summary>
public class ImmunizationImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public ImmunizationImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var immunizationGroups = records
            .Where(kvp => kvp.Key.Global == "AUPNVIMM" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in immunizationGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: VaccineName^CVXCode^EventDate(FM)^Series^LotNumber^Manufacturer^PatientDFN;DPT(
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string vaccineName = ZwrParser.Piece(zeroNode.Value, 1) ?? "UNKNOWN";
                string? cvxCode = ZwrParser.Piece(zeroNode.Value, 2);
                DateTime eventDateTime = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3))
                    ?? DateTime.UtcNow;
                string? series = ZwrParser.Piece(zeroNode.Value, 4);
                string? lotNumber = ZwrParser.Piece(zeroNode.Value, 5);
                string? manufacturer = ZwrParser.Piece(zeroNode.Value, 6);

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 7);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                // .1-node: AdminSite^Route^Dose^ProviderDFN;VA(200,
                string? adminSite = null;
                string? route = null;
                string? dose = null;
                string? adminById = null;
                string? adminByName = null;

                ZwrRecord? dotOneNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == ".1");

                if (dotOneNode != null)
                {
                    adminSite = ZwrParser.Piece(dotOneNode.Value, 1);
                    route = ZwrParser.Piece(dotOneNode.Value, 2);
                    dose = ZwrParser.Piece(dotOneNode.Value, 3);

                    string? providerDfnStr = ZwrParser.Piece(dotOneNode.Value, 4);
                    if (providerDfnStr != null)
                    {
                        long.TryParse(providerDfnStr.Split(';')[0], out long providerDfn);
                        if (providerDfn > 0)
                        {
                            adminById = _ienMap.TryGetKey("VA200", providerDfn)
                                ?? $"STAFF-{providerDfn}";
                            adminByName = await ResolveProviderNameAsync(adminById, providerDfn);
                        }
                    }
                }

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.RecordImmunizationAsync(
                    vaccineName, cvxCode, eventDateTime,
                    series, lotNumber, manufacturer,
                    adminById, adminByName,
                    adminSite, route, dose,
                    null, null, null);

                result.RecordSuccess("Immunization");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} immunizations so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Immunization");
                _logger.LogError(ex, "Failed to import immunization IEN {Ien}", group.Key);
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
