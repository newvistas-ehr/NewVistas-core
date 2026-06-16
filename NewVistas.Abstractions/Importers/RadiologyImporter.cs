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
/// Imports ^RA(75.1,...) (VistA RAD/NUC MED ORDERS file #75.1) into IRadiologyGrain instances.
/// </summary>
public class RadiologyImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public RadiologyImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var radGroups = records
            .Where(kvp => kvp.Key.Global == "RA" && kvp.Key.FileNumber == "75.1")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in radGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("RA75.1", ien, "RAD");
                IRadiologyGrain grain = _grainFactory.GetGrain<IRadiologyGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^RA(75.1,ien,0) = PatientDFN^Procedure(.01)^ImagingType^Urgency^...
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

                string procedureName = ZwrParser.Piece(zeroNode.Value, 2) ?? "UNKNOWN PROCEDURE";
                string? imagingType = ZwrParser.Piece(zeroNode.Value, 3);
                string? urgency = ZwrParser.Piece(zeroNode.Value, 4);
                string? requestingProvider = ZwrParser.Piece(zeroNode.Value, 5);
                string? clinicalHistory = ZwrParser.Piece(zeroNode.Value, 6);

                await grain.OrderStudyAsync(
                    patientKey,
                    procedureName,
                    null, null,
                    imagingType,
                    requestingProvider,
                    null,
                    urgency,
                    clinicalHistory,
                    null, null,
                    null, null);

                // Report text — sub-nodes ^RA(75.1,ien,"RPT",n,0)
                var rptNodes = allRecords
                    .Where(r => r.Subscripts.Count >= 1 && r.Subscripts[0] == "RPT")
                    .OrderBy(r => r.Subscripts.Count > 1 ? r.Subscripts[1] : "0")
                    .ToList();

                if (rptNodes.Count > 0)
                {
                    string reportText = string.Join(Environment.NewLine, rptNodes.Select(n => n.Value));
                    await grain.RecordReportAsync(
                        reportText,
                        null, null,
                        null, null,
                        DateTime.UtcNow);
                }

                // Link to patient grain
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        // Full-history index first — the PatientState list is a capped recent window.
                        await _grainFactory.GetGrain<IPatientHistoryIndexGrain>($"{patKey}:{PatientHistoryDomains.Radiology}")
                            .AddEntryAsync(new HistoryRef { ItemId = grainKey, Date = null });
                        await patient.AddRadiologyIdAsync(grainKey);
                    }
                }

                result.RecordSuccess("Radiology");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} radiology studies so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Radiology");
                _logger.LogError(ex, "Failed to import radiology IEN {Ien}", group.Key);
            }
        }
    }
}
