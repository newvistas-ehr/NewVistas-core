// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ARAgingDashboardViewModel : BasePatientViewModel
{
    [ObservableProperty] private ARAgingReportState? _report;
    [ObservableProperty] private ObservableCollection<AgingBucketSummary> _buckets = new();
    [ObservableProperty] private RevenueCycleMetrics? _metrics;
    [ObservableProperty] private ObservableCollection<AgingAccountDetail> _accountDetails = new();
    [ObservableProperty] private string? _actionMessage;

    public ARAgingDashboardViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Report = await workflow.GenerateARAgingReportAsync(null, null);
        Buckets.Clear();
        AccountDetails.Clear();
        if (Report is not null)
        {
            foreach (var b in Report.BucketSummaries) Buckets.Add(b);
            foreach (var a in Report.AccountDetails) AccountDetails.Add(a);
            Metrics = Report.Metrics;
        }
    }

    [RelayCommand]
    private async Task RefreshReport()
    {
        IsLoading = true; Error = null;
        try
        {
            await LoadDataAsync();
            ActionMessage = "Report refreshed.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
