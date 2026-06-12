// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^PS(52,...) (VistA PRESCRIPTION file #52) into IPharmacyGrain instances.
/// </summary>
public class PharmacyImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public PharmacyImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var rxGroups = records
            .Where(kvp => kvp.Key.Global == "PS" && kvp.Key.FileNumber == "52")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in rxGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("PS52", ien, "RX");
                IPharmacyGrain grain = _grainFactory.GetGrain<IPharmacyGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^PS(52,ien,0) = PatientDFN^Drug(.01)^Dosage^Route^Schedule^...
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

                string drugName = ZwrParser.Piece(zeroNode.Value, 2) ?? "UNKNOWN DRUG";
                string? dosage = ZwrParser.Piece(zeroNode.Value, 3);
                string? route = ZwrParser.Piece(zeroNode.Value, 4);
                string? schedule = ZwrParser.Piece(zeroNode.Value, 5);
                string? sig = ZwrParser.Piece(zeroNode.Value, 6);

                // Days supply, quantity, refills — piece 7-9
                string? daysStr = ZwrParser.Piece(zeroNode.Value, 7);
                string? qtyStr = ZwrParser.Piece(zeroNode.Value, 8);
                string? refillStr = ZwrParser.Piece(zeroNode.Value, 9);

                int? daysSupply = int.TryParse(daysStr, out int d) ? d : null;
                int? quantity = int.TryParse(qtyStr, out int q) ? q : null;
                int? refills = int.TryParse(refillStr, out int rf) ? rf : null;

                string? providerDfn = ZwrParser.Piece(zeroNode.Value, 10);

                await grain.CreatePrescriptionAsync(
                    patientKey,
                    drugName,
                    null,
                    dosage,
                    route,
                    schedule,
                    sig,
                    daysSupply,
                    quantity,
                    refills,
                    providerDfn,
                    null,
                    null, null,
                    null,
                    null);

                // Link to patient grain
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        // Full-history index first — the PatientState list is a capped recent window.
                        await _grainFactory.GetGrain<IPatientHistoryIndexGrain>($"{patKey}:{PatientHistoryDomains.Pharmacy}")
                            .AddEntryAsync(new HistoryRef { ItemId = grainKey, Date = null });
                        await patient.AddPharmacyIdAsync(grainKey);
                    }
                }

                result.RecordSuccess("Pharmacy");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} prescriptions so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Pharmacy");
                _logger.LogError(ex, "Failed to import prescription IEN {Ien}", group.Key);
            }
        }
    }
}
