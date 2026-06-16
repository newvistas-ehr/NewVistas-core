// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Service interface for bulk importing reference data (ICD-10, CPT, LOINC,
/// NDF drug products) from CSV files into Orleans grains.
///
/// This replaces VistA's KIDS (Kernel Installation and Distribution System)
/// for loading standardized code sets into the system.
/// </summary>
public interface IReferenceDataImportService
{
    /// <summary>
    /// Imports ICD-10-CM diagnosis codes from a CSV stream.
    /// Expected columns: Code, ShortDescription, LongDescription, Category
    /// </summary>
    Task<ImportResult> ImportIcd10CodesAsync(IGrainFactory grainFactory, Stream csvStream, bool idempotent = true);

    /// <summary>
    /// Imports VA drug products from a CSV stream.
    /// Expected columns: IEN, VaProductName, GenericName, DrugClassCode, DrugClassName, NationalDrugCode, Strength, DosageForm, Route, Unit
    /// </summary>
    Task<ImportResult> ImportDrugProductsAsync(IGrainFactory grainFactory, Stream csvStream, bool idempotent = true);

    /// <summary>
    /// Imports CPT procedure codes from a CSV stream.
    /// Expected columns: Code, ShortName, LongDescription, Category
    /// </summary>
    Task<ImportResult> ImportCptCodesAsync(IGrainFactory grainFactory, Stream csvStream, bool idempotent = true);

    /// <summary>
    /// Imports LOINC observation codes from a CSV stream.
    /// Expected columns: LOINC_NUM, COMPONENT, PROPERTY, TIME_ASPCT, SYSTEM, SCALE_TYP, METHOD_TYP, SHORTNAME, LONG_COMMON_NAME, STATUS, CLASSTYPE
    /// </summary>
    Task<ImportResult> ImportLoincCodesAsync(IGrainFactory grainFactory, Stream csvStream, bool idempotent = true);
}

/// <summary>
/// Result of a bulk reference data import operation.
/// </summary>
[GenerateSerializer]
public class ImportResult
{
    /// <summary>
    /// Total number of records in the input file.
    /// </summary>
    [Id(0)]
    public int TotalRecords { get; set; }

    /// <summary>
    /// Number of records successfully imported.
    /// </summary>
    [Id(1)]
    public int ImportedRecords { get; set; }

    /// <summary>
    /// Number of records skipped (already existed, idempotent mode).
    /// </summary>
    [Id(2)]
    public int SkippedRecords { get; set; }

    /// <summary>
    /// Number of records that failed to import.
    /// </summary>
    [Id(3)]
    public int ErrorRecords { get; set; }

    /// <summary>
    /// Error messages for failed records.
    /// </summary>
    [Id(4)]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total duration of the import operation.
    /// </summary>
    [Id(5)]
    public TimeSpan Duration { get; set; }
}
