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
/// Imports ^DEN(228,...) (VistA DENTAL PATIENT file #228) and ^DEN(228.1,...) (DENTAL TREATMENT
/// file #228.1) into DentalPatient and DentalTreatment grains via the PatientWorkflowGrain.
/// </summary>
public class DentalImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public DentalImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        await ImportDentalPatientsAsync(records, result);
        await ImportDentalTreatmentsAsync(records, result);
    }

    private async Task ImportDentalPatientsAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var dentalPatientGroups = records
            .Where(kvp => kvp.Key.Global == "DEN" && kvp.Key.FileNumber == "228")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in dentalPatientGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^Eligibility^PerioStatus^RemainingTeeth^DentistDFN;VA(200,
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

                string? eligibilityStr = ZwrParser.Piece(zeroNode.Value, 2);
                string? perioStatusStr = ZwrParser.Piece(zeroNode.Value, 3);
                string? remainingTeethStr = ZwrParser.Piece(zeroNode.Value, 4);

                // Dentist reference
                string? dentistDfnStr = ZwrParser.Piece(zeroNode.Value, 5);
                string? dentistId = null;
                string? dentistName = null;
                if (dentistDfnStr != null)
                {
                    long.TryParse(dentistDfnStr.Split(';')[0], out long dentistDfn);
                    if (dentistDfn > 0)
                    {
                        dentistId = _ienMap.TryGetKey("VA200", dentistDfn) ?? $"STAFF-{dentistDfn}";
                        dentistName = await ResolveProviderNameAsync(dentistId, dentistDfn);
                    }
                }

                // Node 1: LastExamDate(FM)^LastXRayDate(FM)^LastCleaningDate(FM)
                ZwrRecord? oneNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "1");

                DateTime? lastExamDate = oneNode != null
                    ? ZwrParser.ParseFmDate(ZwrParser.Piece(oneNode.Value, 1)) : null;
                DateTime? lastXRayDate = oneNode != null
                    ? ZwrParser.ParseFmDate(ZwrParser.Piece(oneNode.Value, 2)) : null;
                DateTime? lastCleaningDate = oneNode != null
                    ? ZwrParser.ParseFmDate(ZwrParser.Piece(oneNode.Value, 3)) : null;

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                // Parse eligibility enum
                if (Enum.TryParse<DentalEligibilityStatus>(eligibilityStr, ignoreCase: true,
                    out DentalEligibilityStatus eligStatus))
                {
                    await workflow.UpdateDentalEligibilityAsync(eligStatus, null, null);
                }

                // Parse periodontal status enum
                if (Enum.TryParse<DentalPeriodontalStatus>(perioStatusStr, ignoreCase: true,
                    out DentalPeriodontalStatus perioStatus))
                {
                    int? remainingTeeth = null;
                    if (int.TryParse(remainingTeethStr, out int teeth))
                        remainingTeeth = teeth;

                    await workflow.UpdateDentalClinicalStatusAsync(
                        perioStatus, null, remainingTeeth, false, null);
                }

                // Set primary dentist if available
                if (dentistId != null && dentistName != null)
                {
                    await workflow.SetPrimaryDentistAsync(dentistId, dentistName);
                }

                // Record visit dates
                if (lastExamDate != null || lastXRayDate != null || lastCleaningDate != null)
                {
                    await workflow.RecordDentalVisitDatesAsync(
                        lastExamDate, lastXRayDate, lastCleaningDate);
                }

                result.RecordSuccess("Dental");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} dental patient records so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Dental");
                _logger.LogError(ex, "Failed to import dental patient IEN {Ien}", group.Key);
            }
        }
    }

    private async Task ImportDentalTreatmentsAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var treatmentGroups = records
            .Where(kvp => kvp.Key.Global == "DEN" && kvp.Key.FileNumber == "228.1")
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in treatmentGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^ProcCode^ProcDesc^ToothNum^Surface^Date(FM)^ProviderDFN;VA(200,^Status
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

                string procedureCode = ZwrParser.Piece(zeroNode.Value, 2) ?? "UNKNOWN";
                string procedureDesc = ZwrParser.Piece(zeroNode.Value, 3) ?? "Unknown Procedure";

                // Tooth number -> List<int>
                List<int> toothNumbers = new();
                string? toothStr = ZwrParser.Piece(zeroNode.Value, 4);
                if (int.TryParse(toothStr, out int toothNum))
                    toothNumbers.Add(toothNum);

                // Surface -> List<string>
                List<string> surfaces = new();
                string? surfaceStr = ZwrParser.Piece(zeroNode.Value, 5);
                if (!string.IsNullOrEmpty(surfaceStr))
                    surfaces.Add(surfaceStr);

                DateTime treatmentDate = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 6))
                    ?? DateTime.UtcNow;

                // Provider reference
                string? providerDfnStr = ZwrParser.Piece(zeroNode.Value, 7);
                string providerId = "UNKNOWN";
                string providerName = "Unknown Provider";
                if (providerDfnStr != null)
                {
                    long.TryParse(providerDfnStr.Split(';')[0], out long providerDfn);
                    if (providerDfn > 0)
                    {
                        providerId = _ienMap.TryGetKey("VA200", providerDfn) ?? $"STAFF-{providerDfn}";
                        providerName = await ResolveProviderNameAsync(providerId, providerDfn);
                    }
                }

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.RecordDentalTreatmentAsync(
                    treatmentDate,
                    procedureCode,
                    procedureDesc,
                    DentalProcedureCategory.Diagnostic, // default; actual category inferred from code
                    toothNumbers,
                    surfaces,
                    providerId,
                    providerName,
                    null, null,
                    null, null,
                    null, null);

                result.RecordSuccess("Dental");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} dental treatments so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Dental");
                _logger.LogError(ex, "Failed to import dental treatment IEN {Ien}", group.Key);
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
