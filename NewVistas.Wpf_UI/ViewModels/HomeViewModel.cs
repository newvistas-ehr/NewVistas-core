// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly PatientContext _patientContext;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<string> _loadResults = new();

    public string WelcomeMessage => "Welcome to NewVistas Clinical Information System";
    public string Description => "Select a patient ID in the header above, then navigate to any clinical module.";

    public HomeViewModel(ApiClient api, PatientContext patientContext)
    {
        _api = api;
        _patientContext = patientContext;
    }

    private string PatientId => _patientContext.PatientId?.Trim() ?? string.Empty;
    private bool HasPatient => !string.IsNullOrWhiteSpace(PatientId);

    [RelayCommand]
    private async Task LoadDemoData()
    {
        if (!HasPatient) { Error = "Please select a patient ID first."; return; }
        IsLoading = true; Error = null;
        LoadResults.Clear();

        var endpoints = new (string Label, string Url)[]
        {
            ("Scheduling (clinics + appointments)", $"api/scheduling/demo/load?patientId={Esc(PatientId)}"),
            ("Lab Results", $"api/lab/demo/load?patientId={Esc(PatientId)}"),
            ("Outpatient Pharmacy", $"api/outpatientpharmacy/demo/load?patientId={Esc(PatientId)}"),
            ("Inpatient Pharmacy", $"api/inpatientpharmacy/demo/load?patientId={Esc(PatientId)}"),
            ("ADT Movements", $"api/adt/demo/load?patientId={Esc(PatientId)}"),
            ("Patient Encounters (PCE)", $"api/pce/demo/load?patientId={Esc(PatientId)}"),
            ("BCMA / MAR", $"api/bcma/{Esc(PatientId)}/demo/load"),
            ("Drug Formulary (NDF)", "api/drugformulary/demo/load"),
            ("Drug File", "api/drugfile/demo/load"),
            ("Drug Accountability", "api/drugaccountability/demo/load"),
            ("Pharmacy Benefits", $"api/pharmacybenefits/demo/load?patientId={Esc(PatientId)}"),
        };

        foreach (var (label, url) in endpoints)
        {
            try
            {
                var response = await _api.Http.PostAsync(url, null);
                LoadResults.Add(response.IsSuccessStatusCode
                    ? $"OK   {label}"
                    : $"WARN {label} — {response.StatusCode}");
            }
            catch (Exception ex)
            {
                LoadResults.Add($"FAIL {label} — {ex.Message}");
            }
        }

        IsLoading = false;
    }

    private static string Esc(string id) => Uri.EscapeDataString(id);
}
