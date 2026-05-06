// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.Security;
using NewVistas.PT.Models;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Shell ViewModel — owns sidebar navigation, the currently displayed page ViewModel,
/// user info display, and logout functionality.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public PatientContext PatientContext { get; }
    public AuthService AuthService { get; }
    public ObservableCollection<NavSection> NavSections { get; } = new();

    public MainViewModel(IServiceProvider services, PatientContext patientContext, AuthService authService)
    {
        _services = services;
        PatientContext = patientContext;
        AuthService = authService;
        BuildNavSections();
        FilterNavSections();

        // Start on Home
        var homeItem = NavSections.SelectMany(s => s.Items).First(i => i.Label == "Home");
        Navigate(homeItem);

        // Listen for cross-VM navigation requests (e.g., Cover Sheet → Orders)
        PatientContext.NavigationRequested += targetLabel =>
        {
            var target = NavSections.SelectMany(s => s.Items).FirstOrDefault(i => i.Label == targetLabel);
            if (target != null)
                Navigate(target);
        };
    }

    [RelayCommand]
    private void SearchPatient(string? query)
    {
        // Navigate to Patient Lookup with the search query as a pending action
        PatientContext.RequestNavigation("Patient Lookup", $"SEARCH:{query}");
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        await AuthService.LogoutAsync();

        // Close the MainWindow — App.xaml.cs handles showing the login window
        Application.Current?.MainWindow?.Close();
    }

    private void BuildNavSections()
    {
        NavSections.Add(new NavSection(string.Empty,
        [
            Nav("🏠", "Home", () => _services.GetRequiredService<HomeViewModel>()),
            Nav("🔍", "Patient Lookup", () => _services.GetRequiredService<PatientLookupViewModel>()),
            Nav("✏️", "Edit Patient", () => _services.GetRequiredService<PatientEditViewModel>()),
        ]));

        NavSections.Add(new NavSection("Clinical",
        [
            Nav("📋", "Cover Sheet",       () => _services.GetRequiredService<CoverSheetViewModel>(), MenuArea.Clinical),
            Nav("⚠️", "Problems",           () => _services.GetRequiredService<ProblemsViewModel>(), MenuArea.Clinical),
            Nav("💊", "Medications",        () => _services.GetRequiredService<MedicationsViewModel>(), MenuArea.Clinical),
            Nav("📝", "Orders",             () => _services.GetRequiredService<OrdersViewModel>(), MenuArea.Clinical),
            Nav("🗒️", "Notes",              () => _services.GetRequiredService<NotesViewModel>(), MenuArea.Clinical),
            Nav("🩺", "Consults",           () => _services.GetRequiredService<ConsultsViewModel>(), MenuArea.Clinical),
            Nav("🔬", "Labs",               () => _services.GetRequiredService<LabsViewModel>(), MenuArea.Laboratory),
            Nav("❤️", "Vitals",             () => _services.GetRequiredService<VitalsViewModel>(), MenuArea.Clinical),
            Nav("🚫", "Allergies",          () => _services.GetRequiredService<AllergiesViewModel>(), MenuArea.Clinical),
            Nav("💉", "Immunizations",      () => _services.GetRequiredService<ImmunizationsViewModel>(), MenuArea.Clinical),
            Nav("🏥", "Surgery",            () => _services.GetRequiredService<SurgeryViewModel>(), MenuArea.Surgery),
            Nav("📡", "Radiology",          () => _services.GetRequiredService<RadiologyViewModel>(), MenuArea.Radiology),
            Nav("🖼️", "Imaging",            () => _services.GetRequiredService<ImagingViewModel>(), MenuArea.Radiology),
            Nav("💊", "BCMA",               () => _services.GetRequiredService<BcmaViewModel>(), MenuArea.Clinical),
            Nav("🧠", "Mental Health",      () => _services.GetRequiredService<MentalHealthViewModel>(), MenuArea.MentalHealth),
            Nav("🥗", "Dietetics",          () => _services.GetRequiredService<DieteticsViewModel>(), MenuArea.Clinical),
            Nav("⏰", "Reminders",          () => _services.GetRequiredService<RemindersViewModel>(), MenuArea.Clinical),
            Nav("📊", "Health Factors",     () => _services.GetRequiredService<HealthFactorsViewModel>(), MenuArea.Clinical),
            Nav("📅", "Scheduling",         () => _services.GetRequiredService<SchedulingViewModel>(), MenuArea.Registration),
            Nav("🏥", "PCE",                () => _services.GetRequiredService<PceViewModel>(), MenuArea.Clinical),
            Nav("👥", "Care Team",          () => _services.GetRequiredService<CareTeamViewModel>(), MenuArea.Clinical),
            Nav("😖", "Pain Assessment",    () => _services.GetRequiredService<PainAssessmentViewModel>(), MenuArea.Clinical),
            Nav("📋", "Shift Handoff",      () => _services.GetRequiredService<ShiftHandoffViewModel>(), MenuArea.Clinical),
            Nav("📋", "Nursing Care Plan",  () => _services.GetRequiredService<NursingCarePlanViewModel>(), MenuArea.Clinical),
            Nav("📋", "Nursing Tasks",      () => _services.GetRequiredService<NursingTaskWorklistViewModel>(), MenuArea.Clinical),
            Nav("🚑", "Nursing Triage",     () => _services.GetRequiredService<NursingTriageViewModel>(), MenuArea.Clinical),
            Nav("🔬", "Lab Tech",           () => _services.GetRequiredService<LabTechViewModel>(), MenuArea.Laboratory),
            Nav("📡", "Rad Tech",           () => _services.GetRequiredService<RadTechViewModel>(), MenuArea.Radiology),
            Nav("📊", "Provider Dashboard", () => _services.GetRequiredService<ProviderDashboardViewModel>(), MenuArea.Clinical),
        ]));

        NavSections.Add(new NavSection("Pharmacy",
        [
            Nav("💊", "Pharmacy Hub",        () => _services.GetRequiredService<PharmacyHubViewModel>(), MenuArea.Pharmacy),
            Nav("📋", "Outpatient Rx",       () => _services.GetRequiredService<OutpatientPharmacyViewModel>(), MenuArea.Pharmacy),
            Nav("🏥", "Inpatient Meds",      () => _services.GetRequiredService<InpatientPharmacyViewModel>(), MenuArea.Pharmacy),
            Nav("📦", "Drug Accountability", () => _services.GetRequiredService<DrugAccountabilityViewModel>(), MenuArea.Pharmacy),
            Nav("💳", "Benefits & PA",       () => _services.GetRequiredService<PharmacyBenefitsViewModel>(), MenuArea.Pharmacy),
            Nav("📮", "CMOP",               () => _services.GetRequiredService<CmopViewModel>(), MenuArea.Pharmacy),
            Nav("📦", "Ward Stock",          () => _services.GetRequiredService<WardStockViewModel>(), MenuArea.Pharmacy),
            Nav("🔬", "DUR",                () => _services.GetRequiredService<DrugUtilizationReviewViewModel>(), MenuArea.Pharmacy),
            Nav("⚠️", "Interactions",        () => _services.GetRequiredService<InteractionBlockingViewModel>(), MenuArea.Pharmacy),
            Nav("⚗️", "Drug Interaction DB", () => _services.GetRequiredService<DrugInteractionDataViewModel>(), MenuArea.Pharmacy),
            Nav("💵", "Ambulatory Copay",    () => _services.GetRequiredService<AmbulatoryCopayViewModel>(), MenuArea.Financial),
            Nav("📦", "Lab Shipping",        () => _services.GetRequiredService<LabShippingViewModel>(), MenuArea.Laboratory),
        ]));

        NavSections.Add(new NavSection("Administrative",
        [
            Nav("🛏️", "ADT",                () => _services.GetRequiredService<AdtViewModel>(), MenuArea.Registration),
            Nav("🛏️", "Bed Management",     () => _services.GetRequiredService<BedManagementViewModel>(), MenuArea.Registration),
            Nav("🚑", "Emergency Dept",     () => _services.GetRequiredService<EmergencyDepartmentViewModel>(), MenuArea.Clinical),
            Nav("📋", "Means Test",         () => _services.GetRequiredService<MeansTestViewModel>(), MenuArea.Registration),
            Nav("🎖️", "SC Conditions",      () => _services.GetRequiredService<ServiceConnectedViewModel>(), MenuArea.Registration),
            Nav("🦿", "Prosthetics",        () => _services.GetRequiredService<ProstheticsViewModel>(), MenuArea.Registration),
            Nav("📜", "Audit Trail",        () => _services.GetRequiredService<AuditTrailViewModel>(), MenuArea.SystemAdmin),
            Nav("📋", "Registration",       () => _services.GetRequiredService<RegistrationViewModel>(), MenuArea.Registration),
            Nav("💵", "Accts Receivable",   () => _services.GetRequiredService<AccountsReceivableViewModel>(), MenuArea.Financial),
            Nav("💰", "Agent Cashier",      () => _services.GetRequiredService<AgentCashierViewModel>(), MenuArea.Financial),
            Nav("🏥", "Integrated Billing", () => _services.GetRequiredService<IntegratedBillingViewModel>(), MenuArea.Financial),
            Nav("📤", "EDI Billing",        () => _services.GetRequiredService<EdiBillingViewModel>(), MenuArea.Financial),
            Nav("🤝", "Fee Basis",          () => _services.GetRequiredService<FeeBasisViewModel>(), MenuArea.Financial),
            Nav("📦", "IFCAP",              () => _services.GetRequiredService<IfcapViewModel>(), MenuArea.Financial),
            Nav("🚗", "Travel Claims",      () => _services.GetRequiredService<BeneficiaryTravelViewModel>(), MenuArea.Financial),
            Nav("📊", "DRG Grouper",        () => _services.GetRequiredService<DrgGrouperViewModel>(), MenuArea.Financial),
            Nav("📁", "Incomplete Records", () => _services.GetRequiredService<IncompleteRecordsViewModel>(), MenuArea.SystemAdmin),
            Nav("🔒", "Security",           () => _services.GetRequiredService<SecurityViewModel>(), MenuArea.SystemAdmin),
            Nav("🔑", "Security Keys",      () => _services.GetRequiredService<SecurityKeyManagementViewModel>(), MenuArea.SystemAdmin),
            Nav("⚙️", "Site Parameters",    () => _services.GetRequiredService<SiteParametersViewModel>(), MenuArea.SystemAdmin),
            Nav("\U0001f500", "Patient Merge",     () => _services.GetRequiredService<PatientMergeViewModel>(), MenuArea.SystemAdmin),
            Nav("✉️", "Direct Messaging",  () => _services.GetRequiredService<DirectMessagingViewModel>(), MenuArea.SystemAdmin),
            Nav("🔒", "DS4P",              () => _services.GetRequiredService<DataSegmentationViewModel>(), MenuArea.SystemAdmin),
            Nav("📂", "Consult Services",  () => _services.GetRequiredService<ConsultServiceDirectoryViewModel>(), MenuArea.Registration),
            Nav("⚙️", "IB Site Config",    () => _services.GetRequiredService<IBSiteConfigViewModel>(), MenuArea.Financial),
            Nav("📋", "Enhanced Reg.",     () => _services.GetRequiredService<RegistrationEnhancedViewModel>(), MenuArea.Registration),
            Nav("💰", "AR Aging",          () => _services.GetRequiredService<ARAgingDashboardViewModel>(), MenuArea.Financial),
            Nav("📋", "Eligibility",       () => _services.GetRequiredService<EligibilityVerificationViewModel>(), MenuArea.Registration),
            Nav("🏷️", "Auto Eligibility",  () => _services.GetRequiredService<AutoEligibilityViewModel>(), MenuArea.Registration),
            Nav("📝", "Claim Status",      () => _services.GetRequiredService<ClaimStatusInquiryViewModel>(), MenuArea.Financial),
            Nav("📬", "Collection Letters", () => _services.GetRequiredService<CollectionLettersViewModel>(), MenuArea.Financial),
        ]));

        NavSections.Add(new NavSection("Reference",
        [
            Nav("📖", "ICD-10 Codes",   () => _services.GetRequiredService<Icd10BrowserViewModel>()),
            Nav("📚", "NDF Formulary",  () => _services.GetRequiredService<DrugFormularyViewModel>()),
            Nav("🗃️", "Drug File",      () => _services.GetRequiredService<DrugFileViewModel>()),
            Nav("📖", "Lexicon",        () => _services.GetRequiredService<LexiconViewModel>()),
        ]));

        NavSections.Add(new NavSection("Interoperability",
        [
            Nav("🔗", "FHIR Gateway",   () => _services.GetRequiredService<FhirGatewayViewModel>(), MenuArea.SystemAdmin),
            Nav("🧬", "Lab EDI",        () => _services.GetRequiredService<LabEdiViewModel>(), MenuArea.Laboratory),
            Nav("🔬", "Lab Instruments", () => _services.GetRequiredService<LabInstrumentsViewModel>(), MenuArea.Laboratory),
            Nav("👤", "MPI",             () => _services.GetRequiredService<MasterPatientIndexViewModel>(), MenuArea.SystemAdmin),
            Nav("📧", "MailMan",         () => _services.GetRequiredService<MailManViewModel>(), MenuArea.SystemAdmin),
        ]));

        NavSections.Add(new NavSection("Specialty Clinical",
        [
            Nav("🔬", "Anatomic Pathology",   () => _services.GetRequiredService<AnatomicPathologyViewModel>(), MenuArea.Laboratory),
            Nav("👁️", "Blind Rehab",          () => _services.GetRequiredService<BlindRehabilitationViewModel>(), MenuArea.Clinical),
            Nav("🩸", "Blood Bank",           () => _services.GetRequiredService<BloodBankViewModel>(), MenuArea.Laboratory),
            Nav("🧪", "Clinical Procedures",  () => _services.GetRequiredService<ClinicalProceduresViewModel>(), MenuArea.Clinical),
            Nav("🎖️", "Comp & Pension",        () => _services.GetRequiredService<CompensationPensionViewModel>(), MenuArea.Clinical),
            Nav("🦷", "Dental",               () => _services.GetRequiredService<DentalViewModel>(), MenuArea.Clinical),
            Nav("📋", "Event Capture",        () => _services.GetRequiredService<EventCaptureViewModel>(), MenuArea.Clinical),
            Nav("📊", "Health Summary",        () => _services.GetRequiredService<HealthSummaryViewModel>(), MenuArea.Clinical),
            Nav("📡", "Home Telehealth",       () => _services.GetRequiredService<HomeTelehealthViewModel>(), MenuArea.Clinical),
            Nav("💉", "IV Pharmacy",           () => _services.GetRequiredService<IVPharmacyViewModel>(), MenuArea.Pharmacy),
            Nav("🫀", "Medicine",              () => _services.GetRequiredService<MedicineViewModel>(), MenuArea.Clinical),
            Nav("🩺", "Nursing",              () => _services.GetRequiredService<NursingViewModel>(), MenuArea.Clinical),
            Nav("🏋️", "Physical Therapy",    () => { NavigateToPTHub(); return CurrentViewModel!; }, MenuArea.Clinical),
            Nav("🎗️", "Oncology",             () => _services.GetRequiredService<OncologyViewModel>(), MenuArea.Clinical),
            Nav("☢️", "Radiation Therapy",     () => _services.GetRequiredService<RadiationTherapyViewModel>(), MenuArea.Radiology),
            Nav("🤝", "Social Work",           () => _services.GetRequiredService<SocialWorkViewModel>(), MenuArea.Clinical),
            Nav("🦽", "Spinal Cord Injury",    () => _services.GetRequiredService<SpinalCordInjuryViewModel>(), MenuArea.Clinical),
            Nav("🤰", "Prenatal / OB",           () => _services.GetRequiredService<PrenatalViewModel>(), MenuArea.Clinical),
            Nav("🧪", "SA Treatment",           () => _services.GetRequiredService<SubstanceAbuseTreatmentViewModel>(), MenuArea.MentalHealth),
            Nav("💊", "Pharmacy POS",           () => _services.GetRequiredService<PharmacyPosViewModel>(), MenuArea.Pharmacy),
            Nav("📝", "EPCS",                   () => _services.GetRequiredService<EpcsViewModel>(), MenuArea.Pharmacy),
            Nav("♀️", "Women's Health",         () => _services.GetRequiredService<WomensHealthViewModel>(), MenuArea.Clinical),
            Nav("🔗", "Ext. Referrals",        () => _services.GetRequiredService<ExternalReferralViewModel>(), MenuArea.Clinical),
            Nav("📅", "Imm. Forecast",        () => _services.GetRequiredService<ImmunizationForecastViewModel>(), MenuArea.Clinical),
            Nav("🦷", "Periodontal Chart",  () => _services.GetRequiredService<PeriodontalChartViewModel>(), MenuArea.Clinical),
            Nav("🔗", "TOP Matching",       () => _services.GetRequiredService<TopMatchingViewModel>(), MenuArea.Registration),
            Nav("📋", "Patient Recall",     () => _services.GetRequiredService<PatientRecallViewModel>(), MenuArea.Registration),
        ]));

        NavSections.Add(new NavSection("System Modules",
        [
            Nav("📋", "Case Registries",       () => _services.GetRequiredService<ClinicalCaseRegistriesViewModel>(), MenuArea.Clinical),
            Nav("📊", "GPRA Reporting",       () => _services.GetRequiredService<GpraReportingViewModel>(), MenuArea.SystemAdmin),
            Nav("🔬", "PCC Surveillance",    () => _services.GetRequiredService<PccSurveillanceViewModel>(), MenuArea.SystemAdmin),
            Nav("💊", "Controlled Substances", () => _services.GetRequiredService<ControlledSubstancesViewModel>(), MenuArea.Pharmacy),
            Nav("🔧", "Engineering",           () => _services.GetRequiredService<EngineeringViewModel>(), MenuArea.SystemAdmin),
            Nav("👴", "Geriatrics / EC",        () => _services.GetRequiredService<GeriatricsExtendedCareViewModel>(), MenuArea.Clinical),
            Nav("🏠", "Home Health (HBPC)",    () => _services.GetRequiredService<HomeHealthViewModel>(), MenuArea.Clinical),
            Nav("🦠", "Infection Control",     () => _services.GetRequiredService<InfectionControlViewModel>(), MenuArea.Clinical),
            Nav("🤝", "Patient Advocate",      () => _services.GetRequiredService<PatientAdvocateViewModel>(), MenuArea.Registration),
            Nav("🪖", "Polytrauma / TBI",      () => _services.GetRequiredService<PolytraumaTBIViewModel>(), MenuArea.Clinical),
            Nav("📊", "Quality Mgmt",          () => _services.GetRequiredService<QualityManagementViewModel>(), MenuArea.SystemAdmin),
            Nav("📁", "Record Tracking",       () => _services.GetRequiredService<RecordTrackingViewModel>(), MenuArea.Registration),
            Nav("📤", "Release of Info",        () => _services.GetRequiredService<ReleaseOfInformationViewModel>(), MenuArea.Registration),
            Nav("🔬", "Research / IRB",        () => _services.GetRequiredService<ResearchIRBViewModel>(), MenuArea.SystemAdmin),
            Nav("🆘", "Suicide Prevention",    () => _services.GetRequiredService<SuicidePreventionViewModel>(), MenuArea.MentalHealth),
            Nav("🫀", "Transplant",            () => _services.GetRequiredService<TransplantViewModel>(), MenuArea.Surgery),
            Nav("🤲", "Voluntary Service",     () => _services.GetRequiredService<VoluntaryServiceViewModel>(), MenuArea.Registration),
            Nav("\U0001f4ca", "iCare Dashboard",     () => _services.GetRequiredService<iCareDashboardViewModel>(), MenuArea.Clinical),
            Nav("🎗️", "Cancer Registry",      () => _services.GetRequiredService<CancerRegistryViewModel>(), MenuArea.Clinical),
            Nav("📈", "CQM",                  () => _services.GetRequiredService<ClinicalQualityMeasuresViewModel>(), MenuArea.SystemAdmin),
            Nav("🧩", "Decision Support",      () => _services.GetRequiredService<DecisionSupportViewModel>(), MenuArea.Clinical),
            Nav("📡", "eCR",                  () => _services.GetRequiredService<ElectronicCaseReportingViewModel>(), MenuArea.Clinical),
            Nav("🚨", "Mass Casualty",        () => _services.GetRequiredService<MassCasualtyViewModel>(), MenuArea.SystemAdmin),
        ]));

        NavSections.Add(new NavSection("Patient",
        [
            Nav("🌐", "Patient Portal",  () => _services.GetRequiredService<PatientPortalViewModel>(), MenuArea.SystemAdmin),
        ]));

        NavSections.Add(new NavSection("Tools",
        [
            Nav("📥", "ZWR Import", () => _services.GetRequiredService<ZwrImportViewModel>(), MenuArea.SystemAdmin),
        ]));
    }

    /// <summary>
    /// Remove nav items the user lacks security keys for, and remove empty sections.
    /// Called once after BuildNavSections — all checks are O(1) HashSet lookups.
    /// </summary>
    private void FilterNavSections()
    {
        for (int i = NavSections.Count - 1; i >= 0; i--)
        {
            NavSections[i].Items.RemoveAll(item => !AuthService.HasMenuAccess(item.Area));
            if (NavSections[i].Items.Count == 0 && !string.IsNullOrEmpty(NavSections[i].Title))
                NavSections.RemoveAt(i);
        }
    }

    private static NavItem Nav(string icon, string label, Func<object> factory, MenuArea area = MenuArea.General)
        => new(icon, label, factory, area);

    [RelayCommand]
    public void Navigate(NavItem item)
    {
        SelectedNavItem = item;
        CurrentViewModel = (ObservableObject)item.CreateViewModel();
    }

    /// <summary>
    /// Creates a PTHubViewModel, wires up all PT sub-navigation events
    /// (body group selection, wizard, goals, home exercises), and sets it as CurrentViewModel.
    /// </summary>
    private void NavigateToPTHub()
    {
        var hub = _services.GetRequiredService<PTHubViewModel>();

        hub.BodyGroupSelected += (BodyGroup bg) =>
        {
            var session = _services.GetRequiredService<PTSessionViewModel>();
            session.SetBodyGroup(bg);
            session.BackRequested += () => NavigateToPTHub();
            CurrentViewModel = session;
        };

        hub.WizardRequested += () =>
        {
            var wizard = _services.GetRequiredService<PTMeasurementWizardViewModel>();
            wizard.BackToHubRequested += () => NavigateToPTHub();
            CurrentViewModel = wizard;
        };

        hub.GoalsRequested += () =>
        {
            var goals = _services.GetRequiredService<PTGoalsViewModel>();
            CurrentViewModel = goals;
        };

        hub.HomeExercisesRequested += () =>
        {
            var hep = _services.GetRequiredService<PTHomeExercisesViewModel>();
            CurrentViewModel = hep;
        };

        CurrentViewModel = hub;
    }
}
