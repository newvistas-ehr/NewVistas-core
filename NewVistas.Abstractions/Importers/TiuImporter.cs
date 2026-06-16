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
/// Imports ^TIU(8925,...) (VistA TIU DOCUMENT file #8925) into ITiuDocumentGrain instances.
/// </summary>
public class TiuImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public TiuImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var tiuGroups = records
            .Where(kvp => kvp.Key.Global == "TIU" && kvp.Key.FileNumber == "8925")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in tiuGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("TIU8925", ien, "TIU");
                ITiuDocumentGrain grain = _grainFactory.GetGrain<ITiuDocumentGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^TIU(8925,ien,0) = DocumentType(.01)^PatientDFN^AuthorDFN^...
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string documentType = ZwrParser.Piece(zeroNode.Value, 1) ?? "PROGRESS NOTE";

                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 2);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string patientKey = patientDfn > 0
                    ? (_ienMap.TryGetKey("DPT", patientDfn) ?? $"P{patientDfn}")
                    : "UNKNOWN";

                string? authorDfn = ZwrParser.Piece(zeroNode.Value, 3);
                string? referenceDateStr = ZwrParser.Piece(zeroNode.Value, 7);
                DateTime referenceDate = ZwrParser.ParseFmDate(referenceDateStr) ?? DateTime.UtcNow;

                // Report text — stored in "TEXT" sub-nodes ^TIU(8925,ien,"TEXT",line,0)
                var textNodes = allRecords
                    .Where(r => r.Subscripts.Count >= 1 && r.Subscripts[0] == "TEXT")
                    .OrderBy(r => r.Subscripts.Count > 1 ? r.Subscripts[1] : "0")
                    .ToList();

                string reportText = textNodes.Count > 0
                    ? string.Join(Environment.NewLine, textNodes.Select(t => t.Value))
                    : string.Empty;

                await grain.CreateDocumentAsync(
                    patientKey,
                    documentType,
                    null,
                    reportText,
                    null,
                    authorDfn,
                    null,
                    null, null,
                    null, null,
                    null,
                    referenceDate);

                // Link to patient grain
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        // Full-history index first — the PatientState list is a capped recent window.
                        await _grainFactory.GetGrain<IPatientHistoryIndexGrain>($"{patKey}:{PatientHistoryDomains.Tiu}")
                            .AddEntryAsync(new HistoryRef { ItemId = grainKey, Date = referenceDate });
                        await patient.AddTiuDocumentIdAsync(grainKey);
                    }
                }

                result.RecordSuccess("TIU");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} TIU documents so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("TIU");
                _logger.LogError(ex, "Failed to import TIU document IEN {Ien}", group.Key);
            }
        }
    }
}
