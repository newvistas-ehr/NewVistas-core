// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// Seeds a single rich demo patient — SICK,EXTREME LEE (grain key P9001) — with a
/// full longitudinal record modeled on a real, donated patient chart that has been
/// completely de-identified: the patient is renamed SICK,EXTREME LEE and every
/// person mentioned in the source (primary care, neurosurgery, pain, dermatology,
/// orthopedics, and ~20 different radiologists) is collapsed onto a small set of
/// whimsical pseudonyms (Dr. Cannot, Dr. NotYou, Dr. Pain, Dr. Friendly, Dr. Ray,
/// Dr. Newknee). Real names, MRN, SSN, address, and date of birth do NOT appear.
///
/// The clinical arc spans a decade and is faithful to the source:
///   • 2015 — left total knee arthroplasty (post-traumatic OA) and a lumbar MRI for
///     chronic low back pain; older right-tibial-plateau ORIF hardware.
///   • 2016 — renal ultrasound (nonobstructing left renal stone, left renal cyst).
///   • 2019 — CT head for headache (no acute finding).
///   • 2024 — cervical spondylotic myelopathy worked up (MRI / CT / x-ray / DXA),
///     bridged with pain-management cervical epidural steroid injections.
///   • Jan 2025 — C4–C6 decompression with C3–C6 posterior instrumented fusion;
///     post-op course complicated by hypoxia and possible aspiration pneumonia,
///     with PE and DVT ruled out (CTA chest, bilateral lower-extremity venous US),
///     then discharged to inpatient rehab.
///   • 2025–2026 — serial cervical x-rays show a stable fusion; quarterly pain
///     follow-ups; a basal-cell-carcinoma excision; and a same-day primary-care
///     visit for ongoing low back pain.
///
/// The radiology reports are the centerpiece — their findings and impressions are
/// transcribed (lightly trimmed) from the source and attributed to Dr. Ray.
///
/// Keyed at P9001 to stay clear of the imported dataset (P1..P1000). Runs under the
/// SYSTEM-SEED (XUPROG) context like the other demo seeders, and is idempotent — it
/// no-ops if the patient already exists.
/// </summary>
public static class ExtremeLeeSickSeed
{
    private const string Pid = "P9001";

    // Providers — every real clinician in the source chart maps onto one of these.
    private const string DrCannotId = "PROV-CANNOT", DrCannot = "Dr. Cannot";          // Primary Care (Internal Medicine)
    private const string DrNotYouId = "PROV-NOTYOU", DrNotYou = "Dr. NotYou";          // Neurosurgery / spine
    private const string DrPainId = "PROV-PAIN", DrPain = "Dr. Pain";                  // Pain Management
    private const string DrFriendlyId = "PROV-FRIENDLY", DrFriendly = "Dr. Friendly";  // Dermatologic surgery
    private const string DrRayId = "PROV-RAY", DrRay = "Dr. Ray";                      // Diagnostic Radiology (all radiologists)
    private const string DrNewkneeId = "PROV-NEWKNEE", DrNewknee = "Dr. Newknee";      // Orthopedic Surgery
    private const string DrGaspId = "PROV-GASP", DrGasp = "Dr. Gasp";                  // Pulmonary / Critical Care
    private const string DrGermsId = "PROV-GERMS", DrGerms = "Dr. Germs";              // Infectious Disease
    private const string NurseRatchedId = "PROV-RATCHED", NurseRatched = "Nurse Ratched"; // RN (vitals / BCMA)

    // Facilities (free-text on the records — no separate institution file needed for the demo).
    private const string Hospital = "Little Shop of Horrors Hospital";
    private const string Rehab = "Not So Good Inpatient Rehab Center";
    private const string MghSalem = "MGH Salem";
    private const string Radiology = "Diagnostic Radiology";
    private const string Srmc = "Sunshine Regional Medical Center";   // prolonged out-of-area admission (de-identified)

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

            // ── Allergy (CWAD "A") — reaction to mirtazapine documented during pain care ──
            await wf.RecordAllergyAsync(
                "MIRTAZAPINE", "DRUG", null, "O",
                new List<string> { "SWELLING", "EDEMA" }, "MODERATE",
                DrPainId, DrPain,
                "Bilateral lower-extremity swelling the week following a trial of mirtazapine.");

            // ── Problem list (chronological by onset) ───────────────────────────────
            await wf.AddProblemAsync(
                "Low back pain", "M54.50", "C", "CHRONIC",
                new DateTime(2015, 7, 1), DrCannotId, DrCannot, null, null, false,
                "Constant ache 6-7/10 off meds, 2-3/10 on Lyrica + Celexa with breakthrough.");
            await wf.AddProblemAsync(
                "Post-traumatic osteoarthritis of left knee, status post total knee arthroplasty", "M17.5", "C", "CHRONIC",
                new DateTime(2015, 4, 1), DrNewkneeId, DrNewknee, null, null, false,
                "Left TKA 04/2015 over an old proximal tibial plateau fracture (ORIF).");
            await wf.AddProblemAsync(
                "Nonobstructing left renal calculus with left renal cyst", "N20.0", "C", "CHRONIC",
                new DateTime(2016, 11, 1), DrCannotId, DrCannot, null, null, false,
                "1 cm nonobstructing left renal stone and 1.6 cm simple cyst on renal ultrasound.");
            await wf.AddProblemAsync(
                "Benign prostatic hyperplasia", "N40.0", "C", "CHRONIC",
                new DateTime(2018, 1, 1), DrCannotId, DrCannot, null, null, false, null);
            await wf.AddProblemAsync(
                "Gastroesophageal reflux disease", "K21.9", "C", "CHRONIC",
                new DateTime(2018, 1, 1), DrCannotId, DrCannot, null, null, false, "On pantoprazole.");
            await wf.AddProblemAsync(
                "Chronic constipation", "K59.00", "C", "CHRONIC",
                new DateTime(2018, 1, 1), DrCannotId, DrCannot, null, null, false, "On linaclotide (Linzess).");
            await wf.AddProblemAsync(
                "Cervical spondylotic myelopathy with multilevel stenosis and foraminal stenosis", "M47.12", "C", "CHRONIC",
                new DateTime(2024, 5, 9), DrNotYouId, DrNotYou, null, null, false,
                "MRI 05/09/2024: mild central canal stenosis C3-4 through C6-7 with significant foraminal narrowing. Status post C4-C6 decompression and C3-C6 posterior fusion 01/07/2025.");
            await wf.AddProblemAsync(
                "Osteoporosis", "M81.0", "C", "CHRONIC",
                new DateTime(2024, 11, 26), DrCannotId, DrCannot, null, null, false,
                "DXA 11/26/2024: lumbar T-score -3.1, left femoral neck T-score -3.0. On teriparatide.");
            await wf.AddProblemAsync(
                "Chronic pain syndrome", "G89.29", "C", "CHRONIC",
                new DateTime(2021, 11, 1), DrPainId, DrPain, null, null, false,
                "Managed by Pain Management (Dr. Pain); cervical epidural steroid injections and chronic opioid therapy.");
            await wf.AddProblemAsync(
                "Chronic, continuous use of opioids", "Z79.891", "C", "CHRONIC",
                new DateTime(2021, 11, 1), DrPainId, DrPain, null, null, false, null);
            await wf.AddProblemAsync(
                "Vitamin D deficiency", "E55.9", "C", "CHRONIC",
                new DateTime(2024, 4, 1), DrCannotId, DrCannot, null, null, false, null);
            await wf.AddProblemAsync(
                "History of melanoma and squamous cell carcinoma of skin", "Z85.820", "C", "CHRONIC",
                new DateTime(2020, 1, 1), DrFriendlyId, DrFriendly, null, null, false, "Surveillance dermatology.");
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

            async Task Rx(string keySuffix, string drug, string dosage, string route, string schedule,
                string sig, int days, int qty, int refills, string provId, string provName, DateTime start)
            {
                string rxKey = $"RX-{Pid}-{keySuffix}";
                await grainFactory.GetGrain<IPharmacyGrain>(rxKey).CreatePrescriptionAsync(
                    Pid, drug, null, dosage, route, schedule, sig, days, qty, refills,
                    provId, provName, null, null, null, null);
                // Link to the patient: full-history index first, then the capped recent window.
                await grainFactory.GetGrain<IPatientHistoryIndexGrain>($"{Pid}:{PatientHistoryDomains.Pharmacy}")
                    .AddEntryAsync(new HistoryRef { ItemId = rxKey, Date = start });
                await grainFactory.GetGrain<IPatientGrain>(Pid).AddPharmacyIdAsync(rxKey);
            }

            // Orders and immediately results a radiology study, attributing the read to Dr. Ray.
            // (The workflow radiology API does not take a historical order date, so the date of
            // service is carried in the clinical-history text to keep the timeline legible.)
            async Task Rad(string study, string? cpt, string modality,
                string ordProvId, string ordProvName, string clinicalHistory, string reason,
                string findings, string impression, string facility)
            {
                string id = await wf.OrderRadiologyStudyAsync(study, null, cpt, modality,
                    ordProvId, ordProvName, "ROUTINE", clinicalHistory, reason, null, null, facility);
                await wf.CompleteRadiologyAsync(id, findings, impression, DrRayId, DrRay);
            }

