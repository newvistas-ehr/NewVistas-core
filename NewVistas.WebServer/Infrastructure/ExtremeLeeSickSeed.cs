// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds a single rich demo patient — SICK,EXTREME LEE (grain key P9001) — with a
/// full longitudinal record: chronic low back pain, a cervical decompression
/// (admission → rehab), pain-management follow-ups, and a basal-cell-carcinoma
/// excision. Keyed at P9001 to stay clear of the imported dataset (P1..P1000).
///
/// All synthetic. Runs under the SYSTEM-SEED (XUPROG) context like the other demo
/// seeders, and is idempotent — it no-ops if the patient already exists.
/// </summary>
public static class ExtremeLeeSickSeed
{
    private const string Pid = "P9001";

    // Providers (referenced by id/name string on each clinical record).
    private const string DrCannotId = "PROV-CANNOT", DrCannot = "Dr. Cannot";       // Primary Care (Same Day)
    private const string DrNotYouId = "PROV-NOTYOU", DrNotYou = "Dr. NotYou";        // Neurosurgery / spine
    private const string DrPainId = "PROV-PAIN", DrPain = "Dr. Pain";                // Pain Management
    private const string DrFriendlyId = "PROV-FRIENDLY", DrFriendly = "Dr. Friendly"; // Dermatologic surgery

    // Facilities (free-text on the records — no separate institution file needed for the demo).
    private const string Hospital = "Little Shop of Horrors Hospital";
    private const string Rehab = "Not So Good Inpatient Rehab Center";
    private const string MghSalem = "MGH Salem";

    public static async Task SeedAsync(IGrainFactory grainFactory, ILogger logger)
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            var wf = grainFactory.GetGrain<IPatientWorkflowGrain>(Pid);

            // Idempotency guard — skip if already seeded.
            var existing = await wf.GetPatientAsync();
            if (!string.IsNullOrEmpty(existing.Name))
            {
                logger.LogInformation("Demo patient {Id} ({Name}) already exists — skipping rich seed", Pid, existing.Name);
                return;
            }

            logger.LogInformation("Seeding rich demo patient {Id} (SICK,EXTREME LEE)...", Pid);

            // ── Demographics (also registers the patient in the search index) ──────
            await wf.UpdateDemographicsAsync("SICK,EXTREME LEE", "M", new DateTime(1949, 3, 12), "666001234");
            await wf.UpdateAddressAsync("13 Mott Street", null, null, "Salem", "MA", "01970");
            await wf.UpdateContactInfoAsync("978-555-0142", null, "extreme.sick@newvistas.demo");
            await wf.UpdateMaritalStatusAsync("MARRIED");
            await wf.UpdateVeteranInfoAsync("Y", null, null, null);

            // ── Problem list ──────────────────────────────────────────────────────
            await wf.AddProblemAsync(
                "Low back pain", "M54.50", "C", "CHRONIC",
                new DateTime(2025, 10, 1), DrCannotId, DrCannot, null, null, false,
                "Constant ache 6-7/10 off meds, 2-3/10 on Lyrica + Celexa with breakthrough.");
            await wf.AddProblemAsync(
                "Cervical spinal stenosis with foraminal stenosis", "M48.02", "C", "CHRONIC",
                new DateTime(2024, 11, 1), DrNotYouId, DrNotYou, null, null, false,
                "Status post cervical decompression 01/05/2025.");
            await wf.AddProblemAsync(
                "Chronic pain syndrome", "G89.29", "C", "CHRONIC",
                new DateTime(2025, 2, 1), DrPainId, DrPain, null, null, false,
                "Managed by Pain Management (Dr. Pain).");
            await wf.AddProblemAsync(
                "Basal cell carcinoma of skin of left upper limb", "C44.612", "A", "ACUTE",
                new DateTime(2026, 5, 6), DrFriendlyId, DrFriendly, null, null, false,
                "Excised 05/20/2026 with clear margins.");

            // ── Local helpers ───────────────────────────────────────────────────────
            async Task<string> Appt(string clinicId, string clinicName, DateTime when,
                string provId, string provName, string purpose, string type) =>
                await wf.ScheduleAppointmentAsync(clinicId, clinicName, when, 30,
                    provId, provName, purpose, type, allowDoubleBook: true);

            async Task SignedNote(string docType, string text, string subject,
                string authorId, string authorName, string? locName, string? visitId, DateTime date)
            {
                string id = await wf.CreateNoteAsync(docType, null, text, subject,
                    authorId, authorName, null, null, null, locName, visitId, date);
                await wf.SignNoteAsync(id);
            }

