// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection.Extensions;
using NewVistas.AI;
using NewVistas.Abstractions.Services;
using NewVistas.SiloHost.Infrastructure;
using NewVistas.SiloHost.Infrastructure.Cdc;
using NewVistas.SiloHost.Infrastructure.Cdc.Materializers;
using NewVistas.SiloHost.Infrastructure.Profiles;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Drug interaction silo-level cache (shared across all grains in the silo).
builder.Services.AddSingleton<IDrugInteractionCacheService, DrugInteractionCacheService>();

// Patient-index read snapshot holder for the PatientSearchGrain stateless
// workers (pull-through populated; one immutable index copy per silo).
builder.Services.AddSingleton<IPatientIndexSnapshotService, PatientIndexSnapshotService>();

// Route-vs-dose-form validation (RxNorm-derived). The validation service is a
// silo singleton shared by the pharmacy and inpatient order grains. The RxNav
// client defaults to a no-op so the system runs fully offline; a live HTTP
// client can replace it later via configuration.
builder.Services.AddSingleton<IRouteValidationService, RouteValidationService>();
builder.Services.TryAddSingleton<IRxNavDoseFormClient, NullRxNavDoseFormClient>();
builder.Services.TryAddSingleton<IOutboundPrescriptionTransmitter, NullOutboundPrescriptionTransmitter>();

// Clinical-summary narrative seam. When the "ClinicalNarrative" config section is
// Enabled, a live Claude client (Anthropic SDK), wrapped in a resilient fallback, is
// registered. Otherwise the offline template below composes grounded summaries with no
// model or network access. The live registration (plain AddSingleton) wins over the
// TryAddSingleton template fallback when present.
ClinicalNarrativeOptions narrativeOptions = builder.Configuration
    .GetSection(ClinicalNarrativeOptions.SectionName)
    .Get<ClinicalNarrativeOptions>() ?? new ClinicalNarrativeOptions();
builder.Services.AddClinicalNarrativeAi(narrativeOptions);
builder.Services.TryAddSingleton<IClinicalNarrativeService, TemplateClinicalNarrativeService>();
builder.Services.TryAddSingleton<IRadiologyFindingExtractor, HeuristicRadiologyFindingExtractor>();

// Legacy flag still consulted directly by the database-init step below.
// SiteProfileResolver consults the same flag independently for silo configuration.
bool useSqlExpress = SiteProfileResolver.UsesSqlExpressFlag(args);

ISiteProfile profile = SiteProfileResolver.Resolve(builder.Configuration, args, builder.Environment);

builder.Services.AddOrleans(siloBuilder =>
    profile.ConfigureSilo(siloBuilder, builder, AllStoreNames));

// ── CDC Materialization Service (reporting star schema) ────────────────────
// Optional feature — off by default so small clinics that don't need a data
// warehouse avoid the materializer workload and rpt.* schema footprint.
// Sites that want reporting set Cdc:Enabled=true in configuration.
builder.Services.Configure<CdcOptions>(builder.Configuration.GetSection(CdcOptions.SectionName));

bool cdcEnabled = builder.Configuration.GetSection(CdcOptions.SectionName).Get<CdcOptions>()?.Enabled ?? false;
bool cdcHasSqlStorage = useSqlExpress || !builder.Environment.IsDevelopment();

if (cdcEnabled && cdcHasSqlStorage)
{
    builder.Services.AddSingleton<ICdcEntityMaterializer, PatientMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, OrderMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, LabTestMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, PrescriptionMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, ConsultMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, AdtMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, VitalMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, TiuDocumentMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, BcmaMaterializer>();
    builder.Services.AddSingleton<ICdcEntityMaterializer, AuditEventMaterializer>();

    builder.Services.AddHostedService<CdcMaterializationService>();
}

var host = builder.Build();

// Initialize SQL Express database before Orleans starts
ILogger startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
string? federationOutboxConnStr = null;

if (useSqlExpress)
{
    string sqlExpressConnStr = host.Services.GetRequiredService<IConfiguration>()
        .GetConnectionString("SqlExpress")
        ?? throw new InvalidOperationException("SqlExpress connection string not found.");
    await DatabaseInitializer.EnsureDatabaseAsync(sqlExpressConnStr, startupLogger);
    federationOutboxConnStr = sqlExpressConnStr;
}
else if (!builder.Environment.IsDevelopment())
{
    // Production (Azure SQL): database is pre-created by az sql db create,
    // but Orleans schema tables still need to be applied on first run.
    string connectionString = host.Services.GetRequiredService<IConfiguration>()
        .GetConnectionString("OrleansDatabase")
        ?? throw new InvalidOperationException("Orleans database connection string not found.");
    await DatabaseInitializer.EnsureSchemaAsync(connectionString, startupLogger);
    federationOutboxConnStr = connectionString;
}