            // Inpatient lab: order → collect → result → verify, all in one same-day pass.
            async Task Lab(string loinc, string name, string abbr, string specimen, string category,
                DateTime when, string value, string? unit, string? flag, string provId, string provName)
            {
                string id = await wf.OrderLabTestAsync(loinc, name, abbr, null, provId, provName, specimen, category);
                await wf.CollectSpecimenAsync(id, when, "Venipuncture", $"{Srmc} Lab");
                await wf.RecordLabResultAsync(id, when.AddHours(1), value, unit, null, null, flag);
                await wf.VerifyLabResultAsync(id, provId, provName, when.AddHours(2));
            }

            // BCMA standalone medication administration (one MAR pass).
            async Task Mar(string drug, string dosage, string route, DateTime when, string? site)
            {
                await wf.RecordMedicationAdministrationAsync(drug, null, dosage, route,
                    "GIVEN", when, when, NurseRatchedId, NurseRatched, site, null, null, null);
            }

            // Inpatient vitals snapshot.
            async Task Vitals(string ward, DateTime when, string bp, string pulse, string temp, string rr, string spo2)
            {
                await wf.RecordVitalsAsync(null, ward, NurseRatchedId, NurseRatched, when,
                    new Dictionary<string, string>
                    {
                        ["BLOOD PRESSURE"] = bp,
                        ["PULSE"] = pulse,
                        ["TEMPERATURE"] = temp,
                        ["RESPIRATION"] = rr,
                        ["PULSE OXIMETRY"] = spo2,
                    },
                    null);
            }

            // ── Medications (active list) ───────────────────────────────────────────
            // Preferred outpatient pharmacy (EXTERNAL_PHARMACY enhancement): new prescriptions
            // default to CVS #4501; the provider can change it per-Rx.
            await grainFactory.GetGrain<IPatientGrain>(Pid).SetPreferredPharmacyAsync("PHARM-CVS-4501", "CVS PHARMACY #4501");
            await Rx("LYRICA", "LYRICA (PREGABALIN) 150MG", "150 mg", "PO", "BID",
                "Take 1 capsule (150 mg) by mouth twice daily", 30, 60, 3, DrPainId, DrPain, new DateTime(2025, 2, 1));
            await Rx("NUCYNTA", "NUCYNTA (TAPENTADOL) 50MG", "50 mg", "PO", "TID",
                "Take 1 tablet (50 mg) by mouth three times daily", 30, 90, 0, DrPainId, DrPain, new DateTime(2024, 6, 1));
            await Rx("CELEXA", "CELEXA (CITALOPRAM) 20MG", "20 mg", "PO", "DAILY",
                "Take 1 tablet (20 mg) by mouth once daily", 30, 30, 3, DrCannotId, DrCannot, new DateTime(2025, 10, 1));
            await Rx("FORTEO", "FORTEO (TERIPARATIDE) 20MCG/DOSE PEN", "20 mcg", "SC", "DAILY",
                "Inject 20 mcg subcutaneously once daily", 28, 1, 3, DrCannotId, DrCannot, new DateTime(2025, 1, 1));
            await Rx("VITD", "CHOLECALCIFEROL 1,000 UNIT TABLET", "4,000 unit", "PO", "DAILY",
                "Take 4 tablets (4,000 units total) by mouth once daily", 90, 360, 3, DrCannotId, DrCannot, new DateTime(2024, 4, 1));
            await Rx("LINZESS", "LINZESS (LINACLOTIDE) 290MCG CAPSULE", "290 mcg", "PO", "DAILY",
                "Take 1 capsule (290 mcg) by mouth every morning", 30, 30, 3, DrCannotId, DrCannot, new DateTime(2023, 1, 1));
            await Rx("PROTONIX", "PROTONIX (PANTOPRAZOLE) 20MG TABLET", "20 mg", "PO", "DAILY",
                "Take 1 tablet (20 mg) by mouth once daily", 90, 90, 3, DrCannotId, DrCannot, new DateTime(2022, 1, 1));

            // ════════════════════════════════════════════════════════════════════════
            //  HISTORICAL IMAGING (de-identified radiology reports, read by Dr. Ray)
            // ════════════════════════════════════════════════════════════════════════

            // 2015 — left total knee arthroplasty and chronic low back pain.
            await Rad("XR Knee Left, Portable 2 VW", "73564", "X-RAY",
                DrNewkneeId, DrNewknee, "Study performed 04/09/2015. Out-of-brace bone pain.",
                "Early postoperative left total knee arthroplasty",
                "Interval left knee arthroplasty with soft-tissue emphysema consistent with recent postoperative state. " +
                "Metallic components well aligned without perimetallic lucency. Old post-traumatic changes with abandoned " +
                "screw tracks in the proximal tibia.",
                "Normal alignment postoperatively, left total knee arthroplasty.", Radiology);
            await Rad("MRI Femur Left WO Contrast", "73718", "MRI",
                DrNewkneeId, DrNewknee, "Study performed 04/20/2015. Status post left knee arthroplasty 11 days ago; persistent thigh pain.",
                "Persistent left thigh pain after total knee arthroplasty",
                "Incomplete protocol due to patient discomfort. No femoral diaphyseal stress fracture. Extensive intramuscular " +
                "edema within the vastus muscle group, particularly the vastus lateralis. Nonspecific subcutaneous edema along " +
                "the lateral thigh.",
                "Nonspecific intramuscular and subcutaneous edema; no stress fracture.", Radiology);
            await Rad("MRI Lumbar Spine WO Contrast", "72148", "MRI",
                DrCannotId, DrCannot, "Study performed 07/31/2015. Chronic low back pain with bilateral lower-extremity pain and numbness.",
                "Chronic low back pain, rule out radiculopathy",
                "Normal alignment. Mild generalized lumbar disc desiccation with preserved disc height. Minimal tricompartment " +
                "central stenosis at L3-L4 with mild facet disease and small bilateral facet effusions. Mild symmetric facet " +
                "disease at L4-L5. Incidental 25 mm left parapelvic renal cyst.",
                "No disc extrusion, significant central stenosis, or discrete nerve-root impingement. Mild L4-L5 greater than " +
                "L3-L4 degenerative facet disease.", Radiology);

            // 2016 — renal ultrasound for gross hematuria.
            await Rad("US Renal / Retroperitoneal Complete", "76770", "ULTRASOUND",
                DrCannotId, DrCannot, "Study performed 11/25/2016. Gross hematuria.",
                "Gross hematuria, evaluate kidneys",
                "Right kidney 12.1 cm, no nephrolithiasis or hydronephrosis. Left kidney 12.3 cm with a 1.6 cm simple mid-renal " +
                "cyst and a 1 cm coarse calcification adjacent to the cyst. No urinary obstruction.",
                "1 cm nonobstructing left renal stone with simple left renal cyst.", Radiology);

            // 2018 — surveillance of the left knee arthroplasty.
            await Rad("XR Knee Template Left", "73564", "X-RAY",
                DrNewkneeId, DrNewknee, "Study performed 07/24/2018. Evaluation of flexion and extension of left total knee arthroplasty.",
                "Surveillance of left total knee arthroplasty",
                "Anatomic alignment status post left total knee arthroplasty with satisfactory placement of femoral, patellar, " +
                "and tibial components. No hardware loosening or periprosthetic fracture. Hardware tracks along the proximal " +
                "tibia from prior ORIF of a tibial fracture, with hypertrophy of the tibiofibular joint.",
                "Satisfactory left total knee arthroplasty. Hypertrophic changes of the proximal tibiofibular joint reflecting " +
                "post-traumatic osteoarthritis.", Radiology);

            // 2019 — CT head for headache.
            await Rad("CT Head WO Contrast", "70450", "CT",
                DrCannotId, DrCannot, "Study performed 01/07/2019. Headache.",
                "Acute headache",
                "No acute intracranial hemorrhage, mass, or territorial infarction. Patchy periventricular and subcortical white " +
                "matter hypodensities suggesting chronic microangiopathic changes. Dense atherosclerotic calcifications of the " +
                "carotid siphons and intradural vertebral arteries.",
                "No acute intracranial abnormality.", Radiology);

