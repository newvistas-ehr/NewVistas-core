// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class RegistrationViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private PatientEnrollmentState? _enrollment;
    [ObservableProperty] private ObservableCollection<PrfFlagAssignment> _prfFlags = new();
    [ObservableProperty] private MstHistoryState? _mstHistory;
    [ObservableProperty] private ObservableCollection<PatientRelation> _relations = new();
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _loaded;

    // Enrollment form
    [ObservableProperty] private bool _showEnrollmentForm;
    [ObservableProperty] private string _newEnrollmentStatus = "Verified";
    [ObservableProperty] private string _changedByUserId = string.Empty;

    // PRF flag form
    [ObservableProperty] private bool _showFlagForm;
    [ObservableProperty] private string _flagName = string.Empty;
    [ObservableProperty] private string _assignedByUserId = string.Empty;
    [ObservableProperty] private string _assignedByUserName = string.Empty;
    [ObservableProperty] private string _flagNarrative = string.Empty;

    // MST screening form
    [ObservableProperty] private bool _showMstForm;
    [ObservableProperty] private string _mstStatus = "Unasked";
    [ObservableProperty] private string _screenedByUserId = string.Empty;
    [ObservableProperty] private string _screenedByUserName = string.Empty;

    public string[] EnrollmentStatuses { get; } =
    [
        "Verified", "Unverified", "Inactive", "Rejected", "Cancelled",
        "PendingEligibility", "PendingMeansTest", "PendingMST", "Suspended"
    ];

    public string[] MstStatuses { get; } = ["Unasked", "Verified", "Denied", "NoResponse"];

    public string[] NationalFlagNames { get; } =
    [
        "BEHAVIORAL", "HIGH RISK FOR SUICIDE", "URGENT ADDRESS AS FEMALE", "MISSING PATIENT"
    ];

    public RegistrationViewModel(ApiClient api, OrleansGrainService grains)
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

            var enrollGrain = _grains.GetGrain<IPatientEnrollmentGrain>($"ENROLLMENT:{pid}");
            Enrollment = await enrollGrain.GetAsync();

            var prfGrain = _grains.GetGrain<IPrfAssignmentGrain>($"PRF:{pid}");
            PrfAssignmentState prfState = await prfGrain.GetAsync();
            PrfFlags.Clear();
            foreach (PrfFlagAssignment f in prfState.Assignments.Where(a => a.IsActive)) PrfFlags.Add(f);

            var mstGrain = _grains.GetGrain<IMstHistoryGrain>($"MST:{pid}");
            MstHistory = await mstGrain.GetAsync();

            var relGrain = _grains.GetGrain<IPatientRelationGrain>($"RELATION:{pid}");
            PatientRelationState relState = await relGrain.GetAsync();
            Relations.Clear();
            foreach (PatientRelation r in relState.Relations) Relations.Add(r);

            Loaded = true;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleEnrollmentForm() => ShowEnrollmentForm = !ShowEnrollmentForm;

    [RelayCommand]
    private async Task UpdateEnrollmentStatus()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null;
        try
        {
            string pid = PatientId.Trim();
            var enrollGrain = _grains.GetGrain<IPatientEnrollmentGrain>($"ENROLLMENT:{pid}");
            var status = Enum.Parse<EnrollmentStatus>(NewEnrollmentStatus);
            await enrollGrain.UpdateStatusAsync(
                status,
                ChangedByUserId.Length > 0 ? ChangedByUserId : "USER",
                null);
            ShowEnrollmentForm = false;
            await Load();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleFlagForm() => ShowFlagForm = !ShowFlagForm;

    [RelayCommand]
    private async Task AssignFlag()
    {
        if (string.IsNullOrWhiteSpace(PatientId) || string.IsNullOrWhiteSpace(FlagName)) return;
        IsLoading = true; Error = null;
        try
        {
            string pid = PatientId.Trim();
            var prfGrain = _grains.GetGrain<IPrfAssignmentGrain>($"PRF:{pid}");
            await prfGrain.AssignFlagAsync(
                FlagName, FlagName, "NATIONAL", true,
                AssignedByUserId, AssignedByUserName,
                FlagNarrative.Length > 0 ? FlagNarrative : null);
            ShowFlagForm = false;
            FlagName = AssignedByUserId = AssignedByUserName = FlagNarrative = string.Empty;
            await Load();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleMstForm() => ShowMstForm = !ShowMstForm;

    [RelayCommand]
    private async Task RecordMstScreening()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null;
        try
        {
            string pid = PatientId.Trim();
            var mstGrain = _grains.GetGrain<IMstHistoryGrain>($"MST:{pid}");
            var mstStatusEnum = Enum.Parse<MstStatus>(MstStatus);
            await mstGrain.RecordScreeningAsync(
                DateTime.Today, mstStatusEnum, ScreenedByUserId, ScreenedByUserName,
                null, null);
            ShowMstForm = false;
            ScreenedByUserId = ScreenedByUserName = string.Empty;
            await Load();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