            async Task Rx(string keySuffix, string drug, string dosage, string schedule,
                string sig, int days, int qty, int refills, string provId, string provName, DateTime start)
            {
                string rxKey = $"RX-{Pid}-{keySuffix}";
                await grainFactory.GetGrain<IPharmacyGrain>(rxKey).CreatePrescriptionAsync(
                    Pid, drug, null, dosage, "PO", schedule, sig, days, qty, refills,
                    provId, provName, null, null, null, null);
                // Link to the patient: full-history index first, then the capped recent window.
                await grainFactory.GetGrain<IPatientHistoryIndexGrain>($"{Pid}:{PatientHistoryDomains.Pharmacy}")
                    .AddEntryAsync(new HistoryRef { ItemId = rxKey, Date = start });
                await grainFactory.GetGrain<IPatientGrain>(Pid).AddPharmacyIdAsync(rxKey);
            }

            // ── Cervical decompression: admission → surgery → rehab (Jan 2025) ──────
            string adt = await wf.RecordAdmissionAsync(
                new DateTime(2025, 1, 5, 8, 0, 0), "WARD-LSH-SURG", $"{Hospital} — Surgical Ward",
                "S-12", "NEUROSURGERY", DrNotYouId, DrNotYou,
                "Cervical spinal stenosis with foraminal stenosis", "Admitted for cervical decompression.");

            string cervicalMri = await wf.OrderRadiologyStudyAsync(
                "MRI Cervical Spine", null, "72141", "MRI", DrNotYouId, DrNotYou, "ROUTINE",
                "Neck pain, evaluate for stenosis", "Preoperative evaluation", null, null, Hospital);
            await wf.CompleteRadiologyAsync(cervicalMri,
                "MRI of the cervical spine demonstrates central canal stenosis with bilateral neural foraminal narrowing.",
                "Cervical spinal stenosis and foraminal stenosis.", DrNotYouId, DrNotYou);

            string cervicalSurg = await wf.ScheduleSurgeryAsync(
                "Cervical decompression for spinal stenosis", "63045", new DateTime(2025, 1, 5, 9, 0, 0),
                DrNotYouId, DrNotYou, "General anesthesia", "Neurosurgery",
                "Cervical spinal stenosis with foraminal stenosis", "OR-LSH-1", Hospital,
                "Preoperative MRI showed cervical stenosis and foraminal stenosis.");
            await wf.CompleteSurgeryAsync(cervicalSurg,
                "Cervical decompression performed for spinal stenosis and foraminal stenosis. No immediate complication.",
                "Cervical spinal stenosis with foraminal stenosis, status post decompression.");

            await SignedNote("OPERATIVE NOTE",
                @"OPERATIVE NOTE — 01/05/2025
Surgeon: Dr. NotYou
Facility: Little Shop of Horrors Hospital

PREOPERATIVE DIAGNOSIS: Cervical spinal stenosis with foraminal stenosis
(confirmed on preoperative MRI).
PROCEDURE: Cervical decompression.
ANESTHESIA: General.

The patient underwent cervical decompression for spinal stenosis and foraminal
stenosis. Procedure without immediate complication. Admitted postoperatively for
recovery; remained inpatient 3 days and was discharged to the Not So Good Inpatient
Rehab Center.

POSTOPERATIVE DIAGNOSIS: Cervical spinal stenosis with foraminal stenosis, status
post decompression.",
                "Cervical decompression", DrNotYouId, DrNotYou, Hospital, null, new DateTime(2025, 1, 5));

            await wf.RecordDischargeAsync(adt, new DateTime(2025, 1, 8, 12, 0, 0),
                "Status post cervical decompression",
                $"REHAB — transferred to {Rehab}", "3-day length of stay; discharged to inpatient rehab.");

            // ── Cervical post-op follow-ups ─────────────────────────────────────────
            await Appt("CLINIC-NSGY", "NEUROSURGERY CLINIC", new DateTime(2025, 1, 12, 10, 0, 0),
                DrNotYouId, DrNotYou, "Postoperative wound infection — incision/drainage and antibiotics", "FOLLOW-UP");
            await Appt("CLINIC-NSGY", "NEUROSURGERY CLINIC", new DateTime(2025, 2, 5, 10, 0, 0),
                DrNotYouId, DrNotYou, "Post-op follow-up (1 month)", "FOLLOW-UP");
            await Appt("CLINIC-NSGY", "NEUROSURGERY CLINIC", new DateTime(2025, 7, 5, 10, 0, 0),
                DrNotYouId, DrNotYou, "Post-op follow-up (6 month)", "FOLLOW-UP");

