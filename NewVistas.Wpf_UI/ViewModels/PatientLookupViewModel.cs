// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Patient Lookup ViewModel — three-tab patient selection: My Patients, Ward Census, Search.
/// Mirrors the VistA patient selection dialog (ORWPT SELECT) with provider-centric and ward-centric views.
/// </summary>
public partial class PatientLookupViewModel : ObservableObject
{
    protected readonly OrleansGrainService Grains;
    protected readonly PatientContext PatientContext;
    protected readonly AuthService Auth;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    // Tab selection: 0=My Patients, 1=Ward Census, 2=Search
    [ObservableProperty] private int _selectedTab;

    // My Patients tab
    [ObservableProperty] private ObservableCollection<PatientListItem> _myPatients = new();

    // Ward Census tab
    [ObservableProperty] private ObservableCollection<PatientListItem> _wardPatients = new();
    [ObservableProperty] private bool _hasWardAssignment;
    [ObservableProperty] private string _wardName = string.Empty;

    // Search tab
    [ObservableProperty] private ObservableCollection<PatientListItem> _searchResults = new();
    [ObservableProperty] private string _searchQuery = string.Empty;

    // Selected patient in any list
    [ObservableProperty] private PatientListItem? _selectedPatient;

    // Currently loaded patient detail (after selection)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatientAddress))]
    private PatientState? _patient;