            // 2024 — cervical spondylotic myelopathy work-up (the surgical lesion).
            await Rad("MRI Cervical Spine WO Contrast", "72141", "MRI",
                DrNotYouId, DrNotYou, "Study performed 05/09/2024. Worsening neck/arm pain with radicular symptoms.",
                "Cervical radiculopathy / myelopathy, preoperative evaluation",
                "Multilevel degenerative changes with exaggerated cervical lordosis. Disc-osteophyte complexes and ligamentum " +
                "flavum thickening from C3-4 through C6-7 with mild central canal stenosis at each level. Uncovertebral spurring " +
                "with moderate-to-severe neural foraminal narrowing (right at C3-4, bilaterally at C4-5 and C5-6). Spinal cord " +
                "normal in signal. Incompletely imaged large T3 vertebral body hemangioma.",
                "Multilevel degenerative change with mild central canal stenosis C3-4 through C6-7 and significant neural " +
                "foraminal narrowing as above.", Radiology);
            await Rad("XR Cervical Spine Complete 4 or 5 VW", "72050", "X-RAY",
                DrNotYouId, DrNotYou, "Study performed 11/26/2024. Cervical radiculopathy.",
                "Cervical radiculopathy, preoperative views",
                "Lower cervical spine obscured by the shoulders on the lateral view. Multilevel degenerative disc and facet " +
                "disease, most notable at C3-C4 and C4-C5. No listhesis and no change in alignment with flexion or extension. " +
                "No acute fracture.",
                "Degenerative disc and facet disease; no acute osseous injury. Correlate with same-day CT.", Radiology);
            await Rad("CT Cervical Spine WO Contrast", "72125", "CT",
                DrNotYouId, DrNotYou, "Study performed 11/26/2024. Cervical radiculopathy, preoperative evaluation.",
                "Cervical radiculopathy, preoperative evaluation",
                "Osseous demineralization. Mild retrolisthesis of C2 on C3 and C3 on C4. Multilevel disc-osteophyte complexes, " +
                "uncovertebral spurring, and facet arthropathy with high-grade foraminal stenosis at some levels. No ossification " +
                "of the posterior longitudinal ligament. Partially visualized large T3 hemangioma.",
                "Multilevel cervical degenerative change with foraminal stenosis; canal and foraminal patency better evaluated on " +
                "the 05/09/2024 MRI.", Radiology);
            await Rad("XR DXA Bone Density, Hip and Spine", "77080", "DXA",
                DrCannotId, DrCannot, "Study performed 11/26/2024. Screening for osteoporosis.",
                "Osteoporosis screening",
                "Average bone mineral density of the upper four lumbar vertebrae 0.753 g/cm2 (T-score -3.1). Femoral neck bone " +
                "mineral density 0.521 g/cm2 (T-score -3.0).",
                "Osteoporosis of the lumbar spine and left hip.", Radiology);

            // Pain Management bridged the work-up with cervical epidural steroid injections.
            await SignedNote("PROCEDURE NOTE",
                @"CERVICAL EPIDURAL STEROID INJECTION — 06/13/2024
Provider: Dr. Pain
Facility: Pain Management Clinic

PREOPERATIVE DIAGNOSIS: Chronic cervical radiculopathy.
ANESTHESIA: Local.

Under active fluoroscopic guidance a Tuohy needle was advanced to the C7-T1
interlaminar space; the epidural space was confirmed with contrast, and 80 mg of
methylprednisolone in preservative-free saline was injected without difficulty. The
patient tolerated the procedure well with no new neurologic deficits.

COMPLICATIONS: None.",
                "Cervical epidural steroid injection", DrPainId, DrPain, "Pain Management Clinic", null, new DateTime(2024, 6, 13));
            await SignedNote("PROCEDURE NOTE",
                @"CERVICAL EPIDURAL STEROID INJECTION — 10/21/2024
Provider: Dr. Pain
Facility: Pain Management Clinic

PREOPERATIVE DIAGNOSIS: Chronic cervical radiculopathy.
ANESTHESIA: Local.

Repeat C7-T1 interlaminar epidural steroid injection performed under fluoroscopic
guidance with contrast confirmation; 80 mg of methylprednisolone injected. Procedure
tolerated well, no complications. Symptoms ultimately progressed and the patient was
referred to Neurosurgery (Dr. NotYou) for decompression and fusion.",
                "Cervical epidural steroid injection", DrPainId, DrPain, "Pain Management Clinic", null, new DateTime(2024, 10, 21));

            // ════════════════════════════════════════════════════════════════════════
            //  Jan 2025 — cervical decompression + fusion, complicated post-op course
            // ════════════════════════════════════════════════════════════════════════
            string adt = await wf.RecordAdmissionAsync(
                new DateTime(2025, 1, 7, 6, 30, 0), "WARD-LSH-SURG", $"{Hospital} — Surgical Ward",
                "S-12", "NEUROSURGERY", DrNotYouId, DrNotYou,
                "Cervical spondylotic myelopathy with multilevel stenosis and foraminal stenosis",
                "Admitted for cervical decompression and posterior spinal fusion.");

            string cervicalSurg = await wf.ScheduleSurgeryAsync(
                "C4-C6 posterior decompression with C3-C6 posterior spinal instrumented fusion", "22600",
                new DateTime(2025, 1, 7, 8, 0, 0),
                DrNotYouId, DrNotYou, "General anesthesia", "Neurosurgery",
                "Cervical spondylotic myelopathy with multilevel stenosis and foraminal stenosis",
                "OR-LSH-1", Hospital,
                "Preoperative MRI (05/09/2024) showed multilevel stenosis with foraminal narrowing.");
            await wf.CompleteSurgeryAsync(cervicalSurg,
                "C4-C6 posterior decompression performed with C3-C6 posterior instrumented fusion (rods and screws). " +
                "Intraoperative O-arm and fluoroscopy used for hardware placement. No immediate complication.",
                "Cervical spondylotic myelopathy, status post C4-C6 decompression and C3-C6 posterior fusion.");

            await SignedNote("OPERATIVE NOTE",
                @"OPERATIVE NOTE — 01/07/2025
Surgeon: Dr. NotYou
Facility: Little Shop of Horrors Hospital

PREOPERATIVE DIAGNOSIS: Cervical spondylotic myelopathy with multilevel canal and
foraminal stenosis (MRI 05/09/2024).
PROCEDURE: C4-C6 posterior decompression (laminectomy) with C3-C6 posterior spinal
instrumented fusion.
ANESTHESIA: General.

The patient underwent posterior cervical decompression from C4 to C6 with posterior
instrumented fusion C3-C6. Intraoperative O-arm and fluoroscopy confirmed hardware
position. Estimated blood loss minimal; no immediate complication. Admitted
postoperatively for recovery.

POSTOPERATIVE DIAGNOSIS: Cervical spondylotic myelopathy, status post C4-C6
decompression and C3-C6 posterior fusion.",
                "Cervical decompression and fusion", DrNotYouId, DrNotYou, Hospital, null, new DateTime(2025, 1, 7));

            // Post-op inpatient imaging — hypoxia and possible aspiration pneumonia, PE/DVT ruled out.
            await Rad("XR Cervical Spine 3 VW", "72040", "X-RAY",
                DrNotYouId, DrNotYou, "Study performed 01/08/2025. Status post cervical fusion.",
                "Immediate postoperative cervical spine",
                "Interval posterior fusion C3-C6 with intact hardware and posterior decompression. Posterior surgical drain and " +
                "perivertebral bone graft. No acute fracture; alignment unremarkable. Incidentally, increased interstitial " +
                "markings in the visualized upper lobes with a partially visualized left perihilar density.",
                "Interval posterior fusion C3-C6, expected postsurgical changes; recommend chest x-ray for the visualized lung " +
                "findings.", Radiology);
            await Rad("XR Chest 1 VW, Portable", "71045", "X-RAY",
                DrNotYouId, DrNotYou, "Study performed 01/08/2025. New oxygen requirement.",
                "Postoperative oxygen requirement",
                "Ill-defined interstitial and airspace opacities at the left lung base, slightly increased from November 2024. " +
                "Scattered lower-lung atelectasis. No pleural effusion or pneumothorax. Cervical spine hardware noted.",
                "Left basilar opacities, worrisome for basilar pneumonia.", Radiology);
            await Rad("CT Angiogram Chest, PE Protocol", "71275", "CT",
                DrNotYouId, DrNotYou, "Study performed 01/09/2025. Postoperative hypoxia and supplemental O2 requirement; clinical concern for pulmonary embolism.",
                "Clinical concern for pulmonary embolism",
                "No filling defects in the main, lobar, or segmental pulmonary arteries; no right heart strain. Atelectatic and/or " +
                "consolidative changes in the posterior right lower and upper lobes — pneumonia and aspiration are in the " +
                "differential. 5-6 mm nodule in the right middle lobe. Mild centrilobular emphysema. Moderate hiatal hernia.",
                "No evidence of pulmonary embolism. Right-lobe atelectasis/consolidation, pneumonia versus aspiration; recommend " +
                "follow-up CT in 2-3 months, at which time the right-middle-lobe nodule should be reassessed.", Radiology);
            await Rad("US Vascular Lower Extremity Venous, Bilateral", "93970", "ULTRASOUND",
                DrNotYouId, DrNotYou, "Study performed 01/10/2025. Tachycardia, increased oxygen need, suspected PE.",
                "Rule out deep vein thrombosis",
                "Normal compressibility and phasic flow of the bilateral common femoral, femoral, deep femoral, popliteal, " +
                "peroneal, and posterior tibial veins. No intraluminal thrombus.",
                "No acute deep vein thrombosis in either lower extremity.", Radiology);

