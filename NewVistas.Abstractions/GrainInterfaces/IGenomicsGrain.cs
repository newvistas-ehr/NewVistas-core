// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A patient's genomics record — interpreted genetic test reports + their coded reportable variants
/// (HGVS / ClinVar). Key pattern: the patient id.
/// </summary>
public interface IGenomicsGrain : IGrainWithStringKey
{
    /// <summary>Records a genetic test report (with any variants); returns the report id.</summary>
    Task<string> RecordReportAsync(GeneticTestReport report);

    /// <summary>Adds a reportable variant to an existing report.</summary>
    Task AddVariantAsync(string reportId, GeneticVariant variant);

    Task RemoveReportAsync(string reportId);

    Task<GenomicsState> GetAsync();
}
