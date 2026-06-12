// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Security;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient Workflow Grain — orchestrates VistA CPRS-style patient workflows.
///
/// Derived from MUMPS routines:
///   ORWCV.m   — Cover sheet background build (PROB, CWAD, MEDS, RMND, LABS, VITL, VSIT)
///   ORWPT.m   — Patient selection and identifying info
///   ORWDX.m   — Order dialog entry/save
///   ORWDXA.m  — Order actions (hold, unhold, DC, sign, verify)
///   ORWORR.m  — Order retrieval with status filters
///   GMPLSAVE.m/GMPLEDIT.m — Problem list save and edit with audit
///   SDAMEVT.m/SDAM2.m — Appointment events, check-in/check-out
///   TIUSRVN.m/TIUSRVL.m/TIUSRVP.m — TIU note create/list/get
///   GMRCACTM.m — Consult request, accept, schedule, complete
///
/// This grain acts as the single workflow coordinator for all UI clients
/// (VT100 character UI, Blazor, API consumers). Both Orleans clients and
/// API controllers delegate to this grain — no business logic in the UI layer.
/// </summary>
public interface IPatientWorkflowGrain : IGrainWithStringKey
{
    // ─── Cover Sheet (ORWCV.m START/BUILD/POLL) ──────────────────────────

    Task<GrainStates.CoverSheetState> GetCoverSheetAsync();

    // ─── Patient Demographics (DPT file #2, ORWPT.m SELECT) ─────────────

    Task<GrainStates.PatientState> GetPatientAsync();
    Task<GrainStates.PatientDemographicsSummary> GetPatientInfoAsync();
    Task UpdateDemographicsAsync(string name, string sex, DateTime? dateOfBirth, string? socialSecurityNumber);
    Task UpdateAddressAsync(string? streetAddress1, string? streetAddress2, string? streetAddress3, string? city, string? state, string? zipCode);
    Task UpdateContactInfoAsync(string? phoneResidence, string? phoneWork, string? email);
    Task UpdateEmergencyContactAsync(string? name, string? relationship, string? phone);
    Task UpdateVeteranInfoAsync(string veteran, int? serviceConnectedPercentage, string? eligibilityCode, string? primaryEligibilityCode);
    Task UpdateMilitaryServiceAsync(DateTime? serviceEntryDate, DateTime? serviceSeparationDate, string? serviceBranch, string? dischargeType, string? prisonerOfWar);
    Task UpdateMaritalStatusAsync(string? maritalStatus);

    // ─── Patient Identity — DFN / ICN (ORWPT.m, MPIF001.m GETICN) ───────

    /// <summary>
    /// Sets the VistA DFN for this patient and updates the patient search index.
    /// Called at local registration when a PATIENT file (#2) IEN is assigned.
    /// </summary>
    Task SetDfnAsync(string dfn);

    /// <summary>
    /// Sets the national ICN for this patient and updates the patient search index.
    /// Called when VA MPI correlation completes (MPIF001.m GETICN).
    /// </summary>
    Task SetIcnAsync(string icn);

    /// <summary>
    /// Searches all patients in the system-wide patient index.
    /// Mirrors ORWPT LOOKUP: name-prefix, SSN last-4 (4 digits), or DFN (numeric).
    /// </summary>
    Task<List<GrainStates.PatientIndexEntry>> SearchPatientsAsync(string searchTerm, int maxResults = 25);

    // ─── Order Entry Workflow (ORWDX.m SAVE, ORWDXA.m) ───────────────────

    [RequiresSecurityKey(SecurityKeys.ORES, SecurityKeys.ORELSE)]
    [AuditAction("ORDERS", "CREATE", EntityType = "ORDER", IsClinicalWrite = true)]
    Task<string> PlaceOrderAsync(
        string orderType, string orderText, string? orderableItemId,
        string providerId, string providerName,
        string? locationId, string? locationName,
        string urgency, string? instructions, string? indication);
    [RequiresSecurityKey(SecurityKeys.ORES)]
    [AuditAction("ORDERS", "SIGN", EntityType = "ORDER", IsClinicalWrite = true)]
    Task SignOrderAsync(string orderId, string electronicSignature);
    [RequiresSecurityKey(SecurityKeys.ORES, SecurityKeys.ORELSE)]
    [AuditAction("ORDERS", "DISCONTINUE", EntityType = "ORDER", IsClinicalWrite = true)]
    Task DiscontinueOrderAsync(string orderId, string reason);
    [RequiresSecurityKey(SecurityKeys.ORES, SecurityKeys.ORELSE)]
    [AuditAction("ORDERS", "HOLD", EntityType = "ORDER", IsClinicalWrite = true)]
    Task HoldOrderAsync(string orderId);
    [RequiresSecurityKey(SecurityKeys.ORES, SecurityKeys.ORELSE)]
    [AuditAction("ORDERS", "RELEASE", EntityType = "ORDER", IsClinicalWrite = true)]
    Task ReleaseOrderAsync(string orderId);
    Task<List<GrainStates.OrderSummary>> GetOrdersByFilterAsync(int filter);

    /// <summary>
    /// Gets the recent orders cache from the patient grain — zero fan-out.
    /// Returns the last N orders placed (N = site parameter OrdersDisplayCount).
    /// </summary>
    Task<List<GrainStates.OrderSummary>> GetRecentOrdersAsync();

    /// <summary>
    /// Gets order history from the index grain with date range filtering.
    /// This is the "load more" path — reads from index metadata, no grain fan-out.
    /// </summary>
    Task<List<GrainStates.OrderSummary>> GetOrderHistoryAsync(DateTime? from, DateTime? to, int maxCount);

    /// <summary>
    /// Gets a single order's full state. Mirrors ORWORR GET4LST.
    /// </summary>
    Task<GrainStates.OrderState> GetOrderDetailAsync(string orderId);

    /// <summary>
    /// Renews an existing order. Mirrors ORWDXA RENEW.
    /// </summary>
    Task RenewOrderAsync(string orderId, string renewedByProviderId, DateTime? newStopDateTime);

    /// <summary>
    /// Nurse verification of an order. Mirrors ORWDXA VERIFY.
    /// </summary>
    Task VerifyOrderAsync(string orderId, string nurseId);

    /// <summary>
    /// Checks a proposed order for clinical warnings (drug-allergy, duplicate, drug-drug).
    /// Mirrors ORWDXC DISPLAY (order checking at time of entry).
    /// </summary>
    Task<List<GrainStates.OrderCheckResult>> CheckOrderAsync(
        string orderType, string orderText, string? orderableItemId);

    /// <summary>
    /// Executes an order set — places all selected orders from the template.
    /// Mirrors ORWDXM DLGDEF (loading order set dialog and filing selected items).
    /// Returns list of created order IDs.
    /// </summary>
    Task<List<string>> ExecuteOrderSetAsync(
        string orderSetId,
        string providerId, string providerName,
        string? locationId, string? locationName,
        List<string>? selectedTemplateIds);

    // ─── Problem List Workflow (GMPLSAVE.m EN, GMPLEDIT.m) ───────────────

    [RequiresSecurityKey(SecurityKeys.GMPL_PROBLEM)]
    [AuditAction("PROBLEMS", "CREATE", EntityType = "PROBLEM", IsClinicalWrite = true)]
    Task<string> AddProblemAsync(
        string diagnosis, string? diagnosisCode, string? condition,
        string? priority, DateTime? dateOfOnset,
        string? providerId, string? providerName,
        string? clinicId, string? clinicName,
        bool isServiceConnected, string? comments);
    Task<List<GrainStates.ProblemSummary>> GetActiveProblemsAsync();
    Task<List<GrainStates.ProblemSummary>> GetAllProblemsAsync();
    [RequiresSecurityKey(SecurityKeys.GMPL_PROBLEM)]
    [AuditAction("PROBLEMS", "UPDATE", EntityType = "PROBLEM", IsClinicalWrite = true)]
    Task InactivateProblemAsync(string problemId, DateTime dateResolved);

    // ─── Appointment/Check-In Workflow (SDAM2.m, SDAMEVT.m) ──────────────

    [RequiresSecurityKey(SecurityKeys.SD_SCHEDULING)]
    [AuditAction("SCHEDULING", "CREATE", EntityType = "APPOINTMENT", IsClinicalWrite = true)]
    Task<string> ScheduleAppointmentAsync(
        string clinicId, string clinicName, DateTime appointmentDateTime,
        int durationMinutes, string? providerId, string? providerName,
        string? purpose, string? appointmentType, bool allowDoubleBook = false);
    Task CheckInAsync(string appointmentId, DateTime? checkInTime);
    Task CheckOutAsync(string appointmentId, DateTime? checkOutTime);
    Task CancelAppointmentAsync(string appointmentId);
    Task NoShowAppointmentAsync(string appointmentId);
    Task<List<GrainStates.VisitSummary>> GetUpcomingAppointmentsAsync();
    Task<GrainStates.AppointmentState> GetAppointmentAsync(string appointmentId);
    Task RescheduleAppointmentAsync(string appointmentId, DateTime newDateTime, string? reason, string? modifiedBy);
    Task<List<GrainStates.AppointmentEntry>> GetAllAppointmentsAsync(int max = 50);
    Task<List<GrainStates.ClinicEntry>> GetClinicListAsync();

    /// <summary>
    /// Returns available appointment slots for a clinic on a given date.
    /// Generates time slots from 0800-1700 based on clinic's AppointmentLength,
    /// marking each as available or booked. Mirrors SDBUILD.m availability grid.
    /// </summary>
    Task<List<GrainStates.AvailableSlot>> GetAvailableSlotsAsync(string clinicId, DateTime date);

    /// <summary>
    /// Returns daily capacity summary for a clinic including booked count,
    /// remaining slots, overbooking status, and the full slot grid.
    /// </summary>
    Task<GrainStates.ClinicDailyCapacity> GetClinicDailyCapacityAsync(string clinicId, DateTime date);

    // ─── Provider Availability (SD File #44.005, #44.002) ───────────────

    /// <summary>
    /// Returns available appointment slots for a clinic on a given date,
    /// filtered by a specific provider's availability windows and time blocks.
    /// If providerId is null, falls back to the clinic-wide 8-17 grid.
    /// VistA reference: SDBUILD.m + SD Clinic Availability (#44.005).
    /// </summary>
    Task<List<GrainStates.AvailableSlot>> GetProviderAvailableSlotsAsync(
        string clinicId, DateTime date, string? providerId);

    /// <summary>
    /// Returns daily capacity for a clinic filtered by a specific provider's availability.
    /// </summary>
    Task<GrainStates.ClinicDailyCapacity> GetProviderClinicDailyCapacityAsync(
        string clinicId, DateTime date, string? providerId);

    /// <summary>
    /// Returns available slots visible to patient self-scheduling (PATIENT tier only).
    /// Enforces min/max days ahead and allowed appointment types from tier config.
    /// Used by Patient Portal.
    /// </summary>
    Task<List<GrainStates.AvailableSlot>> GetPatientSchedulableSlotsAsync(
        string clinicId, DateTime date, string? providerId);

    // ─── Cancellation with reason (supports batch operations) ───────────

    /// <summary>
    /// Cancels an appointment with a specific reason and cancelling entity.
    /// Used by batch operations (provider unavailability) and patient portal cancellations.
    /// </summary>
    Task CancelAppointmentWithReasonAsync(string appointmentId, string reason, string cancelledBy);

    /// <summary>
    /// Reassigns an appointment to a different provider without changing date/time.
    /// Used when the original provider becomes unavailable.
    /// </summary>
    Task ReassignAppointmentProviderAsync(
        string appointmentId, string newProviderId, string newProviderName, string? reason);

    // ─── Patient Portal Scheduling ──────────────────────────────────────

    /// <summary>
    /// Patient-initiated appointment scheduling. Enforces eligibility gate,
    /// patient-bookable slot tier check, and capacity validation.
    /// The initiator is recorded as "PATIENT:{patientId}" for audit distinction.
    /// </summary>
    Task<string> PatientSelfScheduleAppointmentAsync(
        string clinicId, DateTime appointmentDateTime, string? purpose, string? appointmentType);

    /// <summary>
    /// Patient-initiated appointment cancellation with policy enforcement.
    /// Returns a CancellationPolicyResult indicating success or policy violation.
    /// Appointments within the cancellation notice window are flagged but still allowed.
    /// </summary>
    Task<GrainStates.CancellationPolicyResult> PatientCancelAppointmentAsync(
        string appointmentId, string? reason);

    /// <summary>
    /// Patient-initiated reschedule. Validates the new slot, enforces cancellation
    /// policy on the original slot, then books the new one.
    /// </summary>
    Task PatientRescheduleAppointmentAsync(
        string appointmentId, DateTime newDateTime, string? reason);

    /// <summary>
    /// Patient-initiated waitlist join. Priority is always ROUTINE for patient-initiated entries.
    /// </summary>
    Task<GrainStates.AppointmentWaitListState> PatientJoinWaitListAsync(
        string clinicId, string desiredAppointmentType, string? preferredProviderId,
        DateTime? desiredDateRangeStart, DateTime? desiredDateRangeEnd, string? comments);

    /// <summary>
    /// Get available clinics that permit patient self-scheduling.
    /// Filters the clinic list to only those with the patient-bookable flag.
    /// </summary>
    Task<List<GrainStates.ClinicEntry>> GetPatientBookableClinicsAsync();

    /// <summary>
    /// Get available slots for a clinic, filtered to patient-bookable tier only.
    /// </summary>
    Task<List<GrainStates.AvailableSlot>> GetPatientBookableSlotsAsync(string clinicId, DateTime date);

    /// <summary>
    /// Get all appointments with full details (enriched from AppointmentGrain state).
    /// Returns full appointment data suitable for patient portal display.
    /// </summary>
    Task<List<GrainStates.AppointmentState>> GetAppointmentsWithDetailsAsync(int max = 50);

    /// <summary>
    /// Checks whether this patient is eligible for scheduling based on enrollment status,
    /// means test completion, and termination status.
    /// VistA reference: DG eligibility checks (DGENELA.m, DGENELB.m).
    /// </summary>
    Task<GrainStates.PatientEligibilityResult> CheckPatientEligibilityForSchedulingAsync();

    /// <summary>
    /// Generates appointment confirmation or reminder letter content.
    /// Enriches with patient demographics and clinic details.
    /// </summary>
    Task<GrainStates.AppointmentLetterContent> GenerateAppointmentLetterAsync(
        string appointmentId, string letterType);

    /// <summary>
    /// Processes a batch of appointment reminders. Finds upcoming appointments
    /// within daysAhead that haven't had reminders sent, marks them as sent.
    /// Returns batch result with counts and per-entry details.
    /// </summary>
    Task<GrainStates.ReminderBatchResult> ProcessReminderBatchAsync(int daysAhead);

    /// <summary>
    /// Returns upcoming appointments within daysAhead window that need reminders
    /// (Scheduled status, ReminderSent == false).
    /// </summary>
    Task<List<GrainStates.AppointmentEntry>> GetAppointmentsNeedingRemindersAsync(int daysAhead);

    // ─── Vitals Workflow (GMRVFILE.m, GMRVED*.m) ─────────────────────────

    [RequiresSecurityKey(SecurityKeys.GMRV_VITALS)]
    [AuditAction("VITALS", "CREATE", EntityType = "VITAL", IsClinicalWrite = true)]
    Task RecordVitalsAsync(
        string? locationId, string? locationName,
        string? enteredById, string? enteredByName,
        DateTime dateTimeTaken,
        Dictionary<string, string> vitals,
        Dictionary<string, List<string>>? qualifiers);
    Task<List<GrainStates.VitalSummary>> GetLatestVitalsAsync();

    /// <summary>
    /// Gets vital history from the index grain with date range filtering.
    /// This is the "load more" path — fans out only for the requested slice.
    /// </summary>
    Task<List<GrainStates.VitalSummary>> GetVitalHistoryAsync(DateTime? from, DateTime? to, int maxCount);

    /// <summary>
    /// Gets vital history filtered by vital type and date range.
    /// Useful for trending a specific vital (e.g., all BP readings for the last year).
    /// </summary>
    Task<List<GrainStates.VitalSummary>> GetVitalHistoryByTypeAsync(
        string vitalType, DateTime from, DateTime to);

    // ─── Medication Workflow (Pharmacy + BCMA) ───────────────────────────

    Task<List<GrainStates.MedicationSummary>> GetActiveMedicationsAsync();

    /// <summary>
    /// Paged full medication history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.MedicationSummary>> GetMedicationHistoryAsync(int offset, int maxResults);

    // ─── Allergy Workflow (CWAD "A" flag) ────────────────────────────────

    [RequiresSecurityKey(SecurityKeys.GMRA_ALLERGY)]
    [AuditAction("ALLERGIES", "CREATE", EntityType = "ALLERGY", IsClinicalWrite = true)]
    Task<string> RecordAllergyAsync(
        string allergen, string allergenType, string? reactantId,
        string? observedHistorical, List<string>? reactions,
        string? severity, string? originatorId, string? originatorName,
        string? comments);
    Task<List<GrainStates.AllergySummary>> GetAllergiesAsync();

    // ─── Lab Orders (LR file #63, LRWU.m / LRFN.m / LRVER1.m) ────────────

    [RequiresSecurityKey(SecurityKeys.ORES, SecurityKeys.ORELSE)]
    [AuditAction("LABS", "CREATE", EntityType = "LAB_ORDER", IsClinicalWrite = true)]
    Task<string> OrderLabTestAsync(
        string testId, string testName, string? testCode,
        string? orderId, string? orderingProviderId,
        string? orderingProviderName, string? specimenType, string? category);
    Task<List<GrainStates.LabResultSummary>> GetLabResultsAsync();

    /// <summary>
    /// Paged full lab result history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.LabResultSummary>> GetLabResultHistoryAsync(int offset, int maxResults);

    /// <summary>Get the full state of a single lab test order.</summary>
    Task<GrainStates.LabTestState> GetLabTestAsync(string labTestId);

    /// <summary>Record specimen collection for an existing lab order. LRFN COLLECT.</summary>
    [RequiresSecurityKey(SecurityKeys.LRLAB)]
    [AuditAction("LABS", "UPDATE", EntityType = "LAB_ORDER", IsClinicalWrite = true)]
    Task CollectSpecimenAsync(
        string labTestId,
        DateTime collectionDateTime,
        string? collectionSample,
        string? performingLab);

    /// <summary>Record result values for a collected specimen. LRVER1 RESULT.</summary>
    [RequiresSecurityKey(SecurityKeys.LRLAB)]
    [AuditAction("LABS", "UPDATE", EntityType = "LAB_RESULT", IsClinicalWrite = true)]
    Task RecordLabResultAsync(
        string labTestId,
        DateTime resultDateTime,
        string resultValue,
        string? resultUnit,
        string? referenceLow,
        string? referenceHigh,
        string? abnormalFlag);

    /// <summary>Verify a completed lab result. LRVER1 VERIFY.</summary>
    [RequiresSecurityKey(SecurityKeys.LRVERIFY)]
    [AuditAction("LABS", "VERIFY", EntityType = "LAB_RESULT", IsClinicalWrite = true)]
    Task VerifyLabResultAsync(
        string labTestId,
        string verifyingProviderId,
        string verifyingProviderName,
        DateTime verifiedDateTime);

    /// <summary>
    /// Return the most recent result per test type for this patient.
    /// Reads the PatientLabSummary grain — single-grain read, always fast.
    /// </summary>
    Task<List<GrainStates.LabTestSummaryEntry>> GetLabSummaryAsync();

    /// <summary>Return all test types where the most recent result is flagged abnormal.</summary>
    Task<List<GrainStates.LabTestSummaryEntry>> GetAbnormalLabResultsAsync();

    /// <summary>Return the last N results for a specific test type (LOINC code) from the index.</summary>
    Task<List<GrainStates.LabIndexEntry>> GetRecentResultsByTypeAsync(string loincCode, int n);

    /// <summary>
    /// Ingest a result via the LabResultIngestion grain — writes to batch, updates summary,
    /// and publishes to the LabStreams stream for index maintenance.
    /// </summary>
    Task IngestLabResultAsync(
        string resultId,
        string loincCode,
        string testName,
        string value,
        string units,
        string? referenceRange,
        GrainStates.LabAbnormalFlag abnormalFlag,
        DateTimeOffset resultDate,
        string facilityCode,
        string? facilityName,
        string? specimen,
        string? panelName);

    // ─── TIU Notes (TIUSRVN.m, TIUSRVL.m, TIUSRVP.m) ───────────────────

    [RequiresSecurityKey(SecurityKeys.PROVIDER)]
    [AuditAction("NOTES", "CREATE", EntityType = "NOTE", IsClinicalWrite = true)]
    Task<string> CreateNoteAsync(
        string documentType, string? documentTypeId,
        string reportText, string? subject,
        string? authorId, string? authorName,
        string? cosignerId, string? cosignerName,
        string? locationId, string? locationName,
        string? visitId, DateTime referenceDate);
    [RequiresSecurityKey(SecurityKeys.TIU_SIGN)]
    [AuditAction("NOTES", "SIGN", EntityType = "NOTE", IsClinicalWrite = true)]
    Task SignNoteAsync(string documentId);
    [RequiresSecurityKey(SecurityKeys.TIU_COSIGN)]
    [AuditAction("NOTES", "COSIGN", EntityType = "NOTE", IsClinicalWrite = true)]
    Task CosignNoteAsync(string documentId);
    Task AmendNoteAsync(string documentId, string amendedText);
    Task<string> AddAddendumAsync(
        string parentDocumentId, string reportText,
        string? authorId, string? authorName,
        DateTime referenceDate);
    Task<GrainStates.TiuDocumentState> GetNoteAsync(string documentId);
    Task<List<GrainStates.TiuNoteSummary>> GetNotesAsync(string? documentType, int maxResults);

    /// <summary>
    /// Gets the recent notes cache from the patient grain — zero fan-out.
    /// Returns the last N notes (N = site parameter NotesDisplayCount).
    /// </summary>
    Task<List<GrainStates.TiuNoteSummary>> GetRecentNotesAsync();

    /// <summary>
    /// Gets note history from the index grain with date range filtering.
    /// This is the "load more" path — reads from index metadata, no grain fan-out.
    /// </summary>
    Task<List<GrainStates.TiuNoteSummary>> GetNoteHistoryAsync(DateTime? from, DateTime? to, int maxCount);

    // ─── Consults (GMRCACTM.m, File #123) ───────────────────────────────

    Task<string> RequestConsultAsync(
        string toService, string? toServiceId,
        string? fromService, string? fromServiceId,
        string urgency,
        string? requestingProviderId, string? requestingProviderName,
        string? attentionProviderId, string? attentionProviderName,
        string? reasonForRequest, string? provisionalDiagnosis,
        string? orderId, string? locationId, string? locationName);
    Task AcceptConsultAsync(string consultId);
    Task ScheduleConsultAsync(string consultId);
    Task CompleteConsultAsync(string consultId, string? resultNoteText,
        string? authorId, string? authorName);
    Task CancelConsultAsync(string consultId, string? comments);
    Task DiscontinueConsultAsync(string consultId, string? comments);
    Task<GrainStates.ConsultState> GetConsultAsync(string consultId);
    Task<List<GrainStates.ConsultSummary>> GetConsultsAsync(string? statusFilter, int maxResults);

