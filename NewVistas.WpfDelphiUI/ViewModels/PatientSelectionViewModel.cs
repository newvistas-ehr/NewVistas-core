// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

/// <summary>
/// ViewModel for the patient selection dialog (frmPtSel in Delphi CPRS).
/// Handles patient list type selection, search combo, demographics panel,
/// and the pending/processed notifications tab.
/// </summary>
public partial class PatientSelectionViewModel : ObservableObject
{
    private readonly ApiClient      _api;
    private readonly PatientContext _context;

    // ── Patient list type (radio buttons in fraPtSelOptns) ────────────────

    [ObservableProperty]
    private PatientListType _listType = PatientListType.MyPatients;

    // ── Search ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public ObservableCollection<PatientSearchResultDto> SearchResults { get; } = new();

    [ObservableProperty]
    private PatientSearchResultDto? _selectedPatient;

    // ── Demographics panel (fraPtSelDemog) ────────────────────────────────

    [ObservableProperty]
    private string _demoName = string.Empty;

    [ObservableProperty]
    private string _demoSsn = string.Empty;

    [ObservableProperty]
    private string _demoDob = string.Empty;

    [ObservableProperty]
    private string _demoSex = string.Empty;

    [ObservableProperty]
    private string _demoAge = string.Empty;

    [ObservableProperty]
    private string _demoVeteranStatus = string.Empty;

    [ObservableProperty]
    private string _demoScPercent = string.Empty;

    [ObservableProperty]
    private string _demoWard = string.Empty;

    [ObservableProperty]
    private string _demoRoomBed = string.Empty;

    [ObservableProperty]
    private string _demoPrimaryProvider = string.Empty;

    [ObservableProperty]
    private string _demoAttending = string.Empty;

    [ObservableProperty]
    private string _demoLastVisit = string.Empty;

    // ── Pending notifications (lstvAlerts) ────────────────────────────────

    public ObservableCollection<AlertItem> PendingAlerts  { get; } = new();
    public ObservableCollection<AlertItem> ProcessedAlerts{ get; } = new();

    // ── Error / status ────────────────────────────────────────────────────

    [ObservableProperty]
    private string _statusText = string.Empty;

    // ── Dialog result ─────────────────────────────────────────────────────

    public bool Confirmed { get; private set; }

    public PatientSelectionViewModel(ApiClient api, PatientContext context)
    {
        _api     = api;
        _context = context;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        string q = SearchText.Trim();
        if (q.Length < 2)
        {
            StatusText = "Enter at least 2 characters to search.";
            return;
        }

        IsSearching = true;
        StatusText  = "Searching...";
        SearchResults.Clear();
        SelectedPatient = null;
        ClearDemo();

        try
        {
            var results = await _api.SearchPatientsAsync(q);
            foreach (var r in results)
                SearchResults.Add(r);

            StatusText = results.Count == 0
                ? "No patients found."
                : $"{results.Count} patient(s) found.";
        }
        catch (Exception ex)
        {
            StatusText = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    partial void OnSelectedPatientChanged(PatientSearchResultDto? value)
    {
        if (value is null) { ClearDemo(); return; }
        LoadDemographics(value);
    }

    private void LoadDemographics(PatientSearchResultDto r)
    {
        DemoName            = r.Name;
        DemoSsn             = r.Ssn;
        DemoDob             = r.Dob;
        DemoSex             = string.Empty;
        DemoAge             = string.Empty;
        DemoVeteranStatus   = string.Empty;
        DemoScPercent       = string.Empty;
        DemoWard            = string.Empty;
        DemoRoomBed         = string.Empty;
        DemoPrimaryProvider = string.Empty;
        DemoAttending       = string.Empty;
        DemoLastVisit       = string.Empty;
    }

    private void ClearDemo()
    {
        DemoName = DemoSsn = DemoDob = DemoSex = DemoAge =
        DemoVeteranStatus = DemoScPercent = DemoWard = DemoRoomBed =
        DemoPrimaryProvider = DemoAttending = DemoLastVisit = string.Empty;
    }

    [RelayCommand]
    public void Confirm(System.Windows.Window window)
    {
        if (SelectedPatient is null)
        {
            StatusText = "Please select a patient first.";
            return;
        }

        _context.PatientId   = SelectedPatient.PatientId;
        _context.PatientName = SelectedPatient.Name;
        _context.PatientSsn  = SelectedPatient.Ssn;
        _context.PatientDob  = SelectedPatient.Dob;

        Confirmed       = true;
        window.DialogResult = true;
        window.Close();
    }

    [RelayCommand]
    public void Cancel(System.Windows.Window window)
    {
        window.DialogResult = false;
        window.Close();
    }
}

// ── Support types ─────────────────────────────────────────────────────────────

public enum PatientListType
{
    MyPatients,
    MyProviders,
    MyClinics,
    MyTeams,
    MyWards,
    MySpecialties,
    PcmmTeams,
    RecentPatients,
    AllPatients
}

public record AlertItem(
    string Info,
    string Patient,
    string Location,
    string Urgency,
    string AlertDate,
    string Message,
    string OrderingProvider);
