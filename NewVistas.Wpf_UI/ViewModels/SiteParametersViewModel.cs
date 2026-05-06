// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using NewVistas.Wpf_UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class SiteParametersViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    // Tab state
    [ObservableProperty] private int _selectedTab;

    // Shared feedback
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _successMessage;

    // Display Settings
    [ObservableProperty] private int _vitalsDisplayCount = 5;
    [ObservableProperty] private int _ordersDisplayCount = 5;
    [ObservableProperty] private int _notesDisplayCount = 10;

    // Custom Parameters
    [ObservableProperty] private ObservableCollection<ParameterEntry> _parameters = new();
    [ObservableProperty] private string _newParamName = string.Empty;
    [ObservableProperty] private string _newParamValue = string.Empty;

    public SiteParametersViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    private ISiteParametersGrain GetSiteGrain() =>
        _grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [RelayCommand]
    private async Task LoadDisplaySettingsAsync()
    {
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = GetSiteGrain();
            VitalsDisplayCount = await grain.GetVitalsDisplayCountAsync();
            OrdersDisplayCount = await grain.GetOrdersDisplayCountAsync();
            NotesDisplayCount = await grain.GetNotesDisplayCountAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveDisplaySettingsAsync()
    {
        if (VitalsDisplayCount < 1 || OrdersDisplayCount < 1 || NotesDisplayCount < 1)
        {
            Error = "Display counts must be at least 1.";
            return;
        }

        IsSaving = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = GetSiteGrain();
            await grain.SetVitalsDisplayCountAsync(VitalsDisplayCount);
            await grain.SetOrdersDisplayCountAsync(OrdersDisplayCount);
            await grain.SetNotesDisplayCountAsync(NotesDisplayCount);
            SuccessMessage = "Display settings saved successfully.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsSaving = false; }
    }

    [RelayCommand]
    private async Task LoadParametersAsync()
    {
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = GetSiteGrain();
            SiteParametersState state = await grain.GetParametersAsync();
            Dictionary<string, string> dict = state.Parameters;
            Parameters.Clear();
            foreach (KeyValuePair<string, string> kvp in dict.OrderBy(p => p.Key))
                Parameters.Add(new ParameterEntry { Name = kvp.Key, Value = kvp.Value });
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SetParameterAsync()
    {
        if (string.IsNullOrWhiteSpace(NewParamName) || string.IsNullOrWhiteSpace(NewParamValue))
        {
            Error = "Both parameter name and value are required.";
            return;
        }

        IsSaving = true; Error = null; SuccessMessage = null;
        try
        {
            var grain = GetSiteGrain();
            await grain.SetParameterAsync(NewParamName.Trim(), NewParamValue.Trim());
            SuccessMessage = $"Parameter '{NewParamName.Trim()}' set successfully.";
            NewParamName = string.Empty;
            NewParamValue = string.Empty;
            await LoadParametersAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsSaving = false; }
    }

    [RelayCommand]
    private async Task DeleteParameterAsync(string name)
    {
        Error = null; SuccessMessage = null;
        try
        {
            var grain = GetSiteGrain();
            await grain.SetParameterAsync(name, string.Empty);
            SuccessMessage = $"Parameter '{name}' deleted.";
            await LoadParametersAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class ParameterEntry : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
}
