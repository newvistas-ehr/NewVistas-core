// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class LabsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<LabResultSummary> _labResults = new();
    [ObservableProperty] private ObservableCollection<LabTestSummaryEntry> _labSummary = new();
    [ObservableProperty] private LabTestState? _selectedLab;

    // Order form
    [ObservableProperty] private bool _showOrderForm;
    [ObservableProperty] private string _testName = string.Empty;
    [ObservableProperty] private string _testCode = string.Empty;
    [ObservableProperty] private string _specimenType = "BLOOD";
    [ObservableProperty] private string _category = "CHEMISTRY";
    [ObservableProperty] private string _orderingProviderName = "Provider, Test";

    // Record result form
    [ObservableProperty] private bool _showResultForm;
    [ObservableProperty] private string _resultValue = string.Empty;
    [ObservableProperty] private string _resultUnit = string.Empty;

    public string[] SpecimenTypes { get; } = ["BLOOD", "URINE", "STOOL", "CSF", "TISSUE", "SPUTUM", "SWAB"];
    public string[] Categories { get; } = ["CHEMISTRY", "HEMATOLOGY", "MICROBIOLOGY", "URINALYSIS", "COAGULATION", "IMMUNOLOGY"];

    public LabsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);

        var results = await workflow.GetLabResultsAsync();
        LabResults.Clear();
        foreach (var r in results) LabResults.Add(r);

        var summary = await workflow.GetLabSummaryAsync();
        LabSummary.Clear();
        foreach (var s in summary) LabSummary.Add(s);
    }

    [RelayCommand]
    private async Task SelectLab(LabResultSummary lab)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedLab = await workflow.GetLabTestAsync(lab.LabTestId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleOrderForm() => ShowOrderForm = !ShowOrderForm;

    [RelayCommand]
    private async Task OrderLab()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(TestName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.OrderLabTestAsync(
                $"LAB-{Guid.NewGuid():N}", TestName,
                TestCode.Length > 0 ? TestCode : null,
                null, // orderId
                null, OrderingProviderName, // provider
                SpecimenType, Category);
            ShowOrderForm = false;
            TestName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RecordResult()
    {
        if (SelectedLab is null || !HasPatient || string.IsNullOrWhiteSpace(ResultValue)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordLabResultAsync(
                SelectedLab.LabTestId,
                DateTime.UtcNow,
                ResultValue,
                ResultUnit.Length > 0 ? ResultUnit : null,
                null, null, null); // referenceLow, referenceHigh, abnormalFlag
            ShowResultForm = false;
            ResultValue = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
