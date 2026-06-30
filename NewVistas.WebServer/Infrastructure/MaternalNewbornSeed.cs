// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds a maternal–newborn demo patient — DELIVERED,DONNA (grain key P9002) — with the whole
/// continuum: demographics → a delivered pregnancy with prenatal visits → delivery + postpartum →
/// a newborn in the nursery (BABY GIRL DELIVERED) with exam, newborn screening, and interval
/// measurements. Populates the (previously empty) Prenatal pages and the new Neonatal / Nursery
/// page in one coherent story. Runs under the SYSTEM-SEED (XUPROG) context; idempotent.
/// </summary>
public static class MaternalNewbornSeed
{
    private const string Pid = "P9002";
    private const string DrStorkId = "PROV-STORK", DrStork = "Dr. Stork";   // OB / nursery attending
    private const string WomensClinic = "Women's & Newborn Center";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            IPatientWorkflowGrain wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);

            PatientState existing = await wf.GetPatientAsync();
            if (!string.IsNullOrEmpty(existing.Name))
            {
                logger.LogInformation("Demo patient {Id} ({Name}) already exists — skipping maternal-newborn seed", Pid, existing.Name);
                return;
            }

            logger.LogInformation("Seeding maternal-newborn demo patient {Id} (DELIVERED,DONNA)...", Pid);

            // ── Demographics ────────────────────────────────────────────────────────
            await wf.UpdateDemographicsAsync("DELIVERED,DONNA", "F", new DateTime(1994, 5, 20), "666009002");
            await wf.UpdateAddressAsync("48 Maple Avenue", null, null, "Salem", "MA", "01970");
            await wf.UpdateContactInfoAsync("978-555-0190", null, "donna.delivered@newvistas.demo");
            await wf.UpdateMaritalStatusAsync("MARRIED");

            // The baby's birth — yesterday morning, so the newborn is day-of-life 1 in the nursery.
            DateTime birth = DateTime.UtcNow.Date.AddDays(-1).AddHours(8);
            DateTime lmp = birth.AddDays(-279);
            DateTime edd = lmp.AddDays(280);

            // ── Pregnancy (gravida 2, para 1 → now delivered; low risk) ─────────────
            string pregnancyId = await wf.CreatePregnancyAsync(
                lastMenstrualPeriod: lmp,
                eddByLmp: edd,
                eddByUltrasound: edd,
                definitiveEdd: edd,
                gravida: 2, para: 1, abortions: 0, living: 1,
                riskLevel: PregnancyRiskLevel.Low,
                riskFactors: null,
                providerId: DrStorkId, providerName: DrStork,
                locationId: null, locationName: WomensClinic,
                notes: "Healthy 31 y/o G2P1, uncomplicated prenatal course.");

            // ── Prenatal visits (28w, 36w, 39w) ─────────────────────────────────────
            await wf.CreatePrenatalVisitAsync(pregnancyId, birth.AddDays(-77), 28, 0, 158m, 112, 70, 28m, 144,
                FetalPresentation.Cephalic, true, "Negative", "Negative", "None", null, null, null,
                DrStorkId, DrStork, "Normal interval growth; glucola normal.", birth.AddDays(-49));
            await wf.CreatePrenatalVisitAsync(pregnancyId, birth.AddDays(-21), 36, 0, 170m, 118, 74, 36m, 140,
                FetalPresentation.Cephalic, true, "Trace", "Negative", "Trace", null, null, null,
                DrStorkId, DrStork, "GBS swab obtained.", birth.AddDays(-7));
            await wf.CreatePrenatalVisitAsync(pregnancyId, birth.AddDays(-7), 39, 0, 173m, 120, 76, 39m, 138,
                FetalPresentation.Cephalic, true, "Negative", "Negative", "1+", 1m, 50, -2,
                DrStorkId, DrStork, "Early latent labor; cephalic, reassuring.", null);

            // ── Delivery + postpartum ───────────────────────────────────────────────
            await wf.RecordDeliveryAsync(pregnancyId, new DeliveryInfo
            {
                DeliveryDate = birth,
                DeliveryMethod = DeliveryMethod.SpontaneousVaginal,
                GestationalAgeAtDeliveryWeeks = 39,
                BirthWeightGrams = 3350,
                Apgar1Min = 8,
                Apgar5Min = 9,
                Presentation = FetalPresentation.Cephalic,
                AnesthesiaType = "Epidural",
                PerinealStatus = "Intact",
                EstimatedBloodLossMl = 300,
                PlacentaDelivery = "Spontaneous",
                InfantSex = "F",
                Notes = "Uncomplicated spontaneous vaginal delivery of a vigorous female infant."
            }, PregnancyOutcome.LiveBirth);

            await wf.RecordPostpartumAsync(pregnancyId, new PostpartumInfo
            {
                PostpartumVisitDate = birth.AddHours(12),
                BreastfeedingStatus = "Exclusive",
                ContraceptiveMethod = "Progestin IUD planned at 6-week visit",
                DepressionScreeningResult = "Negative",
                EpdsScore = 4,
                Notes = "Mother recovering well; breastfeeding established."
            });

            // ── Newborn — registered from the delivery, currently in the nursery ────
            string newbornId = await wf.RegisterNewbornFromDeliveryAsync(
                pregnancyId,
                "BABY GIRL DELIVERED",
                NewbornSex.Female,
                birth,
                gestationalAgeWeeks: 39, gestationalAgeDays: 2,
                deliveryMethod: DeliveryMethod.SpontaneousVaginal,
                birthWeightGrams: 3350,
                lengthCm: 50m,
                headCircumferenceCm: 34.5m,
                apgar1Min: 8, apgar5Min: 9, apgar10Min: null,
                multipleBirthOrder: 1, multipleBirthTotal: 1,
                attendingProviderId: DrStorkId, attendingProviderName: DrStork,
                birthLocationName: WomensClinic);

            await wf.RecordNewbornExamAsync(newbornId, new NewbornExam
            {
                General = "Vigorous, well-appearing term newborn, appropriate for gestational age.",
                Heent = "Normocephalic; red reflex present bilaterally; palate intact.",
                Cardiac = "Regular rate and rhythm, no murmur; femoral pulses 2+ and symmetric.",
                Respiratory = "Clear and equal, unlabored.",
                Abdomen = "Soft, nontender; three-vessel cord, clamped.",
                Genitourinary = "Normal female genitalia; voided.",
                Musculoskeletal = "Hips stable (Barlow/Ortolani negative); spine intact.",
                Neurologic = "Active with normal tone; Moro, grasp, and suck intact.",
                Skin = "Pink and well-perfused; mild physiologic jaundice.",
                Impression = "Healthy term newborn — routine newborn care and screening.",
                ExaminerName = DrStork,
                ExamDate = birth.AddHours(2)
            });

            // Newborn screening — CCHD/hearing/bilirubin resulted; metabolic sent (pending).
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.CriticalCongenitalHeartDisease,
                NewbornScreeningResult.Pass, "Pre-ductal 99% / post-ductal 99%", birth.AddHours(26), "Nursery RN", "");
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.Hearing,
                NewbornScreeningResult.Pass, "OAE — pass bilaterally", birth.AddHours(28), "Audiology", "");
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.Bilirubin,
                NewbornScreeningResult.Pass, "TSB 5.8 mg/dL — low-risk zone", birth.AddHours(30), "Lab", "");
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.MetabolicBloodSpot,
                NewbornScreeningResult.Pending, "Heel-stick collected, sent to state lab", birth.AddHours(27), "Nursery RN",
                "Result expected in 5-7 days.");

            // Interval weights / feeding.
            await wf.AddNewbornMeasurementAsync(newbornId, birth, 3350, NewbornFeedingType.Breast, null, "Latching well", "Birth weight.");
            await wf.AddNewbornMeasurementAsync(newbornId, birth.AddHours(24), 3270, NewbornFeedingType.Breast, 5.8m, "Breastfeeding q2-3h", "-2.4% from birth — normal.");

            logger.LogInformation("  + maternal-newborn: pregnancy delivered + newborn {Id} (term, AGA, in nursery; metabolic screen pending)", newbornId);
            logger.LogInformation("Maternal-newborn demo patient {Id} (DELIVERED,DONNA) seeded successfully", Pid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding maternal-newborn demo patient {Id} (non-fatal)", Pid);
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