    /// <summary>
    /// Paged full consult history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.ConsultSummary>> GetConsultHistoryAsync(int offset, int maxResults);

    // ─── Surgery (File #130) ─────────────────────────────────────────────

    Task<string> ScheduleSurgeryAsync(
        string principalProcedure, string? cptCode, DateTime dateOfOperation,
        string? surgeonId, string? surgeonName, string? anesthesiaTechnique,
        string? surgicalSpecialty, string? preOpDiagnosis,
        string? locationId, string? locationName, string? comments);
    Task CompleteSurgeryAsync(string surgeryId, string? operativeReport, string? postOpDiagnosis);
    Task CancelSurgeryAsync(string surgeryId, string? comments);
    Task<GrainStates.SurgeryState> GetSurgeryAsync(string surgeryId);
    Task<List<GrainStates.SurgerySummary>> GetSurgeriesAsync(int maxResults);

    /// <summary>
    /// Paged full surgery history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.SurgerySummary>> GetSurgeryHistoryAsync(int offset, int maxResults);

    // ─── Radiology (File #75.1) ──────────────────────────────────────────

    Task<string> OrderRadiologyStudyAsync(
        string procedureName, string? procedureId, string? cptCode, string? imagingType,
        string? requestingProviderId, string? requestingProviderName,
        string? urgency, string? clinicalHistory, string? reasonForStudy,
        string? orderId, string? locationId, string? locationName);
    Task CompleteRadiologyAsync(string radiologyId, string? reportText, string? impression,
        string? interpretingPhysicianId, string? interpretingPhysicianName);
    Task<GrainStates.RadiologyState> GetRadiologyStudyAsync(string radiologyId);
    Task<List<GrainStates.RadiologySummary>> GetRadiologyStudiesAsync(int maxResults);

    /// <summary>
    /// Paged full radiology history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.RadiologySummary>> GetRadiologyHistoryAsync(int offset, int maxResults);

    // ─── BCMA (File #53.79) ──────────────────────────────────────────────

    /// <summary>Record a standalone (non-order-linked) medication administration.</summary>
    Task<string> RecordMedicationAdministrationAsync(
        string drugName, string? drugId, string? dosage, string? route,
        string actionStatus, DateTime? scheduledDateTime, DateTime administrationDateTime,
        string? administeredById, string? administeredByName,
        string? injectionSite, string? prescriptionId, string? orderId, string? comments);

    /// <summary>List recent administration events in reverse-chronological order.</summary>
    Task<List<GrainStates.BcmaSummary>> GetMedicationAdministrationsAsync(int maxResults);

    /// <summary>
    /// Paged full administration history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.BcmaSummary>> GetBcmaHistoryAsync(int offset, int maxResults);

    // ─── BCMA MAR (Medication Administration Record) ─────────────────────

    /// <summary>Return the full MAR for this patient, all orders.</summary>
    Task<List<GrainStates.MarEntry>> GetPatientMARAsync();

    /// <summary>Return only MAR entries whose next dose is due within 60 minutes.</summary>
    Task<List<GrainStates.MarEntry>> GetDueMedicationsAsync();

    /// <summary>
    /// Pull the current state of an inpatient order into the MAR index.
    /// Call after creating or verifying an inpatient order.
    /// </summary>
    Task SyncOrderToMARAsync(string orderId);

    /// <summary>
    /// Mark an inpatient order as inactive on the MAR.
    /// Call after discontinuing or expiring an order.
    /// </summary>
    Task DeactivateOrderOnMARAsync(string orderId);

    /// <summary>
    /// Core BCMA scan-and-administer workflow.
    /// Creates a <see cref="IBcmaGrain"/> record, links it to the inpatient order,
    /// and updates the MAR entry — all in one coordinated call.
    /// </summary>
    Task<string> AdministerMedicationAsync(
        string orderId,
        string actionStatus,
        DateTime administrationDateTime,
        string? administeredById,
        string? administeredByName,
        string? injectionSite,
        string? prnReason,
        string? comments);

    // ─── Imaging (File #2005) ────────────────────────────────────────────

    Task<string> CaptureImageAsync(
        string objectType, string? procedureDescription, string? specialtyIndex,
        string? imageUrl, string? thumbnailUrl,
        string? dicomSeriesUid, string? dicomStudyUid,
        DateTime? procedureDate, DateTime captureDate, int imageCount,
        string? radiologyId, string? tiuDocumentId,
        string? capturedById, string? capturedByName,
        string? locationId, string? locationName, string? comments);
    Task<List<GrainStates.ImagingSummary>> GetImagesAsync(int maxResults);

    /// <summary>
    /// Paged full imaging history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.ImagingSummary>> GetImagingHistoryAsync(int offset, int maxResults);

    // ─── Clinical Reminders (File #811.9) ────────────────────────────────

    Task<string> CreateReminderAsync(
        string reminderName, string? reminderDefinitionId, string? category,
        string? priority, string? frequency, DateTime? dueDate);
    Task CompleteReminderAsync(string reminderId, string? evaluatedByProviderId, string? evaluatedByProviderName);
    Task<List<GrainStates.ReminderSummary>> GetRemindersAsync();

    // ─── Immunizations (File #9000010.11) ────────────────────────────────

    Task<string> RecordImmunizationAsync(
        string immunizationName, string? cvxCode, DateTime eventDateTime,
        string? series, string? lotNumber, string? manufacturer,
        string? administeredById, string? administeredByName,
        string? administrationSite, string? route, string? dose,
        string? locationId, string? locationName, string? comments);
    Task<List<GrainStates.ImmunizationSummary>> GetImmunizationsAsync();

    // ─── Health Factors (File #9000010.23) ───────────────────────────────

    Task<string> RecordHealthFactorAsync(
        string healthFactorName, string? category, DateTime eventDateTime,
        string? levelSeverity, string? visitId,
        string? locationId, string? locationName,
        string? enteredById, string? enteredByName, string? comments);
    Task<List<GrainStates.HealthFactorSummary>> GetHealthFactorsAsync();

    /// <summary>
    /// Paged full health factor history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.HealthFactorSummary>> GetHealthFactorHistoryAsync(int offset, int maxResults);

