// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ShiftHandoffViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ShiftHandoffIndexEntry> _handoffs = new();
    [ObservableProperty] private NursingShiftHandoffState? _selectedHandoff;
    [ObservableProperty] private string? _actionMessage;

    // New handoff fields
    [ObservableProperty] private string _outgoingNurseId = string.Empty;
    [ObservableProperty] private string _outgoingNurseName = string.Empty;
    [ObservableProperty] private string _situation = string.Empty;
    [ObservableProperty] private string _background = string.Empty;
    [ObservableProperty] private string _assessment = string.Empty;
    [ObservableProperty] private string _recommendation = string.Empty;

    public ShiftHandoffViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<ShiftHandoffIndexEntry> list = await workflow.GetShiftHandoffsAsync();
        Handoffs.Clear();
        foreach (var h in list) Handoffs.Add(h);
        SelectedHandoff = null;
    }

    [RelayCommand]
    private async Task ViewDetail(ShiftHandoffIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedHandoff = await workflow.GetShiftHandoffAsync(entry.HandoffId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateHandoff()
    {
        IsLoading = true; Error = null;
        try
        {
            SbarPatientSummary sbar = new() { Situation = Situation, Background = Background, Assessment = Assessment, Recommendation = Recommendation };
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.CreateShiftHandoffAsync(NursingShift.Day, DateTime.UtcNow.Date, OutgoingNurseId, OutgoingNurseName, null, null, null, sbar, null, null);
            ActionMessage = $"Handoff created: {id}";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CompleteHandoff()
    {
        if (SelectedHandoff is null) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CompleteShiftHandoffAsync(SelectedHandoff.HandoffId);
            ActionMessage = "Handoff completed.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task AcknowledgeHandoff()
    {
        if (SelectedHandoff is null) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AcknowledgeShiftHandoffAsync(SelectedHandoff.HandoffId, "RN-INCOMING", "Incoming Nurse");
            ActionMessage = "Handoff acknowledged.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
