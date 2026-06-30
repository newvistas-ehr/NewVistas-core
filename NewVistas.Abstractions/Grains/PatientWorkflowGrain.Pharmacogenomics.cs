// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Pharmacogenomics (PGx) orchestration. The patient's coded gene results (star-allele diplotype +
/// CPIC phenotype) are stored on a per-patient <see cref="IPharmacogenomicsGrain"/> and matched to
/// drug-gene guidance by the curated <see cref="Pharmacogenomics"/> knowledge base. The same matcher
/// is read by the DUR engine (see PatientWorkflowGrain.DUR.cs) so a drug-gene contraindication
/// surfaces at prescribing time. Read-only decision support — never auto-orders. Access is open
/// (flag-gated by PHARMACOGENOMICS), matching the genetics blueprint's "results-back" model.
/// </summary>
public partial class PatientWorkflowGrain
{
    private IPharmacogenomicsGrain Pgx() => GrainFactory.GetGrain<IPharmacogenomicsGrain>(PatientId);

    // ─── Writes ─────────────────────────────────────────────────────────────

    public async Task<string> RecordPharmacogenomicResultAsync(
        string gene, string diplotype, PgxPhenotype phenotype, decimal? activityScore,
        DateTime? testDate, string lab, string method, string notes, string recordedBy)
    {
        string resultId = Guid.NewGuid().ToString();
        await Pgx().RecordResultAsync(new PgxResultEntry
        {
            ResultId = resultId,
            Gene = gene,
            Diplotype = diplotype,
            Phenotype = phenotype,
            ActivityScore = activityScore,
            Status = PgxResultStatus.Final,
            TestDate = testDate,
            Lab = lab,
            Method = method,
            Notes = notes,
            RecordedBy = recordedBy,
            RecordedDate = DateTime.UtcNow
        });
        return resultId;
    }

    public Task RemovePharmacogenomicResultAsync(string gene) => Pgx().RemoveResultAsync(gene);

    // ─── Reads (open) ───────────────────────────────────────────────────────

    public Task<PharmacogenomicsState> GetPharmacogenomicProfileAsync() => Pgx().GetAsync();

    public async Task<List<PgxRecommendation>> GetPharmacogenomicRecommendationsAsync()
    {
        PharmacogenomicsState profile = await Pgx().GetAsync();
        return Pharmacogenomics.Match(profile.Results);
    }

    public async Task<List<PgxRecommendation>> CheckDrugPharmacogenomicsAsync(string drugName)
    {
        PharmacogenomicsState profile = await Pgx().GetAsync();
        return Pharmacogenomics.MatchDrug(profile.Results, drugName);
    }
}
