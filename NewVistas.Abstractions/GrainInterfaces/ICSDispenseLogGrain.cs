// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing the controlled substance dispense log for a vault location.
/// VistA File #58.80 index — PSNLOG.m
/// Grain key: "CS-DISPENSE-LOG:{locationId}"
/// </summary>
public interface ICSDispenseLogGrain : IGrainWithStringKey
{
    /// <summary>Returns all CS dispense records for this location, newest first.</summary>
    Task<List<CSDispenseSummaryEntry>> GetAllRecordsAsync();

    /// <summary>Returns dispense records filtered by drug identifier.</summary>
    Task<List<CSDispenseSummaryEntry>> GetRecordsByDrugAsync(string drugId);

    /// <summary>Returns dispense records within a date range.</summary>
    Task<List<CSDispenseSummaryEntry>> GetRecordsByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>Returns dispense records filtered by DEA schedule.</summary>
    Task<List<CSDispenseSummaryEntry>> GetRecordsByScheduleAsync(DEADrugSchedule schedule);

    /// <summary>Adds or updates a dispense summary entry in the log.</summary>
    Task UpsertRecordAsync(CSDispenseSummaryEntry entry);

    /// <summary>Removes a dispense record from the log by ID.</summary>
    Task RemoveRecordAsync(string recordId);
}