            await wf.RecordDischargeAsync(adt, new DateTime(2025, 1, 11, 12, 0, 0),
                "Status post C4-C6 decompression and C3-C6 posterior fusion; resolving basilar pneumonia/aspiration",
                $"REHAB — transferred to {Rehab}",
                "Post-op course complicated by hypoxia and possible aspiration pneumonia; PE and DVT excluded. Improved on " +
                "supplemental O2 and antibiotics; discharged to inpatient rehab.");

            await SignedNote("DISCHARGE SUMMARY",
                @"DISCHARGE SUMMARY — admission 01/07/2025, discharge 01/11/2025
Attending: Dr. NotYou
Facility: Little Shop of Horrors Hospital

The patient underwent C4-C6 decompression with C3-C6 posterior instrumented fusion for
cervical spondylotic myelopathy. The postoperative course was complicated by hypoxia
and a new oxygen requirement. Chest imaging showed left basilar opacities; CT pulmonary
angiography excluded pulmonary embolism but demonstrated right-lung atelectasis and
consolidation favored to represent pneumonia versus aspiration. Bilateral lower-extremity
venous ultrasound excluded deep vein thrombosis. Troponins were negative. The patient
improved with supplemental oxygen and antibiotics and was discharged to the Not So Good
Inpatient Rehab Center in stable condition.

A 5-6 mm right-middle-lobe nodule was noted incidentally and a follow-up CT chest in
2-3 months was recommended.",
                "Cervical fusion — discharge summary", DrNotYouId, DrNotYou, Hospital, null, new DateTime(2025, 1, 11));

            // ── Cervical post-op follow-ups + serial imaging (stable fusion) ─────────
            await Appt("CLINIC-NSGY", "NEUROSURGERY CLINIC", new DateTime(2025, 2, 5, 10, 0, 0),
                DrNotYouId, DrNotYou, "Post-op follow-up (1 month)", "FOLLOW-UP");
            await Rad("XR Cervical Spine 2 or 3 VW", "72040", "X-RAY",
                DrNotYouId, DrNotYou, "Study performed 04/17/2025. Status post cervical fusion.",
                "Surveillance of cervical fusion",
                "Posterior fusion C3-C6 with intact rods and screws and no evidence of loosening or fracture. Mild degenerative " +
                "changes of the cervical spine. Alignment unremarkable.",
                "Posterior fusion C3-C6, no hardware complication. Mild degenerative changes.", Radiology);
            await Appt("CLINIC-NSGY", "NEUROSURGERY CLINIC", new DateTime(2025, 7, 5, 10, 0, 0),
                DrNotYouId, DrNotYou, "Post-op follow-up (6 month)", "FOLLOW-UP");
            await Appt("CLINIC-NSGY", "NEUROSURGERY CLINIC", new DateTime(2026, 2, 2, 10, 30, 0),
                DrNotYouId, DrNotYou, "Post-op follow-up (1 year)", "FOLLOW-UP");
            await Rad("XR Cervical Spine 2 or 3 VW", "72040", "X-RAY",
                DrNotYouId, DrNotYou, "Study performed 01/07/2026. Status post cervical fusion.",
                "Surveillance of cervical fusion (1 year)",
                "Posterior fusion C3-C6, rods and screws intact with no evidence of loosening. Grade 1 retrolisthesis of C3 on " +
                "C4. Mild disc-space narrowing and minimal osteophytosis. No acute fracture.",
                "Posterior fusion C3-C6, no hardware complication. Grade 1 retrolisthesis C3-C4. Degenerative changes.", Radiology);
            await SignedNote("NEUROSURGERY NOTE",
                @"NEUROSURGERY FOLLOW-UP — 02/02/2026
Provider: Dr. NotYou

One year status post C4-C6 decompression and C3-C6 posterior fusion for myelopathy. No
recurrence of preoperative extremity weakness. Primary complaint is bilateral hand and
forearm pain; neck pain improved after increasing Nucynta. Exam: full strength in all
groups, sensation intact, narrow-based normal gait, well-healed incision. Imaging shows
stable hardware and a likely solid fusion.

ASSESSMENT/PLAN: Doing well neurologically. The residual hand and arm pain most likely
reflects chronic cord dysfunction predating surgery and is unlikely to improve with
further neurosurgical intervention. Follow up on an as-needed basis.",
                "1-year post-op neurosurgery follow-up", DrNotYouId, DrNotYou, "Neurosurgery Clinic", null, new DateTime(2026, 2, 2));

