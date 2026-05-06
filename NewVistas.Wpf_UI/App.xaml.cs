// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewVistas.Wpf_UI.Services;
using NewVistas.Wpf_UI.ViewModels;
using Orleans;

namespace NewVistas.Wpf_UI;

/// <summary>
/// Interaction logic for App.xaml.
/// Shows a LoginWindow first; on successful authentication, switches to MainWindow.
/// On logout (user-initiated or forced via 401/token expiry), switches back to LoginWindow.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private LoginWindow? _loginWindow;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .UseOrleansClient((context, clientBuilder) =>
            {
                // Development: connect to localhost silo (gateway port 30000)
                clientBuilder.UseLocalhostClustering();

                // Production would use:
                // clientBuilder.UseAdoNetClustering(options => { ... });
            })
            .ConfigureServices(services =>
            {
                // Shared services
                services.AddSingleton<PatientContext>();
                services.AddSingleton<AuthService>();

                // Orleans grain service — sets RequestContext (DUZ equivalent) from JWT
                // before each grain call, so the silo's AuthorizationCallFilter sees the caller.
                services.AddSingleton<OrleansGrainService>();

                // HttpClient for authentication only (login, MFA, logout, session touch)
                // Use a DelegatingHandler to catch 401 responses and trigger forced logout
                services.AddTransient<UnauthorizedHandler>();
                services.AddHttpClient<ApiClient>(c =>
                {
                    c.BaseAddress = new Uri("https://localhost:7127/");
                    c.Timeout = TimeSpan.FromSeconds(60);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                })
                .AddHttpMessageHandler<UnauthorizedHandler>();

                // Shell ViewModel (transient — fresh instance per login session)
                services.AddTransient<MainViewModel>();

                // Login ViewModel (transient — fresh instance each time)
                services.AddTransient<LoginViewModel>();

                // All page ViewModels as Transient so each navigation creates a fresh instance
                services.AddTransient<HomeViewModel>();
                services.AddTransient<PatientLookupViewModel>();
                services.AddTransient<PatientEditViewModel>();
                services.AddTransient<CoverSheetViewModel>();
                services.AddTransient<ProblemsViewModel>();
                services.AddTransient<MedicationsViewModel>();
                services.AddTransient<OrdersViewModel>();
                services.AddTransient<NotesViewModel>();
                services.AddTransient<ConsultsViewModel>();
                services.AddTransient<LabsViewModel>();
                services.AddTransient<VitalsViewModel>();
                services.AddTransient<AllergiesViewModel>();
                services.AddTransient<ImmunizationsViewModel>();
                services.AddTransient<SurgeryViewModel>();
                services.AddTransient<RadiologyViewModel>();
                services.AddTransient<ImagingViewModel>();
                services.AddTransient<BcmaViewModel>();
                services.AddTransient<MentalHealthViewModel>();
                services.AddTransient<DieteticsViewModel>();
                services.AddTransient<RemindersViewModel>();
                services.AddTransient<HealthFactorsViewModel>();
                services.AddTransient<SchedulingViewModel>();
                services.AddTransient<PceViewModel>();
                services.AddTransient<PharmacyHubViewModel>();
                services.AddTransient<OutpatientPharmacyViewModel>();
                services.AddTransient<InpatientPharmacyViewModel>();
                services.AddTransient<DrugAccountabilityViewModel>();
                services.AddTransient<PharmacyBenefitsViewModel>();
                services.AddTransient<AdtViewModel>();
                services.AddTransient<MeansTestViewModel>();
                services.AddTransient<ServiceConnectedViewModel>();
                services.AddTransient<ProstheticsViewModel>();
                services.AddTransient<AuditTrailViewModel>();
                services.AddTransient<Icd10BrowserViewModel>();
                services.AddTransient<DrugFormularyViewModel>();
                services.AddTransient<DrugFileViewModel>();
                services.AddTransient<AccountsReceivableViewModel>();
                services.AddTransient<AgentCashierViewModel>();
                services.AddTransient<EdiBillingViewModel>();
                services.AddTransient<FeeBasisViewModel>();
                services.AddTransient<IfcapViewModel>();
                services.AddTransient<IntegratedBillingViewModel>();
                services.AddTransient<RegistrationViewModel>();

                // Specialty Clinical
                services.AddTransient<AnatomicPathologyViewModel>();
                services.AddTransient<BlindRehabilitationViewModel>();
                services.AddTransient<BloodBankViewModel>();
                services.AddTransient<ClinicalProceduresViewModel>();
                services.AddTransient<CompensationPensionViewModel>();
                services.AddTransient<DentalViewModel>();
                services.AddTransient<EventCaptureViewModel>();
                services.AddTransient<HealthSummaryViewModel>();
                services.AddTransient<HomeTelehealthViewModel>();
                services.AddTransient<IVPharmacyViewModel>();
                services.AddTransient<MedicineViewModel>();
                services.AddTransient<NursingViewModel>();
                services.AddTransient<PTHubViewModel>();
                services.AddTransient<PTSessionViewModel>();
                services.AddTransient<PTGoalsViewModel>();
                services.AddTransient<PTHomeExercisesViewModel>();
                services.AddTransient<PTMeasurementWizardViewModel>();
                services.AddTransient<OncologyViewModel>();
                services.AddTransient<RadiationTherapyViewModel>();
                services.AddTransient<SocialWorkViewModel>();
                services.AddTransient<SpinalCordInjuryViewModel>();
                services.AddTransient<WomensHealthViewModel>();
                services.AddTransient<PrenatalViewModel>();
                services.AddTransient<SubstanceAbuseTreatmentViewModel>();
                services.AddTransient<PharmacyPosViewModel>();
                services.AddTransient<EpcsViewModel>();
                services.AddTransient<GpraReportingViewModel>();
                services.AddTransient<PccSurveillanceViewModel>();
                services.AddTransient<ExternalReferralViewModel>();
                services.AddTransient<ImmunizationForecastViewModel>();

                // System-Level Modules
                services.AddTransient<ClinicalCaseRegistriesViewModel>();
                services.AddTransient<ControlledSubstancesViewModel>();
                services.AddTransient<EngineeringViewModel>();
                services.AddTransient<GeriatricsExtendedCareViewModel>();
                services.AddTransient<HomeHealthViewModel>();
                services.AddTransient<InfectionControlViewModel>();
                services.AddTransient<PatientAdvocateViewModel>();
                services.AddTransient<PolytraumaTBIViewModel>();
                services.AddTransient<QualityManagementViewModel>();
                services.AddTransient<RecordTrackingViewModel>();
                services.AddTransient<ReleaseOfInformationViewModel>();
                services.AddTransient<ResearchIRBViewModel>();
                services.AddTransient<SuicidePreventionViewModel>();
                services.AddTransient<TransplantViewModel>();
                services.AddTransient<VoluntaryServiceViewModel>();
                services.AddTransient<iCareDashboardViewModel>();

                // Interoperability & Infrastructure
                services.AddTransient<FhirGatewayViewModel>();
                services.AddTransient<LabEdiViewModel>();
                services.AddTransient<LexiconViewModel>();
                services.AddTransient<MasterPatientIndexViewModel>();
                services.AddTransient<MailManViewModel>();
                services.AddTransient<SecurityViewModel>();
                services.AddTransient<BedManagementViewModel>();
                services.AddTransient<BeneficiaryTravelViewModel>();
                services.AddTransient<CmopViewModel>();
                services.AddTransient<DrgGrouperViewModel>();
                services.AddTransient<EmergencyDepartmentViewModel>();
                services.AddTransient<IncompleteRecordsViewModel>();
                services.AddTransient<LabInstrumentsViewModel>();
                services.AddTransient<WardStockViewModel>();

                // New parity pages
                services.AddTransient<SecurityKeyManagementViewModel>();
                services.AddTransient<SiteParametersViewModel>();
                services.AddTransient<PatientPortalViewModel>();
                services.AddTransient<PatientMergeViewModel>();

                // Parity pages (2026-03-29)
                services.AddTransient<DrugUtilizationReviewViewModel>();
                services.AddTransient<InteractionBlockingViewModel>();
                services.AddTransient<DrugInteractionDataViewModel>();
                services.AddTransient<AmbulatoryCopayViewModel>();
                services.AddTransient<IBSiteConfigViewModel>();
                services.AddTransient<LabShippingViewModel>();
                services.AddTransient<ConsultServiceDirectoryViewModel>();
                services.AddTransient<CareTeamViewModel>();
                services.AddTransient<NursingCarePlanViewModel>();
                services.AddTransient<NursingTaskWorklistViewModel>();
                services.AddTransient<NursingTriageViewModel>();
                services.AddTransient<PainAssessmentViewModel>();
                services.AddTransient<ShiftHandoffViewModel>();
                services.AddTransient<LabTechViewModel>();
                services.AddTransient<RadTechViewModel>();
                services.AddTransient<ProviderDashboardViewModel>();
                services.AddTransient<TopMatchingViewModel>();
                services.AddTransient<CancerRegistryViewModel>();
                services.AddTransient<ClinicalQualityMeasuresViewModel>();
                services.AddTransient<DirectMessagingViewModel>();
                services.AddTransient<DataSegmentationViewModel>();
                services.AddTransient<DecisionSupportViewModel>();
                services.AddTransient<ElectronicCaseReportingViewModel>();
                services.AddTransient<AutoEligibilityViewModel>();
                services.AddTransient<ClaimStatusInquiryViewModel>();
                services.AddTransient<CollectionLettersViewModel>();
                services.AddTransient<EligibilityVerificationViewModel>();
                services.AddTransient<RegistrationEnhancedViewModel>();
                services.AddTransient<ARAgingDashboardViewModel>();
                services.AddTransient<MassCasualtyViewModel>();
                services.AddTransient<PatientRecallViewModel>();
                services.AddTransient<PeriodontalChartViewModel>();

                // Tools
                services.AddTransient<ZwrImportViewModel>();
            })
            .Build();

        await _host.StartAsync();

        // Subscribe to forced logout events (401 responses, token expiry)
        var authService = _host.Services.GetRequiredService<AuthService>();
        authService.LoggedOut += OnForcedLogout;

        // Wire the 401 handler to the auth service
        var handler = _host.Services.GetRequiredService<UnauthorizedHandler>();
        handler.AuthService = authService;

        ShowLoginWindow();
    }

    private void ShowLoginWindow()
    {
        var loginVm = _host!.Services.GetRequiredService<LoginViewModel>();
        loginVm.Reset();
        loginVm.LoginSucceeded += OnLoginSucceeded;

        _loginWindow = new LoginWindow { DataContext = loginVm };
        _loginWindow.Show();
        MainWindow = _loginWindow;
    }

    private void OnLoginSucceeded()
    {
        // Unsubscribe to prevent double-fire
        if (_loginWindow?.DataContext is LoginViewModel loginVm)
            loginVm.LoginSucceeded -= OnLoginSucceeded;

        _loginWindow?.Hide();

        // Create a fresh MainViewModel each login so nav state is clean
        var mainVm = _host!.Services.GetRequiredService<MainViewModel>();

        _mainWindow = new MainWindow { DataContext = mainVm };
        _mainWindow.Closed += MainWindow_Closed;
        _mainWindow.Show();
        MainWindow = _mainWindow;
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        // User closed the main window — log out and show login again
        if (_mainWindow != null)
            _mainWindow.Closed -= MainWindow_Closed;

        var authService = _host!.Services.GetRequiredService<AuthService>();
        if (authService.IsAuthenticated)
            await authService.LogoutAsync();

        // If the app is shutting down, don't show login again
        if (Current?.Windows.Count == 0 && _loginWindow == null)
            return;

        ShowLoginWindow();
    }

    private void OnForcedLogout()
    {
        // Forced logout (401 or token expiry) — must dispatch to UI thread
        Dispatcher.Invoke(() =>
        {
            if (_mainWindow != null)
            {
                _mainWindow.Closed -= MainWindow_Closed;
                _mainWindow.Close();
                _mainWindow = null;
            }

            ShowLoginWindow();
        });
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            var authService = _host.Services.GetRequiredService<AuthService>();
            authService.LoggedOut -= OnForcedLogout;
            authService.Dispose();

            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}

/// <summary>
/// DelegatingHandler that intercepts 401 Unauthorized responses from the API
/// and triggers a forced logout back to the login screen.
/// </summary>
internal sealed class UnauthorizedHandler : DelegatingHandler
{
    /// <summary>
    /// Set after DI construction by App.xaml.cs.
    /// </summary>
    public AuthService? AuthService { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && AuthService?.IsAuthenticated == true)
        {
            // Don't trigger logout for login/MFA requests
            string? path = request.RequestUri?.AbsolutePath;
            if (path != null && !path.Contains("/api/auth/login") && !path.Contains("/api/auth/mfa/"))
            {
                Application.Current?.Dispatcher.Invoke(() => AuthService.ForceLogout());
            }
        }

        return response;
    }
}