            // ── Pain Management: consult + Dr. Pain meds + quarterly visits ─────────
            string painConsult = await wf.RequestConsultAsync(
                "PAIN MANAGEMENT", null, "NEUROSURGERY", null, "ROUTINE",
                DrNotYouId, DrNotYou, DrPainId, DrPain,
                "Chronic pain management following cervical decompression.", "Chronic pain syndrome",
                null, null, "Pain Management Clinic");
            await wf.AcceptConsultAsync(painConsult);
            await wf.ScheduleConsultAsync(painConsult);

            await Rx("LYRICA", "LYRICA (PREGABALIN) 150MG", "150 mg", "BID",
                "Take 1 capsule (150 mg) by mouth twice daily", 30, 60, 3, DrPainId, DrPain, new DateTime(2025, 2, 1));
            await Rx("NUCYNTA", "NUCYNTA (TAPENTADOL) 50MG", "50 mg", "TID",
                "Take 1 tablet (50 mg) by mouth three times daily", 30, 90, 0, DrPainId, DrPain, new DateTime(2025, 2, 1));
            await Rx("CELEXA", "CELEXA (CITALOPRAM) 20MG", "20 mg", "DAILY",
                "Take 1 tablet (20 mg) by mouth once daily", 30, 30, 3, DrCannotId, DrCannot, new DateTime(2025, 10, 1));

            // Every three months with Dr. Pain (six past + one upcoming).
            var painDates = new[]
            {
                new DateTime(2025, 3, 15), new DateTime(2025, 6, 15), new DateTime(2025, 9, 15),
                new DateTime(2025, 12, 15), new DateTime(2026, 3, 15), new DateTime(2026, 6, 15),
                new DateTime(2026, 9, 15),
            };
            foreach (var d in painDates)
                await Appt("CLINIC-PAIN", "PAIN MANAGEMENT CLINIC", d.AddHours(13),
                    DrPainId, DrPain, "Pain management follow-up (q3 months)", "FOLLOW-UP");

            // ── Basal cell carcinoma: biopsy → excision (May 2026) ──────────────────
            await Appt("CLINIC-DERM", "DERMATOLOGY CLINIC", new DateTime(2026, 5, 6, 11, 0, 0),
                DrFriendlyId, DrFriendly, "Left forearm skin lesion — exam and biopsy", "REGULAR");
            await SignedNote("DERMATOLOGY NOTE",
                @"DERMATOLOGY — 05/06/2026
Provider: Dr. Friendly

A lesion on the left forearm was examined and a punch biopsy was taken. Pathology is
consistent with basal cell carcinoma. Excision scheduled.",
                "Left forearm lesion — biopsy", DrFriendlyId, DrFriendly, "Dermatology Clinic", null, new DateTime(2026, 5, 6));

            string bccSurg = await wf.ScheduleSurgeryAsync(
                "Excision of basal cell carcinoma, left forearm", "11603", new DateTime(2026, 5, 20, 10, 0, 0),
                DrFriendlyId, DrFriendly, "Local anesthesia", "Dermatologic Surgery",
                "Basal cell carcinoma, left forearm", "OR-DERM-1", "Dermatology Surgery Suite",
                "Biopsy-proven basal cell carcinoma; excision with margins.");
            await wf.CompleteSurgeryAsync(bccSurg,
                "Excision of left forearm basal cell carcinoma with surrounding margin. Closed primarily.",
                "Basal cell carcinoma, left forearm — excised, margins clear.");
            await SignedNote("SURGICAL PATHOLOGY",
                @"SURGICAL PATHOLOGY — 05/20/2026
Specimen: Skin, left forearm — excision.
Clinical history: Basal cell carcinoma, biopsy-proven (biopsy 05/06/2026).
Provider: Dr. Friendly

DIAGNOSIS: Basal cell carcinoma, left forearm. Tumor completely excised; margins
clear. Cancer fully removed.",
                "Left forearm BCC — final pathology", DrFriendlyId, DrFriendly, "Dermatology Surgery Suite", null, new DateTime(2026, 5, 20));

            // ── Primary Care Same-Day visit for low back pain (Jun 16 2026) ─────────
            string pcVisit = await Appt("SD-CLINIC-001", "PRIMARY CARE", new DateTime(2026, 6, 16, 9, 0, 0),
                DrCannotId, DrCannot, "Low back pain evaluation", "SAME DAY");

