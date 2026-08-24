// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Bone-health demo data for P9001 (SICK,EXTREME LEE).
///
/// P9001 already carried osteoporosis, but only as prose: a problem-list comment and a
/// radiology narrative, both reading "lumbar T-score -3.1, left femoral neck T-score -3.0".
/// Every number a clinician needs was there and none of it could be trended, compared to a
/// later scan, or classified. This seed backfills that same study as structured data and
/// adds the serial bone turnover markers that make a treatment response visible, which is
/// the whole argument for the module.
///
/// Runs under SYSTEM-SEED (XUPROG); idempotent. Must run after ExtremeLeeSickSeed.
/// </summary>
public static class BoneHealthSeed
{
    private const string Pid = "P9001";
    private const string Lab = "Regional Reference Laboratory";

    // The endocrinologist who carries the bone-health thread in this chart, matching the
    // attribution ExtremeLeeSickSeed already uses for the DXA and the FORTEO prescription.
    private const string DrCannotId = "PROV-CANNOT";
    private const string DrCannot = "Dr. Cannot";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);

            PatientState patient = await wf.GetPatientAsync();
            if (string.IsNullOrEmpty(patient.Name))
            {
                logger.LogInformation("Demo patient {Id} not present — skipping bone health seed", Pid);
                return;
            }

            BoneHealthState existing = await wf.GetBoneHealthRecordAsync();
            if (existing.DxaScans.Count > 0 || existing.TurnoverMarkers.Count > 0)
            {
                logger.LogInformation("Bone health data already seeded for {Id} — skipping", Pid);
                return;
            }

            await wf.EnrollInBoneHealthAsync("Osteoporosis", new DateTime(2024, 11, 26));

            // ── The DXA that already existed as free text, now structured ────
            // Values are taken verbatim from the existing radiology narrative on P9001.
            await wf.RecordDxaScanAsync(new DxaScan
            {
                ScanDate = new DateTime(2024, 11, 26),
                ScannerId = "DXA-HOLOGIC-01",
                ScannerModel = "Hologic Horizon A",
                FacilityName = "VA MEDICAL CENTER",
                LeastSignificantChangeGramsPerCm2 = 0.030m,
                ReferenceDatabase = "NHANES III",
                InterpretedByName = "CANNOT,DR",
                Comment = "Osteoporosis of the lumbar spine and left hip. Backfilled from the "
                        + "11/26/2024 DXA report (CPT 77080), which held these values as narrative text.",
                Measurements =
                {
                    new DxaSiteMeasurement
                    {
                        Site = BoneDensitySite.LumbarSpine,
                        BmdGramsPerCm2 = 0.753m,
                        TScore = -3.1m,
                        RegionDetail = "L1-L4",
                    },
                    new DxaSiteMeasurement
                    {
                        Site = BoneDensitySite.FemoralNeck,
                        BmdGramsPerCm2 = 0.521m,
                        TScore = -3.0m,
                        RegionDetail = "Left femoral neck",
                    },
                },
            });

            // ── Therapy already described in the chart ───────────────────────
            // The problem-list comment says "On teriparatide". Recording it as a course gives
            // the turnover markers something to be read against, and teriparatide is
            // duration-limited, so a course with a start date is what a limit would key on.
            await wf.StartOsteoporosisTherapyAsync(new OsteoporosisTherapyCourse
            {
                AgentName = "Teriparatide",
                TherapyClass = OsteoporosisTherapyClass.AnabolicPthAnalogue,
                StartDate = new DateTime(2024, 12, 10),
                Dose = "20 mcg subcutaneously daily",
                PrescriberName = "CANNOT,DR",
            });

            // ── Serial CTX ───────────────────────────────────────────────────
            // Real serial values from a donated longitudinal record, used with permission
            // here on a synthetic patient so the trajectory view has a genuine longitudinal
            // series rather than invented numbers. They are recorded as observations only —
            // no clinical narrative is attached to them, since the interpretation belongs to
            // the treating clinician and not to a demo seed.
            var ctxSeries = new (DateTime Collected, decimal Value)[]
            {
                (new DateTime(2025, 4, 16, 8, 15, 0), 346m),
                (new DateTime(2025, 12, 3, 8, 5, 0), 270m),
                (new DateTime(2026, 7, 29, 8, 30, 0), 845m),
            };

            // Each draw is placed as a real CPOE lab order (ORDER file #100) rather than a
            // bare lab record, so it appears on the Orders tab and in order history the way a
            // clinician would expect — and the bone-health row points back at the order that
            // produced it. PlaceLabOrderAsync is the CPOE path; OrderLabTestAsync (used by the
            // bulk importer) deliberately creates no order.
            foreach ((DateTime collected, decimal value) in ctxSeries)
            {
                string labTestId = await wf.PlaceLabOrderAsync(
                    "LOINC-33959-8", "Collagen type I C-telopeptide (CTX), serum", "CTX",
                    DrCannotId, DrCannot, "BLOOD", "CHEMISTRY");

                // Fasting morning draw — the collection conditions are the whole reason a CTX
                // is interpretable, so they are recorded, not assumed.
                await wf.CollectSpecimenAsync(labTestId, collected, "Venipuncture, fasting a.m.", $"{Lab}");
                await wf.RecordLabResultAsync(
                    labTestId, collected.AddHours(6), value.ToString("0"), "pg/mL",
                    referenceLow: "100", referenceHigh: "700",
                    abnormalFlag: value > 700m ? "H" : value < 100m ? "L" : null);
                await wf.VerifyLabResultAsync(labTestId, DrCannotId, DrCannot, collected.AddHours(7));

                await wf.RecordBoneTurnoverMarkerAsync(new BoneTurnoverMarkerResult
                {
                    MarkerType = BoneTurnoverMarkerType.SerumCtx,
                    Value = value,
                    Units = "pg/mL",
                    CollectedAt = collected,
                    CollectionTimeKnown = true,
                    Fasting = true,
                    Assay = "Beta-CrossLaps (CTX-I)",
                    PerformingLab = Lab,
                    ReferenceLow = 100m,
                    ReferenceHigh = 700m,
                    SourceLabTestId = labTestId,
                });
            }

            // A deliberately under-documented draw, so the interpretability rule is visible
            // in the demo: same analyte, no collection conditions recorded, therefore not
            // comparable with the fasting morning series above.
            await wf.RecordBoneTurnoverMarkerAsync(new BoneTurnoverMarkerResult
            {
                MarkerType = BoneTurnoverMarkerType.SerumCtx,
                Value = 402m,
                Units = "pg/mL",
                CollectedAt = new DateTime(2026, 2, 11),
                CollectionTimeKnown = false,
                Fasting = null,
                PerformingLab = "Outside laboratory",
                Comment = "Result received from an outside laboratory without collection conditions.",
            });

            // ── Formation marker, so both sides of turnover are represented ──
            // Drawn on the same visit as the December CTX, so it is its own order on the
            // same date — that is how it would actually appear in the order list.
            var p1npDrawn = new DateTime(2025, 12, 3, 8, 5, 0);
            string p1npId = await wf.PlaceLabOrderAsync(
                "LOINC-33955-6", "Procollagen type I N-propeptide (P1NP), serum", "P1NP",
                DrCannotId, DrCannot, "BLOOD", "CHEMISTRY");
            await wf.CollectSpecimenAsync(p1npId, p1npDrawn, "Venipuncture, fasting a.m.", Lab);
            await wf.RecordLabResultAsync(p1npId, p1npDrawn.AddHours(6), "78", "ng/mL", "15", "80", null);
            await wf.VerifyLabResultAsync(p1npId, DrCannotId, DrCannot, p1npDrawn.AddHours(7));

            await wf.RecordBoneTurnoverMarkerAsync(new BoneTurnoverMarkerResult
            {
                MarkerType = BoneTurnoverMarkerType.P1np,
                Value = 78m,
                Units = "ng/mL",
                CollectedAt = p1npDrawn,
                CollectionTimeKnown = true,
                Fasting = true,
                Assay = "Total P1NP",
                PerformingLab = Lab,
                ReferenceLow = 15m,
                ReferenceHigh = 80m,
                SourceLabTestId = p1npId,
            });

            // ── Open orders, so the Orders tab shows live work and not only history ──
            // A repeat CTX is ordered but not yet drawn: placed, unresulted, sitting on the
            // list the way a pending order does.
            await wf.PlaceLabOrderAsync(
                "LOINC-33959-8", "Collagen type I C-telopeptide (CTX), serum — recheck",
                "CTX", DrCannotId, DrCannot, "BLOOD", "CHEMISTRY");

            // Surveillance DXA. The last one was 11/2024, so a two-year repeat is due.
            await wf.PlaceRadiologyOrderAsync(
                "XR DXA Bone Density, Hip and Spine", null, "77080", "DXA",
                DrCannotId, DrCannot, "ROUTINE",
                "Osteoporosis on teriparatide. Interval surveillance DXA; compare with 11/26/2024 study.",
                "Osteoporosis monitoring", null, "VA MEDICAL CENTER");

            // The teriparatide the chart already describes, now with an order behind it, so
            // the medication traces back to a signed order like every other therapy.
            await wf.PlaceOrderAsync(
                "Pharmacy",
                "FORTEO (TERIPARATIDE) 20 MCG/DOSE PEN — inject 20 mcg subcutaneously daily",
                null, DrCannotId, DrCannot, null, "ENDOCRINOLOGY",
                "ROUTINE",
                "Rotate injection sites. Refrigerate. 24-month lifetime maximum — plan follow-on antiresorptive.",
                "Osteoporosis (M81.0)");

            // ── Secondary-cause workup ───────────────────────────────────────
            await wf.RecordBoneSecondaryWorkupAsync(new SecondaryCauseWorkup
            {
                WorkupDate = new DateTime(2024, 12, 2),
                OrderedByName = "CANNOT,DR",
                Results = new Dictionary<string, string>
                {
                    ["Calcium"] = "9.4 mg/dL",
                    ["Albumin"] = "4.1 g/dL",
                    ["Phosphate"] = "3.2 mg/dL",
                    ["25-OH vitamin D"] = "18 ng/mL",
                    ["Parathyroid hormone"] = "62 pg/mL",
                    ["TSH"] = "1.8 mIU/L",
                    ["Creatinine"] = "1.0 mg/dL",
                    ["Alkaline phosphatase"] = "74 U/L",
                    ["Testosterone, total"] = "241 ng/dL",
                },
                IdentifiedCauses = { "Vitamin D insufficiency", "Low total testosterone — repeat and evaluate" },
                Comment = "Secondary workup at diagnosis. Roughly half of osteoporosis in men is secondary, "
                        + "so this panel is part of the initial evaluation rather than an afterthought.",
            });

            logger.LogInformation(
                "Seeded bone health for {Id}: 1 DXA study (2 sites), {Markers} turnover markers, 1 therapy course, 1 secondary workup",
                Pid, ctxSeries.Length + 2);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding bone health data for {Id}", Pid);
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
