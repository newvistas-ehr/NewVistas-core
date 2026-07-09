// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds the emerging-condition (ProtoCondition) demo: an Active "novel respiratory cluster" that
/// tells the whole early-COVID story from first cluster to U07.1 promotion.
///
/// The cluster (P9201-P9213) all share fever + cough (so they match); anosmia is present in ~62% of
/// the confirmed cohort (the <b>Signal</b>) while hearing-change sits at the background rate (the
/// <b>Noise</b> plant). A 30-patient control slice (P9220-P9249) is assessed for the same symptoms
/// (mostly absent) so the analytics denominators are honest — anosmia lifts well above background,
/// hearing does not. Nine members are pre-confirmed, so the demo's FIRST confirm is the 10th and the
/// threshold-10 alert to QM3 (CAMPBELL,DIANE S — Infection Preventionist) fires live. P9214 is a
/// symptom-clean fresh patient for the survey → screen → candidate → confirm walk-through.
///
/// Runs under SYSTEM-SEED (XUPROG); idempotent (keyed off the proto's existence).
/// </summary>
public static class EmergingConditionSeed
{
    private const string ProtoId = "outbreak-2019-resp";
    private const string Epi = "QM3";

    // Catalog symptom codes.
    private const string Fever = "386661006";
    private const string Cough = "49727002";
    private const string Anosmia = "44169009";
    private const string Hearing = "15188001";
    private const string SoreThroat = "267102003";

    private const SymptomPresence P = SymptomPresence.Present;
    private const SymptomPresence A = SymptomPresence.Absent;

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IProtoConditionGrain proto = grainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{ProtoId}");
            if (!string.IsNullOrEmpty((await proto.GetAsync()).Name))
            {
                logger.LogInformation("Emerging-condition demo already seeded — skipping");
                return;
            }

            logger.LogInformation("Seeding emerging-condition demo (novel respiratory cluster)...");

            // ── Define the cluster ───────────────────────────────────────────
            await proto.CreateAsync("Novel respiratory cluster (2019)",
                "Unexplained respiratory illness; flu-negative; anosmia-predominant. No code yet.", Epi);
            await Feature(proto, "fever", Fever, 2);
            await Feature(proto, "cough", Cough, 2);
            await Feature(proto, "anosmia", Anosmia, 3);
            await Feature(proto, "hearing", Hearing, 1);   // the noise plant
            await Feature(proto, "sorethroat", SoreThroat, 1);
            await proto.SetMatchThresholdAsync(0.40, Epi);
            await proto.ActivateAsync(Epi);
            await proto.SetGuidanceAsync(BedIsolationType.Droplet, "Surgical mask + eye protection; single room if available.", new(), Epi);
            await proto.SetAlertRuleAsync(new ProtoAlertRule
            {
                Threshold = 10, WindowDays = 14, CooldownHours = 24, Recipients = new() { Epi }
            }, Epi);

            // ── Cluster patients (all fever+cough → all match) ───────────────
            // (pid, name, anosmia, hearing, sorethroat)
            var cluster = new (string Pid, string Name, SymptomPresence An, SymptomPresence He, SymptomPresence So)[]
            {
                ("P9201", "OUTBREAK,ALAN A",    P, P, P),
                ("P9202", "OUTBREAK,BETH B",    P, A, P),
                ("P9203", "OUTBREAK,CARL C",    P, A, A),
                ("P9204", "OUTBREAK,DORA D",    P, P, P),
                ("P9205", "OUTBREAK,EARL E",    P, A, A),
                ("P9206", "OUTBREAK,FAYE F",    P, A, P),
                ("P9207", "OUTBREAK,GENE G",    A, P, P),
                ("P9208", "OUTBREAK,HANK H",    A, A, A),
                ("P9209", "OUTBREAK,IONA I",    A, A, P),
                // candidates (left unconfirmed so the live demo confirms the 10th)
                ("P9210", "OUTBREAK,JACK J",    P, A, P),
                ("P9211", "OUTBREAK,KARA K",    P, A, A),
                ("P9212", "OUTBREAK,LIAM L",    A, A, P),
                ("P9213", "OUTBREAK,MAYA M",    A, P, A),
            };

            int i = 0;
            foreach (var c in cluster)
            {
                await Patient(grainFactory, c.Pid, c.Name, 1960 + i, new()
                {
                    [Fever] = P, [Cough] = P, [Anosmia] = c.An, [Hearing] = c.He, [SoreThroat] = c.So
                });
                // Screen → candidate.
                await grainFactory.GetGrain<IProtoConditionScreeningGrain>($"PROTO-SCREEN:{c.Pid}").EvaluateAndRecordAsync(ProtoId);
                i++;
            }

            // Pre-confirm the first nine (so the tenth confirm — done live — trips the alert).
            foreach (var c in cluster.Take(9))
                await proto.ConfirmMemberAsync(c.Pid, Epi);

            // ── P9214 — symptom-clean fresh patient for the survey walk-through ─
            await Patient(grainFactory, "P9214", "OUTBREAK,NINA N", 1985, new());

            // ── Control slice (assessed, mostly absent → honest denominators) ─
            for (int n = 0; n < 30; n++)
            {
                string pid = $"P{9220 + n}";
                bool hearing = n < 8; // ~27% hearing-change at background (matches the cluster rate → Noise)
                await Patient(grainFactory, pid, $"CONTROL,PATIENT {n:00}", 1955 + n, new()
                {
                    [Fever] = A, [Cough] = A, [Anosmia] = A, [Hearing] = hearing ? P : A, [SoreThroat] = A
                });
            }

            logger.LogInformation("Emerging-condition demo seeded: proto {Proto} (Active, 9 confirmed + 4 candidates), P9214 fresh, 30 controls",
                ProtoId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding emerging-condition demo (non-fatal)");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    private static Task Feature(IProtoConditionGrain proto, string id, string code, double weight) =>
        proto.AddOrUpdateFeatureAsync(new ProtoFeature
        {
            FeatureId = id,
            Kind = ProtoFeatureKind.Symptom,
            Display = SymptomCatalogDisplay(code),
            Code = code,
            Operator = ProtoFeatureOperator.Present,
            Rule = ProtoFeatureRule.Weighted,
            Weight = weight
        }, Epi);

    private static string SymptomCatalogDisplay(string code) =>
        NewVistas.Abstractions.Clinical.SymptomCatalog.DisplayFor(code);

    private static async Task Patient(IGrainFactory gf, string pid, string name, int birthYear,
        Dictionary<string, SymptomPresence> symptoms)
    {
        IPatientWorkflowGrain wf = gf.GetGrain<IPatientWorkflowGrain>(pid);
        if (string.IsNullOrEmpty((await wf.GetPatientAsync()).Name))
            await wf.UpdateDemographicsAsync(name, name.Contains(",A") ? "M" : "F", new DateTime(birthYear, 6, 15), null);

        if (symptoms.Count == 0)
            return;

        var obs = symptoms.Select(kv => new SymptomObservation
        {
            Code = kv.Key,
            Presence = kv.Value,
            Source = SymptomSource.Survey,
            RecordedBy = "N1"
        }).ToList();
        // Record via the symptom grain directly — no feature-flag/audit dependency during seeding.
        await gf.GetGrain<IPatientSymptomGrain>($"SYMPTOMS:{pid}").RecordObservationsAsync(obs);
    }
}
