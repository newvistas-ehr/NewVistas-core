// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class TopMatchingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<TopMatchIndexEntry> _matches = new();
    [ObservableProperty] private TopMatchingState? _selectedMatch;
    [ObservableProperty] private string? _actionMessage;

    public TopMatchingViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<TopMatchIndexEntry> list = await workflow.GetTopMatchRecordsAsync();
        Matches.Clear();
        foreach (var m in list) Matches.Add(m);
        SelectedMatch = null;
    }

    [RelayCommand]
    private async Task ViewDetail(TopMatchIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedMatch = await workflow.GetTopMatchRecordAsync(entry.MatchId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