// Federation outbox schema — only profiles that opt in (RemoteOnline / RemoteOffline today).
// Reuses whichever connection string the silo's grain storage uses; both remote
// profiles use SqlExpress today, so this falls through cleanly when useSqlExpress is true.
if (profile.UsesFederationOutbox && federationOutboxConnStr is not null)
{
    await DatabaseInitializer.EnsureFederationOutboxSchemaAsync(federationOutboxConnStr, startupLogger);
}

host.Run();

// ─── All grain store names ──────────────────────────────────────────────────

static partial class Program
{
    internal static readonly string[] AllStoreNames =
    [
        "patientStore", "appointmentStore", "labTestStore", "orderStore",
        "pharmacyStore", "bcmaStore", "bcmaMarStore", "radiologyStore",
        "vitalStore", "tiuDocumentStore", "consultStore", "surgeryStore",
        "clinicalReminderStore", "healthFactorStore",
        "mentalHealthStore",
        "imagingStore", "adtStore", "wardLocationStore", "wardCensusStore",
        "labSummaryStore", "labBatchStore", "labIndexStore",
        "icd10Store",
        "patientIndexStore",
        "patientHistoryIndexStore",
        "vaProductStore", "ndfProductIndexStore", "ndfGenericIndexStore", "ndfClassIndexStore",
        "drugInteractionStore",
        "durAssessmentStore", "durAssessmentIndexStore",
        "interactionScreeningStore", "interactionScreeningIndexStore",
        "drugFileStore", "drugIndexStore",
        "orderableItemStore", "orderableItemIndexStore",
        "medRouteStore", "doseUnitStore", "doseFormRouteStore",
        "prescriptionIndexStore",
        "inpatientOrderStore", "inpatientProfileStore",
        "drugAccountabilityStore", "daLocationStore",
        "patientBenefitStore", "formularyStore", "priorAuthStore", "priorAuthIndexStore",
        "visitStore", "visitIndexStore",
        "clinicStore", "clinicIndexStore", "scheduleIndexStore",
        "auditEventStore", "patientAuditIndexStore", "newPersonStore", "providerDirectoryStore",
        "pharmacyDirectoryStore",
        "patientClinicalStreamStore",
        "provisioningTokenStore",
        "provisioningTokenIndexStore",
        "revocationRegistryStore",
        "notificationStore",
        "ibBillingActionStore", "ibBillingActionIndexStore", "ibBillingPatientStore",
        "meansTestBillingClockStore", "ambulatoryCopaySheetStore", "ibSiteParamsStore",
        "insurancePlanStore", "insurancePlanIndexStore",
        "personalPolicyStore", "personalPolicyIndexStore",
        "patientEnrollmentStore", "enrollmentStatusIndexStore", "catastrophicDisabilityIndexStore",
        "prfNationalFlagIndexStore", "prfAssignmentStore", "mstHistoryStore",
        "patientRelationStore", "incomeHouseholdStore", "treatingFacilityListStore",
        "arDebtorStore", "arSiteParamsStore", "arAccountStore", "arAccountIndexStore",
        "arTransactionStore", "arBatchPaymentStore", "arBatchPaymentIndexStore",
        "feePatientStore", "feeVendorStore", "feeVendorIndexStore",
        "feeAuthorizationStore", "feeAuthorizationIndexStore",
        "feeInvoiceStore", "feeInvoiceIndexStore",
        "feeSiteParamsStore", "incomeThresholdStore",
        "feeBatchPaymentStore", "feeBatchPaymentIndexStore",
        "cashierReceiptStore", "cashierReceiptIndexStore",
        "cashierSessionStore", "cashierSessionIndexStore",
        "ediClaimStore", "ediClaimIndexStore",
        "ediTransmissionStore", "ediTransmissionIndexStore",
        "eraStore", "eraIndexStore",
        "topReferralStore", "topReferralIndexStore",
        "ifcapControlPointStore", "ifcapControlPointIndexStore",
        "ifcapPurchaseRequestStore", "ifcapPurchaseRequestIndexStore",
        "ifcapPurchaseOrderStore", "ifcapPurchaseOrderIndexStore",
        "ifcapReceivingReportStore", "ifcapReceivingReportIndexStore",
        "ifcapVendorStore", "ifcapVendorIndexStore",
        "ifcapSiteParamsStore",
        "nursingAssessmentStore", "nursingAssessmentIndexStore",
        "nursingCarePlanStore", "nursingAcuityStore",
        "nursingUnitStore", "nursingUnitIndexStore",
        "bbPatientStore", "bbUnitStore", "bbUnitIndexStore",
        "bbCrossmatchStore", "bbCrossmatchIndexStore",
        "bbTransfusionStore", "bbTransfusionIndexStore",
        "apCaseStore", "apCaseIndexStore",
        "oncTumorStore", "oncTumorIndexStore",
        "oncTreatmentStore", "oncTreatmentIndexStore",
        "dentalPatientStore", "dentalTreatmentStore", "dentalTreatmentIndexStore",
        "socialWorkAssessmentStore", "socialWorkAssessmentIndexStore",
        "socialWorkReferralStore", "socialWorkReferralIndexStore",
        "womensHealthNotificationStore", "womensHealthIndexStore",
        "pregnancyStore", "pregnancyIndexStore",
        "prenatalVisitStore", "prenatalVisitIndexStore",
        "newbornStore", "newbornNurseryStore",
        "pharmacogenomicsStore",
        "saEpisodeStore", "saEpisodeIndexStore",
        "saVisitStore", "saVisitIndexStore",
        "posClaimStore", "posClaimIndexStore",
        "posInsurerStore", "posInsurerIndexStore",
        "epcsRxStore", "epcsRxIndexStore",
        "epcsProviderStore", "epcsProviderIndexStore",
        "gpraReportStore", "gpraReportIndexStore",
        "labSurveillanceTaxonomyStore", "labSurveillanceTaxonomyIndexStore",
        "pccSurvConfigStore", "pccSurvConfigIndexStore",
        "pccSurvMatchStore", "pccSurvMatchIndexStore",
        "sciPatientStore", "sciIndexStore",
        "brPatientStore", "brCenterStore", "brCenterIndexStore",
        "brAdmissionStore", "brAdmissionIndexStore",
        "brOutpatientVisitStore", "brOutpatientVisitIndexStore",
        "htPatientStore", "htReadingStore", "htReadingIndexStore",
        "htDeviceStore", "htDeviceIndexStore",
        "htAlertStore", "htAlertIndexStore",
        "ecEncounterStore", "ecPatientStore", "ecEncounterIndexStore",
        "dssUnitStore", "dssUnitIndexStore",
        "healthSummaryTypeStore", "healthSummaryTypeIndexStore",
        "healthSummaryStore", "healthSummaryIndexStore",
        "engWorkOrderStore", "engWorkOrderIndexStore",
        "engFacilityStore", "engFacilityIndexStore",
        "vsVolunteerStore", "vsIndexStore",
        "medProcedureStore", "medProcedureIndexStore",
        "cpProcedureStore", "cpProcedureIndexStore",
        "rtCourseStore", "rtCourseIndexStore",
        "rtTreatmentStore", "rtTreatmentIndexStore",
        "ivAdmixOrderStore", "ivAdmixOrderIndexStore",
        "csInspectionStore", "csInspectionLogStore", "csDispenseStore", "csDispenseLogStore",
        "txPatientStore", "txWaitlistStore", "txDonorStore", "txDonorIndexStore",
        "tbiScreeningStore", "tbiScreeningIndexStore", "ptRecordStore", "ptRegistryIndexStore",
        "ccrEntryStore", "ccrIndexStore", "ccrPatientStore", "ccrSiteIndexStore",
        "haiCaseStore", "haiCaseIndexStore", "haiOutbreakStore", "haiOutbreakIndexStore",
        "spPlanStore", "spPlanIndexStore", "spRiskStore", "spIndexStore",
        "cpExamStore", "cpExamIndexStore", "cpDbqStore", "cpDbqIndexStore",
        "qmIncidentStore", "qmIncidentIndexStore", "qmReviewStore", "qmReviewIndexStore",
        "paComplaintStore", "paComplaintIndexStore", "paCongressStore", "paCongressIndexStore",
        "roiRequestStore", "roiRequestIndexStore", "roiDisclosureStore", "roiDisclosureIndexStore",
        "rtChartStore", "rtChartIndexStore", "rtRequestStore", "rtRequestIndexStore",
        "gecAssessmentStore", "gecAssessmentIndexStore", "clcAdmissionStore", "clcAdmissionIndexStore",
        "homeCareEpisodeStore", "homeVisitStore", "homeVisitIndexStore",
        "homeCarePlanStore", "homeCareAssessmentStore", "homeCareCensusStore", "homeHealthBillingStore",
        "irbStudyStore", "irbStudyIndexStore", "irbSubjectStore", "irbSubjectIndexStore",
        "autoInstrumentStore", "instrumentIndexStore", "instrumentMessageQueueStore",
        "autoVerifyRulesStore", "shippingManifestStore", "shippingConfigStore",
        "orderSetStore", "orderSetIndexStore",
        "edVisitStore", "edBoardStore",
        "cmopTransmissionStore", "cmopSuspenseStore", "cmopTransmissionIndexStore",
        "btClaimStore", "btIndexStore",
        "bedStore", "bedBoardStore",
        "wardStockItemStore", "wardStockIndexStore", "wardReplenishLogStore",
        "incompleteRecordStore", "incompleteRecordIndexStore",
        "drgStore", "drgIndexStore",
        "lexiconTermStore", "lexiconIndexStore",
        "mpiCorrelationStore", "mpiSearchStore",
        "icnIssuerStore",
        "gpraSubmissionStore",
        "ndwExportRunStore",
        "diabetesRegistryStore", "diabetesRegistryIndexStore",
        "labEdiOrderStore", "labEdiResultStore", "labEdiConfigStore",
        "labEdiQueueStore", "labEdiIndexStore",
        "consultServiceStore",
        "mhInstrumentStore",
        "mailMessageStore", "userMailboxStore", "mailGroupStore",
        "bulletinStore", "mailGroupIndexStore",
        "patientAccessStore",
        "cptCodeStore", "cptCodeIndexStore",
        "loincCodeStore", "loincCodeIndexStore",
        "siteParametersStore", "patientVitalIndexStore", "patientOrderIndexStore",
        "patientNoteIndexStore",
        "accessControlStore",
        "smartClientStore", "smartClientIndexStore",
        "smartAuthStore", "bulkExportStore",
        "cqmMeasureStore", "cqmMeasureIndexStore", "cqmReportStore",
        "ecrTriggerStore", "ecrTriggerIndexStore", "ecrCaseStore", "ecrCaseIndexStore",
        "dsiInterventionStore", "dsiInterventionIndexStore", "dsiEventStore", "dsiEventIndexStore",
        "auditReportStore", "auditReportIndexStore",
        "directAddressStore", "directAddressIndexStore",
        "directMessageStore", "directMessageIndexStore",
        "patientSubmissionStore", "patientSubmissionIndexStore", "patientSubmissionQueueStore",
        "secureMessageThreadStore", "secureMessageIndexStore", "secureMessageQueueStore",
        "ds4pProcessorStore",
        "crReportStore", "crReportIndexStore",
        "patientAccountStore",
        "patientMergeStore",
        "immunizationForecastStore",
        "externalReferralStore",
        "externalReferralIndexStore",
        "iCareDashboardStore",
        "appointmentWaitListStore",
        "appointmentWaitListIndexStore",
        "patientRecallStore",
        "patientRecallIndexStore",
        "encounterFormTemplateStore",
        "encounterFormTemplateIndexStore",
        "encounterFormInstanceStore",
        "encounterFormInstanceIndexStore",
        "autoRefillStore",
        "autoRefillIndexStore",
        "mciIncidentStore",
        "mciIncidentIndexStore",
        "mciCasualtyStore",
        "mciCasualtyIndexStore",
        "periodontalChartStore",
        "periodontalChartIndexStore",
        "anesthesiaRecordStore",
        "anesthesiaRecordIndexStore",
        "careTeamStore",
        "providerPatientIndexStore",
        "providerScheduleIndexStore",
        "eligibilityInquiryStore",
        "eligibilityVerificationIndexStore",
        "payerConfigStore",
        "payerConfigIndexStore",
        "collectionLetterStore",
        "collectionLetterIndexStore",
        "arAgingReportStore",
        "claimStatusInquiryStore",
        "claimStatusInquiryIndexStore",
        "autoEligibilityDeterminationStore",
        "topMatchingStore",
        "topMatchIndexStore",
        "nursingTriageStore",
        "nursingTriageIndexStore",
        "nursingTaskWorklistStore",
        "nursingShiftHandoffStore",
        "shiftHandoffIndexStore",
        "painAssessmentStore",
        "painAssessmentIndexStore",
        "labWorklistStore",
        "labAccessionStore",
        "labAccessionIndexStore",
        "labQcStore",
        "radExamTrackingStore",
        "radWorklistStore",
        "radProtocolStore",
        "radProtocolIndexStore",
        "advanceDirectiveStore",
        "identityVerificationStore",
        "physTherapySessionStore",
        "physTherapySessionIndexStore",
        "physTherapyGoalStore",
        "physTherapyHepStore",
        "physTherapyReferralStore",
        "physTherapyReferralIndexStore",
        "providerAvailabilityStore",
        "providerUnavailabilityStore",
        "drugSafetyAdvisoryStore", "drugSafetyAdvisoryIndexStore", "patientSafetyAdvisoryStore",
        "drugClassCohortStore", "patientDrugClassIndexStore",
        "patientSummaryStore", "radiologyFindingStore"
    ];
}
