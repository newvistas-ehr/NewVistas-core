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
/// Patient sensitivity, break-the-glass access and the authorized-provider list.
///
/// This is the authorization side of the split: the Web tier establishes who you are,
/// and the grain layer decides what you may see. Every read and write here therefore
/// goes straight to <see cref="IPatientAccessControlGrain"/> — pushing these decisions
/// through HTTP would move authorization to the wrong tier and lose the grain call
/// context the silo's audit filter records.
/// </summary>
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

    public SecurityViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    private IPatientAccessControlGrain Access() =>
        Grains.GetGrain<IPatientAccessControlGrain>($"PAC:{PatientId}");

    protected override async Task LoadDataAsync()
    {
        PatientAccessControlState state = await Access().GetAccessControlAsync();

        IsSensitive = state.IsSensitive;
        SensitivityLevel = string.IsNullOrWhiteSpace(state.SensitivityLevel) ? "STANDARD" : state.SensitivityLevel;
        SensitivityCategories = string.Join(", ", state.SensitivityCategories);

        AuthorizedProviders.Clear();
        foreach (string p in state.AuthorizedProviderIds) AuthorizedProviders.Add(p);

        List<PatientAccessLog> log = await Access().GetAccessLogAsync();
        AccessLog.Clear();
        foreach (PatientAccessLog e in log.OrderByDescending(e => e.AccessDateTime))
        {
            AccessLog.Add(new AccessLogEntry(
                e.AccessDateTime.ToString("yyyy-MM-dd HH:mm"),
                string.IsNullOrWhiteSpace(e.UserName) ? e.UserId : e.UserName,
                e.AccessReason,
                e.WasBreakTheGlass,
                e.JustificationText));
        }
    }

    [RelayCommand]
    private async Task UpdateSensitivityAsync()
    {
        try
        {
            List<string> categories = (SensitivityCategories ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            await Access().SetSensitivityAsync(IsSensitive, SensitivityLevel, categories);
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task RecordBtgAsync()
    {
        try
        {
            // Break-the-glass is recorded, never refused — deterrence by visibility.
            await Access().RecordAccessAsync(
                BtgUserId,
                BtgUserName,
                "Break-the-glass access",
                wasBreakTheGlass: true,
                justificationText: BtgJustification);

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
            await Access().AddAuthorizedProviderAsync(NewProviderId.Trim());
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
            await Access().RemoveAuthorizedProviderAsync(SelectedProvider);
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public record AccessLogEntry(string DateTime, string User, string Reason, bool IsBtg, string? Justification);
