// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all Clinical Procedure records.
/// Grain key pattern: "CP-PROC-IDX:{patientId}"
/// </summary>
public interface IClinicProcedureIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all procedure summaries for this patient, ordered by ordered date descending.</summary>
    Task<List<ClinicProcedureIndexEntry>> GetAllProceduresAsync();

    /// <summary>Returns procedure summaries filtered by category.</summary>
    Task<List<ClinicProcedureIndexEntry>> GetProceduresByCategoryAsync(ClinicProcedureCategory category);

    /// <summary>Returns only completed procedure summaries.</summary>
    Task<List<ClinicProcedureIndexEntry>> GetCompletedProceduresAsync();

    /// <summary>Adds or updates a procedure entry in this index.</summary>
    Task UpsertProcedureAsync(ClinicProcedureIndexEntry entry);

    /// <summary>Removes a procedure entry from this index. Idempotent.</summary>
    Task RemoveProcedureAsync(string procedureId);
}
