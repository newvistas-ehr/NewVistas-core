// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly PatientContext _patientContext;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<string> _loadResults = new();

    public string WelcomeMessage => "Welcome to NewVistas Clinical Information System";
    public string Description => "Select a patient ID in the header above, then navigate to any clinical module.";

    public HomeViewModel(PatientContext patientContext)
    {
        _patientContext = patientContext;
    }

    private string PatientId => _patientContext.PatientId?.Trim() ?? string.Empty;
    private bool HasPatient => !string.IsNullOrWhiteSpace(PatientId);

    /// <summary>
    /// Bulk demo seeding is a server-side setup task, not a UI data path.
    ///
    /// This used to POST to eleven <c>demo/load</c> endpoints in turn, which is exactly the
    /// pattern the architecture forbids: an internal UI reaching for the WebServer. The
    /// WebServer already seeds demo data at startup (ExtremeLeeSickSeed and the dataset
    /// import), and the Blazor Home screen has no equivalent button, so the two clients now
    /// agree. Per-module demo data is still available from each module's own screen, where
    /// the seed is a single grain call.
    /// </summary>
    [RelayCommand]
    private Task LoadDemoData()
    {
        Error = null;
        LoadResults.Clear();
        LoadResults.Add("Demo data is seeded by the server at startup.");
        LoadResults.Add("For per-module demo data, use the “Load Demo Data” button on that module’s screen.");
        if (!HasPatient)
            LoadResults.Add("Select a patient ID in the header to browse the seeded records.");
        return Task.CompletedTask;
    }
}
