// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

/// <summary>
/// Base class for all CPRS chart-tab ViewModels.
/// Watches PatientContext and triggers LoadAsync when PatientId changes.
/// </summary>
public abstract partial class ChartTabViewModelBase : ObservableObject
{
    protected readonly ApiClient      Api;
    protected readonly PatientContext Context;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorText = string.Empty;

    protected ChartTabViewModelBase(ApiClient api, PatientContext context)
    {
        Api     = api;
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
