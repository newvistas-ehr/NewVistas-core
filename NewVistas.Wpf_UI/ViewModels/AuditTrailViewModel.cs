// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class AuditTrailViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<AuditEventSummary> _events = new();
    [ObservableProperty] private AuditEventState? _selectedEvent;

    // Filters
    [ObservableProperty] private string _domainFilter = string.Empty;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;

    public string[] DomainOptions { get; } = [
        string.Empty, "ORDERS", "LABS", "PHARMACY", "NOTES", "VITALS",
        "ALLERGIES", "PROBLEMS", "CONSULTS", "ADT", "SCHEDULING", "PCE",
        "BCMA", "IMAGING", "DEMOGRAPHICS"
    ];

    public AuditTrailViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        string domain = DomainFilter.Length > 0 ? DomainFilter : string.Empty;

        List<AuditEventSummary> list;
        if (domain.Length > 0 || FromDate.HasValue || ToDate.HasValue)
        {
            list = await workflow.GetAuditEventsAsync(domain.Length > 0 ? domain : null, FromDate, ToDate, 200);
        }
        else
        {
            list = await workflow.GetRecentAuditEventsAsync(200);
        }

        Events.Clear();
        foreach (var e in list) Events.Add(e);
    }

    [RelayCommand]
    private async Task SelectEvent(AuditEventSummary summary)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedEvent = await workflow.GetAuditEventAsync(summary.EventId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
