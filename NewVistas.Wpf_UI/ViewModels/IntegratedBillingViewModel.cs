// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class IntegratedBillingViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private IBillingPatientState? _copayAccount;
    [ObservableProperty] private ObservableCollection<IBillingActionIndexEntry> _billingActions = new();
    [ObservableProperty] private ObservableCollection<PersonalPolicyIndexEntry> _policies = new();
    [ObservableProperty] private MeansTestBillingClockState? _billingClock;
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _loaded;

    // Copay exemption form
    [ObservableProperty] private bool _showExemptionForm;
    [ObservableProperty] private bool _isExempt;
    [ObservableProperty] private string _exemptionReason = string.Empty;

    // Billing action form
    [ObservableProperty] private bool _showActionForm;
    [ObservableProperty] private string _actionTypeCode = string.Empty;
    [ObservableProperty] private string _actionTypeDesc = string.Empty;
    [ObservableProperty] private decimal _chargeAmount;
    [ObservableProperty] private DateTime _serviceDate = DateTime.Today;
    [ObservableProperty] private string _enteredByUserId = string.Empty;
    [ObservableProperty] private string _enteredByUserName = string.Empty;

    public string[] ExemptionReasons { get; } =
    [
        string.Empty, "SERVICE CONNECTED", "INCOME EXEMPT", "FORMER POW",
        "UNEMPLOYABLE VETERAN", "CATASTROPHICALLY DISABLED", "PURPLE HEART CONFIRMED",
        "AGENT ORANGE RELATED", "COPAY CAP REACHED"
    ];

    public string[] ActionCategories { get; } = ["0-Pharmacy", "1-Inpatient", "2-Outpatient", "3-LongTermCare", "4-Administrative"];

    public IntegratedBillingViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task Load()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null; Loaded = false;
        try
        {
            string pid = PatientId.Trim();
            var patientGrain = _grains.GetGrain<IIBillingPatientGrain>($"IB-PATIENT:{pid}");
            CopayAccount = await patientGrain.GetAsync();

            var actionIndex = _grains.GetGrain<IIBillingActionIndexGrain>($"IB-ACTION-IDX:{pid}");
            List<IBillingActionIndexEntry> actions = await actionIndex.GetAllAsync();
            BillingActions.Clear();
            foreach (IBillingActionIndexEntry a in actions) BillingActions.Add(a);

            var policyIndex = _grains.GetGrain<IPersonalPolicyIndexGrain>($"POLICY-IDX:{pid}");
            List<PersonalPolicyIndexEntry> policiesList = await policyIndex.GetAllAsync();
            Policies.Clear();
            foreach (PersonalPolicyIndexEntry p in policiesList) Policies.Add(p);

            var clockGrain = _grains.GetGrain<IMeansTestBillingClockGrain>($"IB-CLOCK:{pid}");
            BillingClock = await clockGrain.GetAsync();

            Loaded = true;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleExemptionForm()
    {
        ShowExemptionForm = !ShowExemptionForm;
        if (ShowExemptionForm && CopayAccount != null)
        {
            IsExempt = CopayAccount.IsExemptFromCopay;
            ExemptionReason = CopayAccount.ExemptionReasonCode ?? string.Empty;
        }
    }

    [RelayCommand]
    private async Task SaveExemption()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null;
        try
        {
            string pid = PatientId.Trim();
            var patientGrain = _grains.GetGrain<IIBillingPatientGrain>($"IB-PATIENT:{pid}");
            await patientGrain.SetCopayExemptionAsync(
                IsExempt,
                ExemptionReason.Length > 0 ? ExemptionReason : null,
                DateTime.UtcNow,
                null);
            ShowExemptionForm = false;
            await Load();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleActionForm() => ShowActionForm = !ShowActionForm;
}
