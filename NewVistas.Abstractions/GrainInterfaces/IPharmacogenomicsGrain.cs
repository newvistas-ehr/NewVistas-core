// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A patient's pharmacogenomic profile — the coded gene results (star-allele diplotype + CPIC
/// phenotype) that come back from the genotyping lab, stored as discrete data so drug-gene decision
/// support can fire at prescribing time. Key pattern: the patient id.
/// </summary>
public interface IPharmacogenomicsGrain : IGrainWithStringKey
{
    /// <summary>Records (upserts by gene — one current result per gene) a pharmacogenomic result.</summary>
    Task RecordResultAsync(PgxResultEntry result);

    /// <summary>Removes the current result for a gene.</summary>
    Task RemoveResultAsync(string gene);

    Task<PharmacogenomicsState> GetAsync();
}
