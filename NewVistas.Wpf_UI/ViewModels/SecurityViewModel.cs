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

public partial class SecurityViewModel : BasePatientViewModel
{
    // Sensitivity
    [ObservableProperty] private bool _isSensitive;
    [ObservableProperty] private string _sensitivityLevel = "STANDARD";
    [ObservableProperty] private string _sensitivityCategories = string.Empty;

    // Access Log
    [ObservableProperty] private ObservableCollection<AccessLogEntry> _accessLog = new();

    // Break the Glass
    [ObservableProperty] private string _btgUserId = string.Empty;
    [ObservableProperty] private string _btgUserName = string.Empty;
    [ObservableProperty] private string _btgJustification = string.Empty;

    // Authorized Providers
    [ObservableProperty] private ObservableCollection<string> _authorizedProviders = new();
    [ObservableProperty] private string _newProviderId = string.Empty;
    [ObservableProperty] private string? _selectedProvider;

    public string[] SensitivityLevels { get; } = ["STANDARD", "SENSITIVE", "HIGHLY_SENSITIVE"];

    public SecurityViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    private static string Esc(string id) => Uri.EscapeDataString(id.Trim());

    protected override async Task LoadDataAsync()
    {
        var access = await Api.Http.GetFromJsonAsync<PatientAccessInfo>(
            $"api/security/{Esc(PatientId)}/access", ApiClient.Json);
        if (access != null)
        {
            IsSensitive = access.IsSensitive;
            SensitivityLevel = access.SensitivityLevel ?? "STANDARD";
        }

        var log = await Api.Http.GetFromJsonAsync<List<AccessLogEntry>>(
            $"api/security/{Esc(PatientId)}/access/log", ApiClient.Json) ?? [];
        AccessLog.Clear();
        foreach (var e in log) AccessLog.Add(e);
    }

    [RelayCommand]
    private async Task UpdateSensitivityAsync()
    {
        try
        {
            await Api.Http.PostAsJsonAsync($"api/security/{Esc(PatientId)}/sensitivity", new
            {
                IsSensitive,
                SensitivityLevel,
                Categories = SensitivityCategories
            });
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task RecordBtgAsync()
    {
        try
        {
            await Api.Http.PostAsJsonAsync($"api/security/{Esc(PatientId)}/access/btg", new
            {
                UserId = BtgUserId,
                UserName = BtgUserName,
                Justification = BtgJustification
            });
            BtgJustification = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task AddProviderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProviderId)) return;
        try
        {
            await Api.Http.PostAsJsonAsync(
                $"api/security/{Esc(PatientId)}/providers/{Esc(NewProviderId)}", new { });
            NewProviderId = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task RemoveProviderAsync()
    {
        if (SelectedProvider == null) return;
        try
        {
            await Api.Http.DeleteAsync(
                $"api/security/{Esc(PatientId)}/providers/{Esc(SelectedProvider)}");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public record PatientAccessInfo(bool IsSensitive, string? SensitivityLevel);
public record AccessLogEntry(string DateTime, string User, string Reason, bool IsBtg, string? Justification);
