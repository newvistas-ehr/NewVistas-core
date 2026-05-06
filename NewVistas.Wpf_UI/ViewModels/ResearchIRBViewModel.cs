// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ResearchIRBViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<IrbStudyIndexEntry> _studies = new();
    [ObservableProperty] private IrbStudyIndexEntry? _selectedStudy;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ResearchIRBViewModel(ApiClient api, OrleansGrainService grains)
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
            var indexGrain = _grains.GetGrain<IResearchStudyIndexGrain>("IRB-STUDY-IDX");
            List<IrbStudyIndexEntry> list = await indexGrain.GetAllStudiesAsync();
            Studies.Clear();
            foreach (IrbStudyIndexEntry s in list) Studies.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
