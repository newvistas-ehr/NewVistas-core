// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^PCMM(404.43,...) (VistA PCMM TEAM ASSIGNMENT file #404.43) into CareTeam grains
/// via the PatientWorkflowGrain. Sets PCP or adds care team member based on IsPCP flag.
/// The import orchestrator must pre-populate the IenMap with VA200 user IEN mappings
/// before running this importer.
/// </summary>
public class CareTeamImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public CareTeamImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var careTeamGroups = records
            .Where(kvp => kvp.Key.Global == "PCMM" && kvp.Key.FileNumber == "404.43")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in careTeamGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^ProviderDFN;VA(200,^Role^Specialty^IsPCP^Source
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

                // Provider reference
                string? providerDfnStr = ZwrParser.Piece(zeroNode.Value, 2);
                long providerDfn = 0;
                if (providerDfnStr != null)
                    long.TryParse(providerDfnStr.Split(';')[0], out providerDfn);

                if (providerDfn == 0) continue;

                string providerKey = _ienMap.TryGetKey("VA200", providerDfn)
                    ?? $"STAFF-{providerDfn}";
                string providerName = await ResolveProviderNameAsync(providerKey, providerDfn);

                string? role = ZwrParser.Piece(zeroNode.Value, 3);
                string? specialty = ZwrParser.Piece(zeroNode.Value, 4);

                string? isPcpStr = ZwrParser.Piece(zeroNode.Value, 5);
                bool isPcp = isPcpStr == "1";

                string? source = ZwrParser.Piece(zeroNode.Value, 6);

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                if (isPcp)
                {
                    await workflow.SetPcpAsync(providerKey, providerName, specialty);
                }
                else
                {
                    await workflow.AddCareTeamMemberAsync(
                        providerKey, providerName,
                        role ?? "TEAM MEMBER",
                        specialty,
                        source ?? "PCMM IMPORT",
                        null);          // expirationDate
                }

                result.RecordSuccess("CareTeam");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} care team assignments so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("CareTeam");
                _logger.LogError(ex, "Failed to import care team assignment IEN {Ien}", group.Key);
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
