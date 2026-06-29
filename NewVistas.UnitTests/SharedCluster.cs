// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.Services;
using Orleans.Hosting;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Singleton TestCluster shared by all test fixtures. Registers every grain store,
/// memory streams, and default storage so that any grain can be activated.
/// Thread-safe lazy initialization — the cluster is built once on first access
/// and reused for the entire test run.
/// </summary>
public static class SharedCluster
{
    private static TestCluster? _cluster;
    private static readonly object _lock = new();

    public static TestCluster Instance
    {
        get
        {
            if (_cluster is not null) return _cluster;
            lock (_lock)
            {
                if (_cluster is not null) return _cluster;
                var builder = new TestClusterBuilder(1);
                builder.AddSiloBuilderConfigurator<AllStoresConfigurator>();
                builder.AddClientBuilderConfigurator<TransactionClientConfigurator>();
                // Build and deploy into a local, then publish the singleton only once it is
                // fully deployed. Assigning _cluster before Deploy() completes lets the
                // lock-free fast path above hand another fixture a non-deployed cluster
                // (null GrainFactory) under ParallelScope.Fixtures.
                var cluster = builder.Build();
                cluster.Deploy();
                _cluster = cluster;
                return _cluster;
            }
        }
    }

    // Enables Orleans ACID transactions on the test cluster client so tests can invoke
    // the transactional AR money-path grains directly via _cluster.GrainFactory.
    private class TransactionClientConfigurator : IClientBuilderConfigurator
    {
        public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
            => clientBuilder.UseTransactions();
    }

    private class AllStoresConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryStreams("LabStreams");

            // ACID transactions for the AR money paths (mirrors CommonSiloConfig).
            siloBuilder.UseTransactions();

            // Clinical event sourcing — JournaledGrain log-consistency provider
            // used by IPatientClinicalEventStreamGrain.
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");

            // Federation seam — default no-op sink so the stream grain's constructor
            // dependency resolves. Per-test fixtures may register their own sink
            // via a separate ISiloConfigurator (see ClinicalEventReplicationSinkTests).
            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, NullClinicalEventReplicationSink>();
            siloBuilder.Services.AddSingleton<IClusterIdentity>(new StaticClusterIdentity("TEST-CLUSTER", "099"));
            siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Eligibility.IRegistrationEligibilityPolicy,
                NewVistas.Abstractions.Eligibility.NoOpRegistrationEligibilityPolicy>();
            siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Reporting.IGpraSubmissionFormatter,
                NewVistas.Abstractions.Reporting.CsvGpraSubmissionFormatter>();
            siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Reporting.INdwExportFormatter,
                NewVistas.Abstractions.Reporting.CsvNdwExportFormatter>();
            siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Reporting.INdwExportSourceProvider,
                NewVistas.Abstractions.Reporting.PatientIndexNdwExportSourceProvider>();
            siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Federation.IMpiFederationAnnouncer,
                NewVistas.Abstractions.Federation.NoOpMpiFederationAnnouncer>();
            siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Federation.IMpiInboundHandler,
                NewVistas.Abstractions.Federation.DefaultMpiInboundHandler>();
            siloBuilder.Services.AddSingleton<IOutboxStatistics, NoOpOutboxStatistics>();

            // Route-vs-dose-form validation seam (injected into PharmacyGrain and
            // InpatientOrderGrain). RxNav client defaults to the offline no-op.
            siloBuilder.Services.AddSingleton<IRouteValidationService, RouteValidationService>();
            siloBuilder.Services.AddSingleton<IRxNavDoseFormClient, NullRxNavDoseFormClient>();
            siloBuilder.Services.AddSingleton<IOutboundPrescriptionTransmitter, NullOutboundPrescriptionTransmitter>();
            siloBuilder.Services.AddSingleton<IClinicalNarrativeService, TemplateClinicalNarrativeService>();
            siloBuilder.Services.AddSingleton<IRadiologyFindingExtractor, HeuristicRadiologyFindingExtractor>();

