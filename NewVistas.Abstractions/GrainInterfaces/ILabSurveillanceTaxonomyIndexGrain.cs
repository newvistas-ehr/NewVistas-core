// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton Lab Surveillance Taxonomy index grain.
/// Key: "LAB-SURV-TAX-IDX"
/// </summary>
public interface ILabSurveillanceTaxonomyIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.LabSurveillanceTaxonomyIndexEntry>> GetAllAsync();
    Task<List<GrainStates.LabSurveillanceTaxonomyIndexEntry>> GetActiveAsync();
    Task UpsertAsync(GrainStates.LabSurveillanceTaxonomyIndexEntry entry);
}