            // ════════════════════════════════════════════════════════════════════════
            //  Aug–Sep 2025 — prolonged out-of-area hospitalization (Sunshine Regional)
            //  Severe aspiration pneumonia with septic shock and respiratory failure:
            //  Medical ICU → step-down → med-surg floor, a 35-day stay. This arc exists
            //  primarily to exercise the system with a HIGH-VOLUME patient — it generates
            //  ~270 records (daily vitals, labs, MAR administrations, progress notes,
            //  serial imaging, unit transfers, and consults). Synthetic; this episode is
            //  not present in the source chart.
            // ════════════════════════════════════════════════════════════════════════
            string F1(double v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            var srAdmit = new DateTime(2025, 8, 4);
            const string IcuWardId = "WARD-SRMC-ICU", SdWardId = "WARD-SRMC-SD", FloorWardId = "WARD-SRMC-5E";
            string IcuWard = $"{Srmc} — Medical ICU", SdWard = $"{Srmc} — Step-Down Unit", FloorWard = $"{Srmc} — Med-Surg 5E";

            string srAdt = await wf.RecordAdmissionAsync(
                srAdmit.AddHours(2.5), IcuWardId, IcuWard, "ICU-7", "CRITICAL CARE",
                DrGaspId, DrGasp,
                "Acute hypoxemic respiratory failure due to severe aspiration pneumonia with septic shock",
                "Admitted via the ED while traveling out of state; intubated for respiratory failure and started on vasopressors and broad-spectrum antibiotics.");

            await SignedNote("ICU ADMISSION NOTE",
                @"MEDICAL ICU ADMISSION — 08/04/2025
Attending: Dr. Gasp (Critical Care)
Facility: Sunshine Regional Medical Center

A 76-year-old man with cervical myelopathy (status post fusion), osteoporosis, and
chronic opioid therapy presented to the ED while traveling with several days of cough,
fever, and progressive dyspnea, and was found to be in acute hypoxemic respiratory
failure with septic shock. He was intubated in the ED and started on norepinephrine and
broad-spectrum antibiotics (vancomycin and piperacillin-tazobactam). Chest imaging shows
multifocal right pneumonia; aspiration is favored given his dysphagia history.

PLAN: Mechanical ventilation, vasopressor support, broad-spectrum antibiotics pending
cultures, ID and nutrition consults, DVT and stress-ulcer prophylaxis.",
                "ICU admission — respiratory failure and septic shock", DrGaspId, DrGasp, IcuWard, null, srAdmit);

            // Admission and early-course imaging.
            await Rad("XR Chest 1 VW, Portable", "71045", "X-RAY", DrGaspId, DrGasp,
                "Study performed 08/04/2025. Respiratory failure, post-intubation.",
                "Acute respiratory failure; ETT placement",
                "Endotracheal tube tip approximately 4 cm above the carina. Dense right middle and lower lobe airspace " +
                "opacities consistent with multifocal pneumonia/aspiration. Small right pleural effusion.",
                "Multifocal right pneumonia; ETT in satisfactory position.", Srmc);
            await Rad("CT Chest with Contrast", "71260", "CT", DrGaspId, DrGasp,
                "Study performed 08/06/2025. Severe pneumonia, evaluate for complication.",
                "Severe pneumonia, assess for empyema/abscess",
                "Multifocal consolidation of the right middle and lower lobes with air bronchograms and a moderate loculated " +
                "right pleural effusion. No drainable abscess. Mild centrilobular emphysema.",
                "Multifocal right pneumonia with parapneumonic effusion.", Srmc);
            await Rad("US Echocardiogram, Limited", "93308", "ULTRASOUND", DrGaspId, DrGasp,
                "Study performed 08/07/2025. Septic shock, hemodynamic assessment.",
                "Hemodynamic assessment in shock",
                "Preserved left ventricular systolic function, estimated ejection fraction 55%. No pericardial effusion. " +
                "Hyperdynamic state consistent with sepsis.",
                "Normal LV systolic function; findings consistent with sepsis.", Srmc);

            // Inpatient consults.
            string idConsult = await wf.RequestConsultAsync("INFECTIOUS DISEASE", null, "CRITICAL CARE", null, "URGENT",
                DrGaspId, DrGasp, DrGermsId, DrGerms,
                "Severe sepsis from multifocal pneumonia — antibiotic selection and duration.", "Sepsis", null, null, Srmc);
            await wf.AcceptConsultAsync(idConsult);
            await wf.ScheduleConsultAsync(idConsult);
            string ptConsult = await wf.RequestConsultAsync("PHYSICAL THERAPY", null, "CRITICAL CARE", null, "ROUTINE",
                DrGaspId, DrGasp, null, null,
                "ICU-acquired weakness — early mobilization and reconditioning.", "Muscle weakness", null, null, Srmc);
            await wf.AcceptConsultAsync(ptConsult);
            string nutConsult = await wf.RequestConsultAsync("NUTRITION", null, "CRITICAL CARE", null, "ROUTINE",
                DrGaspId, DrGasp, null, null,
                "Enteral nutrition while intubated; high aspiration risk.", "Aspiration", null, null, Srmc);
            await wf.AcceptConsultAsync(nutConsult);
            string spConsult = await wf.RequestConsultAsync("SPEECH PATHOLOGY", null, "CRITICAL CARE", null, "ROUTINE",
                DrGaspId, DrGasp, null, null,
                "Swallow evaluation before advancing oral diet.", "Dysphagia", null, null, Srmc);
            await wf.AcceptConsultAsync(spConsult);

            // ── 35 inpatient days: vitals, labs, MAR passes, notes, transfers ───────
            string currentMovement = srAdt;
            for (int d = 0; d < 35; d++)
            {
                DateTime day = srAdmit.AddDays(d);
                bool icu = d < 11;                 // 08/04 – 08/14
                bool stepDown = d >= 11 && d < 20; // 08/15 – 08/23
                bool floor = d >= 20;              // 08/24 – 09/07
                string ward = icu ? IcuWard : stepDown ? SdWard : FloorWard;

                // Unit transfers.
                if (d == 11)
                    currentMovement = await wf.RecordTransferAsync(currentMovement, day.AddHours(10),
                        SdWardId, SdWard, "SD-3", null, "PULMONARY", DrGaspId, DrGasp,
                        "Extubated and hemodynamically stable off pressors; transferred to step-down.");
                if (d == 20)
                    currentMovement = await wf.RecordTransferAsync(currentMovement, day.AddHours(10),
                        FloorWardId, FloorWard, "512-B", null, "HOSPITAL MEDICINE", DrCannotId, DrCannot,
                        "Off supplemental oxygen; transferred to the medical floor for continued recovery and rehab planning.");

                // Vitals — values trend toward normal as the stay progresses.
                int sbp = Math.Min(130, 95 + d);
                int hr = Math.Max(72, 118 - d);
                double temp = Math.Max(98.2, 102.4 - d * 0.12);
                int rr = Math.Max(16, 28 - d / 3);
                int spo2 = Math.Min(96, 88 + d / 2);
                await Vitals(ward, day.AddHours(6), $"{sbp}/{sbp / 2 + 10}", hr.ToString(), F1(temp), rr.ToString(), spo2.ToString());
                if (icu)
                    await Vitals(ward, day.AddHours(18), $"{sbp + 4}/{sbp / 2 + 12}", (hr + 3).ToString(), F1(temp + 0.2), (rr + 1).ToString(), (spo2 - 1).ToString());

                // Labs — daily CBC/BMP through step-down; ABG + lactate in the ICU; spot checks on the floor.
                if (d < 20)
                {
                    await Lab("LOINC-58410-2", "Complete Blood Count w/ Diff", "CBC", "BLOOD", "HEMATOLOGY",
                        day.AddHours(5), F1(Math.Max(8.0, 16.5 - d * 0.3)), "10*3/uL", d < 6 ? "H" : null, DrGaspId, DrGasp);
                    await Lab("LOINC-24323-8", "Basic Metabolic Panel", "BMP", "BLOOD", "CHEMISTRY",
                        day.AddHours(5), "Within normal limits", null, null, DrGaspId, DrGasp);
                }
                else if (d % 3 == 0)
                {
                    await Lab("LOINC-58410-2", "Complete Blood Count w/ Diff", "CBC", "BLOOD", "HEMATOLOGY",
                        day.AddHours(5), "Normalizing", "10*3/uL", null, DrCannotId, DrCannot);
                }
                if (icu)
                {
                    await Lab("LOINC-24336-0", "Arterial Blood Gas", "ABG", "ARTERIAL BLOOD", "CHEMISTRY",
                        day.AddHours(7), d < 4 ? "Hypoxemic, on ventilator" : "Improving oxygenation", null, d < 4 ? "A" : null, DrGaspId, DrGasp);
                    if (d < 6)
                        await Lab("LOINC-2524-7", "Lactate", "LACT", "BLOOD", "CHEMISTRY",
                            day.AddHours(7), F1(Math.Max(1.0, 4.2 - d * 0.5)), "mmol/L", d < 3 ? "H" : null, DrGaspId, DrGasp);
                }

                // MAR — IV antibiotics BID through day 13, then oral; DVT and ulcer prophylaxis daily.
                if (d < 14)
                {
                    await Mar("VANCOMYCIN 1,250 MG IV", "1,250 mg", "IV", day.AddHours(8), "Left forearm IV");
                    await Mar("PIPERACILLIN-TAZOBACTAM 4.5 G IV", "4.5 g", "IV", day.AddHours(8), "Left forearm IV");
                    await Mar("VANCOMYCIN 1,250 MG IV", "1,250 mg", "IV", day.AddHours(20), "Left forearm IV");
                    await Mar("PIPERACILLIN-TAZOBACTAM 4.5 G IV", "4.5 g", "IV", day.AddHours(20), "Left forearm IV");
                }
                else if (d < 21)
                {
                    await Mar("LEVOFLOXACIN 750 MG PO", "750 mg", "PO", day.AddHours(9), null);
                }
                await Mar("ENOXAPARIN 40 MG SUBCUTANEOUS", "40 mg", "SC", day.AddHours(9), "Abdomen");
                await Mar(floor ? "PANTOPRAZOLE 40 MG PO" : "PANTOPRAZOLE 40 MG IV", "40 mg", floor ? "PO" : "IV", day.AddHours(9), null);

                // Progress notes — daily in the ICU, then a couple times a week.
                if (icu || d % 4 == 0)
                {
                    string noteAuthorId = icu ? DrGaspId : DrCannotId;
                    string noteAuthor = icu ? DrGasp : DrCannot;
                    string phase = icu ? "intubated, sedation weaning, antibiotics continuing"
                        : stepDown ? "extubated and on the step-down unit, working with physical therapy"
                        : "on the medical floor, off oxygen, advancing diet and mobility";
                    await SignedNote(icu ? "CRITICAL CARE PROGRESS NOTE" : "PROGRESS NOTE",
                        $"HOSPITAL DAY {d + 1} — {day:MM/dd/yyyy}\nProvider: {noteAuthor}\nLocation: {ward}\n\n" +
                        $"The patient remains {phase}. Vital signs are trending in the right direction " +
                        $"(SpO2 {spo2}%, HR {hr}, Tmax {F1(temp)}F). Multifocal pneumonia is slowly resolving. " +
                        "Continue current plan; reassess lines, antibiotics, nutrition, and mobility daily.",
                        $"Hospital day {d + 1} progress", noteAuthorId, noteAuthor, ward, null, day.AddHours(11));
                }
            }

            // Pre-discharge chest film and discharge.
            await Rad("XR Chest 1 VW", "71045", "X-RAY", DrCannotId, DrCannot,
                "Study performed 09/05/2025. Resolving pneumonia, pre-discharge.",
                "Pre-discharge chest radiograph",
                "Marked interval improvement of the prior multifocal right airspace opacities with residual scarring and " +
                "atelectasis. Resolving right pleural effusion. No new consolidation.",
                "Resolving multifocal pneumonia.", Srmc);

            await wf.RecordDischargeAsync(currentMovement, new DateTime(2025, 9, 8, 14, 0, 0),
                "Severe aspiration pneumonia with septic shock and acute respiratory failure — resolved",
                $"Discharged to inpatient rehabilitation ({Rehab})",
                "35-day stay (ICU 11 days, then step-down and floor). Completed IV then oral antibiotics, weaned off the " +
                "ventilator and oxygen, and cleared by speech for a modified diet. Discharged to rehab for reconditioning.");

            await SignedNote("DISCHARGE SUMMARY",
                @"DISCHARGE SUMMARY — admission 08/04/2025, discharge 09/08/2025 (35 days)
Attending: Dr. Gasp (Critical Care) / Dr. Cannot (Hospital Medicine)
Facility: Sunshine Regional Medical Center

ADMISSION DIAGNOSIS: Acute hypoxemic respiratory failure due to severe aspiration
pneumonia with septic shock.

HOSPITAL COURSE: The patient required intubation and vasopressor support on admission to
the medical ICU. He was treated with vancomycin and piperacillin-tazobactam, transitioned
to oral levofloxacin, with infectious-disease guidance. He was extubated on hospital day
11 and transferred to step-down, then to the medical floor on day 20. Physical therapy,
nutrition, and speech pathology followed him throughout; a swallow study cleared him for a
modified diet. Serial chest imaging showed steady resolution of the multifocal pneumonia.

DISCHARGE DISPOSITION: Inpatient rehabilitation for reconditioning, with home services to
follow. Continue home medications; complete the oral antibiotic course; follow up with
primary care and pain management after rehab.",
                "Prolonged hospitalization — discharge summary", DrCannotId, DrCannot, FloorWard, null, new DateTime(2025, 9, 8));

            // Immunization / health-factor helpers (used by the high-volume sections below).
            async Task Imm(string name, string? cvx, DateTime when, string? series) =>
                await wf.RecordImmunizationAsync(name, cvx, when, series, null, null,
                    NurseRatchedId, NurseRatched, "Left deltoid", "IM", "0.5 mL", null, "Primary Care", null);
            async Task Hf(string name, string category, DateTime when, string? level) =>
                await wf.RecordHealthFactorAsync(name, category, when, level, null, null, "Primary Care",
                    DrCannotId, DrCannot, null);

            // ════════════════════════════════════════════════════════════════════════
            //  Sep–Oct 2025 — inpatient rehabilitation (45 days, Not So Good Rehab)
            //  Reconditioning after the prolonged ICU stay. Dense daily data: q4h vitals,
            //  a full polypharmacy MAR, weekday therapy notes, and weekly labs — this is
            //  the second big volume generator. Synthetic.
            // ════════════════════════════════════════════════════════════════════════
            var rehabAdmit = new DateTime(2025, 9, 8, 15, 0, 0);
            const string RehabWardId = "WARD-REHAB-2";
            string RehabWard = $"{Rehab} — Unit 2";
            string rehabAdt = await wf.RecordAdmissionAsync(rehabAdmit, RehabWardId, RehabWard, "R-14",
                "REHABILITATION MEDICINE", DrCannotId, DrCannot,
                "Deconditioning and ICU-acquired weakness after prolonged hospitalization",
                "Admitted for intensive PT/OT/speech therapy and reconditioning after a 35-day stay for pneumonia/sepsis.");
            await SignedNote("REHAB ADMISSION NOTE",
                @"INPATIENT REHABILITATION ADMISSION — 09/08/2025
Provider: Dr. Cannot
Facility: Not So Good Inpatient Rehab Center

Transferred directly from Sunshine Regional Medical Center after a 35-day hospitalization
for severe aspiration pneumonia with septic shock and respiratory failure. The patient is
deconditioned with ICU-acquired weakness, ambulating only short distances with a walker
and maximal assist. Goals: independent transfers and ambulation, swallow safety, and
return home. Plan: PT, OT, and speech therapy daily; continue home medications and DVT
prophylaxis; weekly labs.",
                "Rehab admission — reconditioning", DrCannotId, DrCannot, RehabWard, null, rehabAdmit.Date);

            for (int d = 0; d < 45; d++)
            {
                DateTime day = rehabAdmit.Date.AddDays(d);
                // Vitals q4h (6/day) — gently improving.
                int rsbp = Math.Min(132, 118 + d / 4);
                int rhr = Math.Max(68, 84 - d / 6);
                foreach (int h in new[] { 0, 4, 8, 12, 16, 20 })
                    await Vitals(RehabWard, day.AddHours(h + 0.5),
                        $"{rsbp}/{rsbp / 2 + 8}", (rhr + h % 5).ToString(), F1(98.2 + (h % 3) * 0.1),
                        "16", Math.Min(98, 95 + d / 10).ToString());

                // MAR — full home regimen across the day plus DVT prophylaxis (12 passes/day).
                await Mar("LYRICA (PREGABALIN) 150 MG", "150 mg", "PO", day.AddHours(8), null);
                await Mar("LYRICA (PREGABALIN) 150 MG", "150 mg", "PO", day.AddHours(20), null);
                await Mar("NUCYNTA (TAPENTADOL) 50 MG", "50 mg", "PO", day.AddHours(8), null);
                await Mar("NUCYNTA (TAPENTADOL) 50 MG", "50 mg", "PO", day.AddHours(14), null);
                await Mar("NUCYNTA (TAPENTADOL) 50 MG", "50 mg", "PO", day.AddHours(20), null);
                await Mar("CELEXA (CITALOPRAM) 20 MG", "20 mg", "PO", day.AddHours(8), null);
                await Mar("PANTOPRAZOLE 40 MG", "40 mg", "PO", day.AddHours(7), null);
                await Mar("CHOLECALCIFEROL 4,000 UNIT", "4,000 unit", "PO", day.AddHours(8), null);
                await Mar("LINZESS (LINACLOTIDE) 290 MCG", "290 mcg", "PO", day.AddHours(7), null);
                await Mar("FORTEO (TERIPARATIDE) 20 MCG", "20 mcg", "SC", day.AddHours(8), "Right thigh");
                await Mar("ENOXAPARIN 40 MG", "40 mg", "SC", day.AddHours(9), "Abdomen");
                await Mar("ACETAMINOPHEN 650 MG", "650 mg", "PO", day.AddHours(13), null);

                // Therapy notes on weekdays, rotating disciplines.
                if (d % 7 < 5)
                {
                    string disc = (d % 3 == 0) ? "PHYSICAL THERAPY NOTE"
                        : (d % 3 == 1) ? "OCCUPATIONAL THERAPY NOTE" : "SPEECH THERAPY NOTE";
                    await SignedNote(disc,
                        $"REHAB THERAPY — day {d + 1} ({day:MM/dd/yyyy})\nFacility: Not So Good Inpatient Rehab Center\n\n" +
                        "Patient participated in today's session. Endurance and strength improving; " +
                        "gait distance increasing with a rolling walker. Continue plan of care toward independence at home.",
                        $"Rehab therapy day {d + 1}", DrCannotId, DrCannot, RehabWard, null, day.AddHours(10));
                }

                // Weekly labs.
                if (d % 7 == 0)
                {
                    await Lab("LOINC-58410-2", "Complete Blood Count w/ Diff", "CBC", "BLOOD", "HEMATOLOGY",
                        day.AddHours(6), "Stable", "10*3/uL", null, DrCannotId, DrCannot);
                    await Lab("LOINC-24323-8", "Basic Metabolic Panel", "BMP", "BLOOD", "CHEMISTRY",
                        day.AddHours(6), "Within normal limits", null, null, DrCannotId, DrCannot);
                }
            }

            await Rad("Modified Barium Swallow Study", "74230", "FLUOROSCOPY", DrCannotId, DrCannot,
                "Study performed 09/12/2025. Post-extubation dysphagia evaluation.",
                "Swallow safety evaluation",
                "Mild oropharyngeal dysphagia with reduced laryngeal elevation and delayed swallow initiation. No overt " +
                "aspiration with nectar-thick consistency.",
                "Mild oropharyngeal dysphagia; nectar-thick liquids and aspiration precautions recommended.", Rehab);

            await wf.RecordDischargeAsync(rehabAdt, new DateTime(2025, 10, 23, 11, 0, 0),
                "Deconditioning and ICU-acquired weakness — improved",
                "Discharged home with home physical therapy and visiting-nurse services",
                "45-day rehabilitation stay. Regained independent transfers and ambulation with a rolling walker; " +
                "advanced to a regular diet. Discharged home with home services.");
            await SignedNote("REHAB DISCHARGE SUMMARY",
                @"REHABILITATION DISCHARGE SUMMARY — admission 09/08/2025, discharge 10/23/2025 (45 days)
Provider: Dr. Cannot
Facility: Not So Good Inpatient Rehab Center

The patient completed a 45-day course of intensive physical, occupational, and speech
therapy following a prolonged hospitalization for pneumonia and sepsis. He progressed from
maximal-assist transfers to independent ambulation with a rolling walker, and from
nectar-thick liquids to a regular diet after repeat swallow evaluation. Discharged home
with home physical therapy and visiting-nurse services. Resume outpatient primary care and
pain management.",
                "Rehab discharge summary", DrCannotId, DrCannot, RehabWard, null, new DateTime(2025, 10, 23));

            // ════════════════════════════════════════════════════════════════════════
            //  2015–2026 — a decade of outpatient routine (annual physicals, labs,
            //  vitals, immunizations, health factors). Adds longitudinal breadth so the
            //  paged-history views have years of data behind the recent window.
            // ════════════════════════════════════════════════════════════════════════
            for (int y = 2015; y <= 2026; y++)
            {
                var visit = new DateTime(y, 3, 15, 9, 0, 0);
                string apptId = await Appt("SD-CLINIC-001", "PRIMARY CARE", visit,
                    DrCannotId, DrCannot, "Annual physical exam", "REGULAR");
                await Vitals("Primary Care", visit.AddMinutes(15),
                    $"{128 + y % 7}/{76 + y % 5}", (70 + y % 9).ToString(), "98.4", "16", "98");
                await Lab("LOINC-58410-2", "Complete Blood Count w/ Diff", "CBC", "BLOOD", "HEMATOLOGY",
                    visit, "Within normal limits", null, null, DrCannotId, DrCannot);
                await Lab("LOINC-24323-8", "Comprehensive Metabolic Panel", "CMP", "BLOOD", "CHEMISTRY",
                    visit, "Within normal limits", null, null, DrCannotId, DrCannot);
                await Lab("LOINC-24331-1", "Lipid Panel", "LIPID", "BLOOD", "CHEMISTRY",
                    visit, "At goal on no statin", null, null, DrCannotId, DrCannot);
                await Lab("LOINC-4548-4", "Hemoglobin A1c", "A1C", "BLOOD", "CHEMISTRY",
                    visit, F1(5.4 + (y % 3) * 0.1), "%", null, DrCannotId, DrCannot);
                await Lab("LOINC-2857-1", "PSA Screen", "PSA", "BLOOD", "CHEMISTRY",
                    visit, F1(1.1 + (y - 2015) * 0.05), "ng/mL", null, DrCannotId, DrCannot);
                await Imm("Influenza, seasonal, injectable", "150", new DateTime(y, 10, 5, 10, 0, 0), "Annual");
                await SignedNote("ANNUAL PHYSICAL NOTE",
                    $"ANNUAL PHYSICAL EXAM — {visit:MM/dd/yyyy}\nProvider: Dr. Cannot\n\n" +
                    "Stable chronic problems reviewed and medications reconciled. Age-appropriate screening up to date. " +
                    "Routine labs and seasonal influenza vaccine ordered. Continue current plan; follow up in one year " +
                    "or sooner as needed.",
                    $"Annual physical {y}", DrCannotId, DrCannot, "Primary Care", apptId, visit);
            }

            // One-off immunizations and health factors across the decade.
            await Imm("COVID-19 mRNA vaccine", "208", new DateTime(2021, 2, 10, 10, 0, 0), "1 of 2");
            await Imm("COVID-19 mRNA vaccine", "208", new DateTime(2021, 3, 10, 10, 0, 0), "2 of 2");
            await Imm("COVID-19 mRNA booster", "229", new DateTime(2021, 11, 1, 10, 0, 0), "Booster");
            await Imm("COVID-19 bivalent booster", "300", new DateTime(2022, 10, 1, 10, 0, 0), "Booster");
            await Imm("Pneumococcal conjugate PCV20", "216", new DateTime(2024, 3, 15, 10, 0, 0), "1 of 1");
            await Imm("Zoster recombinant (Shingrix)", "187", new DateTime(2019, 5, 1, 10, 0, 0), "1 of 2");
            await Imm("Zoster recombinant (Shingrix)", "187", new DateTime(2019, 8, 1, 10, 0, 0), "2 of 2");
            await Imm("Td (tetanus, diphtheria)", "139", new DateTime(2018, 6, 1, 10, 0, 0), "Booster");
            await Hf("Tobacco - never smoker", "TOBACCO", new DateTime(2015, 3, 15), null);
            await Hf("Alcohol - occasional use", "ALCOHOL", new DateTime(2015, 3, 15), "LIGHT");
            await Hf("Chronic opioid therapy - pain agreement on file", "PAIN", new DateTime(2022, 1, 1), null);
            await Hf("Fall risk - elevated", "SAFETY", new DateTime(2025, 9, 8), "MODERATE");
            await Hf("Home exercise program - back", "ACTIVITY", new DateTime(2026, 6, 16), null);

            // ── Pain Management: consult + quarterly visits ─────────────────────────
            string painConsult = await wf.RequestConsultAsync(
                "PAIN MANAGEMENT", null, "NEUROSURGERY", null, "ROUTINE",
                DrNotYouId, DrNotYou, DrPainId, DrPain,
                "Chronic pain management following cervical decompression and fusion.", "Chronic pain syndrome",
                null, null, "Pain Management Clinic");
            await wf.AcceptConsultAsync(painConsult);
            await wf.ScheduleConsultAsync(painConsult);

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
            await Rad("XR Lumbar Spine 2 VW", "72100", "X-RAY",
                DrCannotId, DrCannot, "Study performed 06/16/2026. Chronic low back pain.",
                "Evaluate chronic low back pain",
                "Mild multilevel degenerative changes of the lumbar spine. No acute fracture or malalignment.",
                "Mild degenerative changes; no acute osseous abnormality.", Radiology);

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

            // ── Oncology: a BRAF-mutant cutaneous melanoma with a molecular profile ──
            // Ties to his documented skin-cancer history (basal-cell excision + melanoma)
            // and exercises the precision-oncology layer end-to-end: the registered tumor
            // is staged, then a BRAF V600E and a PD-L1 result drive matched targeted +
            // immunotherapy decision support on the Oncology page. (Runs under the
            // SYSTEM-SEED XUPROG context set above, so the ONCO MANAGER-gated writes pass.)
            string melanomaId = await wf.RegisterOncologyTumorAsync(
                primarySite: "C43.5",
                primarySiteText: "Malignant melanoma of trunk",
                histology: "8720/3",
                histologyText: "Malignant melanoma, NOS",
                laterality: TumorLaterality.NotApplicable,
                dateOfDiagnosis: new DateTime(2024, 8, 15),
                diagnosisBasis: DiagnosisBasis.HistologyOfPrimary,
                sequenceNumber: 1,
                oncologistId: "PROV-CURE",
                oncologistName: "Dr. Cure");

            await wf.RecordOncologyStagingAsync(
                melanomaId,
                clinicalT: "cT3b", clinicalN: "cN1", clinicalM: "cM0",
                pathologicT: "pT3b", pathologicN: "pN1a", pathologicM: "pM0",
                stageGroup: "IIIB", seerSummaryStage: "3");

            await wf.RecordTumorBiomarkerAsync(
                melanomaId, "BRAF", BiomarkerStatus.Positive, "V600E",
                BiomarkerMethod.NGS, new DateTime(2024, 9, 1), "FoundationOne CDx",
                "Activating mutation — actionable.");
            await wf.RecordTumorBiomarkerAsync(
                melanomaId, "PD-L1", BiomarkerStatus.Positive, "TPS 35%",
                BiomarkerMethod.IHC, new DateTime(2024, 9, 1), "PathGroup", null);
            await wf.RecordTumorBiomarkerAsync(
                melanomaId, "KIT", BiomarkerStatus.Negative, "wild type",
                BiomarkerMethod.NGS, new DateTime(2024, 9, 1), "FoundationOne CDx", null);

            logger.LogInformation("  + oncology: melanoma {Id} staged IIIB with molecular profile (BRAF V600E, PD-L1 35%)", melanomaId);

            // ── Home-Based Care (HBPC) ──────────────────────────────────────────────
            // Mr. Sick fits HBPC: chronic low back pain + post-cervical-fusion mobility limits +
            // melanoma surveillance, managed at home by an interdisciplinary team — mirroring his
            // real PT + visiting-nurse experience. Team-based, longitudinal (not Medicare episodic).
            string hbpcEpisodeId = await wf.AdmitToHomeCareAsync(
                HomeCareProgramType.HomeBasedPrimaryCare,
                new DateTime(2026, 2, 1),
                HomeCareAdmissionSource.AcuteHospital,
                DrCannotId, DrCannot,
                "M54.50", "Low back pain, unspecified",
                HomeCareLevelOfCare.Enhanced,
                "Chronic low back pain and post-cervical-fusion mobility limits; routine clinic visits are taxing — appropriate for team-based home management.",
                "Wife (primary caregiver)",
                "12 Shady Lane, Salem MA");

            await wf.AssignHomeCareTeamMemberAsync(hbpcEpisodeId, DrCannotId, DrCannot, HomeCareDiscipline.Physician, "HBPC Medical Director", true);
            await wf.AssignHomeCareTeamMemberAsync(hbpcEpisodeId, NurseRatchedId, NurseRatched, HomeCareDiscipline.SkilledNursing, "Primary RN", false);
            await wf.AssignHomeCareTeamMemberAsync(hbpcEpisodeId, "PROV-STRETCH", "Dr. Stretch", HomeCareDiscipline.PhysicalTherapy, "Home PT", false);

            string hbpcPlanId = await wf.CreateHomeCarePlanAsync(hbpcEpisodeId, DrCannotId, DrCannot);
            await wf.AddHomeCarePlanProblemAsync(hbpcPlanId, "Chronic low back pain", "Lumbar degenerative disease",
                new List<string> { "Pain controlled to 2-3/10 on regimen", "Walk 2 miles without breakthrough pain" },
                new List<string> { "PT home exercise program 3x/week", "Medication review (Lyrica/Celexa)" },
                HomeCareDiscipline.PhysicalTherapy);
            await wf.AddHomeCarePlanProblemAsync(hbpcPlanId, "Post-cervical-fusion mobility", "C4-C6 decompression + C3-C6 fusion (Jan 2025)",
                new List<string> { "Maintain cervical ROM and safe transfers" },
                new List<string> { "Home safety evaluation", "Assistive-device training" },
                HomeCareDiscipline.PhysicalTherapy);
            await wf.AddHomeCarePlanProblemAsync(hbpcPlanId, "Skin-cancer surveillance", "History of melanoma",
                new List<string> { "Early detection of new or changing lesions" },
                new List<string> { "Monthly skin checks by RN; report changes to oncology" },
                HomeCareDiscipline.SkilledNursing);

            await wf.RecordHomeCareAssessmentAsync(hbpcEpisodeId, NurseRatchedId, NurseRatched, new DateTime(2026, 2, 1),
                new HbpcComprehensiveAssessment
                {
                    FunctionalStatus = "Independent with ADLs; limited standing/walking tolerance (~2 miles).",
                    InstrumentalAdls = "Wife manages medications, meals, and transportation.",
                    HomeSafety = "Grab bars in bathroom; throw rugs removed. Low fall risk.",
                    CaregiverSupport = "Spouse engaged and reliable.",
                    CognitiveMentalStatus = "Alert and oriented; mood stable on Celexa.",
                    Nutrition = "Adequate intake; weight stable.",
                    MedicationReconciliation = "Reconciled: Lyrica, Celexa, PRN analgesics.",
                    FallRisk = "Low-moderate; reassess each visit.",
                    Summary = "Stable; appropriate for Enhanced HBPC with home PT + skilled nursing."
                });

            string hbpcVisit1 = await wf.ScheduleHomeVisitAsync(hbpcEpisodeId, HomeCareDiscipline.SkilledNursing,
                HomeVisitType.Initial, new DateTime(2026, 2, 1), NurseRatchedId, NurseRatched, "Admission nursing visit");
            await wf.CompleteHomeVisitAsync(hbpcVisit1, 45, "BP 138/84, HR 72, O2 96%",
                new List<string> { "Medication reconciliation", "Skin survey — no new lesions" },
                "Stable admission visit; education provided to patient and spouse.", string.Empty, new DateTime(2026, 2, 15));

            string hbpcVisit2 = await wf.ScheduleHomeVisitAsync(hbpcEpisodeId, HomeCareDiscipline.PhysicalTherapy,
                HomeVisitType.Routine, new DateTime(2026, 2, 8), "PROV-STRETCH", "Dr. Stretch", "Home PT — lumbar program");
            await wf.CompleteHomeVisitAsync(hbpcVisit2, 50, "Tolerated session well",
                new List<string> { "Lumbar stabilization exercises", "Gait training" },
                "Progressing; pain 3/10 post-session.", string.Empty, new DateTime(2026, 2, 22));

            await wf.ScheduleHomeVisitAsync(hbpcEpisodeId, HomeCareDiscipline.SkilledNursing,
                HomeVisitType.Routine, DateTime.UtcNow.AddDays(3), NurseRatchedId, NurseRatched, "Routine nursing follow-up");

            logger.LogInformation("  + home-based care: HBPC episode {Id} (3-member team, plan w/ 3 problems, comprehensive assessment, 2 completed visits + 1 upcoming)", hbpcEpisodeId);

            // ── Home Health — Medicare skilled (Phase 2) ────────────────────────────
            // A 2025 episodic Medicare skilled home-health episode after his Jan-2025 cervical
            // fusion: homebound + skilled PT need, a 60-day certification, an OASIS Start-of-Care,
            // a PDGM grouping, EVV-verified visits, a Notice of Admission + claim — then discharged
            // (goals met). Distinct from his current longitudinal HBPC episode; together they show
            // both home-care models on one familiar patient.
            string medEpisodeId = await wf.AdmitToHomeCareAsync(
                HomeCareProgramType.MedicareSkilledHomeHealth,
                new DateTime(2025, 1, 20),
                HomeCareAdmissionSource.AcuteHospital,
                DrNotYouId, DrNotYou,
                "M96.1", "Postlaminectomy syndrome, cervical region",
                HomeCareLevelOfCare.Enhanced,
                "Post-cervical-fusion; requires skilled PT and nursing in the home.",
                "Wife (primary caregiver)",
                "12 Shady Lane, Salem MA");

            await wf.SetHomeCareEligibilityAsync(medEpisodeId, true,
                "Leaving home requires a considerable and taxing effort post-fusion; ambulation limited.",
                SkilledNeedType.PhysicalTherapy);
            await wf.AddHomeCareSecondaryDiagnosisAsync(medEpisodeId, "E11.9");
            await wf.AddHomeCareSecondaryDiagnosisAsync(medEpisodeId, "I10");
            await wf.AssignHomeCareTeamMemberAsync(medEpisodeId, NurseRatchedId, NurseRatched, HomeCareDiscipline.SkilledNursing, "Skilled RN", true);
            await wf.AssignHomeCareTeamMemberAsync(medEpisodeId, "PROV-STRETCH", "Dr. Stretch", HomeCareDiscipline.PhysicalTherapy, "Home PT", false);

            string medCertId = await wf.CertifyHomeCareEpisodeAsync(medEpisodeId, DrNotYouId, DrNotYou,
                new DateTime(2025, 1, 20), new DateTime(2025, 1, 15), isRecertification: false);

            await wf.RecordOasisAsync(medEpisodeId, HomeCareAssessmentType.OasisStartOfCare, "OASIS-E2",
                new Dictionary<string, string>
                {
                    ["M1021"] = "M96.1",  // primary diagnosis
                    ["M1800"] = "1", ["M1810"] = "1", ["M1820"] = "1", ["M1830"] = "1",
                    ["M1840"] = "1", ["M1850"] = "1", ["M1860"] = "2", ["M1033"] = "1"
                },
                NurseRatchedId, NurseRatched, new DateTime(2025, 1, 21));

            // Skilled visits in the first payment period (EVV-verified on the SOC visit).
            (DateTime date, HomeCareDiscipline disc, HomeVisitType vtype)[] medVisits =
            {
                (new DateTime(2025, 1, 21), HomeCareDiscipline.SkilledNursing, HomeVisitType.Initial),
                (new DateTime(2025, 1, 24), HomeCareDiscipline.PhysicalTherapy, HomeVisitType.Routine),
                (new DateTime(2025, 1, 28), HomeCareDiscipline.SkilledNursing, HomeVisitType.Routine),
                (new DateTime(2025, 2, 4),  HomeCareDiscipline.PhysicalTherapy, HomeVisitType.Routine),
                (new DateTime(2025, 2, 11), HomeCareDiscipline.PhysicalTherapy, HomeVisitType.Routine)
            };
            bool firstMedVisit = true;
            foreach ((DateTime date, HomeCareDiscipline disc, HomeVisitType vtype) in medVisits)
            {
                bool isPt = disc == HomeCareDiscipline.PhysicalTherapy;
                string mvid = await wf.ScheduleHomeVisitAsync(medEpisodeId, disc, vtype, date,
                    isPt ? "PROV-STRETCH" : NurseRatchedId, isPt ? "Dr. Stretch" : NurseRatched, "Skilled home visit");
                if (firstMedVisit)
                {
                    await wf.CheckInHomeVisitAsync(mvid, "Patient home — 12 Shady Lane", EvvMethod.Gps);
                    await wf.CheckOutHomeVisitAsync(mvid, "Patient home — 12 Shady Lane");
                    firstMedVisit = false;
                }
                await wf.CompleteHomeVisitAsync(mvid, 45, "Stable",
                    new List<string> { isPt ? "Therapeutic exercise; gait training" : "Skilled assessment; wound/incision check" },
                    "Visit completed per plan of care.", string.Empty, null);
            }

            // Compute the PDGM grouping for the first 30-day payment period, then bill it.
            HomeCareEpisodeState medEpisode = await wf.GetHomeCareEpisodeAsync(medEpisodeId);
            CertificationPeriod medCert = medEpisode.CertificationPeriods.First();
            PaymentPeriod medPp1 = medCert.PaymentPeriods.First();
            PdgmGroupingResult medGrouping = await wf.ComputePdgmGroupingAsync(medEpisodeId, medCert.PeriodId, medPp1.PeriodId);

            await wf.SubmitHomeHealthNoticeOfAdmissionAsync(medEpisodeId, new DateTime(2025, 1, 23));
            string medClaimId = await wf.GenerateHomeHealthClaimAsync(medEpisodeId, medCert.PeriodId, medPp1.PeriodId);
            await wf.SubmitHomeHealthClaimAsync(medEpisodeId, medClaimId, new DateTime(2025, 2, 25));

            await wf.DischargeFromHomeCareAsync(medEpisodeId, new DateTime(2025, 3, 20),
                HomeCareDischargeReason.GoalsMet, "Skilled goals met; transitioned to outpatient PT.");

            logger.LogInformation("  + home health (Medicare): episode {Id} certified, OASIS scrubbed, PDGM {Hipps} ({Group}), NOA + claim, discharged", medEpisodeId, medGrouping.CaseMixGroup, medGrouping.ClinicalGrouping);

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