            await wf.RecordVitalsAsync(null, "Primary Care", DrCannotId, DrCannot, new DateTime(2026, 6, 16, 9, 15, 0),
                new Dictionary<string, string>
                {
                    ["BLOOD PRESSURE"] = "142/86",
                    ["PULSE"] = "78",
                    ["TEMPERATURE"] = "98.2",
                    ["RESPIRATION"] = "16",
                    ["PULSE OXIMETRY"] = "97",
                    ["HEIGHT"] = "70",
                    ["WEIGHT"] = "198",
                },
                null);

            // Spinal X-ray — done that day.
            string spineXray = await wf.OrderRadiologyStudyAsync(
                "X-Ray Lumbar Spine", null, "72100", "X-RAY", DrCannotId, DrCannot, "ROUTINE",
                "Chronic low back pain", "Evaluate low back pain", null, null, "Radiology");
            await wf.CompleteRadiologyAsync(spineXray,
                "Lumbar spine radiographs show mild degenerative changes. No acute fracture or malalignment.",
                "Mild degenerative changes; no acute osseous abnormality.", DrCannotId, DrCannot);

            // Urinalysis — done that day (resulted, normal).
            string ua = await wf.OrderLabTestAsync("LOINC-24357-6", "Urinalysis, complete", "UA",
                null, DrCannotId, DrCannot, "URINE", "URINALYSIS");
            await wf.CollectSpecimenAsync(ua, new DateTime(2026, 6, 16, 9, 30, 0), "Clean catch", "Main Lab");
            await wf.RecordLabResultAsync(ua, new DateTime(2026, 6, 16, 11, 0, 0), "NEGATIVE", null, null, null, null);
            await wf.VerifyLabResultAsync(ua, DrCannotId, DrCannot, new DateTime(2026, 6, 16, 11, 30, 0));

            // Fasting routine labs — ordered, to be drawn before the August visit (left pending).
            await wf.OrderLabTestAsync("LOINC-24323-8", "Comprehensive Metabolic Panel (fasting)", "CMP",
                null, DrCannotId, DrCannot, "BLOOD", "CHEMISTRY");
            await wf.OrderLabTestAsync("LOINC-24331-1", "Lipid Panel (fasting)", "LIPID",
                null, DrCannotId, DrCannot, "BLOOD", "CHEMISTRY");

            // Physical therapy referral back to MGH Salem, focused on the back.
            await wf.RequestConsultAsync(
                "PHYSICAL THERAPY", null, "PRIMARY CARE", null, "ROUTINE",
                DrCannotId, DrCannot, null, null,
                "Chronic low back pain — PT evaluation and treatment, focused on the back this time.",
                "Low back pain", null, null, MghSalem);

            // The visit progress note (the patient's appointment summary).
            await SignedNote("PRIMARY CARE NOTE",
                @"PRIMARY CARE SAME-DAY VISIT — 06/16/2026
Provider: Dr. Cannot

CHIEF COMPLAINT: Low back pain (6-9 months duration).

HISTORY OF PRESENT ILLNESS:
Constant aching low back pain, 6-7/10 without medication, managed to 2-3/10 with
Lyrica and Celexa, though it still breaks through. Worse with standing and walking
(pain-free limit around 2 miles); better sitting or lying down but not fully relieved
even in bed.

PHYSICAL EXAM:
No tenderness on palpation of the abdomen or back. Gait observed (patient ambulating
in slippers, per wife).

ASSESSMENT:
Musculoskeletal low back pain — muscle, tendon, or ligament. Not concerned about
cancer or organ involvement given the patient's excellent PSA and clean colonoscopy.
Noted that 6-9 months of breakthrough pain despite medication is not trivial.

PLAN:
1. Spinal X-ray — done today.
2. Urinalysis — done today.
3. Fasting routine labs — to be done before the August visit.
4. Physical therapy referral back to MGH Salem, focused on the back this time.
5. VA radiological testing — patient to look into what the Navy letter was offering.",
                "Low back pain — HPI, exam, assessment, plan", DrCannotId, DrCannot, "Primary Care", pcVisit, new DateTime(2026, 6, 16));

            logger.LogInformation("Rich demo patient {Id} (SICK,EXTREME LEE) seeded successfully", Pid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error seeding rich demo patient {Id} (non-fatal)", Pid);
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }
}
