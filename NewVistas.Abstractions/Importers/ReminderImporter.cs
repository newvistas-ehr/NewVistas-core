// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^PXRMPT (VistA CLINICAL REMINDER file #811.9) into ClinicalReminder grains
/// via the PatientWorkflowGrain.
/// </summary>
public class ReminderImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public ReminderImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var reminderGroups = records
            .Where(kvp => kvp.Key.Global == "PXRMPT" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in reminderGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: ReminderName^Category^Priority^Frequency^DueDate(FM)^PatientDFN;DPT(
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                string reminderName = ZwrParser.Piece(zeroNode.Value, 1) ?? "UNKNOWN REMINDER";
                string? category = ZwrParser.Piece(zeroNode.Value, 2);
                string? priority = ZwrParser.Piece(zeroNode.Value, 3);
                string? frequency = ZwrParser.Piece(zeroNode.Value, 4);
                DateTime? dueDate = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 5));

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 6);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.CreateReminderAsync(
                    reminderName,
                    null,               // reminderDefinitionId
                    category,
                    priority,
                    frequency,
                    dueDate);

                result.RecordSuccess("Reminder");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} clinical reminders so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("Reminder");
                _logger.LogError(ex, "Failed to import clinical reminder IEN {Ien}", group.Key);
            }
        }
    }
}