    // ─── Mental Health (File #601.71) ────────────────────────────────────

    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "CREATE", EntityType = "MH_SCREEN", IsClinicalWrite = true)]
    Task<string> RecordMentalHealthScreenAsync(
        string instrumentName, DateTime administrationDateTime,
        decimal? totalScore, string? scoreInterpretation, bool? isPositiveScreen,
        Dictionary<string, string>? responses,
        string? administeredById, string? administeredByName,
        string? locationId, string? locationName, string? comments);
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "READ", EntityType = "MH_SCREEN")]
    Task<List<GrainStates.MentalHealthSummary>> GetMentalHealthScreensAsync();

    /// <summary>
    /// Paged full mental health history (newest first); default reads return only the recent window.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "READ", EntityType = "MH_SCREEN")]
    Task<List<GrainStates.MentalHealthSummary>> GetMentalHealthHistoryAsync(int offset, int maxResults);

    // ─── Dietetics (File #115.2) ─────────────────────────────────────────

    Task<string> CreateDietOrderAsync(
        string dietType, string? currentDiet, List<string>? modifications,
        string? texture, string? fluidConsistency, string? calorieLevel,
        string? specialInstructions, DateTime startDateTime,
        string? providerId, string? providerName, string? comments);
    Task DiscontinueDietOrderAsync(string dietOrderId);
    Task<List<GrainStates.DieteticsSummary>> GetDietOrdersAsync();

    // ─── Prosthetics (File #669.1) ───────────────────────────────────────

    Task<string> IssueProstheticAsync(
        string itemDescription, string? hcpcsCode, string? itemCategory,
        DateTime dateIssued, int quantity, decimal? cost,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        bool isServiceConnected, string? comments);
    Task<List<GrainStates.ProstheticsSummary>> GetProstheticsAsync();

    // ─── Means Test (File #408.31) ───────────────────────────────────────

    Task<string> RecordMeansTestAsync(
        string testType, DateTime dateOfTest,
        decimal? annualIncome, decimal? netWorth, int? numberOfDependents,
        string? eligibilityStatus, string? priorityGroup,
        string? completedById, string? completedByName, string? comments);
    Task<List<GrainStates.MeansTestSummary>> GetMeansTestsAsync();

    // ─── Service Connected Conditions (File #2.04) ───────────────────────

    Task<string> RecordServiceConnectedConditionAsync(
        string condition, string? diagnosisCode, int? disabilityPercentage,
        bool isServiceConnected, DateTime? effectiveDate,
        string? extremityAffected, string? comments);
    Task<List<GrainStates.ServiceConnectedSummary>> GetServiceConnectedConditionsAsync();

    // ─── PCE — Patient Care Encounters (File #9000010) ───────────────────

    Task<string> CreateEncounterAsync(
        DateTime visitDateTime,
        string serviceCategory,
        string? locationId,
        string? locationName,
        string? visitType,
        string? stopCode,
        string? primaryProviderId,
        string? primaryProviderName,
        string? linkedAppointmentId,
        string? comments);

    Task<GrainStates.VisitState> GetEncounterAsync(string visitId);

    Task<List<GrainStates.PceVisitEntry>> GetEncounterListAsync(int maxResults);

    Task CheckOutEncounterAsync(string visitId, DateTime checkOutDateTime);

    Task AddEncounterDiagnosisAsync(
        string visitId,
        string icd10Code,
        string description,
        bool isPrimary,
        string? providerId,
        string? providerName);

    Task AddEncounterProcedureAsync(
        string visitId,
        string cptCode,
        string description,
        int quantity,
        string? modifiers,
        string? providerId,
        string? providerName);

    Task CancelEncounterAsync(string visitId, string? reason);

    // ─── ADT — Admit/Discharge/Transfer (File #405) ─────────────────────

    [RequiresSecurityKey(SecurityKeys.DG_ADMIT)]
    [AuditAction("ADT", "CREATE", EntityType = "ADMISSION", IsClinicalWrite = true)]
    Task<string> RecordAdmissionAsync(
        DateTime movementDateTime, string? wardLocationId, string? wardLocationName,
        string? roomBed, string? treatingSpecialtyName,
        string? attendingPhysicianId, string? attendingPhysicianName,
        string? admissionDiagnosis, string? comments);
    [RequiresSecurityKey(SecurityKeys.DG_ADMIT)]
    [AuditAction("ADT", "DISCHARGE", EntityType = "MOVEMENT", IsClinicalWrite = true)]
    Task RecordDischargeAsync(string movementId, DateTime dischargeDateTime,
        string? dischargeDiagnosis, string? disposition, string? comments);
    [RequiresSecurityKey(SecurityKeys.DG_ADMIT)]
    [AuditAction("ADT", "TRANSFER", EntityType = "MOVEMENT", IsClinicalWrite = true)]
    Task<string> RecordTransferAsync(
        string currentMovementId,
        DateTime transferDateTime,
        string? toWardId, string? toWardName,
        string? toRoomBed,
        string? toSpecialtyId, string? toSpecialtyName,
        string? attendingPhysicianId, string? attendingPhysicianName,
        string? comments);
    Task<List<GrainStates.AdtSummary>> GetAdtMovementsAsync();

    /// <summary>
    /// Paged full ADT movement history (newest first); default reads return only the recent window.
    /// </summary>
    Task<List<GrainStates.AdtSummary>> GetAdtHistoryAsync(int offset, int maxResults);
    Task<List<GrainStates.WardCensusEntry>> GetWardCensusAsync(string wardId);
    Task<List<GrainStates.WardLocationEntry>> GetWardListAsync();

    // ─── Audit Trail (VistA AUDIT file #1.1, XUSEC routines) ────────────

    /// <summary>
    /// Log an auditable action for this patient.
    /// Creates an immutable AuditEventGrain and adds a summary to the patient index.
    /// Mirrors VistA XUSEC LOG which writes to the AUDIT file (#1.1).
    /// </summary>
    Task<string> LogAuditEventAsync(
        string domain,
        string action,
        string entityType,
        string entityId,
        string? userId,
        string? userName,
        string? locationId,
        string? locationName,
        string? details,
        string? oldValue = null,
        string? newValue = null);

    /// <summary>
    /// Get the most recent audit events for this patient.
    /// </summary>
    Task<List<GrainStates.AuditEventSummary>> GetRecentAuditEventsAsync(int maxResults = 100);

    /// <summary>
    /// Get audit events filtered by domain and/or date range.
    /// Mirrors VistA XUSEC QUERY with FILE and DATE filters.
    /// </summary>
    Task<List<GrainStates.AuditEventSummary>> GetAuditEventsAsync(
        string? domain,
        DateTime? from,
        DateTime? to,
        int maxResults = 200);

    /// <summary>
    /// Get all audit events for a specific entity (e.g., all actions on one order).
    /// </summary>
    Task<List<GrainStates.AuditEventSummary>> GetAuditEventsByEntityAsync(
        string entityType,
        string entityId);

    /// <summary>
    /// Get the full detail of a single audit event by its ID.
    /// </summary>
    Task<GrainStates.AuditEventState> GetAuditEventAsync(string eventId);

    // ── Notifications / Alerts (VistA ORB NOTIFICATION / ALERT) ──────────────

    /// <summary>
    /// Creates a new alert/notification for this patient.
    /// Mirrors VistA ORB NOTIFICATION (#100.9) alert generation.
    /// Returns the new alert ID (XQAID).
    /// </summary>
    Task<string> CreateAlertAsync(
        int notificationType,
        string notificationTypeText,
        string recipientId,
        string recipientName,
        string? sendingPackage,
        string? messageText,
        string? followUpAction,
        bool isCritical,
        string? xqaData);

    /// <summary>
    /// Gets a specific alert/notification by its XQAID.
    /// </summary>
    Task<GrainStates.NotificationState> GetAlertAsync(string alertId);

    /// <summary>
    /// Marks an alert as processed (acknowledged by the recipient).
    /// </summary>
    Task ProcessAlertAsync(string alertId, DateTime processedDateTime, string processedByUserId);

    /// <summary>
    /// Deletes an alert.
    /// </summary>
    Task DeleteAlertAsync(string alertId, string deletedByUserId);

    /// <summary>
    /// Forwards an alert to another recipient.
    /// </summary>
    Task ForwardAlertAsync(
        string alertId,
        string toRecipientId,
        string toRecipientName,
        string forwardType,
        string? comment,
        string forwardedByUserId);

    // ─── Integrated Billing (IB) — Files #350-354, IBAUTL.m, IBCPACT.m ──────

    /// <summary>Returns all billing actions for this patient (from the per-patient index).</summary>
    Task<List<GrainStates.IBillingActionIndexEntry>> GetBillingActionsAsync();

    /// <summary>Returns billing actions filtered by processing status.</summary>
    Task<List<GrainStates.IBillingActionIndexEntry>> GetBillingActionsByStatusAsync(GrainStates.IBillingActionStatus status);

    /// <summary>Returns the full state of a single billing action record.</summary>
    Task<GrainStates.IBillingActionState> GetBillingActionAsync(string billingActionId);

    /// <summary>
    /// Records a new billing action for this patient.
    /// Creates the action grain, updates the per-patient index, and posts
    /// the transaction to the patient's copay account.
    /// Returns the new billing action ID.
    /// </summary>
    Task<string> RecordBillingActionAsync(
        string actionTypeCode,
        string actionTypeDescription,
        GrainStates.IBActionCategory actionCategory,
        decimal? chargeAmount,
        DateTime serviceDate,
        string enteredByUserId,
        string enteredByUserName,
        string? encounterId,
        string? diagnosisCode,
        string? procedureCode,
        string? locationId,
        string? orderId,
        string? prescriptionId,
        string? notes);

    /// <summary>Cancels a billing action with a remove reason (File #350.3).</summary>
    Task CancelBillingActionAsync(
        string billingActionId,
        string removeReasonCode,
        string removeReasonDescription,
        string removedByUserId);

    /// <summary>Returns the patient's billing / copay account record (File #354).</summary>
    Task<GrainStates.IBillingPatientState> GetPatientCopayAccountAsync();

    /// <summary>Sets or clears the patient's copay exemption status.</summary>
    Task SetCopayExemptionAsync(
        bool isExempt,
        string? reasonCode,
        DateTime? effectiveDate,
        DateTime? expirationDate);

    /// <summary>Returns the patient's Means Test Billing Clock record (File #351).</summary>
    Task<GrainStates.MeansTestBillingClockState> GetBillingClockAsync();

    /// <summary>Sets or updates the patient's billing clock period.</summary>
    Task SetBillingClockAsync(
        string clockStatus,
        DateTime? startDate,
        DateTime? expirationDate,
        string? meansTestId,
        string? billingCategory,
        string? priorityGroup);

    // ─── Insurance (Files #355.x) — IBINSU*.m ────────────────────────────────

    /// <summary>Returns all personal insurance policy entries for this patient.</summary>
    Task<List<GrainStates.PersonalPolicyIndexEntry>> GetPersonalPoliciesAsync();

    /// <summary>Returns the full state of a single personal policy record.</summary>
    Task<GrainStates.PersonalPolicyState> GetPersonalPolicyAsync(string policyId);

    /// <summary>
    /// Adds a new personal insurance policy for this patient.
    /// Creates the policy grain and updates the per-patient policy index.
    /// Returns the new policy ID.
    /// </summary>
    Task<string> AddPersonalPolicyAsync(
        string? groupPlanId,
        string groupPlanName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? effectiveDate,
        DateTime? expirationDate,
        string? coverageType,
        bool isPrimary,
        decimal? copayAmount,
        string? pharmacyMemberId,
        string? notes);

    /// <summary>Marks a personal insurance policy as inactive (coverage ended).</summary>
    Task DeactivatePersonalPolicyAsync(string policyId);

    // ─── Registration — Enrollment (File #27.11) — DGENELA.m, DGENELB.m ─────

    /// <summary>Returns the patient's VA enrollment record.</summary>
    Task<GrainStates.PatientEnrollmentState> GetEnrollmentAsync();

    /// <summary>Updates the patient's enrollment status.</summary>
    Task SetEnrollmentStatusAsync(GrainStates.EnrollmentStatus status, string changedByUserId, string? notes);

    /// <summary>Sets the enrollment priority group and copay exemption flags.</summary>
    Task SetEnrollmentPriorityGroupAsync(
        string priorityGroup,
        string? prioritySubgroup,
        bool meansTestRequired,
        bool copayExempt,
        string? copayExemptionReason);

    // ─── Registration — PRF Patient Record Flags (Files #26.11, #26.13) ──────

    /// <summary>Returns all PRF flag assignments for this patient.</summary>
    Task<GrainStates.PrfAssignmentState> GetPrfFlagsAsync();

    /// <summary>Assigns a PRF flag to the patient.</summary>
    Task AssignPrfFlagAsync(
        string flagId,
        string flagName,
        string flagType,
        bool isNational,
        string assignedByUserId,
        string assignedByUserName,
        string? narrative);

    /// <summary>Deactivates an active PRF flag assignment.</summary>
    Task DeactivatePrfFlagAsync(string flagId, string deactivatedReason, string deactivatedByUserId);

    // ─── Registration — MST History (File #29.11) — DGMSTSC.m ───────────────

    /// <summary>Returns the patient's MST screening history.</summary>
    Task<GrainStates.MstHistoryState> GetMstHistoryAsync();

    /// <summary>Records a new MST screening encounter.</summary>
    Task RecordMstScreeningAsync(
        DateTime screeningDate,
        GrainStates.MstStatus status,
        string screenedByUserId,
        string screenedByUserName,
        string? location,
        string? notes);

    // ─── Registration — Patient Relations (File #408.12) ─────────────────────

    /// <summary>Returns all patient relation and emergency contact records.</summary>
    Task<GrainStates.PatientRelationState> GetPatientRelationsAsync();

    /// <summary>Adds or updates a patient relation record. Returns the relation ID.</summary>
    Task<string> AddOrUpdatePatientRelationAsync(GrainStates.PatientRelation relation);

    /// <summary>Removes a patient relation record by ID.</summary>
    Task RemovePatientRelationAsync(string relationId);

    // ─── Registration — Income / Household (File #408.13) — DGMTU.m ─────────

    /// <summary>Returns the household income record for means test purposes.</summary>
    Task<GrainStates.IncomeHouseholdState> GetIncomeHouseholdAsync();

    /// <summary>Adds or updates a household member income record. Returns the person ID.</summary>
    Task<string> AddOrUpdateIncomePersonAsync(GrainStates.IncomePerson member);

    /// <summary>Records the means test decision outcome.</summary>
    Task RecordMeansTestDecisionAsync(string decision, DateTime decisionDate, decimal? threshold);

    // ─── Registration — Treating Facilities (File #391.91) — VAFHLTR.m ───────

    /// <summary>Returns the list of treating facilities for this patient.</summary>
    Task<GrainStates.TreatingFacilityListState> GetTreatingFacilitiesAsync();

    /// <summary>Adds or updates a treating facility relationship.</summary>
    Task AddOrUpdateTreatingFacilityAsync(GrainStates.TreatingFacilityEntry facility);

    /// <summary>Sets the patient's primary enrollment facility.</summary>
    Task SetPrimaryTreatingFacilityAsync(string facilityId, string facilityName);

    // ─── Accounts Receivable (Files #340, #430, #433) — RCDP*.m, RCSP*.m ────────

    /// <summary>Returns the AR debtor aggregate summary for this patient.</summary>
    Task<GrainStates.ARDebtorState> GetARDebtorAsync();

    /// <summary>Returns all AR account summaries for this patient.</summary>
    Task<List<GrainStates.ARAccountIndexEntry>> GetARAccountsAsync();

    /// <summary>Returns AR account summaries with Active, OnPaymentPlan, or InCollection status.</summary>
    Task<List<GrainStates.ARAccountIndexEntry>> GetActiveARAccountsAsync();

    /// <summary>Returns the full AR account state for a specific account.</summary>
    Task<GrainStates.ARAccountState> GetARAccountAsync(string arAccountId);

    /// <summary>
    /// Creates a new AR account for this patient from the given charge details.
    /// Returns the new AR account ID.
    /// </summary>
    Task<string> CreateARAccountAsync(
        string? billingActionId,
        string arCategory,
        decimal originalAmount,
        DateTime? dueDate);

    /// <summary>
    /// Posts a payment against the specified AR account.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> PostARPaymentAsync(
        string arAccountId,
        decimal amount,
        string paymentMethod,
        string appliedByUserId,
        string appliedByUserName,
        string? receiptNumber,
        string? checkNumber,
        string? notes);

    /// <summary>
    /// Posts a balance adjustment against the specified AR account.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> PostARAdjustmentAsync(
        string arAccountId,
        decimal amount,
        string adjustmentType,
        string appliedByUserId,
        string appliedByUserName,
        string? notes);

    /// <summary>
    /// Waives a portion or all of the outstanding balance on the specified AR account.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> WaiveARAccountAsync(
        string arAccountId,
        decimal waivedAmount,
        string waivedByUserId,
        string waivedByUserName,
        string reason);

    /// <summary>Accrues interest on a specific AR account for this patient.</summary>
    Task AccrueARInterestAsync(string arAccountId, decimal interestAmount, string appliedByUserId);

    /// <summary>Accrues a penalty charge on a specific AR account for this patient.</summary>
    Task AccrueARPenaltyAsync(string arAccountId, decimal penaltyAmount, string appliedByUserId);

    /// <summary>Accrues an administrative cost charge on a specific AR account for this patient.</summary>
    Task AccrueARAdminCostAsync(string arAccountId, decimal adminCostAmount, string appliedByUserId);

    // ─── Fee Basis (Files #162, #162.1, #162.5, #162.6) — FBPAID.m ──────────────

    /// <summary>Returns the fee basis patient summary for this patient.</summary>
    Task<GrainStates.FeePatientState> GetFeePatientAsync();

    /// <summary>Returns all fee basis authorization summaries for this patient.</summary>
    Task<List<GrainStates.FeeAuthorizationIndexEntry>> GetFeeAuthorizationsAsync();

    /// <summary>Returns the full fee basis authorization state for a specific authorization.</summary>
    Task<GrainStates.FeeAuthorizationState> GetFeeAuthorizationAsync(string authId);

    /// <summary>
    /// Creates a new fee basis authorization for this patient from the specified vendor.
    /// Returns the new authorization ID.
    /// </summary>
    Task<string> CreateFeeAuthorizationAsync(
        string vendorId,
        string vendorName,
        string serviceType,
        DateTime authorizationDate,
        DateTime effectiveDate,
        DateTime? expirationDate,
        decimal authorizedAmount,
        string authorizedByUserId,
        string authorizedByUserName,
        string serviceDescription,
        int? maxVisits,
        string? diagnosisCode,
        string? authorizationNumber,
        string? notes);

    /// <summary>Returns all fee basis invoice summaries for this patient.</summary>
    Task<List<GrainStates.FeeInvoiceIndexEntry>> GetFeeInvoicesAsync();

    // ─── Agent Cashier (File #36) — RCDPE.m ──────────────────────────────────────

    /// <summary>Returns all cashier receipt summaries for this patient.</summary>
    Task<List<GrainStates.CashierReceiptIndexEntry>> GetCashierReceiptsAsync();

    // ─── EDI / Electronic Billing (Files #361, #364) — IBCEF*.m, IBCED*.m ────────

    /// <summary>Returns all EDI claim summaries for this patient.</summary>
    Task<List<GrainStates.EdiClaimIndexEntry>> GetEdiClaimsAsync();

    // ─── Blood Bank (File #65) — BBAPI.m, BBTM.m, BBCM.m, BBTRAN.m ─────────────

    /// <summary>Returns the patient's blood bank master record (ABO/Rh, antibodies, history).</summary>
    Task<GrainStates.BloodBankPatientState> GetBloodBankPatientAsync();

    /// <summary>
    /// Records or updates the patient's blood type and antibody screen result.
    /// Corresponds to VistA BB TYPE &amp; SCREEN order (BBTM SCREEN).
    /// </summary>
    Task UpdateBloodTypeAsync(
        GrainStates.AboBloodType aboType,
        GrainStates.RhBloodType rhType,
        GrainStates.AntibodyScreenResult antibodyScreenResult,
        DateTime? antibodyScreenDate,
        string? directAntibodyTest,
        string? specialRequirements,
        string? notes);

    /// <summary>
    /// Requests a crossmatch of the specified blood unit for this patient.
    /// Reserves the blood unit and creates a crossmatch record.
    /// Returns the new crossmatch ID.
    /// </summary>
    Task<string> RequestCrossmatchAsync(
        string unitId,
        GrainStates.CrossmatchUrgency urgency,
        string requestedByUserId,
        string requestedByUserName,
        string? notes);

    /// <summary>Returns all crossmatch records for this patient.</summary>
    Task<List<GrainStates.CrossmatchIndexEntry>> GetCrossmatchesAsync();

    /// <summary>
    /// Records the compatibility test result for an existing crossmatch.
    /// </summary>
    Task RecordCrossmatchResultAsync(
        string crossmatchId,
        GrainStates.CrossmatchResult result,
        GrainStates.CrossmatchMethod method,
        string technicianId,
        string technicianName,
        string? antibodyIdentification);

    /// <summary>
    /// Starts a transfusion of a compatible blood unit to this patient.
    /// Issues the unit from the crossmatch, marks the blood unit as transfused,
    /// and creates the transfusion record.
    /// Returns the new transfusion ID.
    /// </summary>
    Task<string> StartTransfusionAsync(
        string crossmatchId,
        string unitId,
        string administeredByUserId,
        string administeredByUserName,
        string orderedByUserId,
        string orderedByUserName,
        string? infusionSite,
        string? preTransfusionVitals);

    /// <summary>Records successful completion of a transfusion.</summary>
    Task CompleteTransfusionAsync(
        string transfusionId,
        DateTime endDateTime,
        decimal? volumeML,
        string? postTransfusionVitals);

    /// <summary>
    /// Stops a transfusion early.
    /// If a reaction occurred, sets the status to Reaction and records the type.
    /// </summary>
    Task StopTransfusionAsync(
        string transfusionId,
        DateTime endDateTime,
        string stopReason,
        GrainStates.TransfusionReactionType reactionType,
        string? reactionNotes);

    /// <summary>Returns the transfusion history for this patient.</summary>
    Task<List<GrainStates.TransfusionIndexEntry>> GetTransfusionHistoryAsync();

    // ─── Anatomic Pathology (LRAP.m, LRAPSC.m, LRAPACC.m, LRAPAU.m) ────────
    // VistA Files #63.08 (SP), #63.09 (CY), #63.19 (AU)

    /// <summary>
    /// Accessions a new Anatomic Pathology case (SP, CY, or AU).
    /// Returns the new case ID. LRAPACC.m ACCESSION.
    /// </summary>
    Task<string> AccessionAPCaseAsync(
        GrainStates.APCaseType caseType,
        string accessionNumber,
        string? specimenSource,
        string? specimenDescription,
        string? specimenType,
        string? clinicalHistory,
        string? clinicalDiagnosis,
        string? referringProviderId,
        string? referringProviderName,
        string? collectionLocation,
        DateTime? dateCollected,
        DateTime dateReceived);

    /// <summary>Records gross/macroscopic examination of specimen. LRAPSC.m GROSS.</summary>
    Task RecordAPGrossDescriptionAsync(
        string caseId,
        string grossDescription,
        string? pathologistId,
        string? pathologistName,
        int? specimenPartCount,
        decimal? specimenWeightGrams,
        string? frozenSectionDiagnosis);

    /// <summary>Records microscopic/histologic description after slide review. LRAPSC.m MICRO.</summary>
    Task RecordAPMicroscopicDescriptionAsync(string caseId, string microscopicDescription);

    /// <summary>Issues the final signed-out diagnosis. LRAP.m SIGNOUT.</summary>
    Task SignOutAPDiagnosisAsync(
        string caseId,
        string diagnosis,
        List<string> diagnosisCodes,
        string pathologistId,
        string pathologistName,
        DateTime signOutDateTime);

    /// <summary>Issues a preliminary diagnosis before full workup. Status → Preliminary.</summary>
    Task IssueAPPreliminaryDiagnosisAsync(
        string caseId,
        string preliminaryDiagnosis,
        string pathologistId,
        string pathologistName);

    /// <summary>Appends an addendum to a final case.</summary>
    Task AddAPAddendumAsync(string caseId, string addendumText, string pathologistId, string pathologistName);

    /// <summary>Amends (corrects) a signed-out diagnosis.</summary>
    Task AmendAPDiagnosisAsync(
        string caseId,
        string correctedDiagnosis,
        List<string> correctedCodes,
        string amendmentReason,
        string pathologistId,
        string pathologistName);

    /// <summary>Records cytology-specific details (Bethesda category, specimen adequacy).</summary>
    Task RecordAPCytologyDetailsAsync(string caseId, string? bethesdaCategory, string? specimenAdequacy);

    /// <summary>Records autopsy-specific findings. LRAPAU.m AUTOPSY.</summary>
    Task RecordAPAutopsyFindingsAsync(
        string caseId,
        string? causeOfDeath,
        string? underlyingCauseOfDeath,
        GrainStates.MannerOfDeath? mannerOfDeath,
        string? toxicologyFindings,
        decimal? bodyWeightKg,
        string? neuropathologyFindings);

    /// <summary>Returns the full state of a single AP case.</summary>
    Task<GrainStates.AnatomicPathologyState> GetAPCaseAsync(string caseId);

    /// <summary>Returns all AP cases for this patient.</summary>
    Task<List<GrainStates.APCaseIndexEntry>> GetAPCasesAsync();

    /// <summary>Returns AP cases of a specific type (SP, CY, or AU).</summary>
    Task<List<GrainStates.APCaseIndexEntry>> GetAPCasesByTypeAsync(GrainStates.APCaseType caseType);

    // ─── Nursing (Files #210-212) — NUR*.m routines ──────────────────────────

    /// <summary>
    /// Creates a nursing assessment (head-to-toe) for this patient.
    /// Creates the assessment grain and updates the per-patient index.
    /// Returns the new assessment ID.
    /// </summary>
    Task<string> CreateNursingAssessmentAsync(
        DateTime assessmentDateTime,
        string assessmentType,
        string nurseId,
        string nurseName,
        string? locationId,
        string? locationName,
        string? levelOfConsciousness,
        List<string>? orientation,
        string? breathSounds,
        string? oxygenTherapy,
        decimal? spO2,
        string? heartRhythm,
        string? edema,
        string? skinIntegrity,
        int? bradenScore,
        int? painScore,
        string? painLocation,
        string? bowelSounds,
        string? appetiteAssessment,
        decimal? urineOutput,
        bool hasFoley,
        string? anxietyLevel,
        string? mood,
        int? morseScore,
        string? fallRiskLevel,
        List<string>? fallPrecautions,
        string? adlMobility,
        string? narrativeNotes);

    /// <summary>Returns the full state of a specific nursing assessment.</summary>
    Task<GrainStates.NursingAssessmentState> GetNursingAssessmentAsync(string assessmentId);

    /// <summary>Returns the assessment index for this patient, newest first.</summary>
    Task<List<GrainStates.NursingAssessmentIndexEntry>> GetNursingAssessmentsAsync();

    /// <summary>Signs a nursing assessment, updating the index entry status.</summary>
    Task SignNursingAssessmentAsync(string assessmentId, string nurseId, string nurseName);

    /// <summary>Returns the nursing care plan for this patient.</summary>
    Task<GrainStates.NursingCarePlanState> GetNursingCarePlanAsync();

    /// <summary>
    /// Adds a NANDA-style nursing diagnosis to the care plan.
    /// Returns the new problem ID.
    /// </summary>
    Task<string> AddNursingDiagnosisAsync(
        string nursingDiagnosis,
        string? relatedTo,
        string? evidencedBy,
        int? priority,
        string? nurseId,
        string? nurseName);

    /// <summary>Adds a measurable goal to a nursing diagnosis.</summary>
    Task AddCarePlanGoalAsync(string problemId, string goalText, DateTime? targetDate);

    /// <summary>Adds a nursing intervention to a nursing diagnosis.</summary>
    Task AddCarePlanInterventionAsync(
        string problemId,
        string interventionText,
        string? frequency,
        string? nurseId,
        string? nurseName);

    /// <summary>Records an outcome evaluation for a nursing diagnosis.</summary>
    Task RecordCarePlanOutcomeAsync(
        string problemId,
        GrainStates.NursingOutcomeRating rating,
        string evaluatedById,
        string evaluatedByName,
        string? notes);

    /// <summary>Updates the achievement status of a care plan goal.</summary>
    Task UpdateCarePlanGoalStatusAsync(
        string problemId,
        string goalId,
        GrainStates.NursingGoalStatus status);

    /// <summary>Resolves (closes) a nursing diagnosis on the care plan.</summary>
    Task ResolveNursingDiagnosisAsync(string problemId, string? resolutionNotes);

    /// <summary>Records a per-shift acuity classification for this patient.</summary>
    Task RecordNursingAcuityAsync(
        GrainStates.AcuityLevel level,
        int? score,
        string nurseId,
        string nurseName,
        string? shift,
        string? notes);

    /// <summary>Returns the current acuity state and full classification history.</summary>
    Task<GrainStates.NursingAcuityState> GetNursingAcuityAsync();

    // ─── Dental (Files #228, #228.1) — DENPAT.m, DENTX.m, DENPROC.m ─────────

    /// <summary>Returns the patient's dental record (eligibility, clinical status, visit dates).</summary>
    Task<GrainStates.DentalPatientState> GetDentalPatientAsync();

    /// <summary>Updates the patient's VA dental care eligibility and basis.</summary>
    Task UpdateDentalEligibilityAsync(
        GrainStates.DentalEligibilityStatus eligibilityStatus,
        string? eligibilityBasisCode,
        string? eligibilityBasisDescription);

    /// <summary>Sets or updates the patient's primary VA dentist.</summary>
    Task SetPrimaryDentistAsync(string dentistId, string dentistName);

    /// <summary>
    /// Updates the patient's clinical dental status (periodontal classification,
    /// prosthetic status, remaining teeth, fluoride flag, clinical notes).
    /// </summary>
    Task UpdateDentalClinicalStatusAsync(
        GrainStates.DentalPeriodontalStatus periodontalStatus,
        string? prostheticStatus,
        int? remainingTeethCount,
        bool onFluoride,
        string? clinicalNotes);

    /// <summary>
    /// Records one or more visit date fields (exam date, x-ray date, cleaning date).
    /// Null values leave existing dates unchanged.
    /// </summary>
    Task RecordDentalVisitDatesAsync(
        DateTime? lastExamDate,
        DateTime? lastXRayDate,
        DateTime? lastCleaningDate);

    /// <summary>Returns all dental treatment summaries for this patient, newest first.</summary>
    Task<List<GrainStates.DentalTreatmentIndexEntry>> GetDentalTreatmentsAsync();

    /// <summary>Returns dental treatments filtered by lifecycle status.</summary>
    Task<List<GrainStates.DentalTreatmentIndexEntry>> GetDentalTreatmentsByStatusAsync(
        GrainStates.DentalTreatmentStatus status);

    /// <summary>Returns the full state of a single dental treatment record.</summary>
    Task<GrainStates.DentalTreatmentState> GetDentalTreatmentAsync(string treatmentId);

    /// <summary>
    /// Records a new dental treatment / procedure for this patient.
    /// Creates the treatment grain and updates the per-patient treatment index.
    /// Returns the new treatment ID.
    /// </summary>
    Task<string> RecordDentalTreatmentAsync(
        DateTime treatmentDate,
        string procedureCode,
        string procedureDescription,
        GrainStates.DentalProcedureCategory procedureCategory,
        List<int> toothNumbers,
        List<string> surfaces,
        string providerId,
        string providerName,
        string? locationId,
        string? locationName,
        string? diagnosisCode,
        string? anesthesiaType,
        decimal? chargeAmount,
        string? notes);

    /// <summary>Marks a dental treatment as completed.</summary>
    Task CompleteDentalTreatmentAsync(
        string treatmentId,
        DateTime completedDate,
        string completedByUserId,
        string? notes);

    /// <summary>Cancels a dental treatment with a reason.</summary>
    Task CancelDentalTreatmentAsync(string treatmentId, string reason, string cancelledByUserId);

    /// <summary>Marks a dental treatment as referred to a specialist.</summary>
    Task ReferDentalTreatmentAsync(string treatmentId, string referralReason, string referredByUserId);

    // ─── Social Work (File #707) — SWRPATCH.m, SWR*.m ────────────────────────

    /// <summary>
    /// Creates a new social work assessment for this patient.
    /// Creates the assessment grain and updates the per-patient index.
    /// Returns the new assessment ID.
    /// </summary>
    Task<string> CreateSocialWorkAssessmentAsync(
        GrainStates.SocialWorkAssessmentType assessmentType,
        DateTime assessmentDate,
        string? socialWorkerId,
        string? socialWorkerName,
        GrainStates.SocialWorkRiskLevel riskLevel,
        string? housingStatus,
        string? employmentStatus,
        string? socialSupport,
        string? financialStressors,
        string? substanceUseHistory,
        bool? abuseConcernsIdentified,
        bool? safetyPlanInPlace,
        DateTime? anticipatedDischargeDate,
        string? dischargeDisposition,
        string? dischargePlan,
        List<string>? dischargeBarriers,
        string? recommendations,
        string? notes,
        string? locationId,
        string? locationName);

    /// <summary>Signs/completes a draft social work assessment.</summary>
    Task CompleteSocialWorkAssessmentAsync(
        string assessmentId,
        DateTime completedDate,
        string? recommendations,
        string? notes);

    /// <summary>Closes a social work assessment (e.g. patient deceased, transferred).</summary>
    Task CloseSocialWorkAssessmentAsync(string assessmentId, string reason);

    /// <summary>Returns the full state of a single social work assessment.</summary>
    Task<GrainStates.SocialWorkAssessmentState> GetSocialWorkAssessmentAsync(string assessmentId);

    /// <summary>Returns all assessment summaries for this patient.</summary>
    Task<List<GrainStates.SocialWorkAssessmentIndexEntry>> GetSocialWorkAssessmentsAsync();

    /// <summary>Returns assessments filtered by type.</summary>
    Task<List<GrainStates.SocialWorkAssessmentIndexEntry>> GetSocialWorkAssessmentsByTypeAsync(
        GrainStates.SocialWorkAssessmentType assessmentType);

    /// <summary>
    /// Creates a new social work referral for this patient.
    /// Creates the referral grain and updates the per-patient referral index.
    /// Returns the new referral ID.
    /// </summary>
    Task<string> CreateSocialWorkReferralAsync(
        DateTime referralDate,
        string? referralSource,
        string? referralReason,
        GrainStates.SocialWorkReferralServiceType serviceType,
        string? agencyName,
        string? agencyContact,
        string? agencyPhone,
        string? socialWorkerId,
        string? socialWorkerName,
        DateTime? followUpDate,
        string? assessmentId,
        string? locationId,
        string? locationName,
        string? comments);

    /// <summary>Updates the status of an existing social work referral.</summary>
    Task UpdateSocialWorkReferralStatusAsync(
        string referralId,
        GrainStates.SocialWorkReferralStatus status,
        string? outcomeNotes,
        DateTime? followUpDate);

    /// <summary>Closes a social work referral with optional outcome notes.</summary>
    Task CloseSocialWorkReferralAsync(string referralId, string? outcomeNotes);

    /// <summary>Returns the full state of a single social work referral.</summary>
    Task<GrainStates.SocialWorkReferralState> GetSocialWorkReferralAsync(string referralId);

    /// <summary>Returns all referral summaries for this patient.</summary>
    Task<List<GrainStates.SocialWorkReferralIndexEntry>> GetSocialWorkReferralsAsync();

    /// <summary>Returns referrals filtered by status.</summary>
    Task<List<GrainStates.SocialWorkReferralIndexEntry>> GetSocialWorkReferralsByStatusAsync(
        GrainStates.SocialWorkReferralStatus status);

    // ─── Women's Health (VistA File #790 — WOMEN'S HEALTH) ───────────────────

    /// <summary>
    /// Creates a Women's Health notification record (mammography, Pap, contraception,
    /// pregnancy, breast health, or menopause/HRT). Returns the new notification ID.
    /// </summary>
    Task<string> CreateWomensHealthNotificationAsync(
        GrainStates.WomensHealthNotificationType notificationType,
        DateTime procedureDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        GrainStates.MammographyResult? mammographyResult,
        int? biRadsScore,
        GrainStates.PapSmearResult? papSmearResult,
        string? contraceptiveMethod,
        int? gestationalAgeWeeks,
        DateTime? estimatedDueDate,
        string? pregnancyOutcome,
        bool followUpRequired,
        DateTime? nextDueDate,
        bool isRefusal,
        string? notes);

    /// <summary>Marks a notification as completed, optionally recording the follow-up completion date.</summary>
    Task CompleteWomensHealthNotificationAsync(
        string notificationId,
        DateTime? followUpCompletedDate,
        string? notes);

    /// <summary>Sets or clears the follow-up required flag and optionally updates the next due date.</summary>
    Task SetWomensHealthFollowUpAsync(
        string notificationId,
        bool required,
        DateTime? nextDueDate);

    /// <summary>Cancels a Women's Health notification.</summary>
    Task CancelWomensHealthNotificationAsync(string notificationId);

    /// <summary>Returns the full state of a single Women's Health notification.</summary>
    Task<GrainStates.WomensHealthNotificationState> GetWomensHealthNotificationAsync(string notificationId);

    /// <summary>Returns all notification index entries for this patient (newest first).</summary>
    Task<List<GrainStates.WomensHealthIndexEntry>> GetWomensHealthNotificationsAsync();

    /// <summary>Returns notification index entries filtered by notification type.</summary>
    Task<List<GrainStates.WomensHealthIndexEntry>> GetWomensHealthNotificationsByTypeAsync(
        GrainStates.WomensHealthNotificationType notificationType);

    /// <summary>Returns notification index entries where follow-up is required.</summary>
    Task<List<GrainStates.WomensHealthIndexEntry>> GetWomensHealthFollowUpRequiredAsync();

    // ─── Prenatal / OB (IHS Prenatal Care Module — BJPNAPI.m, BWGRVL.m) ────────

    /// <summary>Creates a new pregnancy record with obstetric history and returns the pregnancy ID.</summary>
    Task<string> CreatePregnancyAsync(
        DateTime? lastMenstrualPeriod,
        DateTime? eddByLmp,
        DateTime? eddByUltrasound,
        DateTime definitiveEdd,
        int gravida, int para, int abortions, int living,
        GrainStates.PregnancyRiskLevel riskLevel,
        List<string>? riskFactors,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        string? notes);

    /// <summary>Returns the full pregnancy state.</summary>
    Task<GrainStates.PregnancyState> GetPregnancyAsync(string pregnancyId);

    /// <summary>Returns all pregnancy index entries for this patient (newest first).</summary>
    Task<List<GrainStates.PregnancyIndexEntry>> GetPregnanciesAsync();

    /// <summary>Returns the active pregnancy for this patient, or null.</summary>
    Task<GrainStates.PregnancyIndexEntry?> GetActivePregnancyAsync();

    /// <summary>Updates risk assessment for a pregnancy.</summary>
    Task UpdatePregnancyRiskAsync(string pregnancyId,
        GrainStates.PregnancyRiskLevel riskLevel, List<string> riskFactors);

    /// <summary>Adds a prenatal problem to a pregnancy.</summary>
    Task AddPrenatalProblemAsync(string pregnancyId, GrainStates.PrenatalProblemEntry problem);

    /// <summary>Resolves a prenatal problem.</summary>
    Task ResolvePrenatalProblemAsync(string pregnancyId, string problemId);

    /// <summary>Records delivery information and transitions the pregnancy.</summary>
    Task RecordDeliveryAsync(string pregnancyId,
        GrainStates.DeliveryInfo delivery, GrainStates.PregnancyOutcome outcome);

    /// <summary>Records postpartum follow-up information.</summary>
    Task RecordPostpartumAsync(string pregnancyId, GrainStates.PostpartumInfo postpartum);

    /// <summary>Updates the pregnancy status (e.g., Cancelled, Ectopic).</summary>
    Task UpdatePregnancyStatusAsync(string pregnancyId, GrainStates.PregnancyStatus status);

    /// <summary>Updates the definitive EDD for a pregnancy.</summary>
    Task UpdatePregnancyEddAsync(string pregnancyId, DateTime? eddByUltrasound, DateTime definitiveEdd);

    /// <summary>Creates a prenatal visit and returns the visit ID.</summary>
    Task<string> CreatePrenatalVisitAsync(
        string pregnancyId,
        DateTime visitDate,
        int gestationalAgeWeeks, int gestationalAgeDays,
        decimal? weight,
        int? bloodPressureSystolic, int? bloodPressureDiastolic,
        decimal? fundalHeightCm, int? fetalHeartRate,
        GrainStates.FetalPresentation fetalPresentation,
        bool? fetalMovement,
        string? urineProtein, string? urineGlucose, string? edema,
        decimal? cervicalDilationCm, int? cervicalEffacementPercent, int? fetalStation,
        string? providerId, string? providerName,
        string? notes, DateTime? nextVisitDate);

    /// <summary>Returns the full state of a prenatal visit.</summary>
    Task<GrainStates.PrenatalVisitState> GetPrenatalVisitAsync(string visitId);

    /// <summary>Returns all prenatal visit summaries for a pregnancy (newest first).</summary>
    Task<List<GrainStates.PrenatalVisitIndexEntry>> GetPrenatalVisitsAsync(string pregnancyId);

    /// <summary>Returns the count of prenatal visits for a pregnancy.</summary>
    Task<int> GetPrenatalVisitCountAsync(string pregnancyId);

    // ─── Substance Abuse Treatment (RPMS CDMIS — File #9002170, additive feature) ──

    /// <summary>Creates a SA treatment episode and returns the episode ID.</summary>
    Task<string> CreateSATreatmentEpisodeAsync(
        GrainStates.SATreatmentModality modality,
        GrainStates.SubstanceType primarySubstance,
        List<GrainStates.SubstanceType>? secondarySubstances,
        DateTime intakeDate,
        DateTime? lastUseDate, DateTime? sobrietyDate,
        string? programName, List<string>? treatmentGoals,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        string? notes);

    Task<GrainStates.SATreatmentEpisodeState> GetSATreatmentEpisodeAsync(string episodeId);
    Task<List<GrainStates.SATreatmentEpisodeIndexEntry>> GetSATreatmentEpisodesAsync();
    Task<GrainStates.SATreatmentEpisodeIndexEntry?> GetActiveSATreatmentAsync();

    Task AddSAMATEntryAsync(string episodeId, GrainStates.MATEntry entry);
    Task StopSAMATEntryAsync(string episodeId, string entryId, DateTime endDate);
    Task AddSATreatmentGoalAsync(string episodeId, string goal);

    Task DischargeSATreatmentAsync(string episodeId, DateTime dischargeDate,
        GrainStates.SADischargeDisposition disposition, string? notes);
    Task ReopenSATreatmentAsync(string episodeId, string? notes);

    /// <summary>Creates a SA treatment visit and returns the visit ID.</summary>
    Task<string> CreateSAVisitAsync(
        string episodeId, DateTime visitDate,
        GrainStates.SAVisitType visitType, int? durationMinutes,
        string? udsResult, List<string>? udsSubstancesDetected,
        int? daysSinceLastUse, int? cravingLevel,
        string? providerId, string? providerName, string? notes);

    Task<GrainStates.SAVisitState> GetSAVisitAsync(string visitId);
    Task<List<GrainStates.SAVisitIndexEntry>> GetSAVisitsAsync(string episodeId);
    Task<int> GetSAVisitCountAsync(string episodeId);

    // ─── Pharmacy Point of Sale (RPMS ABSP — File #9002313, additive feature) ───

    /// <summary>Submits a POS claim (B1 billing) and returns the claim ID.</summary>
    Task<string> SubmitPosClaimAsync(
        string? prescriptionId,
        GrainStates.NcpdpTransactionType transactionType,
        string bin, string pcn, string ncpdpVersion,
        string? groupNumber, string? cardholderId, string? relationshipCode,
        string? insurerId, string? insurerName,
        string? ndc, string? drugName, decimal? quantityDispensed, int? daysSupply,
        DateTime? dateOfService,
        decimal? ingredientCostSubmitted, decimal? dispensingFeeSubmitted,
        decimal? usualAndCustomary,
        string? pharmacyNcpdpId, string? pharmacistName,
        string? prescriberNpi, string? prescriberName,
        string? originalClaimId);

    /// <summary>Records adjudication response on a POS claim.</summary>
    Task AdjudicatePosClaimAsync(string claimId,
        GrainStates.PosClaimStatus status,
        decimal? insurancePaidAmount, decimal? patientResponsibility,
        decimal? copayAmount, decimal? coinsuranceAmount, decimal? deductibleAmount,
        string? authorizationNumber,
        List<GrainStates.PosRejection>? rejections,
        List<GrainStates.DurMessage>? durMessages);

    /// <summary>Reverses a POS claim (B2 reversal).</summary>
    Task ReversePosClaimAsync(string claimId);

    Task<GrainStates.PharmacyPosClaimState> GetPosClaimAsync(string claimId);
    Task<List<GrainStates.PosClaimIndexEntry>> GetPosClaimsAsync();
    Task<List<GrainStates.PosClaimIndexEntry>> GetPosClaimsByStatusAsync(GrainStates.PosClaimStatus status);

    // ─── EPCS — E-Prescribing for Controlled Substances (21 CFR Part 1311, additive) ─

    /// <summary>Creates an EPCS e-prescription and returns the EPCS ID.</summary>
    Task<string> CreateEpcsPrescriptionAsync(
        string? prescriptionId,
        GrainStates.EpcsScriptTransactionType transactionType,
        string drugName, string? ndc, string deaSchedule,
        decimal quantity, int daysSupply, int refillsAuthorized,
        string? sig, string? diagnosisCode,
        string? prescriberNpi, string? prescriberDea, string? prescriberName,
        string? prescriberCredentialId,
        GrainStates.EpcsPharmacyDestination? destinationPharmacy);

    /// <summary>Signs an EPCS e-prescription with 2FA verification.</summary>
    Task SignEpcsPrescriptionAsync(string epcsId, GrainStates.EpcsSignatureRecord signature);

    /// <summary>Marks an EPCS e-prescription as transmitted.</summary>
    Task TransmitEpcsPrescriptionAsync(string epcsId, string? transmissionMessageId);

    /// <summary>Marks an EPCS e-prescription as acknowledged.</summary>
    Task AcknowledgeEpcsPrescriptionAsync(string epcsId);

    /// <summary>Cancels an EPCS e-prescription.</summary>
    Task CancelEpcsPrescriptionAsync(string epcsId, string userId, string? reason);

    Task<GrainStates.EpcsPrescriptionState> GetEpcsPrescriptionAsync(string epcsId);
    Task<List<GrainStates.EpcsPrescriptionIndexEntry>> GetEpcsPrescriptionsAsync();
    Task<List<GrainStates.EpcsPrescriptionIndexEntry>> GetEpcsPrescriptionsByStatusAsync(
        GrainStates.EpcsTransmissionStatus status);

    // ─── Spinal Cord Injury / Dysfunction Registry (VistA File #154) — SCIRPAU.m ─

    /// <summary>
    /// Enrolls this patient in the SCI/D registry.
    /// Creates the SCI patient grain and adds an entry to the singleton index.
    /// </summary>
    Task EnrollInSCIRegistryAsync(
        DateTime enrollmentDate,
        string? sciCenter,
        DateTime? dateOfInjuryOnset,
        GrainStates.SCIInjuryType injuryType,
        GrainStates.SCIEtiology etiology,
        string neurologicalLevelOfInjury,
        GrainStates.SCIAisGrade aisGrade,
        string? primaryDiagnosisCode,
        string? primaryDiagnosisDescription,
        string? enrollingProviderId,
        string? enrollingProviderName,
        string? primaryProviderId,
        string? primaryProviderName,
        GrainStates.SCIBladderManagement? bladderManagement,
        GrainStates.SCIBowelProgram? bowelProgram,
        GrainStates.SCILocomotionMethod? locomotionMethod,
        GrainStates.SCILivingSituation? livingSituation,
        List<string>? associatedConditions,
        string? notes);

    /// <summary>Updates the clinical data in the SCI registry record for this patient.</summary>
    Task UpdateSCIPatientAsync(
        string neurologicalLevelOfInjury,
        GrainStates.SCIAisGrade aisGrade,
        string? primaryDiagnosisCode,
        string? primaryDiagnosisDescription,
        GrainStates.SCIBladderManagement? bladderManagement,
        GrainStates.SCIBowelProgram? bowelProgram,
        GrainStates.SCILocomotionMethod? locomotionMethod,
        GrainStates.SCILivingSituation? livingSituation,
        List<string>? associatedConditions,
        string? primaryProviderId,
        string? primaryProviderName,
        string? notes);

    /// <summary>Updates the SCI registry enrollment status for this patient.</summary>
    Task UpdateSCIStatusAsync(GrainStates.SCIRegistryStatus status, string? notes);

    /// <summary>
    /// Adds an annual review or follow-up encounter to this patient's SCI registry record.
    /// Also updates the index grain with the latest NLI and AIS grade.
    /// Returns the new encounter ID.
    /// </summary>
    Task<string> AddSCIAnnualEncounterAsync(
        int fiscalYear,
        DateTime encounterDate,
        GrainStates.SCIEncounterType encounterType,
        GrainStates.SCIAisGrade aisGrade,
        string neurologicalLevel,
        int hospitalAdmissions,
        int urinaryTractInfections,
        int pressureInjuryCount,
        int highestPressureInjuryStage,
        GrainStates.SCIBladderManagement? bladderManagement,
        GrainStates.SCIBowelProgram? bowelProgram,
        GrainStates.SCILivingSituation? livingSituation,
        List<string>? equipmentNeeds,
        string? providerId,
        string? providerName,
        string? notes);

    /// <summary>Returns the full SCI registry record for this patient.</summary>
    Task<GrainStates.SCIPatientState> GetSCIPatientAsync();

    /// <summary>Returns all annual encounter records for this patient.</summary>
    Task<List<GrainStates.SCIAnnualEncounterRecord>> GetSCIAnnualEncountersAsync();

    // ═══════════════════════════════════════════════════════════════════════════
    // Blind Rehabilitation (VistA File #782) — ANRV.m, ANRUTIL.m, ANRVAD.m
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the patient's blind rehabilitation master record.
    /// Initializes the record if it does not yet exist.
    /// Corresponds to VistA ANRUTIL GETBR.
    /// </summary>
    Task<GrainStates.BRPatientState> GetBRPatientAsync();

    /// <summary>
    /// Records or updates the patient's visual acuity assessment.
    /// Corresponds to VistA Visual Acuity file (#783).
    /// </summary>
    Task RecordVisualAcuityAsync(
        string rightEyeDistance,
        string leftEyeDistance,
        string bestCorrectedRight,
        string bestCorrectedLeft,
        GrainStates.VisualField visualFieldRight,
        GrainStates.VisualField visualFieldLeft,
        string? contrastSensitivity,
        DateTime examDate,
        string examinerId,
        string examinerName,
        string? notes);

    /// <summary>
    /// Updates the patient's visual diagnosis.
    /// Corresponds to VistA File #782 field (.06).
    /// </summary>
    Task UpdateBRDiagnosisAsync(
        string primaryDiagnosis,
        string? secondaryDiagnosis,
        GrainStates.BROnsetType onsetType,
        DateTime? onsetDate,
        bool serviceConnected,
        int? serviceConnectedPercentage,
        string? icd10Code,
        string? notes);

    /// <summary>
    /// Issues an assistive device to the patient.
    /// Corresponds to VistA File #782 equipment sub-file.
    /// </summary>
    Task AddBRDeviceAsync(GrainStates.BRDeviceEntry device);

    /// <summary>Records a training goal for the patient.</summary>
    Task AddBRTrainingGoalAsync(string goal, GrainStates.BRTrainingArea area);

    /// <summary>Updates the patient's eligibility status for blind rehabilitation services.</summary>
    Task UpdateBREligibilityAsync(GrainStates.BREligibilityStatus eligibility, string? reason);

    /// <summary>
    /// Creates an inpatient blind rehabilitation admission referral.
    /// Corresponds to VistA ANRVAD CREATE.
    /// </summary>
    Task<string> CreateBRAdmissionAsync(
        string centerId,
        string centerName,
        DateTime admitDate,
        DateTime? plannedDischargeDate,
        List<GrainStates.BRTrainingArea> programAreas,
        GrainStates.BRAdmissionPriority priority,
        string referringProviderId,
        string referringProviderName,
        string? goals,
        string? notes);

    /// <summary>Returns all BR admissions for this patient.</summary>
    Task<List<GrainStates.BRAdmissionIndexEntry>> GetBRAdmissionsAsync();

    /// <summary>
    /// Schedules an outpatient blind rehabilitation training session.
    /// Corresponds to VistA ANRVOP CREATE.
    /// </summary>
    Task<string> ScheduleBROutpatientVisitAsync(
        DateTime visitDate,
        GrainStates.BRTrainingArea trainingArea,
        string therapistId,
        string therapistName,
        string location,
        int durationMinutes,
        string? sessionNotes,
        List<string> skillsAddressed);

    /// <summary>Returns all outpatient BR visits for this patient.</summary>
    Task<List<GrainStates.BROutpatientVisitIndexEntry>> GetBROutpatientVisitsAsync();

    // ═══════════════════════════════════════════════════════════════════════════
    // Home Telehealth / Remote Patient Monitoring (VistA Files #720–720.9)
    // MUMPS routines: HTPATIEN.m, HTMONREC.m, HTMEASUR.m, HTALERT.m
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the patient's Home Telehealth enrollment record.
    /// Corresponds to VistA HTPATIEN GET.
    /// </summary>
    Task<GrainStates.HomeTelehealthPatientState> GetHtPatientAsync();

    /// <summary>
    /// Enrolls the patient in the Home Telehealth program.
    /// Corresponds to VistA HTPATIEN ENROLL.
    /// </summary>
    Task EnrollInHomeTelehealthAsync(
        string? careCoordinatorId,
        string? careCoordinatorName,
        string? primaryCareProviderId,
        string? primaryCareProviderName,
        GrainStates.HtCareProtocol protocol,
        string? notes);

    /// <summary>
    /// Disenrolls the patient from the Home Telehealth program.
    /// Corresponds to VistA HTPATIEN DISENROLL.
    /// </summary>
    Task DisenrollFromHomeTelehealthAsync(string? reason);

    /// <summary>
    /// Assigns a device to this patient and updates the device inventory.
    /// Corresponds to VistA HTPATIEN ADDDEV.
    /// </summary>
    Task AssignHtDeviceAsync(string deviceId, string deviceName, GrainStates.HtDeviceType deviceType);

    /// <summary>
    /// Records the return of an assigned device and frees it in inventory.
    /// Corresponds to VistA HTPATIEN RETDEV.
    /// </summary>
    Task ReturnHtDeviceAsync(string deviceId);

    /// <summary>
    /// Replaces all alert threshold rules for this patient.
    /// Corresponds to VistA HTMONREC SETTHRESH.
    /// </summary>
    Task SetHtAlertThresholdsAsync(List<GrainStates.HtAlertThreshold> thresholds);

    /// <summary>
    /// Records a physiological measurement, checks thresholds, and generates an alert if needed.
    /// Corresponds to VistA HTMEASUR CREATE + HTALERT CHECK.
    /// </summary>
    Task<string> RecordHtReadingAsync(
        GrainStates.HtMeasurementType measurementType,
        decimal? value1,
        decimal? value2,
        string unit,
        DateTime readingDateTime,
        GrainStates.HtReadingSource source,
        string? deviceId,
        string? notes);

    /// <summary>
    /// Returns readings for this patient, with optional filters.
    /// Corresponds to VistA HTMONREC GETREADINGS.
    /// </summary>
    Task<List<GrainStates.HtReadingIndexEntry>> GetHtReadingsAsync(
        GrainStates.HtMeasurementType? measurementType,
        int? days,
        int maxResults);

    /// <summary>
    /// Records clinician review of a specific reading.
    /// Corresponds to VistA HTMONREC REVIEW.
    /// </summary>
    Task ReviewHtReadingAsync(string readingId, string reviewedById, string reviewedByName);

    /// <summary>
    /// Returns alerts for this patient, optionally filtered by status.
    /// Corresponds to VistA HTALERT GETLIST.
    /// </summary>
    Task<List<GrainStates.HtAlertIndexEntry>> GetHtAlertsAsync(GrainStates.HtAlertStatus? status);

    /// <summary>
    /// Acknowledges an alert — clinician has seen it and taken action.
    /// Corresponds to VistA HTALERT ACK.
    /// </summary>
    Task AcknowledgeHtAlertAsync(string alertId, string clinicianId, string clinicianName, string? clinicalResponse);

    /// <summary>
    /// Resolves an alert — clinical issue has been addressed.
    /// Corresponds to VistA HTALERT RESOLVE.
    /// </summary>
    Task ResolveHtAlertAsync(string alertId, string clinicianId, string clinicianName, string? clinicalResponse);

    /// <summary>
    /// Dismisses an alert — determined to be non-actionable.
    /// </summary>
    Task DismissHtAlertAsync(string alertId, string clinicianId, string clinicianName, string? clinicalResponse);

    // ═══════════════════════════════════════════════════════════════════════════
    // Event Capture (VistA Files #721, #724) — ECPEC.m, ECPEEN.m, ECPEWL.m
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a new Event Capture workload encounter for this patient.
    /// Creates the encounter grain, updates the per-patient EC index,
    /// and adds a summary to the application-wide encounter index.
    /// Returns the new encounter ID.
    /// Corresponds to VistA ECPEEN CREATE.
    /// </summary>
    Task<string> CreateEventCaptureEncounterAsync(
        DateTime encounterDateTime,
        string dssUnitId,
        string dssUnitName,
        string? dssUnitCode,
        string? clinicId,
        string? clinicName,
        string? locationId,
        string? locationName,
        string primaryProviderId,
        string primaryProviderName,
        string? attendingProviderId,
        string? attendingProviderName,
        GrainStates.EcEncounterType encounterType,
        GrainStates.EcPatientCategory patientCategory,
        string? primaryStopCode,
        string? creditStopCode,
        string? comments);

    /// <summary>
    /// Returns the full state of a single Event Capture encounter.
    /// </summary>
    Task<GrainStates.EventCaptureEncounterState> GetEventCaptureEncounterAsync(string encounterId);

    /// <summary>
    /// Returns Event Capture encounter summaries for this patient (newest first).
    /// Corresponds to VistA ECPEWL PATIENT.
    /// </summary>
    Task<List<GrainStates.EventCaptureIndexEntry>> GetEventCaptureEncountersAsync(int maxResults);

    /// <summary>
    /// Adds or replaces a CPT procedure entry on an existing encounter.
    /// Corresponds to VistA ECPEEN PROC.
    /// </summary>
    Task AddEcProcedureAsync(
        string encounterId,
        string cptCode,
        string procedureDescription,
        int quantity,
        string providerId,
        string providerName,
        string? modifierCode);

    /// <summary>
    /// Adds a diagnosis code to an existing encounter.
    /// </summary>
    Task AddEcDiagnosisAsync(
        string encounterId,
        string icd10Code,
        string description,
        bool isPrimary);

    /// <summary>
    /// Completes (checks out) an Event Capture encounter.
    /// Updates the encounter grain status and refreshes the index entry.
    /// Corresponds to VistA ECPEEN COMPLETE.
    /// </summary>
    Task CompleteEventCaptureEncounterAsync(
        string encounterId,
        DateTime checkOutDateTime,
        int? visitLengthMinutes);

    /// <summary>
    /// Soft-deletes an Event Capture encounter.
    /// Corresponds to VistA ECPEEN DELETE.
    /// </summary>
    Task DeleteEventCaptureEncounterAsync(
        string encounterId,
        string deletedByProviderId,
        string deletedByProviderName,
        string? reason);

    // ─── Health Summary (GMTS.m BUILD/PRINT, VistA File #142) ───────────────

    /// <summary>
    /// Generate a health summary for this patient using the specified template.
    /// Pulls live data from all enabled component grains and persists the report.
    /// Corresponds to VistA GMTS BUILD/PRINT routines.
    /// </summary>
    Task<string> GenerateHealthSummaryAsync(
        string typeId,
        string requestedById,
        string requestedByName);

    /// <summary>Get a previously generated health summary report by ID.</summary>
    Task<GrainStates.HealthSummaryState> GetHealthSummaryAsync(string reportId);

    /// <summary>List all generated health summaries for this patient (newest first).</summary>
    Task<List<GrainStates.HealthSummaryIndexEntry>> GetHealthSummaryListAsync();

    /// <summary>List generated health summaries for this patient filtered by template type.</summary>
    Task<List<GrainStates.HealthSummaryIndexEntry>> GetHealthSummaryByTypeAsync(string typeId);

    // ═══════════════════════════════════════════════════════════════════════════
    // Oncology / Tumor Registry (VistA Files #160-#165)
    // MUMPS routines: ONCRP.m, ONCS.m, ONCTREAT.m
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers a new tumor in the patient's oncology registry.
    /// Creates the tumor grain, adds to the patient index, and returns the new tumor ID.
    /// ONCRP.m REGISTER.
    /// </summary>
    Task<string> RegisterOncologyTumorAsync(
        string primarySite,
        string primarySiteText,
        string histology,
        string histologyText,
        GrainStates.TumorLaterality laterality,
        DateTime dateOfDiagnosis,
        GrainStates.DiagnosisBasis diagnosisBasis,
        int sequenceNumber,
        string? oncologistId,
        string? oncologistName);

    /// <summary>
    /// Records TNM staging (clinical and/or pathologic) and SEER summary stage for a tumor.
    /// ONCS.m STAGE.
    /// </summary>
    Task RecordOncologyStagingAsync(
        string tumorId,
        string? clinicalT,
        string? clinicalN,
        string? clinicalM,
        string? pathologicT,
        string? pathologicN,
        string? pathologicM,
        string? stageGroup,
        string? seerSummaryStage);

    /// <summary>Updates the disease status of a tumor (e.g. remission, recurrence, deceased). ONCRP.m STATUS.</summary>
    Task UpdateOncologyStatusAsync(
        string tumorId,
        GrainStates.OncologyStatus status,
        DateTime? statusChangeDate,
        string? notes);

    /// <summary>Records a disease recurrence with date and site. ONCRP.m RECUR.</summary>
    Task RecordOncologyRecurrenceAsync(
        string tumorId,
        DateTime recurrenceDate,
        string? recurrenceSite,
        string? notes);

    /// <summary>Returns the full state of a single tumor record.</summary>
    Task<GrainStates.OncologyTumorState> GetOncologyTumorAsync(string tumorId);

    /// <summary>Returns all tumor registry entries for this patient, ordered by diagnosis date descending.</summary>
    Task<List<GrainStates.OncologyTumorIndexEntry>> GetOncologyTumorsAsync();

    /// <summary>Returns only tumors with Active or Recurrence status.</summary>
    Task<List<GrainStates.OncologyTumorIndexEntry>> GetActiveOncologyTumorsAsync();

    /// <summary>
    /// Creates a new oncology treatment episode linked to a tumor.
    /// Adds the treatment ID to the tumor record and the patient treatment index.
    /// Returns the new treatment ID. ONCTREAT.m CREATE.
    /// </summary>
    Task<string> CreateOncologyTreatmentAsync(
        string tumorId,
        GrainStates.OncologyTreatmentType treatmentType,
        string agentName,
        string? doseDescription,
        string? providerId,
        string? providerName,
        string? facilityName,
        string? notes);

    /// <summary>Marks a treatment as Active with an actual start date. ONCTREAT.m START.</summary>
    Task StartOncologyTreatmentAsync(string treatmentId, DateTime startDate);

    /// <summary>Completes a treatment with end date and response assessment. ONCTREAT.m COMPLETE.</summary>
    Task CompleteOncologyTreatmentAsync(
        string treatmentId,
        DateTime endDate,
        GrainStates.TreatmentResponseAssessment responseAssessment,
        string? notes);

    /// <summary>Discontinues a treatment early with end date and reason. ONCTREAT.m DISCONTINUE.</summary>
    Task DiscontinueOncologyTreatmentAsync(
        string treatmentId,
        DateTime endDate,
        string discontinuationReason,
        string? notes);

    /// <summary>Records an interim response assessment without ending the treatment. ONCTREAT.m RESPONSE.</summary>
    Task RecordOncologyResponseAsync(
        string treatmentId,
        GrainStates.TreatmentResponseAssessment responseAssessment,
        DateTime assessmentDate,
        string? notes);

    /// <summary>Updates the number of cycles completed for a chemotherapy/immunotherapy treatment.</summary>
    Task UpdateOncologyCyclesAsync(string treatmentId, int cyclesCompleted);

    /// <summary>Returns the full state of a single treatment record.</summary>
    Task<GrainStates.OncologyTreatmentState> GetOncologyTreatmentAsync(string treatmentId);

    /// <summary>Returns all treatment episodes for this patient, ordered by start date descending.</summary>
    Task<List<GrainStates.OncologyTreatmentIndexEntry>> GetOncologyTreatmentsAsync();

    /// <summary>Returns treatment episodes linked to a specific tumor.</summary>
    Task<List<GrainStates.OncologyTreatmentIndexEntry>> GetOncologyTreatmentsByTumorAsync(string tumorId);

    // ─── Medicine (Procedures) — Files #691-699 — MDAPI.m, MDEV.m, MDEC.m ────────

    /// <summary>
    /// Orders a new Medicine procedure (ECG, Echo, PFT, Endoscopy, etc.) for this patient.
    /// Creates the procedure grain and updates the per-patient index.
    /// Returns the new procedure ID.
    /// MDAPI.m ORDER.
    /// </summary>
    Task<string> OrderMedProcedureAsync(
        GrainStates.MedProcedureCategory category,
        string procedureCode,
        string procedureDescription,
        DateTime orderedDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? indication);

    /// <summary>Schedules a Medicine procedure for a specific date/time.</summary>
    Task ScheduleMedProcedureAsync(string procedureId, DateTime scheduledDate);

    /// <summary>
    /// Completes a Medicine procedure with narrative findings and impression.
    /// Updates the procedure grain and refreshes the per-patient index.
    /// MDEV.m COMPLETE.
    /// </summary>
    Task CompleteMedProcedureAsync(
        string procedureId,
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes);

    /// <summary>Cancels a Medicine procedure with an optional reason.</summary>
    Task CancelMedProcedureAsync(string procedureId, string? reason);

    /// <summary>Records ECG measurements and interpretation for a procedure.</summary>
    Task RecordMedEcgResultsAsync(
        string procedureId,
        int? rate,
        GrainStates.CardiacRhythm? rhythm,
        int? prIntervalMs,
        int? qrsDurationMs,
        int? qtcMs,
        int? axisDegrees,
        string? interpretation,
        bool? isNormal);

    /// <summary>Records echocardiogram-specific results for a procedure.</summary>
    Task RecordMedEchoResultsAsync(
        string procedureId,
        decimal? lvEjectionFraction,
        string? lvDiastolicFunction,
        string? valvularFindings);

    /// <summary>Records stress test results for a procedure.</summary>
    Task RecordMedStressTestResultsAsync(
        string procedureId,
        decimal? peakMets,
        decimal? targetHeartRatePct,
        bool? inducibleIschemia);

    /// <summary>Records pulmonary function test (spirometry + lung volumes) results.</summary>
    Task RecordMedPftResultsAsync(
        string procedureId,
        decimal? fev1,
        decimal? fev1PctPredicted,
        decimal? fvc,
        decimal? fvcPctPredicted,
        decimal? fev1FvcRatio,
        decimal? dlco,
        decimal? dlcoPctPredicted,
        decimal? tlc,
        decimal? rv,
        bool? obstructive,
        bool? restrictive,
        bool? bronchodilatorResponse);

    /// <summary>Records arterial blood gas values for a procedure.</summary>
    Task RecordMedAbgResultsAsync(
        string procedureId,
        decimal? ph,
        decimal? pao2,
        decimal? paco2,
        decimal? hco3,
        decimal? sao2);

    /// <summary>Records GI/Endoscopy findings for a procedure.</summary>
    Task RecordMedEndoscopyResultsAsync(
        string procedureId,
        GrainStates.EndoscopyType endoscopyType,
        GrainStates.BowelPrepQuality? bowelPrepQuality,
        bool? cecumReached,
        int? scopeAdvancedCm,
        bool? biopsyTaken,
        List<string>? biopsySites,
        int? polypCount,
        List<string>? polypDescriptions,
        List<string>? endoscopicInterventions);

    /// <summary>Returns the full state of a single Medicine procedure record.</summary>
    Task<GrainStates.MedProcedureState> GetMedProcedureAsync(string procedureId);

    /// <summary>Returns all Medicine procedure summaries for this patient, ordered by ordered date descending.</summary>
    Task<List<GrainStates.MedProcedureIndexEntry>> GetMedProceduresAsync();

    /// <summary>Returns Medicine procedure summaries filtered by category (Cardiology, PFT, GI, ECG).</summary>
    Task<List<GrainStates.MedProcedureIndexEntry>> GetMedProceduresByCategoryAsync(GrainStates.MedProcedureCategory category);

    /// <summary>Returns only completed Medicine procedure summaries for this patient.</summary>
    Task<List<GrainStates.MedProcedureIndexEntry>> GetCompletedMedProceduresAsync();

    // ─── Clinical Procedures — File #702 ────────────────────────────────────────

    /// <summary>
    /// Orders a new clinical procedure (EEG, EMG, NCS, sleep study, audiometry, etc.).
    /// Returns the new procedure ID.
    /// </summary>
    Task<string> OrderClinicProcedureAsync(
        GrainStates.ClinicProcedureCategory category,
        string procedureCode,
        string procedureDescription,
        DateTime orderedDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? indication);

    /// <summary>Schedules a clinical procedure for a specific date/time.</summary>
    Task ScheduleClinicProcedureAsync(string procedureId, DateTime scheduledDate);

    /// <summary>Completes a clinical procedure with findings and impression.</summary>
    Task CompleteClinicProcedureAsync(
        string procedureId,
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes);

    /// <summary>Cancels a clinical procedure with an optional reason.</summary>
    Task CancelClinicProcedureAsync(string procedureId, string? reason);

    /// <summary>Records EEG results for a clinical procedure.</summary>
    Task RecordClinicEegResultsAsync(
        string procedureId,
        int? durationMinutes,
        string? background,
        GrainStates.EegAlertType? alertType,
        bool? seizureActivity,
        string? focalRegion,
        List<string>? activations);

    /// <summary>Records EMG results for a clinical procedure.</summary>
    Task RecordClinicEmgResultsAsync(
        string procedureId,
        List<string>? musclesStudied,
        GrainStates.EmgFindingType? findingType,
        string? spontaneousActivity,
        string? mupDescription);

    /// <summary>Records nerve conduction study (NCS) results for a clinical procedure.</summary>
    Task RecordClinicNcsResultsAsync(
        string procedureId,
        List<string>? nervesStudied,
        decimal? meanMotorVelocity,
        decimal? meanSensoryVelocity,
        bool? fWavesObtained,
        GrainStates.EmgFindingType? findingType);

    /// <summary>Records sleep study results for a clinical procedure.</summary>
    Task RecordClinicSleepStudyResultsAsync(
        string procedureId,
        GrainStates.SleepStudyType studyType,
        GrainStates.SleepApneaType? apneaType,
        decimal? apneaHypopneaIndex,
        decimal? cpapPressureCmH2O,
        decimal? sleepEfficiencyPct,
        int? totalSleepTimeMin,
        decimal? sleepLatencyMin,
        decimal? remLatencyMin);

    /// <summary>Records audiometry results for a clinical procedure.</summary>
    Task RecordClinicAudiometryResultsAsync(
        string procedureId,
        GrainStates.HearingLossType? hearingLossType,
        decimal? rightEarPta,
        decimal? leftEarPta,
        decimal? speechDiscriminationRight,
        decimal? speechDiscriminationLeft,
        string? tympanometryRight,
        string? tympanometryLeft);

    /// <summary>Returns the full state of a single clinical procedure record.</summary>
    Task<GrainStates.ClinicProcedureState> GetClinicProcedureAsync(string procedureId);

    /// <summary>Returns all clinical procedure summaries for this patient, ordered by ordered date descending.</summary>
    Task<List<GrainStates.ClinicProcedureIndexEntry>> GetClinicProceduresAsync();

    /// <summary>Returns clinical procedure summaries filtered by category.</summary>
    Task<List<GrainStates.ClinicProcedureIndexEntry>> GetClinicProceduresByCategoryAsync(GrainStates.ClinicProcedureCategory category);

    /// <summary>Returns only completed clinical procedure summaries for this patient.</summary>
    Task<List<GrainStates.ClinicProcedureIndexEntry>> GetCompletedClinicProceduresAsync();

    // ─── Radiation Therapy — File #135 ──────────────────────────────────────────

    /// <summary>
    /// Creates a new radiation therapy treatment course.
    /// Returns the new course ID.
    /// </summary>
    Task<string> CreateRtCourseAsync(
        string courseName,
        string diagnosisCode,
        string diagnosisText,
        string treatmentSite,
        GrainStates.RtLaterality laterality,
        GrainStates.RtIntent intent,
        GrainStates.RtModality modality,
        int prescribedDoseCgy,
        int fractionsPlanned,
        int dosePerFractionCgy,
        string? beamEnergy,
        string? oncologistId,
        string? oncologistName,
        string? physicistId,
        string? physicistName,
        string? dosimetristId,
        string? dosimetristName,
        string? treatmentMachineId,
        string? treatmentMachineName,
        string? planningNotes);

    /// <summary>Records CT simulation for an RT course.</summary>
    Task RecordRtSimulationAsync(string courseId, DateTime simulationDate, string? planningNotes);

    /// <summary>Marks an RT course as active (first treatment delivered).</summary>
    Task StartRtCourseAsync(string courseId, DateTime treatmentStartDate);

    /// <summary>Marks an RT course as completed.</summary>
    Task CompleteRtCourseAsync(string courseId, DateTime completionDate, string? notes);

    /// <summary>Discontinues an RT course.</summary>
    Task DiscontinueRtCourseAsync(string courseId, DateTime discontinuationDate, string reason, string? notes);

    /// <summary>Places an RT course on hold.</summary>
    Task PlaceRtCourseOnHoldAsync(string courseId, string? reason);

    /// <summary>Resumes an RT course from hold.</summary>
    Task ResumeRtCourseAsync(string courseId);

    /// <summary>Sets boost phase details for an RT course.</summary>
    Task SetRtBoostAsync(string courseId, string boostSite, int boostDoseCgy, int boostFractionsPlanned);

    /// <summary>Sets brachytherapy details for an RT course.</summary>
    Task SetRtBrachytherapyAsync(string courseId, GrainStates.BrachytherapyDoseRate doseRate, string? isotope);

    /// <summary>
    /// Records a delivered radiation therapy fraction.
    /// Updates both the per-course treatment index and the course cumulative dose.
    /// Returns the new treatment ID.
    /// </summary>
    Task<string> RecordRtFractionAsync(
        string courseId,
        int fractionNumber,
        DateTime treatmentDate,
        int doseDeliveredCgy,
        int? treatmentDurationMin,
        string? machineId,
        string? machineName,
        string? technicianId,
        string? technicianName,
        bool setupVerified,
        string? setupMethod,
        decimal? setupDeviationMm,
        bool interrupted,
        string? interruptionReason,
        string? notes);

    /// <summary>
    /// Records a skipped or cancelled fraction without updating dose totals.
    /// Returns the new treatment ID.
    /// </summary>
    Task<string> RecordRtSkippedFractionAsync(
        string courseId,
        int fractionNumber,
        DateTime scheduledDate,
        GrainStates.RtFractionStatus status,
        string? skipReason);

    /// <summary>Returns the full state of a single RT course.</summary>
    Task<GrainStates.RtCourseState> GetRtCourseAsync(string courseId);

    /// <summary>Returns all RT course summaries for this patient.</summary>
    Task<List<GrainStates.RtCourseIndexEntry>> GetRtCoursesAsync();

    /// <summary>Returns active/on-hold RT courses for this patient.</summary>
    Task<List<GrainStates.RtCourseIndexEntry>> GetActiveRtCoursesAsync();

    /// <summary>Returns all fraction records for a given RT course.</summary>
    Task<List<GrainStates.RtTreatmentIndexEntry>> GetRtCourseTreatmentsAsync(string courseId);

    /// <summary>Returns only delivered fractions for a given RT course.</summary>
    Task<List<GrainStates.RtTreatmentIndexEntry>> GetRtDeliveredFractionsAsync(string courseId);

    // ─── IV Pharmacy — Files #50.8, #53.4 — PSJIV.m, PSJVXU.m, PSJLBL.m ────────

    /// <summary>
    /// Creates a new IV admixture compounding order for this patient.
    /// Creates the order grain and updates the per-patient IV order index.
    /// Returns the new order ID.
    /// PSJIV.m ORDER.
    /// </summary>
    Task<string> CreateIVAdmixOrderAsync(
        string baseSolution,
        int baseSolutionVolumeMl,
        GrainStates.IVAdmixRoute route,
        GrainStates.IVAdmixFrequency frequency,
        GrainStates.IVContainerType containerType,
        int containerCount,
        GrainStates.IVAdmixPriority priority,
        string? linkedInpatientOrderId,
        string? infusionRateStr,
        decimal? infusionRateMlHr,
        decimal? infusionDurationHours,
        string? routeDescription,
        string? frequencyDescription,
        DateTime? startDateTime,
        DateTime? stopDateTime,
        string? providerId,
        string? providerName,
        string? notes);

    /// <summary>Adds a drug additive or base solution to an existing IV admixture order.</summary>
    Task AddIVAdmixAdditiveAsync(string orderId, GrainStates.IVAdmixAdditive additive);

    /// <summary>Removes a drug additive by drug name from an IV admixture order.</summary>
    Task RemoveIVAdmixAdditiveAsync(string orderId, string drugName);

    /// <summary>
    /// Pharmacist verification of the IV order.
    /// PSJVXU.m VERIFY. Status → Verified.
    /// </summary>
    Task VerifyIVAdmixOrderAsync(string orderId, string pharmacistId, string pharmacistName, DateTime verifiedDate);

    /// <summary>
    /// Marks the IV order as in compounding.
    /// Status → Compounding.
    /// </summary>
    Task StartIVAdmixCompoundingAsync(string orderId, string compoundedById, string compoundedByName, DateTime startDate);

    /// <summary>
    /// Completes compounding of the IV admixture and assigns lot/expiration.
    /// PSJLBL.m COMPLETE. Status → Ready.
    /// </summary>
    Task CompleteIVAdmixCompoundingAsync(string orderId, DateTime completedDate, string? lotNumber, DateTime? expirationDate);

    /// <summary>Records that the IV label was printed for an order. PSJLBL.m PRINT.</summary>
    Task PrintIVAdmixLabelAsync(string orderId, string printedBy, DateTime printedDate);

    /// <summary>Records dispensing of the admixture to the ward. Status → Dispensed.</summary>
    Task DispenseIVAdmixOrderAsync(string orderId, DateTime dispensingDateTime);

    /// <summary>Records administration of the admixture to the patient. Status → Administered.</summary>
    Task RecordIVAdmixAdministrationAsync(string orderId, DateTime administrationDateTime);

    /// <summary>Discontinues an IV admixture order with a reason. Status → Discontinued.</summary>
    Task DiscontinueIVAdmixOrderAsync(string orderId, string reason);

    /// <summary>Cancels an IV admixture order with a reason. Status → Cancelled.</summary>
    Task CancelIVAdmixOrderAsync(string orderId, string reason);

    /// <summary>Returns the full state of a single IV admixture order.</summary>
    Task<GrainStates.IVAdmixOrderState> GetIVAdmixOrderAsync(string orderId);

    /// <summary>Returns all IV admixture order summaries for this patient (newest first).</summary>
    Task<List<GrainStates.IVAdmixOrderIndexEntry>> GetIVAdmixOrdersAsync();

    /// <summary>Returns IV admixture orders with Pending or Verified status.</summary>
    Task<List<GrainStates.IVAdmixOrderIndexEntry>> GetPendingIVAdmixOrdersAsync();

    /// <summary>Returns IV admixture orders currently being compounded or ready for dispensing.</summary>
    Task<List<GrainStates.IVAdmixOrderIndexEntry>> GetActiveIVAdmixOrdersAsync();

    // ──────────────────────────────────────────────────────────────────────────
    // Compensation & Pension — VistA File #396 (DVBAB5.m, DVBABEXT.m)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules a C&amp;P exam for this patient linked to a VBA claim.
    /// Returns the new exam ID.
    /// </summary>
    Task<string> ScheduleCPExamAsync(
        string patientName,
        GrainStates.CPExamType examType,
        DateTime scheduledDate,
        string examinerName,
        string examinerTitle,
        GrainStates.CPExaminerType examinerType,
        string examLocation,
        string examFacility,
        string claimNumber,
        string benefitType,
        List<string> disabilityClaimedCodes,
        string createdBy);

    /// <summary>Records completion of a C&amp;P exam with diagnoses and nexus opinion.</summary>
    Task CompleteCPExamAsync(string examId, List<string> diagnoses, bool nexus, string nexusRationale);

    /// <summary>Cancels a C&amp;P exam with a stated reason.</summary>
    Task CancelCPExamAsync(string examId, string cancellationReason);

    /// <summary>Reschedules a C&amp;P exam to a new date.</summary>
    Task RescheduleCPExamAsync(string examId, DateTime newScheduledDate, string reason);

    /// <summary>
    /// Creates a new DBQ document linked to a C&amp;P exam.
    /// Returns the new DBQ ID.
    /// </summary>
    Task<string> CreateDBQAsync(
        string examId,
        string patientName,
        GrainStates.DBQType dbqType,
        string dbqFormNumber,
        string dbqTitle,
        string claimNumber,
        string conditionClaimed,
        string diagnosisCode,
        string diagnosisDescription);

    /// <summary>Updates the clinical narrative sections of a DBQ.</summary>
    Task UpdateDBQSectionsAsync(
        string dbqId,
        string historySection,
        string symptomsSection,
        string functionalImpactSection,
        string rangeOfMotionSection,
        string mentalStatusSection,
        string diagnosticTestsSection);

    /// <summary>Records the examiner's nexus and service-connection opinion on a DBQ.</summary>
    Task RecordDBQOpinionAsync(
        string dbqId,
        bool nexusOpinion,
        string nexusStatement,
        string opinionsSection,
        GrainStates.ServiceConnectionType serviceConnectionType,
        bool residualsPermanent,
        bool expectedImprovement);

    /// <summary>Sets the proposed disability rating percentage on a DBQ.</summary>
    Task SetDBQRatingAsync(string dbqId, int proposedRating);

    /// <summary>Marks a DBQ as clinically complete and updates index. Status → Completed.</summary>
    Task CompleteDBQAsync(string dbqId);

    /// <summary>Signs a DBQ and links it to its parent exam. Status → Signed.</summary>
    Task SignDBQAsync(string dbqId, string signedBy);

    /// <summary>Returns the full state of a single C&amp;P exam.</summary>
    Task<GrainStates.CPExamState> GetCPExamAsync(string examId);

    /// <summary>Returns all C&amp;P exam summaries for this patient, newest first.</summary>
    Task<List<GrainStates.CPExamIndexEntry>> GetCPExamsAsync();

    /// <summary>Returns scheduled and rescheduled C&amp;P exams for this patient.</summary>
    Task<List<GrainStates.CPExamIndexEntry>> GetScheduledCPExamsAsync();

    /// <summary>Returns completed C&amp;P exams for this patient.</summary>
    Task<List<GrainStates.CPExamIndexEntry>> GetCompletedCPExamsAsync();

    /// <summary>Returns the full state of a single DBQ document.</summary>
    Task<GrainStates.DBQState> GetDBQAsync(string dbqId);

    /// <summary>Returns all DBQ summaries for this patient.</summary>
    Task<List<GrainStates.DBQIndexEntry>> GetDBQsAsync();

    /// <summary>Returns all DBQs linked to a specific exam.</summary>
    Task<List<GrainStates.DBQIndexEntry>> GetDBQsForExamAsync(string examId);

    // ─── Lexicon Utility (File #757, LEX*.m) ─────────────────────────────

    /// <summary>
    /// Search the Lexicon for clinical terms across SNOMED, ICD-10, CPT, LOINC.
    /// Mirrors LEX^LEX10 (Lexicon lookup used by Problem List, PCE, Orders).
    /// </summary>
    Task<List<GrainStates.LexiconIndexEntry>> SearchLexiconAsync(
        string searchText, string? codingSystem, int maxResults);

    /// <summary>
    /// Lookup a single code in a specific terminology.
    /// </summary>
    Task<GrainStates.LexiconIndexEntry?> LookupLexiconCodeAsync(string code, string codingSystem);

    // ─── Master Patient Index (File #985, MPIF001.m) ─────────────────────

    /// <summary>
    /// Get this patient's MPI correlation record (ICN, treating facilities).
    /// Mirrors MPIF001.m GETCOR.
    /// </summary>
    Task<GrainStates.MpiCorrelationState> GetMpiCorrelationAsync();

    /// <summary>
    /// Set this patient's MPI correlation (ICN link).
    /// Mirrors MPIF001.m SETCOR.
    /// </summary>
    Task SetMpiCorrelationAsync(string icn, string ssn, DateTime? dateOfBirth, string? sex);

    /// <summary>
    /// Get this patient's MPI treating facility correlations.
    /// Mirrors MPIF001.m GETCOR treating facility section.
    /// </summary>
    Task<List<GrainStates.MpiLocalCorrelation>> GetMpiTreatingFacilitiesAsync();

    // ─── Lab EDI (Reference Lab Orders) ──────────────────────────────────

    /// <summary>
    /// Get all Lab EDI orders for this patient.
    /// </summary>
    Task<List<GrainStates.LabEdiOrderSummary>> GetLabEdiOrdersAsync(int maxResults);

    // ─── Surgery Depth (File #130 Phase 4) ────────────────────────────────

    /// <summary>Record a pre-operative assessment for a surgery case.</summary>
    Task RecordPreOpAssessmentAsync(string surgeryId, string notes, string providerId, string providerName);

    /// <summary>Add a complication record to a surgery case.</summary>
    Task AddSurgicalComplicationAsync(string surgeryId, string complicationType, string description, string? severity, string? treatment);

    /// <summary>Add an implant/device record to a surgery case.</summary>
    Task AddSurgicalImplantAsync(string surgeryId, string implantName, string? manufacturer, string? serialNumber, string? lotNumber);

    /// <summary>Add a surgical assistant to a surgery case with optional role.</summary>
    Task AddSurgicalAssistantAsync(string surgeryId, string assistantId, string assistantName, string? role);

    /// <summary>Record intra-operative details including counts and blood loss.</summary>
    Task RecordIntraOpDetailsAsync(string surgeryId, int? estimatedBloodLoss, int? spongeCountCorrect, int? needleCountCorrect, int? instrumentCountCorrect, string? dispositionAfterSurgery);

    /// <summary>Add an anesthesia agent to a surgery case.</summary>
    Task AddAnesthesiaAgentAsync(string surgeryId, string agent);

    /// <summary>Add a surgical specimen to a surgery case.</summary>
    Task AddSurgicalSpecimenAsync(string surgeryId, string specimenType, string? description, string? pathologyResult);

    /// <summary>Get the list of complications for a surgery case.</summary>
    Task<List<GrainStates.SurgicalComplication>> GetSurgicalComplicationsAsync(string surgeryId);

    // ─── Radiology Depth (File #75.1 Phase 4) ─────────────────────────────

    /// <summary>Record contrast agent administration for a radiology study.</summary>
    Task RecordRadiologyContrastAsync(string studyId, string contrastAgent, string? route, double? volumeMl);

    /// <summary>Record a contrast reaction for a radiology study.</summary>
    Task RecordRadiologyContrastReactionAsync(string studyId, string reactionDetails);

    /// <summary>Record radiation dose metrics for a radiology study.</summary>
    Task RecordRadiationDoseAsync(string studyId, double? doseMSv, double? ctdiVol, double? doseLengthProduct);

    /// <summary>Sign the radiology report.</summary>
    Task SignRadiologyReportAsync(string studyId, string signedById, string signedByName);

    /// <summary>Flag a radiology study as having a critical result.</summary>
    Task FlagCriticalRadiologyResultAsync(string studyId);

    /// <summary>Record that a critical result notification was sent.</summary>
    Task RecordCriticalResultNotificationAsync(string studyId, string notifiedTo);

    /// <summary>Acknowledge receipt of a critical radiology result.</summary>
    Task AcknowledgeCriticalResultAsync(string studyId, string acknowledgedBy);

    /// <summary>Amend a radiology report with additional text.</summary>
    Task AmendRadiologyReportAsync(string studyId, string amendmentText);

    /// <summary>Check whether a radiology study has a critical result flag.</summary>
    Task<bool> IsRadiologyCriticalResultAsync(string studyId);

    // ─── Consults Depth (File #123 Phase 4) ────────────────────────────────

    /// <summary>Add a tracking comment to a consult.</summary>
    Task AddConsultTrackingCommentAsync(string consultId, string authorId, string authorName, string commentText, string? actionTaken);

    /// <summary>Accept a consult with provider details.</summary>
    Task AcceptConsultWithDetailsAsync(string consultId, string acceptedById, string acceptedByName);

    /// <summary>Schedule a consult with date/time and optional clinic.</summary>
    Task ScheduleConsultWithDetailsAsync(string consultId, DateTime scheduledDateTime, string? clinicId, string? clinicName);

    /// <summary>Set the consult type (e.g., PROC, CONSULT).</summary>
    Task SetConsultTypeAsync(string consultId, string consultType);

    /// <summary>Set the clinical history for a consult.</summary>
    Task SetConsultClinicalHistoryAsync(string consultId, string clinicalHistory);

    /// <summary>Set a follow-up recommendation for a consult.</summary>
    Task SetConsultFollowUpRecommendationAsync(string consultId, string recommendation);

    /// <summary>Set the consulting provider for a consult.</summary>
    Task SetConsultingProviderAsync(string consultId, string providerId, string providerName);

    /// <summary>Mark a consult as interfacility with the external facility.</summary>
    Task MarkConsultInterfacilityAsync(string consultId, string externalFacilityId, string externalFacilityName);

    /// <summary>Get all tracking comments for a consult.</summary>
    Task<List<GrainStates.ConsultTrackingComment>> GetConsultTrackingCommentsAsync(string consultId);

    // ─── Mental Health Depth (File #601.71 Phase 4) ────────────────────────

    /// <summary>Record a risk assessment for a mental health instrument administration.</summary>
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "CREATE", EntityType = "MH_SCREEN", IsClinicalWrite = true)]
    Task RecordMentalHealthRiskAssessmentAsync(string instrumentId, int riskLevel, string? riskNotes);

    /// <summary>Set follow-up requirements for a mental health instrument.</summary>
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "UPDATE", EntityType = "MH_SCREEN")]
    Task SetMentalHealthFollowUpAsync(string instrumentId, bool requiresFollowUp, DateTime? followUpDueDate, string? followUpPlan);

    /// <summary>Add an individual item response to a mental health instrument.</summary>
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "CREATE", EntityType = "MH_SCREEN")]
    Task AddMentalHealthItemResponseAsync(string instrumentId, int itemNumber, string questionText, int responseValue, string? responseText);

    /// <summary>Auto-score a mental health instrument based on item responses.</summary>
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "UPDATE", EntityType = "MH_SCREEN", IsClinicalWrite = true)]
    Task ScoreMentalHealthInstrumentAsync(string instrumentId);

    /// <summary>Set the previous score for trending comparison.</summary>
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "UPDATE", EntityType = "MH_SCREEN")]
    Task SetMentalHealthPreviousScoreAsync(string instrumentId, decimal previousScore, DateTime previousDate);

    /// <summary>Calculate the score change between current and previous administration.</summary>
    [RequiresSecurityKey(SecurityKeys.YS_MH_INSTRUMENT)]
    [AuditAction("MENTAL_HEALTH", "UPDATE", EntityType = "MH_SCREEN")]
    Task<decimal?> CalculateMentalHealthScoreChangeAsync(string instrumentId);

    // ─── Immunization Depth (File #9000010.11 Phase 5) ──────────────────────

    /// <summary>Mark an immunization as historical (not administered at this facility).</summary>
    Task MarkImmunizationHistoricalAsync(string immunizationId, string informationSource);

    /// <summary>Record Vaccine Information Statement (VIS) dates for an immunization.</summary>
    Task RecordImmunizationVISAsync(string immunizationId, DateTime visDateOffered, DateTime visDatePublished);

    /// <summary>Set the series/dose information for an immunization.</summary>
    Task SetImmunizationSeriesInfoAsync(string immunizationId, int doseNumber, int dosesInSeries, bool seriesComplete);

    /// <summary>Set administration site and route details for an immunization.</summary>
    Task SetImmunizationAdministrationDetailsAsync(string immunizationId, string site, string route);

    /// <summary>Set the vaccine group classification for an immunization.</summary>
    Task SetImmunizationVaccineGroupAsync(string immunizationId, string groupName, string groupCode);

    /// <summary>Set the manufacturer name and code for an immunization.</summary>
    Task SetImmunizationManufacturerAsync(string immunizationId, string name, string code);

    /// <summary>Update the immunization registry reporting status.</summary>
    Task UpdateImmunizationRegistryStatusAsync(string immunizationId, string status);

    /// <summary>Add a comment to an immunization record.</summary>
    Task AddImmunizationCommentAsync(string immunizationId, string authorName, string commentText);

    /// <summary>Get all comments for an immunization record.</summary>
    Task<List<GrainStates.ImmunizationComment>> GetImmunizationCommentsAsync(string immunizationId);

    // ─── Health Factor Depth (File #9000010.23 Phase 5) ─────────────────────

    /// <summary>Update the severity level for a health factor.</summary>
    Task UpdateHealthFactorSeverityAsync(string healthFactorId, string severityLevel);

    /// <summary>Set category and optional subcategory for a health factor.</summary>
    Task SetHealthFactorCategoryAsync(string healthFactorId, string category, string? subcategory);

    /// <summary>Set the recorded value and optional magnitude for a health factor.</summary>
    Task SetHealthFactorValueAsync(string healthFactorId, string value, string? magnitude);

    /// <summary>Resolve a health factor (mark as no longer active).</summary>
    Task ResolveHealthFactorAsync(string healthFactorId, string resolvedByName);

    /// <summary>Reactivate a previously resolved health factor.</summary>
    Task ReactivateHealthFactorAsync(string healthFactorId);

    /// <summary>Add a historical tracking entry to a health factor.</summary>
    Task AddHealthFactorHistoryEntryAsync(string healthFactorId, string value, string? severityLevel, string? comment, string? recordedByName);

    // ─── SC Condition Depth (File #2.04 Phase 5) ────────────────────────────

    /// <summary>Set the service-connected disability percentage for a condition.</summary>
    Task SetServiceConnectedPercentageAsync(string conditionId, int percentage);

    /// <summary>Record a VA rating decision date and optional decision ID.</summary>
    Task RecordScRatingDecisionAsync(string conditionId, DateTime decisionDate, string? decisionId);

    /// <summary>Add a rated disability to a service-connected condition record.</summary>
    Task AddRatedDisabilityAsync(string conditionId, string conditionName, int ratingPercentage, DateTime effectiveDate, string? diagnosticCode, bool isStatic);

    /// <summary>Calculate the VA combined disability rating from all rated disabilities.</summary>
    Task CalculateScCombinedRatingAsync(string conditionId);

    /// <summary>Set the appeal status and optional filing date for a condition.</summary>
    Task SetScAppealStatusAsync(string conditionId, string status, DateTime? appealFiledDate);

    /// <summary>Record a C&amp;P or rating exam for a service-connected condition.</summary>
    Task RecordScExamAsync(string conditionId, DateTime examDate, string? examiningFacility, DateTime? nextExamDueDate);

    /// <summary>Set the permanent and total disability flag for a condition.</summary>
    Task SetScPermanentAndTotalAsync(string conditionId, bool isPermanentAndTotal);

    /// <summary>Add a clinical note to a service-connected condition record.</summary>
    Task AddScConditionNoteAsync(string conditionId, string authorName, string noteText);

    /// <summary>Remove a rated disability from a service-connected condition record.</summary>
    Task RemoveScRatedDisabilityAsync(string conditionId, string conditionName);

    /// <summary>Set the Special Monthly Compensation level for a service-connected condition.</summary>
    Task SetScSpecialMonthlyCompensationAsync(string conditionId, string? smcLevel);

    /// <summary>Get all rated disabilities for a service-connected condition.</summary>
    Task<List<GrainStates.RatedDisability>> GetScRatedDisabilitiesAsync(string conditionId);

    // ─── Means Test Depth (File #408.31 Phase 5) ────────────────────────────

    /// <summary>Record veteran, spouse, and dependent gross income for a means test.</summary>
    Task RecordMeansTestIncomeAsync(string meansTestId, decimal veteranGrossIncome, decimal? spouseGrossIncome, decimal? dependentIncome);

    /// <summary>Record asset information for a means test.</summary>
    Task RecordMeansTestAssetsAsync(string meansTestId, decimal? totalNetWorth, decimal? propertyValue, decimal? otherAssets);

    /// <summary>Record deductible expenses for a means test.</summary>
    Task RecordMeansTestExpensesAsync(string meansTestId, decimal deductibleExpenses);

    /// <summary>Calculate adjusted income from recorded income and expenses.</summary>
    Task CalculateMeansTestAdjustedIncomeAsync(string meansTestId);

    /// <summary>Set the Geographic Means Test threshold for a means test.</summary>
    Task SetMeansTestGmtThresholdAsync(string meansTestId, decimal gmtThreshold);

    /// <summary>Determine financial hardship status for a means test.</summary>
    Task DetermineMeansTestHardshipAsync(string meansTestId, string determination);

    /// <summary>Set the copay test result for a means test.</summary>
    Task SetMeansTestCopayResultAsync(string meansTestId, string result);

    /// <summary>Add a dependent to a means test record.</summary>
    Task AddMeansTestDependentAsync(string meansTestId, string name, string relationship, decimal income, decimal netWorth, DateTime? dateOfBirth);

    /// <summary>Get all dependents for a means test record.</summary>
    Task<List<GrainStates.MeansTestDependent>> GetMeansTestDependentsAsync(string meansTestId);

    // ─── Prosthetics Depth (File #669.1 Phase 5) ────────────────────────────

    /// <summary>Set the HCPCS code for a prosthetics item.</summary>
    Task SetProstheticsHcpcsCodeAsync(string prostheticsId, string hcpcsCode);

    /// <summary>Record cost and vendor information for a prosthetics item.</summary>
    Task RecordProstheticsCostAsync(string prostheticsId, decimal cost, string? vendorName, string? vendorId);

    /// <summary>Set the warranty period for a prosthetics item.</summary>
    Task SetProstheticsWarrantyAsync(string prostheticsId, DateTime startDate, DateTime endDate);

    /// <summary>Record delivery details for a prosthetics item.</summary>
    Task RecordProstheticsDeliveryAsync(string prostheticsId, DateTime deliveryDate, string deliveryMethod, string? trackingNumber);

    /// <summary>Record fitting details for a prosthetics item.</summary>
    Task RecordProstheticsFittingAsync(string prostheticsId, string fittingNotes, string fittedByName);

    /// <summary>Record patient satisfaction rating for a prosthetics item.</summary>
    Task RecordProstheticsSatisfactionAsync(string prostheticsId, int rating);

    /// <summary>Schedule the next maintenance date for a prosthetics item.</summary>
    Task ScheduleProstheticsMaintenanceAsync(string prostheticsId, DateTime nextDate, string? notes);

    /// <summary>Add a maintenance record for a prosthetics item.</summary>
    Task AddProstheticsMaintenanceRecordAsync(string prostheticsId, string maintenanceType, string? technicianName, string? notes, decimal? cost);

    /// <summary>Get the maintenance history for a prosthetics item.</summary>
    Task<List<GrainStates.ProstheticsMaintenanceRecord>> GetProstheticsMaintenanceHistoryAsync(string prostheticsId);

    // ─── Dietetics Depth (File #115.2 Phase 5) ─────────────────────────────

    /// <summary>Set calorie target and target weight nutrition goals for a diet order.</summary>
    Task SetDietNutritionGoalsAsync(string dietOrderId, int? calorieTarget, decimal? targetWeight);

    /// <summary>Set the fluid restriction in mL for a diet order.</summary>
    Task SetDietFluidRestrictionAsync(string dietOrderId, int? fluidRestrictionMl);

    /// <summary>Set the food texture and consistency level for a diet order.</summary>
    Task SetDietTextureConsistencyAsync(string dietOrderId, string textureConsistency);

    /// <summary>Set tube feeding parameters for a diet order.</summary>
    Task SetDietTubeFeedingAsync(string dietOrderId, bool isTubeFeeding, string? formula, decimal? rateMlHr);

    /// <summary>Set NPO (nothing by mouth) status with optional date range for a diet order.</summary>
    Task SetDietNPOAsync(string dietOrderId, bool isNPO, DateTime? startDate, DateTime? endDate);

    /// <summary>Record meal preferences for a diet order.</summary>
    Task RecordDietMealPreferenceAsync(string dietOrderId, string preferences);

    /// <summary>Record a nutrition assessment score for a diet order.</summary>
    Task RecordDietNutritionAssessmentAsync(string dietOrderId, decimal score, string assessedByName);

    /// <summary>Record the current BMI for a diet order.</summary>
    Task RecordDietBMIAsync(string dietOrderId, decimal bmi);

    /// <summary>Set allergy considerations text for a diet order.</summary>
    Task SetDietAllergyConsiderationsAsync(string dietOrderId, string allergyConsiderations);

    /// <summary>Get the modification history for a diet order.</summary>
    Task<List<GrainStates.DietModificationEntry>> GetDietModificationHistoryAsync(string dietOrderId);

    // ─── Imaging Depth (File #2005 Phase 5) ─────────────────────────────────

    /// <summary>Record DICOM metadata (study, series, instance UIDs, modality, body part, transfer syntax).</summary>
    Task RecordImagingDicomMetadataAsync(string imageId, string? studyUid, string? seriesUid, string? instanceUid, string? modality, string? bodyPart, string? transferSyntax);

    /// <summary>Set pixel dimensions and file size information for an image.</summary>
    Task SetImagingDimensionsAsync(string imageId, int width, int height, long? fileSizeBytes, string? compressionType);

    /// <summary>Set the clinical display status for an image (VIEWABLE, NEEDS_REVIEW, RESTRICTED, DELETED).</summary>
    Task SetImagingClinicalDisplayStatusAsync(string imageId, string status);

    /// <summary>Link an image to a clinical package (RADIOLOGY, SURGERY, etc.).</summary>
    Task LinkImagingToPackageAsync(string imageId, string packageType, string packageReference);

    /// <summary>Record acquisition details for an image.</summary>
    Task RecordImagingAcquisitionAsync(string imageId, string? acquisitionSite, DateTime acquisitionDateTime, string? patientOrientation);

    /// <summary>Add an annotation overlay to an image.</summary>
    Task AddImagingAnnotationAsync(string imageId, string annotationType, string content, string? authorName);

    /// <summary>Add series information for a multi-series imaging study.</summary>
    Task AddImagingSeriesInfoAsync(string imageId, string seriesUid, string? seriesDescription, string? modality, int imageCount, int? seriesNumber);

    // ─── Security / Patient Access (DG SENSITIVITY, XUSEC) ───────────────

    /// <summary>
    /// Set the patient's sensitive record flags and categories.
    /// Updates both the PAC grain and the patient grain's quick-lookup fields.
    /// </summary>
    Task SetPatientSensitivityAsync(bool isSensitive, string sensitivityLevel, List<string> categories);

    /// <summary>
    /// Check whether a user has access to this patient's record.
    /// Returns true if patient is not sensitive or user is an authorized provider.
    /// </summary>
    Task<bool> CheckPatientAccessAsync(string userId);

    /// <summary>
    /// Record a patient record access event (including break-the-glass).
    /// </summary>
    Task RecordPatientAccessAsync(string userId, string userName, string accessReason, bool wasBreakTheGlass, string? justificationText);

    /// <summary>
    /// Add a provider to the patient's authorized access list (treating team).
    /// </summary>
    Task AddAuthorizedProviderAsync(string providerId);

    /// <summary>
    /// Remove a provider from the patient's authorized access list.
    /// </summary>
    Task RemoveAuthorizedProviderAsync(string providerId);

    /// <summary>
    /// Get the patient access audit log.
    /// </summary>
    Task<List<GrainStates.PatientAccessLog>> GetPatientAccessLogAsync();

    // ─── Care Team (PCMM File #404.43) ─────────────────────────────────────

    /// <summary>
    /// Add a provider to the patient's care team. Idempotent — updates if already exists.
    /// Also syncs the provider's patient index and grants access for sensitive records.
    /// </summary>
    Task AddCareTeamMemberAsync(string providerId, string providerName, string role,
        string? specialty, string assignmentSource, DateTime? expirationDate);

    /// <summary>
    /// Remove a provider from the care team. Deactivates (does not delete) for audit trail.
    /// </summary>
    Task RemoveCareTeamMemberAsync(string providerId);

    /// <summary>
    /// Set the Primary Care Provider for this patient. Only one PCP can be active at a time.
    /// </summary>
    Task SetPcpAsync(string providerId, string providerName, string? specialty);

    /// <summary>
    /// Get the current PCP for this patient, or null if none assigned.
    /// </summary>
    Task<GrainStates.CareTeamMember?> GetPcpAsync();

    /// <summary>
    /// Get all care team members (active and inactive).
    /// </summary>
    Task<List<GrainStates.CareTeamMember>> GetCareTeamAsync();

    /// <summary>
    /// Get only active (non-expired) care team members.
    /// </summary>
    Task<List<GrainStates.CareTeamMember>> GetActiveCareTeamAsync();

    /// <summary>
    /// Check whether a provider is an active member of this patient's care team.
    /// </summary>
    Task<bool> IsOnCareTeamAsync(string providerId);

    /// <summary>
    /// Get active care team members eligible for secure messaging — used to populate the "To:" dropdown.
    /// </summary>
    Task<List<GrainStates.CareTeamMember>> GetCareTeamForMessagingAsync();

    // ─── DS4P — Data Segmentation for Privacy (§170.315(b)(7)/(b)(8)) ────

    /// <summary>
    /// Generate a C-CDA document with DS4P security tags for sensitive data.
    /// §170.315(b)(7) — Security tags — summary of care — send.
    /// </summary>
    Task<string> GenerateDs4pCcdaAsync(string documentType, List<string> sensitivityCategories);

    /// <summary>
    /// Analyze a received C-CDA document for DS4P security tags.
    /// §170.315(b)(8) — Security tags — summary of care — receive.
    /// </summary>
    Task<GrainStates.Ds4pAnalysisResult> AnalyzeDs4pCcdaAsync(string messageId, string ccdaXml);

    /// <summary>
    /// Retrieve a previously stored DS4P analysis result.
    /// </summary>
    Task<GrainStates.Ds4pAnalysisResult> GetDs4pAnalysisAsync(string messageId);

    // ─── Cancer Registry Reporting (§170.315(f)(4)) ──────────────────────

    /// <summary>
    /// Generate a NAACCR cancer registry abstract from an oncology tumor record.
    /// §170.315(f)(4) — Transmission to cancer registries.
    /// </summary>
    Task<string> GenerateCancerRegistryReportAsync(
        string tumorId,
        string reportingFacility,
        string registrarId,
        string registrarName);

    /// <summary>Get a cancer registry report by ID.</summary>
    Task<GrainStates.CancerRegistryReportState> GetCancerRegistryReportAsync(string reportId);

    /// <summary>Get the NAACCR abstract content for a report.</summary>
    Task<string> GetCancerRegistryNaaccrAbstractAsync(string reportId);

    /// <summary>Submit a cancer registry report to a named registry.</summary>
    Task SubmitCancerRegistryReportAsync(string reportId, string registryName, string? confirmationNumber);

    /// <summary>Record acceptance of a cancer registry report.</summary>
    Task AcceptCancerRegistryReportAsync(string reportId, string? registryResponse);

    /// <summary>Record rejection of a cancer registry report.</summary>
    Task RejectCancerRegistryReportAsync(string reportId, string rejectionReason);

    // ─── Patient Merge (Site Flavor — PATIENT_MERGE feature) ────────────

    /// <summary>
    /// Merge the source (duplicate) patient into this (surviving) patient.
    /// Requires "PATIENT_MERGE" feature to be enabled on the site.
    /// All clinical data from the source patient is moved to this patient;
    /// the source patient is deactivated and marked as merged; the source
    /// patient's MPI correlation grain and MPI search index entry are
    /// aliased to point at the target ICN (so future searches by the source
    /// ICN resolve to the surviving patient).
    /// Maps to VistA DG MERGE utility (File #15.1).
    /// </summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanMergePatients)]
    [Security.AuditAction("PATIENT_MERGE", "MERGE", EntityType = "PATIENT", IsClinicalWrite = true)]
    Task<GrainStates.PatientMergeResult> MergePatientAsync(
        string sourcePatientId,
        string reason,
        string mergedByUserId,
        string mergedByUserName);

    // ─── Diabetes Registry (Site Flavor — DIABETES_REGISTRY feature) ─────
    // Disease-specific registry for the per-patient subset of RPMS BDM
    // (Diabetes Management). Read methods are open to any authenticated
    // clinician; mutating methods require [CanManageDiabetesRegistry].

    /// <summary>Returns the computed diabetes-registry snapshot for this patient (status enums populated).</summary>
    Task<GrainStates.DiabetesRegistrySnapshot> GetDiabetesRegistrySnapshotAsync();

    /// <summary>Returns a pre-visit plan listing items due/overdue for this patient as of <paramref name="visitDate"/>.</summary>
    Task<GrainStates.DiabetesPreVisitPlan> GetDiabetesPreVisitPlanAsync(DateTime visitDate);

    /// <summary>
    /// Enroll this patient in the diabetes registry. Idempotent. Requires the
    /// DIABETES_REGISTRY site feature to be enabled.
    /// </summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanManageDiabetesRegistry)]
    [Security.AuditAction("DIABETES", "ENROLL", EntityType = "DiabetesRegistry", IsClinicalWrite = true)]
    Task EnrollInDiabetesRegistryAsync(string diabetesType, DateTime enrollmentDate);

    /// <summary>Append an HbA1c result. Recorded into a bounded history for trending.</summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanManageDiabetesRegistry)]
    [Security.AuditAction("DIABETES", "RECORD_HBA1C", EntityType = "DiabetesRegistry", IsClinicalWrite = true)]
    Task RecordDiabetesHbA1cAsync(decimal value, DateTime dateOfTest);

    /// <summary>Record a diabetic foot exam. Annual interval per IHS standard of care.</summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanManageDiabetesRegistry)]
    [Security.AuditAction("DIABETES", "RECORD_FOOT_EXAM", EntityType = "DiabetesRegistry", IsClinicalWrite = true)]
    Task RecordDiabetesFootExamAsync(DateTime dateOfExam, string? providerName);

    /// <summary>Record a dilated retinal eye exam. Annual interval.</summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanManageDiabetesRegistry)]
    [Security.AuditAction("DIABETES", "RECORD_EYE_EXAM", EntityType = "DiabetesRegistry", IsClinicalWrite = true)]
    Task RecordDiabetesEyeExamAsync(DateTime dateOfExam, string? providerName);

    /// <summary>Record an eGFR result for kidney function tracking.</summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanManageDiabetesRegistry)]
    [Security.AuditAction("DIABETES", "RECORD_EGFR", EntityType = "DiabetesRegistry", IsClinicalWrite = true)]
    Task RecordDiabetesEgfrAsync(decimal eGfrValue, DateTime dateOfTest);

    /// <summary>Record a urine albumin/creatinine ratio result for nephropathy screening.</summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanManageDiabetesRegistry)]
    [Security.AuditAction("DIABETES", "RECORD_ACR", EntityType = "DiabetesRegistry", IsClinicalWrite = true)]
    Task RecordDiabetesAcrAsync(decimal acrValue, DateTime dateOfTest);

    // ─── Immunization Forecasting (Site Flavor — IMMUNIZATION_FORECAST feature) ──

    /// <summary>
    /// Generate an immunization forecast for this patient based on their
    /// immunization history, age, and the ACIP vaccine schedule.
    /// Requires "IMMUNIZATION_FORECAST" feature to be enabled on the site.
    /// Maps to IHS RPMS BI FORECAST RPCs.
    /// </summary>
    Task<GrainStates.ImmunizationForecastResult> GenerateImmunizationForecastAsync();

    /// <summary>
    /// Get the most recently generated immunization forecast without recalculating.
    /// Requires "IMMUNIZATION_FORECAST" feature to be enabled on the site.
    /// </summary>
    Task<GrainStates.ImmunizationForecastResult> GetImmunizationForecastAsync();

    // ─── External Referral Tracking (Site Flavor — EXTERNAL_REFERRAL_TRACKING feature) ──

    /// <summary>
    /// Create an external (community care) referral for this patient.
    /// Requires "EXTERNAL_REFERRAL_TRACKING" feature to be enabled on the site.
    /// Maps to IHS RPMS RCIS (Referred Care Information System).
    /// </summary>
    Task<GrainStates.ExternalReferralState> CreateExternalReferralAsync(
        string referralType, string externalFacilityName, string? externalFacilityId,
        string? externalProviderName, string? externalProviderId,
        string purpose, string? diagnosis, string urgency,
        string referredByProviderId, string referredByProviderName,
        string? consultId, string? authorizationNumber,
        DateTime? appointmentDateTime, string? specialInstructions);

    /// <summary>Get all external referrals for this patient.</summary>
    Task<List<GrainStates.ExternalReferralIndexEntry>> GetExternalReferralsAsync();

    /// <summary>Get the full state of a specific external referral.</summary>
    Task<GrainStates.ExternalReferralState> GetExternalReferralAsync(string referralId);

    /// <summary>Update the status of an external referral.</summary>
    /// <summary>
    /// Mark an existing external referral as a Contract Health Services (CHS / PRC)
    /// request and submit it for CHS coordinator approval. The referral must
    /// already exist (created via <see cref="CreateExternalReferralAsync"/>).
    /// Requires the EXTERNAL_REFERRAL_TRACKING site feature.
    ///
    /// IHS-specific (25 CFR Part 136): the requesting provider must declare the
    /// Medical Priority Class and confirm whether alternate resources
    /// (Medicare/Medicaid/private insurance) have been considered. CHS is the
    /// payer of last resort.
    /// </summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanAuthorizeChs)]
    [Security.AuditAction("CHS", "REQUEST_AUTH", EntityType = "ExternalReferral", IsClinicalWrite = true)]
    Task RequestChsAuthorizationAsync(
        string referralId,
        decimal estimatedCost,
        string medicalPriorityClass,
        bool alternateResourcesChecked,
        string? alternateResourcesNote,
        string requestedByProviderId,
        string requestedByProviderName);

    /// <summary>
    /// CHS coordinator approves a pending CHS authorization request. Verifies
    /// the patient holds the IHS CHS eligibility code (stamped by
    /// <c>IhsTribalEligibilityPolicy</c> at registration) before authorizing.
    /// Records the authorized dollar amount and (optional) external auth#.
    /// </summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanAuthorizeChs)]
    [Security.AuditAction("CHS", "APPROVE_AUTH", EntityType = "ExternalReferral", IsClinicalWrite = true)]
    Task ApproveChsAuthorizationAsync(
        string referralId,
        decimal authorizedAmount,
        string? authorizationNumber,
        string approvedById,
        string approvedByName);

    /// <summary>
    /// CHS coordinator denies a pending CHS authorization request. Common
    /// reasons: ineligible patient, fund pool exhausted, alternate resources
    /// available, priority class deferred for the current fiscal year.
    /// </summary>
    [Security.RequiresSecurityKey(Security.SecurityKeys.CanAuthorizeChs)]
    [Security.AuditAction("CHS", "DENY_AUTH", EntityType = "ExternalReferral", IsClinicalWrite = true)]
    Task DenyChsAuthorizationAsync(
        string referralId,
        string denialReason,
        string deniedById,
        string deniedByName);

    Task UpdateExternalReferralStatusAsync(
        string referralId, string status, string? statusReason,
        string updatedById, string updatedByName);

    /// <summary>Record completion of an external referral with outcome.</summary>
    Task CompleteExternalReferralAsync(
        string referralId, DateTime completionDate,
        string? outcomeNotes, string? clinicalFindings);

    // ─── Appointment Wait List (Site Flavor — APPOINTMENT_WAITLIST feature) ──

    /// <summary>
    /// Add this patient to an appointment wait list for a specific clinic.
    /// Requires "APPOINTMENT_WAITLIST" feature to be enabled on the site.
    /// Maps to IHS RPMS SD Wait List (File #409.3) — auto-rebooking from wait list.
    /// </summary>
    Task<GrainStates.AppointmentWaitListState> AddToWaitListAsync(
        string clinicId, string clinicName,
        string desiredAppointmentType,
        string? preferredProviderId, string? preferredProviderName,
        string priority,
        DateTime? desiredDateRangeStart, DateTime? desiredDateRangeEnd,
        string? comments,
        string createdByProviderId, string createdByProviderName);

    /// <summary>Get all wait list entries for this patient.</summary>
    Task<List<GrainStates.AppointmentWaitListIndexEntry>> GetWaitListEntriesAsync();

    /// <summary>Get the full state of a specific wait list entry.</summary>
    Task<GrainStates.AppointmentWaitListState> GetWaitListEntryAsync(string entryId);

    /// <summary>Offer an available appointment slot to a wait-listed patient (auto-rebook).</summary>
    Task OfferWaitListSlotAsync(string entryId, string appointmentId, DateTime offeredDateTime, string offeredByName);

    /// <summary>Patient accepts the offered appointment slot.</summary>
    Task AcceptWaitListOfferAsync(string entryId, string acceptedByName);

    /// <summary>Patient declines the offered appointment slot.</summary>
    Task DeclineWaitListOfferAsync(string entryId, string reason, string declinedByName);

    /// <summary>Cancel a wait list entry.</summary>
    Task CancelWaitListEntryAsync(string entryId, string reason, string cancelledByName);

    // ─── Patient Recall (Site Flavor — PATIENT_RECALL feature) ──

    /// <summary>
    /// Create a recall entry for this patient — automated recall letters for overdue follow-up.
    /// Requires "PATIENT_RECALL" feature to be enabled on the site.
    /// Maps to IHS RPMS SC Recall routines (File #403.5).
    /// </summary>
    Task<GrainStates.PatientRecallState> CreateRecallEntryAsync(
        string clinicId, string clinicName,
        string recallType, DateTime recallDate,
        string? providerId, string? providerName,
        string? diagnosis, string? instructions,
        string createdByProviderId, string createdByProviderName);

    /// <summary>Get all recall entries for this patient.</summary>
    Task<List<GrainStates.PatientRecallIndexEntry>> GetRecallEntriesAsync();

    /// <summary>Get the full state of a specific recall entry.</summary>
    Task<GrainStates.PatientRecallState> GetRecallEntryAsync(string entryId);

    /// <summary>Generate a recall letter for this entry.</summary>
    Task GenerateRecallLetterAsync(string entryId, string letterType, string generatedByName);

    /// <summary>Record a contact attempt for a recall entry.</summary>
    Task RecordRecallContactAttemptAsync(string entryId, string contactMethod, string result, string contactedByName, string? notes);

    /// <summary>Mark a recall entry as having an appointment scheduled.</summary>
    Task ScheduleRecallAppointmentAsync(string entryId, string appointmentId, DateTime appointmentDateTime, string scheduledByName);

    /// <summary>Mark a recall entry as completed.</summary>
    Task CompleteRecallEntryAsync(string entryId, string completedByName, string? notes);

    /// <summary>Cancel a recall entry.</summary>
    Task CancelRecallEntryAsync(string entryId, string reason, string cancelledByName);

    // ─── Encounter Form Templates (Site Flavor — ENCOUNTER_FORM_TEMPLATES feature) ──

    /// <summary>
    /// Create an encounter form instance for this patient from a published template.
    /// Requires "ENCOUNTER_FORM_TEMPLATES" feature to be enabled on the site.
    /// Maps to IHS RPMS PCC encounter forms and VistA Reminder Dialogs.
    /// </summary>
    Task<GrainStates.EncounterFormInstanceState> CreateEncounterFormInstanceAsync(
        string templateId, string templateName,
        string? encounterId,
        string createdByProviderId, string createdByProviderName);

    /// <summary>Get all encounter form instances for this patient.</summary>
    Task<List<GrainStates.EncounterFormInstanceIndexEntry>> GetEncounterFormInstancesAsync();

    /// <summary>Get a specific encounter form instance.</summary>
    Task<GrainStates.EncounterFormInstanceState> GetEncounterFormInstanceAsync(string instanceId);

    /// <summary>Set field values on an encounter form instance.</summary>
    Task SetEncounterFormFieldValuesAsync(string instanceId, Dictionary<string, string?> fieldValues);

    /// <summary>Submit a completed encounter form.</summary>
    Task SubmitEncounterFormAsync(string instanceId, string submittedByName);

    /// <summary>Void an encounter form instance.</summary>
    Task VoidEncounterFormAsync(string instanceId, string voidedByName, string reason);

    // ─── Auto Refill (Site Flavor — AUTO_REFILL feature) ──

    /// <summary>
    /// Enroll a prescription in automated refill scheduling.
    /// Requires "AUTO_REFILL" feature to be enabled on the site.
    /// VistA lacks automated refill scheduling — this is a NewVistas enhancement.
    /// </summary>
    Task<GrainStates.AutoRefillState> EnrollAutoRefillAsync(
        string prescriptionId, string drugName, string drugClass,
        int daysSupply, int refillsRemaining, DateTime lastFillDate,
        string pharmacyId, string pharmacyName,
        string enrolledByProviderId, string enrolledByProviderName);

    /// <summary>Get all auto-refill enrollments for this patient.</summary>
    Task<List<GrainStates.AutoRefillIndexEntry>> GetAutoRefillEnrollmentsAsync();

    /// <summary>Get a specific auto-refill enrollment.</summary>
    Task<GrainStates.AutoRefillState> GetAutoRefillEnrollmentAsync(string enrollmentId);

    /// <summary>Suspend auto-refill for a prescription.</summary>
    Task SuspendAutoRefillAsync(string enrollmentId, string reason, string suspendedByName);

    /// <summary>Resume a suspended auto-refill enrollment.</summary>
    Task ResumeAutoRefillAsync(string enrollmentId, string resumedByName);

    /// <summary>Disenroll a prescription from auto-refill.</summary>
    Task DisenrollAutoRefillAsync(string enrollmentId, string reason, string disenrolledByName);

    // ─── Mass Casualty Mode (Site Flavor — MASS_CASUALTY feature) ──

    /// <summary>
    /// Register this patient as a casualty in a mass casualty incident.
    /// Requires "MASS_CASUALTY" feature to be enabled on the site.
    /// </summary>
    Task<GrainStates.MassCasualtyCasualtyState> RegisterAsMciCasualtyAsync(
        string incidentId, string triageTag, string triageCategory,
        string? chiefInjury, string? arrivalMode, string registeredByName);

    /// <summary>Get MCI casualty records linked to this patient.</summary>
    Task<List<GrainStates.MassCasualtyCasualtyIndexEntry>> GetMciCasualtiesForPatientAsync();

    // ─── Periodontal Charting (Site Flavor — PERIODONTAL_CHARTING feature) ──

    /// <summary>
    /// Create a periodontal chart for this patient.
    /// Requires "PERIODONTAL_CHARTING" feature to be enabled on the site.
    /// Maps to IHS RPMS DENT periodontal charting and VistA Dental Record Manager (File #220).
    /// </summary>
    Task<GrainStates.PeriodontalChartState> CreatePeriodontalChartAsync(
        string providerId, string providerName, string? notes);

    /// <summary>Get all periodontal charts for this patient.</summary>
    Task<List<GrainStates.PeriodontalChartIndexEntry>> GetPeriodontalChartsAsync();

    /// <summary>Get a specific periodontal chart.</summary>
    Task<GrainStates.PeriodontalChartState> GetPeriodontalChartAsync(string chartId);

    /// <summary>Record tooth data on a periodontal chart.</summary>
    Task RecordPeriodontalToothDataAsync(string chartId, int toothNumber, GrainStates.PeriodontalToothData data);

    /// <summary>Finalize a periodontal chart.</summary>
    Task FinalizePeriodontalChartAsync(string chartId, string finalizedByName);

    // ─── Anesthesia Tracking (Site Flavor — ANESTHESIA_TRACKING feature) ──

    /// <summary>
    /// Create an anesthesia record for this patient's surgery.
    /// Requires "ANESTHESIA_TRACKING" feature to be enabled on the site.
    /// Extends VistA Surgery (File #130) with structured anesthesia documentation.
    /// </summary>
    Task<GrainStates.AnesthesiaRecordState> CreateAnesthesiaRecordAsync(
        string surgeryId, string procedureName,
        string anesthesiaType, string anesthesiologistId, string anesthesiologistName,
        string asaClassification, string? airwayClass, string? preOpNotes);

    /// <summary>Get all anesthesia records for this patient.</summary>
    Task<List<GrainStates.AnesthesiaRecordIndexEntry>> GetAnesthesiaRecordsAsync();

    /// <summary>Get a specific anesthesia record.</summary>
    Task<GrainStates.AnesthesiaRecordState> GetAnesthesiaRecordAsync(string recordId);

    /// <summary>Add an anesthetic agent to a record.</summary>
    Task AddAnesthesiaAgentAsync(string recordId, GrainStates.AnesthesiaAgent agent);

    /// <summary>Finalize an anesthesia record.</summary>
    Task FinalizeAnesthesiaRecordAsync(string recordId, string finalizedByName);

    // ─── Drug Utilization Review (PSOORED.m DUR, PSODRDUP.m, PSOVER1.m, DRGINT.m) ──

    /// <summary>
    /// Performs a full Drug Utilization Review for a prescription.
    /// Checks: duplicate drug, duplicate therapy (drug class), drug-allergy contraindication,
    /// drug-drug interaction, max dose, days supply, refill timing, age-based dosing,
    /// renal/hepatic adjustments, and controlled substance enforcement.
    /// Returns the assessment ID. Failed checks place the Rx in PENDING DUR REVIEW.
    /// </summary>
    Task<string> PerformDurAsync(
        string prescriptionId,
        string drugName,
        string? drugId,
        string? drugClass,
        string? dosage,
        string? route,
        string? schedule,
        int? daysSupply,
        int? quantity,
        int? maxDaysSupply,
        int? maxQuantity,
        bool isControlledSubstance,
        string? deaSchedule,
        string? performedBy,
        List<string>? ingredientIens = null,
        decimal? maxDailyDoseMg = null);

    /// <summary>Gets the full DUR assessment for a given assessment ID.</summary>
    Task<GrainStates.DurAssessmentState> GetDurAssessmentAsync(string assessmentId);

    /// <summary>Gets all DUR assessments for this patient.</summary>
    Task<List<GrainStates.DurAssessmentIndexEntry>> GetDurAssessmentsAsync();

    /// <summary>Gets DUR assessments pending pharmacist review (status = Pending or Failed).</summary>
    Task<List<GrainStates.DurAssessmentIndexEntry>> GetPendingDurReviewsAsync();

    /// <summary>Gets the DUR assessment for a specific prescription.</summary>
    Task<GrainStates.DurAssessmentIndexEntry?> GetDurForPrescriptionAsync(string prescriptionId);

    /// <summary>
    /// Overrides a failed DUR check with a documented clinical reason.
    /// Only pharmacists may override. Mirrors PSOORED.m pharmacist override.
    /// </summary>
    Task OverrideDurCheckAsync(
        string assessmentId,
        GrainStates.DurCheckType checkType,
        string pharmacistId,
        string reason);

    /// <summary>
    /// Acknowledges the DUR assessment results. Transitions status to Acknowledged.
    /// </summary>
    Task AcknowledgeDurAsync(string assessmentId, string pharmacistId, string? notes);

    // ─── Drug Interaction Blocking (DRGINT.m — PSO fill/refill integration) ─

    /// <summary>
    /// Screens a prescription's ingredients against all active medication ingredients
    /// for drug-drug interactions. Significant and Contraindicated interactions
    /// block fill/refill until a pharmacist overrides each with a documented reason.
    /// Returns the screening ID.
    /// </summary>
    Task<string> ScreenPrescriptionForInteractionsAsync(
        string prescriptionId,
        string drugName,
        List<GrainStates.DrugIngredient> newDrugIngredients,
        List<GrainStates.DrugIngredient> existingMedicationIngredients,
        string? screenedBy);

    /// <summary>Gets the full interaction screening by screening ID.</summary>
    Task<GrainStates.InteractionScreeningState> GetInteractionScreeningAsync(string screeningId);

    /// <summary>Gets the interaction screening for a specific prescription.</summary>
    Task<GrainStates.InteractionScreeningState> GetInteractionScreeningForPrescriptionAsync(string prescriptionId);

    /// <summary>Gets all interaction screenings for this patient.</summary>
    Task<List<GrainStates.InteractionScreeningIndexEntry>> GetInteractionScreeningsAsync();

    /// <summary>Gets prescriptions currently blocked by drug interactions.</summary>
    Task<List<GrainStates.InteractionScreeningIndexEntry>> GetBlockedPrescriptionsAsync();

    /// <summary>
    /// Overrides a blocking interaction with a documented clinical reason.
    /// When all blocking interactions are overridden, the prescription is unblocked.
    /// </summary>
    Task OverrideInteractionBlockAsync(
        string screeningId,
        int findingIndex,
        string pharmacistId,
        string reason);

    /// <summary>
    /// Checks whether a prescription is cleared for fill/refill.
    /// Returns true if not screened (optional), cleared, or all overridden.
    /// Returns false if blocked by unresolved interactions.
    /// </summary>
    Task<bool> IsPrescriptionClearedForFillAsync(string prescriptionId);

    // ─── Pharmacy Workflow State Machine (PSOORED.m enforced sequence) ──────

    /// <summary>
    /// Fill a prescription with full safety checks. Enforces:
    /// 1. DUR assessment must be Passed/Overridden/Acknowledged
    /// 2. Interaction screening must be Cleared/Overridden/NotScreened
    /// 3. PharmacyGrain guards: must be ACTIVE, verified, not already filled
    /// Mirrors VistA PSOORED.m fill workflow.
    /// </summary>
    Task FillPrescriptionWorkflowAsync(string prescriptionId, DateTime fillDate);

    /// <summary>
    /// Refill a prescription with full safety checks. Same DUR/interaction gates
    /// as fill, plus PharmacyGrain guards: must be ACTIVE, have prior fill,
    /// refills remaining &gt; 0, not expired.
    /// </summary>
    Task RefillPrescriptionWorkflowAsync(string prescriptionId, DateTime fillDate);

    /// <summary>
    /// Verify a prescription with DUR gate. DUR must be Passed/Overridden/Acknowledged
    /// before a pharmacist can verify. PharmacyGrain guards: must be ACTIVE, not already verified.
    /// </summary>
    Task VerifyPrescriptionWorkflowAsync(string prescriptionId, string pharmacistId);

    /// <summary>Print label for a prescription. PharmacyGrain guards: must be verified.</summary>
    Task PrintLabelWorkflowAsync(string prescriptionId, string? rxNumber);

    /// <summary>Place prescription on hold. PharmacyGrain guards: must be ACTIVE.</summary>
    Task HoldPrescriptionWorkflowAsync(string prescriptionId, string reason);

    /// <summary>Resume a held prescription. PharmacyGrain guards: must be HOLD.</summary>
    Task ResumePrescriptionWorkflowAsync(string prescriptionId);

    /// <summary>Discontinue a prescription. PharmacyGrain guards: must not be DISCONTINUED or EXPIRED.</summary>
    Task DiscontinuePrescriptionWorkflowAsync(string prescriptionId, string reason);

    /// <summary>Expire a prescription. PharmacyGrain guards: must be ACTIVE or HOLD.</summary>
    Task ExpirePrescriptionWorkflowAsync(string prescriptionId);

    /// <summary>
    /// Checks whether a prescription's DUR assessment allows fill/verify/refill.
    /// Returns true if DUR status is Passed, OverriddenByPharmacist, or Acknowledged.
    /// Returns false if no DUR exists or DUR status is Pending/Failed.
    /// </summary>
    Task<bool> IsDurClearedForPrescriptionAsync(string prescriptionId);

    // ─── Refill Eligibility (PSO refill date calculation, DEA enforcement) ──

    /// <summary>
    /// Checks whether a prescription is eligible for refill at the proposed date.
    /// Combines grain-level checks (status, refills remaining, expiration, DEA schedule,
    /// early refill 75% rule) with cross-grain safety gates (DUR, interaction screening).
    /// Returns a structured result — does NOT perform the refill.
    /// VistA reference: PSO refill date calculation, CalcMaxRefills, DEA 21 CFR 1306.12.
    /// </summary>
    Task<GrainStates.RefillEligibilityResult> GetRefillEligibilityAsync(
        string prescriptionId, DateTime proposedFillDate);

    // ─── Prior Auth / Insurance Coverage (PSO insurance hooks) ──────────────

    /// <summary>
    /// Checks formulary coverage and Prior Authorization status for a prescription.
    /// Returns whether the drug is covered and, if PA is required, whether an
    /// approved PA exists. Called as a gate before fill/refill.
    /// </summary>
    Task<GrainStates.PriorAuthCoverageResult> CheckPriorAuthStatusAsync(string prescriptionId);

    // ─── NDC/Lot Tracking (PSO dispense recording) ──────────────────────────

    /// <summary>Records the actual NDC and lot number dispensed for a prescription.</summary>
    Task RecordDispenseWorkflowAsync(
        string prescriptionId, string? ndcDispensed, string? lotNumber, string? pharmacistId);

    // ─── Patient Counseling (PSOCP.m) ───────────────────────────────────────

    /// <summary>Records completion of patient counseling. Guard: CounselingRequired must be true.</summary>
    Task RecordCounselingWorkflowAsync(string prescriptionId, string pharmacistId, string? notes);

    // ─── Label Generation (PSJLBL.m) ────────────────────────────────────────

    /// <summary>Generates structured label content including patient name from patient grain.</summary>
    Task<GrainStates.PrescriptionLabelContent> GenerateLabelContentWorkflowAsync(string prescriptionId);

    // ─── Insurance Eligibility Verification (EDI 270/271) — IBCNEDE*.m ─────

    /// <summary>
    /// Submits a new eligibility inquiry (EDI 270) and simulates a payer 271 response.
    /// Creates the inquiry grain, submits it, records a simulated response,
    /// updates the per-patient verification index, and updates the insurance plan verification date.
    /// Returns the inquiry ID.
    /// </summary>
    Task<string> SubmitEligibilityInquiryAsync(
        string? insurancePlanId,
        string? personalPolicyId,
        string payerId,
        string payerName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? patientDateOfBirth,
        List<string> serviceTypeCodes,
        DateTime serviceDate,
        string? initiatedByUserId,
        string? initiatedByUserName,
        string? notes);

    /// <summary>Returns the full state of an eligibility inquiry.</summary>
    Task<GrainStates.EligibilityInquiryState> GetEligibilityInquiryAsync(string inquiryId);

    /// <summary>Returns all eligibility verification entries for this patient.</summary>
    Task<List<GrainStates.EligibilityVerificationIndexEntry>> GetEligibilityVerificationHistoryAsync();

    /// <summary>Returns only eligible verifications for this patient.</summary>
    Task<List<GrainStates.EligibilityVerificationIndexEntry>> GetEligibleVerificationsAsync();

    /// <summary>Returns the most recent verification for a given payer.</summary>
    Task<GrainStates.EligibilityVerificationIndexEntry?> GetLatestVerificationForPayerAsync(string payerName);

    /// <summary>Returns all configured payers available for eligibility verification.</summary>
    Task<List<GrainStates.PayerConfigIndexEntry>> GetPayerConfigListAsync();

    /// <summary>Searches configured payers by name.</summary>
    Task<List<GrainStates.PayerConfigIndexEntry>> SearchPayerConfigsAsync(string query);

    /// <summary>Returns payers that support real-time 270/271 verification.</summary>
    Task<List<GrainStates.PayerConfigIndexEntry>> GetRealTimePayersAsync();

    // ─── Collection Letters (PRCA) — RCCLLT*.m, RCCL*.m ────────────────────

    /// <summary>
    /// Generates a collection letter for this patient based on their active AR accounts.
    /// Fetches AR accounts, computes line items, determines letter type from dunning sequence,
    /// and creates the letter grain. Returns the letter ID.
    /// </summary>
    Task<string> GenerateCollectionLetterAsync(
        GrainStates.CollectionLetterType letterType,
        string? generatedByUserId,
        string? generatedByUserName,
        string? notes);

    /// <summary>Returns all collection letters for this patient.</summary>
    Task<List<GrainStates.CollectionLetterIndexEntry>> GetCollectionLettersAsync();

    /// <summary>Returns the full state of a specific collection letter.</summary>
    Task<GrainStates.CollectionLetterState> GetCollectionLetterAsync(string letterId);

    /// <summary>Marks a collection letter as printed.</summary>
    Task MarkCollectionLetterPrintedAsync(string letterId);

    /// <summary>Marks a collection letter as mailed.</summary>
    Task MarkCollectionLetterMailedAsync(string letterId);

    /// <summary>Marks a collection letter as returned undeliverable.</summary>
    Task MarkCollectionLetterReturnedAsync(string letterId);

    /// <summary>Cancels a collection letter.</summary>
    Task CancelCollectionLetterAsync(string letterId, string? reason);

    // ─── Financial Reporting / AR Aging (PRCA) — RCRP*.m ────────────────────

    /// <summary>
    /// Generates an AR aging report for this patient. Fetches all active AR accounts,
    /// classifies them into aging buckets, and computes revenue cycle metrics.
    /// Returns the report state.
    /// </summary>
    Task<GrainStates.ARAgingReportState> GenerateARAgingReportAsync(
        string? generatedByUserId,
        string? generatedByUserName);

    /// <summary>Returns the most recently generated AR aging report for this patient.</summary>
    Task<GrainStates.ARAgingReportState> GetARAgingReportAsync();

    /// <summary>Returns aging bucket summaries for this patient.</summary>
    Task<List<GrainStates.AgingBucketSummary>> GetARAgingBucketsAsync();

    /// <summary>Returns revenue cycle metrics for this patient.</summary>
    Task<GrainStates.RevenueCycleMetrics?> GetRevenueCycleMetricsAsync();

    /// <summary>Returns AR accounts in a specific aging bucket.</summary>
    Task<List<GrainStates.AgingAccountDetail>> GetAccountsByAgingBucketAsync(GrainStates.AgingBucket bucket);

    // ─── Claim Status Inquiry (EDI 276/277) — IBCSC*.m ─────────────────────

    /// <summary>
    /// Submits a claim status inquiry (276) for an EDI claim and simulates a 277 response.
    /// Returns the inquiry ID.
    /// </summary>
    Task<string> SubmitClaimStatusInquiryAsync(
        string claimId, string payerId, string payerName,
        string? initiatedByUserId, string? initiatedByUserName, string? notes);

    /// <summary>Returns the full state of a claim status inquiry.</summary>
    Task<GrainStates.ClaimStatusInquiryState> GetClaimStatusInquiryAsync(string inquiryId);

    /// <summary>Returns all claim status inquiries for this patient.</summary>
    Task<List<GrainStates.ClaimStatusInquiryIndexEntry>> GetClaimStatusInquiriesAsync();

    /// <summary>Returns claim status inquiries for a specific claim.</summary>
    Task<List<GrainStates.ClaimStatusInquiryIndexEntry>> GetClaimStatusInquiriesByClaimAsync(string claimId);

    // ─── Automatic Eligibility Determination — DGENELA.m ────────────────────

    /// <summary>
    /// Runs automatic eligibility determination using current enrollment, means test,
    /// and SC/priority data. Auto-applies copay exemption and enrollment status if applicable.
    /// Returns the determination state.
    /// </summary>
    Task<GrainStates.AutoEligibilityDeterminationState> RunAutoEligibilityDeterminationAsync(
        string? determinedByUserId, string? determinedByUserName);

    /// <summary>Returns the most recent eligibility determination for this patient.</summary>
    Task<GrainStates.AutoEligibilityDeterminationState> GetAutoEligibilityDeterminationAsync();

    // ─── TOP Federal Debt Matching — RCTP*.m, RCTOP*.m ──────────────────────

    /// <summary>
    /// Records a TOP offset received from Treasury and attempts to match it to the patient's
    /// AR accounts and TOP referrals. Posts payment to matched AR account if found.
    /// Returns the match record ID.
    /// </summary>
    Task<string> ProcessTopOffsetMatchAsync(
        string treasuryTransactionId, string taxpayerIdNumber, string treasuryPatientName,
        decimal offsetAmount, string offsetSource, DateTime offsetReceivedDate,
        string? processedByUserId, string? processedByUserName, string? notes);

    /// <summary>Returns all TOP match records for this patient.</summary>
    Task<List<GrainStates.TopMatchIndexEntry>> GetTopMatchRecordsAsync();

    /// <summary>Returns the full state of a TOP match record.</summary>
    Task<GrainStates.TopMatchingState> GetTopMatchRecordAsync(string matchId);

    // ─── Nursing Intake/Triage Assessment — NUR intake, ESI triage ──────────

    /// <summary>Creates a new triage assessment with chief complaint, vitals, ESI level, and nursing findings.</summary>
    Task<string> CreateTriageAssessmentAsync(
        DateTime triageDateTime, string nurseId, string nurseName,
        string? locationId, string? locationName,
        string chiefComplaint, string? historyOfPresentIllness,
        decimal? temperature, int? heartRate, int? respiratoryRate,
        int? systolicBP, int? diastolicBP, decimal? spO2, int? painScore,
        GrainStates.TriageLevel triageLevel, int? expectedResources,
        string? levelOfConsciousness, string? modeOfArrival,
        bool isAcuteDistress, bool arrivedByAmbulance,
        string? notes);

    /// <summary>Returns the full triage assessment state.</summary>
    Task<GrainStates.NursingTriageState> GetTriageAssessmentAsync(string triageId);

    /// <summary>Returns all triage assessments for this patient.</summary>
    Task<List<GrainStates.NursingTriageIndexEntry>> GetTriageAssessmentsAsync();

    /// <summary>Signs a triage assessment.</summary>
    Task SignTriageAssessmentAsync(string triageId, string nurseId, string nurseName);

    /// <summary>Sets the disposition for a triage assessment.</summary>
    Task SetTriageDispositionAsync(string triageId, GrainStates.TriageDisposition disposition);

    // ─── Nursing Task Worklist — NUR task management ────────────────────────

    /// <summary>
    /// Generates the nursing task worklist by aggregating due medications from MAR,
    /// active care plan interventions, and vital sign schedules into a unified task view.
    /// </summary>
    Task<GrainStates.NursingTaskWorklistState> RefreshNursingTaskWorklistAsync();

    /// <summary>Returns the current task worklist state.</summary>
    Task<GrainStates.NursingTaskWorklistState> GetNursingTaskWorklistAsync();

    /// <summary>Returns only tasks that are due or overdue.</summary>
    Task<List<GrainStates.NursingTask>> GetDueNursingTasksAsync();

    /// <summary>Marks a nursing task as completed.</summary>
    Task CompleteNursingTaskAsync(string taskId, string nurseId, string nurseName, string? notes);

    /// <summary>Defers a nursing task with reason.</summary>
    Task DeferNursingTaskAsync(string taskId, string? reason);

    /// <summary>Adds an ad-hoc nursing task to the worklist.</summary>
    Task AddNursingTaskAsync(
        GrainStates.NursingTaskCategory category,
        GrainStates.NursingTaskPriority priority,
        string description,
        DateTime dueDateTime,
        string? sourceId, string? sourceType);

    // ─── Shift Handoff / Report — NUR shift report ──────────────────────────

    /// <summary>
    /// Creates a shift handoff report with SBAR summary. Auto-populates clinical snapshot
    /// from current vitals, care plan, MAR, and acuity.
    /// </summary>
    Task<string> CreateShiftHandoffAsync(
        GrainStates.NursingShift shift, DateTime shiftDate,
        string outgoingNurseId, string outgoingNurseName,
        string? locationId, string? locationName, string? bedNumber,
        GrainStates.SbarPatientSummary sbar,
        List<string>? safetyConcerns, string? notes);

    /// <summary>Returns the full shift handoff state.</summary>
    Task<GrainStates.NursingShiftHandoffState> GetShiftHandoffAsync(string handoffId);

    /// <summary>Returns all shift handoffs for this patient.</summary>
    Task<List<GrainStates.ShiftHandoffIndexEntry>> GetShiftHandoffsAsync();

    /// <summary>Marks a shift handoff as completed by the outgoing nurse.</summary>
    Task CompleteShiftHandoffAsync(string handoffId);

    /// <summary>Acknowledges a shift handoff by the incoming nurse.</summary>
    Task AcknowledgeShiftHandoffAsync(string handoffId, string incomingNurseId, string incomingNurseName);

    // ─── Pain Assessment Workflow — NUR pain assessment (DVPRS, Wong-Baker, FLACC) ──

    /// <summary>
    /// Records a structured pain assessment using a validated tool (NRS, DVPRS, Wong-Baker, FLACC, CPOT, VAS).
    /// </summary>
    Task<string> RecordPainAssessmentAsync(
        GrainStates.PainAssessmentTool tool, int painScore,
        string? painLocation, string? painCharacter, string? painOnset,
        string? aggravatingFactors, string? alleviatingFactors, string? radiation,
        int? acceptablePainLevel,
        GrainStates.DvprsSupplemental? dvprsSupplemental,
        GrainStates.FlaccScore? flaccComponents,
        string? interventionProvided,
        string nurseId, string nurseName, string? notes);

    /// <summary>Records a pain reassessment after intervention.</summary>
    Task<string> RecordPainReassessmentAsync(
        string initialAssessmentId,
        GrainStates.PainAssessmentTool tool, int postInterventionScore,
        int minutesSinceIntervention,
        string? interventionProvided,
        string nurseId, string nurseName, string? notes);

    /// <summary>Returns the full pain assessment state.</summary>
    Task<GrainStates.PainAssessmentState> GetPainAssessmentAsync(string assessmentId);

    /// <summary>Returns all pain assessments for this patient.</summary>
    Task<List<GrainStates.PainAssessmentIndexEntry>> GetPainAssessmentsAsync();

    /// <summary>Returns the most recent pain assessment.</summary>
    Task<GrainStates.PainAssessmentIndexEntry?> GetLatestPainAssessmentAsync();

    // ─── Lab Tech Worklist / Accessioning / QC / Specimen Rejection / Delta Checks ──

    /// <summary>Creates a lab accession record for a specimen with formal accession number.</summary>
    Task<string> AccessionSpecimenAsync(
        List<string> labTestIds, string specimenType, string? collectionTube,
        DateTime collectionDateTime, string? labSection,
        string? accessionedByUserId, string? accessionedByUserName, string? notes);

    /// <summary>Returns the full accession state.</summary>
    Task<GrainStates.LabAccessionState> GetAccessionAsync(string accessionNumber);

    /// <summary>Returns all accessions for this patient.</summary>
    Task<List<GrainStates.LabAccessionIndexEntry>> GetAccessionsAsync();

    /// <summary>Returns pending accessions.</summary>
    Task<List<GrainStates.LabAccessionIndexEntry>> GetPendingAccessionsAsync();

    /// <summary>Rejects a specimen with reason and optional recollect order.</summary>
    Task RejectSpecimenAsync(string accessionNumber, GrainStates.SpecimenRejectReason reason,
        string? rejectNotes, string rejectedByUserId, bool orderRecollect);

    /// <summary>Records a QC run with Westgard rule evaluation.</summary>
    Task<GrainStates.LabQcResult> RecordLabQcRunAsync(
        string instrumentId, string loincCode, string testName,
        string qcLevel, string lotNumber, decimal measuredValue,
        decimal expectedMean, decimal standardDeviation,
        string techId, string techName, string? notes);

    /// <summary>Returns the QC state for an instrument/test.</summary>
    Task<GrainStates.LabQcState> GetLabQcStateAsync(string instrumentId, string loincCode);

    /// <summary>Checks whether patient testing is allowed (QC passing).</summary>
    Task<bool> IsLabTestingAllowedAsync(string instrumentId, string loincCode);

    /// <summary>Performs a delta check comparing current vs previous result.</summary>
    Task<GrainStates.DeltaCheckResult> PerformDeltaCheckAsync(
        string labTestId, string loincCode, string testName,
        decimal currentValue, DateTime resultDate, string? instrumentId);

    /// <summary>Refreshes the lab tech worklist for a location.</summary>
    Task<GrainStates.LabWorklistState> RefreshLabWorklistAsync(string locationId);

    /// <summary>Returns the lab tech worklist for a location.</summary>
    Task<GrainStates.LabWorklistState> GetLabWorklistAsync(string locationId);

    // ─── Radiology Tech Workflow — RARTE.m ──────────────────────────────────

    /// <summary>Initializes exam tracking for a radiology order and optionally schedules it.</summary>
    Task InitializeRadExamTrackingAsync(string radiologyId, DateTime? scheduledDateTime, string? room);

    /// <summary>Returns the exam tracking state for a radiology order.</summary>
    Task<GrainStates.RadExamTrackingState> GetRadExamTrackingAsync(string radiologyId);

    /// <summary>Assigns an imaging protocol to a radiology exam.</summary>
    Task AssignRadProtocolAsync(string radiologyId, string protocolId, string protocolName, string? parameters);

    /// <summary>Marks a radiology patient as prepped for exam.</summary>
    Task MarkRadPatientPreppedAsync(string radiologyId, string? prepNotes);

    /// <summary>Starts a radiology exam.</summary>
    Task StartRadExamAsync(string radiologyId);

    /// <summary>Completes a radiology exam with image count and tech notes.</summary>
    Task CompleteRadExamAsync(string radiologyId, int? imageCount, string? techNotes);

    /// <summary>Records that images were sent to PACS.</summary>
    Task SendRadImagesToPacsAsync(string radiologyId);

    /// <summary>Links an ImagingGrain image to a radiology exam.</summary>
    Task LinkImageToRadExamAsync(string radiologyId, string imageId);

    /// <summary>Returns all imaging protocols.</summary>
    Task<List<GrainStates.RadProtocolIndexEntry>> GetRadProtocolsAsync();

    /// <summary>Returns protocols filtered by imaging type.</summary>
    Task<List<GrainStates.RadProtocolIndexEntry>> GetRadProtocolsByTypeAsync(string imagingType);

    /// <summary>Refreshes the rad tech worklist for a location.</summary>
    Task<GrainStates.RadWorklistState> RefreshRadWorklistAsync(string locationId);

    /// <summary>Returns the rad tech worklist for a location.</summary>
    Task<GrainStates.RadWorklistState> GetRadWorklistAsync(string locationId);

    // ─── Registration — Bed Availability, Advance Directives, Identity ──────

    /// <summary>Queries available beds facility-wide, optionally filtered by ward or bed type.</summary>
    Task<List<GrainStates.BedSummaryEntry>> FindAvailableBedsAsync(string facilityId, string? wardId, string? bedType);

    /// <summary>Returns total, available, and occupied bed counts for a facility.</summary>
    Task<(int Total, int Available, int Occupied)> GetBedCountsAsync(string facilityId);

    /// <summary>Returns the patient's advance directive state (code status, proxy, documents on file).</summary>
    Task<GrainStates.AdvanceDirectiveState> GetAdvanceDirectivesAsync();

    /// <summary>Updates the patient's code status (Full Code, DNR, DNI, DNR/DNI, Comfort Care).</summary>
    Task UpdateCodeStatusAsync(GrainStates.CodeStatus codeStatus, string updatedByUserId);

    /// <summary>Sets the patient's healthcare proxy / power of attorney.</summary>
    Task SetHealthcareProxyAsync(string proxyName, string? proxyPhone, string? proxyRelationship);

    /// <summary>Adds an advance directive document to the patient's record.</summary>
    Task AddAdvanceDirectiveDocumentAsync(GrainStates.AdvanceDirectiveType directiveType,
        DateTime documentDate, string? documentSource, DateTime? expirationDate, string? notes);

    /// <summary>Returns the patient's identity verification state.</summary>
    Task<GrainStates.IdentityVerificationState> GetIdentityVerificationAsync();

    /// <summary>Records an identity verification event with document type, result, and optional photo.</summary>
    Task<string> RecordIdentityVerificationAsync(
        GrainStates.IdentityDocumentType documentType, string? documentNumber,
        string? issuingAuthority, DateTime? expirationDate,
        GrainStates.IdentityVerificationResult result,
        bool photoOnFile, string? photoReference, string? discrepancyNotes,
        string verifiedByUserId, string verifiedByUserName, string? notes);

    /// <summary>Updates the patient's photo on file.</summary>
    Task UpdatePatientPhotoAsync(string photoReference);

    /// <summary>Returns the patient's insurance policies surfaced during registration.</summary>
    Task<List<GrainStates.PersonalPolicyIndexEntry>> GetInsuranceAtRegistrationAsync();
}