            // Silo-local caches for the StatelessWorker reader grains
            // (drug-interaction checker, patient search).
            siloBuilder.Services.AddSingleton<IDrugInteractionCacheService, DrugInteractionCacheService>();
            siloBuilder.Services.AddSingleton<IPatientIndexSnapshotService, PatientIndexSnapshotService>();

            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryGrainStorage("accessControlStore");
            siloBuilder.AddMemoryGrainStorage("adtStore");
            siloBuilder.AddMemoryGrainStorage("allergyStore");
            siloBuilder.AddMemoryGrainStorage("ambulatoryCopaySheetStore");
            siloBuilder.AddMemoryGrainStorage("apCaseIndexStore");
            siloBuilder.AddMemoryGrainStorage("apCaseStore");
            siloBuilder.AddMemoryGrainStorage("appointmentStore");
            siloBuilder.AddMemoryGrainStorage("arAccountIndexStore");
            siloBuilder.AddMemoryGrainStorage("arAccountStore");
            siloBuilder.AddMemoryGrainStorage("arBatchPaymentIndexStore");
            siloBuilder.AddMemoryGrainStorage("arBatchPaymentStore");
            siloBuilder.AddMemoryGrainStorage("arDebtorStore");
            siloBuilder.AddMemoryGrainStorage("arSiteParamsStore");
            siloBuilder.AddMemoryGrainStorage("arTransactionStore");
            siloBuilder.AddMemoryGrainStorage("auditEventStore");
            siloBuilder.AddMemoryGrainStorage("auditReportIndexStore");
            siloBuilder.AddMemoryGrainStorage("auditReportStore");
            siloBuilder.AddMemoryGrainStorage("autoInstrumentStore");
            siloBuilder.AddMemoryGrainStorage("autoVerifyRulesStore");
            siloBuilder.AddMemoryGrainStorage("bbCrossmatchIndexStore");
            siloBuilder.AddMemoryGrainStorage("bbCrossmatchStore");
            siloBuilder.AddMemoryGrainStorage("bbPatientStore");
            siloBuilder.AddMemoryGrainStorage("bbTransfusionIndexStore");
            siloBuilder.AddMemoryGrainStorage("bbTransfusionStore");
            siloBuilder.AddMemoryGrainStorage("bbUnitIndexStore");
            siloBuilder.AddMemoryGrainStorage("bbUnitStore");
            siloBuilder.AddMemoryGrainStorage("bcmaMarStore");
            siloBuilder.AddMemoryGrainStorage("bcmaStore");
            siloBuilder.AddMemoryGrainStorage("bedBoardStore");
            siloBuilder.AddMemoryGrainStorage("bedStore");
            siloBuilder.AddMemoryGrainStorage("brAdmissionIndexStore");
            siloBuilder.AddMemoryGrainStorage("brAdmissionStore");
            siloBuilder.AddMemoryGrainStorage("brCenterIndexStore");
            siloBuilder.AddMemoryGrainStorage("brCenterStore");
            siloBuilder.AddMemoryGrainStorage("brOutpatientVisitIndexStore");
            siloBuilder.AddMemoryGrainStorage("brOutpatientVisitStore");
            siloBuilder.AddMemoryGrainStorage("brPatientStore");
            siloBuilder.AddMemoryGrainStorage("btClaimStore");
            siloBuilder.AddMemoryGrainStorage("btIndexStore");
            siloBuilder.AddMemoryGrainStorage("bulkExportStore");
            siloBuilder.AddMemoryGrainStorage("bulletinStore");
            siloBuilder.AddMemoryGrainStorage("cashierReceiptIndexStore");
            siloBuilder.AddMemoryGrainStorage("cashierReceiptStore");
            siloBuilder.AddMemoryGrainStorage("cashierSessionIndexStore");
            siloBuilder.AddMemoryGrainStorage("cashierSessionStore");
            siloBuilder.AddMemoryGrainStorage("catastrophicDisabilityIndexStore");
            siloBuilder.AddMemoryGrainStorage("ccrEntryStore");
            siloBuilder.AddMemoryGrainStorage("ccrIndexStore");
            siloBuilder.AddMemoryGrainStorage("ccrPatientStore");
            siloBuilder.AddMemoryGrainStorage("ccrSiteIndexStore");
            siloBuilder.AddMemoryGrainStorage("clcAdmissionIndexStore");
            siloBuilder.AddMemoryGrainStorage("clcAdmissionStore");
            siloBuilder.AddMemoryGrainStorage("clinicIndexStore");
            siloBuilder.AddMemoryGrainStorage("clinicStore");
            siloBuilder.AddMemoryGrainStorage("clinicalReminderStore");
            siloBuilder.AddMemoryGrainStorage("cmopSuspenseStore");
            siloBuilder.AddMemoryGrainStorage("cmopTransmissionIndexStore");
            siloBuilder.AddMemoryGrainStorage("cmopTransmissionStore");
            siloBuilder.AddMemoryGrainStorage("consultServiceStore");
            siloBuilder.AddMemoryGrainStorage("consultStore");
            siloBuilder.AddMemoryGrainStorage("cpDbqIndexStore");
            siloBuilder.AddMemoryGrainStorage("cpDbqStore");
            siloBuilder.AddMemoryGrainStorage("cpExamIndexStore");
            siloBuilder.AddMemoryGrainStorage("cpExamStore");
            siloBuilder.AddMemoryGrainStorage("cpProcedureIndexStore");
            siloBuilder.AddMemoryGrainStorage("cpProcedureStore");
            siloBuilder.AddMemoryGrainStorage("cptCodeIndexStore");
            siloBuilder.AddMemoryGrainStorage("cptCodeStore");
            siloBuilder.AddMemoryGrainStorage("cqmMeasureIndexStore");
            siloBuilder.AddMemoryGrainStorage("cqmMeasureStore");
            siloBuilder.AddMemoryGrainStorage("cqmReportStore");
            siloBuilder.AddMemoryGrainStorage("crReportIndexStore");
            siloBuilder.AddMemoryGrainStorage("crReportStore");
            siloBuilder.AddMemoryGrainStorage("csDispenseLogStore");
            siloBuilder.AddMemoryGrainStorage("csDispenseStore");
            siloBuilder.AddMemoryGrainStorage("csInspectionLogStore");
            siloBuilder.AddMemoryGrainStorage("csInspectionStore");
            siloBuilder.AddMemoryGrainStorage("daLocationStore");
            siloBuilder.AddMemoryGrainStorage("dentalPatientStore");
            siloBuilder.AddMemoryGrainStorage("dentalTreatmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("dentalTreatmentStore");
            siloBuilder.AddMemoryGrainStorage("directAddressIndexStore");
            siloBuilder.AddMemoryGrainStorage("directAddressStore");
            siloBuilder.AddMemoryGrainStorage("directMessageIndexStore");
            siloBuilder.AddMemoryGrainStorage("directMessageStore");
            siloBuilder.AddMemoryGrainStorage("doseUnitStore");
            siloBuilder.AddMemoryGrainStorage("doseFormRouteStore");
            siloBuilder.AddMemoryGrainStorage("drgIndexStore");
            siloBuilder.AddMemoryGrainStorage("drgStore");
            siloBuilder.AddMemoryGrainStorage("drugAccountabilityStore");
            siloBuilder.AddMemoryGrainStorage("drugFileStore");
            siloBuilder.AddMemoryGrainStorage("drugIndexStore");
            siloBuilder.AddMemoryGrainStorage("drugInteractionStore");
            siloBuilder.AddMemoryGrainStorage("durAssessmentStore");
            siloBuilder.AddMemoryGrainStorage("durAssessmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("interactionScreeningStore");
            siloBuilder.AddMemoryGrainStorage("interactionScreeningIndexStore");
            siloBuilder.AddMemoryGrainStorage("ds4pProcessorStore");
            siloBuilder.AddMemoryGrainStorage("dsiEventIndexStore");
            siloBuilder.AddMemoryGrainStorage("dsiEventStore");
            siloBuilder.AddMemoryGrainStorage("dsiInterventionIndexStore");
            siloBuilder.AddMemoryGrainStorage("dsiInterventionStore");
            siloBuilder.AddMemoryGrainStorage("dssUnitIndexStore");
            siloBuilder.AddMemoryGrainStorage("dssUnitStore");
            siloBuilder.AddMemoryGrainStorage("ecEncounterIndexStore");
            siloBuilder.AddMemoryGrainStorage("ecEncounterStore");
            siloBuilder.AddMemoryGrainStorage("ecPatientStore");
            siloBuilder.AddMemoryGrainStorage("ecrCaseIndexStore");
            siloBuilder.AddMemoryGrainStorage("ecrCaseStore");
            siloBuilder.AddMemoryGrainStorage("ecrTriggerIndexStore");
            siloBuilder.AddMemoryGrainStorage("ecrTriggerStore");
            siloBuilder.AddMemoryGrainStorage("edBoardStore");
            siloBuilder.AddMemoryGrainStorage("edVisitStore");
            siloBuilder.AddMemoryGrainStorage("ediClaimIndexStore");
            siloBuilder.AddMemoryGrainStorage("ediClaimStore");
            siloBuilder.AddMemoryGrainStorage("ediTransmissionIndexStore");
            siloBuilder.AddMemoryGrainStorage("ediTransmissionStore");
            siloBuilder.AddMemoryGrainStorage("engFacilityIndexStore");
            siloBuilder.AddMemoryGrainStorage("engFacilityStore");
            siloBuilder.AddMemoryGrainStorage("engWorkOrderIndexStore");
            siloBuilder.AddMemoryGrainStorage("engWorkOrderStore");
            siloBuilder.AddMemoryGrainStorage("enrollmentStatusIndexStore");
            siloBuilder.AddMemoryGrainStorage("epcsProviderIndexStore");
            siloBuilder.AddMemoryGrainStorage("epcsProviderStore");
            siloBuilder.AddMemoryGrainStorage("epcsRxIndexStore");
            siloBuilder.AddMemoryGrainStorage("epcsRxStore");
            siloBuilder.AddMemoryGrainStorage("eraIndexStore");
            siloBuilder.AddMemoryGrainStorage("eraStore");
            siloBuilder.AddMemoryGrainStorage("externalReferralIndexStore");
            siloBuilder.AddMemoryGrainStorage("externalReferralStore");
            siloBuilder.AddMemoryGrainStorage("feeAuthorizationIndexStore");
            siloBuilder.AddMemoryGrainStorage("feeAuthorizationStore");
            siloBuilder.AddMemoryGrainStorage("feeBatchPaymentIndexStore");
            siloBuilder.AddMemoryGrainStorage("feeBatchPaymentStore");
            siloBuilder.AddMemoryGrainStorage("feeInvoiceIndexStore");
            siloBuilder.AddMemoryGrainStorage("feeInvoiceStore");
            siloBuilder.AddMemoryGrainStorage("feePatientStore");
            siloBuilder.AddMemoryGrainStorage("feeSiteParamsStore");
            siloBuilder.AddMemoryGrainStorage("feeVendorIndexStore");
            siloBuilder.AddMemoryGrainStorage("feeVendorStore");
            siloBuilder.AddMemoryGrainStorage("formularyStore");
            siloBuilder.AddMemoryGrainStorage("gecAssessmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("gecAssessmentStore");
            siloBuilder.AddMemoryGrainStorage("gpraReportIndexStore");
            siloBuilder.AddMemoryGrainStorage("gpraReportStore");
            siloBuilder.AddMemoryGrainStorage("haiCaseIndexStore");
            siloBuilder.AddMemoryGrainStorage("haiCaseStore");
            siloBuilder.AddMemoryGrainStorage("haiOutbreakIndexStore");
            siloBuilder.AddMemoryGrainStorage("haiOutbreakStore");
            siloBuilder.AddMemoryGrainStorage("homeCareEpisodeStore");
            siloBuilder.AddMemoryGrainStorage("homeVisitStore");
            siloBuilder.AddMemoryGrainStorage("homeVisitIndexStore");
            siloBuilder.AddMemoryGrainStorage("homeCarePlanStore");
            siloBuilder.AddMemoryGrainStorage("homeCareAssessmentStore");
            siloBuilder.AddMemoryGrainStorage("homeCareCensusStore");
            siloBuilder.AddMemoryGrainStorage("healthFactorStore");
            siloBuilder.AddMemoryGrainStorage("healthSummaryIndexStore");
            siloBuilder.AddMemoryGrainStorage("healthSummaryStore");
            siloBuilder.AddMemoryGrainStorage("healthSummaryTypeIndexStore");
            siloBuilder.AddMemoryGrainStorage("healthSummaryTypeStore");
            siloBuilder.AddMemoryGrainStorage("htAlertIndexStore");
            siloBuilder.AddMemoryGrainStorage("htAlertStore");
            siloBuilder.AddMemoryGrainStorage("htDeviceIndexStore");
            siloBuilder.AddMemoryGrainStorage("htDeviceStore");
            siloBuilder.AddMemoryGrainStorage("htPatientStore");
            siloBuilder.AddMemoryGrainStorage("htReadingIndexStore");
            siloBuilder.AddMemoryGrainStorage("htReadingStore");
            siloBuilder.AddMemoryGrainStorage("iCareDashboardStore");
            siloBuilder.AddMemoryGrainStorage("ibBillingActionIndexStore");
            siloBuilder.AddMemoryGrainStorage("ibBillingActionStore");
            siloBuilder.AddMemoryGrainStorage("ibBillingPatientStore");
            siloBuilder.AddMemoryGrainStorage("ibSiteParamsStore");
            siloBuilder.AddMemoryGrainStorage("icd10Store");
            siloBuilder.AddMemoryGrainStorage("ifcapControlPointIndexStore");
            siloBuilder.AddMemoryGrainStorage("ifcapControlPointStore");
            siloBuilder.AddMemoryGrainStorage("ifcapPurchaseOrderIndexStore");
            siloBuilder.AddMemoryGrainStorage("ifcapPurchaseOrderStore");
            siloBuilder.AddMemoryGrainStorage("ifcapPurchaseRequestIndexStore");
            siloBuilder.AddMemoryGrainStorage("ifcapPurchaseRequestStore");
            siloBuilder.AddMemoryGrainStorage("ifcapReceivingReportIndexStore");
            siloBuilder.AddMemoryGrainStorage("ifcapReceivingReportStore");
            siloBuilder.AddMemoryGrainStorage("ifcapSiteParamsStore");
            siloBuilder.AddMemoryGrainStorage("ifcapVendorIndexStore");
            siloBuilder.AddMemoryGrainStorage("ifcapVendorStore");
            siloBuilder.AddMemoryGrainStorage("imagingStore");
            siloBuilder.AddMemoryGrainStorage("immunizationForecastStore");
            siloBuilder.AddMemoryGrainStorage("incomeHouseholdStore");
            siloBuilder.AddMemoryGrainStorage("incomeThresholdStore");
            siloBuilder.AddMemoryGrainStorage("incompleteRecordIndexStore");
            siloBuilder.AddMemoryGrainStorage("incompleteRecordStore");
            siloBuilder.AddMemoryGrainStorage("inpatientOrderStore");
            siloBuilder.AddMemoryGrainStorage("inpatientProfileStore");
            siloBuilder.AddMemoryGrainStorage("instrumentIndexStore");
            siloBuilder.AddMemoryGrainStorage("instrumentMessageQueueStore");
            siloBuilder.AddMemoryGrainStorage("insurancePlanIndexStore");
            siloBuilder.AddMemoryGrainStorage("insurancePlanStore");
            siloBuilder.AddMemoryGrainStorage("irbStudyIndexStore");
            siloBuilder.AddMemoryGrainStorage("irbStudyStore");
            siloBuilder.AddMemoryGrainStorage("irbSubjectIndexStore");
            siloBuilder.AddMemoryGrainStorage("irbSubjectStore");
            siloBuilder.AddMemoryGrainStorage("ivAdmixOrderIndexStore");
            siloBuilder.AddMemoryGrainStorage("ivAdmixOrderStore");
            siloBuilder.AddMemoryGrainStorage("labBatchStore");
            siloBuilder.AddMemoryGrainStorage("labEdiConfigStore");
            siloBuilder.AddMemoryGrainStorage("labEdiIndexStore");
            siloBuilder.AddMemoryGrainStorage("labEdiOrderStore");
            siloBuilder.AddMemoryGrainStorage("labEdiQueueStore");
            siloBuilder.AddMemoryGrainStorage("labEdiResultStore");
            siloBuilder.AddMemoryGrainStorage("labIndexStore");
            siloBuilder.AddMemoryGrainStorage("labSummaryStore");
            siloBuilder.AddMemoryGrainStorage("labSurveillanceTaxonomyIndexStore");
            siloBuilder.AddMemoryGrainStorage("labSurveillanceTaxonomyStore");
            siloBuilder.AddMemoryGrainStorage("labTestStore");
            siloBuilder.AddMemoryGrainStorage("lexiconIndexStore");
            siloBuilder.AddMemoryGrainStorage("lexiconTermStore");
            siloBuilder.AddMemoryGrainStorage("loincCodeIndexStore");
            siloBuilder.AddMemoryGrainStorage("loincCodeStore");
            siloBuilder.AddMemoryGrainStorage("mailGroupIndexStore");
            siloBuilder.AddMemoryGrainStorage("mailGroupStore");
            siloBuilder.AddMemoryGrainStorage("mailMessageStore");
            siloBuilder.AddMemoryGrainStorage("meansTestBillingClockStore");
            siloBuilder.AddMemoryGrainStorage("medProcedureIndexStore");
            siloBuilder.AddMemoryGrainStorage("medProcedureStore");
            siloBuilder.AddMemoryGrainStorage("medRouteStore");
            siloBuilder.AddMemoryGrainStorage("mentalHealthStore");
            siloBuilder.AddMemoryGrainStorage("mhInstrumentStore");
            siloBuilder.AddMemoryGrainStorage("mpiCorrelationStore");
            siloBuilder.AddMemoryGrainStorage("mpiSearchStore");
            siloBuilder.AddMemoryGrainStorage("icnIssuerStore");
            siloBuilder.AddMemoryGrainStorage("gpraSubmissionStore");
            siloBuilder.AddMemoryGrainStorage("ndwExportRunStore");
            siloBuilder.AddMemoryGrainStorage("diabetesRegistryStore");
            siloBuilder.AddMemoryGrainStorage("diabetesRegistryIndexStore");
            siloBuilder.AddMemoryGrainStorage("mstHistoryStore");
            siloBuilder.AddMemoryGrainStorage("ndfClassIndexStore");
            siloBuilder.AddMemoryGrainStorage("ndfGenericIndexStore");
            siloBuilder.AddMemoryGrainStorage("ndfProductIndexStore");
            siloBuilder.AddMemoryGrainStorage("newPersonStore");
            siloBuilder.AddMemoryGrainStorage("providerDirectoryStore");
            siloBuilder.AddMemoryGrainStorage("pharmacyDirectoryStore");
            siloBuilder.AddMemoryGrainStorage("notificationStore");
            siloBuilder.AddMemoryGrainStorage("nursingAcuityStore");
            siloBuilder.AddMemoryGrainStorage("nursingAssessmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("nursingAssessmentStore");
            siloBuilder.AddMemoryGrainStorage("nursingCarePlanStore");
            siloBuilder.AddMemoryGrainStorage("nursingUnitIndexStore");
            siloBuilder.AddMemoryGrainStorage("nursingUnitStore");
            siloBuilder.AddMemoryGrainStorage("oncTreatmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("oncTreatmentStore");
            siloBuilder.AddMemoryGrainStorage("oncTumorIndexStore");
            siloBuilder.AddMemoryGrainStorage("oncTumorStore");
            siloBuilder.AddMemoryGrainStorage("orderSetIndexStore");
            siloBuilder.AddMemoryGrainStorage("orderSetStore");
            siloBuilder.AddMemoryGrainStorage("orderStore");
            siloBuilder.AddMemoryGrainStorage("orderableItemIndexStore");
            siloBuilder.AddMemoryGrainStorage("orderableItemStore");
            siloBuilder.AddMemoryGrainStorage("paComplaintIndexStore");
            siloBuilder.AddMemoryGrainStorage("paComplaintStore");
            siloBuilder.AddMemoryGrainStorage("paCongressIndexStore");
            siloBuilder.AddMemoryGrainStorage("paCongressStore");
            siloBuilder.AddMemoryGrainStorage("patientAccessStore");
            siloBuilder.AddMemoryGrainStorage("patientAuditIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientBenefitStore");
            siloBuilder.AddMemoryGrainStorage("patientEnrollmentStore");
            siloBuilder.AddMemoryGrainStorage("patientIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientHistoryIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientMergeStore");
            siloBuilder.AddMemoryGrainStorage("patientNoteIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientOrderIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientRelationStore");
            siloBuilder.AddMemoryGrainStorage("patientStore");
            siloBuilder.AddMemoryGrainStorage("patientSubmissionIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientSubmissionQueueStore");
            siloBuilder.AddMemoryGrainStorage("patientSubmissionStore");
            siloBuilder.AddMemoryGrainStorage("patientVitalIndexStore");
            siloBuilder.AddMemoryGrainStorage("physTherapySessionStore");
            siloBuilder.AddMemoryGrainStorage("physTherapySessionIndexStore");
            siloBuilder.AddMemoryGrainStorage("physTherapyGoalStore");
            siloBuilder.AddMemoryGrainStorage("physTherapyHepStore");
            siloBuilder.AddMemoryGrainStorage("physTherapyReferralStore");
            siloBuilder.AddMemoryGrainStorage("physTherapyReferralIndexStore");
            siloBuilder.AddMemoryGrainStorage("pccSurvConfigIndexStore");
            siloBuilder.AddMemoryGrainStorage("pccSurvConfigStore");
            siloBuilder.AddMemoryGrainStorage("pccSurvMatchIndexStore");
            siloBuilder.AddMemoryGrainStorage("pccSurvMatchStore");
            siloBuilder.AddMemoryGrainStorage("personalPolicyIndexStore");
            siloBuilder.AddMemoryGrainStorage("personalPolicyStore");
            siloBuilder.AddMemoryGrainStorage("pharmacyStore");
            siloBuilder.AddMemoryGrainStorage("posClaimIndexStore");
            siloBuilder.AddMemoryGrainStorage("posClaimStore");
            siloBuilder.AddMemoryGrainStorage("posInsurerIndexStore");
            siloBuilder.AddMemoryGrainStorage("posInsurerStore");
            siloBuilder.AddMemoryGrainStorage("pregnancyIndexStore");
            siloBuilder.AddMemoryGrainStorage("pregnancyStore");
            siloBuilder.AddMemoryGrainStorage("prenatalVisitIndexStore");
            siloBuilder.AddMemoryGrainStorage("prenatalVisitStore");
            siloBuilder.AddMemoryGrainStorage("prescriptionIndexStore");
            siloBuilder.AddMemoryGrainStorage("prfAssignmentStore");
            siloBuilder.AddMemoryGrainStorage("prfNationalFlagIndexStore");
            siloBuilder.AddMemoryGrainStorage("priorAuthIndexStore");
            siloBuilder.AddMemoryGrainStorage("priorAuthStore");
            siloBuilder.AddMemoryGrainStorage("problemStore");
            siloBuilder.AddMemoryGrainStorage("ptRecordStore");
            siloBuilder.AddMemoryGrainStorage("ptRegistryIndexStore");
            siloBuilder.AddMemoryGrainStorage("qmIncidentIndexStore");
            siloBuilder.AddMemoryGrainStorage("qmIncidentStore");
            siloBuilder.AddMemoryGrainStorage("qmReviewIndexStore");
            siloBuilder.AddMemoryGrainStorage("qmReviewStore");
            siloBuilder.AddMemoryGrainStorage("radiologyStore");
            siloBuilder.AddMemoryGrainStorage("roiDisclosureIndexStore");
            siloBuilder.AddMemoryGrainStorage("roiDisclosureStore");
            siloBuilder.AddMemoryGrainStorage("roiRequestIndexStore");
            siloBuilder.AddMemoryGrainStorage("roiRequestStore");
            siloBuilder.AddMemoryGrainStorage("rtChartIndexStore");
            siloBuilder.AddMemoryGrainStorage("rtChartStore");
            siloBuilder.AddMemoryGrainStorage("rtCourseIndexStore");
            siloBuilder.AddMemoryGrainStorage("rtCourseStore");
            siloBuilder.AddMemoryGrainStorage("rtRequestIndexStore");
            siloBuilder.AddMemoryGrainStorage("rtRequestStore");
            siloBuilder.AddMemoryGrainStorage("rtTreatmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("rtTreatmentStore");
            siloBuilder.AddMemoryGrainStorage("saEpisodeIndexStore");
            siloBuilder.AddMemoryGrainStorage("saEpisodeStore");
            siloBuilder.AddMemoryGrainStorage("saVisitIndexStore");
            siloBuilder.AddMemoryGrainStorage("saVisitStore");
            siloBuilder.AddMemoryGrainStorage("scheduleIndexStore");
            siloBuilder.AddMemoryGrainStorage("sciIndexStore");
            siloBuilder.AddMemoryGrainStorage("sciPatientStore");
            siloBuilder.AddMemoryGrainStorage("secureMessageIndexStore");
            siloBuilder.AddMemoryGrainStorage("secureMessageQueueStore");
            siloBuilder.AddMemoryGrainStorage("secureMessageThreadStore");
            siloBuilder.AddMemoryGrainStorage("shippingConfigStore");
            siloBuilder.AddMemoryGrainStorage("shippingManifestStore");
            siloBuilder.AddMemoryGrainStorage("siteParametersStore");
            siloBuilder.AddMemoryGrainStorage("smartAuthStore");
            siloBuilder.AddMemoryGrainStorage("smartClientIndexStore");
            siloBuilder.AddMemoryGrainStorage("smartClientStore");
            siloBuilder.AddMemoryGrainStorage("socialWorkAssessmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("socialWorkAssessmentStore");
            siloBuilder.AddMemoryGrainStorage("socialWorkReferralIndexStore");
            siloBuilder.AddMemoryGrainStorage("socialWorkReferralStore");
            siloBuilder.AddMemoryGrainStorage("spIndexStore");
            siloBuilder.AddMemoryGrainStorage("spPlanIndexStore");
            siloBuilder.AddMemoryGrainStorage("spPlanStore");
            siloBuilder.AddMemoryGrainStorage("spRiskStore");
            siloBuilder.AddMemoryGrainStorage("surgeryStore");
            siloBuilder.AddMemoryGrainStorage("tbiScreeningIndexStore");
            siloBuilder.AddMemoryGrainStorage("tbiScreeningStore");
            siloBuilder.AddMemoryGrainStorage("tiuDocumentStore");
            siloBuilder.AddMemoryGrainStorage("topReferralIndexStore");
            siloBuilder.AddMemoryGrainStorage("topReferralStore");
            siloBuilder.AddMemoryGrainStorage("treatingFacilityListStore");
            siloBuilder.AddMemoryGrainStorage("txDonorIndexStore");
            siloBuilder.AddMemoryGrainStorage("txDonorStore");
            siloBuilder.AddMemoryGrainStorage("txPatientStore");
            siloBuilder.AddMemoryGrainStorage("txWaitlistStore");
            siloBuilder.AddMemoryGrainStorage("userMailboxStore");
            siloBuilder.AddMemoryGrainStorage("vaProductStore");
            siloBuilder.AddMemoryGrainStorage("visitIndexStore");
            siloBuilder.AddMemoryGrainStorage("visitStore");
            siloBuilder.AddMemoryGrainStorage("vitalStore");
            siloBuilder.AddMemoryGrainStorage("vsIndexStore");
            siloBuilder.AddMemoryGrainStorage("vsVolunteerStore");
            siloBuilder.AddMemoryGrainStorage("wardCensusStore");
            siloBuilder.AddMemoryGrainStorage("wardLocationStore");
            siloBuilder.AddMemoryGrainStorage("wardReplenishLogStore");
            siloBuilder.AddMemoryGrainStorage("wardStockIndexStore");
            siloBuilder.AddMemoryGrainStorage("wardStockItemStore");
            siloBuilder.AddMemoryGrainStorage("womensHealthIndexStore");
            siloBuilder.AddMemoryGrainStorage("womensHealthNotificationStore");
            siloBuilder.AddMemoryGrainStorage("appointmentWaitListStore");
            siloBuilder.AddMemoryGrainStorage("appointmentWaitListIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientRecallStore");
            siloBuilder.AddMemoryGrainStorage("patientRecallIndexStore");
            siloBuilder.AddMemoryGrainStorage("encounterFormTemplateStore");
            siloBuilder.AddMemoryGrainStorage("encounterFormTemplateIndexStore");
            siloBuilder.AddMemoryGrainStorage("encounterFormInstanceStore");
            siloBuilder.AddMemoryGrainStorage("encounterFormInstanceIndexStore");
            siloBuilder.AddMemoryGrainStorage("autoRefillStore");
            siloBuilder.AddMemoryGrainStorage("autoRefillIndexStore");
            siloBuilder.AddMemoryGrainStorage("mciIncidentStore");
            siloBuilder.AddMemoryGrainStorage("mciIncidentIndexStore");
            siloBuilder.AddMemoryGrainStorage("mciCasualtyStore");
            siloBuilder.AddMemoryGrainStorage("mciCasualtyIndexStore");
            siloBuilder.AddMemoryGrainStorage("periodontalChartStore");
            siloBuilder.AddMemoryGrainStorage("periodontalChartIndexStore");
            siloBuilder.AddMemoryGrainStorage("anesthesiaRecordStore");
            siloBuilder.AddMemoryGrainStorage("anesthesiaRecordIndexStore");
            siloBuilder.AddMemoryGrainStorage("careTeamStore");
            siloBuilder.AddMemoryGrainStorage("providerPatientIndexStore");
            siloBuilder.AddMemoryGrainStorage("providerScheduleIndexStore");
            siloBuilder.AddMemoryGrainStorage("eligibilityInquiryStore");
            siloBuilder.AddMemoryGrainStorage("eligibilityVerificationIndexStore");
            siloBuilder.AddMemoryGrainStorage("payerConfigStore");
            siloBuilder.AddMemoryGrainStorage("payerConfigIndexStore");
            siloBuilder.AddMemoryGrainStorage("collectionLetterStore");
            siloBuilder.AddMemoryGrainStorage("collectionLetterIndexStore");
            siloBuilder.AddMemoryGrainStorage("arAgingReportStore");
            siloBuilder.AddMemoryGrainStorage("claimStatusInquiryStore");
            siloBuilder.AddMemoryGrainStorage("claimStatusInquiryIndexStore");
            siloBuilder.AddMemoryGrainStorage("autoEligibilityDeterminationStore");
            siloBuilder.AddMemoryGrainStorage("topMatchingStore");
            siloBuilder.AddMemoryGrainStorage("topMatchIndexStore");
            siloBuilder.AddMemoryGrainStorage("nursingTriageStore");
            siloBuilder.AddMemoryGrainStorage("nursingTriageIndexStore");
            siloBuilder.AddMemoryGrainStorage("nursingTaskWorklistStore");
            siloBuilder.AddMemoryGrainStorage("nursingShiftHandoffStore");
            siloBuilder.AddMemoryGrainStorage("shiftHandoffIndexStore");
            siloBuilder.AddMemoryGrainStorage("painAssessmentStore");
            siloBuilder.AddMemoryGrainStorage("painAssessmentIndexStore");
            siloBuilder.AddMemoryGrainStorage("labWorklistStore");
            siloBuilder.AddMemoryGrainStorage("labAccessionStore");
            siloBuilder.AddMemoryGrainStorage("labAccessionIndexStore");
            siloBuilder.AddMemoryGrainStorage("labQcStore");
            siloBuilder.AddMemoryGrainStorage("radExamTrackingStore");
            siloBuilder.AddMemoryGrainStorage("radWorklistStore");
            siloBuilder.AddMemoryGrainStorage("radProtocolStore");
            siloBuilder.AddMemoryGrainStorage("radProtocolIndexStore");
            siloBuilder.AddMemoryGrainStorage("advanceDirectiveStore");
            siloBuilder.AddMemoryGrainStorage("identityVerificationStore");
            siloBuilder.AddMemoryGrainStorage("providerAvailabilityStore");
            siloBuilder.AddMemoryGrainStorage("providerUnavailabilityStore");
            siloBuilder.AddMemoryGrainStorage("provisioningTokenStore");
            siloBuilder.AddMemoryGrainStorage("provisioningTokenIndexStore");
            siloBuilder.AddMemoryGrainStorage("revocationRegistryStore");
            siloBuilder.AddMemoryGrainStorage("drugSafetyAdvisoryStore");
            siloBuilder.AddMemoryGrainStorage("drugSafetyAdvisoryIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientSafetyAdvisoryStore");
            siloBuilder.AddMemoryGrainStorage("drugClassCohortStore");
            siloBuilder.AddMemoryGrainStorage("patientDrugClassIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientSummaryStore");
            siloBuilder.AddMemoryGrainStorage("radiologyFindingStore");
        }
    }
}
