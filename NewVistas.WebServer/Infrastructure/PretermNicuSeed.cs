// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds a preterm NICU demo — PRETERM,PAULA (grain key P9003) — exercising the Phase 2 NICU depth:
/// a 30-week / 1300 g (VLBW) infant in a Level III NICU with a respiratory-support timeline
/// (ventilator → CPAP), surfactant + line procedures, a neonatal problem list (RDS / prematurity /
/// jaundice / apnea), active phototherapy, and TPN + trophic feeds. Complements the term well-newborn
/// (P9002) so the nursery board shows both ends of the acuity range. Runs under SYSTEM-SEED (XUPROG);
/// idempotent.
/// </summary>
public static class PretermNicuSeed
{
    private const string Pid = "P9003";
    private const string DrStorkId = "PROV-STORK", DrStork = "Dr. Stork";
    private const string NeoId = "PROV-NEO", Neo = "Dr. Vega (Neonatology)";
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
                logger.LogInformation("Demo patient {Id} ({Name}) already exists — skipping preterm-NICU seed", Pid, existing.Name);
                return;
            }

            logger.LogInformation("Seeding preterm-NICU demo patient {Id} (PRETERM,PAULA)...", Pid);

            // ── Demographics ────────────────────────────────────────────────────────
            await wf.UpdateDemographicsAsync("PRETERM,PAULA", "F", new DateTime(1996, 9, 3), "666009003");
            await wf.UpdateAddressAsync("12 Riverside Drive", null, null, "Lawrence", "MA", "01840");
            await wf.UpdateContactInfoAsync("978-555-0177", null, "paula.preterm@newvistas.demo");
            await wf.UpdateMaritalStatusAsync("MARRIED");

            // Baby born 5 days ago — currently day-of-life 5 in the NICU.
            DateTime birth = DateTime.UtcNow.Date.AddDays(-5).AddHours(6);
            DateTime lmp = birth.AddDays(-30 * 7 - 2);     // ~30+2 weeks gestation
            DateTime edd = lmp.AddDays(280);

            // ── Pregnancy (G1P0, high risk → preterm delivery) ──────────────────────
            string pregnancyId = await wf.CreatePregnancyAsync(
                lastMenstrualPeriod: lmp,
                eddByLmp: edd,
                eddByUltrasound: edd,
                definitiveEdd: edd,
                gravida: 1, para: 0, abortions: 0, living: 0,
                riskLevel: PregnancyRiskLevel.High,
                riskFactors: new List<string> { "Preterm labor", "Preterm premature rupture of membranes (PPROM)" },
                providerId: DrStorkId, providerName: DrStork,
                locationId: null, locationName: WomensClinic,
                notes: "29 y/o G1P0; antenatal betamethasone given for lung maturity.");

            // ── Prenatal visits (24w, 28w) ──────────────────────────────────────────
            await wf.CreatePrenatalVisitAsync(pregnancyId, birth.AddDays(-44), 24, 0, 150m, 122, 80, 24m, 150,
                FetalPresentation.Cephalic, true, "Negative", "Negative", "None", null, null, null,
                DrStorkId, DrStork, "Anatomy normal; cervical length borderline — close follow-up.", birth.AddDays(-16));
            await wf.CreatePrenatalVisitAsync(pregnancyId, birth.AddDays(-16), 28, 0, 156m, 130, 84, 27m, 148,
                FetalPresentation.Cephalic, true, "Trace", "Negative", "Trace", 1m, 30, -3,
                DrStorkId, DrStork, "Threatened preterm labor; betamethasone course completed.", birth.AddDays(-2));

            // ── Delivery (30+2 by C-section) + postpartum ───────────────────────────
            await wf.RecordDeliveryAsync(pregnancyId, new DeliveryInfo
            {
                DeliveryDate = birth,
                DeliveryMethod = DeliveryMethod.CesareanPrimary,
                GestationalAgeAtDeliveryWeeks = 30,
                BirthWeightGrams = 1300,
                Apgar1Min = 5,
                Apgar5Min = 7,
                Presentation = FetalPresentation.Cephalic,
                AnesthesiaType = "Spinal",
                PerinealStatus = "N/A (cesarean)",
                EstimatedBloodLossMl = 700,
                PlacentaDelivery = "Manual",
                InfantSex = "M",
                Notes = "Primary low-transverse cesarean for non-reassuring fetal status after PPROM at 30+2."
            }, PregnancyOutcome.LiveBirth);

            await wf.RecordPostpartumAsync(pregnancyId, new PostpartumInfo
            {
                PostpartumVisitDate = birth.AddDays(2),
                BreastfeedingStatus = "Pumping for NICU",
                ContraceptiveMethod = "Deferred",
                DepressionScreeningResult = "Mild distress — NICU support engaged",
                EpdsScore = 9,
                Notes = "Mother pumping for the NICU; social work and lactation engaged."
            });

            // ── Newborn — preterm, registered from delivery ─────────────────────────
            string newbornId = await wf.RegisterNewbornFromDeliveryAsync(
                pregnancyId,
                "BABY BOY PRETERM",
                NewbornSex.Male,
                birth,
                gestationalAgeWeeks: 30, gestationalAgeDays: 2,
                deliveryMethod: DeliveryMethod.CesareanPrimary,
                birthWeightGrams: 1300,
                lengthCm: 39m,
                headCircumferenceCm: 27.5m,
                apgar1Min: 5, apgar5Min: 7, apgar10Min: 8,
                multipleBirthOrder: 1, multipleBirthTotal: 1,
                attendingProviderId: NeoId, attendingProviderName: Neo,
                birthLocationName: WomensClinic);

            // Escalate to Level III NICU.
            await wf.SetNewbornNurseryLevelAsync(newbornId, NurseryLevelOfCare.NicuLevelIII,
                "30-week preterm with respiratory distress syndrome.");

            await wf.RecordNewbornExamAsync(newbornId, new NewbornExam
            {
                General = "Preterm male infant in a servo-controlled incubator; intubated initially, now on CPAP.",
                Heent = "Anterior fontanelle soft and flat; red reflex deferred; fused eyelids opening.",
                Cardiac = "Regular rate and rhythm; soft systolic murmur — echo ordered to rule out PDA.",
                Respiratory = "Mild subcostal retractions on CPAP; breath sounds equal after surfactant.",
                Abdomen = "Soft, non-distended; UVC and UAC in place; bowel sounds present.",
                Genitourinary = "Normal preterm male genitalia; testes in canal.",
                Musculoskeletal = "Preterm tone; symmetric movement of all extremities.",
                Neurologic = "Appropriate for 30 weeks; head-ultrasound screening scheduled.",
                Skin = "Thin, gelatinous preterm skin; mild jaundice.",
                Impression = "30-week preterm, RDS — surfactant-treated, on CPAP; on TPN and trophic feeds.",
                ExaminerName = Neo,
                ExamDate = birth.AddHours(3)
            });

            // ── Newborn screening (preterm — several deferred / pending) ────────────
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.Bilirubin,
                NewbornScreeningResult.ReferOrFail, "TSB 9.5 mg/dL — above phototherapy threshold for age/GA",
                birth.AddDays(2), "Lab", "Phototherapy started.");
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.CriticalCongenitalHeartDisease,
                NewbornScreeningResult.NotDone, "Deferred — on respiratory support; echo pending", birth.AddDays(1), "Nursery RN",
                "CCHD pulse-ox screen deferred until off support.");
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.Hearing,
                NewbornScreeningResult.Pending, "ABR scheduled prior to discharge", null, "Audiology", "");
            await wf.RecordNewbornScreeningAsync(newbornId, NewbornScreeningType.MetabolicBloodSpot,
                NewbornScreeningResult.Pending, "Initial heel-stick sent; preterm repeat per protocol", birth.AddHours(28), "Nursery RN",
                "Repeat at 2 weeks per preterm protocol.");

            // ── Respiratory-support timeline: ventilator → CPAP ─────────────────────
            await wf.RecordNewbornRespiratorySupportAsync(newbornId, RespiratorySupportType.ConventionalVentilation,
                40, "SIMV rate 30, PIP 18 / PEEP 5", birth.AddMinutes(20), "Intubated in delivery room for RDS.");
            await wf.RecordNewbornRespiratorySupportAsync(newbornId, RespiratorySupportType.Cpap,
                25, "CPAP +6", birth.AddDays(2), "Extubated to bubble CPAP after surfactant; weaning FiO2.");

            // ── Procedures ──────────────────────────────────────────────────────────
            await wf.RecordNewbornProcedureAsync(newbornId, NeonatalProcedureType.Intubation, birth.AddMinutes(10), Neo,
                "2.5 ETT placed in delivery room; confirmed by CO2 and CXR.");
            await wf.RecordNewbornProcedureAsync(newbornId, NeonatalProcedureType.SurfactantAdministration, birth.AddMinutes(35), Neo,
                "Poractant alfa 2.5 mL/kg via ETT for RDS.");
            await wf.RecordNewbornProcedureAsync(newbornId, NeonatalProcedureType.UmbilicalVenousCatheter, birth.AddHours(1), Neo,
                "UVC placed; tip at IVC/RA junction confirmed on CXR.");
            await wf.RecordNewbornProcedureAsync(newbornId, NeonatalProcedureType.UmbilicalArterialCatheter, birth.AddHours(1), Neo,
                "UAC placed for ABG sampling and blood-pressure monitoring.");

            // ── Problem list ────────────────────────────────────────────────────────
            await wf.AddNewbornProblemAsync(newbornId, "Respiratory distress syndrome of newborn", "P22.0",
                birth, "Surfactant-treated; on CPAP.");
            await wf.AddNewbornProblemAsync(newbornId, "Preterm newborn, gestational age 30 completed weeks", "P07.33",
                birth, "VLBW 1300 g; AGA.");
            await wf.AddNewbornProblemAsync(newbornId, "Neonatal jaundice associated with preterm delivery", "P59.0",
                birth.AddDays(2), "On double phototherapy.");
            await wf.AddNewbornProblemAsync(newbornId, "Apnea of prematurity (central)", "P28.41",
                birth.AddDays(1), "On caffeine citrate.");

            // ── Phototherapy (active) ───────────────────────────────────────────────
            await wf.StartNewbornPhototherapyAsync(newbornId, PhototherapyIntensity.Double,
                "Hyperbilirubinemia of prematurity — TSB above threshold", 9.5m, birth.AddDays(2),
                "Double phototherapy; recheck TSB q12h.");

            // ── Nutrition: starter TPN → advancing TPN + trophic feeds ──────────────
            await wf.RecordNewbornNutritionAsync(newbornId, birth.AddHours(4), NeonatalNutritionRoute.Tpn, 80,
                "TPN via UVC: dextrose 10%, amino acids 3 g/kg, lipids 2 g/kg", "Day-1 starter TPN.");
            await wf.RecordNewbornNutritionAsync(newbornId, birth.AddDays(2), NeonatalNutritionRoute.Mixed, 120,
                "TPN advancing (D12.5%, AA 3.5, lipids 3) + trophic EBM 15 mL/kg/day via OG tube", "Trophic feeds started.");

            // ── Interval weights / feeding / bilirubin ──────────────────────────────
            await wf.AddNewbornMeasurementAsync(newbornId, birth, 1300, NewbornFeedingType.IvTpn, null, "NPO at birth", "Birth weight (VLBW).");
            await wf.AddNewbornMeasurementAsync(newbornId, birth.AddDays(2), 1235, NewbornFeedingType.IvTpn, 9.5m, "TPN + trophic feeds", "Expected diuresis (-5%).");
            await wf.AddNewbornMeasurementAsync(newbornId, birth.AddDays(5), 1290, NewbornFeedingType.Mixed, 7.2m, "Advancing enteral feeds", "Regaining toward birth weight; bili improving on phototherapy.");

            logger.LogInformation("  + preterm-NICU: newborn {Id} (30+2, VLBW 1300 g, NICU III; vent→CPAP, surfactant + UVC/UAC, RDS/jaundice/apnea, phototherapy, TPN)", newbornId);
            logger.LogInformation("Preterm-NICU demo patient {Id} (PRETERM,PAULA) seeded successfully", Pid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding preterm-NICU demo patient {Id} (non-fatal)", Pid);
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