    public string PatientAddress => Patient is null ? string.Empty
        : string.Join(", ", new[] { Patient.StreetAddress1, Patient.City, Patient.State, Patient.ZipCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    public PatientLookupViewModel(OrleansGrainService grains, PatientContext patientContext, AuthService authService)
    {
        Grains = grains;
        PatientContext = patientContext;
        Auth = authService;

        // Auto-load on construction: check for pending search action, load My Patients
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Check for pending search action from header search button
        string? pendingAction = PatientContext.TakePendingAction();
        if (pendingAction is not null && pendingAction.StartsWith("SEARCH:", StringComparison.Ordinal))
        {
            string query = pendingAction["SEARCH:".Length..];
            if (!string.IsNullOrWhiteSpace(query))
            {
                SearchQuery = query;
                SelectedTab = 2; // Switch to Search tab
                await SearchAsync();
                return;
            }
        }

        // Default: load My Patients + check ward assignment
        await LoadMyPatientsAsync();
        await CheckWardAssignmentAsync();
    }

    /// <summary>
    /// Load the current user's patient list from the provider patient index grain.
    /// </summary>
    [RelayCommand]
    private async Task LoadMyPatientsAsync()
    {
        string? userId = Auth.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return;

        IsLoading = true;
        Error = null;
        try
        {
            var grain = Grains.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{userId}");
            List<ProviderPatientEntry> patients = await grain.GetActivePatientsAsync();
            MyPatients.Clear();
            foreach (ProviderPatientEntry p in patients)
            {
                MyPatients.Add(new PatientListItem
                {
                    PatientId = p.PatientId,
                    PatientName = p.PatientName,
                    Detail1 = p.DateOfBirth?.ToString("d") ?? "—",
                    Detail2 = string.IsNullOrEmpty(p.SsnLast4) ? "—" : $"***-**-{p.SsnLast4}",
                    Detail3 = p.Relationship
                });
            }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// Check whether the user has a default ward assignment and load the ward name.
    /// </summary>
    private async Task CheckWardAssignmentAsync()
    {
        string? userId = Auth.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            var personGrain = Grains.GetGrain<INewPersonGrain>($"USER:{userId}");
            NewPersonState person = await personGrain.GetPersonAsync();
            if (!string.IsNullOrWhiteSpace(person.DefaultWardId))
            {
                HasWardAssignment = true;
                WardName = string.IsNullOrWhiteSpace(person.DefaultWardName)
                    ? person.DefaultWardId
                    : person.DefaultWardName;
            }
            else
            {
                HasWardAssignment = false;
                WardName = string.Empty;
            }
        }
        catch
        {
            // Ward assignment check is non-critical
            HasWardAssignment = false;
        }
    }

    /// <summary>
    /// Load the ward census for the user's default ward.
    /// </summary>
    [RelayCommand]
    private async Task LoadWardCensusAsync()
    {
        string? userId = Auth.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return;

        IsLoading = true;
        Error = null;
        try
        {
            var personGrain = Grains.GetGrain<INewPersonGrain>($"USER:{userId}");
            NewPersonState person = await personGrain.GetPersonAsync();
            if (string.IsNullOrWhiteSpace(person.DefaultWardId))
            {
                Error = "No ward assignment found. Contact your supervisor to set your default ward.";
                return;
            }

            // DefaultWardId is reinterpreted as a unit id at institution 500
            // (legacy values may carry a "WARD-" prefix — strip it).
            string unitId = ToUnitId(person.DefaultWardId);
            var unitGrain = Grains.GetGrain<IInpatientUnitGrain>($"UNIT:500:{unitId}");
            List<UnitCensusEntry> census = await unitGrain.GetCensusAsync();
            WardPatients.Clear();
            foreach (UnitCensusEntry c in census)
            {
                WardPatients.Add(new PatientListItem
                {
                    PatientId = c.PatientId,
                    PatientName = string.IsNullOrEmpty(c.PatientName) ? c.PatientId : c.PatientName,
                    Detail1 = c.BedId ?? "(boarder)",
                    Detail2 = c.TreatingSpecialty ?? "—",
                    Detail3 = c.AttendingPhysicianName ?? "—"
                });
            }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// Interpret a legacy DefaultWardId as an inpatient unit id
    /// (e.g. "WARD-MED-3A" → "MED-3A").
    /// </summary>
    private static string ToUnitId(string wardId)
    {
        string id = wardId.Trim();
        return id.StartsWith("WARD-", StringComparison.OrdinalIgnoreCase) ? id[5..] : id;
    }

    /// <summary>
    /// Search for patients system-wide by name, SSN last 4, or patient ID.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsLoading = true;
        Error = null;
        try
        {
            // StatelessWorker search reader — scales out instead of funneling
            // through the singleton PATIENT-INDEX via a "SEARCH" workflow grain.
            var grain = Grains.GetGrain<IPatientSearchGrain>("PATIENT-SEARCH");
            List<PatientIndexEntry> results = await grain.SearchAsync(SearchQuery.Trim());
            SearchResults.Clear();
            foreach (PatientIndexEntry r in results)
            {
                SearchResults.Add(new PatientListItem
                {
                    PatientId = r.PatientId,
                    PatientName = r.Name,
                    Detail1 = r.DateOfBirth?.ToString("d") ?? "—",
                    Detail2 = string.IsNullOrEmpty(r.SsnLast4) ? "—" : $"***-**-{r.SsnLast4}",
                    Detail3 = r.Sex
                });
            }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    /// <summary>
    /// Select a patient from any list — sets the shared PatientContext.PatientId
    /// and loads the patient detail panel.
    /// </summary>
    [RelayCommand]
    private async Task SelectPatientAsync(PatientListItem? item)
    {
        if (item is null) return;

        PatientContext.PatientId = item.PatientId;
        IsLoading = true;
        Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(item.PatientId);
            Patient = await workflow.GetPatientAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}

/// <summary>
/// Unified display item for patient lists across all three tabs.
/// </summary>
public class PatientListItem
{
    public string PatientId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;

    /// <summary>DOB (My Patients, Search) or Room-Bed (Ward Census).</summary>
    public string? Detail1 { get; set; }

    /// <summary>SSN last 4 (My Patients, Search) or Treating Specialty (Ward Census).</summary>
    public string? Detail2 { get; set; }

    /// <summary>Relationship (My Patients), Sex (Search), or Attending (Ward Census).</summary>
    public string? Detail3 { get; set; }
}
