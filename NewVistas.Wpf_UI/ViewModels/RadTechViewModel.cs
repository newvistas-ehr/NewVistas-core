// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class RadTechViewModel : BasePatientViewModel
{
    [ObservableProperty] private RadWorklistState? _worklist;
    [ObservableProperty] private RadExamTrackingState? _selectedExam;
    [ObservableProperty] private ObservableCollection<RadProtocolIndexEntry> _protocols = new();
    [ObservableProperty] private string _locationId = "RAD-MAIN";
    [ObservableProperty] private string? _actionMessage;

    public RadTechViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Worklist = await workflow.RefreshRadWorklistAsync(LocationId);
        SelectedExam = null;
    }

    [RelayCommand]
    private async Task ViewExam(RadWorklistItem item)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedExam = await workflow.GetRadExamTrackingAsync(item.RadiologyId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task MarkPrepped()
    {
        if (SelectedExam is null) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.MarkRadPatientPreppedAsync(SelectedExam.RadiologyId, null);
            ActionMessage = "Patient prepped.";
            SelectedExam = await workflow.GetRadExamTrackingAsync(SelectedExam.RadiologyId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task StartExam()
    {
        if (SelectedExam is null) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.StartRadExamAsync(SelectedExam.RadiologyId);
            ActionMessage = "Exam started.";
            SelectedExam = await workflow.GetRadExamTrackingAsync(SelectedExam.RadiologyId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CompleteExam()
    {
        if (SelectedExam is null) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CompleteRadExamAsync(SelectedExam.RadiologyId, 24, "Standard exam completed");
            ActionMessage = "Exam completed.";
            SelectedExam = await workflow.GetRadExamTrackingAsync(SelectedExam.RadiologyId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadProtocols()
    {
        try
        {
            var index = Grains.GetGrain<IRadProtocolIndexGrain>("RAD-PROTOCOL-INDEX");
            List<RadProtocolIndexEntry> list = await index.GetAllAsync();
            Protocols.Clear();
            foreach (var p in list) Protocols.Add(p);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
