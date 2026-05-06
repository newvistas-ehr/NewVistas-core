// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^DGPT (VistA PATIENT MOVEMENT file #405) into IAdtGrain instances.
/// </summary>
public class AdtImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public AdtImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var adtGroups = records
            .Where(kvp => kvp.Key.Global == "DGPT" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in adtGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("DGPT", ien, "ADT");
                IAdtGrain grain = _grainFactory.GetGrain<IAdtGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // 0-node: ^DGPT(ien,0) = PatientDFN^TransactionType(.02)^MovementDateTime(.01)^Ward^RoomBed
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

                string? transactionType = ZwrParser.Piece(zeroNode.Value, 2);
                DateTime movementDateTime = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3))
                    ?? DateTime.UtcNow;
                string? wardLocation = ZwrParser.Piece(zeroNode.Value, 4);
                string? roomBed = ZwrParser.Piece(zeroNode.Value, 5);
                string? treatingSpecialty = ZwrParser.Piece(zeroNode.Value, 6);
                string? attendingPhysician = ZwrParser.Piece(zeroNode.Value, 7);
                string? diagnosis = ZwrParser.Piece(zeroNode.Value, 8);

                // Record as admission by default; transfer/discharge handled by transaction type
                await grain.RecordAdmissionAsync(
                    patientKey,
                    movementDateTime,
                    null,
                    wardLocation,
                    roomBed,
                    null,
                    treatingSpecialty,
                    attendingPhysician,
                    null,
                    null,
                    diagnosis,
                    null);

                // Link to patient grain
                if (patientDfn > 0)
                {
                    string? patKey = _ienMap.TryGetKey("DPT", patientDfn);
                    if (patKey != null)
                    {
                        IPatientGrain patient = _grainFactory.GetGrain<IPatientGrain>(patKey);
                        await patient.AddAdtIdAsync(grainKey);
                    }
                }

                result.RecordSuccess("ADT");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} ADT movements so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("ADT");
                _logger.LogError(ex, "Failed to import ADT IEN {Ien}", group.Key);
            }
        }
    }
}
