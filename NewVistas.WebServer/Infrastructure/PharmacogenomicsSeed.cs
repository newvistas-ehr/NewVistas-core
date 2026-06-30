// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds a pharmacogenomic (PGx) profile onto the existing complex demo patient P9001 (SICK,
/// EXTREME LEE). A representative panel that lights up the drug-gene decision support and the DUR
/// engine: CYP2C19 poor metabolizer (→ clopidogrel), HLA-B*57:01 positive (→ abacavir), DPYD
/// intermediate (→ fluoropyrimidines), SLCO1B1 decreased function (→ simvastatin), plus normal
/// TPMT / CYP2D6 / G6PD results so the panel shows both alerts and clean genes. Runs under
/// SYSTEM-SEED (XUPROG); idempotent. Must run after ExtremeLeeSickSeed (P9001 must exist).
/// </summary>
public static class PharmacogenomicsSeed
{
    private const string Pid = "P9001";
    private const string Lab = "Genomics Reference Laboratory";
    private const string RecordedBy = "Pharmacogenomics Service";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);

            PatientState patient = await wf.GetPatientAsync();
            if (string.IsNullOrEmpty(patient.Name))
            {
                logger.LogInformation("Demo patient {Id} not present — skipping pharmacogenomics seed", Pid);
                return;
            }

            PharmacogenomicsState existing = await wf.GetPharmacogenomicProfileAsync();
            if (existing.Results.Count > 0)
            {
                logger.LogInformation("Pharmacogenomics profile for {Id} already seeded ({N} genes) — skipping", Pid, existing.Results.Count);
                return;
            }

            logger.LogInformation("Seeding pharmacogenomics profile for demo patient {Id} (SICK,EXTREME LEE)...", Pid);

            DateTime tested = DateTime.UtcNow.Date.AddMonths(-6);
            const string panel = "NGS PGx panel";
            const string targeted = "Targeted genotyping";

            // CYP2C19 *2/*2 — poor metabolizer → clopidogrel (avoid), voriconazole (adjust).
            await wf.RecordPharmacogenomicResultAsync("CYP2C19", "*2/*2", PgxPhenotype.PoorMetabolizer, null,
                tested, Lab, panel, "Two no-function alleles; CYP2C19 poor metabolizer.", RecordedBy);

            // HLA-B*57:01 positive → abacavir (contraindicated).
            await wf.RecordPharmacogenomicResultAsync("HLA-B*57:01", "Positive", PgxPhenotype.Positive, null,
                tested, Lab, targeted, "HLA-B*57:01 allele detected.", RecordedBy);

            // DPYD intermediate (c.1905+1G>A / *2A heterozygous) → fluoropyrimidine dose reduction.
            await wf.RecordPharmacogenomicResultAsync("DPYD", "c.1905+1G>A (*2A) heterozygous", PgxPhenotype.IntermediateMetabolizer, 1.0m,
                tested, Lab, panel, "One no-function variant; reduced DPD activity.", RecordedBy);

            // SLCO1B1 decreased function (*1/*5) → simvastatin myopathy risk.
            await wf.RecordPharmacogenomicResultAsync("SLCO1B1", "*1/*5", PgxPhenotype.DecreasedFunction, null,
                tested, Lab, panel, "Decreased transporter function.", RecordedBy);

            // Normal results — shown in the panel as no-action genes.
            await wf.RecordPharmacogenomicResultAsync("TPMT", "*1/*1", PgxPhenotype.NormalMetabolizer, null,
                tested, Lab, panel, "Two function alleles; normal TPMT activity.", RecordedBy);
            await wf.RecordPharmacogenomicResultAsync("CYP2D6", "*1/*1", PgxPhenotype.NormalMetabolizer, 2.0m,
                tested, Lab, panel, "Activity score 2.0; normal metabolizer.", RecordedBy);
            await wf.RecordPharmacogenomicResultAsync("G6PD", "Normal", PgxPhenotype.NormalFunction, null,
                tested, Lab, targeted, "Normal G6PD enzyme activity.", RecordedBy);

            logger.LogInformation("  + pharmacogenomics: 7 genes on {Id} (CYP2C19 PM, HLA-B*57:01+, DPYD IM, SLCO1B1 decreased; DUR will fire on clopidogrel/abacavir)", Pid);
            logger.LogInformation("Pharmacogenomics profile for demo patient {Id} seeded successfully", Pid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding pharmacogenomics profile for {Id} (non-fatal)", Pid);
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
