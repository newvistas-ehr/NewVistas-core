// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

/// <summary>
/// Base class for all CPRS chart-tab ViewModels.
/// Watches PatientContext and triggers LoadAsync when PatientId changes.
/// </summary>
public abstract partial class ChartTabViewModelBase : ObservableObject
{
    protected readonly ChartDataService Data;
    protected readonly PatientContext Context;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorText = string.Empty;

    protected ChartTabViewModelBase(ChartDataService data, PatientContext context)
    {
        Data    = data;
        Context = context;

        Context.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PatientContext.PatientId))
                _ = ReloadAsync();
        };
    }

    protected string PatientId => Context.PatientId.Trim();

    public async Task ReloadAsync()
    {
        ErrorText = string.Empty;
        if (string.IsNullOrWhiteSpace(PatientId))
        {
            ClearData();
            return;
        }

        IsLoading = true;
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"Load error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected abstract Task LoadAsync();
    protected abstract void ClearData();
}
