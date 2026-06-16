// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^SRF (VistA SURGERY file #130) into ISurgeryGrain instances.
/// </summary>
public class SurgeryImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public SurgeryImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var surgeryGroups = records
            .Where(kvp => kvp.Key.Global == "SRF" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in surgeryGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("SRF", ien, "SURG");
                ISurgeryGrain grain = _grainFactory.GetGrain<ISurgeryGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^SRF(ien,0) = PatientDFN^Procedure(.01)^DateOfOperation(.09)^...
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 1);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string patientKey = patientDfn > 0
                    ? (_ienMap.TryGetKey("DPT", patientDfn) ?? $"P{patientDfn}")
                    : "UNKNOWN";

                string procedure = ZwrParser.Piece(zeroNode.Value, 2) ?? "UNKNOWN PROCEDURE";
                DateTime dateOfOperation = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3))
                    ?? DateTime.UtcNow;
                string? surgeonDfn = ZwrParser.Piece(zeroNode.Value, 4);
                string? anesthesia = ZwrParser.Piece(zeroNode.Value, 5);
                string? specialty = ZwrParser.Piece(zeroNode.Value, 6);
                string? preOpDiag = ZwrParser.Piece(zeroNode.Value, 7);

                await grain.ScheduleSurgeryAsync(
                    patientKey,
                    procedure,
                    null,
                    dateOfOperation,
                    surgeonDfn,
                    null,
                    anesthesia,
                    specialty,
                    preOpDiag,
                    null, null,
                    null);

                // Operative report — sub-nodes ^SRF(ien,"OP",n,0)
                var opNodes = allRecords
                    .Where(r => r.Subscripts.Count >= 1 && r.Subscripts[0] == "OP")
                    .OrderBy(r => r.Subscripts.Count > 1 ? r.Subscripts[1] : "0")
                    .ToList();

                if (opNodes.Count > 0)
                {
                    string opReport = string.Join(Environment.NewLine, opNodes.Select(n => n.Value));
                    await grain.RecordOperativeReportAsync(opReport, null, null);
                }

                // Link to patient grain
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        // Full-history index first — the PatientState list is a capped recent window.
                        await _grainFactory.GetGrain<IPatientHistoryIndexGrain>($"{patKey}:{PatientHistoryDomains.Surgery}")
                            .AddEntryAsync(new HistoryRef { ItemId = grainKey, Date = dateOfOperation });
                        await patient.AddSurgeryIdAsync(grainKey);
                    }
                }

                result.RecordSuccess("Surgery");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} surgeries so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Surgery");
                _logger.LogError(ex, "Failed to import surgery IEN {Ien}", group.Key);
            }
        }
    }
}
