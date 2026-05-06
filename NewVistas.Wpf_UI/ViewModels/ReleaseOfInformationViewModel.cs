// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ReleaseOfInformationViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<ROIRequestIndexEntry> _requests = new();
    [ObservableProperty] private ROIRequestIndexEntry? _selectedRequest;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ReleaseOfInformationViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true; Error = null;
        try
        {
            var indexGrain = _grains.GetGrain<IROIRequestIndexGrain>("ROI-REQUEST-IDX");
            List<ROIRequestIndexEntry> list = await indexGrain.GetAllRequestsAsync();
            Requests.Clear();
            foreach (ROIRequestIndexEntry r in list) Requests.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
