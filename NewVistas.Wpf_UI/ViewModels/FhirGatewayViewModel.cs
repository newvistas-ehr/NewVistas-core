// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Exercises the public FHIR endpoint. This is one of the few screens that legitimately
/// speaks HTTP: the point of the screen IS the outsider-facing surface, so it must go
/// through the same gateway an external FHIR client would. Every other ViewModel talks
/// to grains directly.
/// </summary>
public partial class FhirGatewayViewModel : BasePatientViewModel
{
    private readonly ApiClient _api;

    [ObservableProperty] private string _resourceType = "Patient";
    [ObservableProperty] private string _resultJson = string.Empty;
    [ObservableProperty] private string _capabilityStatement = string.Empty;

    public string[] ResourceTypes { get; } =
        ["Patient", "Condition", "AllergyIntolerance", "Observation", "MedicationRequest",
         "DiagnosticReport", "Encounter", "Appointment"];

    public FhirGatewayViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, patientContext) => _api = api;

    private static string Esc(string id) => Uri.EscapeDataString(id.Trim());

    protected override async Task LoadDataAsync()
    {
        string url = ResourceType == "Patient"
            ? $"api/fhir/Patient/{Esc(PatientId)}"
            : $"api/fhir/{ResourceType}?patient={Esc(PatientId)}";
        var response = await _api.Http.GetAsync(url);
        ResultJson = await response.Content.ReadAsStringAsync();
    }

    [RelayCommand]
    private async Task LoadCapabilityAsync()
    {
        try
        {
            var response = await _api.Http.GetAsync("api/fhir/metadata");
            CapabilityStatement = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
