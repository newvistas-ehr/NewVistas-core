// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ElectronicCaseReportingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<EcrCaseSummary> _cases = new();
    [ObservableProperty] private ObservableCollection<EcrTriggerSummary> _triggers = new();
    [ObservableProperty] private string _screenPatientId = string.Empty;

    public ElectronicCaseReportingViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var caseIndex = Grains.GetGrain<IEcrCaseIndexGrain>("ECR-CASE-INDEX");
        List<EcrCaseSummary> list = await caseIndex.GetAllCasesAsync();
        Cases.Clear();
        foreach (var c in list) Cases.Add(c);
    }

    [RelayCommand]
    private async Task LoadTriggers()
    {
        IsLoading = true; Error = null;
        try
        {
            var index = Grains.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
            List<EcrTriggerSummary> list = await index.GetAllTriggersAsync();
            Triggers.Clear();
            foreach (var t in list) Triggers.Add(t);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadActiveTriggers()
    {
        IsLoading = true; Error = null;
        try
        {
            var index = Grains.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
            List<EcrTriggerSummary> list = await index.GetActiveTriggersAsync();
            Triggers.Clear();
            foreach (var t in list) Triggers.Add(t);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
