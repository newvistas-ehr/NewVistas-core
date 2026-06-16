// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Base class for all patient-context ViewModels.
/// Provides shared patient ID (from PatientContext), loading state,
/// error display, and a Load command that derived classes implement.
///
/// Uses OrleansGrainService for direct grain access (data operations)
/// and ApiClient for authentication-only operations.
/// </summary>
public abstract partial class BasePatientViewModel : ObservableObject
{
    protected readonly OrleansGrainService Grains;
    protected readonly ApiClient Api;
    protected readonly PatientContext PatientContext;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _error;

    protected BasePatientViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
    {
        Grains = grains;
        Api = api;
        PatientContext = patientContext;
    }

    protected string PatientId => PatientContext.PatientId.Trim();

    protected bool HasPatient => !string.IsNullOrWhiteSpace(PatientId);

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync()
    {
        if (!CanLoad()) return;
        IsLoading = true;
        Error = null;
        try
        {
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected abstract Task LoadDataAsync();

    private bool CanLoad() => HasPatient && !IsLoading;
}
