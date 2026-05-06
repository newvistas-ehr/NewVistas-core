// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class SubstanceAbuseTreatmentViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<SATreatmentEpisodeIndexEntry> _episodes = new();
    [ObservableProperty] private ObservableCollection<SAVisitIndexEntry> _visits = new();
    [ObservableProperty] private string _selectedEpisodeId = string.Empty;

    public SubstanceAbuseTreatmentViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<SATreatmentEpisodeIndexEntry> list = await workflow.GetSATreatmentEpisodesAsync();
        Episodes.Clear();
        foreach (SATreatmentEpisodeIndexEntry e in list) Episodes.Add(e);

        // Auto-load visits for active episode
        SATreatmentEpisodeIndexEntry? active = list.FirstOrDefault(
            e => e.Status == SATreatmentStatus.Active || e.Status == SATreatmentStatus.Reopened);
        if (active != null)
        {
            SelectedEpisodeId = active.EpisodeId;
            List<SAVisitIndexEntry> visitList = await workflow.GetSAVisitsAsync(active.EpisodeId);
            Visits.Clear();
            foreach (SAVisitIndexEntry v in visitList) Visits.Add(v);
        }
    }
}
