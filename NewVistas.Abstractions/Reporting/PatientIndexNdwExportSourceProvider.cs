// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;

namespace NewVistas.Abstractions.Reporting;

/// <summary>
/// Default <see cref="INdwExportSourceProvider"/>: returns every patient in
/// the cluster's <see cref="IPatientIndexGrain"/>. Ignores
/// <c>facilityId</c> and the period (the single-cluster index already
/// represents one facility's cohort and "active in period" is a deferred
/// refinement).
///
/// Suitable for round-1 demos and small clinics. Large deployments should
/// register their own provider that filters by per-period encounter
/// activity, multi-facility federation, etc.
/// </summary>
public sealed class PatientIndexNdwExportSourceProvider : INdwExportSourceProvider
{
    private readonly IGrainFactory _grainFactory;

    public PatientIndexNdwExportSourceProvider(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<IReadOnlyList<string>> GetPatientIcnsForExportAsync(
        string facilityId, DateTime periodStart, DateTime periodEnd)
    {
        IPatientIndexGrain index = _grainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");
        return await index.GetAllPatientIdsAsync();
    }
}
