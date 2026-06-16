// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Reporting;

/// <summary>
/// Per-deployment policy for "which patients are included in this NDW export
/// run?" — IHS convention is to include patients who were "active users" at
/// the facility during the reporting period (had at least one encounter), but
/// the precise filter varies by site and by the IHS NDW spec version.
///
/// <para>
/// One implementation registered as a singleton per silo. The default
/// (<see cref="GrainInterfaces.PatientIndexNdwExportSourceProvider"/> wired
/// in <see cref="GrainInterfaces.PatientIndexNdwExportSourceProvider"/>) returns
/// every patient in the cluster's index — appropriate for round-1 demos and
/// small clinics where everyone counts. Larger deployments should register
/// their own provider that walks an encounter index for activity in the
/// period.
/// </para>
/// </summary>
public interface INdwExportSourceProvider
{
    /// <summary>
    /// Return the ICNs of patients to include in the export for this facility
    /// and reporting period.
    /// </summary>
    Task<IReadOnlyList<string>> GetPatientIcnsForExportAsync(
        string facilityId,
        DateTime periodStart,
        DateTime periodEnd);
}
