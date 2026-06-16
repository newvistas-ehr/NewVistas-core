// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^DPT (VistA PATIENT file #2) records into IPatientGrain instances.
/// </summary>
public class PatientImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public PatientImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var patientGroups = records
            .Where(kvp => kvp.Key.Global == "DPT" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in patientGroups)
        {
            try
            {
                long ien = group.Key;
                string grainKey = _ienMap.GetOrCreateKey("DPT", ien, "PATIENT");
                IPatientGrain grain = _grainFactory.GetGrain<IPatientGrain>(grainKey);

                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Demographics: ^DPT(ien,0) = Name^Sex^DOB(FM)^SSN
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode != null)
                {
                    string name = ZwrParser.Piece(zeroNode.Value, 1) ?? string.Empty;
                    string sex = ZwrParser.Piece(zeroNode.Value, 2) ?? string.Empty;
                    DateTime? dob = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3));
                    string? ssn = ZwrParser.Piece(zeroNode.Value, 4);

                    await grain.UpdateDemographicsAsync(name, sex, dob, ssn);
                }

                // Address: ^DPT(ien,.11) = Street1^Street2^City^State^Zip
                ZwrRecord? addrNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == ".11");

                if (addrNode != null)
                {
                    await grain.UpdateAddressAsync(
                        ZwrParser.Piece(addrNode.Value, 1),
                        ZwrParser.Piece(addrNode.Value, 2),
                        null,
                        ZwrParser.Piece(addrNode.Value, 3),
                        ZwrParser.Piece(addrNode.Value, 4),
                        ZwrParser.Piece(addrNode.Value, 5));
                }

                // Phone: ^DPT(ien,.13) = PhoneResidence
                ZwrRecord? phoneNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == ".13");

                if (phoneNode != null)
                {
                    string? workPhone = null;
                    ZwrRecord? workNode = allRecords.FirstOrDefault(r =>
                        r.Subscripts.Count == 1 && r.Subscripts[0] == ".132");
                    if (workNode != null)
                        workPhone = workNode.Value;

                    await grain.UpdateContactInfoAsync(phoneNode.Value, workPhone, null);
                }

                // Emergency contact: ^DPT(ien,.33) = Name^Relationship^Phone
                ZwrRecord? emergNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == ".33");

                if (emergNode != null)
                {
                    await grain.UpdateEmergencyContactAsync(
                        ZwrParser.Piece(emergNode.Value, 1),
                        ZwrParser.Piece(emergNode.Value, 2),
                        ZwrParser.Piece(emergNode.Value, 3));
                }

                // Veteran info: ^DPT(ien,.36) = Veteran^SC%^Elig^PrimaryElig
                ZwrRecord? vetNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == ".36");

                if (vetNode != null)
                {
                    string veteran = ZwrParser.Piece(vetNode.Value, 1) ?? string.Empty;
                    string? scPctStr = ZwrParser.Piece(vetNode.Value, 2);
                    int? scPct = int.TryParse(scPctStr, out int pct) ? pct : null;

                    await grain.UpdateVeteranInfoAsync(
                        veteran, scPct,
                        ZwrParser.Piece(vetNode.Value, 3),
                        ZwrParser.Piece(vetNode.Value, 4));
                }

                // Military service: ^DPT(ien,.32) = EntryDate^SepDate^Branch^DischargeType^POW
                ZwrRecord? milNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == ".32");

                if (milNode != null)
                {
                    await grain.UpdateMilitaryServiceAsync(
                        ZwrParser.ParseFmDate(ZwrParser.Piece(milNode.Value, 1)),
                        ZwrParser.ParseFmDate(ZwrParser.Piece(milNode.Value, 2)),
                        ZwrParser.Piece(milNode.Value, 3),
                        ZwrParser.Piece(milNode.Value, 4),
                        ZwrParser.Piece(milNode.Value, 5));
                }

                // Set VistA DFN (the IEN is the DFN in PATIENT file #2)
                await grain.SetDfnAsync(ien.ToString());

                result.RecordSuccess("Patient");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} patients so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Patient");
                _logger.LogError(ex, "Failed to import patient IEN {Ien}", group.Key);
            }
        }
    }
}
